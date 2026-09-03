using System;
using System.Collections.Generic;
using UnityEngine;
using MkLike.Core;
using MkLike.Data;
using MkLike.Economy;
using MkLike.Utils;
using MkLike.Combat;
using MkLike.Core.Save;

namespace MkLike.Quest
{
    /// <summary>
    /// 퀘스트 진행도 데이터.
    /// </summary>
    [Serializable]
    public class QuestProgress
    {
        public string questId;
        public int currentAmount;
        public bool isCompleted;
        public bool isRewardClaimed;
    }

    /// <summary>
    /// 퀘스트/미션 관리 매니저.
    /// 메인 퀘스트(순차), 일일/주간 퀘스트(리셋) 관리.
    /// 이벤트 구독으로 자동 진행도 추적, 완료 시 보상 지급.
    /// </summary>
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        [SerializeField] private QuestDataSO[] _questCatalog;

        private readonly List<QuestProgress> _activeQuests = new();
        private readonly Dictionary<string, QuestDataSO> _questDataMap = new();
        private readonly Dictionary<string, QuestProgress> _progressMap = new();

        /// <summary>마지막 일일 리셋 날짜 (yyyy-MM-dd)</summary>
        private string _lastDailyReset;

        /// <summary>마지막 주간 리셋 날짜 (yyyy-MM-dd, 월요일 기준)</summary>
        private string _lastWeeklyReset;

        /// <summary>현재 활성 가이드 퀘스트 ID (null이면 모든 가이드 퀘스트 완료)</summary>
        private string _currentGuideQuestId;

        /// <summary>완료된 가이드 퀘스트 인덱스 (세이브/로드용)</summary>
        private int _completedGuideIndex = -1;

        // ── 순환 퀘스트 ──
        /// <summary>현재 활성 순환 퀘스트 ID (null이면 비활성)</summary>
        private string _currentCyclingQuestId;

        /// <summary>순환 퀘스트 현재 순서 인덱스</summary>
        private int _cyclingCurrentOrder;

        /// <summary>순환 퀘스트 회차 (1부터 시작, 0 = 미시작)</summary>
        private int _cyclingRound;

        /// <summary>순환 퀘스트 SO 목록 (cyclingOrder 순서)</summary>
        private readonly List<QuestDataSO> _cyclingQuests = new();

        /// <summary>가이드 퀘스트가 모두 완료되었는지</summary>
        private bool _isGuideComplete;

        /// <summary>활성 퀘스트 목록 (읽기 전용)</summary>
        public IReadOnlyList<QuestProgress> ActiveQuests => _activeQuests;

        /// <summary>현재 활성 가이드 퀘스트 ID</summary>
        public string CurrentGuideQuestId => _currentGuideQuestId;

        /// <summary>완료된 가이드 퀘스트 인덱스 (세이브용)</summary>
        public int CompletedGuideIndex => _completedGuideIndex;

        /// <summary>현재 활성 순환 퀘스트 ID</summary>
        public string CurrentCyclingQuestId => _currentCyclingQuestId;

        /// <summary>순환 퀘스트 회차 (UI 표시용)</summary>
        public int CyclingRound => _cyclingRound;

        /// <summary>순환 퀘스트 활성 여부</summary>
        public bool IsCyclingActive => _isGuideComplete && !string.IsNullOrEmpty(_currentCyclingQuestId);

        private void SyncGuideIndexToSave()
        {
            var sd = SaveManager.Instance?.CurrentData?.quest;
            if (sd != null)
                sd.completedGuideIndex = _completedGuideIndex;
        }

        private void SyncCyclingToSave()
        {
            var sd = SaveManager.Instance?.CurrentData?.quest;
            if (sd == null) return;
            sd.cyclingCurrentOrder = _cyclingCurrentOrder;
            sd.cyclingRound = _cyclingRound;
        }

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
            if (_questCatalog == null || _questCatalog.Length == 0) return;

            Initialize(_questCatalog);

            // 세이브에서 가이드 진행도 복원
            var saveData = SaveManager.Instance?.CurrentData?.quest;
            if (saveData != null && saveData.completedGuideIndex >= 0)
            {
                RestoreGuideProgress(saveData.completedGuideIndex);
            }
            else
            {
                // 빈 세이브 → 처음부터 시작
                RestoreGuideProgress(-1);
            }

            // 세이브에서 순환 퀘스트 진행도 복원
            if (saveData != null && saveData.cyclingRound > 0)
            {
                RestoreCyclingProgress(saveData.cyclingCurrentOrder, saveData.cyclingRound);
            }

            Debug.Log($"[QuestManager] 초기화 완료 — 카탈로그 {_questCatalog.Length}개, 가이드 인덱스 {_completedGuideIndex}, 순환 회차 {_cyclingRound}");
        }

        private void OnEnable()
        {
            // 도메인 리로드 후 static Instance 재등록 (DDOL에서 Awake 재실행 안 됨)
            if (Instance == null)
                Instance = this;

            EventBus<MonsterDiedEvent>.Subscribe(OnMonsterDied);
            EventBus<StageChangedEvent>.Subscribe(OnStageChanged);
            EventBus<LevelUpEvent>.Subscribe(OnLevelUp);
            EventBus<DungeonCompletedEvent>.Subscribe(OnDungeonCompleted);
            EventBus<StatAllocatedEvent>.Subscribe(OnStatAllocated);
            EventBus<CurrencyChangedEvent>.Subscribe(OnCurrencyChanged);
            EventBus<GachaResultEvent>.Subscribe(OnGachaResult);
            EventBus<EquipmentChangedEvent>.Subscribe(OnEquipmentChanged);
            EventBus<SkillLevelUpEvent>.Subscribe(OnSkillLevelUp);
            EventBus<ArenaMatchEvent>.Subscribe(OnArenaMatch);
            EventBus<TowerFloorReachedEvent>.Subscribe(OnTowerFloorReached);
            EventBus<QuestCompletedEvent>.Subscribe(OnQuestCompleted);
            EventBus<SkillUsedEvent>.Subscribe(OnSkillUsed);
            EventBus<AttendanceCheckedEvent>.Subscribe(OnAttendanceChecked);
            EventBus<OfflineRewardClaimedEvent>.Subscribe(OnOfflineRewardClaimed);
            EventBus<BlessingChangedEvent>.Subscribe(OnBlessingChanged);
            EventBus<AchievementCompletedEvent>.Subscribe(OnAchievementCompleted);
            EventBus<JobChangedEvent>.Subscribe(OnJobChanged);
            EventBus<CollectionEntryRegisteredEvent>.Subscribe(OnCollectionRegistered);
            EventBus<QuickHuntCompletedEvent>.Subscribe(OnQuickHuntCompleted);

            EventBus<BoosterUsedEvent>.Subscribe(OnBoosterUsed);
            EventBus<StarForceEvent>.Subscribe(OnStarForceResult);
            EventBus<PotentialChangedEvent>.Subscribe(OnPotentialChanged);
            EventBus<GuildJoinedEvent>.Subscribe(OnGuildJoined);
            EventBus<SummonLevelUpEvent>.Subscribe(OnSummonLevelUp);
            EventBus<EliteSummonLevelUpEvent>.Subscribe(OnEliteSummonLevelUp);
            EventBus<EliteSummonedEvent>.Subscribe(OnEliteSummoned);
            EventBus<BossRaidEnteredEvent>.Subscribe(OnBossRaidEntered);
            EventBus<LoadCompletedEvent>.SubscribeSticky(OnLoadCompleted);
            EventBus<BeforeSaveEvent>.SubscribeSticky(OnBeforeSave);
        }

        private void OnDisable()
        {
            EventBus<MonsterDiedEvent>.Unsubscribe(OnMonsterDied);
            EventBus<StageChangedEvent>.Unsubscribe(OnStageChanged);
            EventBus<LevelUpEvent>.Unsubscribe(OnLevelUp);
            EventBus<DungeonCompletedEvent>.Unsubscribe(OnDungeonCompleted);
            EventBus<StatAllocatedEvent>.Unsubscribe(OnStatAllocated);
            EventBus<CurrencyChangedEvent>.Unsubscribe(OnCurrencyChanged);
            EventBus<GachaResultEvent>.Unsubscribe(OnGachaResult);
            EventBus<EquipmentChangedEvent>.Unsubscribe(OnEquipmentChanged);
            EventBus<SkillLevelUpEvent>.Unsubscribe(OnSkillLevelUp);
            EventBus<ArenaMatchEvent>.Unsubscribe(OnArenaMatch);
            EventBus<TowerFloorReachedEvent>.Unsubscribe(OnTowerFloorReached);
            EventBus<QuestCompletedEvent>.Unsubscribe(OnQuestCompleted);
            EventBus<SkillUsedEvent>.Unsubscribe(OnSkillUsed);
            EventBus<AttendanceCheckedEvent>.Unsubscribe(OnAttendanceChecked);
            EventBus<OfflineRewardClaimedEvent>.Unsubscribe(OnOfflineRewardClaimed);
            EventBus<BlessingChangedEvent>.Unsubscribe(OnBlessingChanged);
            EventBus<AchievementCompletedEvent>.Unsubscribe(OnAchievementCompleted);
            EventBus<JobChangedEvent>.Unsubscribe(OnJobChanged);
            EventBus<CollectionEntryRegisteredEvent>.Unsubscribe(OnCollectionRegistered);
            EventBus<QuickHuntCompletedEvent>.Unsubscribe(OnQuickHuntCompleted);

            EventBus<BoosterUsedEvent>.Unsubscribe(OnBoosterUsed);
            EventBus<StarForceEvent>.Unsubscribe(OnStarForceResult);
            EventBus<PotentialChangedEvent>.Unsubscribe(OnPotentialChanged);
            EventBus<GuildJoinedEvent>.Unsubscribe(OnGuildJoined);
            EventBus<SummonLevelUpEvent>.Unsubscribe(OnSummonLevelUp);
            EventBus<EliteSummonLevelUpEvent>.Unsubscribe(OnEliteSummonLevelUp);
            EventBus<EliteSummonedEvent>.Unsubscribe(OnEliteSummoned);
            EventBus<BossRaidEnteredEvent>.Unsubscribe(OnBossRaidEntered);
            EventBus<LoadCompletedEvent>.Unsubscribe(OnLoadCompleted);
            EventBus<BeforeSaveEvent>.Unsubscribe(OnBeforeSave);
        }

        private void OnBeforeSave(BeforeSaveEvent evt)
        {
            var sd = SaveManager.Instance?.CurrentData?.quest;
            if (sd == null) return;

            sd.completedGuideIndex = _completedGuideIndex;
            sd.cyclingCurrentOrder = _cyclingCurrentOrder;
            sd.cyclingRound = _cyclingRound;
            sd.lastDailyReset = _lastDailyReset;
            sd.lastWeeklyReset = _lastWeeklyReset;

            sd.activeQuests.Clear();
            for (int i = 0; i < _activeQuests.Count; i++)
            {
                var p = _activeQuests[i];
                sd.activeQuests.Add(new QuestProgressData
                {
                    questId = p.questId,
                    currentAmount = p.currentAmount,
                    isCompleted = p.isCompleted,
                    isRewardClaimed = p.isRewardClaimed
                });
            }
        }

        /// <summary>
        /// 퀘스트 카탈로그를 기반으로 초기화한다.
        /// 저장된 진행도가 있으면 복원하고, 없으면 새로 생성한다.
        /// </summary>
        public void Initialize(QuestDataSO[] questCatalog)
        {
            if (questCatalog == null || questCatalog.Length == 0)
            {
                Debug.LogWarning("[QuestManager] 퀘스트 카탈로그가 비어있음");
                return;
            }

            _questCatalog = questCatalog;
            _questDataMap.Clear();
            _progressMap.Clear();
            _activeQuests.Clear();

            _cyclingQuests.Clear();

            for (int i = 0; i < questCatalog.Length; i++)
            {
                QuestDataSO data = questCatalog[i];
                if (data == null) continue;

                _questDataMap[data.id] = data;

                if (data.questType == QuestType.Cycling)
                {
                    _cyclingQuests.Add(data);
                }
            }

            // 순환 퀘스트를 cyclingOrder 순서로 정렬
            _cyclingQuests.Sort((a, b) => a.cyclingOrder.CompareTo(b.cyclingOrder));

            // 날짜 초기화
            _lastDailyReset = DateTime.Now.ToString("yyyy-MM-dd");
            _lastWeeklyReset = GetMondayDate(DateTime.Now);

            ActivateQuests();

            Debug.Log($"[QuestManager] 초기화 완료 — 활성 퀘스트: {_activeQuests.Count}개");
        }

        /// <summary>
        /// 저장된 진행도를 복원한다.
        /// </summary>
        public void RestoreProgress(List<QuestProgress> savedProgress, string lastDailyReset, string lastWeeklyReset)
        {
            if (savedProgress == null) return;

            _lastDailyReset = lastDailyReset ?? DateTime.Now.ToString("yyyy-MM-dd");
            _lastWeeklyReset = lastWeeklyReset ?? GetMondayDate(DateTime.Now);

            _progressMap.Clear();
            _activeQuests.Clear();

            for (int i = 0; i < savedProgress.Count; i++)
            {
                QuestProgress progress = savedProgress[i];
                _progressMap[progress.questId] = progress;
                _activeQuests.Add(progress);
            }

            // 리셋 체크 후 재활성화
            CheckDailyReset();
            CheckWeeklyReset();
        }

        /// <summary>
        /// 가이드 퀘스트 진행도를 복원한다 (세이브/로드용).
        /// </summary>
        public void RestoreGuideProgress(int completedGuideIndex)
        {
            _completedGuideIndex = completedGuideIndex;
            _currentGuideQuestId = null; // Initialize()에서 설정된 값 초기화 → 다음 퀘스트 탐색 보장
            ActivateNextGuideQuest();
        }

        /// <summary>
        /// 보상을 수령한다.
        /// </summary>
        public bool ClaimReward(string questId)
        {
            if (!_progressMap.TryGetValue(questId, out QuestProgress progress))
            {
                Debug.LogWarning($"[QuestManager] 퀘스트를 찾을 수 없음: {questId}");
                return false;
            }

            if (!progress.isCompleted)
            {
                Debug.LogWarning($"[QuestManager] 아직 완료되지 않은 퀘스트: {questId}");
                return false;
            }

            if (progress.isRewardClaimed)
            {
                Debug.LogWarning($"[QuestManager] 이미 보상을 수령함: {questId}");
                return false;
            }

            if (!_questDataMap.TryGetValue(questId, out QuestDataSO questData))
            {
                Debug.LogWarning($"[QuestManager] 퀘스트 데이터를 찾을 수 없음: {questId}");
                return false;
            }

            // 메인 보상 지급 (순환 퀘스트는 회차별 스케일링)
            bool isCycling = questData.questType == QuestType.Cycling;
            int mainReward = isCycling ? GetScaledReward(questData.rewardAmount) : questData.rewardAmount;
            int bonusReward = isCycling ? GetScaledReward(questData.bonusRewardAmount) : questData.bonusRewardAmount;

            if (mainReward > 0 && CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.Add(questData.rewardType, mainReward);
            }

            // 보너스 보상 지급
            if (bonusReward > 0 && CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.Add(questData.bonusRewardType, bonusReward);
            }

            progress.isRewardClaimed = true;

            EventBus.Publish(new QuestRewardClaimedEvent
            {
                QuestId = questId,
                RewardType = questData.rewardType,
                RewardAmount = questData.rewardAmount
            });

            // 2026-04-23 이슈 15 FeedbackBus: 퀘스트 보상 수령 Toast (중복 방지 — 빈번하지 않은 가이드/이벤트만)
            if (questData.questType == QuestType.Main || questData.questType == QuestType.Guide)
            {
                MkLike.Core.FeedbackBus.Emit(
                    MkLike.Core.FeedbackKind.Generic,
                    $"퀘스트 완료: {questData.displayName}");

                // 2026-04-23 SFX 보완: 보상 수령 오디오
                MkLike.Core.AudioManager.Instance?.PlaySfx(MkLike.Core.SfxType.UiReward);
            }

            Debug.Log($"[QuestManager] 보상 수령 완료: {questData.displayName} ({questData.rewardType} x{questData.rewardAmount})");

            // 메인 퀘스트는 다음 퀘스트 활성화
            if (questData.questType == QuestType.Main)
            {
                ActivateNextMainQuest();
            }

            // 가이드 퀘스트는 체인 다음 퀘스트 활성화 + 전용 이벤트
            if (questData.questType == QuestType.Guide)
            {
                _completedGuideIndex = questData.guideChainIndex;
                SyncGuideIndexToSave();

                EventBus.Publish(new GuideQuestCompletedEvent
                {
                    QuestId = questId,
                    NextQuestId = questData.nextGuideQuestId,
                    ChainIndex = questData.guideChainIndex,
                    RewardType = questData.rewardType,
                    RewardAmount = questData.rewardAmount,
                    BonusRewardType = questData.bonusRewardType,
                    BonusRewardAmount = questData.bonusRewardAmount,
                    DisplayName = questData.displayName
                });

                ActivateNextGuideQuest();
            }

            // 순환 퀘스트 완료 → 다음 순환 퀘스트 활성화
            if (questData.questType == QuestType.Cycling)
            {
                EventBus.Publish(new CyclingQuestCompletedEvent
                {
                    QuestId = questId,
                    CyclingOrder = questData.cyclingOrder,
                    Round = _cyclingRound,
                    RewardType = questData.rewardType,
                    RewardAmount = GetScaledReward(questData.rewardAmount),
                    DisplayName = questData.displayName
                });

                AdvanceCyclingQuest();
            }

            return true;
        }

        /// <summary>
        /// 일일 리셋을 확인한다. 날짜가 변경되면 일일 퀘스트를 리셋한다.
        /// </summary>
        public void CheckDailyReset()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (string.Equals(_lastDailyReset, today, StringComparison.Ordinal)) return;

            _lastDailyReset = today;
            ResetQuestsByType(QuestType.Daily);
            Debug.Log("[QuestManager] 일일 퀘스트 리셋 완료");
        }

        /// <summary>
        /// 주간 리셋을 확인한다. 월요일이 변경되면 주간 퀘스트를 리셋한다.
        /// </summary>
        public void CheckWeeklyReset()
        {
            string monday = GetMondayDate(DateTime.Now);
            if (string.Equals(_lastWeeklyReset, monday, StringComparison.Ordinal)) return;

            _lastWeeklyReset = monday;
            ResetQuestsByType(QuestType.Weekly);
            Debug.Log("[QuestManager] 주간 퀘스트 리셋 완료");
        }

        /// <summary>
        /// 현재 진행도 목록을 반환한다 (저장용).
        /// </summary>
        public List<QuestProgress> GetProgressForSave()
        {
            return new List<QuestProgress>(_activeQuests);
        }

        /// <summary>
        /// 퀘스트 ID로 QuestDataSO를 조회한다.
        /// </summary>
        public QuestDataSO GetQuestData(string questId)
        {
            return _questDataMap.TryGetValue(questId, out QuestDataSO data) ? data : null;
        }

        /// <summary>마지막 일일 리셋 날짜 (저장용)</summary>
        public string LastDailyReset => _lastDailyReset;

        /// <summary>마지막 주간 리셋 날짜 (저장용)</summary>
        public string LastWeeklyReset => _lastWeeklyReset;

        private void ActivateQuests()
        {
            // 메인 퀘스트: 첫 번째 미완료 퀘스트 활성화
            ActivateNextMainQuest();

            // 가이드 퀘스트: 첫 번째 미완료 가이드 퀘스트 활성화
            ActivateNextGuideQuest();

            // 일일/주간 퀘스트: 전부 활성화
            foreach (var kvp in _questDataMap)
            {
                QuestDataSO data = kvp.Value;
                if (data.questType == QuestType.Daily || data.questType == QuestType.Weekly)
                {
                    ActivateQuest(data.id);
                }
            }
        }

        private void ActivateNextMainQuest()
        {
            foreach (var kvp in _questDataMap)
            {
                QuestDataSO data = kvp.Value;
                if (data.questType != QuestType.Main) continue;

                if (_progressMap.TryGetValue(data.id, out QuestProgress existing))
                {
                    if (existing.isRewardClaimed) continue;
                    // 이미 활성화된 미완료 메인 퀘스트 존재
                    return;
                }

                ActivateQuest(data.id);
                return;
            }
        }

        /// <summary>
        /// 가이드 퀘스트 체인에서 다음 미완료 퀘스트를 활성화한다.
        /// guideChainIndex 순서대로 진행한다.
        /// </summary>
        private void ActivateNextGuideQuest()
        {
            // 이미 활성 가이드 퀘스트가 있고 완료 전이면 건너뜀
            if (!string.IsNullOrEmpty(_currentGuideQuestId))
            {
                if (_progressMap.TryGetValue(_currentGuideQuestId, out QuestProgress existing))
                {
                    if (!existing.isRewardClaimed) return;
                }
            }

            // 가이드 퀘스트를 chainIndex 순서로 정렬하여 다음 미완료 퀘스트 탐색
            QuestDataSO nextGuide = null;
            int lowestIndex = int.MaxValue;

            foreach (var kvp in _questDataMap)
            {
                QuestDataSO data = kvp.Value;
                if (data.questType != QuestType.Guide) continue;
                if (data.guideChainIndex <= _completedGuideIndex) continue;

                if (_progressMap.TryGetValue(data.id, out QuestProgress progress))
                {
                    if (progress.isRewardClaimed) continue;
                }

                if (data.guideChainIndex < lowestIndex)
                {
                    lowestIndex = data.guideChainIndex;
                    nextGuide = data;
                }
            }

            if (nextGuide == null)
            {
                _currentGuideQuestId = null;
                _isGuideComplete = true;

                // 가이드 완료 → 순환 퀘스트 시작
                if (_cyclingQuests.Count > 0 && _cyclingRound == 0)
                {
                    _cyclingRound = 1;
                    _cyclingCurrentOrder = 0;
                    SyncCyclingToSave();
                    ActivateNextCyclingQuest();
                    Debug.Log("[QuestManager] 가이드 퀘스트 전체 완료 → 순환 퀘스트 시작");

                    // 2026-04-23 이슈: 350 완료 시 인게임 피드백 부재 → FeedbackBus 마일스톤 Toast
                    FeedbackBus.Emit(FeedbackKind.Generic, "모든 가이드 완료! 순환 퀘스트를 시작합니다", 3);
                    // 350 완료는 큰 마일스톤 → GuideQuestComplete SFX 강조
                    AudioManager.Instance?.PlaySfx(SfxType.GuideQuestComplete);
                }
                return;
            }

            _currentGuideQuestId = nextGuide.id;
            ActivateQuest(nextGuide.id);

            EventBus.Publish(new GuideQuestActivatedEvent
            {
                QuestId = nextGuide.id,
                ChainIndex = nextGuide.guideChainIndex,
                DisplayName = nextGuide.displayName,
                Description = nextGuide.description,
                RequiredAmount = nextGuide.requiredAmount,
                Condition = nextGuide.condition
            });

            Debug.Log($"[QuestManager] 가이드 퀘스트 활성화: G-{nextGuide.guideChainIndex:D3} {nextGuide.displayName}");

            // SetProgress 기반 조건의 현재 값 동기화 요청
            // 도메인 격리로 직접 참조 불가 → 이벤트로 각 시스템에 재발행 요청
            EventBus.Publish(new QuestStateRefreshEvent { Condition = nextGuide.condition });
        }

        /// <summary>
        /// 현재 활성 가이드 퀘스트의 진행도를 조회한다. UI 바인딩용.
        /// </summary>
        public QuestProgress GetCurrentGuideProgress()
        {
            if (string.IsNullOrEmpty(_currentGuideQuestId)) return null;
            return _progressMap.TryGetValue(_currentGuideQuestId, out QuestProgress p) ? p : null;
        }

        /// <summary>
        /// 현재 활성 가이드 퀘스트의 데이터를 조회한다. UI 바인딩용.
        /// </summary>
        public QuestDataSO GetCurrentGuideData()
        {
            if (string.IsNullOrEmpty(_currentGuideQuestId)) return null;
            return _questDataMap.TryGetValue(_currentGuideQuestId, out QuestDataSO d) ? d : null;
        }

        /// <summary>
        /// 가이드 퀘스트 완료 시 자동 보상 수령 + 다음 퀘스트 진행.
        /// GoalGuideWidget에서 호출한다.
        /// </summary>
        public bool ClaimGuideRewardAndAdvance()
        {
            if (string.IsNullOrEmpty(_currentGuideQuestId)) return false;
            return ClaimReward(_currentGuideQuestId);
        }

        private void ActivateQuest(string questId)
        {
            if (_progressMap.ContainsKey(questId)) return;

            var progress = new QuestProgress
            {
                questId = questId,
                currentAmount = 0,
                isCompleted = false,
                isRewardClaimed = false
            };

            _progressMap[questId] = progress;
            _activeQuests.Add(progress);
        }

        private void ResetQuestsByType(QuestType type)
        {
            // 해당 타입의 기존 진행도 제거
            for (int i = _activeQuests.Count - 1; i >= 0; i--)
            {
                QuestProgress progress = _activeQuests[i];
                if (_questDataMap.TryGetValue(progress.questId, out QuestDataSO data) && data.questType == type)
                {
                    _progressMap.Remove(progress.questId);
                    _activeQuests.RemoveAt(i);
                }
            }

            // 해당 타입 퀘스트 재활성화
            foreach (var kvp in _questDataMap)
            {
                QuestDataSO data = kvp.Value;
                if (data.questType == type)
                {
                    ActivateQuest(data.id);
                }
            }
        }

        private void AddProgress(QuestCondition condition, int amount)
        {
            for (int i = 0; i < _activeQuests.Count; i++)
            {
                QuestProgress progress = _activeQuests[i];
                if (progress.isCompleted) continue;

                if (!_questDataMap.TryGetValue(progress.questId, out QuestDataSO data)) continue;
                if (data.condition != condition) continue;

                progress.currentAmount += amount;

                if (progress.currentAmount >= data.requiredAmount)
                {
                    progress.currentAmount = data.requiredAmount;
                    progress.isCompleted = true;

                    EventBus.Publish(new QuestCompletedEvent
                    {
                        QuestId = progress.questId,
                        QuestType = data.questType
                    });

                    Debug.Log($"[QuestManager] 퀘스트 완료: {data.displayName}");
                }
            }
        }

        private void SetProgress(QuestCondition condition, int value)
        {
            for (int i = 0; i < _activeQuests.Count; i++)
            {
                QuestProgress progress = _activeQuests[i];
                if (progress.isCompleted) continue;

                if (!_questDataMap.TryGetValue(progress.questId, out QuestDataSO data)) continue;
                if (data.condition != condition) continue;

                if (value <= progress.currentAmount) continue;

                progress.currentAmount = value;

                if (progress.currentAmount >= data.requiredAmount)
                {
                    progress.currentAmount = data.requiredAmount;
                    progress.isCompleted = true;

                    EventBus.Publish(new QuestCompletedEvent
                    {
                        QuestId = progress.questId,
                        QuestType = data.questType
                    });

                    Debug.Log($"[QuestManager] 퀘스트 완료: {data.displayName}");
                }
            }
        }

        // --- 이벤트 핸들러 ---

        private void OnMonsterDied(MonsterDiedEvent evt)
        {
            AddProgress(QuestCondition.KillMonsters, 1);
        }

        private void OnStageChanged(StageChangedEvent evt)
        {
            // 스테이지 인덱스를 절대 진행도로 사용 (챕터 * 10 + 스테이지)
            int absoluteStage = (evt.Chapter - 1) * 10 + evt.StageIndex;
            SetProgress(QuestCondition.ClearStage, absoluteStage);
        }

        private void OnLevelUp(LevelUpEvent evt)
        {
            SetProgress(QuestCondition.LevelUp, evt.CurrentLevel);
        }

        private void OnDungeonCompleted(DungeonCompletedEvent evt)
        {
            AddProgress(QuestCondition.ClearDungeon, 1);
        }

        private void OnStatAllocated(StatAllocatedEvent evt)
        {
            AddProgress(QuestCondition.AllocateStats, evt.PointsSpent);
        }

        private void OnCurrencyChanged(CurrencyChangedEvent evt)
        {
            // 골드 소비 추적
            if (evt.Type == CurrencyType.Gold && evt.CurrentAmount < evt.PreviousAmount)
            {
                BigNumber spent = evt.PreviousAmount - evt.CurrentAmount;
                AddProgress(QuestCondition.SpendGold, spent.ToIntClamped());
            }
        }

        private void OnGachaResult(GachaResultEvent evt)
        {
            AddProgress(QuestCondition.GachaPull, 1);
            if (evt.PoolName == "Weapon")
                AddProgress(QuestCondition.WeaponGacha, 1);
            if (evt.PoolName == "Elite")
                AddProgress(QuestCondition.EliteSummon, 1);
        }

        private void OnEquipmentChanged(EquipmentChangedEvent evt)
        {
            if (evt.IsEquipped)
            {
                AddProgress(QuestCondition.EnhanceEquipment, 1);
                AddProgress(QuestCondition.EquipItem, 1);
            }
        }

        private void OnSkillLevelUp(SkillLevelUpEvent evt)
        {
            AddProgress(QuestCondition.SkillLevelUp, 1);
        }

        private void OnArenaMatch(ArenaMatchEvent evt)
        {
            AddProgress(QuestCondition.ArenaMatch, 1);
            // 티어 달성 추적 (티어 인덱스 + 1 = 달성 단계)
            SetProgress(QuestCondition.ArenaTierReach, evt.ArenaRank + 1);
        }

        private void OnTowerFloorReached(TowerFloorReachedEvent evt)
        {
            if (evt.IsNewHighest)
            {
                SetProgress(QuestCondition.TowerFloorClear, evt.Floor);
            }
        }

        private void OnQuestCompleted(QuestCompletedEvent evt)
        {
            AddProgress(QuestCondition.QuestComplete, 1);
        }

        private void OnSkillUsed(SkillUsedEvent evt)
        {
            AddProgress(QuestCondition.SkillUse, 1);
        }

        private void OnAttendanceChecked(AttendanceCheckedEvent evt)
        {
            AddProgress(QuestCondition.ReceiveAttendance, 1);
        }

        private void OnOfflineRewardClaimed(OfflineRewardClaimedEvent evt)
        {
            AddProgress(QuestCondition.ReceiveOfflineReward, 1);
        }

        private void OnBlessingChanged(BlessingChangedEvent evt)
        {
            AddProgress(QuestCondition.ReceiveBlessing, 1);
        }

        private void OnAchievementCompleted(AchievementCompletedEvent evt)
        {
            AddProgress(QuestCondition.AchievementClear, 1);
        }

        // OnCostumeSetCompleted: 2026-04-20 Costume 시스템 완전 제거
        // OnRelicEquipped: 2026-04-20 유물 시스템 완전 제거

        private void OnJobChanged(JobChangedEvent evt)
        {
            // 직업 선택(T0) = 1단계, 1차 전직(T1) = 2단계, ...
            SetProgress(QuestCondition.JobAdvance, evt.CurrentTier + 1);
        }

        private void OnCollectionRegistered(CollectionEntryRegisteredEvent evt)
        {
            // 도감 수집 수 카운터
            AddProgress(QuestCondition.CollectionCount, 1);

            // 도감 완성률(%) 업데이트 — 전체 도감 달성률 기반
            var cbm = CollectionBookManager.Instance;
            if (cbm != null)
            {
                int ratePercent = Mathf.FloorToInt(cbm.GetOverallCompletionRate() * 100f);
                SetProgress(QuestCondition.CollectionRate, ratePercent);
            }
        }

        private void OnQuickHuntCompleted(QuickHuntCompletedEvent evt)
        {
            AddProgress(QuestCondition.QuickHunt, evt.TicketsUsed);
        }

        // OnHeroPowerMilestone: 2026-04-20 HeroPower 시스템 완전 제거

        private void OnBoosterUsed(BoosterUsedEvent evt)
        {
            AddProgress(QuestCondition.UseBooster, 1);
        }

        // OnAbilityUnlocked: 2026-04-20 Ability 시스템 완전 제거

        private void OnStarForceResult(StarForceEvent evt)
        {
            if (evt.Result == StarForceResult.Success)
                SetProgress(QuestCondition.StarForceReach, evt.NewStarForce);
        }

        // OnArtifactEquipped: 2026-04-20 Artifact 시스템 완전 제거

        private void OnPotentialChanged(PotentialChangedEvent evt)
        {
            if (evt.Result is "Upgraded" or "Rerolled")
                SetProgress(QuestCondition.PotentialSet, evt.TotalPotentialSets);
        }

        // OnCostumeEquipped / OnClimberPowerUp: 2026-04-20 Costume/Climber 시스템 완전 제거

        private void OnGuildJoined(GuildJoinedEvent evt)
        {
            AddProgress(QuestCondition.GuildJoin, 1);
        }

        private void OnSummonLevelUp(SummonLevelUpEvent evt)
        {
            if (evt.PoolName == "Weapon")
                SetProgress(QuestCondition.WeaponSummonLevelReach, evt.NewLevel);
        }

        private void OnEliteSummonLevelUp(EliteSummonLevelUpEvent evt)
        {
            SetProgress(QuestCondition.EliteSummonLevelReach, evt.NewLevel);
        }

        // 엘리트 소환 실행마다 1회 진행 (OnGachaResult의 PoolName=="Elite" 분기는 구 경로 — 실 경로는 EliteSummonManager.TrySummon)
        private void OnEliteSummoned(EliteSummonedEvent evt)
        {
            AddProgress(QuestCondition.EliteSummon, 1);
        }

        private void OnBossRaidEntered(BossRaidEnteredEvent evt)
        {
            AddProgress(QuestCondition.BossRaidEntry, 1);
        }

        private void OnLoadCompleted(LoadCompletedEvent evt)
        {
            if (_questCatalog == null || _questCatalog.Length == 0) return;

            // 카탈로그 재초기화 + 세이브에서 진행도 복원
            Initialize(_questCatalog);

            var saveData = SaveManager.Instance?.CurrentData?.quest;

            // 활성 퀘스트 진행도 복원 (일일/주간/가이드 공통)
            if (saveData != null && saveData.activeQuests != null && saveData.activeQuests.Count > 0)
            {
                var restored = new List<QuestProgress>(saveData.activeQuests.Count);
                for (int i = 0; i < saveData.activeQuests.Count; i++)
                {
                    var src = saveData.activeQuests[i];
                    restored.Add(new QuestProgress
                    {
                        questId = src.questId,
                        currentAmount = src.currentAmount,
                        isCompleted = src.isCompleted,
                        isRewardClaimed = src.isRewardClaimed
                    });
                }
                RestoreProgress(restored, saveData.lastDailyReset, saveData.lastWeeklyReset);
            }

            if (saveData != null && saveData.completedGuideIndex >= 0)
            {
                RestoreGuideProgress(saveData.completedGuideIndex);
            }
            else
            {
                // 빈 세이브 → 처음부터 시작
                RestoreGuideProgress(0);
            }

            if (saveData != null && saveData.cyclingRound > 0)
            {
                RestoreCyclingProgress(saveData.cyclingCurrentOrder, saveData.cyclingRound);
            }

            Debug.Log($"[QuestManager] LoadCompleted 재초기화 — 가이드 인덱스 {_completedGuideIndex}, 활성 퀘스트 {_activeQuests.Count}");
        }

        // ── 순환 퀘스트 ──

        /// <summary>순환 퀘스트 진행도 복원 (세이브/로드용).</summary>
        public void RestoreCyclingProgress(int currentOrder, int round)
        {
            _cyclingCurrentOrder = currentOrder;
            _cyclingRound = round;
            _isGuideComplete = true;
            ActivateNextCyclingQuest();
        }

        /// <summary>다음 순환 퀘스트를 활성화한다.</summary>
        private void ActivateNextCyclingQuest()
        {
            if (_cyclingQuests.Count == 0) return;

            // 이미 활성 순환 퀘스트가 있고 미완료면 스킵
            if (!string.IsNullOrEmpty(_currentCyclingQuestId))
            {
                if (_progressMap.TryGetValue(_currentCyclingQuestId, out QuestProgress existing))
                {
                    if (!existing.isRewardClaimed) return;
                }
            }

            // 현재 순서의 순환 퀘스트 찾기
            QuestDataSO cyclingData = null;
            for (int i = 0; i < _cyclingQuests.Count; i++)
            {
                if (_cyclingQuests[i].cyclingOrder == _cyclingCurrentOrder)
                {
                    cyclingData = _cyclingQuests[i];
                    break;
                }
            }

            if (cyclingData == null && _cyclingQuests.Count > 0)
            {
                cyclingData = _cyclingQuests[0];
                _cyclingCurrentOrder = cyclingData.cyclingOrder;
            }

            if (cyclingData == null) return;

            // 이전 순환 퀘스트 진행도 제거 (재사용을 위해)
            if (!string.IsNullOrEmpty(_currentCyclingQuestId) && _currentCyclingQuestId != cyclingData.id)
            {
                _progressMap.Remove(_currentCyclingQuestId);
                _activeQuests.RemoveAll(p => p.questId == _currentCyclingQuestId);
            }

            // 현재 퀘스트 진행도도 리셋 (순환이므로)
            _progressMap.Remove(cyclingData.id);
            _activeQuests.RemoveAll(p => p.questId == cyclingData.id);

            _currentCyclingQuestId = cyclingData.id;
            ActivateQuest(cyclingData.id);

            EventBus.Publish(new CyclingQuestActivatedEvent
            {
                QuestId = cyclingData.id,
                CyclingOrder = cyclingData.cyclingOrder,
                Round = _cyclingRound,
                DisplayName = cyclingData.displayName,
                Description = cyclingData.description,
                RequiredAmount = cyclingData.requiredAmount,
                Condition = cyclingData.condition
            });

            Debug.Log($"[QuestManager] 순환 퀘스트 활성화: {cyclingData.displayName} (회차 {_cyclingRound}, 순서 {_cyclingCurrentOrder})");
        }

        /// <summary>순환 퀘스트 완료 후 다음으로 진행한다.</summary>
        private void AdvanceCyclingQuest()
        {
            if (_cyclingQuests.Count == 0) return;

            // 다음 순서로 이동
            int nextOrder = _cyclingCurrentOrder + 1;

            // 마지막 순서를 넘으면 다음 회차로
            int maxOrder = _cyclingQuests[_cyclingQuests.Count - 1].cyclingOrder;
            if (nextOrder > maxOrder)
            {
                _cyclingRound++;
                _cyclingCurrentOrder = _cyclingQuests[0].cyclingOrder;
                Debug.Log($"[QuestManager] 순환 퀘스트 {_cyclingRound}회차 시작");
            }
            else
            {
                _cyclingCurrentOrder = nextOrder;
            }

            SyncCyclingToSave();
            ActivateNextCyclingQuest();
        }

        /// <summary>회차에 따른 보상 스케일링. 회차당 10% 증가.</summary>
        private int GetScaledReward(int baseAmount)
        {
            if (_cyclingRound <= 1) return baseAmount;
            float multiplier = 1f + (_cyclingRound - 1) * 0.1f;
            return Mathf.RoundToInt(baseAmount * multiplier);
        }

        /// <summary>현재 활성 순환 퀘스트 데이터 조회 (UI용).</summary>
        public QuestDataSO GetCurrentCyclingData()
        {
            if (string.IsNullOrEmpty(_currentCyclingQuestId)) return null;
            return _questDataMap.TryGetValue(_currentCyclingQuestId, out QuestDataSO d) ? d : null;
        }

        /// <summary>현재 활성 순환 퀘스트 진행도 조회 (UI용).</summary>
        public QuestProgress GetCurrentCyclingProgress()
        {
            if (string.IsNullOrEmpty(_currentCyclingQuestId)) return null;
            return _progressMap.TryGetValue(_currentCyclingQuestId, out QuestProgress p) ? p : null;
        }

        /// <summary>순환 퀘스트 보상 수령 + 다음 진행. GoalGuideWidget에서 호출.</summary>
        public bool ClaimCyclingRewardAndAdvance()
        {
            if (string.IsNullOrEmpty(_currentCyclingQuestId)) return false;
            return ClaimReward(_currentCyclingQuestId);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 해당 날짜가 속한 주의 월요일 날짜를 반환한다.
        /// </summary>
        private static string GetMondayDate(DateTime date)
        {
            int diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            DateTime monday = date.AddDays(-diff);
            return monday.ToString("yyyy-MM-dd");
        }
    }
}
