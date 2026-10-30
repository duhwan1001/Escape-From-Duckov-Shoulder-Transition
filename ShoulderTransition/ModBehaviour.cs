using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Duckov;
using UnityEngine;

namespace ShoulderTransition
{
    public enum MirrorMode { Model, Socket }

    /// <summary>
    /// 토글 효과음 (모두 게임 내장 FMOD 이벤트 재활용, F11 덤프로 확인한 경로)
    ///   GunReload    : SFX/Combat/Gun/Reload/{든 총의 reloadKey}_end  — 장전 마무리 철컥 (총기별로 다름)
    ///   GunUnload    : SFX/Combat/Gun/unload                           — 탄창 빼는 철컥
    ///   EquipGunLight: SFX/Item/equip_gun_light                        — 가벼운 총 장비 슥슥
    ///   EquipGunHeavy: SFX/Item/equip_gun_heavy                        — 무거운 총 장비 슥슥
    ///   EquipDefault : SFX/Item/equip_default                          — 일반 장비 착용 슥슥
    ///   UiClick      : UI/click
    /// </summary>
    public enum ToggleSound { GunReload, GunUnload, EquipGunLight, EquipGunHeavy, EquipDefault, UiClick, None }

    /// <summary>
    /// 플레이어 오리가 총을 왼손(왼쪽)으로 들게 하는 모드.
    ///
    /// mode = Model  : CharacterModel 트랜스폼의 localScale.x 를 -1 로 만들어 캐릭터 모델 전체를 거울상으로.
    ///                 날개·총·총구(Muzzle)가 한꺼번에 왼쪽으로 가고, 탄은 거울상 총구 위치에서 나간다. Harmony 불필요.
    /// mode = Socket : CharacterModel.LateUpdate 에 Harmony Prefix/Postfix 를 걸어 RightHandSocket(+MeleeWeaponSocket)만
    ///                 모델 루트 기준 YZ 평면으로 미러링. 몸/날개는 그대로, 총만 왼쪽.
    ///
    /// 두 모드 모두 LevelManager.MainCharacter(로컬 플레이어)에만 적용 → 코옵 원격 플레이어/NPC 영향 없음.
    ///
    /// 설정 UI: <c>config</c> 필드가 public 이고 [Serializable] 이라 SlimeNull.DuckovModSettings 가 설치돼 있으면
    /// 게임 옵션 창에 자동으로 항목이 생긴다(의존성 없음). 없으면 dll 옆 config.json 을 직접 편집.
    /// </summary>
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        [Serializable]
        public class Config
        {
            [InspectorName("Mirror ON (왼손 사격)")]
            [Tooltip("Hold the gun on the left side. / 총을 왼쪽으로. 토글 키로도 전환됩니다.")]
            public bool mirror = true;

            [InspectorName("Mode (방식)")]
            [Tooltip("Model: whole duck mirrored, hands follow the gun. Socket: only the weapon socket moves, face/gear stay.\nModel: 오리 전체 거울상(손도 따라감). Socket: 총만 왼쪽, 얼굴/장비 유지.")]
            public MirrorMode mode = MirrorMode.Model;

            [Header("Keys / 키")]
            [InspectorName("Toggle key (토글 키)")]
            public KeyCode toggleKey = KeyCode.LeftAlt;

            [InspectorName("Mode switch key (모드 전환 키, 기본 없음)")]
            [Tooltip("Optional hotkey to flip Model/Socket. Leave None and use the Mode dropdown instead.\n선택 핫키. None이면 비활성, 모드는 위 드롭다운으로 변경.")]
            public KeyCode modeKey = KeyCode.None;

            [Header("Sound / 소리")]
            [InspectorName("Toggle sound (전환 효과음)")]
            [Tooltip("GunReload: reload-finish click of the held gun. GunUnload: magazine-out click. EquipGunLight/Heavy, EquipDefault: equip swoosh. UiClick: UI click.\nGunReload: 든 총의 장전 마무리 철컥. GunUnload: 탄 빼기 철컥. EquipGunLight/Heavy·EquipDefault: 장비 착용 슥슥. UiClick: UI 클릭음.")]
            public ToggleSound toggleSound = ToggleSound.GunReload;

            [Header("Co-op")]
            [InspectorName("Co-op sync (상대 토글 반영)")]
            [Tooltip("Show other players' handedness when they toggle (they also need this mod; the host must have it to relay).\n다른 플레이어가 토글하면 내 화면에서도 그 사람이 바뀜. 상대도 이 모드가 필요하고, 중계를 위해 호스트에도 있어야 함.")]
            public bool coopSync = true;

            [Header("Socket mode options")]
            [InspectorName("Socket: mirror gun mesh (총 메쉬도 거울상)")]
            public bool socketMirrorMesh = false;

            [InspectorName("Socket: mirror melee socket (근접무기 소켓도)")]
            public bool socketMirrorMelee = true;

            [Header("Debug keys")]
            [InspectorName("Hierarchy dump key")]
            public KeyCode dumpKey = KeyCode.F9;

            [InspectorName("Close-up snapshot key")]
            public KeyCode snapKey = KeyCode.F10;

            [InspectorName("FMOD event list dump key")]
            public KeyCode fmodDumpKey = KeyCode.F11;

            [InspectorName("Remote-mirror test key (nearest NPC)")]
            [Tooltip("Debug: applies the co-op remote mirror to the nearest non-player character to preview how others will look.")]
            public KeyCode remoteTestKey = KeyCode.None;

            public Config Clone() { return (Config)MemberwiseClone(); }
        }

        public const string Tag = "[ShoulderTransition] ";

        /// <summary>public + [Serializable] → SlimeNull.DuckovModSettings 가 옵션 창에 그룹으로 표시</summary>
        [InspectorName("ShoulderTransition")]
        public Config config = new Config();

        private Config _applied;          // 마지막으로 반영한 설정 스냅샷 (UI/핫키 어느 쪽에서 바뀌어도 여기서 감지)
        private string _cfgPath;
        private int _shotLogBudget = 5;

        // ───────────────────────── 로그 (코옵 모드가 Debug.Log INFO 를 삼키므로 파일에도 기록) ─────────────────────────
        private static string _logPath;
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public static void Log(string msg)
        {
            Debug.Log(Tag + msg);
            try
            {
                if (_logPath == null) _logPath = Path.Combine(ModDir(), "ShoulderTransition.log");
                File.AppendAllText(_logPath, DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg + Environment.NewLine, Utf8NoBom);
            }
            catch { }
        }

        // ───────────────────────── Unity 생명주기 ─────────────────────────
        private void Awake()
        {
            try { _logPath = Path.Combine(ModDir(), "ShoulderTransition.log"); File.WriteAllText(_logPath, "", Utf8NoBom); } catch { }

            LoadConfig();
            _applied = config.Clone();
            Log("v" + typeof(ModBehaviour).Assembly.GetName().Version + " loaded. mirror=" + config.mirror + " mode=" + config.mode +
                " toggleKey=" + config.toggleKey + " modeKey=" + config.modeKey + " sound=" + config.toggleSound +
                " | game " + Application.version + " unity " + Application.unityVersion);
            ItemAgent_Gun.OnMainCharacterShootEvent += OnMainCharacterShoot;
            SocketMirrorPatch.LocalWantsPatch = () => config.mode == MirrorMode.Socket;
            CoopSync.LocalState = () => new CoopSync.HandState { mirror = config.mirror, mode = config.mode };
            ApplyMode();
            TryHookCoop();
        }

        // ───────────────────────── 코옵 연동 (선택) ─────────────────────────
        private float _nextCoopCheck;

        /// <summary>코옵 모드가 로드돼 있으면 연결. 아니면 5초마다 재확인(로드 오더가 뒤인 경우 대비).</summary>
        private void TryHookCoop()
        {
            _nextCoopCheck = Time.unscaledTime + 5f;
            if (!config.coopSync || CoopSync.Hooked) return;
            if (!CoopSync.Available) return;
            try { CoopSync.Hook(); }
            catch (Exception e) { Log("coop hook failed: " + e.GetType().Name + " " + e.Message); _nextCoopCheck = float.MaxValue; }
        }

        private void UnhookCoop()
        {
            if (!CoopSync.Hooked) return;
            try { CoopSync.Unhook(); }
            catch (Exception e) { Log("coop unhook failed: " + e.Message); }
        }

        private void NotifyCoop()
        {
            if (!CoopSync.Hooked) return;
            try { CoopSync.SendLocalState(); }
            catch (Exception e) { Log("coop notify failed: " + e.Message); }
        }

        private static bool _remoteTestOn;

        /// <summary>디버그: 가장 가까운 비플레이어 캐릭터에 원격 미러를 적용해 "남이 보는 모습" 미리보기</summary>
        private void RemoteTest()
        {
            var main = CharacterMainControl.Main;
            CharacterMainControl best = null; float bestD = float.MaxValue;
            foreach (var c in FindObjectsOfType<CharacterMainControl>())
            {
                if (c == null || c == main || c.characterModel == null) continue;
                float d = main ? (c.transform.position - main.transform.position).sqrMagnitude : 0f;
                if (d < bestD) { bestD = d; best = c; }
            }
            if (best == null) { Log("remote test: no other character"); return; }
            _remoteTestOn = !_remoteTestOn;
            RemoteMirror.Set("debug", best, _remoteTestOn, config.mode);
            Log("remote test on '" + best.name + "' mirror=" + _remoteTestOn + " mode=" + config.mode);
        }

        private void Update()
        {
            // 핫키는 설정값만 바꾼다. 실제 반영은 아래 DetectConfigChanges 에서 (설정 UI에서 바꿔도 같은 경로).
            if (KeyDown(config.toggleKey)) config.mirror = !config.mirror;
            if (KeyDown(config.modeKey)) config.mode = config.mode == MirrorMode.Model ? MirrorMode.Socket : MirrorMode.Model;
            if (KeyDown(config.dumpKey)) DumpHierarchy();
            if (KeyDown(config.snapKey)) Snapshot();
            if (KeyDown(config.fmodDumpKey)) DumpFmodEvents();
            if (KeyDown(config.remoteTestKey))
            {
                // Ctrl + 키: 코옵 LAN 호스트 시작/정지 (디버그), 그냥 키: 가까운 NPC 원격 미러 미리보기
                if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && CoopSync.Hooked)
                {
                    try { CoopSync.DebugToggleHost(); } catch (Exception e) { Log("debug host failed: " + e); }
                }
                else RemoteTest();
            }

            DetectConfigChanges();

            if (config.coopSync && !CoopSync.Hooked && Time.unscaledTime >= _nextCoopCheck) TryHookCoop();
        }

        /// <summary>KeyCode.None 이면 미할당(항상 false)</summary>
        private static bool KeyDown(KeyCode k) { return k != KeyCode.None && Input.GetKeyDown(k); }

        private void LateUpdate()
        {
            // Model 모드는 여기서 매 프레임 상태만 확인 (모델이 재생성되면 다시 적용, 꺼지면 원복)
            ModelMirror.Tick(config.mirror && config.mode == MirrorMode.Model);
            RemoteMirror.Tick();
        }

        protected override void OnBeforeDeactivate() { Cleanup(); }
        private void OnDestroy() { Cleanup(); }

        private void Cleanup()
        {
            ItemAgent_Gun.OnMainCharacterShootEvent -= OnMainCharacterShoot;
            UnhookCoop();
            RemoteMirror.Clear();
            ModelMirror.Restore();
            SocketMirrorPatch.Remove();
        }

        // ───────────────────────── 설정 변경 감지 ─────────────────────────
        private void DetectConfigChanges()
        {
            if (config == null) config = new Config();
            bool changed = false;

            bool handChanged = false;
            if (config.mirror != _applied.mirror)
            {
                changed = handChanged = true;
                _shotLogBudget = 5;
                Log(config.mirror ? "mirror ON" : "mirror OFF");
                PlayToggleSound();
            }
            if (config.mode != _applied.mode)
            {
                changed = handChanged = true;
                _shotLogBudget = 5;
                Log("mode = " + config.mode);
                ApplyMode();
            }
            if (handChanged) NotifyCoop();

            if (config.coopSync != _applied.coopSync)
            {
                changed = true;
                Log("coopSync = " + config.coopSync);
                if (config.coopSync) TryHookCoop(); else UnhookCoop();
            }
            if (!changed)
            {
                changed = config.toggleKey != _applied.toggleKey || config.modeKey != _applied.modeKey ||
                          config.toggleSound != _applied.toggleSound || config.socketMirrorMesh != _applied.socketMirrorMesh ||
                          config.socketMirrorMelee != _applied.socketMirrorMelee || config.dumpKey != _applied.dumpKey ||
                          config.snapKey != _applied.snapKey || config.fmodDumpKey != _applied.fmodDumpKey ||
                          config.remoteTestKey != _applied.remoteTestKey;
            }
            if (changed)
            {
                _applied = config.Clone();
                SaveConfig();
            }
        }

        private void ApplyMode()
        {
            if (config.mode == MirrorMode.Socket)
            {
                ModelMirror.Restore();
                SocketMirrorPatch.IsEnabled = () => config.mirror;
                SocketMirrorPatch.MirrorMesh = () => config.socketMirrorMesh;
                SocketMirrorPatch.MirrorMelee = () => config.socketMirrorMelee;
                if (!TryApplySocketPatch())
                {
                    Log("socket 모드를 켤 수 없어 Model 모드로 되돌립니다.");
                    config.mode = MirrorMode.Model;
                    _applied.mode = MirrorMode.Model;
                    SaveConfig();
                }
            }
            else
            {
                SocketMirrorPatch.RemoveIfUnused();
            }
        }

        /// <summary>Harmony 타입은 이 메서드 안에서만 JIT 되므로 0Harmony.dll 이 없어도 Model 모드는 동작한다.</summary>
        private static bool TryApplySocketPatch()
        {
            try { return SocketMirrorPatch.Apply(); }
            catch (Exception e)
            {
                Log("ERROR Harmony(0Harmony.dll) 로드 실패: " + e.GetType().Name + " " + e.Message);
                return false;
            }
        }

        // ───────────────────────── 효과음 (게임 내장 FMOD 이벤트 재활용) ─────────────────────────
        private void PlayToggleSound()
        {
            try
            {
                string ev = null; GameObject at = null;
                switch (config.toggleSound)
                {
                    case ToggleSound.None: return;
                    case ToggleSound.UiClick: ev = "UI/click"; break;
                    case ToggleSound.GunUnload: ev = "SFX/Combat/Gun/unload"; break;
                    case ToggleSound.EquipGunLight: ev = "SFX/Item/equip_gun_light"; break;
                    case ToggleSound.EquipGunHeavy: ev = "SFX/Item/equip_gun_heavy"; break;
                    case ToggleSound.EquipDefault: ev = "SFX/Item/equip_default"; break;
                    case ToggleSound.GunReload:
                        {
                            var main = CharacterMainControl.Main;
                            var gun = main != null ? main.CurrentHoldItemAgent as ItemAgent_Gun : null;
                            if (gun != null && gun.GunItemSetting != null && gun.gameObject.activeInHierarchy)
                            {
                                // 게임이 장전 완료 시 재생하는 것과 같은 이벤트 (총기별 reloadKey)
                                ev = "SFX/Combat/Gun/Reload/" + gun.GunItemSetting.reloadKey.ToLowerInvariant() + "_end";
                                at = gun.gameObject;
                            }
                            else ev = "SFX/Combat/Gun/unload";
                            break;
                        }
                }
                if (!AudioManager.Initialized) { Log("sound skipped (FMOD not initialized): " + ev); return; }
                var inst = at != null ? AudioManager.Post(ev, at) : AudioManager.Post(ev);
                Log("sound '" + ev + "' posted, valid=" + (inst.HasValue && inst.Value.isValid()));
            }
            catch (Exception e) { Log("sound failed: " + e.GetType().Name + " " + e.Message); }
        }

        /// <summary>F11: 로드된 FMOD 뱅크의 모든 이벤트 경로를 fmod_events.txt 로 (재활용할 효과음 고르기용)</summary>
        private void DumpFmodEvents()
        {
            try
            {
                var sb = new StringBuilder();
                var sys = FMODUnity.RuntimeManager.StudioSystem;
                FMOD.Studio.Bank[] banks;
                sys.getBankList(out banks);
                int total = 0;
                foreach (var bank in banks)
                {
                    string bankPath; bank.getPath(out bankPath);
                    FMOD.Studio.EventDescription[] events;
                    if (bank.getEventList(out events) != FMOD.RESULT.OK) continue;
                    sb.AppendLine("# " + bankPath + " (" + events.Length + ")");
                    var paths = new List<string>();
                    foreach (var ev in events) { string p; if (ev.getPath(out p) == FMOD.RESULT.OK) paths.Add(p); }
                    paths.Sort(StringComparer.Ordinal);
                    foreach (var p in paths) sb.AppendLine(p);
                    total += paths.Count;
                }
                var file = Path.Combine(ModDir(), "fmod_events.txt");
                File.WriteAllText(file, sb.ToString(), Utf8NoBom);
                Log("fmod events dumped: " + total + " → " + file);
            }
            catch (Exception e) { Log("fmod dump failed: " + e); }
        }

        // ───────────────────────── 자가 검증: 발사 시 총구/총알 위치 ─────────────────────────
        private void OnMainCharacterShoot(ItemAgent_Gun gun)
        {
            try
            {
                if (_shotLogBudget <= 0 && (Time.frameCount % 50) != 0) return;
                if (_shotLogBudget > 0) _shotLogBudget--;
                var main = CharacterMainControl.Main;
                if (gun == null || gun.muzzle == null || main == null) return;
                Transform space = main.modelRoot != null ? main.modelRoot : main.transform;
                Vector3 local = space.InverseTransformPoint(gun.muzzle.position);

                string projInfo = "";
                try
                {
                    var f = typeof(ItemAgent_Gun).GetField("projInst", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    var proj = f != null ? f.GetValue(gun) as Component : null;
                    if (proj != null)
                    {
                        Vector3 pl = space.InverseTransformPoint(proj.transform.position);
                        projInfo = " | BULLET '" + proj.name + "' local=" + F(pl) + " (" + Side(pl.x) + ") dir local=" + F(space.InverseTransformDirection(proj.transform.forward));
                    }
                }
                catch (Exception e) { projInfo = " | BULLET read failed: " + e.Message; }

                Log("SHOT gun=" + gun.name + " muzzle local=" + F(local) + " (" + Side(local.x) + ") mirror=" + config.mirror + " mode=" + config.mode + projInfo);
            }
            catch (Exception e) { Log("shot log failed: " + e.Message); }
        }

        private static string Side(float x) { return x < -0.01f ? "LEFT" : x > 0.01f ? "RIGHT" : "CENTER"; }

        // ───────────────────────── 근접 스냅샷 (디버그) ─────────────────────────
        private static int _snapIndex;

        private void Snapshot()
        {
            var main = CharacterMainControl.Main;
            if (main == null) { Log("snapshot: MainCharacter 없음"); return; }
            var src = Camera.main;
            if (src == null) { foreach (var c in Camera.allCameras) if (c.enabled) { src = c; break; } }
            if (src == null) { Log("snapshot: 카메라 없음"); return; }

            GameObject go = null; RenderTexture rt = null; Texture2D tex = null;
            try
            {
                Vector3 center = main.transform.position + Vector3.up * 0.55f;
                Vector3 fwd = main.CurrentAimDirection; fwd.y = 0f; if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward; fwd.Normalize();
                go = new GameObject("ShoulderTransition_SnapCam");
                var cam = go.AddComponent<Camera>();
                cam.CopyFrom(src);
                cam.targetDisplay = 0; cam.orthographic = false; cam.fieldOfView = 35f; cam.nearClipPlane = 0.05f;
                cam.transform.position = center + fwd * 2.6f + Vector3.up * 1.6f;
                cam.transform.LookAt(center);
                rt = new RenderTexture(1024, 1024, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                var path = Path.Combine(ModDir(), "snap_" + (_snapIndex++) + ".png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Log("snapshot saved: " + path + " (mirror=" + config.mirror + " mode=" + config.mode + ")");
            }
            catch (Exception e) { Log("snapshot failed: " + e); }
            finally
            {
                if (go != null) Destroy(go);
                if (rt != null) { rt.Release(); Destroy(rt); }
                if (tex != null) Destroy(tex);
            }
        }

        // ───────────────────────── 계층 덤프 (디버그) ─────────────────────────
        private void DumpHierarchy()
        {
            var main = CharacterMainControl.Main;
            if (main == null) { Log("MainCharacter 없음. 세이브를 로드한 뒤 덤프 키."); return; }

            var sb = new StringBuilder();
            sb.AppendLine(Tag + "===== PLAYER HIERARCHY DUMP =====");
            sb.AppendLine("main = " + main.name + "  modelRoot = " + Name(main.modelRoot) + "  characterModel = " + (main.characterModel ? main.characterModel.name : "null"));
            if (main.characterModel != null)
            {
                var m = main.characterModel;
                sb.AppendLine("RightHandSocket   = " + PathOf(m.RightHandSocket));
                sb.AppendLine("LefthandSocket    = " + PathOf(m.LefthandSocket));
                sb.AppendLine("MeleeWeaponSocket = " + PathOf(m.MeleeWeaponSocket));
                sb.AppendLine("model localScale  = " + m.transform.localScale);
            }
            var held = main.CurrentHoldItemAgent;
            sb.AppendLine("held item agent = " + (held ? held.name : "null") + "  aim socket = " + PathOf(main.CurrentUsingAimSocket));
            var gun = held as ItemAgent_Gun;
            if (gun != null && gun.muzzle != null)
                sb.AppendLine("muzzle = " + PathOf(gun.muzzle) + "  world=" + gun.muzzle.position + "  fwd=" + gun.muzzle.forward +
                              "  charPos=" + main.transform.position + "  aimDir=" + main.CurrentAimDirection +
                              "  reloadKey=" + (gun.GunItemSetting ? gun.GunItemSetting.reloadKey : "?"));
            DumpRecursive(main.transform, 0, sb);
            sb.AppendLine(Tag + "===== END DUMP =====");

            Debug.Log(sb.ToString());
            try
            {
                var path = Path.Combine(ModDir(), "hierarchy_dump.txt");
                File.WriteAllText(path, sb.ToString(), Utf8NoBom);
                Log("dump saved: " + path);
            }
            catch (Exception e) { Log("dump save failed: " + e.Message); }
        }

        private static void DumpRecursive(Transform t, int depth, StringBuilder sb)
        {
            var indent = new string(' ', depth * 2);
            var comps = string.Join(",", t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name));
            sb.AppendLine(indent + t.name + "  pos=" + F(t.localPosition) + " rot=" + F(t.localEulerAngles) + " scl=" + F(t.localScale) + "  [" + comps + "]");
            for (int i = 0; i < t.childCount; i++) DumpRecursive(t.GetChild(i), depth + 1, sb);
        }

        private static string F(Vector3 v) { return "(" + v.x.ToString("0.###") + "," + v.y.ToString("0.###") + "," + v.z.ToString("0.###") + ")"; }
        private static string Name(UnityEngine.Object o) { return o ? o.name : "null"; }

        private static string PathOf(Transform t)
        {
            if (t == null) return "null";
            var sb = new StringBuilder(t.name);
            while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
            return sb.ToString();
        }

        // ───────────────────────── 설정 파일 (config.json, 사람이 읽을 수 있게 enum 은 이름으로) ─────────────────────────
        private static string ModDir()
        {
            try { return Path.GetDirectoryName(typeof(ModBehaviour).Assembly.Location); }
            catch { return Application.persistentDataPath; }
        }

        private static IEnumerable<FieldInfo> ConfigFields()
        {
            return typeof(Config).GetFields(BindingFlags.Instance | BindingFlags.Public);
        }

        private void LoadConfig()
        {
            _cfgPath = Path.Combine(ModDir(), "config.json");
            config = new Config();
            try
            {
                if (!File.Exists(_cfgPath)) { SaveConfig(); return; }
                var text = File.ReadAllText(_cfgPath);
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (Match m in Regex.Matches(text, "\"(\\w+)\"\\s*:\\s*(?:\"([^\"]*)\"|([^,}\\s]+))"))
                    map[m.Groups[1].Value] = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;

                // 구버전 키 이름 호환
                string legacy;
                if (!map.ContainsKey("mirror") && map.TryGetValue("enabled", out legacy)) map["mirror"] = legacy;

                foreach (var f in ConfigFields())
                {
                    string raw;
                    if (!map.TryGetValue(f.Name, out raw)) continue;
                    try
                    {
                        object v = null;
                        if (f.FieldType == typeof(bool)) v = raw.Equals("true", StringComparison.OrdinalIgnoreCase);
                        else if (f.FieldType.IsEnum)
                        {
                            try { v = Enum.Parse(f.FieldType, raw, true); }
                            catch { int n; if (int.TryParse(raw, out n)) v = Enum.ToObject(f.FieldType, n); }
                        }
                        else if (f.FieldType == typeof(int)) v = int.Parse(raw, CultureInfo.InvariantCulture);
                        else if (f.FieldType == typeof(float)) v = float.Parse(raw, CultureInfo.InvariantCulture);
                        else if (f.FieldType == typeof(string)) v = raw;
                        if (v != null) f.SetValue(config, v);
                    }
                    catch (Exception e) { Log("config: '" + f.Name + "'=" + raw + " 무시 (" + e.Message + ")"); }
                }
            }
            catch (Exception e)
            {
                Log("config load failed, using defaults: " + e.Message);
                config = new Config();
            }
        }

        private void SaveConfig()
        {
            try
            {
                var sb = new StringBuilder("{\n");
                var fields = ConfigFields().ToArray();
                for (int i = 0; i < fields.Length; i++)
                {
                    var f = fields[i]; var v = f.GetValue(config);
                    string s;
                    if (v is bool) s = (bool)v ? "true" : "false";
                    else if (v is Enum || v is string) s = "\"" + v + "\"";
                    else s = Convert.ToString(v, CultureInfo.InvariantCulture);
                    sb.Append("    \"").Append(f.Name).Append("\": ").Append(s).Append(i < fields.Length - 1 ? ",\n" : "\n");
                }
                sb.Append("}\n");
                File.WriteAllText(_cfgPath, sb.ToString(), Utf8NoBom);
            }
            catch (Exception e) { Log("config save failed: " + e.Message); }
        }
    }

    /// <summary>Model 모드: 메인 캐릭터의 CharacterModel 트랜스폼 X 스케일 반전.</summary>
    internal static class ModelMirror
    {
        private static Transform _applied;

        public static void Tick(bool want)
        {
            var main = CharacterMainControl.Main;
            Transform model = (main != null && main.characterModel != null) ? main.characterModel.transform : null;

            if (_applied != null)
            {
                if (!want || _applied != model) Restore();
            }
            else if (!ReferenceEquals(_applied, null))
            {
                _applied = null; // 파괴된 트랜스폼 참조 정리
            }

            if (want && model != null && ReferenceEquals(_applied, null))
            {
                var s = model.localScale;
                if (s.x > 0f) { s.x = -s.x; model.localScale = s; }
                _applied = model;
                ModBehaviour.Log("model mirror applied to '" + model.name + "' scale=" + s + " parent=" + (model.parent ? model.parent.name : "null"));
            }
        }

        public static void Restore()
        {
            if (_applied != null)
            {
                var s = _applied.localScale;
                if (s.x < 0f) { s.x = -s.x; _applied.localScale = s; }
            }
            _applied = null;
        }
    }
}
