using System.Collections.Generic;
using UnityEngine;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Combat;
using MkLike.Data;
using MkLike.Dungeon;
using MkLike.Economy;
using MkLike.Equipment;
using MkLike.Growth;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// 추천 시스템 (UX-24~28).
    /// 장비/동료/스탯/던전에 대한 최적 추천을 제공한다.
    /// UI 어셈블리에 위치하여 모든 시스템을 참조할 수 있다.
    /// </summary>
    public class RecommendationManager : MonoBehaviour
    {
        public static RecommendationManager Instance { get; private set; }

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
            EventBus<EquipmentInventoryChangedEvent>.Subscribe(OnEquipmentInventoryChanged);
        }

        private void OnDisable()
        {
            EventBus<EquipmentInventoryChangedEvent>.Unsubscribe(OnEquipmentInventoryChanged);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ── 장비 획득 시 추천 갱신 (자동 장착 제거 — 유저가 직접 장착) ──

        private void OnEquipmentInventoryChanged(EquipmentInventoryChangedEvent evt)
        {
            if (!evt.IsAdded) return;

            // 추천 목록만 갱신 (자동 장착하지 않음 — 유저가 비교 후 직접 장착)
            var recommendations = GetRecommendedEquipment();
            if (recommendations.Count > 0)
            {
                EventBus.Publish(new EquipmentRecommendationEvent
                {
                    RecommendationCount = recommendations.Count,
                    TopCpGain = recommendations[0].CpGain
                });
                Debug.Log($"[RecommendationManager] 더 좋은 장비 {recommendations.Count}건 발견 (CP +{recommendations[0].CpGain})");
            }
        }

        // ── UX-24: 장비 추천 ──

        /// <summary>
        /// 각 슬롯별로 인벤토리에서 CP 기여가 가장 높은 장비를 추천한다.
        /// 현재 장착 장비보다 CP 기여가 높은 장비만 반환한다.
        /// </summary>
        public List<EquipmentRecommendation> GetRecommendedEquipment()
        {
            var result = new List<EquipmentRecommendation>();
            if (EquipmentManager.Instance == null) return result;

            var inventory = EquipmentManager.Instance.Inventory;
            if (inventory == null || inventory.Count == 0) return result;

            // 슬롯별로 최고 CP 장비 탐색
            var bestBySlot = new Dictionary<EquipmentSlot, (EquipmentInstance instance, int cpScore)>();

            for (int i = 0; i < inventory.Count; i++)
            {
                var inst = inventory[i];
                var data = EquipmentManager.Instance.GetData(inst.equipmentId);
                if (data == null) continue;

                int cp = CalculateEquipmentCp(data, inst);
                if (!bestBySlot.ContainsKey(data.slot) || cp > bestBySlot[data.slot].cpScore)
                {
                    bestBySlot[data.slot] = (inst, cp);
                }
            }

            // 현재 장착과 비교
            foreach (var kvp in bestBySlot)
            {
                var equipped = EquipmentManager.Instance.GetEquipped(kvp.Key);
                int equippedCp = 0;
                if (equipped != null)
                {
                    var equippedData = EquipmentManager.Instance.GetData(equipped.equipmentId);
                    if (equippedData != null)
                        equippedCp = CalculateEquipmentCp(equippedData, equipped);
                }

                if (kvp.Value.cpScore > equippedCp && kvp.Value.instance != equipped)
                {
                    var data = EquipmentManager.Instance.GetData(kvp.Value.instance.equipmentId);
                    result.Add(new EquipmentRecommendation
                    {
                        Slot = kvp.Key,
                        Instance = kvp.Value.instance,
                        CpGain = kvp.Value.cpScore - equippedCp,
                        DisplayName = data != null ? data.displayName : kvp.Value.instance.equipmentId
                    });
                }
            }

            // CP 이득이 큰 순으로 정렬
            result.Sort((a, b) => b.CpGain.CompareTo(a.CpGain));
            return result;
        }

        private int CalculateEquipmentCp(EquipmentDataSO data, EquipmentInstance inst)
        {
            string grade = inst.grade ?? "Normal";
            int atk = data.GetAtk(grade);
            int hp = data.GetHp(grade);
            int def = data.GetDef(grade);

            // 슬롯 강화 보너스 (주문서 레벨당 5%, 성급당 3%)
            var slotEnh = EquipmentManager.Instance.GetSlotEnhancement(data.slot);
            float scrollBonus = 1f + slotEnh.scrollLevel * 0.05f;
            float starBonus = 1f + slotEnh.starForce * 0.03f;
            float totalMult = scrollBonus * starBonus;

            // CP = ATK * 3 + DEF * 2 + HP * 0.5 (공격력 가중)
            return Mathf.RoundToInt((atk * 3f + def * 2f + hp * 0.5f) * totalMult);
        }

        // ── UX-26: 스탯 분배 추천 ──

        /// <summary>
        /// 현재 직업에 맞는 최적 스탯 분배 비율을 반환한다.
        /// 전사: HP/방어 중심, 궁수: 공격/크리 중심, 마법사: 공격/크리 중심
        /// </summary>
        public StatDistributionRecommendation GetRecommendedStatDistribution()
        {
            var rec = new StatDistributionRecommendation();
            var job = JobSystem.Instance != null ? JobSystem.Instance.CurrentJob : JobType.Warrior;

            switch (job)
            {
                case JobType.Warrior:
                    rec.AtkPercent = 20;
                    rec.DefPercent = 25;
                    rec.HpPercent = 30;
                    rec.CritPercent = 15;
                    rec.AccuracyPercent = 10;
                    rec.Description = "전사 추천: 체력·방어 우선 투자";
                    break;
                case JobType.Archer:
                    rec.AtkPercent = 35;
                    rec.DefPercent = 10;
                    rec.HpPercent = 15;
                    rec.CritPercent = 30;
                    rec.AccuracyPercent = 10;
                    rec.Description = "궁수 추천: 공격·크리티컬 우선 투자";
                    break;
                case JobType.Mage:
                    rec.AtkPercent = 40;
                    rec.DefPercent = 5;
                    rec.HpPercent = 15;
                    rec.CritPercent = 25;
                    rec.AccuracyPercent = 15;
                    rec.Description = "마법사 추천: 공격력 집중 투자";
                    break;
                default:
                    rec.AtkPercent = 25;
                    rec.DefPercent = 20;
                    rec.HpPercent = 25;
                    rec.CritPercent = 20;
                    rec.AccuracyPercent = 10;
                    rec.Description = "균형 분배";
                    break;
            }

            // 사용 가능한 포인트로 실제 추천 수치 계산
            if (StatAllocationSystem.Instance != null)
            {
                int available = GetAvailableStatPoints();
                if (available > 0)
                {
                    rec.RecommendedAtk = Mathf.RoundToInt(available * rec.AtkPercent / 100f);
                    rec.RecommendedDef = Mathf.RoundToInt(available * rec.DefPercent / 100f);
                    rec.RecommendedHp = Mathf.RoundToInt(available * rec.HpPercent / 100f);
                    rec.RecommendedCrit = Mathf.RoundToInt(available * rec.CritPercent / 100f);
                    rec.RecommendedAccuracy = available - rec.RecommendedAtk - rec.RecommendedDef - rec.RecommendedHp - rec.RecommendedCrit;
                }
            }

            return rec;
        }

        private int GetAvailableStatPoints()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
                return 0;

            int level = SaveManager.Instance.CurrentData.player.level;
            int totalPoints = (level - 1) * 5; // 레벨당 5포인트
            int allocated = StatAllocationSystem.Instance != null ? StatAllocationSystem.Instance.TotalAllocatedPoints : 0;
            return Mathf.Max(0, totalPoints - allocated);
        }

        // ── UX-27: 던전 추천 ──

        /// <summary>
        /// 현재 가장 부족한 재화를 분석하여 추천 던전을 반환한다.
        /// </summary>
        public DungeonRecommendation GetRecommendedDungeon()
        {
            var rec = new DungeonRecommendation();

            if (CurrencyManager.Instance == null) return rec;

            // 재화별 부족도 평가: 낮을수록 부족
            var shortages = new List<(CurrencyType type, BigNumber amount, DungeonType dungeon, string reason)>
            {
                (CurrencyType.RuneFragment, CurrencyManager.Instance.GetAmount(CurrencyType.RuneFragment), DungeonType.Enhancement, "룬 조각 부족 → 강화 던전"),
                (CurrencyType.WeaponStone, CurrencyManager.Instance.GetAmount(CurrencyType.WeaponStone), DungeonType.Weapon, "무기 강화석 부족 → 무기 던전"),
                (CurrencyType.ClimbToken, CurrencyManager.Instance.GetAmount(CurrencyType.ClimbToken), DungeonType.Climber, "등반의 증표 부족 → 시련 던전"),
                (CurrencyType.HuntPoint, CurrencyManager.Instance.GetAmount(CurrencyType.HuntPoint), DungeonType.Equipment, "사냥 포인트 부족 → 장비 던전")
            };

            // 가장 적게 보유한 재화의 던전 추천
            shortages.Sort((a, b) => a.amount.CompareTo(b.amount));
            rec.RecommendedType = shortages[0].dungeon;
            rec.Reason = shortages[0].reason;
            rec.ShortCurrencyType = shortages[0].type;
            rec.CurrentAmount = shortages[0].amount;

            return rec;
        }

        // ── UX-28: 패널별 추천 버튼 지원 ──

        /// <summary>
        /// 추천 장비를 자동 장착한다.
        /// </summary>
        public int AutoEquipRecommended()
        {
            var recommendations = GetRecommendedEquipment();
            int equipped = 0;

            for (int i = 0; i < recommendations.Count; i++)
            {
                var rec = recommendations[i];
                if (EquipmentManager.Instance != null)
                {
                    EquipmentManager.Instance.Equip(rec.Instance.instanceId);
                    equipped++;
                }
            }

            return equipped;
        }

        /// <summary>
        /// 추천 스탯을 자동 분배한다.
        /// </summary>
        public int AutoAllocateStats()
        {
            if (StatAllocationSystem.Instance == null) return 0;

            var rec = GetRecommendedStatDistribution();
            int total = 0;

            if (rec.RecommendedAtk > 0)
            {
                StatAllocationSystem.Instance.AllocatePoints("atk", rec.RecommendedAtk);
                total += rec.RecommendedAtk;
            }
            if (rec.RecommendedDef > 0)
            {
                StatAllocationSystem.Instance.AllocatePoints("def", rec.RecommendedDef);
                total += rec.RecommendedDef;
            }
            if (rec.RecommendedHp > 0)
            {
                StatAllocationSystem.Instance.AllocatePoints("hp", rec.RecommendedHp);
                total += rec.RecommendedHp;
            }
            if (rec.RecommendedCrit > 0)
            {
                StatAllocationSystem.Instance.AllocatePoints("crit", rec.RecommendedCrit);
                total += rec.RecommendedCrit;
            }
            if (rec.RecommendedAccuracy > 0)
            {
                StatAllocationSystem.Instance.AllocatePoints("accuracy", rec.RecommendedAccuracy);
                total += rec.RecommendedAccuracy;
            }

            return total;
        }
    }

    // ── 추천 데이터 구조체 ──

    public struct EquipmentRecommendation
    {
        public EquipmentSlot Slot;
        public EquipmentInstance Instance;
        public int CpGain;
        public string DisplayName;
    }

    public struct StatDistributionRecommendation
    {
        public int AtkPercent;
        public int DefPercent;
        public int HpPercent;
        public int CritPercent;
        public int AccuracyPercent;
        public string Description;
        public int RecommendedAtk;
        public int RecommendedDef;
        public int RecommendedHp;
        public int RecommendedCrit;
        public int RecommendedAccuracy;
    }

    public struct DungeonRecommendation
    {
        public DungeonType RecommendedType;
        public string Reason;
        public CurrencyType ShortCurrencyType;
        public BigNumber CurrentAmount;
    }
}
