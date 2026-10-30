using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ShoulderTransition
{
    /// <summary>
    /// Socket 모드. CharacterModel.LateUpdate(SyncRightHandRotation 가 소켓 회전을 덮어쓰는 곳) 앞뒤에 끼어든다.
    ///   Prefix  : 지난 프레임에 우리가 미러링해 둔 소켓을 원래 포즈로 되돌린다 (게임 로직은 항상 원본 포즈만 본다).
    ///   Postfix : 대상 캐릭터면 소켓을 modelRoot 기준 YZ 평면으로 미러링하고 원본/미러 포즈를 기억한다.
    /// 대상 = (로컬 Socket 모드가 켜진) 메인 캐릭터 + <see cref="ExtraModels"/> (코옵 원격 플레이어 중 Socket 모드인 사람).
    /// 렌더링과 Update 단계(총 발사 = muzzle.position)는 미러링된 포즈를 보게 된다.
    /// </summary>
    internal static class SocketMirrorPatch
    {
        public const string HarmonyId = "com.kdh.shouldertransition";

        public static Func<bool> IsEnabled = () => false;     // 로컬 플레이어에 적용할지
        public static Func<bool> MirrorMesh = () => false;
        public static Func<bool> MirrorMelee = () => true;

        /// <summary>로컬 설정과 무관하게 소켓 미러링할 모델들 (코옵 원격 플레이어)</summary>
        public static readonly HashSet<CharacterModel> ExtraModels = new HashSet<CharacterModel>();

        /// <summary>로컬 Socket 모드 사용 중인지 (RemoveIfUnused 판단용, ModBehaviour 가 설정)</summary>
        public static Func<bool> LocalWantsPatch = () => false;

        private static Harmony _harmony;
        private static bool _patched;

        private sealed class Saved
        {
            public CharacterModel owner;
            public Transform t;
            public Vector3 pos, mPos, scl, mScl;
            public Quaternion rot, mRot;
        }

        private static readonly List<Saved> _saved = new List<Saved>();

        public static bool Apply()
        {
            if (_patched) return true;
            var target = AccessTools.Method(typeof(CharacterModel), "LateUpdate");
            if (target == null)
            {
                ModBehaviour.Log("ERROR CharacterModel.LateUpdate 를 찾지 못했습니다 (게임 업데이트로 구조가 바뀌었을 수 있음).");
                return false;
            }
            _harmony = _harmony ?? new Harmony(HarmonyId);
            _harmony.Patch(target,
                prefix: new HarmonyMethod(typeof(SocketMirrorPatch), nameof(Prefix)),
                postfix: new HarmonyMethod(typeof(SocketMirrorPatch), nameof(Postfix)));
            _patched = true;
            ModBehaviour.Log("socket patch applied (Harmony " + typeof(Harmony).Assembly.GetName().Version + ")");
            return true;
        }

        /// <summary>로컬도 원격도 소켓 미러가 필요 없으면 패치 해제</summary>
        public static void RemoveIfUnused()
        {
            bool localWants = false;
            try { localWants = LocalWantsPatch(); } catch { }
            if (!localWants && ExtraModels.Count == 0) Remove();
        }

        public static void Remove()
        {
            RestoreAll();
            if (_patched && _harmony != null)
            {
                try { _harmony.UnpatchAll(HarmonyId); } catch (Exception e) { Debug.LogWarning(ModBehaviour.Tag + "unpatch failed: " + e.Message); }
            }
            _patched = false;
        }

        // ───────────────────────── Harmony 훅 ─────────────────────────
        private static void Prefix(CharacterModel __instance)
        {
            for (int i = _saved.Count - 1; i >= 0; i--)
                if (_saved[i].owner == __instance) { RestoreEntry(_saved[i]); _saved.RemoveAt(i); }
        }

        private static void Postfix(CharacterModel __instance)
        {
            var c = __instance.characterMainControl;
            if (c == null) return;

            bool target = (IsEnabled() && c.IsMainCharacter) || (ExtraModels.Count > 0 && ExtraModels.Contains(__instance));
            if (!target) return;

            Transform space = c.modelRoot != null ? c.modelRoot : __instance.transform;
            MirrorAndSave(__instance, __instance.RightHandSocket, space);
            if (MirrorMelee()) MirrorAndSave(__instance, __instance.MeleeWeaponSocket, space);
        }

        // ───────────────────────── 미러 수학 ─────────────────────────
        private static void MirrorAndSave(CharacterModel owner, Transform t, Transform space)
        {
            if (t == null) return;
            var s = new Saved { owner = owner, t = t, pos = t.localPosition, rot = t.localRotation, scl = t.localScale };

            // space(모델 루트) 로컬 좌표에서 x → -x 평면 대칭
            Vector3 lp = space.InverseTransformPoint(t.position);
            lp.x = -lp.x;
            t.position = space.TransformPoint(lp);

            // 회전의 거울상: 대칭축(x) 성분은 유지, 나머지 두 성분 부호 반전 (M R M, M=diag(-1,1,1))
            Quaternion lr = Quaternion.Inverse(space.rotation) * t.rotation;
            lr = new Quaternion(lr.x, -lr.y, -lr.z, lr.w);
            t.rotation = space.rotation * lr;

            if (MirrorMesh())
            {
                var sc = t.localScale;
                sc.x = -sc.x;
                t.localScale = sc;
            }

            s.mPos = t.localPosition; s.mRot = t.localRotation; s.mScl = t.localScale;
            _saved.Add(s);
        }

        private static void RestoreEntry(Saved s)
        {
            if (s.t == null) return;
            // 다른 코드(애니메이터 등)가 그 사이에 포즈를 바꿨으면 건드리지 않는다
            if (s.t.localPosition != s.mPos || s.t.localRotation != s.mRot || s.t.localScale != s.mScl) return;
            s.t.localPosition = s.pos;
            s.t.localRotation = s.rot;
            s.t.localScale = s.scl;
        }

        private static void RestoreAll()
        {
            foreach (var s in _saved) RestoreEntry(s);
            _saved.Clear();
        }
    }
}
