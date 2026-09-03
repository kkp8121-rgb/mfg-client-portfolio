using System;
using UnityEngine;
using MkLike.Core;
using MkLike.Combat;
using MkLike.Economy;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 매일 접속 시 랜덤 축복 1개를 부여하는 시스템.
    /// 24시간 지속되며, 루비 50으로 재롤 가능 (1일 3회).
    /// CombatStats modifier를 통해 플레이어 스탯에 반영된다.
    /// </summary>
    public class DailyBlessingSystem : MonoBehaviour
    {
        public static DailyBlessingSystem Instance { get; private set; }

        private const int MAX_REROLLS_PER_DAY = 3;
        private const int REROLL_COST_RUBY = 50;
        private const string MODIFIER_KEY_PREFIX = "blessing_";

        // PlayerPrefs 키
        private const string PREF_BLESSING_TYPE = "blessing_type";
        private const string PREF_BLESSING_DATE = "blessing_date";
        private const string PREF_BLESSING_REROLLS = "blessing_rerolls";

        // 가중 랜덤 확률 (합계 100)
        private static readonly (BlessingType type, int weight)[] BlessingWeights =
        {
            (BlessingType.Luck, 25),
            (BlessingType.Warrior, 25),
            (BlessingType.Guardian, 20),
            (BlessingType.Explorer, 20),
            (BlessingType.Blessed, 10)
        };

        [SerializeField] private BlessingType _currentBlessing;
        [SerializeField] private bool _hasBlessing;
        [SerializeField] private int _rerollsRemaining = MAX_REROLLS_PER_DAY;

        /// <summary>현재 활성 축복</summary>
        public BlessingType CurrentBlessing => _currentBlessing;

        /// <summary>축복 활성 여부</summary>
        public bool HasBlessing => _hasBlessing;

        /// <summary>오늘 남은 재롤 횟수</summary>
        public int RerollsRemaining => _rerollsRemaining;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            CheckDailyReset();

            if (!_hasBlessing)
            {
                GrantRandomBlessing();
            }
        }

        /// <summary>
        /// 날짜 변경 시 축복/재롤을 리셋한다.
        /// 저장된 날짜와 현재 날짜가 다르면 축복을 제거하고 재롤 횟수를 초기화한다.
        /// </summary>
        public void CheckDailyReset()
        {
            string savedDate = PlayerPrefs.GetString(PREF_BLESSING_DATE, "");
            string today = DateTime.Now.ToString("yyyy-MM-dd");

            if (savedDate == today)
            {
                // 같은 날 — 저장된 축복 복원
                _hasBlessing = PlayerPrefs.HasKey(PREF_BLESSING_TYPE);
                if (_hasBlessing)
                {
                    _currentBlessing = (BlessingType)PlayerPrefs.GetInt(PREF_BLESSING_TYPE, 0);
                    _rerollsRemaining = PlayerPrefs.GetInt(PREF_BLESSING_REROLLS, MAX_REROLLS_PER_DAY);
                    ApplyBlessingModifiers(_currentBlessing);
                }
            }
            else
            {
                // 날짜 변경 — 리셋
                RemoveBlessingModifiers();
                _hasBlessing = false;
                _rerollsRemaining = MAX_REROLLS_PER_DAY;
                PlayerPrefs.SetString(PREF_BLESSING_DATE, today);
                PlayerPrefs.SetInt(PREF_BLESSING_REROLLS, MAX_REROLLS_PER_DAY);
                PlayerPrefs.DeleteKey(PREF_BLESSING_TYPE);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// 가중 랜덤으로 축복을 부여하고 CombatStats modifier를 적용한다.
        /// </summary>
        public void GrantRandomBlessing()
        {
            RemoveBlessingModifiers();

            _currentBlessing = SelectWeightedRandom();
            _hasBlessing = true;

            ApplyBlessingModifiers(_currentBlessing);
            SaveBlessingState();

            EventBus<BlessingChangedEvent>.Publish(new BlessingChangedEvent
            {
                Type = _currentBlessing,
                IsReroll = false
            });

#if UNITY_EDITOR
            Debug.Log($"[DailyBlessingSystem] 축복 부여: {_currentBlessing}");
#endif
        }

        /// <summary>
        /// 루비 50을 소비하여 축복을 재롤한다.
        /// 1일 3회 제한. 성공 시 true를 반환한다.
        /// </summary>
        public bool TryReroll()
        {
            if (_rerollsRemaining <= 0)
            {
#if UNITY_EDITOR
                Debug.Log("[DailyBlessingSystem] 재롤 횟수 소진");
#endif
                return false;
            }

            var currencyManager = CurrencyManager.Instance;
            if (currencyManager == null)
            {
                Debug.LogWarning("[DailyBlessingSystem] CurrencyManager 없음");
                return false;
            }

            if (!currencyManager.Spend(CurrencyType.Ruby, REROLL_COST_RUBY))
            {
#if UNITY_EDITOR
                Debug.Log("[DailyBlessingSystem] 루비 부족");
#endif
                return false;
            }

            RemoveBlessingModifiers();

            _currentBlessing = SelectWeightedRandom();
            _rerollsRemaining--;

            ApplyBlessingModifiers(_currentBlessing);
            SaveBlessingState();

            EventBus<BlessingChangedEvent>.Publish(new BlessingChangedEvent
            {
                Type = _currentBlessing,
                IsReroll = true
            });

#if UNITY_EDITOR
            Debug.Log($"[DailyBlessingSystem] 재롤 결과: {_currentBlessing} (남은 횟수: {_rerollsRemaining})");
#endif
            return true;
        }

        /// <summary>
        /// 축복별 CombatStats modifier를 적용한다.
        /// </summary>
        private void ApplyBlessingModifiers(BlessingType type)
        {
            var player = FindFirstObjectByType<PlayerCharacter>();
            if (player == null) return;

            var stats = player.GetComponent<CombatStats>();
            if (stats == null) return;

            stats.SetCpReason("축복");
            switch (type)
            {
                case BlessingType.Luck:
                    stats.AddModifier(MODIFIER_KEY_PREFIX + "gold",
                        new StatModifier(ModifierSource.Blessing, "blessing_luck", StatType.GoldBonus, 0f, 0.5f));
                    break;

                case BlessingType.Warrior:
                    stats.AddModifier(MODIFIER_KEY_PREFIX + "atk",
                        new StatModifier(ModifierSource.Blessing, "blessing_warrior", StatType.Atk, 0f, 0.3f));
                    break;

                case BlessingType.Guardian:
                    stats.AddModifier(MODIFIER_KEY_PREFIX + "def",
                        new StatModifier(ModifierSource.Blessing, "blessing_guardian", StatType.Def, 0f, 0.3f));
                    break;

                case BlessingType.Explorer:
                    stats.AddModifier(MODIFIER_KEY_PREFIX + "exp",
                        new StatModifier(ModifierSource.Blessing, "blessing_explorer", StatType.ExpBonus, 0f, 0.5f));
                    break;

                case BlessingType.Blessed:
                    stats.AddModifier(MODIFIER_KEY_PREFIX + "atk",
                        new StatModifier(ModifierSource.Blessing, "blessing_all_atk", StatType.Atk, 0f, 0.2f));
                    stats.AddModifier(MODIFIER_KEY_PREFIX + "def",
                        new StatModifier(ModifierSource.Blessing, "blessing_all_def", StatType.Def, 0f, 0.2f));
                    stats.AddModifier(MODIFIER_KEY_PREFIX + "hp",
                        new StatModifier(ModifierSource.Blessing, "blessing_all_hp", StatType.MaxHp, 0f, 0.2f));
                    stats.AddModifier(MODIFIER_KEY_PREFIX + "crit",
                        new StatModifier(ModifierSource.Blessing, "blessing_all_crit", StatType.CritRate, 0f, 0.2f));
                    stats.AddModifier(MODIFIER_KEY_PREFIX + "atkspd",
                        new StatModifier(ModifierSource.Blessing, "blessing_all_atkspd", StatType.AttackSpeed, 0f, 0.2f));
                    stats.AddModifier(MODIFIER_KEY_PREFIX + "movespd",
                        new StatModifier(ModifierSource.Blessing, "blessing_all_movespd", StatType.MoveSpeed, 0f, 0.2f));
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// 현재 축복의 CombatStats modifier를 모두 제거한다.
        /// </summary>
        private void RemoveBlessingModifiers()
        {
            var player = FindFirstObjectByType<PlayerCharacter>();
            if (player == null) return;

            var stats = player.GetComponent<CombatStats>();
            if (stats == null) return;

            stats.SetCpReason("축복");
            stats.ClearModifiers(ModifierSource.Blessing);
        }

        /// <summary>
        /// 가중 랜덤으로 축복 유형을 선택한다.
        /// </summary>
        private BlessingType SelectWeightedRandom()
        {
            if (BlessingWeights == null || BlessingWeights.Length == 0)
            {
                Debug.LogError("[DailyBlessingSystem] BlessingWeights 비어 있음");
                return default;
            }

            int totalWeight = 0;
            for (int i = 0; i < BlessingWeights.Length; i++)
            {
                totalWeight += BlessingWeights[i].weight;
            }

            int roll = UnityEngine.Random.Range(0, totalWeight);
            int cumulative = 0;

            for (int i = 0; i < BlessingWeights.Length; i++)
            {
                cumulative += BlessingWeights[i].weight;
                if (roll < cumulative)
                {
                    return BlessingWeights[i].type;
                }
            }

            return BlessingWeights[BlessingWeights.Length - 1].type;
        }

        /// <summary>
        /// 축복 상태를 PlayerPrefs에 저장한다.
        /// </summary>
        private void SaveBlessingState()
        {
            PlayerPrefs.SetInt(PREF_BLESSING_TYPE, (int)_currentBlessing);
            PlayerPrefs.SetString(PREF_BLESSING_DATE, DateTime.Now.ToString("yyyy-MM-dd"));
            PlayerPrefs.SetInt(PREF_BLESSING_REROLLS, _rerollsRemaining);
            PlayerPrefs.Save();
        }
    }
}
