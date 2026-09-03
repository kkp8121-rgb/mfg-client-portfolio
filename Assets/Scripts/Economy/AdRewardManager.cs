using System;
using System.Collections.Generic;
using UnityEngine;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.Economy
{
    /// <summary>
    /// 광고 리워드 매니저.
    /// 광고 시청 → 보상 적용 흐름을 관리한다. 실제 광고 SDK 연동 전 placeholder 구현.
    /// 일일 시청 횟수를 PlayerPrefs로 추적하며, 날짜가 바뀌면 자동 리셋된다.
    /// </summary>
    public class AdRewardManager : MonoBehaviour
    {
        public static AdRewardManager Instance { get; private set; }

        private const string PREFS_PREFIX = "AdReward_";
        private const string PREFS_DATE_KEY = "AdReward_LastDate";
        private const float BOOST_DURATION_SECONDS = 1800f; // 30분

        /// <summary>슬롯별 일일 제한 설정 (기획 기반 기본값)</summary>
        private static readonly Dictionary<AdRewardType, int> DEFAULT_LIMITS = new()
        {
            { AdRewardType.OfflineReward2x,   3 },
            { AdRewardType.DungeonExtraEntry,  2 },
            { AdRewardType.GoldBoost,          5 },
            { AdRewardType.FreeSummon,         2 },
            { AdRewardType.Revive,             0 }, // 무제한
            { AdRewardType.ExpBoost,           3 },
            { AdRewardType.EnhanceProtect,     0 }, // 무제한
            { AdRewardType.DailyBonusBox,      1 }
        };

        /// <summary>슬롯별 오늘 시청 횟수</summary>
        private readonly Dictionary<AdRewardType, int> _watchCounts = new();

        // 부스트 상태
        private bool _isGoldBoostActive;
        private float _goldBoostEndTime;
        private bool _isExpBoostActive;
        private float _expBoostEndTime;

        // 1회성 플래그 (다른 시스템이 참조 후 소비)
        private bool _offlineReward2xFlag;
        private bool _reviveFlag;
        private bool _enhanceProtectFlag;

        public bool IsGoldBoostActive => _isGoldBoostActive && Time.realtimeSinceStartup < _goldBoostEndTime;
        public bool IsExpBoostActive => _isExpBoostActive && Time.realtimeSinceStartup < _expBoostEndTime;
        public float GoldBoostRemainingSeconds => _isGoldBoostActive ? Mathf.Max(0f, _goldBoostEndTime - Time.realtimeSinceStartup) : 0f;
        public float ExpBoostRemainingSeconds => _isExpBoostActive ? Mathf.Max(0f, _expBoostEndTime - Time.realtimeSinceStartup) : 0f;

        /// <summary>
        /// 오프라인 보상 2배 플래그. OfflineRewardSystem이 참조 후 ConsumeOfflineReward2x()로 소비한다.
        /// </summary>
        public bool HasOfflineReward2x => _offlineReward2xFlag;

        /// <summary>
        /// 부활 플래그. 보스전 사망 시 참조 후 ConsumeRevive()로 소비한다.
        /// </summary>
        public bool HasRevive => _reviveFlag;

        /// <summary>
        /// 강화 보호 플래그. 13성+ 강화 시 참조 후 ConsumeEnhanceProtect()로 소비한다.
        /// </summary>
        public bool HasEnhanceProtect => _enhanceProtectFlag;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            LoadDailyCounts();
        }

        private void Update()
        {
            // 부스트 만료 체크
            if (_isGoldBoostActive && Time.realtimeSinceStartup >= _goldBoostEndTime)
            {
                _isGoldBoostActive = false;
                Debug.Log("[AdRewardManager] 골드 2배 부스트 만료");
            }

            if (_isExpBoostActive && Time.realtimeSinceStartup >= _expBoostEndTime)
            {
                _isExpBoostActive = false;
                Debug.Log("[AdRewardManager] 경험치 2배 부스트 만료");
            }
        }

        /// <summary>
        /// 해당 슬롯의 광고를 시청할 수 있는지 확인한다.
        /// </summary>
        public bool CanWatchAd(AdRewardType type)
        {
            CheckDateReset();
            int limit = GetDailyLimit(type);
            if (limit <= 0) return true; // 0 = 무제한
            int used = GetUsedCount(type);
            return used < limit;
        }

        /// <summary>
        /// 해당 슬롯의 오늘 남은 시청 횟수를 반환한다. 무제한이면 int.MaxValue.
        /// </summary>
        public int GetRemainingCount(AdRewardType type)
        {
            CheckDateReset();
            int limit = GetDailyLimit(type);
            if (limit <= 0) return int.MaxValue; // 무제한
            int used = GetUsedCount(type);
            return Mathf.Max(0, limit - used);
        }

        /// <summary>
        /// 광고를 시청하고 보상을 적용한다.
        /// 실제 광고 SDK 호출은 placeholder — 즉시 성공 처리.
        /// </summary>
        public void WatchAd(AdRewardType type)
        {
            if (!CanWatchAd(type))
            {
                Debug.LogWarning($"[AdRewardManager] 일일 제한 초과: {type}");
                return;
            }

            // TODO: 실제 광고 SDK 호출 (UnityAds, AdMob 등)
            // 광고 시청 완료 콜백에서 OnAdCompleted(type) 호출하도록 변경 예정
            Debug.Log($"[AdRewardManager] 광고 시청 시작: {type} (placeholder — 즉시 완료)");

            OnAdCompleted(type);
        }

        /// <summary>
        /// 오프라인 보상 2배 플래그를 소비한다.
        /// </summary>
        public void ConsumeOfflineReward2x()
        {
            _offlineReward2xFlag = false;
        }

        /// <summary>
        /// 부활 플래그를 소비한다.
        /// </summary>
        public void ConsumeRevive()
        {
            _reviveFlag = false;
        }

        /// <summary>
        /// 강화 보호 플래그를 소비한다.
        /// </summary>
        public void ConsumeEnhanceProtect()
        {
            _enhanceProtectFlag = false;
        }

        private void OnAdCompleted(AdRewardType type)
        {
            IncrementCount(type);
            ApplyReward(type);

            int remaining = GetRemainingCount(type);
            EventBus<AdRewardEvent>.Publish(new AdRewardEvent
            {
                Type = type,
                RemainingToday = remaining
            });

            Debug.Log($"[AdRewardManager] 보상 적용 완료: {type}, 잔여 {(remaining == int.MaxValue ? "무제한" : remaining.ToString())}회");
        }

        private void ApplyReward(AdRewardType type)
        {
            switch (type)
            {
                case AdRewardType.OfflineReward2x:
                    _offlineReward2xFlag = true;
                    break;

                case AdRewardType.DungeonExtraEntry:
                    ApplyDungeonExtraEntry();
                    break;

                case AdRewardType.GoldBoost:
                    _isGoldBoostActive = true;
                    _goldBoostEndTime = Time.realtimeSinceStartup + BOOST_DURATION_SECONDS;
                    Debug.Log("[AdRewardManager] 골드 2배 부스트 활성화 (30분)");
                    break;

                case AdRewardType.FreeSummon:
                    ApplyFreeSummon();
                    break;

                case AdRewardType.Revive:
                    _reviveFlag = true;
                    break;

                case AdRewardType.ExpBoost:
                    _isExpBoostActive = true;
                    _expBoostEndTime = Time.realtimeSinceStartup + BOOST_DURATION_SECONDS;
                    Debug.Log("[AdRewardManager] 경험치 2배 부스트 활성화 (30분)");
                    break;

                case AdRewardType.EnhanceProtect:
                    _enhanceProtectFlag = true;
                    break;

                case AdRewardType.DailyBonusBox:
                    ApplyDailyBonusBox();
                    break;

                default:
                    Debug.LogWarning($"[AdRewardManager] 미처리 광고 타입: {type}");
                    break;
            }
        }

        private void ApplyDungeonExtraEntry()
        {
            // AdRewardEvent를 통해 DungeonManager가 구독하여 AddKeys(1) 처리
            // Economy → Dungeon 직접 참조 불가 (순환 의존), EventBus로 느슨한 결합
            Debug.Log("[AdRewardManager] 던전 추가 입장권 +1 (이벤트 기반 — DungeonManager 구독 필요)");
        }

        private void ApplyFreeSummon()
        {
            // GachaManager로 무료 소환 1회 실행 (무기 풀 기본)
            if (GachaManager.Instance != null)
            {
                GachaManager.Instance.Pull(GachaPoolType.Weapon);
                Debug.Log("[AdRewardManager] 무료 소환 1회 실행");
            }
            else if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.Add(CurrencyType.WeaponTicket, 1);
                Debug.Log("[AdRewardManager] GachaManager 없음 — 무기 소환권 1장 지급 (fallback)");
            }
        }

        private void ApplyDailyBonusBox()
        {
            // 랜덤 보상 지급: 골드, 루비, 소환권 중 랜덤
            if (CurrencyManager.Instance == null)
            {
                Debug.LogWarning("[AdRewardManager] CurrencyManager 없음 — 일일 보너스 미적용");
                return;
            }

            int roll = UnityEngine.Random.Range(0, 3);
            switch (roll)
            {
                case 0:
                    CurrencyManager.Instance.Add(CurrencyType.Gold, 10000);
                    Debug.Log("[AdRewardManager] 일일 보너스 상자: 골드 10,000");
                    break;
                case 1:
                    CurrencyManager.Instance.Add(CurrencyType.Ruby, 50);
                    Debug.Log("[AdRewardManager] 일일 보너스 상자: 루비 50");
                    break;
                case 2:
                    CurrencyManager.Instance.Add(CurrencyType.WeaponTicket, 1);
                    Debug.Log("[AdRewardManager] 일일 보너스 상자: 무기 소환권 1");
                    break;
            }
        }

        private int GetDailyLimit(AdRewardType type)
        {
            return DEFAULT_LIMITS.TryGetValue(type, out int limit) ? limit : 0;
        }

        private int GetUsedCount(AdRewardType type)
        {
            return _watchCounts.TryGetValue(type, out int count) ? count : 0;
        }

        private void IncrementCount(AdRewardType type)
        {
            if (!_watchCounts.ContainsKey(type))
                _watchCounts[type] = 0;
            _watchCounts[type]++;
            SaveDailyCounts();
        }

        private void CheckDateReset()
        {
            string savedDate = PlayerPrefs.GetString(PREFS_DATE_KEY, "");
            string today = DateTime.Today.ToString("yyyy-MM-dd");

            if (savedDate != today)
            {
                _watchCounts.Clear();
                PlayerPrefs.SetString(PREFS_DATE_KEY, today);
                ClearSavedCounts();
                Debug.Log("[AdRewardManager] 날짜 변경 — 일일 광고 횟수 리셋");
            }
        }

        private void LoadDailyCounts()
        {
            string savedDate = PlayerPrefs.GetString(PREFS_DATE_KEY, "");
            string today = DateTime.Today.ToString("yyyy-MM-dd");

            if (savedDate != today)
            {
                PlayerPrefs.SetString(PREFS_DATE_KEY, today);
                ClearSavedCounts();
                return;
            }

            foreach (AdRewardType type in Enum.GetValues(typeof(AdRewardType)))
            {
                string key = PREFS_PREFIX + type;
                int count = PlayerPrefs.GetInt(key, 0);
                if (count > 0)
                    _watchCounts[type] = count;
            }
        }

        private void SaveDailyCounts()
        {
            foreach (var kvp in _watchCounts)
            {
                string key = PREFS_PREFIX + kvp.Key;
                PlayerPrefs.SetInt(key, kvp.Value);
            }
            PlayerPrefs.Save();
        }

        private void ClearSavedCounts()
        {
            foreach (AdRewardType type in Enum.GetValues(typeof(AdRewardType)))
            {
                string key = PREFS_PREFIX + type;
                PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.Save();
        }
    }
}
