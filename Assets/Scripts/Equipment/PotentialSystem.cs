using System.Collections.Generic;
using UnityEngine;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Economy;
using MkLike.Utils;

namespace MkLike.Equipment
{
    /// <summary>
    /// 잠재능력 변경 결과.
    /// </summary>
    public enum PotentialChangeResult
    {
        /// <summary>등급 승급 성공</summary>
        Upgraded,
        /// <summary>옵션 재설정 (등급 유지)</summary>
        Rerolled,
        /// <summary>실패 (변화 없음, 천장 카운터만 증가)</summary>
        Failed
    }

    /// <summary>
    /// 장비 잠재능력 + 보조 잠재능력 시스템 (슬롯 기반).
    /// C4-07 잠재능력 등급 승급 + 옵션 재롤
    /// C4-08 보조 잠재능력 (12성 이상 개방)
    /// </summary>
    public class PotentialSystem : MonoBehaviour
    {
        public static PotentialSystem Instance { get; private set; }

        private const int SUB_POTENTIAL_MIN_STAR = 12;

        // ── 승급 확률 & 천장 테이블 ──

        private static readonly Dictionary<PotentialGrade, float> UpgradeRates = new()
        {
            { PotentialGrade.None, 0.06f },
            { PotentialGrade.Rare, 0.0333f },
            { PotentialGrade.Epic, 0.0167f },
            { PotentialGrade.Unique, 0.006f },
            { PotentialGrade.Legendary, 0.0021f }
        };

        private static readonly Dictionary<PotentialGrade, int> PityCeilings = new()
        {
            { PotentialGrade.None, 33 },
            { PotentialGrade.Rare, 60 },
            { PotentialGrade.Epic, 120 },
            { PotentialGrade.Unique, 333 },
            { PotentialGrade.Legendary, 714 }
        };

        // ── 옵션 풀 ──

        private static readonly string[] PercentOptions = { "ATK", "HP", "DEF" };
        private static readonly string[] FlatOptions = { "CritRate", "CritDmg", "AtkSpeed", "BossDmg", "IgnoreDef" };

        private static readonly Dictionary<PotentialGrade, (int min, int max)> PercentRanges = new()
        {
            { PotentialGrade.Rare, (1, 3) },
            { PotentialGrade.Epic, (2, 6) },
            { PotentialGrade.Unique, (3, 9) },
            { PotentialGrade.Legendary, (5, 12) },
            { PotentialGrade.Mythic, (7, 15) }
        };

        private static readonly Dictionary<PotentialGrade, (int min, int max)> FlatRanges = new()
        {
            { PotentialGrade.Rare, (1, 2) },
            { PotentialGrade.Epic, (1, 3) },
            { PotentialGrade.Unique, (2, 5) },
            { PotentialGrade.Legendary, (3, 7) },
            { PotentialGrade.Mythic, (4, 10) }
        };

        // 천장 카운터: slot → 누적 실패 횟수
        private readonly Dictionary<EquipmentSlot, int> _pityCounters = new();
        private readonly Dictionary<EquipmentSlot, int> _subPityCounters = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            EventBus.SubscribeSticky<BeforeSaveEvent>(OnBeforeSave);
            EventBus.SubscribeSticky<LoadCompletedEvent>(OnLoadCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<BeforeSaveEvent>(OnBeforeSave);
            EventBus.Unsubscribe<LoadCompletedEvent>(OnLoadCompleted);
        }

        private void OnBeforeSave(BeforeSaveEvent evt) => SyncPityToSaveData();

        private void OnLoadCompleted(LoadCompletedEvent evt) => LoadPityState();

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ══════════════════════════════════
        //  C4-07: 잠재능력 (메인) — 슬롯 기반
        // ══════════════════════════════════

        /// <summary>
        /// 슬롯의 잠재능력 변경이 가능한지 확인한다.
        /// </summary>
        public bool CanChangePotential(EquipmentSlot slot)
        {
            if (EquipmentManager.Instance == null || CurrencyManager.Instance == null)
                return false;

            var slotEnh = EquipmentManager.Instance.GetSlotEnhancement(slot);
            if (slotEnh.potentialGrade >= PotentialGrade.Mythic) return false;

            long goldCost = GetPotentialChangeCost(slotEnh);
            return CurrencyManager.Instance.HasEnough(CurrencyType.Gold, goldCost)
                && CurrencyManager.Instance.HasEnough(CurrencyType.PotentialStone, 1);
        }

        /// <summary>
        /// 슬롯의 잠재능력을 변경한다.
        /// </summary>
        public PotentialChangeResult ChangePotential(EquipmentSlot slot)
        {
            if (EquipmentManager.Instance == null || CurrencyManager.Instance == null)
                return PotentialChangeResult.Failed;

            var slotEnh = EquipmentManager.Instance.GetSlotEnhancement(slot);

            if (slotEnh.potentialGrade >= PotentialGrade.Mythic)
            {
                Debug.LogWarning("[PotentialSystem] 이미 최고 등급");
                return PotentialChangeResult.Failed;
            }

            long goldCost = GetPotentialChangeCost(slotEnh);
            if (!CurrencyManager.Instance.SpendMultiple(
                (CurrencyType.Gold, goldCost),
                (CurrencyType.PotentialStone, 1)))
            {
                return PotentialChangeResult.Failed;
            }

            if (!_pityCounters.ContainsKey(slot))
                _pityCounters[slot] = 0;
            _pityCounters[slot]++;

            PotentialGrade currentGrade = slotEnh.potentialGrade;
            bool isPityReached = IsPityReached(currentGrade, _pityCounters[slot]);
            bool isUpgradeSuccess = isPityReached || Random.value <= UpgradeRates[currentGrade];

            PotentialChangeResult result;

            if (isUpgradeSuccess)
            {
                slotEnh.potentialGrade = GetNextGrade(currentGrade);
                slotEnh.potentialOptions = GeneratePotentialOptions(slotEnh.potentialGrade);
                _pityCounters[slot] = 0;
                result = PotentialChangeResult.Upgraded;

                Debug.Log($"[PotentialSystem] {slot} 잠재능력 승급! {currentGrade} → {slotEnh.potentialGrade}" +
                    (isPityReached ? " (천장)" : ""));
            }
            else if (currentGrade > PotentialGrade.None)
            {
                slotEnh.potentialOptions = GeneratePotentialOptions(currentGrade);
                result = PotentialChangeResult.Rerolled;

                Debug.Log($"[PotentialSystem] {slot} 잠재능력 옵션 재설정 (등급 유지: {currentGrade})");
            }
            else
            {
                result = PotentialChangeResult.Failed;

                Debug.Log($"[PotentialSystem] {slot} 잠재능력 승급 실패 (천장: {_pityCounters[slot]}/{PityCeilings[currentGrade]})");
            }

            SyncEquipmentSave();

            // 이벤트 발행
            int totalSets = CountPotentialSets();
            EventBus.Publish(new PotentialChangedEvent
            {
                Slot = slot,
                NewGrade = slotEnh.potentialGrade,
                Result = result.ToString(),
                TotalPotentialSets = totalSets
            });

            // 2026-04-23 이슈 15 FeedbackBus: 잠재능력 결과별 Toast + 쉐이크
            switch (result)
            {
                case PotentialChangeResult.Upgraded:
                    MkLike.Core.FeedbackBus.Emit(
                        MkLike.Core.FeedbackKind.EquipEnhance,
                        $"잠재능력 승급! {slot} → {slotEnh.potentialGrade}",
                        shakeIntensity: 2);
                    break;
                case PotentialChangeResult.Rerolled:
                    MkLike.Core.FeedbackBus.Emit(
                        MkLike.Core.FeedbackKind.EquipEnhance,
                        $"잠재능력 재설정 ({slot} {slotEnh.potentialGrade})");
                    break;
                case PotentialChangeResult.Failed:
                    MkLike.Core.FeedbackBus.Emit(
                        MkLike.Core.FeedbackKind.Negative,
                        $"잠재능력 실패 ({slot})");
                    break;
            }

            return result;
        }

        // ══════════════════════════════════
        //  C4-08: 보조 잠재능력 — 슬롯 기반
        // ══════════════════════════════════

        /// <summary>
        /// 슬롯의 보조 잠재능력 변경이 가능한지 확인한다.
        /// </summary>
        public bool CanChangeSubPotential(EquipmentSlot slot)
        {
            if (EquipmentManager.Instance == null || CurrencyManager.Instance == null)
                return false;

            var slotEnh = EquipmentManager.Instance.GetSlotEnhancement(slot);
            if (slotEnh.starForce < SUB_POTENTIAL_MIN_STAR) return false;
            if (slotEnh.subPotentialGrade >= PotentialGrade.Mythic) return false;

            long goldCost = GetSubPotentialChangeCost(slotEnh);
            return CurrencyManager.Instance.HasEnough(CurrencyType.Gold, goldCost)
                && CurrencyManager.Instance.HasEnough(CurrencyType.SuperPotentialStone, 1);
        }

        /// <summary>
        /// 슬롯의 보조 잠재능력을 변경한다.
        /// </summary>
        public PotentialChangeResult ChangeSubPotential(EquipmentSlot slot)
        {
            if (EquipmentManager.Instance == null || CurrencyManager.Instance == null)
                return PotentialChangeResult.Failed;

            var slotEnh = EquipmentManager.Instance.GetSlotEnhancement(slot);

            if (slotEnh.starForce < SUB_POTENTIAL_MIN_STAR)
            {
                Debug.LogWarning($"[PotentialSystem] 보조 잠재능력은 {SUB_POTENTIAL_MIN_STAR}성 이상 필요 (현재: {slotEnh.starForce}성)");
                return PotentialChangeResult.Failed;
            }

            if (slotEnh.subPotentialGrade >= PotentialGrade.Mythic)
            {
                Debug.LogWarning("[PotentialSystem] 보조 잠재능력 이미 최고 등급");
                return PotentialChangeResult.Failed;
            }

            long goldCost = GetSubPotentialChangeCost(slotEnh);
            if (!CurrencyManager.Instance.SpendMultiple(
                (CurrencyType.Gold, goldCost),
                (CurrencyType.SuperPotentialStone, 1)))
            {
                return PotentialChangeResult.Failed;
            }

            if (!_subPityCounters.ContainsKey(slot))
                _subPityCounters[slot] = 0;
            _subPityCounters[slot]++;

            PotentialGrade currentGrade = slotEnh.subPotentialGrade;
            bool isPityReached = IsPityReached(currentGrade, _subPityCounters[slot]);
            bool isUpgradeSuccess = isPityReached || Random.value <= UpgradeRates[currentGrade];

            PotentialChangeResult result;

            if (isUpgradeSuccess)
            {
                slotEnh.subPotentialGrade = GetNextGrade(currentGrade);
                slotEnh.subPotentialOptions = GeneratePotentialOptions(slotEnh.subPotentialGrade);
                _subPityCounters[slot] = 0;
                result = PotentialChangeResult.Upgraded;

                Debug.Log($"[PotentialSystem] {slot} 보조 잠재능력 승급! {currentGrade} → {slotEnh.subPotentialGrade}" +
                    (isPityReached ? " (천장)" : ""));
            }
            else if (currentGrade > PotentialGrade.None)
            {
                slotEnh.subPotentialOptions = GeneratePotentialOptions(currentGrade);
                result = PotentialChangeResult.Rerolled;

                Debug.Log($"[PotentialSystem] {slot} 보조 잠재능력 옵션 재설정 (등급 유지: {currentGrade})");
            }
            else
            {
                result = PotentialChangeResult.Failed;

                Debug.Log($"[PotentialSystem] {slot} 보조 잠재능력 승급 실패 (천장: {_subPityCounters[slot]}/{PityCeilings[currentGrade]})");
            }

            SyncEquipmentSave();
            return result;
        }

        // ══════════════════════════════════
        //  비용 계산
        // ══════════════════════════════════

        /// <summary>잠재능력 변경 골드 비용.</summary>
        public long GetPotentialChangeCost(SlotEnhancementData slotEnh)
        {
            if (slotEnh == null) return 0;
            int tier = (int)slotEnh.potentialGrade;
            return (long)(200 * System.Math.Pow(1.15, tier));
        }

        /// <summary>보조 잠재능력 변경 골드 비용.</summary>
        public long GetSubPotentialChangeCost(SlotEnhancementData slotEnh)
        {
            if (slotEnh == null) return 0;
            int tier = (int)slotEnh.subPotentialGrade;
            return (long)(300 * System.Math.Pow(1.15, tier));
        }

        /// <summary>현재 천장 카운터 (메인 잠재능력).</summary>
        public int GetPityCount(EquipmentSlot slot)
        {
            return _pityCounters.TryGetValue(slot, out int count) ? count : 0;
        }

        /// <summary>현재 천장 카운터 (보조 잠재능력).</summary>
        public int GetSubPityCount(EquipmentSlot slot)
        {
            return _subPityCounters.TryGetValue(slot, out int count) ? count : 0;
        }

        /// <summary>현재 등급의 천장 한도.</summary>
        public int GetPityCeiling(PotentialGrade grade)
        {
            return PityCeilings.TryGetValue(grade, out int ceiling) ? ceiling : 0;
        }

        // ══════════════════════════════════
        //  옵션 생성
        // ══════════════════════════════════

        public static List<string> GeneratePotentialOptions(PotentialGrade grade)
        {
            var options = new List<string>(3);

            if (grade == PotentialGrade.None)
                return options;

            if (!PercentRanges.ContainsKey(grade))
                return options;

            var pRange = PercentRanges[grade];
            var fRange = FlatRanges[grade];

            for (int i = 0; i < 3; i++)
            {
                if (Random.value < 0.5f)
                {
                    string stat = PercentOptions[Random.Range(0, PercentOptions.Length)];
                    int value = Random.Range(pRange.min, pRange.max + 1);
                    options.Add($"{stat}+{value}%");
                }
                else
                {
                    string stat = FlatOptions[Random.Range(0, FlatOptions.Length)];
                    int value = Random.Range(fRange.min, fRange.max + 1);
                    options.Add($"{stat}+{value}%");
                }
            }

            return options;
        }

        // ══════════════════════════════════
        //  내부 유틸
        // ══════════════════════════════════

        /// <summary>잠재능력이 설정된 슬롯 수를 반환한다.</summary>
        private int CountPotentialSets()
        {
            if (EquipmentManager.Instance == null) return 0;
            int count = 0;
            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                var enh = EquipmentManager.Instance.GetSlotEnhancement(slot);
                if (enh != null && enh.potentialGrade > PotentialGrade.None)
                    count++;
            }
            return count;
        }

        private static PotentialGrade GetNextGrade(PotentialGrade current)
        {
            return current switch
            {
                PotentialGrade.None => PotentialGrade.Rare,
                PotentialGrade.Rare => PotentialGrade.Epic,
                PotentialGrade.Epic => PotentialGrade.Unique,
                PotentialGrade.Unique => PotentialGrade.Legendary,
                PotentialGrade.Legendary => PotentialGrade.Mythic,
                _ => PotentialGrade.Mythic
            };
        }

        private static bool IsPityReached(PotentialGrade currentGrade, int pityCount)
        {
            if (!PityCeilings.TryGetValue(currentGrade, out int ceiling))
                return false;
            return pityCount >= ceiling;
        }

        private void SyncEquipmentSave()
        {
            // EquipmentManager의 인벤토리/강화/슬롯 + PotentialSystem의 pity 모두
            // BeforeSaveEvent 구독자로서 자동 집계되므로 SaveManager.Save() 한 번으로 충분.
            SaveManager.Instance?.Save();
        }

        // ── 천장 카운터 저장/로드 (슬롯 기반) ──

        private void LoadPityState()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null) return;

            var data = SaveManager.Instance.CurrentData.potentialPity;
            if (data == null) return;

            _pityCounters.Clear();
            _subPityCounters.Clear();

            if (data.main != null)
            {
                for (int i = 0; i < data.main.Count; i++)
                {
                    var entry = data.main[i];
                    if (!string.IsNullOrEmpty(entry.instanceId) && entry.count > 0)
                    {
                        if (System.Enum.TryParse<EquipmentSlot>(entry.instanceId, out var slot))
                            _pityCounters[slot] = entry.count;
                    }
                }
            }

            if (data.sub != null)
            {
                for (int i = 0; i < data.sub.Count; i++)
                {
                    var entry = data.sub[i];
                    if (!string.IsNullOrEmpty(entry.instanceId) && entry.count > 0)
                    {
                        if (System.Enum.TryParse<EquipmentSlot>(entry.instanceId, out var slot))
                            _subPityCounters[slot] = entry.count;
                    }
                }
            }

            Debug.Log($"[PotentialSystem] 천장 카운터 로드 완료 — 메인:{_pityCounters.Count}건 보조:{_subPityCounters.Count}건");
        }

        /// <summary>
        /// 런타임 pity 상태를 CurrentData.potentialPity에 기록한다. BeforeSaveEvent에서 호출된다.
        /// 디스크 write는 SaveManager가 담당하므로 여기서는 CurrentData만 갱신.
        /// </summary>
        private void SyncPityToSaveData()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null) return;

            var data = SaveManager.Instance.CurrentData.potentialPity;
            if (data == null)
            {
                data = new PotentialPitySaveData();
                SaveManager.Instance.CurrentData.potentialPity = data;
            }

            data.main.Clear();
            foreach (var kvp in _pityCounters)
            {
                if (kvp.Value > 0)
                    data.main.Add(new PotentialPityEntry { instanceId = kvp.Key.ToString(), count = kvp.Value });
            }

            data.sub.Clear();
            foreach (var kvp in _subPityCounters)
            {
                if (kvp.Value > 0)
                    data.sub.Add(new PotentialPityEntry { instanceId = kvp.Key.ToString(), count = kvp.Value });
            }
        }
    }
}
