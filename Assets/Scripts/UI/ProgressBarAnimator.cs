using DG.Tweening;
using MkLike.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace MkLike.UI
{
    /// <summary>
    /// Image.fillAmount를 부드럽게 애니메이션하며, 진행도에 따라 색상을 변경한다.
    /// </summary>
    public class ProgressBarAnimator : MonoBehaviour
    {
        [SerializeField] private Image _fillImage;
        [SerializeField] private float _animDuration = 0.3f;
        [SerializeField] private bool _useColorGradient = true;

        private static readonly Color ColorLow = new Color(0.9f, 0.2f, 0.2f);
        private static readonly Color ColorMid = new Color(0.95f, 0.8f, 0.15f);
        private static readonly Color ColorHigh = new Color(0.2f, 0.85f, 0.3f);

        private Tween _fillTween;
        private Tween _colorTween;

        /// <summary>
        /// 진행도를 설정한다. 0~1 범위.
        /// </summary>
        public void SetProgress(float target01)
        {
            target01 = Mathf.Clamp01(target01);

            if (_fillImage == null) return;

            _fillTween?.Kill();
            _fillTween = DOTween.To(() => _fillImage.fillAmount, x => _fillImage.fillAmount = x, target01, _animDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject);

            if (_useColorGradient)
            {
                Color targetColor = GetColorForProgress(target01);
                _colorTween?.Kill();
                _colorTween = ColorTweenHelper.To(_fillImage, targetColor, _animDuration)
                    .SetLink(gameObject);
            }
        }

        /// <summary>
        /// 연출 없이 즉시 설정한다.
        /// </summary>
        public void SetProgressImmediate(float target01)
        {
            target01 = Mathf.Clamp01(target01);

            if (_fillImage == null) return;

            _fillTween?.Kill();
            _colorTween?.Kill();

            _fillImage.fillAmount = target01;

            if (_useColorGradient)
            {
                _fillImage.color = GetColorForProgress(target01);
            }
        }

        private static Color GetColorForProgress(float t)
        {
            if (t < 0.33f)
                return Color.Lerp(ColorLow, ColorMid, t / 0.33f);
            if (t < 0.67f)
                return Color.Lerp(ColorMid, ColorHigh, (t - 0.33f) / 0.34f);
            return ColorHigh;
        }

        private void OnDestroy()
        {
            _fillTween?.Kill();
            _colorTween?.Kill();
        }
    }
}
