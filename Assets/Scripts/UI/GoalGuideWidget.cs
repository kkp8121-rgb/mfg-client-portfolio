using Cysharp.Threading.Tasks;
using DG.Tweening;
using MkLike.Combat;
using MkLike.Core;
using MkLike.Data;
using MkLike.Dungeon;
using MkLike.Quest;
using MkLike.Utils;
using MkLike.Core.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MkLike.UI
{
    /// <summary>
    /// "다음 목표" 가이드 위젯. 화면 우측에 플로팅하여 현재 가이드 퀘스트 + 진행도 + 보상 미리보기를 표시.
    /// QuestManager의 가이드 퀘스트 체인과 연동하여 완료 시 축하 연출 + 자동 보상 수령 + 다음 목표 전환.
    /// 가이드 퀘스트가 없을 때는 레거시 목표(스테이지/레벨/탑)를 폴백으로 표시한다.
    /// </summary>
    public class GoalGuideWidget : MonoBehaviour
    {
        [Header("UI 참조")]
        [SerializeField] private RectTransform _widgetRoot;
        [SerializeField] private TMP_Text _goalTitleText;
        [SerializeField] private TMP_Text _goalProgressText;
        [SerializeField] private Image _progressFill;
        [SerializeField] private TMP_Text _rewardPreviewText;
        [SerializeField] private Button _toggleButton;
        [SerializeField] private Image _toggleArrow;
        [SerializeField] private CanvasGroup _contentGroup;

        [Header("보상 수령 버튼")]
        [SerializeField] private Button _claimButton;
        [SerializeField] private TMP_Text _claimButtonText;

        [Header("축하 연출")]
        [SerializeField] private GameObject _celebrationEffect;
        [SerializeField] private TMP_Text _celebrationText;

        [Header("설정")]
        [SerializeField] private float _expandedX;
        [SerializeField] private float _collapsedX = 200f;
        [SerializeField] private float _slideSpeed = 0.25f;

        private bool _isExpanded = true;
        private GoalData _currentGoal;
        private Tween _slideTween;
        private bool _guideQuestReady;

        private void Awake()
        {
            EnsureComponents();
        }

        private void Start()
        {
            if (_toggleButton != null)
                _toggleButton.onClick.AddListener(ToggleExpand);

            if (_claimButton != null)
            {
                _claimButton.onClick.AddListener(OnClaimClicked);
                _claimButton.gameObject.SetActive(false);
            }

            if (_celebrationEffect != null)
                _celebrationEffect.SetActive(false);

            RefreshGoal();
        }

        private void OnEnable()
        {
            EventBus<StageChangedEvent>.Subscribe(OnStageChanged);
            EventBus<LevelUpEvent>.Subscribe(OnLevelUp);
            EventBus<TowerFloorReachedEvent>.Subscribe(OnTowerFloorReached);
            EventBus<QuestCompletedEvent>.Subscribe(OnQuestCompleted);
            EventBus<DungeonCompletedEvent>.Subscribe(OnDungeonCompleted);
            EventBus<GuideQuestActivatedEvent>.Subscribe(OnGuideQuestActivated);
            EventBus<GuideQuestCompletedEvent>.Subscribe(OnGuideQuestCompleted);
            EventBus<CyclingQuestActivatedEvent>.Subscribe(OnCyclingQuestActivated);
            EventBus<CyclingQuestCompletedEvent>.Subscribe(OnCyclingQuestCompleted);
            EventBus<MonsterDiedEvent>.Subscribe(OnMonsterDied);
            EventBus<EquipmentChangedEvent>.Subscribe(OnEquipmentChanged);
            EventBus<GachaResultEvent>.Subscribe(OnGachaResult);
            EventBus<SkillLevelUpEvent>.Subscribe(OnSkillLevelUp);
            UIState.OnPanelStateChanged += OnPanelStateChanged;
        }

        private void OnDisable()
        {
            EventBus<StageChangedEvent>.Unsubscribe(OnStageChanged);
            EventBus<LevelUpEvent>.Unsubscribe(OnLevelUp);
            EventBus<TowerFloorReachedEvent>.Unsubscribe(OnTowerFloorReached);
            EventBus<QuestCompletedEvent>.Unsubscribe(OnQuestCompleted);
            EventBus<DungeonCompletedEvent>.Unsubscribe(OnDungeonCompleted);
            EventBus<GuideQuestActivatedEvent>.Unsubscribe(OnGuideQuestActivated);
            EventBus<GuideQuestCompletedEvent>.Unsubscribe(OnGuideQuestCompleted);
            EventBus<CyclingQuestActivatedEvent>.Unsubscribe(OnCyclingQuestActivated);
            EventBus<CyclingQuestCompletedEvent>.Unsubscribe(OnCyclingQuestCompleted);
            EventBus<MonsterDiedEvent>.Unsubscribe(OnMonsterDied);
            EventBus<EquipmentChangedEvent>.Unsubscribe(OnEquipmentChanged);
            EventBus<GachaResultEvent>.Unsubscribe(OnGachaResult);
            EventBus<SkillLevelUpEvent>.Unsubscribe(OnSkillLevelUp);
            UIState.OnPanelStateChanged -= OnPanelStateChanged;
        }

        /// <summary>패널 열림/닫힘 시 위젯 Canvas 토글 (탭 패널 위 겹침 방지)</summary>
        private void OnPanelStateChanged(bool anyPanelOpen)
        {
            var canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.enabled = !anyPanelOpen;
        }

        private void ToggleExpand()
        {
            _isExpanded = !_isExpanded;

            _slideTween?.Kill();
            float targetX = _isExpanded ? _expandedX : _collapsedX;

            _slideTween = DOTween.To(
                () => _widgetRoot.anchoredPosition.x,
                x => { var p = _widgetRoot.anchoredPosition; p.x = x; _widgetRoot.anchoredPosition = p; },
                targetX, _slideSpeed)
                .SetEase(_isExpanded ? Ease.OutBack : Ease.InBack)
                .SetUpdate(true)
                .SetLink(gameObject);

            if (_toggleArrow != null)
            {
                _toggleArrow.rectTransform.DORotate(
                    new Vector3(0f, 0f, _isExpanded ? 0f : 180f), _slideSpeed)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }

            if (_contentGroup != null)
            {
                DOTween.To(() => _contentGroup.alpha, a => _contentGroup.alpha = a,
                    _isExpanded ? 1f : 0f, _slideSpeed * 0.7f)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }
        }

        /// <summary>
        /// 현재 게임 상태에서 가장 적합한 목표를 결정하고 표시한다.
        /// 가이드 퀘스트 우선, 없으면 레거시 폴백.
        /// </summary>
        public void RefreshGoal()
        {
            var goal = DetermineCurrentGoal();
            if (goal.Title == _currentGoal.Title && goal.Current == _currentGoal.Current)
                return;

            bool isNewGoal = goal.Title != _currentGoal.Title;
            _currentGoal = goal;

            UpdateDisplay(isNewGoal);
            UpdateClaimButton();
        }

        private void UpdateDisplay(bool animate)
        {
            if (_goalTitleText != null)
                _goalTitleText.text = _currentGoal.Title;

            if (_goalProgressText != null)
                _goalProgressText.text = $"{_currentGoal.Current} / {_currentGoal.Target}";

            float ratio = _currentGoal.Target > 0
                ? Mathf.Clamp01((float)_currentGoal.Current / _currentGoal.Target)
                : 0f;

            if (_progressFill != null)
            {
                DOTween.Kill(_progressFill);
                if (animate)
                {
                    _progressFill.fillAmount = 0f;
                    DOTween.To(() => _progressFill.fillAmount, v => _progressFill.fillAmount = v, ratio, 0.4f)
                        .SetEase(Ease.OutQuad)
                        .SetUpdate(true)
                        .SetLink(gameObject);
                }
                else
                {
                    DOTween.To(() => _progressFill.fillAmount, v => _progressFill.fillAmount = v, ratio, 0.25f)
                        .SetUpdate(true)
                        .SetLink(gameObject);
                }
            }

            if (_rewardPreviewText != null)
                _rewardPreviewText.text = _currentGoal.RewardPreview;

            if (animate && _widgetRoot != null)
            {
                _widgetRoot.DOPunchScale(Vector3.one * 0.08f, 0.3f, 4, 0.5f)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }
        }

        private void UpdateClaimButton()
        {
            if (_claimButton == null) return;

            var qm = QuestManager.Instance;
            if (qm == null)
            {
                _claimButton.gameObject.SetActive(false);
                return;
            }

            // 가이드 퀘스트 또는 순환 퀘스트 중 활성인 쪽의 진행도 체크
            QuestProgress progress = qm.GetCurrentGuideProgress();
            if (progress == null)
                progress = qm.GetCurrentCyclingProgress();

            bool canClaim = progress != null && progress.isCompleted && !progress.isRewardClaimed;
            _claimButton.gameObject.SetActive(canClaim);

            if (canClaim && _claimButtonText != null)
                _claimButtonText.text = "보상 수령!";
        }

        private void OnClaimClicked()
        {
            var qm = QuestManager.Instance;
            if (qm == null) return;

            // 가이드 퀘스트 우선, 없으면 순환 퀘스트
            if (qm.ClaimGuideRewardAndAdvance())
            {
                PlayCompletionEffect();
                return;
            }

            if (qm.ClaimCyclingRewardAndAdvance())
            {
                PlayCompletionEffect();
            }
        }

        private void PlayCompletionEffect()
        {
            PlayCompletionAsync().Forget();
        }

        private async UniTaskVoid PlayCompletionAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();

            if (_claimButton != null)
                _claimButton.gameObject.SetActive(false);

            if (_celebrationEffect != null)
            {
                _celebrationEffect.SetActive(true);
                _celebrationEffect.transform.localScale = Vector3.zero;
                _celebrationEffect.transform.DOScale(1f, 0.3f)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }

            if (_celebrationText != null)
            {
                _celebrationText.text = "목표 달성!";
                _celebrationText.transform.localScale = Vector3.zero;
                _celebrationText.transform.DOScale(1.2f, 0.25f)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }

            AudioManager.Instance?.PlaySfx(SfxType.UiReward);

            await UniTask.Delay(1500, cancellationToken: token, ignoreTimeScale: true);

            if (_celebrationEffect != null)
            {
                var cg = _celebrationEffect.GetComponent<CanvasGroup>();
                if (cg == null) cg = _celebrationEffect.AddComponent<CanvasGroup>();
                DOTween.To(() => cg.alpha, a => cg.alpha = a, 0f, 0.3f)
                    .SetUpdate(true)
                    .SetLink(gameObject)
                    .OnComplete(() =>
                    {
                        if (_celebrationEffect != null)
                            _celebrationEffect.SetActive(false);
                    });
            }

            // 다음 목표로 전환
            RefreshGoal();
        }

        // ── 목표 결정 로직 ──

        private GoalData DetermineCurrentGoal()
        {
            // 우선순위 1: 가이드 퀘스트 (QuestManager 연동)
            var guideGoal = GetGuideQuestGoal();
            if (guideGoal.HasValue)
                return guideGoal.Value;

            // 우선순위 2: 순환 퀘스트 (가이드 완료 후)
            var cyclingGoal = GetCyclingQuestGoal();
            if (cyclingGoal.HasValue)
                return cyclingGoal.Value;

            // 폴백: 레거시 목표 (스테이지 보스 > 레벨 마일스톤 > 탑 층)
            return GetFallbackGoal();
        }

        private GoalData? GetGuideQuestGoal()
        {
            var qm = QuestManager.Instance;
            if (qm == null) return null;

            QuestDataSO data = qm.GetCurrentGuideData();
            if (data == null) return null;

            QuestProgress progress = qm.GetCurrentGuideProgress();
            if (progress == null) return null;

            string rewardStr = FormatReward(data.rewardType, data.rewardAmount);
            if (data.bonusRewardAmount > 0)
                rewardStr += $" + {FormatReward(data.bonusRewardType, data.bonusRewardAmount)}";

            _guideQuestReady = true;

            return new GoalData
            {
                Title = data.displayName,
                Current = progress.currentAmount,
                Target = data.requiredAmount,
                RewardPreview = rewardStr
            };
        }

        private GoalData? GetCyclingQuestGoal()
        {
            var qm = QuestManager.Instance;
            if (qm == null || !qm.IsCyclingActive) return null;

            QuestDataSO data = qm.GetCurrentCyclingData();
            if (data == null) return null;

            QuestProgress progress = qm.GetCurrentCyclingProgress();
            if (progress == null) return null;

            string rewardStr = FormatReward(data.rewardType, data.rewardAmount);
            _guideQuestReady = true;

            return new GoalData
            {
                Title = $"[{qm.CyclingRound}회차] {data.displayName}",
                Current = progress.currentAmount,
                Target = data.requiredAmount,
                RewardPreview = rewardStr
            };
        }

        private GoalData GetFallbackGoal()
        {
            _guideQuestReady = false;

            // 1. 스테이지 보스 클리어
            var stageManager = UnityEngine.Object.FindFirstObjectByType<StageManager>();
            if (stageManager != null)
            {
                int chapter = stageManager.CurrentChapter;
                int stage = stageManager.CurrentStageIndex;
                int bossStage = 10; // StagesPerChapter 기본값

                if (stage < bossStage)
                {
                    return new GoalData
                    {
                        Title = $"{chapter}챕터 보스 클리어",
                        Current = stage,
                        Target = bossStage,
                        RewardPreview = "다음 챕터 해금"
                    };
                }
            }

            // 2. 레벨 마일스톤 (10단위)
            var levelSystem = Object.FindFirstObjectByType<LevelSystem>();
            if (levelSystem != null)
            {
                int level = levelSystem.CurrentLevel;
                int nextMilestone = ((level / 10) + 1) * 10;
                return new GoalData
                {
                    Title = $"Lv.{nextMilestone} 도달",
                    Current = level,
                    Target = nextMilestone,
                    RewardPreview = nextMilestone switch
                    {
                        10 => "1차 전직 해금",
                        30 => "2차 전직 해금",
                        60 => "3차 전직 해금",
                        _ => $"스탯 포인트 +{(nextMilestone - level) * 5}"
                    }
                };
            }

            // 3. 탑 층 (SaveData 기반)
            {
                int floor = SaveManager.Instance?.CurrentData?.progress?.currentFloor ?? 1;
                int nextTen = ((floor / 10) + 1) * 10;
                return new GoalData
                {
                    Title = $"탑 {nextTen}층 돌파",
                    Current = floor,
                    Target = nextTen,
                    RewardPreview = "마일스톤 보상"
                };
            }

            return new GoalData
            {
                Title = "모험을 시작하세요",
                Current = 0,
                Target = 1,
                RewardPreview = ""
            };
        }

        private static string FormatReward(CurrencyType type, int amount)
        {
            string typeName = type switch
            {
                CurrencyType.Gold => "골드",
                CurrencyType.Ruby => "루비",
                CurrencyType.RuneFragment => "룬 조각",
                CurrencyType.StarCrystal => "별의 결정",
                CurrencyType.ClimbToken => "등반의 증표",
                CurrencyType.WeaponTicket => "무기 소환권",
                CurrencyType.BlueDiamond => "다이아",
                CurrencyType.QuickHuntTicket => "소탕권",
                _ => type.ToString()
            };
            return $"{typeName} x{amount:N0}";
        }

        // ── 이벤트 핸들러 ──

        private void OnStageChanged(StageChangedEvent evt)
        {
            bool wasComplete = _currentGoal.Current >= _currentGoal.Target;
            RefreshGoal();

            if (!wasComplete && _currentGoal.Current >= _currentGoal.Target && !_guideQuestReady)
                PlayCompletionEffect();
        }

        private void OnLevelUp(LevelUpEvent evt)
        {
            RefreshGoal();

            if (!_guideQuestReady && evt.CurrentLevel % 10 == 0)
                PlayCompletionEffect();
        }

        private void OnTowerFloorReached(TowerFloorReachedEvent evt)
        {
            RefreshGoal();
            if (!_guideQuestReady && evt.Floor % 10 == 0)
                PlayCompletionEffect();
        }

        private void OnQuestCompleted(QuestCompletedEvent evt)
        {
            RefreshGoal();
            UpdateClaimButton();
        }

        private void OnDungeonCompleted(DungeonCompletedEvent evt)
        {
            RefreshGoal();
        }

        private void OnGuideQuestActivated(GuideQuestActivatedEvent evt)
        {
            RefreshGoal();
        }

        private void OnGuideQuestCompleted(GuideQuestCompletedEvent evt)
        {
            RefreshGoal();
        }

        private void OnCyclingQuestActivated(CyclingQuestActivatedEvent evt)
        {
            RefreshGoal();
        }

        private void OnCyclingQuestCompleted(CyclingQuestCompletedEvent evt)
        {
            RefreshGoal();
        }

        private void OnMonsterDied(MonsterDiedEvent evt)
        {
            // 몬스터 처치 퀘스트 진행도 업데이트
            if (_guideQuestReady) RefreshGoal();
        }

        private void OnEquipmentChanged(EquipmentChangedEvent evt)
        {
            if (_guideQuestReady) RefreshGoal();
        }

        private void OnGachaResult(GachaResultEvent evt)
        {
            if (_guideQuestReady) RefreshGoal();
        }

        private void OnSkillLevelUp(SkillLevelUpEvent evt)
        {
            if (_guideQuestReady) RefreshGoal();
        }

        private void EnsureComponents()
        {
            if (GetComponentInParent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5;
                gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
                gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            if (_widgetRoot == null)
                _widgetRoot = transform as RectTransform ?? gameObject.AddComponent<RectTransform>();

            // 배경 패널 (반투명 다크)
            if (GetComponent<Image>() == null)
            {
                var bg = gameObject.AddComponent<Image>();
                bg.color = new Color(0.08f, 0.08f, 0.15f, 0.85f);
                bg.raycastTarget = true;
            }

            if (_contentGroup == null)
                _contentGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            // 토글 버튼 (좌측 탭 형태)
            if (_toggleButton == null)
            {
                var go = CreateUIChild("ToggleButton");
                _toggleButton = go.AddComponent<Button>();
                var img = go.AddComponent<Image>();
                img.color = new Color(0.15f, 0.15f, 0.25f, 0.9f);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0.3f);
                rt.anchorMax = new Vector2(0f, 0.7f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(24f, 0f);
            }

            if (_toggleArrow == null)
            {
                var go = CreateUIChild("ToggleArrow");
                _toggleArrow = go.AddComponent<Image>();
                _toggleArrow.color = new Color(0f, 0f, 0f, 0f); // 투명 — 텍스트로 대체
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(_toggleButton.transform, false);
                rt.anchorMin = new Vector2(0.1f, 0.1f);
                rt.anchorMax = new Vector2(0.9f, 0.9f);

                // 텍스트 화살표
                var arrowTextGo = new GameObject("ArrowText", typeof(RectTransform));
                arrowTextGo.transform.SetParent(go.transform, false);
                var arrowTmp = arrowTextGo.AddComponent<TextMeshProUGUI>();
                arrowTmp.text = "\u25B6"; // ▶
                arrowTmp.fontSize = 12f;
                arrowTmp.alignment = TextAlignmentOptions.Center;
                arrowTmp.color = new Color(0.7f, 0.7f, 0.8f);
                arrowTmp.raycastTarget = false;
                var arrowRt = arrowTextGo.GetComponent<RectTransform>();
                arrowRt.anchorMin = Vector2.zero;
                arrowRt.anchorMax = Vector2.one;
                arrowRt.offsetMin = Vector2.zero;
                arrowRt.offsetMax = Vector2.zero;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            // 제목: "다음 목표" 라벨 (상단)
            if (_goalTitleText == null)
            {
                var go = CreateUIChild("GoalTitleText");
                _goalTitleText = go.AddComponent<TextMeshProUGUI>();
                _goalTitleText.fontSize = 16f;
                _goalTitleText.fontStyle = FontStyles.Bold;
                _goalTitleText.alignment = TextAlignmentOptions.Left;
                _goalTitleText.color = Color.white;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -4f);
                rt.sizeDelta = new Vector2(-16f, 22f);
            }

            // 진행도 텍스트 (중앙)
            if (_goalProgressText == null)
            {
                var go = CreateUIChild("GoalProgressText");
                _goalProgressText = go.AddComponent<TextMeshProUGUI>();
                _goalProgressText.fontSize = 13f;
                _goalProgressText.alignment = TextAlignmentOptions.Right;
                _goalProgressText.color = new Color(0.8f, 0.85f, 0.9f);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -28f);
                rt.sizeDelta = new Vector2(-16f, 18f);
            }

            // 프로그레스 바 배경
            var progressBarBg = transform.Find("ProgressBarBg");
            if (progressBarBg == null)
            {
                var bgGo = CreateUIChild("ProgressBarBg");
                var bgImg = bgGo.AddComponent<Image>();
                bgImg.color = new Color(0.15f, 0.15f, 0.22f, 1f);
                var bgRt = bgGo.GetComponent<RectTransform>();
                bgRt.anchorMin = new Vector2(0f, 1f);
                bgRt.anchorMax = new Vector2(1f, 1f);
                bgRt.pivot = new Vector2(0.5f, 1f);
                bgRt.anchoredPosition = new Vector2(0f, -48f);
                bgRt.sizeDelta = new Vector2(-16f, 12f);
                progressBarBg = bgGo.transform;
            }

            // 프로그레스 바 Fill
            if (_progressFill == null)
            {
                var go = CreateUIChild("ProgressFill");
                go.transform.SetParent(progressBarBg, false);
                _progressFill = go.AddComponent<Image>();
                _progressFill.sprite = CreateWhiteSprite();
                _progressFill.color = new Color(0.25f, 0.85f, 0.45f, 1f);
                _progressFill.type = Image.Type.Filled;
                _progressFill.fillMethod = Image.FillMethod.Horizontal;
                _progressFill.fillAmount = 0f;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            // 보상 미리보기 (하단)
            if (_rewardPreviewText == null)
            {
                var go = CreateUIChild("RewardPreviewText");
                _rewardPreviewText = go.AddComponent<TextMeshProUGUI>();
                _rewardPreviewText.fontSize = 12f;
                _rewardPreviewText.alignment = TextAlignmentOptions.Left;
                _rewardPreviewText.color = new Color(1f, 0.85f, 0.1f);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -64f);
                rt.sizeDelta = new Vector2(-16f, 16f);
            }

            // 보상 수령 버튼 (하단)
            if (_claimButton == null)
            {
                var go = CreateUIChild("ClaimButton");
                _claimButton = go.AddComponent<Button>();
                var img = go.AddComponent<Image>();
                img.color = new Color(0.2f, 0.7f, 0.3f, 0.95f);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.15f, 0f);
                rt.anchorMax = new Vector2(0.85f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 6f);
                rt.sizeDelta = new Vector2(0f, 28f);
            }

            if (_claimButtonText == null)
            {
                var go = CreateUIChild("ClaimButtonText");
                go.transform.SetParent(_claimButton.transform, false);
                _claimButtonText = go.AddComponent<TextMeshProUGUI>();
                _claimButtonText.fontSize = 14f;
                _claimButtonText.fontStyle = FontStyles.Bold;
                _claimButtonText.alignment = TextAlignmentOptions.Center;
                _claimButtonText.color = Color.white;
                _claimButtonText.text = "보상 수령!";
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            // 축하 연출
            if (_celebrationEffect == null)
            {
                _celebrationEffect = new GameObject("CelebrationEffect", typeof(RectTransform));
                _celebrationEffect.transform.SetParent(transform, false);
                var ceRt = _celebrationEffect.GetComponent<RectTransform>();
                ceRt.anchorMin = Vector2.zero;
                ceRt.anchorMax = Vector2.one;
                ceRt.offsetMin = Vector2.zero;
                ceRt.offsetMax = Vector2.zero;
                var ceBg = _celebrationEffect.AddComponent<Image>();
                ceBg.color = new Color(1f, 0.85f, 0.1f, 0.15f);
                _celebrationEffect.SetActive(false);
            }

            if (_celebrationText == null)
            {
                var go = CreateUIChild("CelebrationText");
                go.transform.SetParent(_celebrationEffect.transform, false);
                _celebrationText = go.AddComponent<TextMeshProUGUI>();
                _celebrationText.fontSize = 22f;
                _celebrationText.fontStyle = FontStyles.Bold;
                _celebrationText.alignment = TextAlignmentOptions.Center;
                _celebrationText.color = new Color(1f, 0.85f, 0.1f);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            ApplyWidgetTheme();
        }

        private void ApplyWidgetTheme()
        {
            var tm = UIThemeManager.Instance;
            if (tm == null) return;

            var rootBg = GetComponent<Image>();
            if (rootBg != null)
                tm.ApplyFrameBackground(rootBg);

            // 토글 버튼
            if (_toggleButton != null)
                tm.ApplyButtonFull(_toggleButton);

            // 보상 수령 버튼
            if (_claimButton != null)
                tm.ApplyConfirmButton(_claimButton.GetComponent<Image>());
        }

        private GameObject CreateUIChild(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            return go;
        }

        private static Sprite CreateWhiteSprite()
        {
            var tex = Texture2D.whiteTexture;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
        }

        private void OnDestroy()
        {
            _slideTween?.Kill();
            if (_widgetRoot != null) DOTween.Kill(_widgetRoot);
            if (_progressFill != null) DOTween.Kill(_progressFill);
            if (_toggleArrow != null) DOTween.Kill(_toggleArrow.rectTransform);
            if (_celebrationEffect != null) _celebrationEffect.transform.DOKill();
            if (_celebrationText != null) _celebrationText.transform.DOKill();
        }

        private struct GoalData
        {
            public string Title;
            public int Current;
            public int Target;
            public string RewardPreview;
        }
    }
}
