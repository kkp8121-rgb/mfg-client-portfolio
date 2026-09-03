using UnityEditor;
using UnityEngine;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Combat;
using MkLike.Economy;
using MkLike.Equipment;

namespace MkLike.Editor
{
    /// <summary>
    /// Play 모드에서 엘리트 소환 + 가챠 E2E를 프로그래밍적으로 검증한다.
    /// </summary>
    public static class R8E2ETestEditor
    {
        [MenuItem("mkLike/R8 E2E Test — Elite + Gacha (Play Mode)")]
        public static void RunE2ETest()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[R8E2E] Play 모드에서만 실행 가능합니다.");
                return;
            }

            RunAsync().Forget();
        }

        private static async UniTaskVoid RunAsync()
        {
            Debug.LogError("[R8E2E] === E2E 테스트 시작 ===");
            int passed = 0;
            int failed = 0;

            // ── 1. 엘리트 소환 E2E ──
            Debug.LogError("[R8E2E] --- 엘리트 소환 테스트 ---");

            var cm = CurrencyManager.Instance;
            var esm = EliteSummonManager.Instance;
            var em = EquipmentManager.Instance;

            if (cm == null) { Debug.LogError("[R8E2E] FAIL: CurrencyManager null"); failed++; }
            else { Debug.LogError("[R8E2E] PASS: CurrencyManager 존재"); passed++; }

            if (esm == null) { Debug.LogError("[R8E2E] FAIL: EliteSummonManager null"); failed++; }
            else { Debug.LogError("[R8E2E] PASS: EliteSummonManager 존재"); passed++; }

            if (em == null) { Debug.LogError("[R8E2E] FAIL: EquipmentManager null"); failed++; }
            else { Debug.LogError("[R8E2E] PASS: EquipmentManager 존재"); passed++; }

            if (cm != null && esm != null)
            {
                // HuntPoint 지급
                BigNumber prevHp = cm.GetAmount(CurrencyType.HuntPoint);
                cm.Add(CurrencyType.HuntPoint, 1000);
                BigNumber afterHp = cm.GetAmount(CurrencyType.HuntPoint);

                if (afterHp >= prevHp + 1000)
                { Debug.LogError($"[R8E2E] PASS: HuntPoint 지급 ({prevHp.ToKoreanShort()} → {afterHp.ToKoreanShort()})"); passed++; }
                else
                { Debug.LogError($"[R8E2E] FAIL: HuntPoint 지급 실패 ({prevHp.ToKoreanShort()} → {afterHp.ToKoreanShort()})"); failed++; }

                // 소환 비용 확인
                int cost = esm.CurrentSummonCost;
                Debug.LogError($"[R8E2E] INFO: 소환 비용={cost}, 소환 레벨={esm.SummonLevel}, 최고 등급={esm.CurrentMaxGrade}");

                // 소환 시도
                bool canSummon = esm.CanSummon;
                if (canSummon)
                { Debug.LogError("[R8E2E] PASS: CanSummon=true"); passed++; }
                else
                { Debug.LogError("[R8E2E] FAIL: CanSummon=false (HuntPoint 충분한데도)"); failed++; }

                if (canSummon)
                {
                    int prevInvCount = em.Catalog?.Count ?? 0;
                    bool summoned = esm.TrySummon();
                    if (summoned)
                    { Debug.LogError("[R8E2E] PASS: TrySummon() 성공"); passed++; }
                    else
                    { Debug.LogError("[R8E2E] FAIL: TrySummon() 실패"); failed++; }

                    // 쿨다운 확인
                    float cd = esm.CooldownRemaining;
                    if (cd > 0)
                    { Debug.LogError($"[R8E2E] PASS: 쿨다운 작동 ({cd:F1}초)"); passed++; }
                    else
                    { Debug.LogError("[R8E2E] WARN: 쿨다운 0 (SO 설정 확인 필요)"); }
                }
            }

            await UniTask.Delay(500);

            // ── 2. 가챠 E2E ──
            Debug.LogError("[R8E2E] --- 가챠 테스트 ---");

            var gm = GachaManager.Instance;
            if (gm == null) { Debug.LogError("[R8E2E] FAIL: GachaManager null"); failed++; }
            else { Debug.LogError("[R8E2E] PASS: GachaManager 존재"); passed++; }

            if (cm != null && gm != null && em != null)
            {
                // 루비 지급
                cm.Add(CurrencyType.Ruby, 10000);
                Debug.LogError($"[R8E2E] INFO: 루비 지급 → {cm.GetAmount(CurrencyType.Ruby)}");

                // 장비 가챠 1회
                int prevCount = 0;
                var field = typeof(EquipmentManager).GetField("_inventory",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var inv = field.GetValue(em) as System.Collections.IList;
                    prevCount = inv?.Count ?? 0;
                }

                var result = gm.Pull(GachaPoolType.Equipment);
                if (result != null)
                {
                    Debug.LogError($"[R8E2E] PASS: 가챠 Pull 성공 — {result.itemId} ({result.grade})");
                    passed++;

                    // 인벤토리에 추가됐는지 확인
                    await UniTask.DelayFrame(2);
                    int afterCount = 0;
                    if (field != null)
                    {
                        var inv = field.GetValue(em) as System.Collections.IList;
                        afterCount = inv?.Count ?? 0;
                    }

                    if (afterCount > prevCount)
                    { Debug.LogError($"[R8E2E] PASS: 인벤토리 추가 ({prevCount} → {afterCount})"); passed++; }
                    else
                    { Debug.LogError($"[R8E2E] FAIL: 인벤토리 미추가 ({prevCount} → {afterCount})"); failed++; }
                }
                else
                {
                    Debug.LogError("[R8E2E] FAIL: 가챠 Pull 결과 null (풀 데이터 확인 필요)");
                    failed++;
                }
            }

            Debug.LogError($"[R8E2E] === 완료: {passed} PASS / {failed} FAIL ===");
        }
    }
}
