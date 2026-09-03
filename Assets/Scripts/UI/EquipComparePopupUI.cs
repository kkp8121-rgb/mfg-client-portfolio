using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using MkLike.Core;

namespace MkLike.UI
{
    /// <summary>
    /// UI Toolkit 기반 장비 비교 팝업.
    /// EquipmentChangedEvent / WeaponChangedEvent 구독 → 스탯 전후 비교 표시.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class EquipComparePopupUI : MonoBehaviour
    {
        private const int MAX_STAT_ROWS = 4;
        private const float AUTO_CLOSE_DELAY = 4f;

        private UIDocument _doc;
        private VisualElement _root;

        // 캐싱된 요소
        private VisualElement _dimOverlay;
        private Label _titleText;
        private Button _closeBtn;

        // 스탯 행
        private VisualElement[] _statRowElements;
        private Label[] _statNameLabels;
        private Label[] _statBeforeLabels;
        private Label[] _statAfterLabels;
        private Label[] _statDeltaLabels;

        // CP 행
        private Label _cpBeforeText;
        private Label _cpAfterText;
        private Label _cpDeltaText;

        // 자동 닫기
        private float _autoCloseTimer;
        private bool _isAutoClosing;

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            _doc.sortingOrder = 100; // HUD(0)/TabBar(10) 위에 렌더링
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[EquipComparePopupUI] rootVisualElement == null, UI 초기화 스킵");
                return;
            }
            _root.pickingMode = PickingMode.Ignore;

            CacheElements();
            BindCallbacks();

            SetVisible(false);
        }

        private void Update()
        {
            if (!_isAutoClosing) return;

            _autoCloseTimer -= Time.unscaledDeltaTime;
            if (_autoCloseTimer <= 0f)
            {
                _isAutoClosing = false;
                SetVisible(false);
            }
        }

        private void CacheElements()
        {
            _dimOverlay = _root.Q<VisualElement>("dim-overlay");
            _titleText = _root.Q<Label>("title-text");
            _closeBtn = _root.Q<Button>("close-btn");
            _cpBeforeText = _root.Q<Label>("cp-before-text");
            _cpAfterText = _root.Q<Label>("cp-after-text");
            _cpDeltaText = _root.Q<Label>("cp-delta-text");

            _statRowElements = new VisualElement[MAX_STAT_ROWS];
            _statNameLabels = new Label[MAX_STAT_ROWS];
            _statBeforeLabels = new Label[MAX_STAT_ROWS];
            _statAfterLabels = new Label[MAX_STAT_ROWS];
            _statDeltaLabels = new Label[MAX_STAT_ROWS];

            for (int i = 0; i < MAX_STAT_ROWS; i++)
            {
                _statRowElements[i] = _root.Q<VisualElement>($"stat-row-{i}");
                _statNameLabels[i] = _root.Q<Label>($"stat-name-{i}");
                _statBeforeLabels[i] = _root.Q<Label>($"stat-before-{i}");
                _statAfterLabels[i] = _root.Q<Label>($"stat-after-{i}");
                _statDeltaLabels[i] = _root.Q<Label>($"stat-delta-{i}");
            }
        }

        private void BindCallbacks()
        {
            if (_closeBtn != null)
                _closeBtn.RegisterCallback<ClickEvent>(OnCloseClicked);

            if (_dimOverlay != null)
            {
                _dimOverlay.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.target == _dimOverlay)
                        SetVisible(false);
                });
            }
        }

        /// <summary>
        /// 전후 비교 팝업을 표시한다.
        /// </summary>
        public void ShowComparison(string title, StatDelta[] deltas, long cpBefore, long cpAfter)
        {
            if (_titleText != null)
                _titleText.text = title;

            // 스탯 행 갱신
            for (int i = 0; i < MAX_STAT_ROWS; i++)
            {
                if (_statRowElements[i] == null) continue;

                if (deltas != null && i < deltas.Length)
                {
                    _statRowElements[i].style.display = DisplayStyle.Flex;
                    var delta = deltas[i];

                    if (_statNameLabels[i] != null)
                        _statNameLabels[i].text = delta.StatName;

                    if (_statBeforeLabels[i] != null)
                        _statBeforeLabels[i].text = delta.Before.ToString("N0");

                    if (_statAfterLabels[i] != null)
                        _statAfterLabels[i].text = delta.After.ToString("N0");

                    int diff = delta.After - delta.Before;
                    if (_statDeltaLabels[i] != null)
                    {
                        ApplyDeltaStyle(_statDeltaLabels[i], diff);
                    }
                }
                else
                {
                    _statRowElements[i].style.display = DisplayStyle.None;
                }
            }

            // CP 비교
            if (_cpBeforeText != null)
                _cpBeforeText.text = $"{cpBefore:N0}";

            if (_cpAfterText != null)
                _cpAfterText.text = $"{cpAfter:N0}";

            long cpDelta = cpAfter - cpBefore;
            if (_cpDeltaText != null)
                ApplyDeltaStyle(_cpDeltaText, cpDelta);

            SetVisible(true);

            // 자동 닫기 타이머 시작
            _autoCloseTimer = AUTO_CLOSE_DELAY;
            _isAutoClosing = true;

            AudioManager.Instance?.PlayUiSfx("sfx_ui_compare");
        }

        private static void ApplyDeltaStyle(Label label, long diff)
        {
            label.RemoveFromClassList("delta--increase");
            label.RemoveFromClassList("delta--decrease");
            label.RemoveFromClassList("delta--neutral");

            if (diff > 0)
            {
                label.text = $"+{diff:N0}";
                label.AddToClassList("delta--increase");
            }
            else if (diff < 0)
            {
                label.text = $"{diff:N0}";
                label.AddToClassList("delta--decrease");
            }
            else
            {
                label.text = "0";
                label.AddToClassList("delta--neutral");
            }
        }

        private void OnCloseClicked(ClickEvent evt)
        {
            _isAutoClosing = false;
            SetVisible(false);
        }

        private void OnDisable()
        {
            _root?.RemoveFromClassList("popup--open");
        }

        /// <summary>
        /// 팝업 표시 여부를 설정한다.
        /// </summary>
        public void SetVisible(bool isVisible)
        {
            if (isVisible)
            {
                if (_dimOverlay != null)
                    _dimOverlay.style.display = DisplayStyle.Flex;
                PlayOpenAnimation();
            }
            else
            {
                _isAutoClosing = false;
                PlayCloseAnimation();
            }
        }

        private async void PlayOpenAnimation()
        {
            if (_root == null) return;
            _root.RemoveFromClassList("popup--open");
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, this.GetCancellationTokenOnDestroy());
            _root.AddToClassList("popup--open");
        }

        private async void PlayCloseAnimation()
        {
            if (_root == null) return;
            _root.RemoveFromClassList("popup--open");
            await UniTask.Delay(150, cancellationToken: this.GetCancellationTokenOnDestroy());
            if (_dimOverlay != null)
                _dimOverlay.style.display = DisplayStyle.None;
        }
    }
}
