using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// UI Toolkit 기반 재화 부족 팝업.
    /// CurrencyShortageEvent를 구독하여 자동 표시한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class CurrencyShortagePopupUI : MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _root;

        // 캐싱된 요소
        private VisualElement _dimOverlay;
        private Label _titleText;
        private Label _requiredText;
        private Label _currentText;
        private Label _shortageText;
        private VisualElement _sourceContainer;
        private Button _closeBtn;

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            _doc.sortingOrder = 100; // HUD(0)/TabBar(10) 위에 렌더링
            _root = _doc.rootVisualElement;
            if (_root == null)
            {
                Debug.LogWarning("[CurrencyShortagePopupUI] rootVisualElement == null, UI 초기화 스킵");
                return;
            }
            _root.pickingMode = PickingMode.Ignore;

            CacheElements();
            BindCallbacks();

            EventBus<CurrencyShortageEvent>.Subscribe(OnCurrencyShortage);

            // 초기 숨김
            SetVisible(false);
        }

        private void OnDisable()
        {
            EventBus<CurrencyShortageEvent>.Unsubscribe(OnCurrencyShortage);
            _root?.RemoveFromClassList("popup--open");
        }

        private void CacheElements()
        {
            _dimOverlay = _root.Q<VisualElement>("dim-overlay");
            _titleText = _root.Q<Label>("title-text");
            _requiredText = _root.Q<Label>("required-text");
            _currentText = _root.Q<Label>("current-text");
            _shortageText = _root.Q<Label>("shortage-text");
            _sourceContainer = _root.Q<VisualElement>("source-container");
            _closeBtn = _root.Q<Button>("close-btn");
        }

        private void BindCallbacks()
        {
            if (_closeBtn != null)
                _closeBtn.RegisterCallback<ClickEvent>(OnCloseClicked);

            // 딤 오버레이 클릭 시 닫기
            if (_dimOverlay != null)
            {
                _dimOverlay.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.target == _dimOverlay)
                        SetVisible(false);
                });
            }
        }

        private void OnCurrencyShortage(CurrencyShortageEvent evt)
        {
            PopulateUI(evt.Type, evt.Required, evt.Current);
            SetVisible(true);
        }

        private void PopulateUI(CurrencyType type, BigNumber required, BigNumber current)
        {
            BigNumber shortage = required - current;
            string displayName = CurrencySourceMap.GetDisplayName(type);

            if (_titleText != null)
                _titleText.text = $"{displayName} 부족!";

            if (_requiredText != null)
                _requiredText.text = NumberFormatter.FormatKorean(required);

            if (_currentText != null)
                _currentText.text = NumberFormatter.FormatKorean(current);

            if (_shortageText != null)
                _shortageText.text = NumberFormatter.FormatKorean(shortage);

            BuildSourceButtons(type);
        }

        private void BuildSourceButtons(CurrencyType type)
        {
            if (_sourceContainer == null) return;

            _sourceContainer.Clear();

            List<CurrencySource> sources = CurrencySourceMap.GetSources(type, 3);
            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                var btn = CreateSourceButton(source);
                _sourceContainer.Add(btn);
            }
        }

        private VisualElement CreateSourceButton(CurrencySource source)
        {
            var btn = new Button();
            btn.AddToClassList("source-btn");

            var nameLabel = new Label(source.ContentName);
            nameLabel.AddToClassList("source-btn__name");
            btn.Add(nameLabel);

            var descLabel = new Label(source.Description);
            descLabel.AddToClassList("source-btn__desc");
            btn.Add(descLabel);

            string panelAction = source.PanelAction;
            btn.RegisterCallback<ClickEvent>(_ =>
            {
                SetVisible(false);
                Debug.Log($"[CurrencyShortagePopupUI] 패널 이동: {panelAction}");
            });

            return btn;
        }

        private void OnCloseClicked(ClickEvent evt)
        {
            SetVisible(false);
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
