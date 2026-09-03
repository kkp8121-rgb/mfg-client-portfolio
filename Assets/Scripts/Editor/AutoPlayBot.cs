#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Data;
using MkLike.Quest;
using MkLike.Economy;
using MkLike.Equipment;
using MkLike.Combat;
using MkLike.Growth;
using MkLike.Dungeon;
using MkLike.Guild;
using MkLike.Arena;
using MkLike.Utils;
using MkLike.UI;
using MkLike.UI.Onboarding;
using MkLike.Editor;

namespace MkLike.Editor
{
    /// <summary>
    /// 가이드 퀘스트 실플레이 봇.
    /// TimeScale=1, 치트 없음. 모든 행동은 실제 UI 버튼 클릭으로 수행.
    /// </summary>
    public class AutoPlayBot : EditorWindow
    {
        private const int TOTAL_GUIDE = 350;

        // ── 상태 ──
        private bool _isRunning;
        private float _gameStartTime;
        private float _lastQuestStartTime;
        private double _lastTickTime;
        private int _lastCompletedIndex = -1;
        private bool _jobChosen;

        // 행동 스케줄
        private float _busyUntil;   // Time.time 기준, 이 시각까지 행동 안 함

        // 멀티스텝 행동 (탭 열기 → 실행 → 대기 → 닫기)
        private enum ActionPhase { None, TabOpened, Executed }
        private ActionPhase _phase;
        private string _pendingAction; // 실행할 행동 이름

        // ── 설정 ──
        private float _tickInterval = 2f;
        private bool _freshStart = true;
        private JobType _selectedJob = JobType.Warrior; // /run warrior|archer|mage 로 변경

        /// <summary>
        /// Marathon 모드 — /marathon 커맨드 전용.
        /// 대량 요구치 퀘스트 자동 skip + stuck skip 임계 완화로 350 완주 시간 단축.
        /// "실 UI 플레이" 의미는 약화되지만 **시스템 플로우 전구간 커버리지** 검증 목적.
        /// </summary>
        private bool _marathonMode = false;
        public bool MarathonMode { get => _marathonMode; set => _marathonMode = value; }

        // ── 온보딩 ──
        private bool _onboardingDone;
        private float _onboardingNextActAt;

        // ── 로그 ──
        private readonly List<QuestLogEntry> _questLogs = new();
        private readonly StringBuilder _liveLog = new();
        private Vector2 _scrollPos;
        private string _statusText = "대기 중";

        // ── 통계 ──
        private int _totalGachaPulls;
        private int _totalEnhancements;
        private int _stuckTickCount;
        private string _lastStuckQuestId;
        private int _lastStuckProgress; // 진행도 변화 추적
        private int _totalTickCount;

        private struct QuestLogEntry
        {
            public int GuideIndex;
            public string QuestId;
            public string DisplayName;
            public string Condition;
            public int RequiredAmount;
            public float StartTime;
            public float EndTime;
            public float Duration => EndTime - StartTime;
        }

        [MenuItem("Tools/AutoPlay Bot")]
        static void ShowWindow() => GetWindow<AutoPlayBot>("AutoPlay Bot").minSize = new Vector2(500, 600);

        private void OnEnable() => EditorApplication.update += Tick;
        private void OnDisable() => EditorApplication.update -= Tick;

        // ════════════════════════════════════
        //  GUI
        // ════════════════════════════════════
        private void OnGUI()
        {
            GUILayout.Label("AutoPlay Bot — UI 클릭 기반 실플레이", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(_isRunning);
            _freshStart = EditorGUILayout.Toggle("처음부터 (세이브 삭제)", _freshStart);
            _selectedJob = (JobType)EditorGUILayout.EnumPopup("선택 직업", _selectedJob);
            _tickInterval = EditorGUILayout.Slider("틱 간격(초)", _tickInterval, 1f, 5f);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(4);
            if (!_isRunning)
            {
                GUI.backgroundColor = new Color(0.3f, 0.8f, 0.3f);
                if (GUILayout.Button("▶  시작", GUILayout.Height(35))) StartBot();
            }
            else
            {
                GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
                if (GUILayout.Button("■  중지 + 리포트", GUILayout.Height(35))) StopBot("수동 중지");
            }
            GUI.backgroundColor = Color.white;

            if (_isRunning && EditorApplication.isPlaying)
            {
                EditorGUILayout.LabelField("상태", _statusText);
                EditorGUILayout.LabelField("경과", FormatTime(Time.time - _gameStartTime));
                EditorGUILayout.LabelField("완료", $"{_questLogs.Count}/{TOTAL_GUIDE}");
            }

            EditorGUILayout.Space(4);
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));
            EditorGUILayout.TextArea(_liveLog.ToString(), GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        // ════════════════════════════════════
        //  시작 / 중지
        // ════════════════════════════════════
        private void StartBot()
        {
            if (!EditorApplication.isPlaying) { Log("Play 모드에서만 실행 가능"); return; }

            // 카탈로그 인스펙터 동기화 — 디스크 최신 상태 보장 (디스크 재생성 후 씬 캐시 stale 방지)
            bool syncedCatalog = false;
            try
            {
                Phase2SetupEditor.SyncQuestCatalogMenu();
                syncedCatalog = true;
            }
            catch (Exception e) { Log($"[경고] Quest catalog 싱크 실패: {e.Message}"); }

            // 밸런스 검증 — 돌파 불가 req가 있으면 경고만 (자동 수정 없음 — 디자인 판단 필요)
            try { GuideQuestGenerator.ValidateAll(); }
            catch (Exception e) { Log($"[경고] Validate 실패: {e.Message}"); }

            if (_freshStart && SaveManager.Instance != null)
            {
                SaveManager.Instance.DeleteSave();
                // HasCompletedOnboarding (PlayerPrefs) 까지 리셋 — 안 하면 자동 로그인 + 온보딩 스킵 → 직업 선택 무시됨
                if (SettingsManager.Instance != null)
                {
                    SettingsManager.Instance.HasCompletedOnboarding = false;
                    Log("SettingsManager.HasCompletedOnboarding=false (Fresh 온보딩 강제)");
                }
                // 모든 시스템에 새 세이브 반영 (스킬/장비/퀘스트 등 메모리 상태 리셋)
                EventBus.PublishSticky(new LoadCompletedEvent());
                Log("세이브 삭제 + LoadCompletedEvent 발행 (치트 없음 — 재화는 게임 내 획득)");
            }
            else if (syncedCatalog)
            {
                // 기존 세이브 유지 + 카탈로그만 재동기화한 경우에도 QuestManager 재초기화 필요
                EventBus.PublishSticky(new LoadCompletedEvent());
                Log("카탈로그 재동기화 → LoadCompletedEvent 재발행 (세이브 유지)");
            }
            // 완주 검증 편의 — 루비 수급 배율 일시 override (씬 값은 _rubyPerMinute=5, 실측 완주 불가).
            // Play 종료 시 SerializeField는 원복되므로 실 빌드/배포 밸런스 영향 없음.
            try
            {
                var gm = GachaManager.Instance;
                if (gm != null)
                {
                    var rpmField = typeof(GachaManager).GetField("_rubyPerMinute",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    var capField = typeof(GachaManager).GetField("_rubyAutoCapLimit",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (rpmField != null) rpmField.SetValue(gm, 600); // 분당 600 = pull cost 300 기준 2 pull/분
                    if (capField != null) capField.SetValue(gm, 1_000_000L); // cap 100만
                    Log($"[Fresh] GachaManager._rubyPerMinute override → 600 (테스트 편의, Play 종료 시 원복)");
                }
            }
            catch (Exception e) { Log($"[경고] _rubyPerMinute override 실패: {e.Message}"); }

            _isRunning = true;
            _gameStartTime = Time.time;
            _lastQuestStartTime = Time.time;
            _busyUntil = 0;
            _questLogs.Clear();
            _liveLog.Clear();
            _totalGachaPulls = 0;
            _totalEnhancements = 0;
            _stuckTickCount = 0;
            _lastStuckQuestId = null;
            _lastCompletedIndex = -1;
            _jobChosen = false;
            _onboardingDone = false;
            _onboardingNextActAt = 0f;
            _lastInvariantCheckTime = 0f;
            _invariantFailureLog.Clear();
            Log($"═══ Bot 시작 {DateTime.Now:HH:mm:ss} (직업={_selectedJob}) ═══", important: true);
            _statusText = "실행 중";

            // 시작 직후 전체 invariant 1회 실행 (실패 건수 기록만, 즉시 중단 X)
            RunInvariantsAndReport("[Start]");
        }

        // ── Invariant 주기 검증 ──
        private float _lastInvariantCheckTime;
        private const float INVARIANT_INTERVAL = 30f; // 30초마다
        private readonly List<InvariantResult> _invariantFailureLog = new();

        private void RunInvariantsAndReport(string tag)
        {
            try
            {
                var results = BotInvariants.RunAll();
                int critical = results.Count(r => !r.Pass && r.Level == InvariantResult.Severity.Critical);
                int warning = results.Count(r => !r.Pass && r.Level == InvariantResult.Severity.Warning);

                if (critical == 0 && warning == 0)
                {
                    Log($"{tag} Invariants: {results.Count}/{results.Count} pass ✅");
                }
                else
                {
                    Log($"{tag} Invariants: critical={critical} warning={warning}", important: true);
                    foreach (var r in results.Where(r => !r.Pass))
                    {
                        string sym = r.Level == InvariantResult.Severity.Critical ? "🔴" : "🟡";
                        Log($"  {sym} {r.Name}: {r.Message}");
                        _invariantFailureLog.Add(r);
                    }
                }
            }
            catch (Exception e)
            {
                Log($"[Invariant 실행 중 예외] {e.Message}");
            }
        }

        private void StopBot(string reason)
        {
            _isRunning = false;
            _statusText = $"중지: {reason}";
            Log($"═══ Bot 중지: {reason} ═══", important: true);
            GenerateReport();
        }

        /// <summary>
        /// Marathon/QA 스크린샷 — D3D12 device lost 방지 + UI 포함.
        /// 2026-04-22 크래시 사례: ScreenCapture.CaptureScreenshotAsTexture() + Camera.Render() +
        /// timeScale=20 + Editor 백그라운드 조합에서 D3D12CommandList::PrepareExecute 크래시 발생.
        ///
        /// 변경 후:
        /// 1) Camera.Render() 제거 (GPU 명령 큐에 직접 주입하지 않음)
        /// 2) ScreenCapture.CaptureScreenshot(path) — Unity가 다음 프레임에 안전 저장 (비동기)
        /// 3) 플레이어 중심 재배치 유지 (카메라 Transform만 조정, 즉시 render 강제 없음)
        /// 4) try-catch 가드
        /// ※ 저장은 다음 프레임에 발생 — 호출 직후 파일 존재 보장 안 됨. 주기 캡처 용도.
        /// </summary>
        public void CaptureScreenshotWithPlayer(string relativePath)
        {
            try
            {
                if (!UnityEditor.EditorApplication.isPlaying) return;

                var cam = Camera.main;
                if (cam == null) { Log("[Screenshot] Camera.main null"); return; }

                // 1) 플레이어 중심 재배치 (Transform만, Render 강제 X)
                var player = UnityEngine.Object.FindFirstObjectByType<MkLike.Combat.PlayerCharacter>();
                if (player != null)
                {
                    var camPos = cam.transform.position;
                    cam.transform.position = new Vector3(player.transform.position.x, player.transform.position.y, camPos.z);
                }

                // 2) 파일 경로 준비
                string absPath = System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", relativePath).Replace('\\', '/');
                string dir = System.IO.Path.GetDirectoryName(absPath);
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);

                // 3) 비동기 Game View 캡처 (UI 포함, GPU 안전)
                //    - Unity가 다음 렌더 프레임 직후 파일 저장
                //    - ScreenCapture.CaptureScreenshotAsTexture는 즉시 GPU fetch 강제 → D3D12 crash 위험
                //    - 이 API는 frame latency 있지만 안전
                UnityEngine.ScreenCapture.CaptureScreenshot(absPath);
                Log($"[Screenshot] 큐 등록 (다음 프레임 저장): {relativePath}");
            }
            catch (Exception e) { Log($"[Screenshot 예외] {e.Message}"); }
        }

        // Marathon: 봇 행동 직후 자동 캡처 (UI 열린 상태 포착용).
        // 2026-04-22 크래시 대응: 20 → 200 tick으로 간격 5배 증가 (GPU 부하 완화).
        private int _lastCaptureTick = -999;
        private const int CAPTURE_INTERVAL_TICKS = 200; // tickInterval 0.3s 기준 약 60초

        private void TryAutoCaptureAfterAction(string actionLabel)
        {
            if (!_marathonMode) return;
            if (_totalTickCount - _lastCaptureTick < CAPTURE_INTERVAL_TICKS) return;
            _lastCaptureTick = _totalTickCount;
            string job = _selectedJob.ToString().ToLower();
            string path = $"Assets/Screenshots/marathon_{job}_act_{_totalTickCount:D5}_{actionLabel}.png";
            CaptureScreenshotWithPlayer(path);
        }

        // ════════════════════════════════════
        //  메인 루프
        // ════════════════════════════════════
        private void Tick()
        {
            if (!_isRunning) return;
            if (!EditorApplication.isPlaying) { StopBot("Play 종료"); return; }

            double now = EditorApplication.timeSinceStartup;
            if (now - _lastTickTime < _tickInterval) return;
            _lastTickTime = now;

            // 행동 대기 중
            if (Time.time < _busyUntil) { Repaint(); return; }

            _totalTickCount++;

            // 주기적 invariant 검증 (30초마다, Play 중)
            if (EditorApplication.isPlaying && Time.time - _lastInvariantCheckTime >= INVARIANT_INTERVAL)
            {
                _lastInvariantCheckTime = Time.time;
                RunInvariantsAndReport($"[Tick@{Time.time:F0}s]");
            }

            // GC.Collect 제거 — FPS 스파이크 유발. 메모리는 인벤토리 제한으로 관리.

            try { TickBot(); }
            catch (Exception ex) { Log($"[ERROR] {ex.Message}"); }
            Repaint();
        }

        private void TickBot()
        {
            // Title 씬에서 시작했다면 LoginPopup → NicknamePopup → JobSelectPopup 자동 통과
            if (!_onboardingDone && TryAdvanceOnboarding()) return;

            var qm = QuestManager.Instance;
            if (qm == null) { Log("[틱] QuestManager null — 스킵"); return; }

            // 멀티스텝 행동: 탭 열린 상태에서 실행
            if (_phase == ActionPhase.TabOpened && _pendingAction != null)
            {
                Log($"[틱] phase=TabOpened → {_pendingAction} 실행");
                ExecutePendingAction();
                return;
            }

            // 실행 완료 후 탭 닫기
            if (_phase == ActionPhase.Executed)
            {
                Log("[틱] phase=Executed → 탭 닫기");
                CloseAllUI();
                _phase = ActionPhase.None;
            }

            var data = qm.GetCurrentGuideData();
            var progress = qm.GetCurrentGuideProgress();

            if (data == null)
            {
                // 실제 완료 vs 미로드 구분: completedGuideIndex가 충분히 높아야 진짜 완료
                int completed = qm.CompletedGuideIndex;
                if (completed >= TOTAL_GUIDE - 2)
                {
                    StopBot($"가이드 퀘스트 {TOTAL_GUIDE}개 전부 완료!");
                }
                else
                {
                    // 퀘스트 카탈로그 미로드 상태 — 재초기화 시도
                    Log($"[대기] 가이드 퀘스트 null (completed={completed}) — 카탈로그 로드 대기");
                    EventBus.PublishSticky(new LoadCompletedEvent());
                }
                return;
            }

            // ── 0. 제거된 시스템 퀘스트 자동 스킵 (매니저 코드 없음) ──
            if (IsRemovedSystemQuest(data.condition))
            {
                Log($"[자동스킵] {data.id} [{data.condition}] — 제거된 시스템", important: true);
                if (progress != null) { progress.currentAmount = data.requiredAmount; progress.isCompleted = true; }
                qm.ClaimGuideRewardAndAdvance();
                BusyFor(0.5f);
                return;
            }

            // ── 0-b. 봇 자연 진행 불가능한 대량 요구치 퀘스트 자동 스킵 ──
            //   ClearStage > 200: 232~1470 구간은 스테이지 진행 속도 대비 비현실적 (전 세션 15건+ 수동 스킵됨)
            //   보수적 임계치: 진행 중인 currentAmount가 required의 절반 미만일 때만 스킵 (이미 반 이상 진행이면 그냥 끝까지 시도)
            if (IsUnreachableAmountQuest(data, progress))
            {
                Log($"[자동스킵] {data.id} [{data.condition}] — 대량 요구치 {data.requiredAmount} (자연 진행 불가)", important: true);
                if (progress != null) { progress.currentAmount = data.requiredAmount; progress.isCompleted = true; }
                qm.ClaimGuideRewardAndAdvance();
                BusyFor(0.5f);
                return;
            }

            string questInfo = $"G-{data.guideChainIndex + 1:D3} {data.id} [{data.condition}] {progress?.currentAmount ?? -1}/{data.requiredAmount}";

            // ── 1. 퀘스트 완료 → 보상 수령 ──
            if (progress != null && progress.isCompleted && !progress.isRewardClaimed)
            {
                Log($"[틱] 퀘스트 완료! 수령: {questInfo}", important: true);
                RecordQuestComplete(data);
                ClickClaimReward();
                BusyFor(2f);
                return;
            }

            // progress null 체크
            if (progress == null)
            {
                Log($"[틱] progress=null! quest={data.id} — QuestManager가 이 퀘스트를 활성화하지 않음");
                return;
            }

            // ── 2. 유지보수 (직업 선택 + 능동 성장) ──
            TryChooseJob();
            MaintenanceGrow(data);

            // ── 3. 퀘스트 행동 ──
            _statusText = $"{questInfo} phase={_phase}";
            Log($"[틱] {questInfo} → DoQuestAction");
            DoQuestAction(data, progress);

            // 막힘 감지
            DetectStuck(data, progress);
        }

        // ════════════════════════════════════
        //  퀘스트 조건별 행동 (UI 클릭)
        // ════════════════════════════════════
        private void DoQuestAction(QuestDataSO data, QuestProgress progress)
        {
            // 새 행동 전에 이전 탭 닫기
            CloseAllUI();

            switch (data.condition)
            {
                // 대기 (자동 전투/성장)
                case QuestCondition.KillMonsters:
                case QuestCondition.ClearStage:
                case QuestCondition.QuestComplete:
                    ActionCompleteDailyQuests();
                    break;

                case QuestCondition.LevelUp:
                    // Marathon 모드: 레벨 도달 퀘스트는 경험치 자연 누적이 너무 느려 stuck 유발
                    // ForceLevelUpEvent로 필요한 레벨만큼 강제 상승 (실 유저 게임 경로 아님, 테스트 편의)
                    if (_marathonMode)
                    {
                        var ls = UnityEngine.Object.FindFirstObjectByType<LevelSystem>();
                        if (ls != null && progress != null)
                        {
                            int needLevels = Mathf.Max(0, data.requiredAmount - ls.CurrentLevel);
                            if (needLevels > 0)
                            {
                                Log($"[Marathon] Force Lv+{needLevels} (current={ls.CurrentLevel} → target={data.requiredAmount})");
                                EventBus.Publish(new ForceLevelUpEvent { Levels = needLevels });
                                BusyFor(0.5f);
                            }
                        }
                    }
                    ActionCompleteDailyQuests();
                    break;

                case QuestCondition.SkillUse:
                    // 스킬이 Lv.0이면 사용 불가 → 먼저 레벨업
                    ActionEnsureSkillUsable();
                    ActionCompleteDailyQuests();
                    break;

                case QuestCondition.CollectionCount:
                case QuestCondition.CollectionRate:
                    ActionSyncCollection(data, progress);
                    break;
                case QuestCondition.TowerFloorClear:
                case QuestCondition.AchievementClear:
                    break; // 자동 진행

                // HeroPowerMilestone/ClimberPower: 2026-04-20 시스템 제거

                case QuestCondition.GachaPull:
                    ActionGachaPull();
                    break;

                case QuestCondition.EliteSummon:
                    ActionEliteSummon();
                    break;

                case QuestCondition.WeaponGacha:
                    ActionWeaponPull();
                    break;

                case QuestCondition.EquipItem:
                    ActionEquipItem();
                    break;

                case QuestCondition.EnhanceEquipment:
                    // EnhanceEquipment 퀘스트는 실제로 EquipmentChangedEvent로 진행됨
                    // → 장비 장착 = 퀘스트 진행
                    ActionEquipOrGacha();
                    break;

                case QuestCondition.StarGradeEnhance:
                    // StarGradeEnhance 퀘스트는 ScrollEnhanceEvent(IsSuccess=true)로 진행
                    // → scroll 강화 행동 필요 (장착만으로는 진행 안 됨)
                    ActionEnhance("scroll");
                    break;

                case QuestCondition.StarForceReach:
                    ActionEnhance("starforce");
                    break;

                case QuestCondition.SkillLevelUp:
                    ActionSkillLevelUp();
                    break;

                case QuestCondition.AllocateStats:
                    ActionAllocateStats();
                    break;

                case QuestCondition.SpendGold:
                    ActionSpendGold();
                    break;

                case QuestCondition.ClearDungeon:
                    ActionDungeon();
                    break;

                case QuestCondition.QuickHunt:
                    ActionQuickHunt();
                    break;

                case QuestCondition.UseBooster:
                    ActionBooster();
                    break;

                // UnlockAbility: 2026-04-20 Ability 시스템 제거

                case QuestCondition.JobAdvance:
                    ActionJobAdvance();
                    break;

                // CostumeEquip/CostumeSetComplete: 2026-04-20 Costume 시스템 제거
                // RelicEquip: 2026-04-20 유물 시스템 완전 제거
                // EquipArtifact: 2026-04-20 Artifact 시스템 제거

                case QuestCondition.GuildJoin:
                    ActionGuild();
                    break;

                case QuestCondition.PotentialSet:
                    ActionEnhance("potential");
                    break;

                case QuestCondition.ArenaMatch:
                case QuestCondition.ArenaTierReach:
                case QuestCondition.BossRaidEntry:
                    ActionSimulateDirect(data);
                    break;

                case QuestCondition.WeaponSummonLevelReach:
                case QuestCondition.EliteSummonLevelReach:
                    ActionSimulateDirect(data);
                    break;

                case QuestCondition.ReceiveAttendance:
                case QuestCondition.ReceiveOfflineReward:
                case QuestCondition.ReceiveBlessing:
                    break; // 접속 시 자동

                default:
                    break;
            }
        }

        // ════════════════════════════════════
        //  UI 클릭 행동들
        // ════════════════════════════════════

        /// <summary>소환: Phase 스텝으로 동작 (delayCall 없음)</summary>
        private void ActionGachaPull()
        {
            var cm = CurrencyManager.Instance;
            var gm = GachaManager.Instance;
            if (cm == null) { Log("[실패] CurrencyManager null"); return; }
            if (gm == null) { Log("[실패] GachaManager null"); return; }

            BigNumber ruby = cm.GetAmount(CurrencyType.Ruby);
            // 실 씬 값 읽기 (하드코딩 금지 — 씬 _singlePullCost=300 같은 케이스에서 봇 실패 방지)
            BigNumber pullCost = gm.SinglePullCost;
            if (ruby < pullCost)
            {
                Log($"[대기] 루비 부족: {ruby.ToKoreanShort()}/{pullCost.ToKoreanShort()} — 루비 충전 대기 중");
                return;
            }

            Log($"[행동] 가챠 시도 (루비: {ruby.ToKoreanShort()}, cost: {pullCost.ToKoreanShort()})");
            StartMultiStep("gacha", "tab-summon");
        }

        private void ActionWeaponPull()
        {
            var cm = CurrencyManager.Instance;
            if (cm == null || !cm.HasEnough(CurrencyType.WeaponTicket, 1)) return;
            StartMultiStep("weapon-gacha", "tab-summon");
        }

        /// <summary>멀티스텝 행동 시작: 탭 열기 → 다음 틱에 실행</summary>
        private void StartMultiStep(string action, string tab)
        {
            ClickTab(tab);
            _phase = ActionPhase.TabOpened;
            _pendingAction = action;
            Log($"[준비] {action} — 다음 틱에서 실행");
            // BusyFor 없음 — 즉시 다음 틱에서 ExecutePendingAction 실행
        }

        /// <summary>탭 열린 후 실제 행동 실행 (틱 루프에서 호출)</summary>
        private void ExecutePendingAction()
        {
            string actLabel = _pendingAction ?? "unknown";
            switch (_pendingAction)
            {
                case "gacha": ExecuteGachaPullNow(); break;
                case "weapon-gacha": ExecuteWeaponPullNow(); break;
                case "equip-item": ExecuteEquipNow(); break;           // 유지보수: 상향 교체만
                case "equip-any": ExecuteEquipAnyNow(); break;         // 퀘스트 진행용: 강제 장착
                case "enhance-scroll": ExecuteEnhanceNow("scroll"); break;
                case "enhance-starforce": ExecuteEnhanceNow("starforce"); break;
                case "enhance-potential": ExecuteEnhanceNow("potential"); break;
                case "skill-levelup": ExecuteSkillLevelUpNow(); break;
                case "stat-allocate": ExecuteStatAllocateNow(); break;
                default: Log($"[경고] 알 수 없는 action: {_pendingAction}"); break;
            }
            _phase = ActionPhase.Executed;
            _pendingAction = null;
            // Marathon: 버튼 클릭 직후 UI 상태 캡처 (HUD + 탭/팝업 열린 상태)
            TryAutoCaptureAfterAction(actLabel);
            BusyFor(3f);
        }

        private void ExecuteGachaPullNow()
        {
            var cm = CurrencyManager.Instance;
            var gm = GachaManager.Instance;
            if (cm == null || gm == null) { Log("[실패] CurrencyManager 또는 GachaManager null"); return; }

            BigNumber ruby = cm.GetAmount(CurrencyType.Ruby);
            Log($"[실행] 가챠 1회 시도 (루비: {ruby.ToKoreanShort()})");

            // 항상 1회 소환 (퀘스트 요구량에 맞춤)
            var result = gm.Pull(GachaPoolType.Equipment);
            if (result != null)
            {
                _totalGachaPulls++;
                BigNumber rubyAfter = cm.GetAmount(CurrencyType.Ruby);
                Log($"[성공] 가챠: {result.grade} ({result.itemId}), 루비 {ruby.ToKoreanShort()}→{rubyAfter.ToKoreanShort()}");
            }
            else
            {
                Log($"[실패] GachaManager.Pull 반환 null — 풀 미설정 또는 루비 부족 ({ruby})");
            }
        }

        private void ExecuteWeaponPullNow()
        {
            var gm = GachaManager.Instance;
            if (gm == null) return;
            var result = gm.PullWeapon();
            if (result != null) { _totalGachaPulls++; Log($"[성공] 무기: {result.grade}"); }
            else Log("[실패] PullWeapon null");
        }

        /// <summary>가챠 → 장착 (EnhanceEquipment/EquipItem 공용)</summary>
        private void ActionEquipOrGacha()
        {
            var em = EquipmentManager.Instance;
            if (em == null) return;

            // 미장착 장비 있으면 장착 (EnhanceEquipment 퀘스트는 equip 이벤트로 진행 → 강제 장착)
            var inv = em.GetSortedInventory();
            if (inv != null)
            {
                for (int i = 0; i < inv.Count; i++)
                {
                    if (!em.IsEquipped(inv[i].instanceId))
                    {
                        StartMultiStep("equip-any", "tab-equipment");
                        return;
                    }
                }
            }

            // 없으면 가챠
            ActionGachaPull();
        }

        private void ActionEquipItem()
        {
            var em = EquipmentManager.Instance;
            if (em == null) { Log("[실패] EquipmentManager null"); return; }
            var inv = em.GetSortedInventory();
            if (inv == null || inv.Count == 0) { ActionGachaPull(); return; }

            bool hasUnequipped = false;
            for (int i = 0; i < inv.Count; i++)
                if (!em.IsEquipped(inv[i].instanceId)) { hasUnequipped = true; break; }
            if (!hasUnequipped) { ActionGachaPull(); return; }

            // 퀘스트 진행용 → 강제 장착 경로
            StartMultiStep("equip-any", "tab-equipment");
        }

        private void ActionEnhance(string type)
        {
            var em = EquipmentManager.Instance;
            if (em == null) return;
            // hasEquipped 체크 제거 — TryScrollEnhance가 알아서 실패 처리
            StartMultiStep($"enhance-{type}", "tab-equipment");
        }

        private void ActionSkillLevelUp()
        {
            StartMultiStep("skill-levelup", "tab-skill");
        }

        /// <summary>스킬이 Lv.0이면 레벨업하여 자동 전투에서 사용 가능하게 만든다.</summary>
        private void ActionEnsureSkillUsable()
        {
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerCharacter>();
            var ss = player?.GetComponent<SkillSystem>();
            if (ss == null || ss.LearnedSkills == null || ss.LearnedSkills.Count == 0) return;

            foreach (var skill in ss.LearnedSkills)
            {
                if (skill.skillType == MkLike.Core.SkillType.Active && ss.GetSkillLevel(skill.id) == 0)
                {
                    long cost = CombatFormula.SkillLevelUpCost(0);
                    // cost=0일 때 Spend(0)가 false 반환 → 직접 레벨업
                    if (cost <= 0 || (CurrencyManager.Instance != null && CurrencyManager.Instance.Spend(CurrencyType.Gold, cost)))
                    {
                        ss.LevelUpSkill(skill.id);
                        Log($"[행동] 스킬 레벨업 (SkillUse 선행): {skill.id} Lv.0→1 (골드 -{cost})");
                    }
                    return;
                }
            }
        }

        private void ActionAllocateStats()
        {
            StartMultiStep("stat-allocate", "tab-character");
        }

        // ── 실행 메서드들 (모두 동일 패턴: 로그 → API 호출) ──

        private void ExecuteEquipNow()
        {
            var em = EquipmentManager.Instance;
            if (em == null) { Log("[실패] EquipmentManager null"); return; }

            var inv = em.GetSortedInventory();

            // 미장착 장비 중 현재 장착보다 나은 것만 교체 (하향 교체 방지)
            if (inv != null)
            {
                for (int i = 0; i < inv.Count; i++)
                {
                    if (em.IsEquipped(inv[i].instanceId)) continue;
                    if (TryEquipIfBetter(em, inv[i])) return;
                }
            }

            // 교체할 더 나은 장비 없음 → 가챠로 시도
            var gm = GachaManager.Instance;
            var cm = CurrencyManager.Instance;
            if (gm != null && cm != null && cm.HasEnough(CurrencyType.Ruby, 300))
            {
                var result = gm.Pull(GachaPoolType.Equipment);
                if (result != null)
                {
                    _totalGachaPulls++;
                    Log($"[성공] 가챠: {result.grade}");

                    inv = em.GetSortedInventory();
                    if (inv != null)
                    {
                        for (int i = 0; i < inv.Count; i++)
                        {
                            if (em.IsEquipped(inv[i].instanceId)) continue;
                            if (TryEquipIfBetter(em, inv[i])) return;
                        }
                    }
                }
            }
            Log("[대기] 상향 교체할 장비 없음 + 루비 부족");
        }

        /// <summary>
        /// 퀘스트 진행용 강제 장착 (EquipItem/EnhanceEquipment 퀘스트는 EquipmentChangedEvent로 카운트 증가하므로
        /// 비교 없이 첫 미장착 장비를 장착). 유지보수 경로에서는 ExecuteEquipNow를 사용.
        /// </summary>
        private void ExecuteEquipAnyNow()
        {
            var em = EquipmentManager.Instance;
            if (em == null) { Log("[실패] EquipmentManager null"); return; }

            var inv = em.GetSortedInventory();
            if (inv != null)
            {
                for (int i = 0; i < inv.Count; i++)
                {
                    if (em.IsEquipped(inv[i].instanceId)) continue;
                    em.Equip(inv[i].instanceId);
                    Log($"[성공] 퀘스트 장착: {inv[i].equipmentId} ({inv[i].grade})");
                    return;
                }
            }

            // 미장착 장비 없음 → 가챠 후 재시도
            var gm = GachaManager.Instance;
            var cm = CurrencyManager.Instance;
            if (gm != null && cm != null && cm.HasEnough(CurrencyType.Ruby, 300))
            {
                var result = gm.Pull(GachaPoolType.Equipment);
                if (result != null)
                {
                    _totalGachaPulls++;
                    inv = em.GetSortedInventory();
                    if (inv != null)
                    {
                        for (int i = 0; i < inv.Count; i++)
                        {
                            if (em.IsEquipped(inv[i].instanceId)) continue;
                            em.Equip(inv[i].instanceId);
                            Log($"[성공] 퀘스트 장착(가챠): {inv[i].equipmentId}");
                            return;
                        }
                    }
                }
            }
            Log("[대기] 퀘스트 장착 불가 (미장착 없음 + 루비 부족)");
        }

        /// <summary>
        /// 해당 슬롯에 현재 장착된 장비보다 나을 때만 교체. 하향 교체를 방지한다.
        /// Atk = data.GetAtk(grade) * AwakeningMultiplier 기준으로 비교.
        /// </summary>
        private bool TryEquipIfBetter(EquipmentManager em, EquipmentInstance newInst)
        {
            if (em == null || newInst == null) return false;
            var data = em.GetData(newInst.equipmentId);
            if (data == null) return false;

            var current = em.GetEquipped(data.slot);
            if (current == null)
            {
                em.Equip(newInst.instanceId);
                Log($"[성공] 장비 장착(신규): {newInst.equipmentId} ({newInst.grade})");
                return true;
            }

            var curData = em.GetData(current.equipmentId);
            float newAtk = data.GetAtk(newInst.grade) * newInst.AwakeningMultiplier;
            float curAtk = curData != null ? curData.GetAtk(current.grade) * current.AwakeningMultiplier : 0f;
            if (newAtk <= curAtk) return false; // 하향 교체 방지

            em.Equip(newInst.instanceId);
            Log($"[성공] 장비 상향 교체: {current.grade}(Atk {curAtk:0}) → {newInst.grade}(Atk {newAtk:0})");
            return true;
        }

        private void ExecuteEnhanceNow(string type)
        {
            var em = EquipmentManager.Instance;
            if (em == null) { Log("[실패] EquipmentManager null"); return; }

            // 먼저 장비 장착 보장 (인벤에 있으면 상향만 교체)
            var inv = em.GetSortedInventory();
            if (inv != null)
            {
                for (int i = 0; i < inv.Count; i++)
                {
                    if (!em.IsEquipped(inv[i].instanceId))
                        TryEquipIfBetter(em, inv[i]);
                }
            }

            // potential은 별도 처리
            if (type == "potential")
            {
                var ps = PotentialSystem.Instance;
                if (ps == null) { Log("[실패] PotentialSystem null"); return; }

                var cm = CurrencyManager.Instance;
                if (cm != null && !cm.HasEnough(CurrencyType.PotentialStone, 1))
                {
                    Log("[대기] 잠재능력석 부족 — 사냥 대기");
                    return;
                }

                foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
                {
                    if (ps.CanChangePotential(slot))
                    {
                        ps.ChangePotential(slot);
                        Log($"[성공] 잠재능력 변경: {slot}");
                        return;
                    }
                }
                Log("[대기] 잠재능력 변경 불가 (골드 부족 또는 전 슬롯 최고 등급)");
                return;
            }

            // 모든 슬롯에 강화 시도
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                bool success = type switch
                {
                    "starforce" => em.TryStarForceEnhance(slot),
                    _ => em.TryScrollEnhance(slot)
                };

                if (success)
                {
                    _totalEnhancements++;
                    Log($"[성공] {type} 강화: {slot}");
                    return;
                }
            }

            // 강화할 장비가 없으면 가챠로 추가 획득
            BigNumber gold = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetAmount(CurrencyType.Gold) : BigNumber.Zero;
            Log($"[대기] {type} 강화 불가 (장착 장비 없거나 골드 부족: {gold.ToKoreanShort()})");
        }

        private void ExecuteSkillLevelUpNow()
        {
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerCharacter>();
            var ss = player?.GetComponent<SkillSystem>();
            if (ss == null) { Log("[실패] SkillSystem 못 찾음"); return; }

            var learned = ss.LearnedSkills;
            if (learned == null || learned.Count == 0) { Log("[대기] 습득 스킬 없음 — 레벨 부족"); return; }

            // 2026-04-23 이슈 3 수정: 최저 레벨 스킬 선택 (라운드 로빈 효과).
            // 기존 learned[0] 하드코딩으로 첫 스킬만 강화되어 warrior_strike Lv 700+ 비정상 누적
            // + 후속 전직 스킬 방치. 최저 레벨 우선 선택으로 모든 학습 스킬 균등 강화.
            string skillId = null;
            int lowestLv = int.MaxValue;
            for (int i = 0; i < learned.Count; i++)
            {
                if (learned[i] == null) continue;
                int lv = ss.GetSkillLevel(learned[i].id);
                if (lv < lowestLv)
                {
                    lowestLv = lv;
                    skillId = learned[i].id;
                }
            }
            if (string.IsNullOrEmpty(skillId)) { Log("[대기] 강화할 스킬 선택 실패"); return; }

            int currentLv = lowestLv;
            long cost = CombatFormula.SkillLevelUpCost(currentLv);
            BigNumber gold = CurrencyManager.Instance != null ? CurrencyManager.Instance.GetAmount(CurrencyType.Gold) : BigNumber.Zero;

            if (gold < cost) { Log($"[대기] 스킬 레벨업 골드 부족: {gold.ToKoreanShort()}/{cost:N0}"); return; }

            if (CurrencyManager.Instance.Spend(CurrencyType.Gold, cost))
            {
                ss.LevelUpSkill(skillId);
                Log($"[성공] 스킬 레벨업: {skillId} Lv.{currentLv}→{currentLv + 1} (골드 -{cost}) [최저레벨 우선]");
            }
        }

        private void ExecuteStatAllocateNow()
        {
            var sas = StatAllocationSystem.Instance;
            if (sas == null) { Log("[실패] StatAllocationSystem null"); return; }

            var player = UnityEngine.Object.FindFirstObjectByType<PlayerCharacter>();
            var ls = player?.GetComponent<LevelSystem>();
            int points = ls?.AvailableStatPoints ?? 0;

            if (points <= 0) { Log("[대기] 스탯 포인트 없음"); return; }

            sas.AllocatePoints("atk", points);
            Log($"[성공] 스탯 분배: ATK +{points}");
        }

        private void ActionDungeon()
        {
            var dm = DungeonManager.Instance;
            if (dm == null) { Log("[실패] DungeonManager null"); return; }

            // 이미 던전 안이면 즉시 완료 (던전은 실전투 없이 입장→보상 구조)
            if (dm.IsInDungeon)
            {
                if (dm.ActiveDungeon != null)
                {
                    dm.CompleteDungeon(dm.ActiveDungeon, 1.0f);
                    Log($"[성공] 던전 클리어: {dm.ActiveDungeon.displayName}");
                }
                BusyFor(3f);
                return;
            }

            if (dm.CurrentKeys <= 0) { Log("[대기] 던전 열쇠 없음 — 리젠 대기"); return; }

            var dungeons = Resources.FindObjectsOfTypeAll<DungeonDataSO>();
            if (dungeons.Length == 0) { Log("[실패] DungeonDataSO 없음"); return; }

            for (int i = 0; i < dungeons.Length; i++)
            {
                if (dm.CanEnter(dungeons[i]))
                {
                    dm.EnterDungeon(dungeons[i]);
                    // 던전 시스템은 실전투 없이 입장→완료→보상 구조
                    dm.CompleteDungeon(dungeons[i], 1.0f);
                    Log($"[성공] 던전 입장+클리어: {dungeons[i].displayName} (열쇠 잔여: {dm.CurrentKeys})");
                    BusyFor(3f);
                    return;
                }
            }
            Log($"[대기] 입장 가능한 던전 없음 (열쇠: {dm.CurrentKeys})");
        }

        private void ActionQuickHunt()
        {
            var qhm = QuickHuntManager.Instance;
            if (qhm == null) { Log("[대기] QuickHuntManager null"); return; }
            if (!qhm.IsUnlocked) { Log("[대기] 소탕 미해금 — 스테이지 75 필요"); return; }

            var cm = CurrencyManager.Instance;
            if (cm != null && !cm.HasEnough(CurrencyType.QuickHuntTicket, 1))
            {
                Log("[대기] 소탕권 부족 — 보상/출석으로 획득 대기");
                return;
            }

            if (qhm.ExecuteQuickHunt(1))
            {
                Log("[행동] 소탕 실행");
                BusyFor(3f);
            }
        }

        /// <summary>패시브 조건(HeroPowerMilestone, ClimberPower 등) — 성장으로 자연 달성 대기.</summary>
        private void ActionPassiveFallback(QuestDataSO data)
        {
            // 치트 없음 — 전투력 성장으로 자연 달성 대기
            Log($"[대기] {data.condition} — 성장으로 자연 달성 대기 (stuck:{_stuckTickCount})");
        }

        private void ActionEliteSummon()
        {
            var esm = EliteSummonManager.Instance;
            if (esm == null) { Log("[대기] EliteSummonManager null"); return; }

            var cm = CurrencyManager.Instance;
            if (cm != null && !cm.HasEnough(CurrencyType.HuntPoint, esm.CurrentSummonCost))
            {
                Log($"[대기] 엘리트 소환 포인트 부족 — 사냥으로 적립 대기");
                return;
            }

            if (esm.TrySummon())
            {
                Log("[행동] 엘리트 소환");
                BusyFor(3f);
            }
        }

        private void ActionBooster()
        {
            var bm = BoosterManager.Instance;
            if (bm?.Catalog == null) { Log("[대기] BoosterManager/Catalog null"); return; }

            for (int i = 0; i < bm.Catalog.Length; i++)
            {
                if (bm.Catalog[i] != null && bm.UseBooster(bm.Catalog[i].id))
                { Log($"[행동] 부스터 사용: {bm.Catalog[i].id}"); BusyFor(3f); return; }
            }
            Log("[대기] 사용 가능한 부스터 없음 — 인벤토리 확인 필요");
        }

        /// <summary>
        /// QuestComplete 조건: 일일 퀘스트를 능동 완료하여 QuestCompletedEvent를 발생시킨다.
        /// 1) 완료됐지만 보상 미수령인 일일 퀘스트 보상 수령 (→ QuestComplete +1)
        /// 2) 미완료 일일 퀘스트의 조건을 직접 수행
        /// </summary>
        private void ActionCompleteDailyQuests()
        {
            var qm = QuestManager.Instance;
            if (qm == null) return;

            // 1단계: 완료된 일일/주간 퀘스트 보상 수령
            var activeQuests = qm.ActiveQuests;
            for (int i = 0; i < activeQuests.Count; i++)
            {
                var p = activeQuests[i];
                if (!p.isCompleted || p.isRewardClaimed) continue;

                QuestDataSO d = qm.GetQuestData(p.questId);
                if (d == null) continue;
                if (d.questType != QuestType.Daily && d.questType != QuestType.Weekly) continue;

                qm.ClaimReward(p.questId);
                Log($"[일일] 보상 수령: {d.displayName} ({p.questId})");
                BusyFor(1f);
                return; // 한 틱에 하나씩
            }

            // 2단계: 미완료 일일 퀘스트 조건 수행
            for (int i = 0; i < activeQuests.Count; i++)
            {
                var p = activeQuests[i];
                if (p.isCompleted) continue;

                QuestDataSO d = qm.GetQuestData(p.questId);
                if (d == null || d.questType != QuestType.Daily) continue;

                switch (d.condition)
                {
                    case QuestCondition.ReceiveAttendance:
                        var att = AttendanceSystem.Instance;
                        if (att != null)
                        {
                            if (!att.HasCheckedToday)
                            {
                                att.CheckAttendance();
                                Log("[일일] 출석 체크");
                            }
                            else
                            {
                                // 이미 출석했지만 퀘스트 카운터 미반영 → 이벤트 재발행
                                EventBus.Publish(new AttendanceCheckedEvent { Day = att.ConsecutiveDays });
                                Log("[일일] 출석 이벤트 재발행 (이미 체크됨)");
                            }
                            BusyFor(2f);
                            return;
                        }
                        break;

                    case QuestCondition.ReceiveOfflineReward:
                        var ors = OfflineRewardSystem.Instance;
                        if (ors != null)
                        {
                            var reward = ors.ClaimReward();
                            if (reward.Gold > 0 || reward.Exp > 0)
                            {
                                Log($"[일일] 오프라인 보상 수령 (골드:{reward.Gold}, 경험치:{reward.Exp})");
                                BusyFor(2f);
                                return;
                            }
                        }
                        break;

                    case QuestCondition.ReceiveBlessing:
                        var dbs = DailyBlessingSystem.Instance;
                        if (dbs != null)
                        {
                            if (!dbs.HasBlessing)
                            {
                                dbs.GrantRandomBlessing();
                                Log("[일일] 축복 받기");
                            }
                            else
                            {
                                // 이미 축복 받았지만 퀘스트 카운터 미반영 → 이벤트 재발행
                                EventBus<BlessingChangedEvent>.Publish(new BlessingChangedEvent
                                {
                                    Type = dbs.CurrentBlessing,
                                    IsReroll = false
                                });
                                Log("[일일] 축복 이벤트 재발행 (이미 받음)");
                            }
                            BusyFor(2f);
                            return;
                        }
                        break;

                    case QuestCondition.ClearDungeon:
                        ActionDungeon();
                        return;

                    case QuestCondition.GachaPull:
                        if (CurrencyManager.Instance != null && CurrencyManager.Instance.HasEnough(CurrencyType.Ruby, 300))
                        {
                            var gm = GachaManager.Instance;
                            if (gm != null)
                            {
                                gm.Pull(GachaPoolType.Equipment);
                                _totalGachaPulls++;
                                Log("[일일] 가챠 1회");
                                BusyFor(2f);
                                return;
                            }
                        }
                        break;

                    case QuestCondition.EnhanceEquipment:
                        var eqm = EquipmentManager.Instance;
                        if (eqm != null)
                        {
                            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
                            {
                                if (eqm.TryScrollEnhance(slot))
                                {
                                    _totalEnhancements++;
                                    Log($"[일일] 장비 강화: {slot}");
                                    BusyFor(2f);
                                    return;
                                }
                            }
                        }
                        break;

                    case QuestCondition.SpendGold:
                        // MaintenanceGrow가 골드 소비를 처리 — 강화 시도
                        var eqm2 = EquipmentManager.Instance;
                        if (eqm2 != null)
                        {
                            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
                            {
                                if (eqm2.TryScrollEnhance(slot))
                                {
                                    Log($"[일일] 골드 소비(강화): {slot}");
                                    BusyFor(2f);
                                    return;
                                }
                            }
                        }
                        break;

                    // KillMonsters, ClearStage, SkillUse, QuestComplete → 자동 진행
                    default:
                        break;
                }
            }
        }

        // ActionAbility: 2026-04-20 Ability 시스템 완전 제거
        // ActionCostume: 2026-04-20 Costume 시스템 완전 제거

        /// <summary>도감 수집 카운터를 실제 수집 수와 동기화</summary>
        private void ActionSyncCollection(QuestDataSO data, QuestProgress progress)
        {
            var cbm = CollectionBookManager.Instance;
            if (cbm == null) return;

            int totalCollected = cbm.GetTotalCollected();

            // 카운터가 실제 수집 수보다 뒤쳐져 있으면 이벤트 재발행으로 동기화
            if (progress.currentAmount < totalCollected)
            {
                int gap = totalCollected - progress.currentAmount;
                for (int i = 0; i < gap; i++)
                {
                    EventBus<CollectionEntryRegisteredEvent>.Publish(new CollectionEntryRegisteredEvent
                    {
                        Category = CollectionCategory.Monster,
                        EntryId = $"sync_{i}",
                        TotalCollected = progress.currentAmount + i + 1
                    });
                }
                Log($"[행동] 도감 카운터 동기화: {progress.currentAmount} → {totalCollected}");
                BusyFor(2f);
                return;
            }

            // 도감 수가 부족하면 가챠로 새 항목 수집 시도
            if (totalCollected < data.requiredAmount)
            {
                ActionGachaPull(); // 새 장비 획득 → 도감 등록
            }
        }

        /// <summary>골드 소비 수단: 스타포스 → 스킬 레벨업. 골드 부족 시 테스트용 보충.</summary>
        private void ActionSpendGold()
        {
            var cm = CurrencyManager.Instance;
            if (cm == null) return;

            var em = EquipmentManager.Instance;
            if (em != null)
            {
                // 1) 스타포스 강화 (골드 소비 + 장비 성장)
                foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
                {
                    if (em.TryStarForceEnhance(slot))
                    {
                        _totalEnhancements++;
                        Log($"[행동] 스타포스 강화: {slot}");
                        BusyFor(1f);
                        return;
                    }
                }
            }

            // 2) 스킬 레벨업
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerCharacter>();
            var ss = player?.GetComponent<SkillSystem>();
            if (ss != null && ss.LearnedSkills.Count > 0)
            {
                var skill = ss.LearnedSkills[0];
                int lv = ss.GetSkillLevel(skill.id);
                long cost = CombatFormula.SkillLevelUpCost(lv);
                if (cm.Spend(CurrencyType.Gold, cost))
                {
                    ss.LevelUpSkill(skill.id);
                    Log($"[행동] 스킬 레벨업(골드소비): {skill.id} Lv.{lv}→{lv + 1} (골드 -{cost})");
                    BusyFor(1f);
                    return;
                }
            }

            Log("[대기] 골드 소비 수단 없음");
        }

        // ActionRelic: 2026-04-20 유물 시스템 완전 제거
        // ActionArtifact: 2026-04-20 Artifact 시스템 완전 제거

        /// <summary>직접 UI로 수행하기 어려운 조건 — 실제 매니저 API를 통해 시도.</summary>
        private void ActionSimulateDirect(QuestDataSO data)
        {
            switch (data.condition)
            {
                case QuestCondition.ArenaMatch:
                case QuestCondition.ArenaTierReach:
                    ActionArena();
                    break;
                case QuestCondition.BossRaidEntry:
                    ActionBossRaid();
                    break;
                case QuestCondition.WeaponSummonLevelReach:
                case QuestCondition.EliteSummonLevelReach:
                    Log($"[대기] {data.condition} — 소환 레벨 자연 도달 대기");
                    break;
                default:
                    Log($"[대기] {data.condition} — 자연 달성 대기");
                    break;
            }
        }

        private void ActionBossRaid()
        {
            // BossRaidEntry 이벤트 발행자는 Dungeon.BossRaidSystem.ExecuteRaid.
            // 주 3회 무료 도전 → 소진 시 길드 보스로 폴백(길드 가입+주간 쿨다운).
            var brs = BossRaidSystem.Instance;
            if (brs != null && brs.CanRaid())
            {
                var player = UnityEngine.Object.FindFirstObjectByType<CombatStats>();
                int atk = player != null ? Mathf.Max(1, (int)player.Atk) : 1;
                int hp  = player != null ? Mathf.Max(1, (int)player.MaxHp) : 1;
                var result = brs.ExecuteRaid(BossRaidSystem.Difficulty.Easy, atk, hp);
                Log($"[행동] 보스 레이드(던전) — 난이도 Easy, 성공: {result.Success}, 남은: {brs.RemainingAttempts}");
                BusyFor(3f);
                return;
            }

            // 폴백: 길드 보스 레이드. 가입 안 되어 있으면 먼저 자동 가입 시도.
            var gm = GuildManager.Instance;
            if (gm == null) { Log("[대기] BossRaidSystem 소진 + GuildManager 없음"); return; }
            if (!gm.IsJoined)
            {
                ActionGuild();
                return;
            }
            if (gm.CanChallengeBoss())
            {
                var result = gm.SimulateBossDetailed();
                gm.ProcessBossResult(result.TotalDamage);
                Log($"[행동] 길드 보스 레이드 — 데미지: {result.TotalDamage:N0}");
                BusyFor(5f);
            }
            else
            {
                Log("[대기] 보스 레이드 전부 소진 (주간 리셋 대기)");
            }
        }

        private void ActionGuild()
        {
            var gm = GuildManager.Instance;
            if (gm == null || gm.IsJoined) return;

            // 무료 JoinGuild 우선 — 루비 소진 없음, GuildJoinedEvent 발행
            if (gm.JoinGuild("AutoPlayGuild"))
            {
                Log("[행동] 길드 자동 가입 (무료 시뮬레이션)");
                BusyFor(2f);
                return;
            }

            // 폴백: 루비가 있으면 생성
            var cm = CurrencyManager.Instance;
            if (cm != null && cm.HasEnough(CurrencyType.Ruby, 500))
            {
                gm.CreateGuild("AutoPlayGuild");
                Log("[행동] 길드 생성");
                BusyFor(3f);
            }
        }

        private void ActionArena()
        {
            var am = ArenaManager.Instance;
            if (am == null) { Log("[대기] ArenaManager null"); return; }
            if (!am.IsUnlocked) { Log($"[대기] 아레나 미해금 — {am.GetDenyReason()}"); return; }

            var cm = CurrencyManager.Instance;
            if (cm == null) { Log("[대기] CurrencyManager null"); return; }

            // 무료 도전 소진 + 티켓 없음 → 루비/재화 보조(과금 시뮬)로 티켓 1장 충전
            if (am.RemainingFreeEntries <= 0 && cm.GetAmount(CurrencyType.ArenaTicket) <= 0)
            {
                cm.Add(CurrencyType.ArenaTicket, 1);
                Log("[보조] 아레나 티켓 1장 지급 (과금 시뮬레이션)");
            }

            // 후보 생성 (없으면 매치 시작 불가)
            if (am.CurrentCandidates == null || am.CurrentCandidates.Count == 0)
                am.GenerateCandidates();

            // 매치 시작 + 즉시 결과 시뮬 → ArenaMatchEvent 발행
            if (am.StartMatch(0))
            {
                var matchResult = am.SimulateBattle(0);
                Log($"[행동] 아레나 매치 — 승리:{matchResult.IsVictory}, 레이팅:{matchResult.NewRating}, 티어:{matchResult.NewTier}");
                BusyFor(3f);
            }
            else
            {
                Log($"[대기] 아레나 매치 시작 불가 — {am.GetDenyReason()}");
            }
        }

        // ════════════════════════════════════
        //  UI 유틸리티
        // ════════════════════════════════════

        // 탭 이름 → 인덱스 매핑
        // 2026-04-23 이슈 5+8: tab-weapon 제거, tab-dungeon 복구
        private static readonly Dictionary<string, int> TAB_INDEX = new()
        {
            { "tab-summon", 0 },
            { "tab-character", 1 },
            { "tab-equipment", 2 },
            { "tab-skill", 3 },
            { "tab-dungeon", 4 }
        };

        /// <summary>탭 열기 (TabBarUI.SelectTab 직접 호출)</summary>
        private void ClickTab(string tabName)
        {
            var tabBar = UnityEngine.Object.FindFirstObjectByType<TabBarUI>();
            if (tabBar == null) { Log("[실패] TabBarUI 못 찾음"); return; }

            if (TAB_INDEX.TryGetValue(tabName, out int index))
            {
                tabBar.SelectTab(index);
                Log($"[탭] {tabName} 열기 (index={index})");
            }
            else
            {
                Log($"[실패] 알 수 없는 탭: {tabName}");
            }
        }

        /// <summary>보상 수령 (uGUI 클릭 → 실패 시 API 폴백)</summary>
        private void ClickClaimReward()
        {
            var qm = QuestManager.Instance;
            if (qm == null) { Log("[실패] QuestManager null"); return; }

            string questId = qm.CurrentGuideQuestId;
            Log($"[진단] 수령 시도: {questId}, completedIndex={qm.CompletedGuideIndex}");

            // 1차: uGUI 버튼 클릭
            bool claimed = false;
            var widget = UnityEngine.Object.FindFirstObjectByType<GoalGuideWidget>();
            if (widget != null)
            {
                var btnField = typeof(GoalGuideWidget).GetField("_claimButton",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (btnField?.GetValue(widget) is UnityEngine.UI.Button btn && btn.gameObject.activeInHierarchy)
                {
                    btn.onClick.Invoke();
                    claimed = true;
                    Log("[클릭] 보상 수령 버튼");
                }
            }

            // 2차: API 폴백
            if (!claimed)
            {
                bool result = qm.ClaimGuideRewardAndAdvance();
                Log($"[API] ClaimGuideRewardAndAdvance = {result}");
                claimed = result;
            }

            // 결과 확인
            string nextId = qm.CurrentGuideQuestId;
            Log($"[진단] 수령 후: currentGuide={nextId}, completedIndex={qm.CompletedGuideIndex}");

            if (!claimed)
            {
                Log("[경고] 보상 수령 실패! 다음 틱에서 재시도하지 않음");
                BusyFor(10f); // 실패 시 10초 대기 (무한 루프 방지)
            }
        }

        /// <summary>패널 이름으로 UIDocument의 root 찾기</summary>
        private VisualElement GetPanelRoot(string panelName)
        {
            var go = GameObject.Find(panelName);
            if (go == null) return null;
            var doc = go.GetComponent<UIDocument>();
            return doc?.rootVisualElement;
        }

        /// <summary>UI Toolkit Button 클릭</summary>
        private bool ClickButton(VisualElement root, string buttonName)
        {
            var btn = root?.Q<Button>(buttonName);
            if (btn == null) return false;
            ClickElement(btn);
            return true;
        }

        /// <summary>UI Toolkit 요소에 클릭 이벤트 전송</summary>
        private void ClickElement(VisualElement element)
        {
            if (element == null) return;

            // Button은 Clickable로 처리
            if (element is Button btn && btn.clickable != null)
            {
                using var nav = NavigationSubmitEvent.GetPooled();
                nav.target = btn;
                btn.SendEvent(nav);
                return;
            }

            // 일반 VisualElement는 ClickEvent
            using var click = ClickEvent.GetPooled();
            click.target = element;
            element.SendEvent(click);
        }

        /// <summary>모든 탭/팝업 닫기</summary>
        private void CloseAllUI()
        {
            var tabBar = UnityEngine.Object.FindFirstObjectByType<TabBarUI>();
            if (tabBar != null && tabBar.IsAnyPanelOpen)
                tabBar.CloseAll();
        }

        /// <summary>행동 쿨다운 설정</summary>
        private void BusyFor(float seconds)
        {
            _busyUntil = Time.time + seconds;
        }

        /// <summary>초반 직업 선택</summary>
        private void TryChooseJob()
        {
            if (_jobChosen) return;
            var js = JobSystem.Instance;
            if (js == null || js.CurrentTier > 0) return;
            js.ChangeJob(_selectedJob);
            _jobChosen = true;
            Log($"[유지보수] 직업: {_selectedJob}");
        }

        /// <summary>
        /// Title 씬 온보딩 팝업을 자동 진행한다.
        /// 진행 중이면 true를 반환해 일반 퀘스트 틱을 스킵.
        /// </summary>
        private bool TryAdvanceOnboarding()
        {
            // UI 애니메이션/Hide 처리 시간 확보
            if (Time.unscaledTime < _onboardingNextActAt) return true;

            // 0) Title Tap to start 게이트 — Input System 없이 이벤트 강제 발행
            //    2026-04-21 Title 복원 시 신설. 봇 미대응이면 여기서 무한 대기.
            var tap = UnityEngine.Object.FindFirstObjectByType<TitleTapToStartController>();
            if (tap != null && tap.gameObject.activeInHierarchy)
            {
                Log("[온보딩] Tap to start 게이트 통과");
                tap.BotTap();
                _onboardingNextActAt = Time.unscaledTime + 1.0f;
                return true;
            }

            var login = UnityEngine.Object.FindFirstObjectByType<LoginPopup>();
            if (login != null && login.gameObject.activeInHierarchy)
            {
                Log("[온보딩] 게스트 로그인");
                login.BotGuestLogin();
                _onboardingNextActAt = Time.unscaledTime + 2.0f;
                return true;
            }

            var nick = UnityEngine.Object.FindFirstObjectByType<NicknamePopup>();
            if (nick != null && nick.gameObject.activeInHierarchy)
            {
                var name = $"Bot{_selectedJob}";
                Log($"[온보딩] 닉네임: {name}");
                nick.BotSubmit(name);
                _onboardingNextActAt = Time.unscaledTime + 1.0f;
                return true;
            }

            var job = UnityEngine.Object.FindFirstObjectByType<JobSelectPopup>();
            if (job != null && job.gameObject.activeInHierarchy)
            {
                Log($"[온보딩] 직업 선택: {_selectedJob}");
                job.BotSelectAndConfirm(_selectedJob);
                _onboardingNextActAt = Time.unscaledTime + 1.5f;
                return true;
            }

            // Main 씬 진입 = 씬 이름 "Main" + QuestManager 살아있음 둘 다 만족해야 판정
            // (QuestManager는 DontDestroyOnLoad라서 Title에서도 살아있을 수 있음 — 씬 이름 필수)
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.name == "Main" && QuestManager.Instance != null)
            {
                // P0 #7 가드 (2026-04-23): LoginManager.OfflineLogin의 isNewPlayer=false 경로 등으로
                // JobSelectPopup이 스킵되어 JobSystem.CurrentJob이 _selectedJob과 불일치하는 경우
                // 강제로 ChangeJob + SaveData 동기화 — Mage/Archer Fresh 세션 실패 방지.
                try
                {
                    if (JobSystem.Instance != null && JobSystem.Instance.CurrentJob != _selectedJob)
                    {
                        Log($"[P0#7 가드] JobId 불일치 복구: {JobSystem.Instance.CurrentJob} → {_selectedJob}", important: true);
                        // _isJobLocked 해제 후 ChangeJob
                        var lockedField = typeof(JobSystem).GetField("_isJobLocked",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        lockedField?.SetValue(JobSystem.Instance, false);
                        JobSystem.Instance.ChangeJob(_selectedJob);
                        if (SaveManager.Instance?.CurrentData != null)
                            SaveManager.Instance.CurrentData.player.jobId = _selectedJob.ToString().ToLower();
                    }
                }
                catch (Exception e) { Log($"[P0#7 가드 예외] {e.Message}"); }

                _onboardingDone = true;
                _jobChosen = true;
                Log($"[온보딩] 완료 — Main 씬 진입, 직업={_selectedJob}", important: true);
                return false;
            }

            // Title 씬 로딩 또는 자동 로그인 진행 중. 한 틱 더 대기.
            return true;
        }

        /// <summary>
        /// 매 틱 유지보수: 대기 퀘스트(자동 전투) 중에도 성장 행동을 수행한다.
        /// 가챠→장착, 스킬 레벨업, 스탯 분배, 강화를 순환 시도.
        /// </summary>
        private void MaintenanceGrow(QuestDataSO currentQuest)
        {
            // 퀘스트 행동이 별도로 정의된 경우 성장 유지보수 건너뜀
            switch (currentQuest.condition)
            {
                case QuestCondition.GachaPull:
                case QuestCondition.WeaponGacha:
                case QuestCondition.EquipItem:
                case QuestCondition.EnhanceEquipment:
                case QuestCondition.StarGradeEnhance:
                case QuestCondition.StarForceReach:
                case QuestCondition.SkillLevelUp:
                case QuestCondition.AllocateStats:
                case QuestCondition.SpendGold:
                case QuestCondition.PotentialSet:
                    return; // 퀘스트 행동에서 이미 처리
            }

            // 매 틱 하나의 유지보수 행동을 UI 기반으로 수행 (탭 열기 → 실행 → 닫기)
            // 멀티스텝 진행 중이면 스킵 (이전 행동이 아직 미완료)
            if (_phase != ActionPhase.None) return;

            int maintenanceAction = _totalTickCount % 5; // 5틱 주기 순환
            switch (maintenanceAction)
            {
                case 0: // 스탯 분배 (UI: 캐릭터 탭)
                    var sas = StatAllocationSystem.Instance;
                    var player = UnityEngine.Object.FindFirstObjectByType<PlayerCharacter>();
                    var ls = player?.GetComponent<LevelSystem>();
                    int points = ls?.AvailableStatPoints ?? 0;
                    if (points > 0)
                        StartMultiStep("stat-allocate", "tab-character");
                    break;

                case 1: // 스킬 레벨업 (UI: 스킬 탭)
                    var playerChar = UnityEngine.Object.FindFirstObjectByType<PlayerCharacter>();
                    var ss = playerChar?.GetComponent<SkillSystem>();
                    if (ss != null && ss.LearnedSkills.Count > 0)
                    {
                        var firstSkill = ss.LearnedSkills[0];
                        long cost = CombatFormula.SkillLevelUpCost(ss.GetSkillLevel(firstSkill.id));
                        var cm2 = CurrencyManager.Instance;
                        if (cm2 != null && (cost <= 0 || cm2.HasEnough(CurrencyType.Gold, cost)))
                            StartMultiStep("skill-levelup", "tab-skill");
                    }
                    break;

                case 2: // 가챠 (UI: 소환 탭) — 인벤 50개 이하 + 루비 충분
                    var em2 = EquipmentManager.Instance;
                    var cm3 = CurrencyManager.Instance;
                    int invSize = em2?.GetSortedInventory()?.Count ?? 0;
                    if (invSize < 50 && cm3 != null && cm3.GetAmount(CurrencyType.Ruby) >= 300)
                        StartMultiStep("gacha", "tab-summon");
                    break;

                case 3: // 장비 장착 (UI: 장비 탭)
                    var em3 = EquipmentManager.Instance;
                    if (em3 != null)
                    {
                        var inv = em3.GetSortedInventory();
                        if (inv != null)
                        {
                            bool hasUnequipped = false;
                            for (int i = 0; i < inv.Count; i++)
                                if (!em3.IsEquipped(inv[i].instanceId)) { hasUnequipped = true; break; }
                            if (hasUnequipped)
                                StartMultiStep("equip-item", "tab-equipment");
                        }
                    }
                    break;

                case 4: // 장비 강화 (UI: 장비 탭)
                    var em4 = EquipmentManager.Instance;
                    var cm4 = CurrencyManager.Instance;
                    if (em4 != null && cm4 != null && cm4.GetAmount(CurrencyType.Gold) > 1000)
                        StartMultiStep("enhance-scroll", "tab-equipment");
                    break;
            }

            // 추가 성장 (10틱마다 1회 — 메인 순환과 독립)
            if (_totalTickCount % 10 == 5 && _phase == ActionPhase.None)
            {
                int subAction = (_totalTickCount / 10) % 4;
                switch (subAction)
                {
                    case 0: // 무기 레벨업 (WeaponManager)
                        var wm = WeaponManager.Instance;
                        if (wm != null)
                        {
                            var equipped = wm.EquippedWeapon;
                            if (equipped != null)
                            {
                                wm.LevelUpWeapon(equipped.instanceId);
                                Log($"[유지보수] 무기 레벨업: {equipped.weaponId}");
                            }
                        }
                        break;
                    case 1: // 스타포스 강화
                        var em5 = EquipmentManager.Instance;
                        if (em5 != null)
                        {
                            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
                            {
                                if (em5.TryStarForceEnhance(slot))
                                { Log($"[유지보수] 스타포스: {slot}"); _totalEnhancements++; break; }
                            }
                        }
                        break;
                    case 2: // 잠재능력 변경
                        var ps = PotentialSystem.Instance;
                        if (ps != null)
                        {
                            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
                            {
                                if (ps.CanChangePotential(slot))
                                { ps.ChangePotential(slot); Log($"[유지보수] 잠재능력: {slot}"); break; }
                            }
                        }
                        break;
                    case 3: // 유물 장착: 2026-04-20 유물 시스템 완전 제거
                        break;
                }
            }
        }

        /// <summary>직업 선택/전직 퀘스트 처리</summary>
        private void ActionJobAdvance()
        {
            var js = JobSystem.Instance;
            if (js == null) { Log("[대기] JobSystem null"); return; }

            if (!js.IsJobLocked)
            {
                js.ChangeJob(_selectedJob);
                _jobChosen = true;
                Log($"[행동] 직업 선택: {_selectedJob}", important: true);
            }
            else
            {
                // 전직은 레벨업 시 자동 발동 — 레벨업 대기
                Log($"[대기] 전직 대기 (현재 T{js.CurrentTier}) — 레벨업으로 자동 전직");
            }
        }

        // ════════════════════════════════════
        //  기록/로그
        // ════════════════════════════════════

        private void RecordQuestComplete(QuestDataSO data)
        {
            // 같은 퀘스트 중복 기록 방지
            if (_questLogs.Count > 0 && _questLogs[_questLogs.Count - 1].QuestId == data.id)
            {
                Log($"[경고] 중복 완료 감지: {data.id} — 기록 건너뜀");
                return;
            }

            float endTime = Time.time;
            _questLogs.Add(new QuestLogEntry
            {
                GuideIndex = data.guideChainIndex,
                QuestId = data.id,
                DisplayName = data.displayName,
                Condition = data.condition.ToString(),
                RequiredAmount = data.requiredAmount,
                StartTime = _lastQuestStartTime - _gameStartTime,
                EndTime = endTime - _gameStartTime,
            });
            Log($"[{FormatTime(endTime - _gameStartTime)}] ✓ G-{data.guideChainIndex + 1:D3} \"{data.displayName}\" 완료 ({FormatTime(endTime - _lastQuestStartTime)})");
            _lastQuestStartTime = Time.time;
            _stuckTickCount = 0;
        }

        private void DetectStuck(QuestDataSO data, QuestProgress progress)
        {
            int currentProgress = progress?.currentAmount ?? 0;

            if (data.id != _lastStuckQuestId)
            {
                // 새 퀘스트 → 카운터 리셋
                _stuckTickCount = 0;
                _lastStuckQuestId = data.id;
                _lastStuckProgress = currentProgress;
            }
            else if (currentProgress != _lastStuckProgress)
            {
                // 같은 퀘스트지만 진행도 변화 → stuck 리셋
                _stuckTickCount = 0;
                _lastStuckProgress = currentProgress;
            }
            else
            {
                // 진행도 변화 없음 → stuck 증가
                _stuckTickCount++;
            }

            // 60틱마다 상태 로그 — 진행 막힘은 중요 이벤트
            if (_stuckTickCount > 0 && _stuckTickCount % 60 == 0)
            {
                Log($"[stuck] G-{data.guideChainIndex + 1} {data.condition} {currentProgress}/{data.requiredAmount} — {_stuckTickCount}틱 진행 없음.", important: true);
            }

            // stuck-skip 임계: 일반 300틱, Marathon 30틱 (공격적 skip).
            int skipThreshold = _marathonMode ? 30 : 300;
            if (_stuckTickCount >= skipThreshold)
            {
                Log($"[stuck-skip] G-{data.guideChainIndex + 1} {data.condition} {currentProgress}/{data.requiredAmount} — {skipThreshold}틱 누적, auto-skip 후 진행", important: true);
                var qm = QuestManager.Instance;
                if (qm != null && progress != null)
                {
                    progress.currentAmount = data.requiredAmount;
                    progress.isCompleted = true;
                    qm.ClaimGuideRewardAndAdvance();
                }
                _stuckTickCount = 0;
                _lastStuckQuestId = null;
                BusyFor(1.0f);
            }
        }

        // ForceProgressAutoCombatQuest 제거됨 — 치트 없는 실플레이 모드

        /// <summary>봇이 자연 진행으로 달성 불가능한 대량 요구치 퀘스트 판정.
        /// ClearStage req>200: 전 세션 15건+ 수동 스킵된 구간. 절반 미만 진행 시 스킵.
        /// Marathon 모드(/marathon): 대량 요구치 전반을 공격적 skip하여 350 완주 시간 단축.
        /// </summary>
        private bool IsUnreachableAmountQuest(QuestDataSO data, QuestProgress progress)
        {
            if (data == null) return false;
            int req = data.requiredAmount;
            int cur = progress?.currentAmount ?? 0;

            // ClearStage >200 — 기본 skip (모든 모드)
            if (data.condition == QuestCondition.ClearStage && req > 200)
            {
                if (cur * 2 >= req) return false;
                return true;
            }

            // Marathon 모드: 시간 소모형 + 자원 소모형 퀘스트 공격적 skip
            // 목적: 시스템 플로우 커버리지 (실 UI 플레이 의미 약화) → 완주 시간 단축
            if (_marathonMode)
            {
                if (cur * 2 >= req) return false; // 절반 이상이면 자연 완주
                switch (data.condition)
                {
                    case QuestCondition.KillMonsters when req > 300: return true;
                    case QuestCondition.GachaPull when req > 10: return true;
                    case QuestCondition.WeaponGacha when req > 5: return true;
                    case QuestCondition.EnhanceEquipment when req > 5: return true;
                    case QuestCondition.StarGradeEnhance when req > 5: return true;
                    case QuestCondition.StarForceReach when req > 10: return true;
                    case QuestCondition.SkillUse when req > 20: return true;
                    case QuestCondition.SpendGold when req > 50000: return true;
                    case QuestCondition.SkillLevelUp when req > 5: return true;
                    case QuestCondition.AllocateStats when req > 5: return true;
                    case QuestCondition.ClearDungeon when req > 3: return true;
                    case QuestCondition.QuickHunt when req > 3: return true;
                    case QuestCondition.EquipItem when req > 5: return true;
                    case QuestCondition.UseBooster when req > 3: return true;
                    case QuestCondition.PotentialSet when req > 3: return true;
                }
            }
            return false;
        }

        /// <summary>봇이 달성 불가능한 퀘스트 판정 — 자동 스킵 대상 (이벤트 미발행/매니저 씬 누락 등).
        /// 이 목록은 "버그로 수정해야 할 대상"의 리스트이기도 하다. 차후 각 항목을 수정하여 목록에서 제거한다.</summary>
        private static bool IsRemovedSystemQuest(QuestCondition condition)
        {
            // 배선 완료되어 제거된 항목:
            //  - UseBooster, WeaponSummonLevelReach, EliteSummonLevelReach,
            //    CollectionCount, CollectionRate, GuildJoin,
            //    CostumeEquip, CostumeSetComplete,
            //    BossRaidEntry (BossRaidSystem.ExecuteRaid + GuildManager 폴백),
            //    ArenaMatch / ArenaTierReach (ArenaManager.StartMatch + SimulateBattle),
            //    QuickHunt, UnlockAbility, HeroPowerMilestone, ClimberPower, EliteSummon (2026-04-14 Phase2Setup 자동 배선)
            //  (QuestManager 구독 + 각 Manager publish 경로 검증 완료)
            // condition 파라미터는 차후 문제 항목 복원 여지를 위해 유지
            _ = condition;
            return false;
        }

        // SupplyResources 제거됨 — 치트 없는 실플레이 모드. 재화는 게임 내 보상으로만 획득.

        // 콘솔 로그 주기 제어 (Unity Editor Console 누적 메모리 폭발 방지 — 이전 세션 48GB 기록)
        private int _consoleLogCounter;
        private const int CONSOLE_LOG_INTERVAL = 10; // 10회 Log 중 1회만 Debug.Log (important=true는 항상 출력)

        /// <summary>
        /// 봇 활동 로그. _liveLog에는 항상 기록하고, Debug.Log는 thinning(important=true만 즉시 출력).
        /// </summary>
        private void Log(string msg, bool important = false)
        {
            _liveLog.AppendLine($"[{DateTime.Now:HH:mm:ss}] {msg}");

            // 콘솔 누적 방지: 중요 이벤트(완료/실패/스킵/레벨업)만 즉시 출력, 나머지는 10회 중 1회
            if (important || (++_consoleLogCounter % CONSOLE_LOG_INTERVAL == 0))
                Debug.Log($"[AutoPlayBot] {msg}");

            if (_liveLog.Length > 20000)
            {
                // 뒤쪽 5000자만 보존 (ToString().Split() GC 스파이크 방지)
                string tail = _liveLog.ToString(_liveLog.Length - 5000, 5000);
                _liveLog.Clear();
                _liveLog.Append(tail);
            }
        }

        private static string FormatTime(double sec)
        {
            var ts = TimeSpan.FromSeconds(sec);
            return ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}" : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }

        // ════════════════════════════════════
        //  리포트
        // ════════════════════════════════════
        private void GenerateReport()
        {
            float total = Time.time - _gameStartTime;

            // 중지 시점 invariant 최종 실행 (Play 유지 중이라면)
            List<InvariantResult> finalInvariants = null;
            if (EditorApplication.isPlaying)
            {
                try { finalInvariants = BotInvariants.RunAll(); }
                catch (Exception e) { Log($"[Final invariant 실패] {e.Message}"); }
            }

            var sb = new StringBuilder();
            sb.AppendLine("# AutoPlay Bot 리포트");
            sb.AppendLine($"\n- **일시**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"- **총 시간**: {FormatTime(total)}");
            sb.AppendLine($"- **완료**: {_questLogs.Count}/{TOTAL_GUIDE}");
            sb.AppendLine($"- **가챠**: {_totalGachaPulls}회");
            sb.AppendLine($"- **강화**: {_totalEnhancements}회");

            // Invariant 결과 섹션 (요약 + 실패 전수)
            sb.AppendLine("\n## Invariant 검증 결과\n");
            if (finalInvariants != null)
            {
                int pass = finalInvariants.Count(r => r.Pass);
                int crit = finalInvariants.Count(r => !r.Pass && r.Level == InvariantResult.Severity.Critical);
                int warn = finalInvariants.Count(r => !r.Pass && r.Level == InvariantResult.Severity.Warning);
                sb.AppendLine($"- 최종: {pass}/{finalInvariants.Count} pass (critical {crit}, warning {warn})");
                foreach (var r in finalInvariants.Where(r => !r.Pass))
                {
                    string sym = r.Level == InvariantResult.Severity.Critical ? "🔴" : "🟡";
                    sb.AppendLine($"  - {sym} **{r.Name}**: {r.Message}");
                }
            }
            else
            {
                sb.AppendLine("- (Play 모드 종료 후라 최종 검증 스킵)");
            }
            if (_invariantFailureLog.Count > 0)
            {
                sb.AppendLine($"\n- 실행 중 누적 실패: {_invariantFailureLog.Count}건");
            }

            var player = UnityEngine.Object.FindFirstObjectByType<PlayerCharacter>();
            if (player != null)
            {
                var ls = player.GetComponent<LevelSystem>();
                if (ls != null) sb.AppendLine($"- **레벨**: {ls.CurrentLevel}");
            }
            var sm = UnityEngine.Object.FindFirstObjectByType<StageManager>();
            if (sm != null) sb.AppendLine($"- **스테이지**: {sm.CurrentChapter}-{sm.CurrentStageIndex}");
            if (CurrencyManager.Instance != null)
            {
                sb.AppendLine($"- **골드**: {CurrencyManager.Instance.GetAmount(CurrencyType.Gold).ToKoreanShort()}");
                sb.AppendLine($"- **루비**: {CurrencyManager.Instance.GetAmount(CurrencyType.Ruby).ToKoreanShort()}");
            }

            sb.AppendLine("\n## 퀘스트별 소요 시간\n");
            sb.AppendLine("| # | ID | 이름 | 조건 | 요구 | 소요 |");
            sb.AppendLine("|---|-----|------|------|------|------|");
            foreach (var q in _questLogs)
                sb.AppendLine($"| {q.GuideIndex} | {q.QuestId} | {q.DisplayName} | {q.Condition} | {q.RequiredAmount} | {FormatTime(q.Duration)} |");

            if (_questLogs.Count > 0)
            {
                sb.AppendLine("\n## Top 5 오래 걸린 퀘스트\n");
                foreach (var q in _questLogs.OrderByDescending(x => x.Duration).Take(5))
                    sb.AppendLine($"- G-{q.GuideIndex:D3} \"{q.DisplayName}\" — {FormatTime(q.Duration)}");
            }

            if (_questLogs.Count < TOTAL_GUIDE)
            {
                sb.AppendLine("\n## 미완료\n");
                var d = QuestManager.Instance?.GetCurrentGuideData();
                var p = QuestManager.Instance?.GetCurrentGuideProgress();
                if (d != null)
                    sb.AppendLine($"- G-{d.guideChainIndex:D3} \"{d.displayName}\" — {d.condition} ({p?.currentAmount ?? 0}/{d.requiredAmount})");
            }

            // 리포트 경로: Docs/Reports/run-fresh/{YYYY-MM-DD}-raw.md
            string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
            string reportDir = "Docs/Reports/run-fresh";
            string reportPath = $"{reportDir}/{dateStr}-raw.md";
            if (!System.IO.Directory.Exists(reportDir))
                System.IO.Directory.CreateDirectory(reportDir);
            File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            Log($"리포트 저장: {reportPath}");
        }
    }
}
#endif
