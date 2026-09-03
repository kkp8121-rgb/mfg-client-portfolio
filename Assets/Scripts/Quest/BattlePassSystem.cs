using System.Collections.Generic;
using UnityEngine;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Data;
using MkLike.Economy;
using MkLike.Utils;

namespace MkLike.Quest
{
    /// <summary>
    /// 배틀패스 시스템.
    /// 무료/프리미엄 트랙, BXP 획득 → 레벨업, 보상 수령을 관리한다.
    /// </summary>
    public class BattlePassSystem : MonoBehaviour
    {
        public static BattlePassSystem Instance { get; private set; }

        [SerializeField] private SeasonDataSO _currentSeasonData;
        [SerializeField] private List<BattlePassRewardSO> _rewards = new();

        /// <summary>현재 배틀패스 레벨</summary>
        public int CurrentLevel { get; private set; }

        /// <summary>현재 레벨 내 누적 BXP</summary>
        public int CurrentBxp { get; private set; }

        /// <summary>프리미엄 패스 보유 여부</summary>
        public bool IsPremium { get; private set; }

        /// <summary>수령 완료한 무료 보상 레벨 목록</summary>
        private readonly HashSet<int> _claimedFreeRewards = new();

        /// <summary>수령 완료한 프리미엄 보상 레벨 목록</summary>
        private readonly HashSet<int> _claimedPremiumRewards = new();

        /// <summary>레벨당 보상 빠른 검색용 딕셔너리</summary>
        private readonly Dictionary<int, BattlePassRewardSO> _rewardByLevel = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // _rewards가 비어있으면 Resources/Data/BattlePass/ 폴백 로드
            if (_rewards == null || _rewards.Count == 0)
            {
                var loaded = Resources.LoadAll<BattlePassRewardSO>("Data/BattlePass");
                if (loaded != null && loaded.Length > 0)
                {
                    _rewards = new List<BattlePassRewardSO>(loaded);
                    Debug.Log($"[BattlePassSystem] Resources/Data/BattlePass에서 {loaded.Length}개 보상 SO 폴백 로드");
                }
                else
                {
                    Debug.LogWarning("[BattlePassSystem] _rewards 비어있음 + Resources 폴더도 비어있음. " +
                        "'mkLike/BattlePass/Generate Default Rewards (50)' 메뉴 실행 필요.");
                }
            }
        }

        private void OnEnable()
        {
            EventBus.SubscribeSticky<Core.BeforeSaveEvent>(OnBeforeSave);
            EventBus.SubscribeSticky<Core.LoadCompletedEvent>(OnLoadCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<Core.BeforeSaveEvent>(OnBeforeSave);
            EventBus.Unsubscribe<Core.LoadCompletedEvent>(OnLoadCompleted);
        }

        private void OnBeforeSave(Core.BeforeSaveEvent evt) => SyncToSaveData();

        private void OnLoadCompleted(Core.LoadCompletedEvent evt)
        {
            var save = SaveManager.Instance?.CurrentData;
            if (save == null) return;
            Initialize(save.battlePass);
        }

        /// <summary>
        /// 세이브 데이터에서 배틀패스 상태를 로드하여 초기화한다.
        /// LoadCompletedEvent 구독으로 자동 호출. 외부에서도 호출 가능(호환성 유지).
        /// </summary>
        public void Initialize(BattlePassSaveData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[BattlePassSystem] BattlePassSaveData가 null — 기본값으로 초기화");
                data = new BattlePassSaveData();
            }

            CurrentLevel = data.level;
            CurrentBxp = data.currentBxp;
            IsPremium = data.isPremium;

            _claimedFreeRewards.Clear();
            _claimedPremiumRewards.Clear();

            if (data.claimedFreeRewards != null)
            {
                for (int i = 0; i < data.claimedFreeRewards.Count; i++)
                {
                    _claimedFreeRewards.Add(data.claimedFreeRewards[i]);
                }
            }

            if (data.claimedPremiumRewards != null)
            {
                for (int i = 0; i < data.claimedPremiumRewards.Count; i++)
                {
                    _claimedPremiumRewards.Add(data.claimedPremiumRewards[i]);
                }
            }

            BuildRewardLookup();

            Debug.Log($"[BattlePassSystem] 초기화 완료 — Lv{CurrentLevel}, BXP:{CurrentBxp}, Premium:{IsPremium}");
        }

        /// <summary>
        /// 보상 SO 리스트를 레벨별 딕셔너리로 구성한다.
        /// _rewards가 비어있으면 Resources/Data/BattlePass/ 에서 lazy-load.
        /// (Awake 타이밍 or Inspector 미할당 양쪽 모두 방어)
        /// </summary>
        private void BuildRewardLookup()
        {
            if (_rewards == null || _rewards.Count == 0)
            {
                var loaded = Resources.LoadAll<BattlePassRewardSO>("Data/BattlePass");
                if (loaded != null && loaded.Length > 0)
                {
                    _rewards = new List<BattlePassRewardSO>(loaded);
                    Debug.Log($"[BattlePassSystem] BuildRewardLookup lazy-load {loaded.Length}개");
                }
            }

            _rewardByLevel.Clear();
            for (int i = 0; i < _rewards.Count; i++)
            {
                var reward = _rewards[i];
                if (reward == null) continue;
                _rewardByLevel[reward.Level] = reward;
            }
        }

        /// <summary>
        /// GetReward도 lazy-load 시도 — 패널에서 호출 시 rewards 비어있으면 즉시 로드.
        /// </summary>
        public BattlePassRewardSO GetRewardLazy(int level)
        {
            if (_rewardByLevel.Count == 0) BuildRewardLookup();
            _rewardByLevel.TryGetValue(level, out var reward);
            return reward;
        }

        /// <summary>
        /// 시즌 데이터와 보상 목록을 설정한다.
        /// </summary>
        public void SetSeasonData(SeasonDataSO seasonData, List<BattlePassRewardSO> rewards)
        {
            _currentSeasonData = seasonData;
            if (rewards != null)
            {
                _rewards = rewards;
            }
            BuildRewardLookup();
        }

        /// <summary>
        /// BXP를 획득한다. 레벨업 조건 충족 시 자동으로 레벨업 처리한다.
        /// </summary>
        /// <param name="amount">획득할 BXP 양</param>
        public void AddBxp(int amount)
        {
            if (amount <= 0) return;
            if (_currentSeasonData == null)
            {
                Debug.LogWarning("[BattlePassSystem] SeasonData가 설정되지 않음");
                return;
            }

            int maxLevel = _currentSeasonData.MaxLevel;
            if (CurrentLevel >= maxLevel) return;

            CurrentBxp += amount;
            int bxpPerLevel = _currentSeasonData.BxpPerLevel;

            // 레벨업 처리 (다중 레벨업 가능)
            while (CurrentBxp >= bxpPerLevel && CurrentLevel < maxLevel)
            {
                CurrentBxp -= bxpPerLevel;
                CurrentLevel++;

                // SFX: 배틀패스 레벨업
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySfx(SfxType.LevelUp);

                EventBus.Publish(new BattlePassLevelUpEvent
                {
                    Level = CurrentLevel,
                    IsMilestone = CurrentLevel % 10 == 0
                });

                Debug.Log($"[BattlePassSystem] 레벨업! Lv{CurrentLevel}");
            }

            // 최대 레벨 도달 시 잔여 BXP 0으로
            if (CurrentLevel >= maxLevel)
            {
                CurrentBxp = 0;
            }

            SaveManager.Instance?.Save();

            EventBus.Publish(new BattlePassBxpGainedEvent
            {
                Amount = amount,
                CurrentBxp = CurrentBxp,
                CurrentLevel = CurrentLevel
            });
        }

        /// <summary>
        /// 무료 트랙 보상을 수령한다.
        /// </summary>
        /// <param name="level">수령할 레벨</param>
        /// <returns>수령 성공 여부</returns>
        public bool ClaimFreeReward(int level)
        {
            if (level > CurrentLevel)
            {
                Debug.LogWarning($"[BattlePassSystem] 무료 보상 수령 실패 — 레벨 미달 (현재: {CurrentLevel}, 필요: {level})");
                return false;
            }

            if (_claimedFreeRewards.Contains(level))
            {
                Debug.LogWarning($"[BattlePassSystem] 무료 보상 수령 실패 — 이미 수령함 (Lv{level})");
                return false;
            }

            if (!_rewardByLevel.TryGetValue(level, out var reward))
            {
                Debug.LogWarning($"[BattlePassSystem] 무료 보상 수령 실패 — 보상 데이터 없음 (Lv{level})");
                return false;
            }

            if (reward.FreeRewardAmount <= 0) return false;

            // CurrencyManager를 통해 보상 지급
            if (reward.FreeRewardAmount > 0 && Economy.CurrencyManager.Instance != null)
            {
                Economy.CurrencyManager.Instance.Add(reward.FreeRewardType, reward.FreeRewardAmount);
            }

            _claimedFreeRewards.Add(level);
            SaveManager.Instance?.Save();

            // SFX: 보상 수령
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(SfxType.UiReward);

            EventBus.Publish(new BattlePassRewardClaimedEvent
            {
                Level = level,
                IsPremium = false,
                RewardType = reward.FreeRewardType,
                RewardAmount = reward.FreeRewardAmount
            });

            // 2026-04-23 이슈 15 FeedbackBus: 배틀패스 무료 보상 Toast
            MkLike.Core.FeedbackBus.Emit(
                MkLike.Core.FeedbackKind.Generic,
                $"배틀패스 Lv.{level} 보상!");

            Debug.Log($"[BattlePassSystem] 무료 보상 수령 — Lv{level}: {reward.FreeRewardType} x{reward.FreeRewardAmount}");
            return true;
        }

        /// <summary>
        /// 프리미엄 트랙 보상을 수령한다.
        /// </summary>
        /// <param name="level">수령할 레벨</param>
        /// <returns>수령 성공 여부</returns>
        public bool ClaimPremiumReward(int level)
        {
            if (!IsPremium)
            {
                Debug.LogWarning("[BattlePassSystem] 프리미엄 보상 수령 실패 — 프리미엄 패스 미보유");
                return false;
            }

            if (level > CurrentLevel)
            {
                Debug.LogWarning($"[BattlePassSystem] 프리미엄 보상 수령 실패 — 레벨 미달 (현재: {CurrentLevel}, 필요: {level})");
                return false;
            }

            if (_claimedPremiumRewards.Contains(level))
            {
                Debug.LogWarning($"[BattlePassSystem] 프리미엄 보상 수령 실패 — 이미 수령함 (Lv{level})");
                return false;
            }

            if (!_rewardByLevel.TryGetValue(level, out var reward))
            {
                Debug.LogWarning($"[BattlePassSystem] 프리미엄 보상 수령 실패 — 보상 데이터 없음 (Lv{level})");
                return false;
            }

            if (reward.PremiumRewardAmount <= 0) return false;

            if (reward.PremiumRewardAmount > 0 && Economy.CurrencyManager.Instance != null)
            {
                Economy.CurrencyManager.Instance.Add(reward.PremiumRewardType, reward.PremiumRewardAmount);
            }

            _claimedPremiumRewards.Add(level);
            SaveManager.Instance?.Save();

            // SFX: 프리미엄 보상 수령
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(SfxType.UiReward);

            EventBus.Publish(new BattlePassRewardClaimedEvent
            {
                Level = level,
                IsPremium = true,
                RewardType = reward.PremiumRewardType,
                RewardAmount = reward.PremiumRewardAmount
            });

            Debug.Log($"[BattlePassSystem] 프리미엄 보상 수령 — Lv{level}: {reward.PremiumRewardType} x{reward.PremiumRewardAmount}");
            return true;
        }

        /// <summary>
        /// 프리미엄 패스를 구매 활성화한다.
        /// 이미 도달한 프리미엄 보상을 소급 수령 가능하게 된다.
        /// </summary>
        public void ActivatePremium()
        {
            if (IsPremium)
            {
                Debug.LogWarning("[BattlePassSystem] 이미 프리미엄 패스 보유");
                return;
            }

            IsPremium = true;
            SaveManager.Instance?.Save();

            EventBus.Publish(new BattlePassPremiumActivatedEvent());

            Debug.Log("[BattlePassSystem] 프리미엄 패스 활성화");
        }

        /// <summary>
        /// 무료 보상 수령 가능 여부를 확인한다.
        /// </summary>
        public bool CanClaimFreeReward(int level)
        {
            return level <= CurrentLevel
                   && !_claimedFreeRewards.Contains(level)
                   && _rewardByLevel.ContainsKey(level);
        }

        /// <summary>
        /// 프리미엄 보상 수령 가능 여부를 확인한다.
        /// </summary>
        public bool CanClaimPremiumReward(int level)
        {
            return IsPremium
                   && level <= CurrentLevel
                   && !_claimedPremiumRewards.Contains(level)
                   && _rewardByLevel.ContainsKey(level);
        }

        /// <summary>
        /// 현재 레벨의 BXP 진행률을 반환한다 (0~1).
        /// </summary>
        public float GetLevelProgress()
        {
            if (_currentSeasonData == null || _currentSeasonData.BxpPerLevel <= 0)
                return 0f;

            if (CurrentLevel >= _currentSeasonData.MaxLevel)
                return 1f;

            return (float)CurrentBxp / _currentSeasonData.BxpPerLevel;
        }

        /// <summary>
        /// 지정 레벨의 보상 데이터를 반환한다. 딕셔너리 비어있으면 lazy-load.
        /// </summary>
        public BattlePassRewardSO GetReward(int level)
        {
            if (_rewardByLevel.Count == 0) BuildRewardLookup();
            _rewardByLevel.TryGetValue(level, out var reward);
            return reward;
        }

        /// <summary>
        /// 시즌 리셋. 모든 배틀패스 진행을 초기화한다.
        /// </summary>
        public void ResetProgress()
        {
            CurrentLevel = 0;
            CurrentBxp = 0;
            IsPremium = false;
            _claimedFreeRewards.Clear();
            _claimedPremiumRewards.Clear();

            SaveManager.Instance?.Save();

            Debug.Log("[BattlePassSystem] 진행 리셋 완료");
        }

        /// <summary>
        /// CurrentData.battlePass에 런타임 상태를 기록한다. BeforeSaveEvent에서 호출된다.
        /// 디스크 write는 SaveManager가 담당 — 즉시 저장이 필요한 지점은 SaveManager.Save() 직접 호출.
        /// </summary>
        private void SyncToSaveData()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
                return;

            var bp = SaveManager.Instance.CurrentData.battlePass;
            bp.level = CurrentLevel;
            bp.currentBxp = CurrentBxp;
            bp.isPremium = IsPremium;
            bp.claimedFreeRewards = new List<int>(_claimedFreeRewards);
            bp.claimedPremiumRewards = new List<int>(_claimedPremiumRewards);
        }

        // ── UI 호환 프로퍼티 ──

        /// <summary>현재 시즌 이름</summary>
        public string CurrentSeasonName => _currentSeasonData != null ? _currentSeasonData.DisplayName : "시즌 없음";

        /// <summary>시즌 남은 일수</summary>
        public int RemainingDays
        {
            get
            {
                if (SeasonManager.Instance == null || !SeasonManager.Instance.IsSeasonActive)
                    return 0;
                return Mathf.Max(0, (int)SeasonManager.Instance.RemainingTime.TotalDays);
            }
        }

        /// <summary>레벨당 필요 BXP</summary>
        public int BxpPerLevel => _currentSeasonData != null ? _currentSeasonData.BxpPerLevel : 1000;

        /// <summary>프리미엄 패스 활성 여부</summary>
        public bool IsPremiumActive => IsPremium;

        /// <summary>무료 보상 수령 완료 여부</summary>
        public bool IsFreeRewardClaimed(int level) => _claimedFreeRewards.Contains(level);

        /// <summary>프리미엄 보상 수령 완료 여부</summary>
        public bool IsPremiumRewardClaimed(int level) => _claimedPremiumRewards.Contains(level);

        /// <summary>무료 보상 설명 문자열</summary>
        public string GetFreeRewardDescription(int level)
        {
            var reward = GetReward(level);
            if (reward == null || reward.FreeRewardAmount <= 0) return "-";
            return $"{reward.FreeRewardType} x{reward.FreeRewardAmount}";
        }

        /// <summary>프리미엄 보상 설명 문자열</summary>
        public string GetPremiumRewardDescription(int level)
        {
            var reward = GetReward(level);
            if (reward == null || reward.PremiumRewardAmount <= 0) return "-";
            return $"{reward.PremiumRewardType} x{reward.PremiumRewardAmount}";
        }

        /// <summary>프리미엄 패스 구매 (루비 소비)</summary>
        public bool PurchasePremium()
        {
            if (IsPremium) return false;
            // 프리미엄 패스 비용: 루비 2000
            if (Economy.CurrencyManager.Instance != null &&
                !Economy.CurrencyManager.Instance.Spend(CurrencyType.Ruby, 2000))
            {
                Debug.LogWarning("[BattlePassSystem] 프리미엄 구매 실패 — 루비 부족");
                return false;
            }
            ActivatePremium();
            return true;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
