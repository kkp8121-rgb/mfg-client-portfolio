using UnityEngine;
using UnityEngine.UIElements;
using MkLike.Core;
using MkLike.Combat;
using MkLike.Economy;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// UI Toolkit 기반 정예 소환 팝업.
    /// HUD의 엘리트 버튼에서 열리며, 소환 레벨/비용/등급/통계를 표시한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class EliteSummonPopupUI : MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _root;

        // 캐싱된 요소
        private VisualElement _eliteRoot;
        private Label _levelValue;
        private Label _levelMax;
        private Label _gradeValue;
        private VisualElement _progressFill;
        private Label _levelUpText;
        private Label _killsValue;
        private Label _hpValue;
        private Button _summonBtn;
        private Label _summonBtnText;
        private Label _summonCost;
        private Label _cooldownLabel;
        private Button _closeBtn;

        private bool _isVisible;

        // 2026-04-23 이슈 11: 팝업 열린 동안 HP/처치수/레벨업 진행도 실시간 갱신
        // 기존엔 이벤트(OnEliteSummoned/Killed/LevelUp) 발생 시에만 RefreshUI → 팝업을
        // 열어둔 채 전투하며 HP 쌓이는 중에는 수치 변화 안 보였음.
        private float _uiRefreshTimer;
        private const float UI_REFRESH_INTERVAL = 0.25f;

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            _doc.sortingOrder = 100;
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[EliteSummonPopupUI] rootVisualElement == null, UI 초기화 스킵");
                return;
            }
            _root.pickingMode = PickingMode.Ignore;

            CacheElements();
            BindCallbacks();

            EventBus<EliteSummonedEvent>.Subscribe(OnEliteSummoned);
            EventBus<EliteKilledEvent>.Subscribe(OnEliteKilled);
            EventBus<EliteSummonLevelUpEvent>.Subscribe(OnLevelUp);

            SetVisible(false);
        }

        private void OnDisable()
        {
            EventBus<EliteSummonedEvent>.Unsubscribe(OnEliteSummoned);
            EventBus<EliteKilledEvent>.Unsubscribe(OnEliteKilled);
            EventBus<EliteSummonLevelUpEvent>.Unsubscribe(OnLevelUp);
        }

        private void Update()
        {
            if (!_isVisible) return;

            var mgr = EliteSummonManager.Instance;
            if (mgr == null) return;

            // 쿨다운 표시 (매 프레임 부드럽게)
            float cd = mgr.CooldownRemaining;
            if (cd > 0f)
            {
                if (_cooldownLabel != null)
                    _cooldownLabel.text = $"쿨다운: {cd:F1}초";
                if (_summonBtn != null)
                    _summonBtn.SetEnabled(false);
            }
            else
            {
                if (_cooldownLabel != null)
                    _cooldownLabel.text = "";
                if (_summonBtn != null)
                    _summonBtn.SetEnabled(mgr.CanSummon);
            }

            // 2026-04-23 이슈 11: HP/처치수/레벨업 진행도 주기 갱신 (0.25s, unscaled)
            // timeScale 영향 받지 않게 unscaledDeltaTime 사용 (Marathon 가속 환경 호환)
            _uiRefreshTimer -= Time.unscaledDeltaTime;
            if (_uiRefreshTimer <= 0f)
            {
                _uiRefreshTimer = UI_REFRESH_INTERVAL;
                RefreshUI();
            }
        }

        private void CacheElements()
        {
            _eliteRoot = _root.Q<VisualElement>("elite-root");
            _levelValue = _root.Q<Label>("elite-level-value");
            _levelMax = _root.Q<Label>("elite-level-max");
            _gradeValue = _root.Q<Label>("elite-grade-value");
            _progressFill = _root.Q<VisualElement>("elite-progress-fill");
            _levelUpText = _root.Q<Label>("elite-levelup-text");
            _killsValue = _root.Q<Label>("elite-kills-value");
            _hpValue = _root.Q<Label>("elite-hp-value");
            _summonBtn = _root.Q<Button>("elite-summon-btn");
            _summonBtnText = _root.Q<Label>("elite-summon-btn-text");
            _summonCost = _root.Q<Label>("elite-summon-cost");
            _cooldownLabel = _root.Q<Label>("elite-cooldown");
            _closeBtn = _root.Q<Button>("elite-close-btn");
        }

        private void BindCallbacks()
        {
            if (_summonBtn != null)
                _summonBtn.RegisterCallback<ClickEvent>(OnSummonClicked);

            if (_closeBtn != null)
                _closeBtn.RegisterCallback<ClickEvent>(OnCloseClicked);

            // 딤 배경 클릭 시 닫기
            if (_eliteRoot != null)
            {
                _eliteRoot.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.target == _eliteRoot)
                        SetVisible(false);
                });
            }
        }

        // ── 공개 API ──

        /// <summary>팝업을 열고 데이터를 갱신한다.</summary>
        public void Show()
        {
            RefreshUI();
            SetVisible(true);
        }

        /// <summary>팝업을 닫는다.</summary>
        public void Hide()
        {
            SetVisible(false);
        }

        // ── UI 갱신 ──

        private void RefreshUI()
        {
            var mgr = EliteSummonManager.Instance;
            if (mgr == null) return;

            // 소환 레벨
            if (_levelValue != null)
                _levelValue.text = mgr.SummonLevel.ToString();
            if (_levelMax != null)
                _levelMax.text = $"/ {(mgr.NextLevelUpCost >= 0 ? "10" : "MAX")}";

            // 해금 등급
            if (_gradeValue != null)
            {
                _gradeValue.text = GradeDisplayName(mgr.CurrentMaxGrade);
                _gradeValue.style.color = GradeColor(mgr.CurrentMaxGrade);
            }

            // 레벨업 진행도
            int nextCost = mgr.NextLevelUpCost;
            if (nextCost > 0)
            {
                float progress = Mathf.Clamp01((float)mgr.TotalSpent / nextCost);
                if (_progressFill != null)
                    _progressFill.style.width = new StyleLength(new Length(progress * 100f, LengthUnit.Percent));
                if (_levelUpText != null)
                    _levelUpText.text = $"{NumberFormatter.FormatKorean(mgr.TotalSpent)} / {NumberFormatter.FormatKorean(nextCost)}";
            }
            else
            {
                if (_progressFill != null)
                    _progressFill.style.width = new StyleLength(new Length(100f, LengthUnit.Percent));
                if (_levelUpText != null)
                    _levelUpText.text = "MAX";
            }

            // 통계
            if (_killsValue != null)
                _killsValue.text = NumberFormatter.FormatKorean(mgr.KillCount);

            // 보유 포인트
            var cm = CurrencyManager.Instance;
            BigNumber hp = cm != null ? cm.GetAmount(CurrencyType.HuntPoint) : BigNumber.Zero;
            if (_hpValue != null)
                _hpValue.text = NumberFormatter.FormatKorean(hp);

            // 소환 버튼
            if (_summonCost != null)
                _summonCost.text = NumberFormatter.FormatKorean(mgr.CurrentSummonCost);
            if (_summonBtn != null)
                _summonBtn.SetEnabled(mgr.CanSummon);
        }

        // ── 이벤트 핸들러 ──

        private void OnSummonClicked(ClickEvent _)
        {
            var mgr = EliteSummonManager.Instance;
            if (mgr == null) return;

            if (mgr.TrySummon())
            {
                RefreshUI();
                HudPanel.ShowToast("정예 몬스터 소환!");
            }
        }

        private void OnCloseClicked(ClickEvent _)
        {
            SetVisible(false);
        }

        private void OnEliteSummoned(EliteSummonedEvent evt)
        {
            RefreshUI();
        }

        private void OnEliteKilled(EliteKilledEvent evt)
        {
            RefreshUI();
            HudPanel.ShowToast($"정예 처치! {GradeDisplayName(evt.DroppedGrade)} 장비 획득!");
        }

        private void OnLevelUp(EliteSummonLevelUpEvent evt)
        {
            RefreshUI();
            HudPanel.ShowToast($"소환 레벨업! Lv.{evt.NewLevel} ({GradeDisplayName(evt.NewMaxGrade)} 해금)");
        }

        // ── 유틸 ──

        private void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_eliteRoot != null)
                _eliteRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static string GradeDisplayName(string grade)
        {
            return grade switch
            {
                "Normal" => "일반",
                "Rare" => "희귀",
                "Epic" => "에픽",
                "Unique" => "유니크",
                "Legendary" => "전설",
                "Mythic" => "신화",
                _ => grade
            };
        }

        private static StyleColor GradeColor(string grade)
        {
            return grade switch
            {
                "Normal" => new StyleColor(new Color(0.7f, 0.7f, 0.7f)),
                "Rare" => new StyleColor(new Color(0.3f, 0.6f, 1f)),
                "Epic" => new StyleColor(new Color(0.6f, 0.3f, 0.9f)),
                "Unique" => new StyleColor(new Color(1f, 0.85f, 0f)),
                "Legendary" => new StyleColor(new Color(1f, 0.5f, 0.1f)),
                "Mythic" => new StyleColor(new Color(1f, 0.2f, 0.3f)),
                _ => new StyleColor(Color.white)
            };
        }
    }
}
