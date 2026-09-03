using DG.Tweening;
using MkLike.Core;
using MkLike.Utils;
using TMPro;
using UnityEngine;

namespace MkLike.UI
{
    public class TutorialHighlight : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _highlightOverlay;
        [SerializeField] private RectTransform _highlightFrame;
        [SerializeField] private TMP_Text _guideText;
        [SerializeField] private RectTransform _guideArrow;

        [SerializeField] private float _arrowBounceDistance = 15f;
        [SerializeField] private float _arrowBounceDuration = 0.6f;

        private Tween _frameTween;
        private Tween _arrowTween;

        private void OnEnable()
        {
            EventBus<TutorialStepStartedEvent>.Subscribe(OnTutorialStepStarted);
        }

        private void OnDisable()
        {
            EventBus<TutorialStepStartedEvent>.Unsubscribe(OnTutorialStepStarted);
        }

        public void HighlightTarget(RectTransform target, string guideMessage)
        {
            KillAllTweens();

            _highlightOverlay.alpha = 0f;
            _highlightOverlay.gameObject.SetActive(true);
            DOTween.To(() => _highlightOverlay.alpha, x => _highlightOverlay.alpha = x, 0.7f, 0.3f).SetUpdate(true).SetLink(gameObject);

            _highlightFrame.position = target.position;
            _highlightFrame.sizeDelta = target.sizeDelta;
            _highlightFrame.gameObject.SetActive(true);

            _frameTween = _highlightFrame.DOPunchScale(Vector3.one * 0.05f, 0.5f, 1)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true)
                .SetLink(gameObject);

            _guideText.text = guideMessage;
            _guideText.alpha = 0f;
            DOTween.To(() => _guideText.alpha, x => _guideText.alpha = x, 1f, 0.3f).SetUpdate(true).SetLink(gameObject);

            _guideArrow.gameObject.SetActive(true);
            var arrowPos = _guideArrow.anchoredPosition;
            _arrowTween = DOTween.To(() => _guideArrow.anchoredPosition.y, y => { var p = _guideArrow.anchoredPosition; p.y = y; _guideArrow.anchoredPosition = p; }, arrowPos.y + _arrowBounceDistance, _arrowBounceDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        public void ClearHighlight()
        {
            KillAllTweens();

            if (_highlightOverlay != null)
            {
                DOTween.To(() => _highlightOverlay.alpha, x => _highlightOverlay.alpha = x, 0f, 0.2f)
                    .SetUpdate(true)
                    .SetLink(gameObject)
                    .OnComplete(() => _highlightOverlay.gameObject.SetActive(false));
            }

            if (_highlightFrame != null)
                _highlightFrame.gameObject.SetActive(false);

            if (_guideText != null)
                _guideText.alpha = 0f;

            if (_guideArrow != null)
                _guideArrow.gameObject.SetActive(false);
        }

        private void OnTutorialStepStarted(TutorialStepStartedEvent evt)
        {
            if (evt.Step == TutorialStep.None || evt.Step == TutorialStep.Completed)
            {
                ClearHighlight();
            }
        }

        private void KillAllTweens()
        {
            _frameTween?.Kill();
            _frameTween = null;
            _arrowTween?.Kill();
            _arrowTween = null;

            if (_highlightOverlay != null) DOTween.Kill(_highlightOverlay);
            if (_highlightFrame != null) DOTween.Kill(_highlightFrame);
            if (_guideText != null) DOTween.Kill(_guideText.transform);
            if (_guideArrow != null) DOTween.Kill(_guideArrow);
        }

        private void OnDestroy()
        {
            KillAllTweens();
        }
    }
}
