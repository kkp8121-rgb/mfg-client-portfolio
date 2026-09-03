using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace MkLike.UI
{
    /// <summary>
    /// 개별 탭 버튼. 클릭 시 DOTween 바운스 + 색상 전환.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class TabButton : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI tabText;
        [SerializeField] private Color normalColor = Color.gray;
        [SerializeField] private Color selectedColor = Color.white;
        [SerializeField] private int tabIndex;

        private TabController _controller;
        private Button _button;
        private Vector3 _originalScale;
        private Tween _scaleTween;
        private Tween _colorTween;
        private Sprite _normalSprite;
        private Sprite _selectedSprite;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _originalScale = transform.localScale;
            ApplyTabTheme();
        }

        public void Initialize(TabController controller)
        {
            _controller = controller;
            _button.onClick.AddListener(OnClick);
        }

        public void SetSelected(bool selected)
        {
            if (backgroundImage == null) return;

            // 스프라이트가 있으면 스프라이트 교체 + Color.white (원본 표시)
            if (_normalSprite != null && _selectedSprite != null)
            {
                backgroundImage.sprite = selected ? _selectedSprite : _normalSprite;
                backgroundImage.color = Color.white;
                return; // 스프라이트 전환만으로 충분, 색상 애니메이션 불필요
            }

            var targetColor = selected ? selectedColor : normalColor;
            _colorTween?.Kill();
            _colorTween = DOTween.To(
                () => backgroundImage.color,
                c => backgroundImage.color = c,
                targetColor, 0.2f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true).SetLink(gameObject);

            var targetScale = selected ? _originalScale * 1.05f : _originalScale;
            _scaleTween?.Kill();
            _scaleTween = transform.DOScale(targetScale, 0.2f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true).SetLink(gameObject);
        }

        private void OnClick()
        {
            _scaleTween?.Kill();
            transform.localScale = _originalScale;
            _scaleTween = transform.DOPunchScale(Vector3.one * 0.12f, 0.25f, 8, 0.5f)
                .SetUpdate(true).SetLink(gameObject);

            if (_controller != null)
                _controller.SwitchTab(tabIndex);
        }

        private void ApplyTabTheme()
        {
            var theme = UIThemeManager.Instance;
            if (theme == null) return;

            _normalSprite = theme.TabNormal;
            _selectedSprite = theme.TabSelected;

            if (backgroundImage != null && _normalSprite != null)
            {
                backgroundImage.sprite = _normalSprite;
                backgroundImage.type = Image.Type.Sliced;
                backgroundImage.color = Color.white; // Stone Kit 스프라이트 원본 표시
            }
        }

        private void OnDestroy()
        {
            _scaleTween?.Kill();
            _colorTween?.Kill();
        }
    }
}
