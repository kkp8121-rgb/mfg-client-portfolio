using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using MkLike.Utils;
using MkLike.Core;
using MkLike.Combat;
using MkLike.Data;
using MkLike.Economy;

namespace MkLike.UI
{
    /// <summary>
    /// 메인 HUD 패널. 레퍼런스(메이플 키우기) 레이아웃 기반.
    /// 상단 바(킬카운트, 스테이지), 좌측 정보(레벨, CP, 골드, HP), 하단 EXP바.
    /// </summary>
    public class HudPanel : MonoBehaviour
    {
        [Header("상단 바")]
        [SerializeField] private TextMeshProUGUI _killCountText;
        [SerializeField] private TextMeshProUGUI _stageNameText;
        [SerializeField] private TextMeshProUGUI _stageProgressText;

        [Header("좌측 정보 패널")]
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private TextMeshProUGUI _cpText;
        [SerializeField] private TextMeshProUGUI _goldText;
        [SerializeField] private TextMeshProUGUI _rubyText;
        [SerializeField] private Slider _hpSlider;
        [SerializeField] private TextMeshProUGUI _hpText;

        [Header("하단 EXP 바")]
        [SerializeField] private Slider _expSlider;
        [SerializeField] private TextMeshProUGUI _expText;

        [Header("스킬 슬롯")]
        [SerializeField] private SkillSlotUI[] _skillSlots;

        [Header("재도전 버튼")]
        [SerializeField] private GameObject _retryButtonObj;

        [Header("사망 오버레이")]
        [SerializeField] private GameObject _deathOverlay;

        [Header("설정")]
        [SerializeField] private Button _settingsButton;
        [SerializeField] private GameObject _miscPanelObj;

        [Header("참조")]
        [SerializeField] private CombatStats _playerStats;

        private LevelSystem _levelSystem;
        private SkillSystem _skillSystem;
        private Tween _expTween;
        private Tween _expCountTween;
        private Sequence _expLevelUpSeq;
        private Tween _hpTween;
        private Tween _deathOverlayTween;
        private float _skillUpdateTimer;
        private const float SKILL_UPDATE_INTERVAL = 0.1f;
        private int _totalKills;
        private int _currentLevel = 1;
        private BigNumber _currentGold;
        private BigNumber _currentRuby;
        private string _currentStageName = "1-1";
        private int _currentChapter = 1;
        private int _currentStageIndex = 1;
        private bool _isFarmingMode;

        private void OnEnable()
        {
            EventBus.Subscribe<StageChangedEvent>(OnStageChanged);
            EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            EventBus.Subscribe<ExpGainedEvent>(OnExpGained);
            EventBus.Subscribe<MonsterDiedEvent>(OnMonsterDied);
            EventBus.Subscribe<FarmingModeEvent>(OnFarmingModeChanged);
            EventBus.Subscribe<SkillLearnedEvent>(OnSkillLearned);
            UIState.OnPanelStateChanged += OnPanelStateChanged;

            if (_playerStats != null)
                _playerStats.OnHpChanged += OnHpChanged;

            if (_retryButtonObj != null)
                _retryButtonObj.SetActive(false);

            // 스킬 슬롯 잠금 상태로 표시
            if (_skillSlots != null)
            {
                for (int i = 0; i < _skillSlots.Length; i++)
                {
                    if (_skillSlots[i] != null)
                        _skillSlots[i].ShowLocked();
                }
            }
        }

        private void Start()
        {
            // 설정 버튼 → MiscPanel 토글
            if (_settingsButton != null)
                _settingsButton.onClick.AddListener(ToggleMiscPanel);

            // 재도전 버튼 런타임 와이어링 (에디터 persistent listener 누락 대비)
            WireRetryButton();

            // FindFirstObjectByType 캐싱
            _skillSystem = FindFirstObjectByType<SkillSystem>();
            _levelSystem = FindFirstObjectByType<LevelSystem>();

            // GUI Kit 테마 스프라이트 적용
            ApplyThemeSprites();

            // 이미 습득된 스킬을 4슬롯(Active/Passive/Buff/Awakening)에 동기화
            SyncLearnedSkills();
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
            UIState.OnPanelStateChanged -= OnPanelStateChanged;

            if (_playerStats != null)
                _playerStats.OnHpChanged -= OnHpChanged;
        }

        private void Update()
        {
            _skillUpdateTimer -= Time.deltaTime;
            if (_skillUpdateTimer <= 0f)
            {
                _skillUpdateTimer = SKILL_UPDATE_INTERVAL;
                UpdateSkillSlots();
            }
        }

        // ── 외부 API ──

        public void SetInitialValues(int floor, int level, long gold, float expRatio)
        {
            _currentStageName = $"{floor}";
            _currentLevel = level;
            _currentGold = gold;

            // 루비 초기값 동기화
            if (CurrencyManager.Instance != null)
                _currentRuby = CurrencyManager.Instance.GetAmount(CurrencyType.Ruby);

            UpdateStageDisplay();
            UpdateLevelText();
            UpdateGoldText();
            UpdateRubyText();
            UpdateExpBar(expRatio, instant: true);
            RefreshCP();
            RefreshHp();
            UpdateKillCount();
        }

        public void Refresh()
        {
            RefreshCP();
            RefreshHp();
        }

        public void RefreshExp(float ratio)
        {
            UpdateExpBar(ratio);
        }

        public void SetPlayerStats(CombatStats stats)
        {
            if (_playerStats != null)
                _playerStats.OnHpChanged -= OnHpChanged;

            _playerStats = stats;

            if (_playerStats != null)
            {
                _playerStats.OnHpChanged += OnHpChanged;
                RefreshCP();
                RefreshHp();
            }
        }

        // ── 이벤트 핸들러 ──

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
                UpdateExpBar(_levelSystem.ExpRatio);

            // 레벨업 연출
            PlayLevelUpEffect();

            // 레벨 텍스트 펀치
            if (_levelText != null)
            {
                _levelText.transform.DOKill();
                _levelText.transform.localScale = Vector3.one;
                _levelText.transform.DOPunchScale(Vector3.one * 0.4f, 0.3f, 6, 0.5f)
                    .SetEase(Ease.OutBack).SetUpdate(true).SetLink(gameObject);
            }
        }

        private void OnCurrencyChanged(CurrencyChangedEvent evt)
        {
            if (evt.Type == CurrencyType.Gold)
            {
                BigNumber prev = _currentGold;
                _currentGold = evt.CurrentAmount;
                UpdateGoldText();

                // 골드 획득 피드백: 증가 시 펀치 + 색상 플래시
                if (_goldText != null && evt.CurrentAmount > prev)
                {
                    _goldText.transform.DOKill();
                    _goldText.transform.localScale = Vector3.one;
                    _goldText.transform.DOPunchScale(Vector3.one * 0.25f, 0.3f, 6, 0.5f)
                        .SetUpdate(true).SetLink(gameObject);
                    ColorTweenHelper.To(_goldText,
                        new Color(1f, 1f, 0.5f, 1f), 0.1f)
                        .SetUpdate(true)
                        .OnComplete(() =>
                            ColorTweenHelper.To(_goldText,
                                new Color(1f, 0.85f, 0.086f, 1f), 0.25f).SetUpdate(true));
                }
            }
            else if (evt.Type == CurrencyType.Ruby)
            {
                BigNumber prev = _currentRuby;
                _currentRuby = evt.CurrentAmount;
                UpdateRubyText();

                if (_rubyText != null && evt.CurrentAmount > prev && !_isFlashingRuby)
                {
                    _rubyText.transform.DOKill();
                    _rubyText.transform.localScale = Vector3.one;
                    _rubyText.transform.DOPunchScale(Vector3.one * 0.25f, 0.3f, 6, 0.5f)
                        .SetUpdate(true).SetLink(gameObject);
                }
            }
        }

        private void OnExpGained(ExpGainedEvent evt)
        {
            if (_levelSystem != null)
                UpdateExpBar(_levelSystem.ExpRatio);
        }

        private void OnMonsterDied(MonsterDiedEvent evt)
        {
            _totalKills++;
            UpdateKillCount();

            // 킬카운트 펀치 애니메이션
            if (_killCountText != null)
            {
                _killCountText.transform.DOKill();
                _killCountText.transform.localScale = Vector3.one;
                _killCountText.transform.DOPunchScale(Vector3.one * 0.2f, 0.2f, 8, 0.4f)
                    .SetUpdate(true).SetLink(gameObject);
            }
        }

        private void OnFarmingModeChanged(FarmingModeEvent evt)
        {
            _isFarmingMode = evt.IsActive;
            bool showRetry = evt.IsActive && evt.IsPlayerDead;

            if (_retryButtonObj != null)
            {
                _retryButtonObj.SetActive(showRetry);

                // 재도전 버튼이 탭 바를 가리지 않도록 raycast 영역 제한
                if (showRetry)
                {
                    var images = _retryButtonObj.GetComponentsInChildren<Image>(true);
                    for (int i = 0; i < images.Length; i++)
                    {
                        // 버튼 자체의 Image만 raycast 허용, 배경은 차단하지 않음
                        var btn = images[i].GetComponent<Button>();
                        images[i].raycastTarget = btn != null;
                    }
                }
            }

            if (evt.IsActive && evt.IsPlayerDead)
            {
                // 사망 상태: HP 0 표시
                if (_hpSlider != null)
                    _hpSlider.value = 0f;
                if (_hpText != null)
                    _hpText.text = "사망 — 재도전";

                // 사망 오버레이 표시
                ShowDeathOverlay();
            }
            else
            {
                // 사망 오버레이 숨기기
                HideDeathOverlay();
                RefreshHp();
            }
        }

        private void OnHpChanged(int current, int max)
        {
            if (_isFarmingMode)
            {
                // 파밍 모드(사망 대기)에서는 HP 변경 무시 — 재도전 시 RefreshHp로 복원
                return;
            }

            if (max <= 0) return;
            float target = (float)current / max;

            if (_hpSlider != null)
            {
                _hpTween?.Kill();
                _hpTween = DOTween.To(
                    () => _hpSlider.value,
                    x => _hpSlider.value = x,
                    target, 0.35f).SetEase(Ease.OutCubic).SetUpdate(true);
            }
            if (_hpText != null)
                _hpText.text = $"{current}/{max}";
        }

        private void OnSkillLearned(SkillLearnedEvent evt)
        {
            // 모든 스킬 타입에 대해 알림 표시
            ShowSkillLearnedNotification(evt.SkillName, evt.SkillType);

            if (_skillSlots == null) return;

            // 스킬 타입 → 슬롯 인덱스 매핑
            int slotIndex = GetSlotIndex(evt.SkillType);
            if (slotIndex < 0 || slotIndex >= _skillSlots.Length) return;

            if (_skillSystem == null) return;

            // 학습된 스킬 데이터 찾기
            SkillDataSO skillData = null;
            foreach (var skill in _skillSystem.LearnedSkills)
            {
                if (skill.id == evt.SkillId)
                {
                    skillData = skill;
                    break;
                }
            }
            if (skillData == null) return;

            // 해당 슬롯에 스킬 할당 (전직 시 교체됨)
            _skillSlots[slotIndex].AssignSkill(skillData);
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

        private void SyncLearnedSkills()
        {
            if (_skillSlots == null || _skillSystem == null) return;

            var allSkills = _skillSystem.AllSkills;
            if (allSkills == null) return;

            // 학습된 스킬 ID 수집
            var learnedIds = new System.Collections.Generic.HashSet<string>();
            foreach (var skill in _skillSystem.LearnedSkills)
                learnedIds.Add(skill.id);

            // 각 타입별로 가장 최근(마지막) 학습된 스킬 + 다음 미해금 스킬 찾기
            // 슬롯: Active(0), Passive(1), Buff(2), Awakening(3)
            SkillDataSO[] latestLearned = new SkillDataSO[4];
            SkillDataSO[] nextPreview = new SkillDataSO[4];

            foreach (var skill in allSkills)
            {
                if (skill == null) continue;
                int idx = GetSlotIndex(skill.skillType);
                if (idx < 0 || idx >= _skillSlots.Length) continue;

                if (learnedIds.Contains(skill.id))
                {
                    // 마지막 학습된 스킬로 갱신 (allSkills가 레벨순이므로 나중 것이 덮어씀)
                    latestLearned[idx] = skill;
                }
                else if (nextPreview[idx] == null)
                {
                    // 아직 미해금인 첫 번째 스킬 = 다음 미리보기
                    nextPreview[idx] = skill;
                }
            }

            // 슬롯에 적용
            for (int i = 0; i < _skillSlots.Length && i < 4; i++)
            {
                if (_skillSlots[i] == null) continue;

                if (latestLearned[i] != null)
                    _skillSlots[i].AssignSkill(latestLearned[i]);
                else if (nextPreview[i] != null)
                    _skillSlots[i].ShowLockedPreview(nextPreview[i]);
                else
                    _skillSlots[i].ShowLocked();
            }
        }

        private void UpdateSkillSlots()
        {
            if (_skillSlots == null || _skillSystem == null) return;

            for (int i = 0; i < _skillSlots.Length; i++)
            {
                var slot = _skillSlots[i];
                if (slot == null || !slot.IsAssigned || !slot.IsUnlocked) continue;

                // Active(0), Passive(1)는 쿨타임/버프 없음
                if (i <= 1) continue;

                string id = slot.SkillId;

                // 쿨타임 갱신
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
                slot.SetCooldown(cdRemaining, cdTotal);

                // 버프 지속시간 갱신
                bool isActive = _skillSystem.IsBuffActive(id);
                float buffRemaining = _skillSystem.GetBuffRemaining(id);
                float buffTotal = 0f;
                if (isActive)
                {
                    foreach (var skill in _skillSystem.LearnedSkills)
                    {
                        if (skill.id == id)
                        {
                            buffTotal = skill.duration;
                            break;
                        }
                    }
                }
                slot.SetBuffActive(isActive, buffRemaining, buffTotal);
            }
        }

        // ── 내부 갱신 ──

        private void UpdateStageDisplay()
        {
            if (_stageNameText != null)
                _stageNameText.text = _currentStageName;
            if (_stageProgressText != null)
            {
                int maxStage = _currentStageIndex >= 10 ? _currentStageIndex : 10;
                _stageProgressText.text = $"스테이지 {_currentStageIndex}/{maxStage}";
            }
        }

        private void UpdateLevelText()
        {
            if (_levelText != null)
                _levelText.text = $"Lv.{_currentLevel}";
        }

        private void UpdateGoldText()
        {
            if (_goldText != null)
                _goldText.text = FormatGold(_currentGold);
        }

        private void UpdateRubyText()
        {
            if (_isFlashingRuby) return; // 플래시 중에는 덮어쓰지 않음
            if (_rubyText != null)
                _rubyText.text = FormatGold(_currentRuby);
        }

        /// <summary>
        /// 루비 부족 시 HUD의 루비 텍스트를 빨간색으로 깜빡인다.
        /// ShopPanel 등 외부에서 호출한다.
        /// </summary>
        public void FlashRubyInsufficient()
        {
            if (_rubyText == null) return;
            if (_isFlashingRuby) return;
            _isFlashingRuby = true;

            Color origColor = _rubyText.color;

            // 즉시 동기적으로 변경 (async 없음, WebGL 안전)
            _rubyText.text = "루비 부족!";
            _rubyText.color = new Color(1f, 0.15f, 0.15f, 1f);
            _rubyText.fontSize = _rubyText.fontSize * 1.3f;

            Debug.Log($"[HudPanel] FlashRubyInsufficient 실행 — text을 '루비 부족!'으로 변경 완료");

            // transform 펀치 (WebGL에서 확인된 안전한 방식)
            _rubyText.transform.DOKill();
            _rubyText.transform.localScale = Vector3.one;
            _rubyText.transform.DOPunchScale(Vector3.one * 0.3f, 0.5f, 8, 0.5f)
                .SetUpdate(true)
                .SetLink(_rubyText.gameObject);

            // 2초 후 복원 (DOTween 타이머 — async/UniTask 없음)
            float restoreSize = _rubyText.fontSize / 1.3f;
            DOTween.Sequence()
                .AppendInterval(2f)
                .OnComplete(() =>
                {
                    if (_rubyText != null)
                    {
                        _rubyText.color = origColor;
                        _rubyText.fontSize = restoreSize;
                        UpdateRubyText();
                    }
                    _isFlashingRuby = false;
                })
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private bool _isFlashingRuby;

        private void UpdateExpBar(float ratio, bool instant = false)
        {
            float clamped = Mathf.Clamp01(ratio);
            long currentExp = _levelSystem != null ? _levelSystem.CurrentExp : 0;
            long requiredExp = _levelSystem != null ? _levelSystem.RequiredExp : 1;

            _expTween?.Kill();
            _expCountTween?.Kill();
            _expLevelUpSeq?.Kill();

            if (_expSlider == null)
            {
                UpdateExpText(currentExp, requiredExp);
                return;
            }

            if (instant)
            {
                _expSlider.value = clamped;
                UpdateExpText(currentExp, requiredExp);
                return;
            }

            float prevFill = _expSlider.value;
            bool isLevelUp = clamped < prevFill - 0.1f && prevFill > 0.3f;

            if (isLevelUp)
            {
                // 레벨업: fill → 1.0 → snap 0 → target
                _expLevelUpSeq = DOTween.Sequence()
                    .Append(DOTween.To(() => _expSlider.value, x => _expSlider.value = x, 1f, 0.25f)
                        .SetEase(Ease.OutCubic))
                    .AppendCallback(() => _expSlider.value = 0f)
                    .Append(DOTween.To(() => _expSlider.value, x => _expSlider.value = x, clamped, 0.3f)
                        .SetEase(Ease.OutCubic))
                    .SetUpdate(true)
                    .SetLink(gameObject);

                // 숫자 카운트업: 레벨업 후 새 경험치 표시
                UpdateExpText(currentExp, requiredExp);
            }
            else
            {
                // 일반 경험치 획득: 부드러운 fill + 숫자 카운트업
                _expTween = DOTween.To(() => _expSlider.value, x => _expSlider.value = x, clamped, 0.4f)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true)
                    .SetLink(gameObject);

                // 숫자 카운트업
                long displayStart = (long)(prevFill * requiredExp);
                double displayExp = displayStart;
                _expCountTween = DOTween.To(
                    () => displayExp,
                    x => {
                        displayExp = x;
                        if (_expText != null)
                            _expText.text = $"{NumberFormatter.FormatKorean((long)x)} / {NumberFormatter.FormatKorean(requiredExp)}";
                    },
                    (double)currentExp, 0.4f)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true)
                    .SetLink(gameObject);
                return;
            }
        }

        private void UpdateExpText(long currentExp, long requiredExp)
        {
            if (_expText != null)
                _expText.text = $"{NumberFormatter.FormatKorean(currentExp)} / {NumberFormatter.FormatKorean(requiredExp)}";
        }

        private void UpdateKillCount()
        {
            if (_killCountText != null)
                _killCountText.text = $"x {_totalKills}";
        }

        private void RefreshCP()
        {
            if (_cpText == null) return;

            // CP 텍스트가 잘리지 않도록 auto-sizing 활성화 (최초 1회)
            if (!_cpText.enableAutoSizing)
            {
                _cpText.enableAutoSizing = true;
                _cpText.fontSizeMin = 12;
                _cpText.fontSizeMax = _cpText.fontSize > 0 ? _cpText.fontSize : 18;
                // overflow 모드를 Overflow로 설정하여 영역 밖에서도 표시
                _cpText.overflowMode = TextOverflowModes.Overflow;
            }

            if (_playerStats != null)
            {
                int cp = CombatFormula.CalculateCP(
                    _playerStats.Atk, _playerStats.Def,
                    _playerStats.MaxHp, _playerStats.CritRate);
                _cpText.text = $"전투력 {NumberFormatter.FormatKorean(cp)}";
            }
            else
            {
                _cpText.text = "전투력 ---";
            }
        }

        private void RefreshHp()
        {
            if (_hpSlider == null || _playerStats == null) return;
            int max = _playerStats.MaxHp;
            if (max > 0)
            {
                // 초기 갱신은 즉시
                _hpTween?.Kill();
                _hpSlider.value = (float)_playerStats.CurrentHp / max;
                if (_hpText != null)
                    _hpText.text = $"{_playerStats.CurrentHp}/{max}";
            }
        }

        /// <summary>
        /// 재도전 버튼에서 호출. StageManager.RetryStage()를 실행한다.
        /// </summary>
        public void OnRetryButtonClicked()
        {
            var stageManager = FindFirstObjectByType<StageManager>();
            if (stageManager != null)
                stageManager.RetryStage();
        }

        private void WireRetryButton()
        {
            if (_retryButtonObj == null) return;
            var btn = _retryButtonObj.GetComponent<Button>();
            if (btn == null) return;

            // persistent listener가 없을 수 있으므로 런타임 리스너 추가
            btn.onClick.RemoveListener(OnRetryButtonClicked);
            btn.onClick.AddListener(OnRetryButtonClicked);
        }

        // ── 사망 오버레이 ──

        /// <summary>
        /// 사망 시 화면을 어둡게 하는 오버레이 + 텍스트 연출을 표시한다.
        /// </summary>
        private void ShowDeathOverlay()
        {
            if (_deathOverlay == null)
                CreateDeathOverlay();

            if (_deathOverlay == null) return;

            _deathOverlay.SetActive(true);

            var cg = _deathOverlay.GetComponent<CanvasGroup>();
            if (cg == null) cg = _deathOverlay.AddComponent<CanvasGroup>();

            // 페이드인 연출 — DOTween.To 제네릭 사용 (DOTweenModuleUI 금지)
            cg.alpha = 0f;
            _deathOverlayTween?.Kill();
            _deathOverlayTween = DOTween.To(() => cg.alpha, x => cg.alpha = x, 1f, 0.5f)
                .SetEase(Ease.InCubic)
                .SetUpdate(true)
                .SetLink(_deathOverlay);

            // "사망" 텍스트 스케일 연출
            var deathText = _deathOverlay.transform.Find("DeathText");
            if (deathText != null)
            {
                deathText.localScale = Vector3.one * 0.3f;
                deathText.DOScale(Vector3.one, 0.5f)
                    .SetEase(Ease.OutBack)
                    .SetDelay(0.3f)
                    .SetUpdate(true)
                    .SetLink(deathText.gameObject);
            }
        }

        /// <summary>
        /// 외부에서 패널이 열릴 때 사망 오버레이를 임시로 숨긴다.
        /// 패널이 닫히면 SetDeathOverlayVisible(true)로 복원한다.
        /// </summary>
        public void SetDeathOverlayVisible(bool visible)
        {
            if (_deathOverlay == null || !_deathOverlay.activeSelf && !visible) return;

            // 사망 상태가 아니면 숨기기만 해야 하므로 무시
            if (visible && !_isFarmingMode) return;

            if (visible)
            {
                // 패널 닫힘 → 사망 오버레이 복원 (사망 상태일 때만)
                _deathOverlay.SetActive(true);
                var cg = _deathOverlay.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
            }
            else
            {
                // 패널 열림 → 사망 오버레이 임시 숨김
                _deathOverlay.SetActive(false);
            }
        }

        /// <summary>
        /// 사망 오버레이를 숨긴다.
        /// </summary>
        private void HideDeathOverlay()
        {
            if (_deathOverlay == null) return;

            _deathOverlayTween?.Kill();

            var cg = _deathOverlay.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                _deathOverlayTween = DOTween.To(() => cg.alpha, x => cg.alpha = x, 0f, 0.3f)
                    .SetUpdate(true)
                    .SetLink(_deathOverlay)
                    .OnComplete(() =>
                    {
                        if (_deathOverlay != null)
                            _deathOverlay.SetActive(false);
                    });
            }
            else
            {
                _deathOverlay.SetActive(false);
            }
        }

        /// <summary>
        /// 사망 오버레이 UI를 런타임에 생성한다.
        /// 반투명 검정 배경 + "사망" 대형 텍스트 + "재도전" 안내 텍스트.
        /// </summary>
        private void CreateDeathOverlay()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _deathOverlay = new GameObject("DeathOverlay");
            _deathOverlay.transform.SetParent(canvas.transform, false);

            // 전체 화면 커버
            var rt = _deathOverlay.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            // 반투명 검정 배경
            var bg = _deathOverlay.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.65f);
            bg.raycastTarget = false; // 재도전 버튼 클릭을 막지 않도록

            var cg = _deathOverlay.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false; // 하위 UI 클릭 허용

            // Canvas 추가 — sortingOrder를 팝업(100)보다 충분히 낮게 설정하여
            // 상점/던전/캐릭터 패널을 열었을 때 사망 오버레이가 가려지도록 한다
            var overlayCanvas = _deathOverlay.AddComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 5; // 일반 전투 위, 팝업(100)/탭바(150)보다 아래

            // "사망" 텍스트
            var deathTextObj = new GameObject("DeathText");
            deathTextObj.transform.SetParent(_deathOverlay.transform, false);
            var dRt = deathTextObj.AddComponent<RectTransform>();
            dRt.anchorMin = new Vector2(0.5f, 0.55f);
            dRt.anchorMax = new Vector2(0.5f, 0.55f);
            dRt.anchoredPosition = Vector2.zero;
            dRt.sizeDelta = new Vector2(500, 120);

            var deathTmp = deathTextObj.AddComponent<TextMeshProUGUI>();
            deathTmp.text = "사  망";
            deathTmp.fontSize = 72;
            deathTmp.fontStyle = FontStyles.Bold;
            deathTmp.alignment = TextAlignmentOptions.Center;
            deathTmp.color = new Color(0.9f, 0.15f, 0.15f, 1f);
            deathTmp.enableVertexGradient = true;
            deathTmp.colorGradient = new VertexGradient(
                new Color(1f, 0.3f, 0.3f), new Color(1f, 0.3f, 0.3f),
                new Color(0.6f, 0.05f, 0.05f), new Color(0.6f, 0.05f, 0.05f));

            // "화면의 재도전 버튼을 눌러주세요" 안내
            var hintObj = new GameObject("HintText");
            hintObj.transform.SetParent(_deathOverlay.transform, false);
            var hRt = hintObj.AddComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0.5f, 0.42f);
            hRt.anchorMax = new Vector2(0.5f, 0.42f);
            hRt.anchoredPosition = Vector2.zero;
            hRt.sizeDelta = new Vector2(400, 50);

            var hintTmp = hintObj.AddComponent<TextMeshProUGUI>();
            hintTmp.text = "재도전 버튼을 눌러주세요";
            hintTmp.fontSize = 24;
            hintTmp.alignment = TextAlignmentOptions.Center;
            hintTmp.color = new Color(0.85f, 0.85f, 0.85f, 0.8f);

            _deathOverlay.SetActive(false);
        }

        private void ToggleMiscPanel()
        {
            // 런타임 자동 탐색: _miscPanelObj가 와이어되지 않은 경우
            if (_miscPanelObj == null)
            {
                var miscPanel = FindFirstObjectByType<MiscPanel>(FindObjectsInactive.Include);
                if (miscPanel != null)
                    _miscPanelObj = miscPanel.gameObject;
            }
            if (_miscPanelObj == null) return;

            // 버튼 펀치 피드백
            if (_settingsButton != null)
            {
                _settingsButton.transform.DOKill();
                _settingsButton.transform.localScale = Vector3.one;
                _settingsButton.transform.DOPunchScale(Vector3.one * 0.2f, 0.25f, 6, 0.5f)
                    .SetUpdate(true).SetLink(gameObject);
            }

            bool isActive = _miscPanelObj.activeSelf;
            if (isActive)
            {
                // MiscPanel은 BasePopup이므로 Hide 사용
                var miscPanel = _miscPanelObj.GetComponent<MiscPanel>();
                if (miscPanel != null)
                    miscPanel.Hide();
                else
                    _miscPanelObj.SetActive(false);
            }
            else
            {
                var miscPanel = _miscPanelObj.GetComponent<MiscPanel>();
                if (miscPanel != null)
                    miscPanel.Show();
                else
                    _miscPanelObj.SetActive(true);
            }
        }

        private void PlayLevelUpEffect()
        {
            // 팝업/패널이 열려있으면 레벨업 UI 연출 억제 (팝업 위에 렌더링되는 문제 방지)
            if (UIState.IsAnyPanelOpen) return;

            // Canvas 찾기
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            // 임시 오브젝트 생성
            var obj = new GameObject("LevelUpEffect");
            obj.transform.SetParent(canvas.transform, false);

            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(600, 120);

            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = $"레벨 업!";
            tmp.fontSize = 64;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.85f, 0.1f, 1f); // 골드색
            tmp.enableVertexGradient = true;
            tmp.colorGradient = new VertexGradient(
                new Color(1f, 0.95f, 0.4f), new Color(1f, 0.95f, 0.4f),
                new Color(1f, 0.65f, 0.1f), new Color(1f, 0.65f, 0.1f));

            // 연출: 작게→크게 + 위로 떠오르며 사라짐
            obj.transform.localScale = Vector3.one * 0.3f;
            var cg = obj.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            var seq = DOTween.Sequence()
                // 등장: 스케일업 + 페이드인
                .Append(DOTween.To(() => obj.transform.localScale,
                    v => obj.transform.localScale = v,
                    Vector3.one * 1.1f, 0.3f).SetEase(Ease.OutBack))
                .Join(DOTween.To(() => cg.alpha, a => cg.alpha = a, 1f, 0.15f))
                // 유지
                .AppendInterval(0.6f)
                // 퇴장: 위로 떠오르며 페이드아웃
                .Append(DOTween.To(() => rt.anchoredPosition,
                    v => rt.anchoredPosition = v,
                    new Vector2(0, 80f), 0.4f).SetEase(Ease.InCubic))
                .Join(DOTween.To(() => cg.alpha, a => cg.alpha = a, 0f, 0.3f))
                .OnComplete(() => Destroy(obj))
                .SetUpdate(true);
        }

        private void ShowSkillLearnedNotification(string skillName, SkillType skillType)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            string typeLabel = skillType switch
            {
                SkillType.Active => "기본공격",
                SkillType.Passive => "패시브",
                SkillType.Buff => "버프",
                SkillType.Awakening => "각성기",
                _ => "스킬"
            };

            var obj = new GameObject("SkillLearnedNotif");
            obj.transform.SetParent(canvas.transform, false);

            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.6f);
            rt.anchorMax = new Vector2(0.5f, 0.6f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(500, 80);

            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = $"[{typeLabel}] {skillName} 습득!";
            tmp.fontSize = 36;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.4f, 1f, 0.6f, 1f);

            obj.transform.localScale = Vector3.one * 0.5f;
            var cg = obj.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            DOTween.Sequence()
                .Append(DOTween.To(() => obj.transform.localScale,
                    v => obj.transform.localScale = v,
                    Vector3.one, 0.25f).SetEase(Ease.OutBack))
                .Join(DOTween.To(() => cg.alpha, a => cg.alpha = a, 1f, 0.15f))
                .AppendInterval(1f)
                .Append(DOTween.To(() => rt.anchoredPosition,
                    v => rt.anchoredPosition = v,
                    new Vector2(0, 60f), 0.35f).SetEase(Ease.InCubic))
                .Join(DOTween.To(() => cg.alpha, a => cg.alpha = a, 0f, 0.3f))
                .OnComplete(() => Destroy(obj))
                .SetUpdate(true);
        }

        /// <summary>
        /// UIThemeManager의 슬라이더 스프라이트를 HP/EXP 바에 적용하고,
        /// IconRegistry의 재화 아이콘을 골드/루비 텍스트 옆에 표시한다.
        /// </summary>
        private void ApplyThemeSprites()
        {
            var theme = UIThemeManager.Instance;

            // HP_Wrapper, TopSection, BottomBarBackground 등 NULL sprite 요소에 FrameBackground 적용
            if (theme != null && theme.FrameBackground != null)
            {
                var hudRoot = transform.Find("HudRoot");
                if (hudRoot != null)
                {
                    ApplyFrameToChild(hudRoot, "TopSection", theme);
                }
                var canvas = GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    ApplyFrameToChild(canvas.transform, "BottomBarBackground", theme);
                }
            }

            // 슬라이더 스프라이트 — UIThemeManager 또는 직접 Resources.Load fallback
            Sprite fill = theme?.SliderFill;
            Sprite bg = theme?.SliderBackground;
            Sprite handle = null;

            if (fill == null)
                fill = Resources.Load<Sprite>("UI/Stone/Gage/gage_orange");
            if (bg == null)
                bg = Resources.Load<Sprite>("UI/Stone/Gage/gage_bg");

            ApplySliderSprites(_hpSlider, fill, handle, bg, new Color(0.2f, 0.85f, 0.3f));
            Color expColor = theme != null ? theme.Theme.accentBlue : new Color(0.29f, 0.56f, 0.85f);
            ApplySliderSprites(_expSlider, fill, handle, bg, expColor);

            // 설정 버튼에 GUI Kit 아이콘 적용
            if (_settingsButton != null)
            {
                var settingIcon = _settingsButton.GetComponentInChildren<Image>();
                if (settingIcon != null)
                {
                    Sprite icon = IconRegistry.Instance != null
                        ? IconRegistry.Instance.GetUIIcon("setting")
                        : Resources.Load<Sprite>("Icons/Stone/icon_setting");
                    if (icon != null)
                        settingIcon.sprite = icon;
                }

                // 버튼 배경에 테마 스프라이트 적용
                var btnImg = _settingsButton.GetComponent<Image>();
                if (btnImg != null)
                {
                    Sprite btnSprite = theme?.ButtonNormal;
                    if (btnSprite == null)
                        btnSprite = Resources.Load<Sprite>("UI/Stone/Button/btn_normal");
                    UIThemeManager.ApplySpriteOrColor(btnImg, btnSprite, theme != null ? theme.Theme.buttonNormal : new Color(0.33f, 0.20f, 0.51f));
                }
            }

            // 재화 아이콘 적용
            ApplyCurrencyIcon(_goldText, CurrencyType.Gold);
            ApplyCurrencyIcon(_rubyText, CurrencyType.Ruby);
        }

        private void ApplySliderSprites(Slider slider, Sprite fill, Sprite handle, Sprite bg, Color fillColor)
        {
            if (slider == null) return;

            // 배경 스프라이트 (sprite 있으면 Color.white로 원본 표시)
            var bgImg = slider.GetComponentInChildren<Image>();
            if (bgImg != null && bg != null)
            {
                bgImg.sprite = bg;
                bgImg.type = Image.Type.Sliced;
                bgImg.color = Color.white;
            }

            // Fill Area
            if (slider.fillRect != null)
            {
                var fillImg = slider.fillRect.GetComponent<Image>();
                if (fillImg != null)
                {
                    if (fill != null)
                    {
                        fillImg.sprite = fill;
                        fillImg.type = Image.Type.Sliced;
                    }
                    fillImg.color = fillColor;
                }
            }

            // Handle (있으면)
            if (slider.handleRect != null && handle != null)
            {
                var handleImg = slider.handleRect.GetComponent<Image>();
                if (handleImg != null)
                {
                    handleImg.sprite = handle;
                    handleImg.type = Image.Type.Sliced;
                }
            }
        }

        private void ApplyCurrencyIcon(TextMeshProUGUI text, CurrencyType type)
        {
            if (text == null) return;

            Sprite icon = null;
            if (IconRegistry.Instance != null)
                icon = IconRegistry.Instance.GetCurrencyIcon(type);

            if (icon == null) return;

            var parent = text.transform.parent;
            if (parent == null) parent = text.transform;

            // 텍스트 좌측 여백 확보 — 아이콘과 겹치지 않도록 margin 설정
            var textRt = text.GetComponent<RectTransform>();
            float iconSize = 22f;
            float gap = 4f; // 아이콘~텍스트 사이 최소 간격
            float totalOffset = iconSize + gap;

            // 텍스트의 왼쪽 오프셋을 아이콘만큼 확보 (offsetMin.x 조정)
            if (textRt.offsetMin.x < totalOffset)
            {
                textRt.offsetMin = new Vector2(totalOffset, textRt.offsetMin.y);
            }

            // enableAutoSizing으로 큰 숫자도 잘리지 않게 설정
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = text.fontSize > 0 ? text.fontSize : 18f;

            // SceneSetupEditor가 만든 기존 "Icon" 또는 이전 "CurrencyIcon_*" 검색
            string iconName = $"CurrencyIcon_{type}";
            Transform existing = parent.Find(iconName);
            if (existing == null) existing = parent.Find("Icon");

            if (existing != null)
            {
                var existingImg = existing.GetComponent<Image>();
                if (existingImg != null)
                {
                    existingImg.sprite = icon;
                    existingImg.type = Image.Type.Simple;
                    existingImg.preserveAspect = true;
                }
                // 기존 아이콘 위치도 재조정
                var existingRt = existing.GetComponent<RectTransform>();
                if (existingRt != null)
                {
                    existingRt.sizeDelta = new Vector2(iconSize, iconSize);
                    existingRt.anchoredPosition = new Vector2(2f, 0f);
                }
                return;
            }

            // 아이콘이 없을 때만 새로 생성 — parent 좌측에 배치
            var iconGo = new GameObject(iconName, typeof(RectTransform));
            iconGo.transform.SetParent(parent, false);
            var img = iconGo.AddComponent<Image>();
            img.sprite = icon;
            img.raycastTarget = false;
            img.preserveAspect = true;

            var rt = iconGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(iconSize, iconSize);
            // parent 왼쪽 기준 배치
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(2f, 0f);
        }

        // ── 패널 열림/닫힘 시 HUD 요소 토글 ──

        private void OnPanelStateChanged(bool anyPanelOpen)
        {
            SetHudElementsVisible(!anyPanelOpen);
        }

        /// <summary>
        /// 탭 패널이 열릴 때 HUD 요소를 숨기고, 닫힐 때 복원한다.
        /// TopSection, BottomExpBar, SkillSlotBar, BottomBarBackground와
        /// 독립 위젯(CombatFeedHUD, ComboLogHUD, GoalGuideWidget 등)을 제어.
        /// </summary>
        private void SetHudElementsVisible(bool visible)
        {
            // HudRoot 자식 요소 토글 (이름 기반)
            var hudRoot = transform.Find("HudRoot");
            if (hudRoot != null)
            {
                SetChildActive(hudRoot, "TopSection", visible);
                SetChildActive(hudRoot, "BottomExpBar", visible);
                SetChildActive(hudRoot, "SkillSlotBar", visible);
                SetChildActive(hudRoot, "RetryButton", visible && _retryButtonObj != null && _retryButtonObj.activeSelf);
            }

            // BottomBarBackground (HudRoot 밖, Canvas 직속)
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var bbBg = canvas.transform.Find("BottomBarBackground");
                if (bbBg != null) bbBg.gameObject.SetActive(visible);
            }

            // 독립 HUD 위젯들 (FindFirstObjectByType은 비싸므로 캐싱)
            SetWidgetVisible<CombatFeedHUD>(visible);
            SetWidgetVisible<ComboLogHUD>(visible);
            SetWidgetVisible<GoalGuideWidget>(visible);
            // DailyChecklistWidget: 2026-04-20 DailyChecklist 시스템 완전 제거
            SetWidgetVisible<ContextRecommendBanner>(visible);
        }

        private static void ApplyFrameToChild(Transform parent, string childName, UIThemeManager theme)
        {
            var child = parent.Find(childName);
            if (child == null) return;
            var img = child.GetComponent<Image>();
            if (img != null && img.sprite == null)
            {
                img.sprite = theme.FrameBackground;
                img.type = Image.Type.Sliced;
                img.color = new Color(0.2f, 0.18f, 0.16f, 0.95f);
            }
        }

        private static void SetChildActive(Transform parent, string childName, bool active)
        {
            var child = parent.Find(childName);
            if (child != null) child.gameObject.SetActive(active);
        }

        private static void SetWidgetVisible<T>(bool visible) where T : MonoBehaviour
        {
            var widget = FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (widget != null) widget.gameObject.SetActive(visible);
        }

        private void OnDestroy()
        {
            _expTween?.Kill();
            _expCountTween?.Kill();
            _expLevelUpSeq?.Kill();
            _hpTween?.Kill();
            _deathOverlayTween?.Kill();
        }

        /// <summary>
        /// 화면 중앙에 토스트 메시지를 표시한다.
        /// 2026-04-23 UI Toolkit ToastUI로 리다이렉트 (uGUI HudPanel 비활성 씬에서 무음 종료 버그 수정).
        /// 호출 측 인터페이스는 유지 — 내부 구현만 UITK로 교체.
        /// </summary>
        public static void ShowToast(string message)
        {
            ToastUI.Show(message);
        }

        // ── 유틸리티 ──

        public static string FormatGold(long amount)
        {
            return NumberFormatter.FormatKorean(amount);
        }

        /// <summary>BigNumber 버전 (오버플로우 안전).</summary>
        public static string FormatGold(BigNumber amount)
        {
            return NumberFormatter.FormatKorean(amount);
        }
    }
}
