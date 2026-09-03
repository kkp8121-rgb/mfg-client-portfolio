using DG.Tweening;
using MkLike.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MkLike.UI
{
    /// <summary>
    /// 버튼에 부착하여 누름/뗌 시 스케일 + 색상 피드백을 제공한다.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private float _pressScale = 0.95f;
        [SerializeField] private float _duration = 0.12f;
        [SerializeField] private float _colorDarken = 0.15f;
        [SerializeField] private bool _useColorFeedback = true;

        private Vector3 _originalScale;
        private Image _image;
        private Color _originalColor;

        private void Awake()
        {
            _originalScale = transform.localScale;
            _image = GetComponent<Image>();
            if (_image != null)
                _originalColor = _image.color;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            transform.DOKill();
            transform.DOScale(_originalScale * _pressScale, _duration * 0.5f)
                .SetUpdate(true)
                .SetLink(gameObject);

            if (_useColorFeedback && _image != null)
            {
                Color darkened = new Color(
                    _originalColor.r - _colorDarken,
                    _originalColor.g - _colorDarken,
                    _originalColor.b - _colorDarken,
                    _originalColor.a
                );
                _image.color = darkened;
            }

            AudioManager.Instance?.PlayUiSfx("sfx_ui_tap");
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            transform.DOKill();
            transform.DOScale(_originalScale, _duration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .SetLink(gameObject);

            if (_useColorFeedback && _image != null)
                _image.color = _originalColor;
        }

        /// <summary>
        /// 외부에서 버튼 색상이 변경되었을 때 기준 색상을 갱신한다.
        /// </summary>
        public void RefreshOriginalColor()
        {
            if (_image != null)
                _originalColor = _image.color;
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }
    }
}
