using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Combat;
using MkLike.Data;
using MkLike.Economy;
using MkLike.Dungeon;
using MkLike.Quest;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// UI Toolkit 기반 통합 HUD 컨트롤러.
    /// 상단 바(P4-01), 하단 EXP/스킬(P4-02), 전투 피드(P4-03),
    /// 챌린지 알림(P4-04), 콤보 카운터(P4-05)를 하나의 UIDocument로 관리한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HudUI : MonoBehaviour
    {
        // ── 설정 ──
        private const float SKILL_UPDATE_INTERVAL = 0.1f;
        private const int MAX_FEED_LINES = 5;
        private const float FEED_DISPLAY_DURATION = 2f;

        // ── UI 참조 ──
        private UIDocument _doc;
        private VisualElement _root;

        // P4-01: 상단 바
        private Label _killCountLabel;
        private Label _stageNameLabel;
        private Label _stageProgressLabel;
        private Label _levelLabel;
        private Label _cpLabel;
        private VisualElement _hpFill;
        private Label _hpLabel;
        private Label _goldLabel;
        private Label _rubyLabel;
        private Button _settingsBtn;

        // 퀵메뉴 버튼 (중복/개발도구/코스튬 제거 후 5개만 유지)
        private Button _quickEliteBtn;
        private Button _quickHuntBtn;
        private Button _boosterBtn;
        private Button _quickArenaBtn;
        private Button _quickGuildBtn;
        private VisualElement _boosterIcons;

        // P4-02: 하단
        private VisualElement _expFill;
        private Label _expLabel;
        private VisualElement[] _skillSlots = new VisualElement[4];
        private VisualElement[] _skillIcons = new VisualElement[4];
        private VisualElement[] _skillCdOverlays = new VisualElement[4];
        private Label[] _skillCdTexts = new Label[4];
        private Label[] _skillLabels = new Label[4];

        // P4-03: 전투 피드
        private Label[] _feedLines = new Label[MAX_FEED_LINES];
        private float[] _feedTimers = new float[MAX_FEED_LINES];

        // P4-05: 콤보 (삭제됨)

        // 가이드 퀘스트 위젯
        private VisualElement _guideWidget;
        private Label _guideTitleLabel;
        private Label _guideDescLabel;
        private VisualElement _guideFill;
        private Label _guideProgressLabel;
        private Label _guideRewardLabel;
        private Button _guideClaimBtn;

        // V1-03: KPS (킬/초)
        private Label _kpsLabel;
        private const float KPS_CALC_INTERVAL = 1f;
        private float _kpsTimer;
        private int _kpsKillSnapshot;
        private float _kpsValue;

        // ── 게임 시스템 참조 ──
        private LevelSystem _levelSystem;
        private SkillSystem _skillSystem;
        private CombatStats _playerStats;

        // ── 상태 ──
        private int _totalKills;
        private int _currentLevel = 1;
        private BigNumber _currentGold;
        private BigNumber _currentRuby;
        private string _currentStageName = "1-1";
        private int _currentChapter = 1;
        private int _currentStageIndex = 1;
        private bool _isFarmingMode;
        private float _skillUpdateTimer;

        // 스킬 추적
        private string[] _assignedSkillIds = new string[4];
        private bool[] _skillUnlocked = new bool[4];

        // 현재 HP 비율 (트윈 대용 보간)
        private float _hpDisplayRatio = 1f;
        private float _hpTargetRatio = 1f;
        private float _expDisplayRatio;
        private float _expTargetRatio;
        private bool _expLevelUpAnimating;
        private double _expDisplayValue;
        private long _expTargetValue;
        private long _expRequiredValue = 1;

        // ── 퀵메뉴 잠금 ──
        private const string QUICK_LOCKED_CLASS = "quick-menu__btn--locked";

        // 퀵메뉴 버튼별 해금에 필요한 가이드 퀘스트 chainIndex (-1 = 항상 열림)
        // 순서: 정예소환, 소탕, 부스터, 아레나, 길드 (HUD.uxml과 _quickMenuButtons 배열 순서 동기)
        // 2026-04-23 완화: 유저가 초반에 전 기능 체험 가능하도록 해금 인덱스 하향 조정
        // 기존 값은 긴 가이드 체인 후기 진행이었음 — 유저 접근성 우선
        private static readonly int[] QUICK_UNLOCK_GUIDE =
        {
            8,   // 0: 정예소환 — g9(idx8): 기본 전투 적응 후
            5,   // 1: 소탕     — g6(idx5): 초반 훈련 이후
            12,  // 2: 부스터   — g13(idx12): 기본 시스템 익숙해진 후
            20,  // 3: 아레나   — g21(idx20): 스테이지 20+ 도달 부근
            30,  // 4: 길드     — g31(idx30): 스테이지 30+ 도달 부근
        };

        private Button[] _quickMenuButtons;

        // ── 초기화 ──

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            _doc.sortingOrder = 0;
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[HudUI] rootVisualElement == null, UI 초기화 스킵");
                return;
            }

            // HUD는 전체 화면을 덮지만 클릭은 통과시켜야 함 (TabBar 등 다른 UIDocument로)
            _root.pickingMode = PickingMode.Ignore;

            CacheElements();
            BindButtons();

            // 게임 시스템 탐색
            _levelSystem = FindFirstObjectByType<LevelSystem>();
            _skillSystem = FindFirstObjectByType<SkillSystem>();
            var player = FindFirstObjectByType<PlayerCharacter>();
            if (player != null)
                SetPlayerStats(player.GetComponent<CombatStats>());

            // EventBus 구독
            EventBus.Subscribe<StageChangedEvent>(OnStageChanged);
            EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            EventBus.Subscribe<ExpGainedEvent>(OnExpGained);
            EventBus.Subscribe<MonsterDiedEvent>(OnMonsterDied);
            EventBus.Subscribe<FarmingModeEvent>(OnFarmingModeChanged);
            EventBus.Subscribe<SkillLearnedEvent>(OnSkillLearned);
            // 전투 피드 이벤트
            EventBus.Subscribe<GoldGainedEvent>(OnGoldGained);
            EventBus.Subscribe<LootDroppedEvent>(OnLootDropped);

            // 가이드 퀘스트 이벤트
            EventBus.Subscribe<GuideQuestActivatedEvent>(OnGuideQuestActivated);
            EventBus.Subscribe<GuideQuestCompletedEvent>(OnGuideQuestCompleted);
            EventBus.Subscribe<QuestCompletedEvent>(OnQuestCompleted);

            // CP 변화 피드백
            EventBus.Subscribe<CpChangedEvent>(OnCpChanged);

            // 가이드 위젯 버튼
            _guideClaimBtn?.RegisterCallback<ClickEvent>(OnGuideClaimClicked);

            // 패널 상태
            UIState.OnPanelStateChanged += OnPanelStateChanged;

            // 초기 동기화
            SyncInitialState();
            SyncLearnedSkills();
            RefreshGuideWidget();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<StageChangedEvent>(OnStageChanged);
            EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            EventBus.Unsubscribe<ExpGainedEvent>(OnExpGained);
            EventBus.Unsubscribe<MonsterDiedEvent>(OnMonsterDied);
            EventBus.Unsubscribe<FarmingModeEvent>(OnFarmingModeChanged);
            EventBus.Unsubscribe<SkillLearnedEvent>(OnSkillLearned);
            EventBus.Unsubscribe<GoldGainedEvent>(OnGoldGained);
            EventBus.Unsubscribe<LootDroppedEvent>(OnLootDropped);
            EventBus.Unsubscribe<GuideQuestActivatedEvent>(OnGuideQuestActivated);
            EventBus.Unsubscribe<GuideQuestCompletedEvent>(OnGuideQuestCompleted);
            EventBus.Unsubscribe<QuestCompletedEvent>(OnQuestCompleted);
            EventBus.Unsubscribe<CpChangedEvent>(OnCpChanged);

            UIState.OnPanelStateChanged -= OnPanelStateChanged;

            if (_playerStats != null)
                _playerStats.OnHpChanged -= OnHpChanged;

            // 버튼 클릭 해제 (메모리 누수 방지)
            if (_settingsBtn != null) _settingsBtn.clicked -= OnSettingsClicked;
            if (_quickEliteBtn != null) _quickEliteBtn.clicked -= OnQuickEliteClicked;
            if (_quickHuntBtn != null) _quickHuntBtn.clicked -= OnQuickHuntClicked;
            if (_boosterBtn != null) _boosterBtn.clicked -= OnBoosterClicked;
            if (_quickArenaBtn != null) _quickArenaBtn.clicked -= OnQuickArenaClicked;
            if (_quickGuildBtn != null) _quickGuildBtn.clicked -= OnQuickGuildClicked;
        }

        private void CacheElements()
        {
            // P4-01
            _killCountLabel = _root.Q<Label>("kill-count");
            _stageNameLabel = _root.Q<Label>("stage-name");
            _stageProgressLabel = _root.Q<Label>("stage-progress");
            _levelLabel = _root.Q<Label>("level-text");
            _cpLabel = _root.Q<Label>("cp-text");
            _hpFill = _root.Q("hp-fill");
            _hpLabel = _root.Q<Label>("hp-text");
            _goldLabel = _root.Q<Label>("gold-text");
            _rubyLabel = _root.Q<Label>("ruby-text");
            _settingsBtn = _root.Q<Button>("settings-btn");
            _quickEliteBtn = _root.Q<Button>("quick-elite-btn");
            _quickHuntBtn = _root.Q<Button>("quick-hunt-btn");
            _boosterBtn = _root.Q<Button>("booster-btn");
            _quickArenaBtn = _root.Q<Button>("quick-arena-btn");
            _quickGuildBtn = _root.Q<Button>("quick-guild-btn");
            _boosterIcons = _root.Q<VisualElement>("booster-icons");

            // P4-02
            _expFill = _root.Q("exp-fill");
            _expLabel = _root.Q<Label>("exp-text");

            for (int i = 0; i < 4; i++)
            {
                _skillSlots[i] = _root.Q($"skill-slot-{i}");
                _skillIcons[i] = _root.Q($"skill-icon-{i}");
                _skillCdOverlays[i] = _root.Q($"skill-cd-overlay-{i}");
                _skillCdTexts[i] = _root.Q<Label>($"skill-cd-text-{i}");
                _skillLabels[i] = _root.Q<Label>($"skill-label-{i}");
            }

            // P4-03
            for (int i = 0; i < MAX_FEED_LINES; i++)
            {
                _feedLines[i] = _root.Q<Label>($"feed-line-{i}");
                _feedTimers[i] = 0f;
            }

            // P4-05: 콤보 삭제됨

            // V1-03: KPS
            _kpsLabel = _root.Q<Label>("kps-text");

            // 가이드 퀘스트 위젯
            _guideWidget = _root.Q("guide-quest-widget");
            _guideTitleLabel = _root.Q<Label>("guide-quest-title");
            _guideDescLabel = _root.Q<Label>("guide-quest-desc");
            _guideFill = _root.Q("guide-quest-fill");
            _guideProgressLabel = _root.Q<Label>("guide-quest-progress");
            _guideRewardLabel = _root.Q<Label>("guide-quest-reward");
            _guideClaimBtn = _root.Q<Button>("guide-quest-claim");
        }

        private void BindButtons()
        {
            if (_settingsBtn != null)
                _settingsBtn.clicked += OnSettingsClicked;

            if (_quickEliteBtn != null)
                _quickEliteBtn.clicked += OnQuickEliteClicked;
            if (_quickHuntBtn != null)
                _quickHuntBtn.clicked += OnQuickHuntClicked;
            if (_boosterBtn != null)
                _boosterBtn.clicked += OnBoosterClicked;
            if (_quickArenaBtn != null)
                _quickArenaBtn.clicked += OnQuickArenaClicked;
            if (_quickGuildBtn != null)
                _quickGuildBtn.clicked += OnQuickGuildClicked;

            // 퀵메뉴 버튼 배열 (QUICK_UNLOCK_GUIDE 순서와 동일)
            _quickMenuButtons = new Button[]
            {
                _quickEliteBtn,   // 0: 정예소환
                _quickHuntBtn,    // 1: 소탕
                _boosterBtn,      // 2: 부스터
                _quickArenaBtn,   // 3: 아레나
                _quickGuildBtn,   // 4: 길드
            };
        }

        private void SyncInitialState()
        {
            // 루비 초기값
            if (CurrencyManager.Instance != null)
            {
                _currentGold = CurrencyManager.Instance.GetAmount(CurrencyType.Gold);
                _currentRuby = CurrencyManager.Instance.GetAmount(CurrencyType.Ruby);
            }

            // EXP 초기값
            if (_levelSystem != null)
            {
                _currentLevel = _levelSystem.CurrentLevel;
                _expTargetRatio = _levelSystem.ExpRatio;
                _expDisplayRatio = _expTargetRatio;
                _expTargetValue = _levelSystem.CurrentExp;
                _expDisplayValue = _expTargetValue;
                _expRequiredValue = _levelSystem.RequiredExp;
            }

            UpdateKillCount();
            UpdateStageDisplay();
            UpdateLevelText();
            UpdateGoldText();
            UpdateRubyText();
            RefreshCP();
            RefreshHp(true);
            UpdateExpBar(true);
            RefreshQuickMenuLockVisuals();

        }

        // ── 퀵메뉴 잠금 ──

        private bool IsQuickMenuUnlocked(int index)
        {
            if (index < 0 || index >= QUICK_UNLOCK_GUIDE.Length) return false;
            int requiredGuideIndex = QUICK_UNLOCK_GUIDE[index];
            if (requiredGuideIndex < 0) return true;

            var qm = QuestManager.Instance;
            if (qm == null) return false; // QM 미초기화 시 잠금 기본값
            return qm.CompletedGuideIndex >= requiredGuideIndex;
        }

        private void RefreshQuickMenuLockVisuals()
        {
            if (_quickMenuButtons == null) return;

            for (int i = 0; i < _quickMenuButtons.Length; i++)
            {
                var btn = _quickMenuButtons[i];
                if (btn == null) continue;

                bool locked = !IsQuickMenuUnlocked(i);
                if (locked)
                    btn.AddToClassList(QUICK_LOCKED_CLASS);
                else
                    btn.RemoveFromClassList(QUICK_LOCKED_CLASS);
            }
        }

        // ── 외부 API ──

        /// <summary>
        /// 외부에서 초기값을 설정한다 (GameManager 등에서 호출).
        /// </summary>
        public void SetInitialValues(int floor, int level, long gold, float expRatio)
        {
            _currentStageName = $"{floor}";
            _currentLevel = level;
            _currentGold = gold;

            if (CurrencyManager.Instance != null)
                _currentRuby = CurrencyManager.Instance.GetAmount(CurrencyType.Ruby);

            _expTargetRatio = Mathf.Clamp01(expRatio);
            _expDisplayRatio = _expTargetRatio;

            if (_levelSystem != null)
            {
                _expTargetValue = _levelSystem.CurrentExp;
                _expDisplayValue = _expTargetValue;
                _expRequiredValue = _levelSystem.RequiredExp;
            }

            UpdateStageDisplay();
            UpdateLevelText();
            UpdateGoldText();
            UpdateRubyText();
            UpdateExpBar(true);
            RefreshCP();
            RefreshHp(true);
            UpdateKillCount();
        }

        /// <summary>
        /// 플레이어 CombatStats를 설정한다.
        /// </summary>
        public void SetPlayerStats(CombatStats stats)
        {
            if (_playerStats != null)
                _playerStats.OnHpChanged -= OnHpChanged;

            _playerStats = stats;

            if (_playerStats != null)
            {
                _playerStats.OnHpChanged += OnHpChanged;
                RefreshCP();
                RefreshHp(true);
            }
        }

        /// <summary>
        /// 전체 새로고침.
        /// </summary>
        public void Refresh()
        {
            RefreshCP();
            RefreshHp(true);
        }

        /// <summary>
        /// EXP 비율 갱신.
        /// </summary>
        public void RefreshExp(float ratio)
        {
            float clamped = Mathf.Clamp01(ratio);
            bool isLevelUp = clamped < _expDisplayRatio - 0.1f && _expDisplayRatio > 0.3f;
            if (isLevelUp)
                _expLevelUpAnimating = true;
            _expTargetRatio = clamped;

            if (_levelSystem != null)
            {
                _expTargetValue = _levelSystem.CurrentExp;
                _expRequiredValue = _levelSystem.RequiredExp;
                if (isLevelUp)
                    _expDisplayValue = 0;
            }
        }

        // ── Update (매 프레임) ──

        private void Update()
        {
            float dt = Time.deltaTime;

            // HP 보간
            if (Mathf.Abs(_hpDisplayRatio - _hpTargetRatio) > 0.001f)
            {
                _hpDisplayRatio = Mathf.Lerp(_hpDisplayRatio, _hpTargetRatio, dt * 8f);
                ApplyHpBar(_hpDisplayRatio);
            }

            // EXP 보간 (레벨업 시 fill → 1.0 → snap 0 → target)
            if (_expLevelUpAnimating)
            {
                _expDisplayRatio = Mathf.Lerp(_expDisplayRatio, 1f, dt * 10f);
                ApplyExpFill(_expDisplayRatio);
                if (_expDisplayRatio >= 0.99f)
                {
                    _expDisplayRatio = 0f;
                    _expLevelUpAnimating = false;
                    ApplyExpFill(0f);
                }
            }
            else if (Mathf.Abs(_expDisplayRatio - _expTargetRatio) > 0.001f)
            {
                _expDisplayRatio = Mathf.Lerp(_expDisplayRatio, _expTargetRatio, dt * 6f);
                ApplyExpFill(_expDisplayRatio);
            }

            // EXP 숫자 카운트업
            if (System.Math.Abs(_expDisplayValue - _expTargetValue) > 0.5)
            {
                _expDisplayValue = System.Math.Round(
                    _expDisplayValue + (_expTargetValue - _expDisplayValue) * Mathf.Min(1f, dt * 6f));
                ApplyExpText((long)_expDisplayValue, _expRequiredValue);
            }

            // 스킬 슬롯 갱신
            _skillUpdateTimer -= dt;
            if (_skillUpdateTimer <= 0f)
            {
                _skillUpdateTimer = SKILL_UPDATE_INTERVAL;
                UpdateSkillSlots();
            }

            // 전투 피드 페이드
            UpdateFeedTimers(dt);

            // KPS 갱신
            UpdateKps(dt);
        }

        // ═══════ P4-01: 이벤트 핸들러 ═══════

        private void OnStageChanged(StageChangedEvent evt)
        {
            _currentStageName = evt.DisplayName;
            _currentChapter = evt.Chapter;
            _currentStageIndex = evt.StageIndex;
            UpdateStageDisplay();
        }

        private void OnLevelUp(LevelUpEvent evt)
        {
            _currentLevel = evt.CurrentLevel;
            UpdateLevelText();
            RefreshCP();

            if (_levelSystem != null)
                RefreshExp(_levelSystem.ExpRatio);

            // UI Toolkit 레벨업 배너 표시
            ShowLevelUpBanner(evt.PreviousLevel, evt.CurrentLevel);
        }

        private void OnCurrencyChanged(CurrencyChangedEvent evt)
        {
            if (evt.Type == CurrencyType.Gold)
            {
                _currentGold = evt.CurrentAmount;
                UpdateGoldText();
            }
            else if (evt.Type == CurrencyType.Ruby)
            {
                _currentRuby = evt.CurrentAmount;
                UpdateRubyText();
            }
        }

        private void OnExpGained(ExpGainedEvent evt)
        {
            if (_levelSystem != null)
                RefreshExp(_levelSystem.ExpRatio);

            // 전투 피드에도 추가
            if (evt.Amount > 0)
                AddFeedLog($"+{FormatNumber(evt.Amount)} 경험치", new Color(0.3f, 0.9f, 0.95f));
        }

        private void OnMonsterDied(MonsterDiedEvent evt)
        {
            _totalKills++;
            UpdateKillCount();

            // 가이드 퀘스트 갱신은 10킬마다 (매 킬 갱신은 성능 낭비)
            if (_totalKills % 10 == 0)
                RefreshGuideWidget();
        }

        private void OnFarmingModeChanged(FarmingModeEvent evt)
        {
            _isFarmingMode = evt.IsActive;

            if (evt.IsActive && evt.IsPlayerDead)
            {
                _hpTargetRatio = 0f;
                _hpDisplayRatio = 0f;
                ApplyHpBar(0f);
                if (_hpLabel != null)
                    _hpLabel.text = "사망";
            }
            else
            {
                RefreshHp(true);
            }
        }

        private void OnHpChanged(int current, int max)
        {
            if (_isFarmingMode) return;
            if (max <= 0) return;

            _hpTargetRatio = (float)current / max;
            if (_hpLabel != null)
                _hpLabel.text = $"{current}/{max}";
        }

        private void OnSkillLearned(SkillLearnedEvent evt)
        {
            SyncLearnedSkills();
        }

        private void OnPanelStateChanged(bool isOpen)
        {
            // 패널 열릴 때 HUD 요소 전체 숨김 (탭 바만 유지)
            var topBar = _root.Q("top-bar");
            var bottomArea = _root.Q("bottom-area");
            var combatFeed = _root.Q("combat-feed");
            var quickMenu = _root.Q("quick-menu");

            if (topBar != null)
                topBar.style.display = isOpen ? DisplayStyle.None : DisplayStyle.Flex;
            if (bottomArea != null)
                bottomArea.style.display = isOpen ? DisplayStyle.None : DisplayStyle.Flex;
            if (combatFeed != null)
                combatFeed.style.display = isOpen ? DisplayStyle.None : DisplayStyle.Flex;
            if (quickMenu != null)
                quickMenu.style.display = isOpen ? DisplayStyle.None : DisplayStyle.Flex;
            if (_guideWidget != null)
                _guideWidget.style.display = isOpen ? DisplayStyle.None
                    : (QuestManager.Instance != null && !string.IsNullOrEmpty(QuestManager.Instance.CurrentGuideQuestId)
                        ? DisplayStyle.Flex : DisplayStyle.None);
        }

        // ═══════ P4-01: 갱신 메서드 ═══════

        private void UpdateStageDisplay()
        {
            if (_stageNameLabel != null)
                _stageNameLabel.text = _currentStageName;
            if (_stageProgressLabel != null)
            {
                int maxStage = _currentStageIndex >= 10 ? _currentStageIndex : 10;
                _stageProgressLabel.text = $"스테이지 {_currentStageIndex}/{maxStage}";
            }
        }

        private void UpdateLevelText()
        {
            if (_levelLabel != null)
                _levelLabel.text = $"Lv.{_currentLevel}";
        }

        private void UpdateGoldText()
        {
            if (_goldLabel != null)
                _goldLabel.text = FormatGold(_currentGold);
        }

        private void UpdateRubyText()
        {
            if (_rubyLabel != null)
                _rubyLabel.text = FormatGold(_currentRuby);
        }

        private void UpdateKillCount()
        {
            if (_killCountLabel != null)
                _killCountLabel.text = $"x {_totalKills}";
        }

        private void UpdateKps(float dt)
        {
            _kpsTimer += dt;
            if (_kpsTimer < KPS_CALC_INTERVAL) return;

            int killsDelta = _totalKills - _kpsKillSnapshot;
            _kpsValue = killsDelta / _kpsTimer;
            _kpsKillSnapshot = _totalKills;
            _kpsTimer = 0f;

            if (_kpsLabel != null)
            {
                if (_kpsValue >= 0.1f)
                    _kpsLabel.text = $"{_kpsValue:F1}/s";
                else
                    _kpsLabel.text = "";
            }
        }

        private void RefreshCP()
        {
            if (_cpLabel == null) return;
            if (_playerStats != null)
            {
                long cp = CombatFormula.CalculateCP(_playerStats);
                _cpLabel.text = $"전투력 {cp:N0}";
            }
            else
            {
                _cpLabel.text = "전투력 ---";
            }
        }

        private void RefreshHp(bool instant = false)
        {
            if (_playerStats == null) return;
            int max = _playerStats.MaxHp;
            if (max <= 0) return;

            float ratio = (float)_playerStats.CurrentHp / max;
            _hpTargetRatio = ratio;

            if (instant)
            {
                _hpDisplayRatio = ratio;
                ApplyHpBar(ratio);
            }

            if (_hpLabel != null)
                _hpLabel.text = $"{_playerStats.CurrentHp}/{max}";
        }

        private void ApplyHpBar(float ratio)
        {
            if (_hpFill != null)
                _hpFill.style.width = new StyleLength(new Length(ratio * 100f, LengthUnit.Percent));
        }

        // ═══════ P4-02: EXP + 스킬 ═══════

        private void UpdateExpBar(bool instant = false)
        {
            if (instant)
            {
                _expDisplayRatio = _expTargetRatio;
                if (_levelSystem != null)
                {
                    _expTargetValue = _levelSystem.CurrentExp;
                    _expDisplayValue = _expTargetValue;
                    _expRequiredValue = _levelSystem.RequiredExp;
                }
                ApplyExpBar(_expTargetRatio);
            }
        }

        private void ApplyExpBar(float ratio)
        {
            ApplyExpFill(ratio);
            long currentExp = _levelSystem != null ? _levelSystem.CurrentExp : 0;
            long requiredExp = _levelSystem != null ? _levelSystem.RequiredExp : 1;
            ApplyExpText(currentExp, requiredExp);
        }

        private void ApplyExpFill(float ratio)
        {
            float clamped = Mathf.Clamp01(ratio);
            if (_expFill != null)
                _expFill.style.width = new StyleLength(new Length(clamped * 100f, LengthUnit.Percent));
        }

        private void ApplyExpText(long currentExp, long requiredExp)
        {
            if (_expLabel != null)
                _expLabel.text = $"{NumberFormatter.FormatKorean(currentExp)} / {NumberFormatter.FormatKorean(requiredExp)}";
        }

        private void SyncLearnedSkills()
        {
            if (_skillSystem == null) return;

            var allSkills = _skillSystem.AllSkills;
            if (allSkills == null) return;

            var learnedIds = new System.Collections.Generic.HashSet<string>();
            foreach (var skill in _skillSystem.LearnedSkills)
                learnedIds.Add(skill.id);

            SkillDataSO[] latestLearned = new SkillDataSO[4];
            SkillDataSO[] firstPerSlot = new SkillDataSO[4]; // 슬롯별 첫 번째 스킬 (미학습 딤드용)

            foreach (var skill in allSkills)
            {
                if (skill == null) continue;
                int idx = GetSlotIndex(skill.skillType);
                if (idx < 0 || idx >= 4) continue;

                // 슬롯별 첫 번째 스킬 기록 (미학습 시 아이콘 표시용)
                if (firstPerSlot[idx] == null)
                    firstPerSlot[idx] = skill;

                if (learnedIds.Contains(skill.id))
                    latestLearned[idx] = skill;
            }

            for (int i = 0; i < 4; i++)
            {
                if (latestLearned[i] != null)
                {
                    _assignedSkillIds[i] = latestLearned[i].id;
                    _skillUnlocked[i] = true;

                    // 학습 완료 — 실제 아이콘 + 풀 opacity
                    if (_skillIcons[i] != null)
                    {
                        if (latestLearned[i].icon != null)
                            _skillIcons[i].style.backgroundImage = new StyleBackground(latestLearned[i].icon);
                        else
                            _skillIcons[i].style.backgroundImage = StyleKeyword.None;
                    }

                    if (_skillSlots[i] != null)
                        _skillSlots[i].style.opacity = 1f;

                    if (_skillLabels[i] != null)
                        _skillLabels[i].text = latestLearned[i].displayName;
                }
                else
                {
                    _assignedSkillIds[i] = null;
                    _skillUnlocked[i] = false;

                    // 미학습 — 실제 스킬 아이콘을 딤드(어둡게) 표시
                    if (_skillIcons[i] != null)
                    {
                        var preview = firstPerSlot[i];
                        if (preview != null && preview.icon != null)
                            _skillIcons[i].style.backgroundImage = new StyleBackground(preview.icon);
                        else
                            _skillIcons[i].style.backgroundImage = StyleKeyword.None;
                    }

                    // 잠금 상태 표시
                    if (_skillSlots[i] != null)
                        _skillSlots[i].style.opacity = 0.3f;

                    if (_skillLabels[i] != null)
                        _skillLabels[i].text = "미해금";
                }
            }
        }

        private void UpdateSkillSlots()
        {
            if (_skillSystem == null) return;

            for (int i = 0; i < 4; i++)
            {
                if (!_skillUnlocked[i] || string.IsNullOrEmpty(_assignedSkillIds[i])) continue;

                // Active(0), Passive(1)는 쿨타임 없음
                if (i <= 1)
                {
                    if (_skillCdOverlays[i] != null)
                        _skillCdOverlays[i].style.height = new StyleLength(0f);
                    if (_skillCdTexts[i] != null)
                        _skillCdTexts[i].text = "";
                    continue;
                }

                string id = _assignedSkillIds[i];
                float cdRemaining = _skillSystem.GetCooldownRemaining(id);
                float cdTotal = 0f;
                foreach (var skill in _skillSystem.LearnedSkills)
                {
                    if (skill.id == id)
                    {
                        cdTotal = skill.cooldown;
                        break;
                    }
                }

                // 쿨다운 오버레이 높이 (아래→위)
                if (cdTotal > 0f && cdRemaining > 0f)
                {
                    float ratio = cdRemaining / cdTotal;
                    if (_skillCdOverlays[i] != null)
                        _skillCdOverlays[i].style.height = new StyleLength(new Length(ratio * 100f, LengthUnit.Percent));
                    if (_skillCdTexts[i] != null)
                        _skillCdTexts[i].text = $"{cdRemaining:F1}";
                }
                else
                {
                    if (_skillCdOverlays[i] != null)
                        _skillCdOverlays[i].style.height = new StyleLength(0f);
                    if (_skillCdTexts[i] != null)
                        _skillCdTexts[i].text = "";
                }
            }
        }

        private int GetSlotIndex(SkillType type)
        {
            return type switch
            {
                SkillType.Active => 0,
                SkillType.Passive => 1,
                SkillType.Buff => 2,
                SkillType.Awakening => 3,
                _ => -1
            };
        }

        // ═══════ P4-03: 전투 피드 ═══════

        private void OnGoldGained(GoldGainedEvent evt)
        {
            if (evt.Amount <= 0) return;
            AddFeedLog($"+{FormatNumber(evt.Amount)} 골드", new Color(1f, 0.84f, 0f));
        }

        private void OnLootDropped(LootDroppedEvent evt)
        {
            string currencyName = DisplayNameUtils.GetCurrencyDisplayName(evt.Type);
            Color color = GetCurrencyColor(evt.Type);
            AddFeedLog($"+{FormatNumber(evt.Amount)} {currencyName}", color);
        }

        private void AddFeedLog(string message, Color color)
        {
            // 위로 밀기 (index 4=최상단 → 0=최하단)
            for (int i = MAX_FEED_LINES - 1; i > 0; i--)
            {
                if (_feedLines[i] != null && _feedLines[i - 1] != null)
                {
                    _feedLines[i].text = _feedLines[i - 1].text;
                    _feedLines[i].style.color = _feedLines[i - 1].style.color;
                    _feedLines[i].style.opacity = _feedLines[i - 1].style.opacity;
                    _feedTimers[i] = _feedTimers[i - 1];
                }
            }

            // 맨 아래 슬롯에 새 로그
            if (_feedLines[0] != null)
            {
                _feedLines[0].text = message;
                _feedLines[0].style.color = color;
                _feedLines[0].style.opacity = 1f;
                _feedTimers[0] = FEED_DISPLAY_DURATION;
            }
        }

        private void UpdateFeedTimers(float dt)
        {
            bool isPanelOpen = UIState.IsAnyPanelOpen;

            for (int i = 0; i < MAX_FEED_LINES; i++)
            {
                if (_feedLines[i] == null) continue;

                if (isPanelOpen)
                {
                    _feedLines[i].style.opacity = 0f;
                    continue;
                }

                if (_feedTimers[i] <= 0f)
                {
                    float currentOpacity = _feedLines[i].resolvedStyle.opacity;
                    if (currentOpacity > 0f)
                        _feedLines[i].style.opacity = Mathf.Max(0f, currentOpacity - dt * 4f);
                }
                else
                {
                    _feedTimers[i] -= dt;
                    if (_feedTimers[i] < 1f)
                        _feedLines[i].style.opacity = Mathf.Max(0f, _feedTimers[i]);
                }
            }
        }

        // ═══════ 설정 버튼 ═══════

        private void OnSettingsClicked()
        {
            var miscPanel = FindFirstObjectByType<MiscPanel>(FindObjectsInactive.Include);
            if (miscPanel == null) return;

            if (miscPanel.gameObject.activeSelf)
                miscPanel.Hide();
            else
                miscPanel.Show();
        }

        private void OnQuickEliteClicked()
        {
            if (!CheckQuickMenuUnlock(0)) return;
            var elitePopup = Object.FindFirstObjectByType<EliteSummonPopupUI>(FindObjectsInactive.Include);
            if (elitePopup != null)
            {
                elitePopup.gameObject.SetActive(true);
                elitePopup.Show();
            }
        }

        private void OnQuickHuntClicked()
        {
            if (!CheckQuickMenuUnlock(1)) return;
            var huntPopup = Object.FindFirstObjectByType<QuickHuntPopup>(FindObjectsInactive.Include);
            if (huntPopup != null)
            {
                huntPopup.gameObject.SetActive(true);
                huntPopup.Show();
            }
        }

        private void OnBoosterClicked()
        {
            if (!CheckQuickMenuUnlock(2)) return;
            var boosterPopup = Object.FindFirstObjectByType<BoosterPopup>(FindObjectsInactive.Include);
            if (boosterPopup != null)
            {
                boosterPopup.gameObject.SetActive(true);
                boosterPopup.Show();
            }
        }

        private void OnQuickArenaClicked()
        {
            if (!CheckQuickMenuUnlock(3)) return;
            var arenaPopup = Object.FindFirstObjectByType<ArenaPopup>(FindObjectsInactive.Include);
            if (arenaPopup != null)
            {
                arenaPopup.gameObject.SetActive(true);
                arenaPopup.Show();
            }
        }

        private void OnQuickGuildClicked()
        {
            if (!CheckQuickMenuUnlock(4)) return;
            var guildPopup = Object.FindFirstObjectByType<GuildPopup>(FindObjectsInactive.Include);
            if (guildPopup != null)
            {
                guildPopup.gameObject.SetActive(true);
                guildPopup.Show();
            }
        }

        /// <summary>
        /// 퀵메뉴 잠금 확인. 잠금이면 토스트+에러음 출력 후 false 반환.
        /// </summary>
        private bool CheckQuickMenuUnlock(int index)
        {
            if (IsQuickMenuUnlocked(index)) return true;
            AudioManager.Instance?.PlayUiSfx("sfx_ui_error");
            ToastUI.Show("가이드 퀘스트를 진행하세요", "\uD83D\uDD12");
            return false;
        }

        // ═══════ 유틸 ═══════
        // 2026-04-23 리팩토링: FormatGold 중복 제거 — HudPanel.FormatGold(BigNumber) 직접 사용 가능.
        // FormatNumber는 K/M/B 축약 포맷으로 NumberFormatter와 포맷이 달라 유지 (필요 시 추후 NumberFormatter에 흡수).

        private static string FormatGold(BigNumber value) => NumberFormatter.FormatKorean(value);

        private static string FormatNumber(long value)
        {
            if (value >= 1_000_000_000) return $"{value / 1_000_000_000f:0.#}B";
            if (value >= 1_000_000) return $"{value / 1_000_000f:0.#}M";
            if (value >= 1_000) return $"{value / 1_000f:0.#}K";
            return value.ToString("N0");
        }

        private static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int min = (int)(seconds / 60f);
            int sec = (int)(seconds % 60f);
            int ms = (int)((seconds % 1f) * 10f);

            if (min > 0)
                return $"{min:D2}:{sec:D2}.{ms}";
            return $"{sec:D2}.{ms}";
        }

        private static Color GetCurrencyColor(CurrencyType type)
        {
            return type switch
            {
                CurrencyType.Gold => new Color(1f, 0.84f, 0f),
                CurrencyType.Ruby => new Color(0.9f, 0.2f, 0.4f),
                CurrencyType.BlueDiamond => new Color(0.6f, 0.8f, 1f),
                _ => new Color(0.85f, 0.85f, 0.85f)
            };
        }

        // ══════════════════════════════════════
        // 레벨업 배너 (UI Toolkit 오버레이)
        // ══════════════════════════════════════

        private void ShowLevelUpBanner(int prevLevel, int newLevel)
        {
            // 2026-04-23 패널 열린 상태에선 HUD 배너 숨김 (Toast는 panel 위로 이미 표시됨)
            if (UIState.IsAnyPanelOpen) return;
            ShowLevelUpBannerAsync(prevLevel, newLevel).Forget();
        }

        private async UniTaskVoid ShowLevelUpBannerAsync(int prevLevel, int newLevel)
        {
            var ct = this.GetCancellationTokenOnDestroy();

            // 배너 컨테이너 (플래시 오버레이 제거 — 유저 피드백)
            var banner = new VisualElement();
            banner.pickingMode = PickingMode.Ignore;
            banner.style.position = Position.Absolute;
            banner.style.left = 0;
            banner.style.right = 0;
            banner.style.top = new Length(35, LengthUnit.Percent);
            banner.style.alignItems = Align.Center;

            // 메인 텍스트
            var mainLabel = new Label("레벨 업!");
            mainLabel.pickingMode = PickingMode.Ignore;
            mainLabel.style.fontSize = 36;
            mainLabel.style.color = new Color(1f, 0.85f, 0.1f);
            mainLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            mainLabel.style.unityTextOutlineWidth = 1f;
            mainLabel.style.unityTextOutlineColor = new Color(0.3f, 0.15f, 0f);
            mainLabel.style.scale = new Scale(new Vector2(0f, 0f));
            mainLabel.style.transitionProperty = new List<StylePropertyName> { new("scale") };
            mainLabel.style.transitionDuration = new List<TimeValue> { new(0.25f, TimeUnit.Second) };
            mainLabel.style.transitionTimingFunction = new List<EasingFunction> { new(EasingMode.EaseOutBack) };
            banner.Add(mainLabel);

            // 서브 텍스트
            var subLabel = new Label($"Lv.{prevLevel}  →  Lv.{newLevel}");
            subLabel.pickingMode = PickingMode.Ignore;
            subLabel.style.fontSize = 18;
            subLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            subLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            subLabel.style.marginTop = 4;
            subLabel.style.opacity = 0f;
            subLabel.style.transitionProperty = new List<StylePropertyName> { new("opacity") };
            subLabel.style.transitionDuration = new List<TimeValue> { new(0.3f, TimeUnit.Second) };
            banner.Add(subLabel);

            _root.Add(banner);

            // 프레임 대기 후 애니메이션 트리거
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, ct);
            mainLabel.style.scale = new Scale(Vector2.one);
            subLabel.style.opacity = 1f;

            // 1.5초 유지
            await UniTask.Delay(1500, cancellationToken: ct);

            // 페이드아웃
            mainLabel.style.transitionProperty = new List<StylePropertyName> { new("opacity"), new("scale") };
            mainLabel.style.transitionDuration = new List<TimeValue> { new(0.4f, TimeUnit.Second), new(0.4f, TimeUnit.Second) };
            mainLabel.style.transitionTimingFunction = new List<EasingFunction> { new(EasingMode.EaseIn), new(EasingMode.EaseIn) };
            mainLabel.style.opacity = 0f;
            mainLabel.style.scale = new Scale(new Vector2(0.5f, 0.5f));

            subLabel.style.opacity = 0f;

            await UniTask.Delay(500, cancellationToken: ct);

            // 요소 정리
            _root.Remove(banner);
        }

        // ═══════ CP 변화 피드백 ═══════

        private void OnCpChanged(CpChangedEvent evt)
        {
            if (evt.Delta == 0 || UIState.IsAnyPanelOpen) return;

            // 스킬 버프 토글/해제 등 일시 변화는 피드에 표시하지 않음 (노이즈)
            if (evt.Reason == "스킬 버프" || evt.Reason == "스킬 버프 해제") return;

            // CP 변화량이 일정 이상일 때만 배너 표시 (초반 자동 세팅 노이즈 억제)
            if (Mathf.Abs(evt.Delta) < 500) return;

            string sign = evt.Delta > 0 ? "+" : "";
            string reason = string.IsNullOrEmpty(evt.Reason) ? "" : $" ({evt.Reason})";
            Color color = evt.Delta > 0 ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.3f, 0.3f);

            AddFeedLog($"CP {sign}{NumberFormatter.FormatKorean(evt.Delta)}{reason}", color);
        }

        // ═══════ 가이드 퀘스트 위젯 ═══════

        private void RefreshGuideWidget()
        {
            if (_guideWidget == null) return;

            var qm = QuestManager.Instance;
            if (qm == null || string.IsNullOrEmpty(qm.CurrentGuideQuestId))
            {
                _guideWidget.style.display = DisplayStyle.None;
                return;
            }

            var data = qm.GetCurrentGuideData();
            var progress = qm.GetCurrentGuideProgress();
            if (data == null)
            {
                _guideWidget.style.display = DisplayStyle.None;
                return;
            }

            _guideWidget.style.display = DisplayStyle.Flex;

            if (_guideTitleLabel != null)
                _guideTitleLabel.text = $"가이드 #{data.guideChainIndex + 1}";

            if (_guideDescLabel != null)
                _guideDescLabel.text = data.displayName;

            int current = progress?.currentAmount ?? 0;
            int target = data.requiredAmount;
            float ratio = target > 0 ? Mathf.Clamp01((float)current / target) : 0f;

            if (_guideFill != null)
                _guideFill.style.width = new Length(ratio * 100f, LengthUnit.Percent);

            if (_guideProgressLabel != null)
                _guideProgressLabel.text = $"{current} / {target}";

            if (_guideRewardLabel != null)
            {
                string rewardStr = FormatGuideReward(data.rewardType, data.rewardAmount);
                if (data.bonusRewardAmount > 0)
                    rewardStr += $" + {FormatGuideReward(data.bonusRewardType, data.bonusRewardAmount)}";
                _guideRewardLabel.text = rewardStr;
            }

            UpdateGuideClaimButton(progress);
        }

        private void UpdateGuideClaimButton(QuestProgress progress)
        {
            if (_guideClaimBtn == null) return;

            bool canClaim = progress != null && progress.isCompleted && !progress.isRewardClaimed;
            if (canClaim)
            {
                _guideClaimBtn.AddToClassList("guide-quest__claim--visible");
                // 완료 시 위젯 글로우
                if (_guideWidget != null)
                    _guideWidget.AddToClassList("guide-quest--complete");
            }
            else
            {
                _guideClaimBtn.RemoveFromClassList("guide-quest__claim--visible");
                if (_guideWidget != null)
                    _guideWidget.RemoveFromClassList("guide-quest--complete");
            }
        }

        private void OnGuideClaimClicked(ClickEvent _)
        {
            var qm = QuestManager.Instance;
            if (qm == null) return;

            if (qm.ClaimGuideRewardAndAdvance())
            {
                AudioManager.Instance?.PlayUiSfx("sfx_ui_reward");
                RefreshGuideWidget();
            }
        }

        private void OnGuideQuestActivated(GuideQuestActivatedEvent evt)
        {
            RefreshGuideWidget();
        }

        private void OnGuideQuestCompleted(GuideQuestCompletedEvent evt)
        {
            RefreshGuideWidget();
            RefreshQuickMenuLockVisuals();
        }

        private void OnQuestCompleted(QuestCompletedEvent evt)
        {
            RefreshGuideWidget();
        }

        private static string FormatGuideReward(CurrencyType type, int amount)
        {
            string name = type switch
            {
                CurrencyType.Gold => "골드",
                CurrencyType.Ruby => "루비",
                CurrencyType.WeaponTicket => "소환권",
                _ => type.ToString()
            };
            return $"{name} x{amount:N0}";
        }
    }
}
