using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace MkLike.UI
{
    /// <summary>
    /// 모든 팝업의 기반 클래스.
    /// CanvasGroup을 통해 표시/숨김을 제어하며, 서브클래스에서 OnShow/OnHide를 오버라이드한다.
    /// DOTween 기반 Show/Hide 연출을 기본 제공한다.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class BasePopup : MonoBehaviour
    {
        [SerializeField] private bool _useShowAnimation = true;
        [SerializeField] private float _showDuration = 0.25f;
        [SerializeField] private float _hideDuration = 0.15f;

        protected CanvasGroup canvasGroup;
        private RectTransform _rectTransform;
        private Sequence _showSequence;
        private Sequence _hideSequence;

        /// <summary>팝업이 현재 보이는 상태인지 여부</summary>
        public bool IsVisible { get; private set; }

        protected virtual void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            _rectTransform = GetComponent<RectTransform>();
            ApplyPopupTheme();
        }

        /// <summary>
        /// UIThemeManager에서 팝업 배경/버튼/닫기 스프라이트 + 테마 색상을 적용한다.
        /// </summary>
        private void ApplyPopupTheme()
        {
            var theme = UIThemeManager.Instance;
            if (theme == null) return;

            // 자신의 Image에 팝업 배경 스프라이트 적용 (sprite 있으면 Color.white)
            var bgImage = GetComponent<Image>();
            if (bgImage != null)
            {
                var popupBg = theme.GetPopupBackground() ?? theme.PanelBackground;
                UIThemeManager.ApplySpriteOrColor(bgImage, popupBg, theme.Theme.panelBackground);
            }

            // 자식 Button에 버튼 스프라이트 적용
            var buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                var btn = buttons[i];
                var btnImg = btn.GetComponent<Image>();
                if (btnImg == null) continue;

                // 닫기 버튼은 별도 스프라이트
                if (btn.name.Contains("Close"))
                {
                    theme.ApplyCloseButton(btnImg);
                    continue;
                }

                theme.ApplyButtonFull(btn);
            }
        }

        /// <summary>
        /// 팝업을 표시한다.
        /// </summary>
        public void Show()
        {
            // Awake가 아직 호출되지 않은 비활성 패널 대비
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();

            KillSequences();
            gameObject.SetActive(true);

            if (_useShowAnimation)
            {
                canvasGroup.alpha = 0f;
                _rectTransform.localScale = Vector3.one * 0.85f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;

                _showSequence = DOTween.Sequence()
                    .Join(DOTween.To(() => canvasGroup.alpha, x => canvasGroup.alpha = x, 1f, _showDuration * 0.8f))
                    .Join(_rectTransform.DOScale(1f, _showDuration).SetEase(Ease.OutBack))
                    .OnComplete(() =>
                    {
                        canvasGroup.interactable = true;
                        canvasGroup.blocksRaycasts = true;
                    })
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }
            else
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                _rectTransform.localScale = Vector3.one;
            }

            IsVisible = true;
            OnShow();
        }

        /// <summary>
        /// 팝업을 숨긴다.
        /// </summary>
        public void Hide()
        {
            // Awake가 아직 호출되지 않은 비활성 패널 대비
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();

            KillSequences();

            if (_useShowAnimation && gameObject.activeInHierarchy)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                IsVisible = false;
                OnHide();

                _hideSequence = DOTween.Sequence()
                    .Join(DOTween.To(() => canvasGroup.alpha, x => canvasGroup.alpha = x, 0f, _hideDuration))
                    .Join(_rectTransform.DOScale(0.9f, _hideDuration).SetEase(Ease.InBack))
                    .OnComplete(() =>
                    {
                        gameObject.SetActive(false);
                        _rectTransform.localScale = Vector3.one;
                    })
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }
            else
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                IsVisible = false;
                OnHide();
                gameObject.SetActive(false);
            }
        }

        private void KillSequences()
        {
            _showSequence?.Kill();
            _showSequence = null;
            _hideSequence?.Kill();
            _hideSequence = null;
        }

        /// <summary>
        /// 팝업이 열릴 때 호출된다. 서브클래스에서 초기화 로직을 작성한다.
        /// </summary>
        protected virtual void OnShow() { }

        /// <summary>
        /// 팝업이 닫힐 때 호출된다. 서브클래스에서 정리 로직을 작성한다.
        /// </summary>
        protected virtual void OnHide() { }

        /// <summary>
        /// 뒤로가기/ESC 입력 시 호출된다.
        /// 기본 동작은 UIManager를 통해 최상위 팝업을 닫는다.
        /// </summary>
        public virtual void OnBackButton()
        {
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ClosePopup();
            }
        }

        protected virtual void OnDestroy()
        {
            KillSequences();
        }
    }
}
