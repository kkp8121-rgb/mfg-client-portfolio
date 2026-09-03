using UnityEngine;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Data;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 챕터-스테이지 진행을 관리하는 매니저.
    /// 탑다운 웨이브 기반 스폰 로직.
    /// 100마리 처치 → 미니보스 → 처치 시 다음 스테이지, 실패 시 무한 파밍.
    /// </summary>
    public class StageManager : MonoBehaviour
    {
        #region 설정값

        [Header("스테이지 설정")]
        [SerializeField] private int _monstersPerStage = 100;
        [SerializeField] private float _nextStageDelay = 2f;
        [SerializeField] private float _miniBossTimeout = 30f;
        [SerializeField] private int _stagesPerChapter = 9;

        [Header("초반 밸런스")]
        [Tooltip("챕터 1에서 스테이지당 필요 킬 수 (이후 챕터에서 점진적으로 증가)")]
        [SerializeField] private int _earlyStageKillCount = 10;
        [Tooltip("킬 수가 _monstersPerStage에 도달하는 챕터 번호")]
        [SerializeField] private int _killCountRampChapter = 5;

        [Header("참조")]
        [SerializeField] private MonsterSpawner _monsterSpawner;
        [SerializeField] private PlayerCharacter _player;
        [SerializeField] private ArenaMap _arenaMap;

        #endregion

        #region 상태

        private int _currentChapter = 1;
        private int _currentStageIndex = 1;
        private int _killCount;
        private bool _miniBossActive;
        private bool _chapterBossActive;
        private bool _isTransitioning;
        private bool _isFarmingMode;
        private int _bossFailCount; // 보스 연속 실패 횟수
        private CombatStats _playerStats;

        // 스턱 복구 워치독 (무한 진행 중 상태 멈춤 감지)
        [Header("스턱 복구")]
        [SerializeField] private float _stuckTimeout = 45f;
        private float _lastProgressTime;
        private int _watchdogToken;

        #endregion

        #region 프로퍼티

        public int CurrentChapter => _currentChapter;
        public int CurrentStageIndex => _currentStageIndex;
        public int KillCount => _killCount;
        public bool IsMiniBossActive => _miniBossActive;
        public bool IsChapterBossActive => _chapterBossActive;
        public bool IsFarmingMode => _isFarmingMode;
        public bool IsBossStage => _currentStageIndex > _stagesPerChapter;

        public string CurrentStageName =>
            IsBossStage ? $"{_currentChapter}-BOSS" : $"{_currentChapter}-{_currentStageIndex}";

        #endregion

        #region Unity 생명주기

        private void OnEnable()
        {
            EventBus.Subscribe<MonsterDiedEvent>(OnMonsterDied);
            EventBus.SubscribeSticky<BeforeSaveEvent>(OnBeforeSave);
            // 2026-04-23 P0: LoadCompletedEvent 구독 추가 — 재시작 시 progress.currentFloor 복원
            // (Save/Load 감사 에이전트 발견 Critical — 매 로드마다 스테이지 Lv1로 초기화되던 잠재 버그)
            EventBus.SubscribeSticky<LoadCompletedEvent>(OnLoadCompleted);
            EventBus.Subscribe<QuestStateRefreshEvent>(OnQuestStateRefresh);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<MonsterDiedEvent>(OnMonsterDied);
            EventBus.Unsubscribe<BeforeSaveEvent>(OnBeforeSave);
            EventBus.Unsubscribe<LoadCompletedEvent>(OnLoadCompleted);
            EventBus.Unsubscribe<QuestStateRefreshEvent>(OnQuestStateRefresh);
            UnsubscribePlayerDeath();
        }

        private void OnQuestStateRefresh(QuestStateRefreshEvent evt)
        {
            if (evt.Condition == QuestCondition.ClearStage)
            {
                EventBus.Publish(new StageChangedEvent
                {
                    Chapter = _currentChapter,
                    StageIndex = _currentStageIndex,
                    DisplayName = CurrentStageName
                });
            }
        }

        private void Start()
        {
            if (_player == null)
                _player = FindFirstObjectByType<PlayerCharacter>();

            if (_arenaMap == null)
                _arenaMap = FindFirstObjectByType<ArenaMap>();

            SubscribePlayerDeath();

            DelayedStartAsync().Forget();
        }

        private async UniTaskVoid DelayedStartAsync()
        {
            await UniTask.Yield(cancellationToken: destroyCancellationToken);

            // SaveData에서 진행 상태 복원
            var data = SaveManager.Instance?.CurrentData;
            if (data != null && data.progress.maxFloor > 0)
            {
                int floor = data.progress.currentFloor;
                if (floor <= 0) floor = data.progress.maxFloor;
                int chapter = (floor - 1) / (_stagesPerChapter + 1) + 1;
                int stage = (floor - 1) % (_stagesPerChapter + 1) + 1;
                _currentChapter = Mathf.Max(1, chapter);
                _currentStageIndex = Mathf.Max(1, stage);
                Debug.Log($"[StageManager] 세이브에서 복원: 챕터 {_currentChapter}, 스테이지 {_currentStageIndex} (floor={floor})");
            }

            StartStage(_currentChapter, _currentStageIndex);
        }

        #endregion

        #region 스테이지 시작

        public void StartStage(int chapter, int stageIndex)
        {
            // 참조 안전 확인 (씬 로드 직후 null일 수 있음)
            if (_player == null)
                _player = FindFirstObjectByType<PlayerCharacter>();
            if (_monsterSpawner == null)
                _monsterSpawner = FindFirstObjectByType<MonsterSpawner>();

            _currentChapter = Mathf.Max(1, chapter);
            _currentStageIndex = Mathf.Max(1, stageIndex);
            _killCount = 0;
            _miniBossActive = false;
            _chapterBossActive = false;
            _isTransitioning = false;
            _isFarmingMode = false;

            if (_player != null)
            {
                _player.ResetToSpawnPoint();
                var stats = _player.GetComponent<CombatStats>();
                if (stats != null)
                    stats.ResetHp();
            }

            if (LootManager.Instance != null)
                LootManager.Instance.SetFloor(GetEffectiveFloor());

            EventBus.Publish(new StageChangedEvent
            {
                Chapter = _currentChapter,
                StageIndex = _currentStageIndex,
                DisplayName = CurrentStageName
            });

            // 진행 상태 저장
            SyncProgressToSave();

            if (IsBossStage)
            {
                // 보스 스테이지: 챕터 보스만 스폰
                _chapterBossActive = true;
                if (_monsterSpawner != null)
                {
                    _monsterSpawner.StopSpawning();
                    _monsterSpawner.SpawnChapterBoss(GetEffectiveFloor());
                }
#if UNITY_EDITOR
                Debug.Log($"[StageManager] {CurrentStageName} 챕터 보스 등장!");
#endif
            }
            else
            {
                // 일반 스테이지: 웨이브 스폰
                if (_monsterSpawner != null)
                    _monsterSpawner.StartStage(_currentChapter, _currentStageIndex);
#if UNITY_EDITOR
                Debug.Log($"[StageManager] {CurrentStageName} 시작 (목표: {GetRequiredKills()}마리, 챕터{_currentChapter})");
#endif
            }

            // 무한 진행 대응: 스턱 상태 워치독 시작
            _lastProgressTime = Time.time;
            StuckWatchdogAsync(++_watchdogToken).Forget();
        }

        private int GetEffectiveFloor()
        {
            return (_currentChapter - 1) * (_stagesPerChapter + 1) + _currentStageIndex;
        }

        /// <summary>
        /// 현재 챕터에 따라 스테이지 클리어에 필요한 킬 수를 반환한다.
        /// 초반 챕터는 적은 킬 수로 빠르게 진행, 이후 점진적으로 증가.
        /// </summary>
        private int GetRequiredKills()
        {
            if (_currentChapter >= _killCountRampChapter)
                return _monstersPerStage;

            // 챕터 1~(rampChapter-1)에서 선형 보간: earlyKill → monstersPerStage
            float t = (float)(_currentChapter - 1) / Mathf.Max(1, _killCountRampChapter - 1);
            return Mathf.RoundToInt(Mathf.Lerp(_earlyStageKillCount, _monstersPerStage, t));
        }

        #endregion

        #region 스테이지 진행

        private void AdvanceToNextStage()
        {
            // 다음 스테이지 진입 시 무적 해제
            if (_player != null)
            {
                var stats = _player.GetComponent<CombatStats>();
                if (stats != null)
                    stats.IsInvincible = false;
            }

            int prevChapter = _currentChapter;

            if (IsBossStage)
            {
                _currentChapter++;
                _currentStageIndex = 1;
#if UNITY_EDITOR
                Debug.Log($"[StageManager] 챕터 {prevChapter} 클리어! → 챕터 {_currentChapter} 시작");
#endif
            }
            else if (_currentStageIndex >= _stagesPerChapter)
            {
                _currentStageIndex = _stagesPerChapter + 1;
#if UNITY_EDITOR
                Debug.Log($"[StageManager] → {CurrentStageName}");
#endif
            }
            else
            {
                _currentStageIndex++;
#if UNITY_EDITOR
                Debug.Log($"[StageManager] → {CurrentStageName}");
#endif
            }

            StartStage(_currentChapter, _currentStageIndex);
        }

        private async UniTaskVoid AdvanceAfterDelayAsync(float delay)
        {
            _isTransitioning = true;
            await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: destroyCancellationToken);
            _isTransitioning = false;
            AdvanceToNextStage();
        }

        /// <summary>
        /// 현재 스테이지를 재도전한다.
        /// 보스 스테이지 실패 시 이전 스테이지로 후퇴하여 파밍 후 재도전.
        /// </summary>
        public void RetryStage()
        {
            if (_player == null)
                _player = FindFirstObjectByType<PlayerCharacter>();
            if (_monsterSpawner == null)
                _monsterSpawner = FindFirstObjectByType<MonsterSpawner>();

            _isFarmingMode = false;

            if (_monsterSpawner != null)
                _monsterSpawner.ClearAllMonsters();

            EventBus.Publish(new FarmingModeEvent { IsActive = false, StageName = CurrentStageName });

            if (_player != null)
            {
                var stats = _player.GetComponent<CombatStats>();
                if (stats != null)
                {
                    stats.IsInvincible = false;
                    stats.ReviveWithRatio(1f);
                }
                _player.Revive();
            }

            // 보스 스테이지 실패 → 직전 스테이지에서 무한 파밍 (유저 재도전 선택)
            if (IsBossStage)
            {
                _bossFailCount++;
                _currentStageIndex = _stagesPerChapter; // 보스 직전 스테이지 (9)
#if UNITY_EDITOR
                Debug.Log($"[StageManager] 보스 실패 ({_bossFailCount}회) → {_currentChapter}-{_currentStageIndex}에서 무한 파밍. 유저가 재도전 선택.");
#endif
                StartStage(_currentChapter, _currentStageIndex);
                // 즉시 무한 파밍 모드 진입 (무적 + 몬스터 스폰 계속)
                EnterFarmingMode();
                return;
            }

            _bossFailCount = 0;
#if UNITY_EDITOR
            Debug.Log($"[StageManager] {CurrentStageName} 재도전");
#endif
            StartStage(_currentChapter, _currentStageIndex);
        }

        #endregion

        #region 플레이어 사망 구독

        private void SubscribePlayerDeath()
        {
            UnsubscribePlayerDeath();

            if (_player == null)
                _player = FindFirstObjectByType<PlayerCharacter>();

            if (_player != null)
                _playerStats = _player.GetComponent<CombatStats>();

            if (_playerStats != null)
                _playerStats.OnDeath += OnPlayerDeath;
        }

        private void UnsubscribePlayerDeath()
        {
            if (_playerStats != null)
                _playerStats.OnDeath -= OnPlayerDeath;

            _playerStats = null;
        }

        private void OnPlayerDeath()
        {
            if (IsBossStage)
            {
                // 보스 스테이지: 전체 재시작 (킬카운트 리셋)
                RetryStage();
                return;
            }

            // 일반 스테이지: 킬카운트 유지하며 즉시 부활 (진행도 보존)
            if (_player != null)
            {
                var stats = _player.GetComponent<CombatStats>();
                if (stats != null)
                    stats.ReviveWithRatio(1f);
                _player.Revive();
            }
        }

        #endregion

        #region 이벤트 핸들러

        private void OnMonsterDied(MonsterDiedEvent evt)
        {
            if (_isTransitioning) return;

            _lastProgressTime = Time.time;

            if (_chapterBossActive)
            {
                OnChapterBossDefeated();
                return;
            }

            if (_miniBossActive)
            {
                OnMiniBossDefeated();
                return;
            }

            _killCount++;
            int required = GetRequiredKills();

            Debug.Log($"[StageManager] {CurrentStageName} 처치 진행: {_killCount}/{required}");

            EventBus.Publish(new StageProgressEvent
            {
                KillCount = _killCount,
                RequiredKills = required,
                StageName = CurrentStageName
            });

            if (_killCount >= required && !IsBossStage)
            {
                SpawnMiniBoss();
            }
        }

        #endregion

        #region 미니보스

        private void SpawnMiniBoss()
        {
            _miniBossActive = true;

            // 보스 BGM 전환
            AudioManager.Instance?.PlayBgm(BgmType.Boss, 0.8f);

            if (_monsterSpawner != null)
            {
                _monsterSpawner.StopSpawning();
                _monsterSpawner.SpawnMiniBoss(GetEffectiveFloor());
            }

            MiniBossTimeoutAsync().Forget();

#if UNITY_EDITOR
            Debug.Log($"[StageManager] {CurrentStageName} 미니보스 등장!");
#endif
        }

        private async UniTaskVoid MiniBossTimeoutAsync()
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(_miniBossTimeout), cancellationToken: destroyCancellationToken);

            if (_miniBossActive)
                OnMiniBossFailed();
        }

        private void OnMiniBossDefeated()
        {
            _miniBossActive = false;

            // 챕터 BGM 복귀
            AudioManager.Instance?.PlayChapterBgm(_currentChapter);

#if UNITY_EDITOR
            Debug.Log($"[StageManager] {CurrentStageName} 미니보스 처치! → 다음 스테이지");
#endif
            AdvanceAfterDelayAsync(_nextStageDelay).Forget();
        }

        private void OnMiniBossFailed()
        {
            _miniBossActive = false;

            if (_monsterSpawner != null)
                _monsterSpawner.DespawnMiniBoss();

            EnterFarmingMode();

#if UNITY_EDITOR
            Debug.Log($"[StageManager] {CurrentStageName} 미니보스 타임아웃 → 무한 파밍 모드");
#endif
        }

        public void EnterFarmingMode()
        {
            if (_isFarmingMode) return;
            _isFarmingMode = true;
            _miniBossActive = false;
            _chapterBossActive = false;
            _killCount = 0;

            bool playerDead = _player != null && _player.GetComponent<CombatStats>()?.IsDead == true;

            if (playerDead)
            {
                // 사망으로 인한 진입: 자동 부활 타이머 취소, 스폰+몬스터 제거, 재도전 대기
                var combat = _player.GetComponent<CharacterCombat>();
                if (combat != null)
                    combat.CancelRevive();

                if (_monsterSpawner != null)
                {
                    _monsterSpawner.StopSpawning();
                    _monsterSpawner.ClearAllMonsters();
                }

#if UNITY_EDITOR
                Debug.Log($"[StageManager] {CurrentStageName} 사망 — 재도전 대기");
#endif
            }
            else
            {
                // 보스 타임아웃 등으로 진입: 플레이어 생존, 무적 파밍
                if (_player != null)
                {
                    var stats = _player.GetComponent<CombatStats>();
                    if (stats != null)
                        stats.IsInvincible = true;
                }

                if (_monsterSpawner != null)
                    _monsterSpawner.ResumeSpawning();

#if UNITY_EDITOR
                Debug.Log($"[StageManager] {CurrentStageName} 무한 파밍 모드 진입");
#endif
            }

            EventBus.Publish(new FarmingModeEvent { IsActive = true, StageName = CurrentStageName, IsPlayerDead = playerDead });
        }

        #endregion

        #region 스턱 복구 워치독

        /// <summary>
        /// 무한 진행 안전장치. 일정 시간 진행도(몬스터 처치/전환)가 없으면 자동 복구.
        /// 보스 스테이지에서 보스가 사라졌거나, 몬스터 스폰이 끊기거나, 플레이어가 멈춘 상황을 감지한다.
        /// </summary>
        private async UniTaskVoid StuckWatchdogAsync(int token)
        {
            int chapterSnapshot = _currentChapter;
            int stageSnapshot = _currentStageIndex;
            float checkInterval = 5f;

            while (!destroyCancellationToken.IsCancellationRequested)
            {
                await UniTask.Delay(System.TimeSpan.FromSeconds(checkInterval), cancellationToken: destroyCancellationToken);

                // 다른 워치독이 시작되었거나 스테이지가 바뀌면 종료
                if (token != _watchdogToken) return;
                if (_currentChapter != chapterSnapshot || _currentStageIndex != stageSnapshot) return;
                if (_isTransitioning || _isFarmingMode) return;

                if (Time.time - _lastProgressTime < _stuckTimeout) continue;

                // 스턱 감지됨 → 상황에 따라 복구
                Debug.LogWarning($"[StageManager] 스턱 감지 {CurrentStageName} ({_stuckTimeout:F0}s 무진행). 복구 시도.");

                if (_chapterBossActive)
                {
                    // 보스가 살아있으나 상호작용 불가 상태 → 보스 리스폰
                    if (_monsterSpawner != null)
                    {
                        _monsterSpawner.ClearAllMonsters();
                        _monsterSpawner.SpawnChapterBoss(GetEffectiveFloor());
                    }
                    _lastProgressTime = Time.time;
                    Debug.Log($"[StageManager] 보스 리스폰 복구");
                }
                else if (_miniBossActive)
                {
                    // 미니보스 멈춤 → 타임아웃 처리
                    OnMiniBossFailed();
                    return;
                }
                else
                {
                    // 일반 스테이지에서 스폰 멈춤 → 스폰 재개
                    if (_monsterSpawner != null)
                    {
                        _monsterSpawner.ClearAllMonsters();
                        _monsterSpawner.StartStage(_currentChapter, _currentStageIndex);
                    }
                    _lastProgressTime = Time.time;
                    Debug.Log($"[StageManager] 스폰 재개 복구");
                }
            }
        }

        #endregion

        #region 챕터 보스

        private void OnChapterBossDefeated()
        {
            _chapterBossActive = false;
            _bossFailCount = 0;

            // 챕터 BGM 복귀
            AudioManager.Instance?.PlayChapterBgm(_currentChapter);

            if (_monsterSpawner != null)
                _monsterSpawner.ClearAllMonsters();

#if UNITY_EDITOR
            Debug.Log($"[StageManager] 챕터 {_currentChapter} 보스 처치!");
#endif
            AdvanceAfterDelayAsync(_nextStageDelay).Forget();
        }

        #endregion

        #region 세이브 동기화

        private void OnBeforeSave(BeforeSaveEvent evt)
        {
            SyncProgressToSave();
        }

        private void SyncProgressToSave()
        {
            var data = SaveManager.Instance?.CurrentData;
            if (data == null) return;

            int floor = GetEffectiveFloor();
            data.progress.currentFloor = floor;
            if (floor > data.progress.maxFloor)
                data.progress.maxFloor = floor;
        }

        /// <summary>
        /// 2026-04-23 P0 수정: Save/Load 감사 에이전트 발견.
        /// 이전엔 OnBeforeSave만 있고 Load 시점에 progress.currentFloor를 읽지 않아
        /// 서버 sync 등 게임 실행 중 재로드 시 스테이지가 동기화 안 됐음.
        /// (DelayedStartAsync는 최초 Start 1회만 호출되므로 그 이후 Load 이벤트는 미반영)
        /// </summary>
        private void OnLoadCompleted(LoadCompletedEvent evt)
        {
            var data = SaveManager.Instance?.CurrentData;
            if (data == null) return;

            int floor = data.progress.currentFloor;
            if (floor <= 0) floor = data.progress.maxFloor;
            if (floor <= 0) return; // fresh save — 복원할 것 없음

            // DelayedStartAsync와 동일 로직 (floor = (chapter-1) * (_stagesPerChapter + 1) + stageIndex)
            int perChapter = Mathf.Max(1, _stagesPerChapter + 1);
            int chapter = (floor - 1) / perChapter + 1;
            int stage = (floor - 1) % perChapter + 1;
            _currentChapter = Mathf.Max(1, chapter);
            _currentStageIndex = Mathf.Max(1, stage);
#if UNITY_EDITOR
            Debug.Log($"[StageManager] LoadCompleted 복원: floor={floor} → Ch{_currentChapter}-{_currentStageIndex}");
#endif
        }

        #endregion
    }
}
