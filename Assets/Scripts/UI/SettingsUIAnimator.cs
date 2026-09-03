using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace MkLike.UI
{
    public class SettingsUIAnimator : MonoBehaviour
    {
        [SerializeField] private Toggle[] _toggles;
        [SerializeField] private Slider[] _sliders;

        private void Awake()
        {
            for (int i = 0; i < _toggles.Length; i++)
            {
                var toggle = _toggles[i];
                var checkmark = toggle.graphic?.rectTransform;
                if (checkmark == null) continue;

                toggle.onValueChanged.AddListener(isOn =>
                {
                    DOTween.Kill(checkmark);
                    if (isOn)
                    {
                        checkmark.localScale = Vector3.zero;
                        checkmark.DOScale(Vector3.one, 0.15f)
                            .SetEase(Ease.OutBack)
                            .SetUpdate(true)
                            .SetLink(toggle.gameObject);
                    }
                    else
                    {
                        checkmark.DOScale(Vector3.zero, 0.1f)
                            .SetEase(Ease.InBack)
                            .SetUpdate(true)
                            .SetLink(toggle.gameObject);
                    }
                });
            }

            for (int i = 0; i < _sliders.Length; i++)
            {
                var slider = _sliders[i];
                var handle = slider.handleRect;
                if (handle == null) continue;

                slider.onValueChanged.AddListener(_ =>
                {
                    DOTween.Kill(handle);
                    handle.DOPunchScale(Vector3.one * 0.15f, 0.1f, 1)
                        .SetUpdate(true)
                        .SetLink(slider.gameObject);
                });
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _toggles.Length; i++)
            {
                if (_toggles[i] != null && _toggles[i].graphic != null)
                    DOTween.Kill(_toggles[i].graphic.rectTransform);
            }

            for (int i = 0; i < _sliders.Length; i++)
            {
                if (_sliders[i] != null && _sliders[i].handleRect != null)
                    DOTween.Kill(_sliders[i].handleRect);
            }
        }
    }
}
