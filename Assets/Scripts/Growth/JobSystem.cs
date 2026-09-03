using UnityEngine;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Combat;
// Dungeon 참조 제거 (순환 의존 방지) — TowerManager는 SaveData로 접근
using MkLike.Utils;

namespace MkLike.Growth
{
    /// <summary>
    /// 직업 및 전직 관리 시스템.
    /// 현재 직업(JobType)과 전직 단계(tier 0~3)를 관리하며,
    /// 레벨업 이벤트를 구독하여 전직 조건 충족 시 자동 전직한다.
    /// </summary>
    public class JobSystem : MonoBehaviour
    {
        public static JobSystem Instance { get; private set; }

        /// <summary>현재 직업</summary>
        public JobType CurrentJob { get; private set; } = JobType.Warrior;

        /// <summary>현재 전직 단계 (0~4)</summary>
        public int CurrentTier { get; private set; }

        /// <summary>최대 전직 단계</summary>
        private const int MAX_TIER = 4;

        /// <summary>4차 전직 필요 최소 탑 층수</summary>
        private const int TIER4_REQUIRED_FLOOR = 300;

        /// <summary>전직 티어별 보너스 비율 (1차:5%, 2차:8%, 3차:12%, 4차:15%)</summary>
        private static readonly float[] TierBonusPercents = { 0.05f, 0.08f, 0.12f, 0.15f };

        /// <summary>전직 필요 레벨 (Tier 0→1: 40, 1→2: 80, 2→3: 120, 3→4: 160)</summary>
        private static readonly int[] AdvancementLevels = { 40, 80, 120, 160 };

        /// <summary>직업 선택 잠금 (한 번 선택하면 변경 불가)</summary>
        private bool _isJobLocked;

        /// <summary>직업별 티어별 표시 이름</summary>
        private static readonly string[,] JobDisplayNames =
        {
            // Warrior
            { "견습 전사", "나이트", "워로드", "타이탄", "드래곤 슬레이어" },
            // Archer
            { "견습 궁수", "스카우트", "윈드워커", "호크아이", "스톰브링어" },
            // Mage
            { "견습 마법사", "소서러", "세이지", "룬마스터", "아크메이지" }
        };

        private CombatStats _playerStats;

        /// <summary>다음 전직에 필요한 레벨. 최대 티어면 -1 반환.</summary>
        public int NextAdvancementLevel
        {
            get
            {
                if (CurrentTier >= MAX_TIER)
                    return -1;
                return AdvancementLevels[CurrentTier];
            }
        }

        /// <summary>전직이 가능한 상태인지 (최대 티어 미만)</summary>
        public bool CanAdvance => CurrentTier < MAX_TIER;

        /// <summary>직업이 이미 확정되었는지</summary>
        public bool IsJobLocked => _isJobLocked;

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
            Initialize();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Subscribe<QuestStateRefreshEvent>(OnQuestStateRefresh);
            EventBus<BeforeSaveEvent>.SubscribeSticky(OnBeforeSave);
            EventBus<LoadCompletedEvent>.SubscribeSticky(OnLoadCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Unsubscribe<QuestStateRefreshEvent>(OnQuestStateRefresh);
            EventBus<BeforeSaveEvent>.Unsubscribe(OnBeforeSave);
            EventBus<LoadCompletedEvent>.Unsubscribe(OnLoadCompleted);
        }

        private void OnBeforeSave(BeforeSaveEvent evt) => SaveJobData();

        private void OnLoadCompleted(LoadCompletedEvent evt) => Initialize();

        private void OnQuestStateRefresh(QuestStateRefreshEvent evt)
        {
            // 2026-04-23 P0 #9 수정: CurrentJobId 누락 시 SpumCharacterManager.OnJobChanged가
            // switch default인 JobType.Warrior로 떨어져 SPUM이 Warrior로 교체되던 버그.
            // QuestCondition.JobAdvance 트리거인 퀘스트 상태 알림용 이벤트지만 SPUM 구독자도
            // 같은 이벤트를 받으므로 CurrentJobId 필수 포함.
            if (evt.Condition == QuestCondition.JobAdvance && CurrentTier >= 0)
                EventBus.Publish(new JobChangedEvent
                {
                    CurrentJobId = JobTypeToId(CurrentJob),
                    PreviousJobId = JobTypeToId(CurrentJob),
                    CurrentTier = CurrentTier,
                    PreviousTier = CurrentTier
                });
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// SaveData에서 직업/전직 정보를 복원한다.
        /// jobId가 비어있으면 Warrior/Tier0 기본값을 사용한다.
        /// </summary>
        public void Initialize()
        {
            SaveData saveData = SaveManager.Instance != null
                ? SaveManager.Instance.CurrentData
                : null;

            if (saveData == null)
            {
                CurrentJob = JobType.Warrior;
                CurrentTier = 0;
                Debug.Log("[JobSystem] SaveData 없음 — 기본값 Warrior/T0");
                return;
            }

            PlayerData playerData = saveData.player;

            if (string.IsNullOrEmpty(playerData.jobId))
            {
                CurrentJob = JobType.Warrior;
                CurrentTier = 0;
                playerData.jobId = "warrior";
                playerData.jobTier = 0;
                _isJobLocked = false;
                Debug.Log("[JobSystem] jobId 비어있음 — 기본값 Warrior/T0 설정");
            }
            else
            {
                CurrentJob = ParseJobId(playerData.jobId);
                CurrentTier = Mathf.Clamp(playerData.jobTier, 0, MAX_TIER);
                _isJobLocked = true; // 이미 직업이 선택된 상태
            }

            Debug.Log($"[JobSystem] 초기화 완료: {GetJobDisplayName(CurrentJob, CurrentTier)} (T{CurrentTier})");

            // 현재 티어에 맞는 modifier 재적용 (1~4차 모두)
            if (CurrentTier >= 1)
            {
                ApplyJobTierModifiers(CurrentTier);
            }
        }

        /// <summary>
        /// 기본 직업을 최초 선택한다 (UI에서 호출).
        /// 한 번 선택하면 변경 불가 (직업이 이미 설정된 경우 무시).
        /// </summary>
        /// <param name="newJob">선택할 직업</param>
        public void ChangeJob(JobType newJob)
        {
            // 이미 직업이 선택된 상태면 변경 불가
            if (_isJobLocked)
            {
                Debug.LogWarning("[JobSystem] 직업이 이미 선택됨 — 변경 불가");
                return;
            }

            string previousJobId = JobTypeToId(CurrentJob);
            int previousTier = CurrentTier;

            CurrentJob = newJob;
            CurrentTier = 0;

            // 직업 변경 시 기존 전직 modifier 제거
            CachePlayerStats();
            if (_playerStats != null)
            {
                _playerStats.SetCpReason("전직");
                _playerStats.ClearModifiers(ModifierSource.Job);
            }

            _isJobLocked = true; // 선택 확정 후 잠금
            SaveJobData();

            EventBus.Publish(new JobChangedEvent
            {
                PreviousJobId = previousJobId,
                CurrentJobId = JobTypeToId(CurrentJob),
                PreviousTier = previousTier,
                CurrentTier = CurrentTier
            });

            // 2026-04-23 침묵 액션 보완: 직업 선택 FeedbackBus Toast + 쉐이크
            MkLike.Core.FeedbackBus.Emit(
                MkLike.Core.FeedbackKind.Generic,
                $"{GetJobDisplayName(CurrentJob, CurrentTier)} 선택!",
                shakeIntensity: 2);

            // 2026-04-23 SFX 보완: 직업 선택 오디오
            MkLike.Core.AudioManager.Instance?.PlaySfx(MkLike.Core.SfxType.JobAdvance);

            Debug.Log($"[JobSystem] 직업 변경: {GetJobDisplayName(CurrentJob, CurrentTier)}");
        }

        /// <summary>
        /// 직업과 티어에 해당하는 표시 이름을 반환한다.
        /// </summary>
        /// <param name="job">직업 타입</param>
        /// <param name="tier">전직 단계 (0~3)</param>
        /// <returns>표시 이름 (예: "나이트")</returns>
        public static string GetJobDisplayName(JobType job, int tier)
        {
            int jobIndex = (int)job;
            int clampedTier = Mathf.Clamp(tier, 0, MAX_TIER);

            if (jobIndex < 0 || jobIndex >= JobDisplayNames.GetLength(0))
                return "알 수 없는 직업";

            return JobDisplayNames[jobIndex, clampedTier];
        }

        /// <summary>
        /// 현재 직업의 표시 이름을 반환한다.
        /// </summary>
        public string GetCurrentDisplayName()
        {
            return GetJobDisplayName(CurrentJob, CurrentTier);
        }

        /// <summary>
        /// 레벨업 이벤트 핸들러.
        /// 전직 가능 레벨 도달 시 자동으로 전직한다.
        /// </summary>
        private void OnLevelUp(LevelUpEvent evt)
        {
            if (!CanAdvance)
                return;

            if (evt.CurrentLevel >= NextAdvancementLevel)
            {
                // 4차 전직: 레벨 조건만 (탑 조건 삭제 — 레벨 160 달성 시 자동 전직)

                Advance();
            }
        }

        /// <summary>
        /// 전직을 수행한다. 티어를 1 올리고 JobChangedEvent를 발행한다.
        /// </summary>
        private void Advance()
        {
            string previousJobId = JobTypeToId(CurrentJob);
            int previousTier = CurrentTier;

            CurrentTier++;

            SaveJobData();

            // 전직 시 CombatStats modifier 적용 (1~4차 모두)
            ApplyJobTierModifiers(CurrentTier);

            EventBus.Publish(new JobChangedEvent
            {
                PreviousJobId = previousJobId,
                CurrentJobId = JobTypeToId(CurrentJob),
                PreviousTier = previousTier,
                CurrentTier = CurrentTier
            });

            // 2026-04-23 이슈 15 FeedbackBus: 전직 시 Toast + 강한 쉐이크 (큰 변화)
            MkLike.Core.FeedbackBus.Emit(
                MkLike.Core.FeedbackKind.Generic,
                $"전직! {GetJobDisplayName(CurrentJob, previousTier)} → {GetJobDisplayName(CurrentJob, CurrentTier)}",
                shakeIntensity: 3);

            // 2026-04-23 SFX 누락 보완: JobAdvance 오디오
            MkLike.Core.AudioManager.Instance?.PlaySfx(MkLike.Core.SfxType.JobAdvance);

            Debug.Log($"[JobSystem] 전직! {GetJobDisplayName(CurrentJob, previousTier)} → {GetJobDisplayName(CurrentJob, CurrentTier)} (T{CurrentTier})");
        }

        /// <summary>
        /// 전직 티어 보너스를 CombatStats에 적용한다.
        /// 티어별 누적: 현재 티어까지의 보너스를 합산하여 적용.
        /// 1차: +5%, 2차: +13%(5+8), 3차: +25%(5+8+12), 4차: +40%(5+8+12+15)
        /// </summary>
        private void ApplyJobTierModifiers(int tier)
        {
            CachePlayerStats();
            if (_playerStats == null) return;

            _playerStats.SetCpReason("전직");
            _playerStats.ClearModifiers(ModifierSource.Job);

            if (tier <= 0) return;

            // 티어별 보너스 누적 합산
            float totalPercent = 0f;
            int maxIndex = Mathf.Min(tier, TierBonusPercents.Length);
            for (int i = 0; i < maxIndex; i++)
            {
                totalPercent += TierBonusPercents[i];
            }

            string sourceId = $"job_tier{tier}_{JobTypeToId(CurrentJob)}";
            _playerStats.AddModifier("job_tier_atk", new StatModifier(
                ModifierSource.Job, sourceId, StatType.Atk, 0f, totalPercent));
            _playerStats.AddModifier("job_tier_def", new StatModifier(
                ModifierSource.Job, sourceId, StatType.Def, 0f, totalPercent));
            _playerStats.AddModifier("job_tier_hp", new StatModifier(
                ModifierSource.Job, sourceId, StatType.MaxHp, 0f, totalPercent));

            Debug.Log($"[JobSystem] T{tier} modifier 적용: ATK/DEF/HP +{totalPercent * 100:F0}%");
        }

        private void CachePlayerStats()
        {
            if (_playerStats == null)
            {
                var player = FindFirstObjectByType<PlayerCharacter>();
                if (player != null)
                    _playerStats = player.GetComponent<CombatStats>();
            }
        }

        /// <summary>
        /// 현재 직업/티어를 SaveData에 기록한다.
        /// </summary>
        private void SaveJobData()
        {
            if (SaveManager.Instance == null)
                return;

            SaveData saveData = SaveManager.Instance.CurrentData;
            if (saveData == null)
                return;

            saveData.player.jobId = JobTypeToId(CurrentJob);
            saveData.player.jobTier = CurrentTier;
        }

        /// <summary>
        /// 문자열 jobId를 JobType으로 변환한다.
        /// </summary>
        private static JobType ParseJobId(string jobId)
        {
            switch (jobId.ToLowerInvariant())
            {
                case "warrior": return JobType.Warrior;
                case "archer": return JobType.Archer;
                case "mage": return JobType.Mage;
                default:
                    Debug.LogWarning($"[JobSystem] 알 수 없는 jobId: {jobId} — Warrior로 대체");
                    return JobType.Warrior;
            }
        }

        /// <summary>
        /// JobType을 저장용 문자열로 변환한다.
        /// </summary>
        private static string JobTypeToId(JobType job)
        {
            switch (job)
            {
                case JobType.Warrior: return "warrior";
                case JobType.Archer: return "archer";
                case JobType.Mage: return "mage";
                default: return "warrior";
            }
        }
    }
}
