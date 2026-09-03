using System;
using System.Collections.Generic;
using UnityEngine;
using MkLike.Core;
using MkLike.Economy;
using MkLike.Utils;

namespace MkLike.Quest
{
    /// <summary>
    /// 업적 카테고리.
    /// </summary>
    public enum AchievementCategory
    {
        Combat,     // 전투 (처치수)
        Growth,     // 성장 (레벨)
        Equipment,  // 장비 (강화횟수)
        Collection, // 수집 (동료/유물 수)
        Costume,    // 코스튬
        Challenge,  // 챌린지
        Blessing,   // 축복
        Ad,         // 광고
        Prestige,   // 환생
        Compendium, // 도감
        Season,     // 시즌
        Pvp,        // PvP (아레나)
        Pet,        // 펫
        Guild,      // 길드
        ClimbingParty // 등반조합
    }

    /// <summary>
    /// 업적 조건 타입.
    /// </summary>
    public enum AchievementCondition
    {
        KillTotal,          // 총 몬스터 처치 수
        ReachLevel,         // 레벨 달성
        EnhanceCount,       // 장비 강화 횟수
        CompanionCount,     // 동료 보유 수
        RelicCount,         // 유물 보유 수
        GachaPullTotal,     // 총 가챠 횟수
        GoldEarnTotal,      // 총 골드 획득량
        StageCleared,       // 스테이지 클리어 수
        DungeonCleared,     // 던전 클리어 수
        JobAdvance,         // 전직 횟수
        CostumeCollect,     // 코스튬 수집 수
        CostumeSetComplete, // 코스튬 세트 완성 수
        ChallengeCleared,   // 챌린지 완료 수
        ChallengeStarsTotal,// 챌린지 별 누적
        BlessingReroll,     // 축복 재롤 횟수
        BlessingObtained,   // 축복 획득 횟수
        AdWatched,          // 광고 시청 횟수
        PrestigeCount,      // 환생 횟수
        CollectionTotal,    // 도감 수집 수
        SeasonRewardClaimed,// 시즌 보상 수령 수
        PvpVictory,         // PvP 승리 횟수
        PvpWinStreak,       // PvP 연승 기록
        PetMaxLevel,        // 펫 최고 레벨 달성
        PetEvolveCount,     // 펫 진화 횟수
        GuildContribute,    // 길드 기여 횟수
        GuildBossKill,      // 길드 보스 처치 수
        CostumeSynthesis,   // 코스튬 합성 횟수
        CostumeDisassemble, // 코스튬 분해 횟수
        ClimbingPartyCombination, // 등반조합 완성 수
        TitleCollect        // 칭호 수집 수
    }

    /// <summary>
    /// 업적 정의 데이터 (코드 내 정적 정의 또는 SO에서 로드).
    /// </summary>
    [Serializable]
    public class AchievementData
    {
        public string id;
        public string displayName;
        public string description;
        public AchievementCategory category;
        public AchievementCondition condition;
        public int requiredAmount;
        public CurrencyType rewardType;
        public int rewardAmount;
        // costumeRewardId: 2026-04-20 Costume 제거 + 2026-04-23 dead field 삭제
        /// <summary>칭호 보상 ID (비어있으면 칭호 보상 없음)</summary>
        public string titleRewardId;
        /// <summary>칭호 표시명 (titleRewardId가 설정된 경우)</summary>
        public string titleDisplayName;
    }

    /// <summary>
    /// 업적 진행 상태.
    /// </summary>
    [Serializable]
    public class AchievementProgress
    {
        public string achievementId;
        public int currentAmount;
        public bool isCompleted;
        public bool isClaimed;
    }

    /// <summary>
    /// 업적 완료 이벤트.
    /// </summary>
    public struct AchievementCompletedEvent : IEvent
    {
        public string AchievementId;
        public string DisplayName;
    }

    /// <summary>
    /// 업적 보상 수령 이벤트.
    /// </summary>
    public struct AchievementClaimedEvent : IEvent
    {
        public string AchievementId;
        public CurrencyType RewardType;
        public int RewardAmount;
    }

    /// <summary>
    /// 영구 달성 업적 시스템.
    /// 이벤트 구독으로 자동 진행 추적, 보상 수령 시 CurrencyManager.Add().
    /// </summary>
    public class AchievementSystem : MonoBehaviour
    {
        public static AchievementSystem Instance { get; private set; }

        /// <summary>업적 카탈로그 (id → data)</summary>
        private readonly Dictionary<string, AchievementData> _catalog = new();

        /// <summary>업적 진행도 (id → progress)</summary>
        private readonly Dictionary<string, AchievementProgress> _progressMap = new();

        /// <summary>전체 진행도 리스트 (저장/조회용)</summary>
        private readonly List<AchievementProgress> _allProgress = new();

        /// <summary>누적 카운터 (AchievementCondition → 누적값)</summary>
        private readonly Dictionary<AchievementCondition, int> _counters = new();

        /// <summary>등록된 업적 목록 (읽기 전용)</summary>
        public IReadOnlyList<AchievementProgress> AllProgress => _allProgress;

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

        private void Start()
        {
            // 카탈로그가 비어있으면 기본 업적 자가 초기화
            if (_catalog.Count == 0)
            {
                Initialize(GenerateDefaultAchievements());
                Debug.Log("[AchievementSystem] 기본 업적 자가 초기화 완료");
            }
        }

        /// <summary>기본 업적 생성 (전투/성장 기반, 봇 자동 달성 가능)</summary>
        private static List<AchievementData> GenerateDefaultAchievements()
        {
            var list = new List<AchievementData>(20);

            list.Add(new AchievementData { id = "ach_kill_01", displayName = "첫 사냥", description = "몬스터 10마리 처치", category = AchievementCategory.Combat, condition = AchievementCondition.KillTotal, requiredAmount = 10, rewardType = CurrencyType.Gold, rewardAmount = 500 });
            list.Add(new AchievementData { id = "ach_kill_02", displayName = "숙련 사냥꾼", description = "몬스터 100마리 처치", category = AchievementCategory.Combat, condition = AchievementCondition.KillTotal, requiredAmount = 100, rewardType = CurrencyType.Ruby, rewardAmount = 50 });
            list.Add(new AchievementData { id = "ach_kill_03", displayName = "학살자", description = "몬스터 500마리 처치", category = AchievementCategory.Combat, condition = AchievementCondition.KillTotal, requiredAmount = 500, rewardType = CurrencyType.Ruby, rewardAmount = 100 });
            list.Add(new AchievementData { id = "ach_kill_04", displayName = "전설의 사냥꾼", description = "몬스터 2000마리 처치", category = AchievementCategory.Combat, condition = AchievementCondition.KillTotal, requiredAmount = 2000, rewardType = CurrencyType.Ruby, rewardAmount = 300 });
            list.Add(new AchievementData { id = "ach_kill_05", displayName = "몬스터 학살왕", description = "몬스터 5000마리 처치", category = AchievementCategory.Combat, condition = AchievementCondition.KillTotal, requiredAmount = 5000, rewardType = CurrencyType.Ruby, rewardAmount = 500 });

            list.Add(new AchievementData { id = "ach_level_01", displayName = "신입 모험가", description = "Lv.10 달성", category = AchievementCategory.Growth, condition = AchievementCondition.ReachLevel, requiredAmount = 10, rewardType = CurrencyType.Ruby, rewardAmount = 50 });
            list.Add(new AchievementData { id = "ach_level_02", displayName = "성장하는 모험가", description = "Lv.20 달성", category = AchievementCategory.Growth, condition = AchievementCondition.ReachLevel, requiredAmount = 20, rewardType = CurrencyType.Ruby, rewardAmount = 100 });
            list.Add(new AchievementData { id = "ach_level_03", displayName = "숙련 모험가", description = "Lv.30 달성", category = AchievementCategory.Growth, condition = AchievementCondition.ReachLevel, requiredAmount = 30, rewardType = CurrencyType.Ruby, rewardAmount = 150 });
            list.Add(new AchievementData { id = "ach_level_04", displayName = "베테랑", description = "Lv.50 달성", category = AchievementCategory.Growth, condition = AchievementCondition.ReachLevel, requiredAmount = 50, rewardType = CurrencyType.Ruby, rewardAmount = 300 });
            list.Add(new AchievementData { id = "ach_level_05", displayName = "레전드", description = "Lv.100 달성", category = AchievementCategory.Growth, condition = AchievementCondition.ReachLevel, requiredAmount = 100, rewardType = CurrencyType.Ruby, rewardAmount = 500 });

            list.Add(new AchievementData { id = "ach_stage_01", displayName = "1장 클리어", description = "스테이지 10 클리어", category = AchievementCategory.Challenge, condition = AchievementCondition.StageCleared, requiredAmount = 10, rewardType = CurrencyType.Gold, rewardAmount = 2000 });
            list.Add(new AchievementData { id = "ach_stage_02", displayName = "3장 클리어", description = "스테이지 30 클리어", category = AchievementCategory.Challenge, condition = AchievementCondition.StageCleared, requiredAmount = 30, rewardType = CurrencyType.Ruby, rewardAmount = 100 });

            list.Add(new AchievementData { id = "ach_gacha_01", displayName = "첫 소환", description = "가챠 1회", category = AchievementCategory.Growth, condition = AchievementCondition.GachaPullTotal, requiredAmount = 1, rewardType = CurrencyType.Gold, rewardAmount = 1000 });
            list.Add(new AchievementData { id = "ach_gacha_02", displayName = "소환 마니아", description = "가챠 10회", category = AchievementCategory.Growth, condition = AchievementCondition.GachaPullTotal, requiredAmount = 10, rewardType = CurrencyType.Ruby, rewardAmount = 100 });

            list.Add(new AchievementData { id = "ach_gold_01", displayName = "모으는 재미", description = "골드 누적 10000 획득", category = AchievementCategory.Growth, condition = AchievementCondition.GoldEarnTotal, requiredAmount = 10000, rewardType = CurrencyType.Ruby, rewardAmount = 50 });
            list.Add(new AchievementData { id = "ach_gold_02", displayName = "부자의 길", description = "골드 누적 50000 획득", category = AchievementCategory.Growth, condition = AchievementCondition.GoldEarnTotal, requiredAmount = 50000, rewardType = CurrencyType.Ruby, rewardAmount = 150 });

            list.Add(new AchievementData { id = "ach_dungeon_01", displayName = "던전 탐험가", description = "던전 1회 클리어", category = AchievementCategory.Challenge, condition = AchievementCondition.DungeonCleared, requiredAmount = 1, rewardType = CurrencyType.Gold, rewardAmount = 3000 });

            list.Add(new AchievementData { id = "ach_equip_01", displayName = "장비 수집가", description = "장비 강화 5회", category = AchievementCategory.Equipment, condition = AchievementCondition.EnhanceCount, requiredAmount = 5, rewardType = CurrencyType.Gold, rewardAmount = 2000 });
            list.Add(new AchievementData { id = "ach_equip_02", displayName = "강화의 달인", description = "장비 강화 20회", category = AchievementCategory.Equipment, condition = AchievementCondition.EnhanceCount, requiredAmount = 20, rewardType = CurrencyType.Ruby, rewardAmount = 150 });

            list.Add(new AchievementData { id = "ach_job_01", displayName = "직업 선택", description = "직업 1회 전직", category = AchievementCategory.Growth, condition = AchievementCondition.JobAdvance, requiredAmount = 1, rewardType = CurrencyType.Ruby, rewardAmount = 100 });

            return list;
        }

        private void OnEnable()
        {
            EventBus<MonsterDiedEvent>.Subscribe(OnMonsterDied);
            EventBus<LevelUpEvent>.Subscribe(OnLevelUp);
            EventBus<EquipmentChangedEvent>.Subscribe(OnEquipmentChanged);
            EventBus<GachaResultEvent>.Subscribe(OnGachaResult);
            EventBus<GoldGainedEvent>.Subscribe(OnGoldGained);
            EventBus<StageChangedEvent>.Subscribe(OnStageChanged);
            EventBus<DungeonCompletedEvent>.Subscribe(OnDungeonCompleted);
            EventBus<JobChangedEvent>.Subscribe(OnJobChanged);
            // CostumeObtainedEvent / CostumeSetCompletedEvent: 2026-04-20 Costume 시스템 완전 제거
            EventBus<BlessingChangedEvent>.Subscribe(OnBlessingChanged);
            // AdRewardEvent: 2026-04-20 AdRewardManager 시스템 완전 제거
            EventBus<PrestigeExecutedEvent>.Subscribe(OnPrestigeExecuted);
            EventBus<CollectionEntryRegisteredEvent>.Subscribe(OnCollectionEntryRegistered);
            EventBus<SeasonRewardClaimedEvent>.Subscribe(OnSeasonRewardClaimed);
            EventBus<PvpVictoryEvent>.Subscribe(OnPvpVictory);
            // (펫 시스템 제거됨 / Climber 시스템 2026-04-20 제거 → ClimbingPartyCompletedEvent 구독 삭제 2026-04-23)
            EventBus<TitleUnlockedEvent>.Subscribe(OnTitleUnlocked);
        }

        private void OnDisable()
        {
            EventBus<MonsterDiedEvent>.Unsubscribe(OnMonsterDied);
            EventBus<LevelUpEvent>.Unsubscribe(OnLevelUp);
            EventBus<EquipmentChangedEvent>.Unsubscribe(OnEquipmentChanged);
            EventBus<GachaResultEvent>.Unsubscribe(OnGachaResult);
            EventBus<GoldGainedEvent>.Unsubscribe(OnGoldGained);
            EventBus<StageChangedEvent>.Unsubscribe(OnStageChanged);
            EventBus<DungeonCompletedEvent>.Unsubscribe(OnDungeonCompleted);
            EventBus<JobChangedEvent>.Unsubscribe(OnJobChanged);
            // CostumeObtainedEvent / CostumeSetCompletedEvent: 2026-04-20 Costume 시스템 완전 제거
            EventBus<BlessingChangedEvent>.Unsubscribe(OnBlessingChanged);
            // AdRewardEvent: 2026-04-20 AdRewardManager 시스템 완전 제거
            EventBus<PrestigeExecutedEvent>.Unsubscribe(OnPrestigeExecuted);
            EventBus<CollectionEntryRegisteredEvent>.Unsubscribe(OnCollectionEntryRegistered);
            EventBus<SeasonRewardClaimedEvent>.Unsubscribe(OnSeasonRewardClaimed);
            EventBus<PvpVictoryEvent>.Unsubscribe(OnPvpVictory);
            // (펫 시스템 제거됨 / ClimbingPartyCompletedEvent 2026-04-23 제거)
            EventBus<TitleUnlockedEvent>.Unsubscribe(OnTitleUnlocked);
        }

        /// <summary>
        /// 업적 카탈로그를 등록하고 초기화한다.
        /// </summary>
        public void Initialize(List<AchievementData> achievements)
        {
            if (achievements == null || achievements.Count == 0)
            {
                Debug.LogWarning("[AchievementSystem] 업적 카탈로그가 비어있음");
                return;
            }

            _catalog.Clear();
            _progressMap.Clear();
            _allProgress.Clear();

            // 카운터 초기화
            foreach (AchievementCondition cond in Enum.GetValues(typeof(AchievementCondition)))
            {
                _counters[cond] = 0;
            }

            for (int i = 0; i < achievements.Count; i++)
            {
                AchievementData data = achievements[i];
                if (data == null || string.IsNullOrEmpty(data.id)) continue;

                _catalog[data.id] = data;

                if (!_progressMap.ContainsKey(data.id))
                {
                    var progress = new AchievementProgress
                    {
                        achievementId = data.id,
                        currentAmount = 0,
                        isCompleted = false,
                        isClaimed = false
                    };
                    _progressMap[data.id] = progress;
                    _allProgress.Add(progress);
                }
            }

            Debug.Log($"[AchievementSystem] 초기화 완료 — 업적 {_catalog.Count}개 등록");
        }

        /// <summary>
        /// 저장된 진행도를 복원한다.
        /// </summary>
        public void RestoreProgress(List<AchievementProgress> savedProgress,
            Dictionary<AchievementCondition, int> savedCounters = null)
        {
            if (savedProgress == null) return;

            for (int i = 0; i < savedProgress.Count; i++)
            {
                AchievementProgress saved = savedProgress[i];
                if (_progressMap.TryGetValue(saved.achievementId, out AchievementProgress existing))
                {
                    existing.currentAmount = saved.currentAmount;
                    existing.isCompleted = saved.isCompleted;
                    existing.isClaimed = saved.isClaimed;
                }
            }

            if (savedCounters != null)
            {
                foreach (var kvp in savedCounters)
                {
                    _counters[kvp.Key] = kvp.Value;
                }
            }

            Debug.Log($"[AchievementSystem] 진행도 복원 완료 — {savedProgress.Count}개");
        }

        /// <summary>
        /// 업적 보상을 수령한다.
        /// </summary>
        public bool ClaimReward(string achievementId)
        {
            if (!_progressMap.TryGetValue(achievementId, out AchievementProgress progress))
            {
                Debug.LogWarning($"[AchievementSystem] 업적을 찾을 수 없음: {achievementId}");
                return false;
            }

            if (!progress.isCompleted)
            {
                Debug.LogWarning($"[AchievementSystem] 아직 완료되지 않은 업적: {achievementId}");
                return false;
            }

            if (progress.isClaimed)
            {
                Debug.LogWarning($"[AchievementSystem] 이미 보상을 수령함: {achievementId}");
                return false;
            }

            if (!_catalog.TryGetValue(achievementId, out AchievementData data))
            {
                Debug.LogWarning($"[AchievementSystem] 업적 데이터를 찾을 수 없음: {achievementId}");
                return false;
            }

            if (data.rewardAmount > 0 && CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.Add(data.rewardType, data.rewardAmount);
            }

            // 칭호 보상 지급
            if (!string.IsNullOrEmpty(data.titleRewardId))
            {
                UnlockTitle(data.titleRewardId, data.titleDisplayName, achievementId);
            }

            progress.isClaimed = true;

            EventBus.Publish(new AchievementClaimedEvent
            {
                AchievementId = achievementId,
                RewardType = data.rewardType,
                RewardAmount = data.rewardAmount
            });

            // 2026-04-23 이슈 15 FeedbackBus: 업적 보상 Toast + 약한 쉐이크
            MkLike.Core.FeedbackBus.Emit(
                MkLike.Core.FeedbackKind.Generic,
                $"업적 달성! {data.displayName}",
                shakeIntensity: 1);

            Debug.Log($"[AchievementSystem] 보상 수령: {data.displayName} ({data.rewardType} x{data.rewardAmount})");
            return true;
        }

        /// <summary>
        /// 특정 업적의 진행도를 반환한다.
        /// </summary>
        public AchievementProgress GetProgress(string achievementId)
        {
            return _progressMap.TryGetValue(achievementId, out AchievementProgress progress) ? progress : null;
        }

        /// <summary>
        /// 특정 카테고리의 업적 목록을 반환한다.
        /// </summary>
        public List<AchievementProgress> GetByCategory(AchievementCategory category)
        {
            var result = new List<AchievementProgress>();
            for (int i = 0; i < _allProgress.Count; i++)
            {
                AchievementProgress progress = _allProgress[i];
                if (_catalog.TryGetValue(progress.achievementId, out AchievementData data) &&
                    data.category == category)
                {
                    result.Add(progress);
                }
            }
            return result;
        }

        /// <summary>
        /// 수령 가능한 업적 수를 반환한다.
        /// </summary>
        public int GetClaimableCount()
        {
            int count = 0;
            for (int i = 0; i < _allProgress.Count; i++)
            {
                AchievementProgress progress = _allProgress[i];
                if (progress.isCompleted && !progress.isClaimed) count++;
            }
            return count;
        }

        /// <summary>
        /// 업적 데이터를 반환한다.
        /// </summary>
        public AchievementData GetData(string achievementId)
        {
            return _catalog.TryGetValue(achievementId, out AchievementData data) ? data : null;
        }

        /// <summary>
        /// 현재 진행도 리스트를 반환한다 (저장용).
        /// </summary>
        public List<AchievementProgress> GetProgressForSave()
        {
            return new List<AchievementProgress>(_allProgress);
        }

        /// <summary>
        /// 누적 카운터를 반환한다 (저장용).
        /// </summary>
        public Dictionary<AchievementCondition, int> GetCountersForSave()
        {
            return new Dictionary<AchievementCondition, int>(_counters);
        }

        // --- 내부: 진행도 갱신 ---

        private void AddProgress(AchievementCondition condition, int amount)
        {
            if (!_counters.ContainsKey(condition))
                _counters[condition] = 0;

            _counters[condition] += amount;
            int totalValue = _counters[condition];

            UpdateProgressByCondition(condition, totalValue);
        }

        private void SetProgress(AchievementCondition condition, int value)
        {
            if (!_counters.ContainsKey(condition))
                _counters[condition] = 0;

            if (value <= _counters[condition]) return;

            _counters[condition] = value;
            UpdateProgressByCondition(condition, value);
        }

        private void UpdateProgressByCondition(AchievementCondition condition, int value)
        {
            for (int i = 0; i < _allProgress.Count; i++)
            {
                AchievementProgress progress = _allProgress[i];
                if (progress.isCompleted) continue;

                if (!_catalog.TryGetValue(progress.achievementId, out AchievementData data)) continue;
                if (data.condition != condition) continue;

                progress.currentAmount = value;

                if (progress.currentAmount >= data.requiredAmount)
                {
                    progress.currentAmount = data.requiredAmount;
                    progress.isCompleted = true;

                    EventBus.Publish(new AchievementCompletedEvent
                    {
                        AchievementId = progress.achievementId,
                        DisplayName = data.displayName
                    });

                    Debug.Log($"[AchievementSystem] 업적 달성: {data.displayName}");
                }
            }
        }

        // --- 이벤트 핸들러 ---

        private void OnMonsterDied(MonsterDiedEvent evt)
        {
            AddProgress(AchievementCondition.KillTotal, 1);
        }

        private void OnLevelUp(LevelUpEvent evt)
        {
            SetProgress(AchievementCondition.ReachLevel, evt.CurrentLevel);
        }

        private void OnEquipmentChanged(EquipmentChangedEvent evt)
        {
            if (evt.IsEquipped)
            {
                AddProgress(AchievementCondition.EnhanceCount, 1);
            }
        }

        // OnRelicObtained: 2026-04-20 유물 시스템 완전 제거

        private void OnGachaResult(GachaResultEvent evt)
        {
            AddProgress(AchievementCondition.GachaPullTotal, 1);
        }

        private void OnGoldGained(GoldGainedEvent evt)
        {
            AddProgress(AchievementCondition.GoldEarnTotal, (int)Mathf.Min(evt.Amount, int.MaxValue));
        }

        private void OnStageChanged(StageChangedEvent evt)
        {
            int absoluteStage = (evt.Chapter - 1) * 10 + evt.StageIndex;
            SetProgress(AchievementCondition.StageCleared, absoluteStage);
        }

        private void OnDungeonCompleted(DungeonCompletedEvent evt)
        {
            AddProgress(AchievementCondition.DungeonCleared, 1);
        }

        private void OnJobChanged(JobChangedEvent evt)
        {
            AddProgress(AchievementCondition.JobAdvance, 1);
        }

        // OnCostumeObtained / OnCostumeSetCompleted: 2026-04-20 Costume 시스템 완전 제거

        private void OnBlessingChanged(BlessingChangedEvent evt)
        {
            if (evt.IsReroll)
            {
                AddProgress(AchievementCondition.BlessingReroll, 1);
            }
            else
            {
                AddProgress(AchievementCondition.BlessingObtained, 1);
            }
        }

        // OnAdReward: 2026-04-20 AdRewardManager 시스템 완전 제거

        private void OnPrestigeExecuted(PrestigeExecutedEvent evt)
        {
            SetProgress(AchievementCondition.PrestigeCount, evt.PrestigeCount);
        }

        private void OnCollectionEntryRegistered(CollectionEntryRegisteredEvent evt)
        {
            SetProgress(AchievementCondition.CollectionTotal, evt.TotalCollected);
        }

        private void OnSeasonRewardClaimed(SeasonRewardClaimedEvent evt)
        {
            SetProgress(AchievementCondition.SeasonRewardClaimed, evt.ClaimedCount);
        }

        private void OnPvpVictory(PvpVictoryEvent evt)
        {
            SetProgress(AchievementCondition.PvpVictory, evt.TotalVictories);
            SetProgress(AchievementCondition.PvpWinStreak, evt.CurrentStreak);
        }

        // OnClimbingPartyCompleted / ClimbingPartyCombination AchievementCondition:
        // 2026-04-23 제거 — Climber 시스템 2026-04-20 삭제 후 Publish 부재 dead code.

        private void OnTitleUnlocked(TitleUnlockedEvent evt)
        {
            AddProgress(AchievementCondition.TitleCollect, 1);
        }

        // --- AE-04: 칭호 시스템 ---

        private const string TITLE_SAVE_KEY = "TitleSaveData";

        /// <summary>해금된 칭호 목록</summary>
        private readonly HashSet<string> _unlockedTitles = new();

        /// <summary>현재 장착 중인 칭호 ID</summary>
        private string _equippedTitleId;

        public IReadOnlyCollection<string> UnlockedTitles => _unlockedTitles;
        public string EquippedTitleId => _equippedTitleId;

        /// <summary>
        /// 칭호를 해금한다.
        /// </summary>
        private void UnlockTitle(string titleId, string titleName, string achievementId)
        {
            if (string.IsNullOrEmpty(titleId)) return;
            if (_unlockedTitles.Contains(titleId)) return;

            _unlockedTitles.Add(titleId);
            SaveTitleData();

            EventBus.Publish(new TitleUnlockedEvent
            {
                TitleId = titleId,
                TitleName = titleName,
                AchievementId = achievementId
            });

            Debug.Log($"[AchievementSystem] 칭호 해금: {titleName} (ID: {titleId})");
        }

        /// <summary>
        /// 칭호를 장착한다. 프로필에 표시된다.
        /// </summary>
        public bool EquipTitle(string titleId)
        {
            if (string.IsNullOrEmpty(titleId)) return false;
            if (!_unlockedTitles.Contains(titleId))
            {
                Debug.LogWarning($"[AchievementSystem] 미해금 칭호 장착 시도: {titleId}");
                return false;
            }

            if (_equippedTitleId == titleId) return false;

            string previousId = _equippedTitleId;
            _equippedTitleId = titleId;
            SaveTitleData();

            EventBus.Publish(new TitleEquippedEvent
            {
                TitleId = titleId,
                IsEquipped = true
            });

            Debug.Log($"[AchievementSystem] 칭호 장착: {titleId}");
            return true;
        }

        /// <summary>
        /// 칭호를 해제한다.
        /// </summary>
        public void UnequipTitle()
        {
            if (string.IsNullOrEmpty(_equippedTitleId)) return;

            string previousId = _equippedTitleId;
            _equippedTitleId = null;
            SaveTitleData();

            EventBus.Publish(new TitleEquippedEvent
            {
                TitleId = previousId,
                IsEquipped = false
            });

            Debug.Log("[AchievementSystem] 칭호 해제");
        }

        /// <summary>
        /// 칭호 해금 여부를 확인한다.
        /// </summary>
        public bool HasTitle(string titleId)
        {
            return _unlockedTitles.Contains(titleId);
        }

        /// <summary>
        /// 칭호 데이터를 저장한다.
        /// </summary>
        private void SaveTitleData()
        {
            var save = new TitleSaveData
            {
                UnlockedTitleIds = new List<string>(_unlockedTitles),
                EquippedTitleId = _equippedTitleId
            };
            string json = JsonUtility.ToJson(save);
            PlayerPrefs.SetString(TITLE_SAVE_KEY, json);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 칭호 데이터를 로드한다. Initialize() 이후 호출.
        /// </summary>
        public void LoadTitleData()
        {
            _unlockedTitles.Clear();
            _equippedTitleId = null;

            if (!PlayerPrefs.HasKey(TITLE_SAVE_KEY)) return;

            string json = PlayerPrefs.GetString(TITLE_SAVE_KEY);
            if (string.IsNullOrEmpty(json)) return;

            var save = JsonUtility.FromJson<TitleSaveData>(json);
            if (save == null) return;

            if (save.UnlockedTitleIds != null)
            {
                for (int i = 0; i < save.UnlockedTitleIds.Count; i++)
                    _unlockedTitles.Add(save.UnlockedTitleIds[i]);
            }

            _equippedTitleId = save.EquippedTitleId;
            Debug.Log($"[AchievementSystem] 칭호 로드 — 해금: {_unlockedTitles.Count}, 장착: {_equippedTitleId ?? "없음"}");
        }

        [Serializable]
        private class TitleSaveData
        {
            public List<string> UnlockedTitleIds;
            public string EquippedTitleId;
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
