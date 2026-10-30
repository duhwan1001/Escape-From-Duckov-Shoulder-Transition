using System;
using System.Collections.Generic;
using System.Linq;
using EscapeFromDuckovCoopMod;
using LiteNetLib.Utils;
using UnityEngine;

namespace ShoulderTransition
{
    /// <summary>
    /// 코옵(EscapeFromDuckovCoopMod) 연동: 각자 "내 왼손 상태"를 보내고, 받는 쪽은 **그 플레이어의 원격 캐릭터만** 뒤집는다.
    ///
    ///   클라이언트 ──(내 상태: mirror, mode)──▶ 호스트
    ///   호스트     ──(전원 상태 표: id→상태)──▶ 모든 클라이언트   (호스트 자신의 상태도 표에 포함)
    ///
    /// id 는 호스트가 부여하는 플레이어 id(NetService.GetPlayerId) 이며, 코옵 모드가 각 클라이언트에 원격 캐릭터를 만들 때
    /// ModApiEvents.PlayerSpawned(cmc, id, isLocal:false) 로 같은 id 를 알려주므로 그걸로 캐릭터를 찾는다.
    ///
    /// 이 클래스의 메서드 안에서만 코옵 API 타입을 쓰므로(Mono 는 메서드 JIT 시점에 어셈블리를 찾음)
    /// 코옵 모드가 없으면 <see cref="Available"/> 가 false 를 돌려주고 아무 것도 건드리지 않는다.
    /// 호스트에 이 모드가 없으면 중계가 안 되어 클라이언트끼리는 동기화되지 않는다.
    /// </summary>
    internal static class CoopSync
    {
        public const string Channel = "ShoulderTransition.Hand";
        private const byte Version = 1;
        private const byte KindClientState = 0;   // client → host : [ver][kind][mirror][mode]
        private const byte KindHostTable = 1;     // host → clients: [ver][kind][count][(id, mirror, mode)...]

        public struct HandState { public bool mirror; public MirrorMode mode; }

        /// <summary>ModBehaviour 가 넣어주는 "내 현재 상태"</summary>
        public static Func<HandState> LocalState = () => new HandState();

        private static IDisposable _sub;
        private static bool _hooked;
        private static readonly Dictionary<string, HandState> _table = new Dictionary<string, HandState>(StringComparer.Ordinal);
        private static readonly Dictionary<string, CharacterMainControl> _remote = new Dictionary<string, CharacterMainControl>(StringComparer.Ordinal);

        public static bool Hooked { get { return _hooked; } }

        /// <summary>코옵 모드 + API 어셈블리가 로드돼 있는가 (타입을 건드리지 않고 이름만 확인)</summary>
        public static bool Available
        {
            get
            {
                bool api = false, coop = false;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var n = a.GetName().Name;
                    if (n == "EscapeFromDuckovModApi") api = true;
                    else if (n == "EscapeFromDuckovCoopMod") coop = true;
                }
                return api && coop;
            }
        }

        // ───────────────────────── 연결/해제 ─────────────────────────
        public static void Hook()
        {
            if (_hooked) return;
            _sub = ModNetworkApi.RegisterHandler(Channel, OnMessage);
            ModApiEvents.PlayerSpawned += OnPlayerSpawned;
            ModNetworkApi.PeerConnected += OnPeerConnected;
            ModNetworkApi.PeerDisconnected += OnPeerDisconnected;
            _hooked = true;
            ModBehaviour.Log("coop sync hooked (channel " + Channel + ")");
            SendLocalState(); // 이미 접속 중이었다면 바로 알림 (네트워크 미시작이면 무시됨)
        }

        public static void Unhook()
        {
            if (!_hooked) return;
            try
            {
                // 나가기 전에 "오른손" 으로 되돌려 달라고 알림
                var s = new HandState { mirror = false, mode = MirrorMode.Model };
                SendState(s);
            }
            catch { }
            try { if (_sub != null) _sub.Dispose(); } catch { }
            _sub = null;
            try { ModApiEvents.PlayerSpawned -= OnPlayerSpawned; } catch { }
            try { ModNetworkApi.PeerConnected -= OnPeerConnected; } catch { }
            try { ModNetworkApi.PeerDisconnected -= OnPeerDisconnected; } catch { }
            _table.Clear();
            _remote.Clear();
            RemoteMirror.Clear();
            _hooked = false;
            ModBehaviour.Log("coop sync unhooked");
        }

        /// <summary>디버그: 코옵 패널 없이 LAN 호스트 시작/정지 (네트워크 경로 검증용). 코옵 패널의 "호스트 생성"과 같은 호출.</summary>
        public static void DebugToggleHost()
        {
            var svc = NetService.Instance;
            if (svc == null) { ModBehaviour.Log("debug host: NetService 없음"); return; }
            if (svc.NetworkStarted) { svc.StopNetwork(); ModBehaviour.Log("debug host: network stopped"); return; }
            svc.StartNetwork(isServer: true);
            ModBehaviour.Log("debug host: StartNetwork(host) → started=" + svc.NetworkStarted + " isServer=" + svc.IsServer + " id=" + svc.GetPlayerId(null));
        }

        /// <summary>매 LateUpdate: 원격 캐릭터 시각 상태 유지(모델 재생성 대응), 죽은 참조 정리</summary>
        public static void Tick()
        {
            if (!_hooked) return;
            RemoteMirror.Tick();
        }

        // ───────────────────────── 보내기 ─────────────────────────
        public static void SendLocalState()
        {
            if (!_hooked) return;
            try { SendState(LocalState()); }
            catch (Exception e) { ModBehaviour.Log("coop send failed: " + e.GetType().Name + " " + e.Message); }
        }

        private static void SendState(HandState s)
        {
            var svc = NetService.Instance;
            if (svc == null || !svc.NetworkStarted) return;

            if (svc.IsServer)
            {
                // 호스트: 내 항목을 표에 넣고 전체 표 방송
                _table[svc.GetPlayerId(null)] = s;
                BroadcastTable("local change");
            }
            else
            {
                bool ok = ModNetworkApi.SendToServer(Channel, w =>
                {
                    w.Put(Version); w.Put(KindClientState); w.Put(s.mirror); w.Put((byte)s.mode);
                });
                ModBehaviour.Log("coop → host: mirror=" + s.mirror + " mode=" + s.mode + (ok ? "" : " (not sent)"));
            }
        }

        private static void BroadcastTable(string why)
        {
            int peers = ModNetworkApi.Broadcast(Channel, w => WriteTable(w), includeServer: false);
            ModBehaviour.Log("coop host → " + peers + " client(s): table " + Describe() + " [" + why + "]");
        }

        private static void WriteTable(NetDataWriter w)
        {
            w.Put(Version); w.Put(KindHostTable);
            w.Put((ushort)_table.Count);
            foreach (var kv in _table) { w.Put(kv.Key); w.Put(kv.Value.mirror); w.Put((byte)kv.Value.mode); }
        }

        private static string Describe()
        {
            return "{" + string.Join(", ", _table.Select(kv => kv.Key + ":" + (kv.Value.mirror ? "L" : "R") + "/" + kv.Value.mode)) + "}";
        }

        // ───────────────────────── 받기 ─────────────────────────
        private static void OnMessage(ModMessageContext ctx)
        {
            try
            {
                var r = new NetDataReader(ctx.Payload.ToArray());
                byte ver = r.GetByte();
                if (ver != Version) { ModBehaviour.Log("coop: unknown payload version " + ver); return; }
                byte kind = r.GetByte();
                var svc = NetService.Instance;

                if (kind == KindClientState)
                {
                    if (svc == null || !svc.IsServer) return;            // 클라이언트 상태는 호스트만 처리
                    var s = new HandState { mirror = r.GetBool(), mode = (MirrorMode)r.GetByte() };
                    string id = svc.GetPlayerId(ctx.Sender);              // 호스트가 부여한 id = 모두가 PlayerSpawned 로 받는 id
                    _table[id] = s;
                    ModBehaviour.Log("coop host ← " + id + ": mirror=" + s.mirror + " mode=" + s.mode);
                    ApplyAll();
                    BroadcastTable("relay");
                }
                else if (kind == KindHostTable)
                {
                    if (svc != null && svc.IsServer) return;              // 호스트는 자기 표를 이미 가짐
                    _table.Clear();
                    int n = r.GetUShort();
                    for (int i = 0; i < n; i++)
                    {
                        string id = r.GetString();
                        _table[id] = new HandState { mirror = r.GetBool(), mode = (MirrorMode)r.GetByte() };
                    }
                    ModBehaviour.Log("coop ← host table " + Describe());
                    ApplyAll();
                }
            }
            catch (Exception e) { ModBehaviour.Log("coop message failed: " + e.GetType().Name + " " + e.Message); }
        }

        private static void OnPlayerSpawned(CharacterMainControl cmc, string id, bool isLocal)
        {
            try
            {
                if (isLocal) { SendLocalState(); return; }          // 내 캐릭터 준비됨 → 내 상태 알림
                if (cmc == null || string.IsNullOrEmpty(id)) return;
                _remote[id] = cmc;
                ModBehaviour.Log("coop remote player spawned: " + id + " (" + cmc.name + ")");
                ApplyAll();
            }
            catch (Exception e) { ModBehaviour.Log("coop spawn handler failed: " + e.Message); }
        }

        private static void OnPeerConnected(CoopPeer peer)
        {
            try
            {
                var svc = NetService.Instance;
                if (svc == null || !svc.IsServer) return;
                // 새로 들어온 클라이언트에게 현재 표를 보내 준다 (그쪽 원격 캐릭터가 아직 없으면 그쪽에서 보관했다가 적용)
                ModNetworkApi.SendToClient(peer, Channel, w => WriteTable(w), echoToServer: false);
                ModBehaviour.Log("coop host → new peer: table " + Describe());
            }
            catch (Exception e) { ModBehaviour.Log("coop peer handler failed: " + e.Message); }
        }

        private static void OnPeerDisconnected(CoopPeer peer)
        {
            try
            {
                var svc = NetService.Instance;
                if (svc == null || !svc.IsServer) return;
                string id = svc.GetPlayerId(peer);
                if (_table.Remove(id)) { ApplyAll(); BroadcastTable("peer left"); }
            }
            catch { }
        }

        /// <summary>표에 있는 상태를 알고 있는 원격 캐릭터에 반영. 표에 없는 원격은 오른손으로.</summary>
        private static void ApplyAll()
        {
            foreach (var id in _remote.Keys.ToList())
            {
                var cmc = _remote[id];
                if (cmc == null) { _remote.Remove(id); RemoteMirror.Forget(id); continue; }
                HandState s;
                if (_table.TryGetValue(id, out s)) RemoteMirror.Set(id, cmc, s.mirror, s.mode);
                else RemoteMirror.Set(id, cmc, false, MirrorMode.Model);
            }
        }
    }

    /// <summary>원격 플레이어 캐릭터들의 미러 시각 상태 (Model: 스케일 반전, Socket: Harmony 패치의 Extra 집합).</summary>
    internal static class RemoteMirror
    {
        private sealed class Entry
        {
            public CharacterMainControl cmc;
            public bool mirror;
            public MirrorMode mode;
            public Transform scaledModel;        // Model 모드로 스케일을 뒤집어 둔 트랜스폼
            public CharacterModel socketModel;   // Socket 모드로 Extra 에 넣어 둔 모델
        }

        private static readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

        public static void Set(string id, CharacterMainControl cmc, bool mirror, MirrorMode mode)
        {
            Entry e;
            if (!_entries.TryGetValue(id, out e)) { e = new Entry(); _entries[id] = e; }
            if (e.cmc == cmc && e.mirror == mirror && e.mode == mode) return;
            Undo(e);
            e.cmc = cmc; e.mirror = mirror; e.mode = mode;
            Apply(e);
            ModBehaviour.Log("coop remote '" + id + "' → " + (mirror ? "LEFT" : "RIGHT") + " (" + mode + ")");
        }

        public static void Forget(string id)
        {
            Entry e;
            if (_entries.TryGetValue(id, out e)) { Undo(e); _entries.Remove(id); }
        }

        public static void Clear()
        {
            foreach (var e in _entries.Values) Undo(e);
            _entries.Clear();
            SocketMirrorPatch.ExtraModels.Clear();
            SocketMirrorPatch.RemoveIfUnused();
        }

        /// <summary>모델이 재생성(장비 변경 등)되면 다시 적용, 캐릭터가 사라지면 정리</summary>
        public static void Tick()
        {
            if (_entries.Count == 0) return;
            foreach (var id in _entries.Keys.ToList())
            {
                var e = _entries[id];
                if (e.cmc == null) { Undo(e); _entries.Remove(id); continue; }
                if (!e.mirror) continue;
                var model = e.cmc.characterModel;
                bool stale = e.mode == MirrorMode.Model ? (e.scaledModel == null || (model != null && e.scaledModel != model.transform))
                                                       : (e.socketModel == null || e.socketModel != model);
                if (stale) { Undo(e); Apply(e); }
            }
        }

        private static void Apply(Entry e)
        {
            if (!e.mirror || e.cmc == null || e.cmc.characterModel == null) return;
            var model = e.cmc.characterModel;
            if (e.mode == MirrorMode.Model)
            {
                var t = model.transform; var s = t.localScale;
                if (s.x > 0f) { s.x = -s.x; t.localScale = s; }
                e.scaledModel = t;
            }
            else
            {
                SocketMirrorPatch.ExtraModels.Add(model);
                e.socketModel = model;
                try { SocketMirrorPatch.Apply(); } catch (Exception ex) { ModBehaviour.Log("coop socket patch failed: " + ex.Message); }
            }
        }

        private static void Undo(Entry e)
        {
            if (e.scaledModel != null)
            {
                var s = e.scaledModel.localScale;
                if (s.x < 0f) { s.x = -s.x; e.scaledModel.localScale = s; }
            }
            e.scaledModel = null;
            if (e.socketModel != null) SocketMirrorPatch.ExtraModels.Remove(e.socketModel);
            e.socketModel = null;
            SocketMirrorPatch.RemoveIfUnused();
        }
    }
}
