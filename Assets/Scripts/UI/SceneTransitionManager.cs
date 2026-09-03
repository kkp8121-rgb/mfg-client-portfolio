using UnityEngine;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// 씬 전환 이펙트 매니저.
    /// 페이드/와이프/원형 마스크 전환을 통일된 인터페이스로 제공한다.
    /// DontDestroyOnLoad로 씬 간 유지된다.
    /// </summary>
    public class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }

        [SerializeField] private CanvasGroup _fadeOverlay;
        [SerializeField] private RectTransform _wipeBar;
        [SerializeField] private RectTransform _circleMask;
        [SerializeField] private float _defaultDuration = 0.5f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (_fadeOverlay != null)
            {
                _fadeOverlay.alpha = 0f;
                _fadeOverlay.blocksRaycasts = false;
                _fadeOverlay.gameObject.SetActive(false);
            }

            if (_wipeBar != null)
                _wipeBar.gameObject.SetActive(false);

            if (_circleMask != null)
                _circleMask.gameObject.SetActive(false);
        }

        /// <summary>
        /// 화면을 검은색으로 페이드아웃 (불투명으로 전환).
        /// </summary>
        public async UniTask FadeOut(float duration = -1)
        {
            if (_fadeOverlay == null) return;
            float dur = duration < 0 ? _defaultDuration : duration;

            _fadeOverlay.gameObject.SetActive(true);
            _fadeOverlay.blocksRaycasts = true;
            _fadeOverlay.alpha = 0f;

            await DOTween.To(() => _fadeOverlay.alpha, x => _fadeOverlay.alpha = x, 1f, dur)
                .SetUpdate(true)
                .SetLink(gameObject)
                .ToUniTask();
        }

        /// <summary>
        /// 화면을 페이드인 (투명으로 전환, 게임 보이기).
        /// </summary>
        public async UniTask FadeIn(float duration = -1)
        {
            if (_fadeOverlay == null) return;
            float dur = duration < 0 ? _defaultDuration : duration;

            _fadeOverlay.alpha = 1f;

            await DOTween.To(() => _fadeOverlay.alpha, x => _fadeOverlay.alpha = x, 0f, dur)
                .SetUpdate(true)
                .SetLink(gameObject)
                .ToUniTask();

            _fadeOverlay.blocksRaycasts = false;
            _fadeOverlay.gameObject.SetActive(false);
        }

        /// <summary>
        /// 좌→우 와이프 전환. onMidPoint에서 씬 로딩 등을 수행한다.
        /// </summary>
        public async UniTask WipeTransition(System.Action onMidPoint = null)
        {
            if (_wipeBar == null) return;

            var canvas = _wipeBar.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var canvasRect = canvas.GetComponent<RectTransform>();
            float screenWidth = canvasRect.rect.width;

            _wipeBar.gameObject.SetActive(true);
            _wipeBar.sizeDelta = new Vector2(screenWidth, _wipeBar.sizeDelta.y);
            _wipeBar.anchoredPosition = new Vector2(-screenWidth, 0f);

            // 와이프 진입 (좌 → 중앙)
            await DOTween.To(() => _wipeBar.anchoredPosition.x, x => { var p = _wipeBar.anchoredPosition; p.x = x; _wipeBar.anchoredPosition = p; }, 0f, _defaultDuration)
                .SetEase(Ease.InOutQuad)
                .SetUpdate(true)
                .SetLink(gameObject)
                .ToUniTask();

            onMidPoint?.Invoke();
            await UniTask.Delay(100, ignoreTimeScale: true, cancellationToken: destroyCancellationToken);

            // 와이프 퇴장 (중앙 → 우)
            await DOTween.To(() => _wipeBar.anchoredPosition.x, x => { var p = _wipeBar.anchoredPosition; p.x = x; _wipeBar.anchoredPosition = p; }, screenWidth, _defaultDuration)
                .SetEase(Ease.InOutQuad)
                .SetUpdate(true)
                .SetLink(gameObject)
                .ToUniTask();

            _wipeBar.gameObject.SetActive(false);
        }

        /// <summary>
        /// 원형 마스크 전환 (구멍 축소 → onMidPoint → 구멍 확대).
        /// </summary>
        public async UniTask CircleTransition(System.Action onMidPoint = null)
        {
            if (_circleMask == null) return;

            var canvas = _circleMask.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var canvasRect = canvas.GetComponent<RectTransform>();
            float maxSize = Mathf.Max(canvasRect.rect.width, canvasRect.rect.height) * 1.5f;

            _circleMask.gameObject.SetActive(true);
            _circleMask.sizeDelta = new Vector2(maxSize, maxSize);

            // 원 축소 (큰 원 → 0)
            await DOTween.To(() => _circleMask.sizeDelta, s => _circleMask.sizeDelta = s, Vector2.zero, _defaultDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .SetLink(gameObject)
                .ToUniTask();

            onMidPoint?.Invoke();
            await UniTask.Delay(100, ignoreTimeScale: true, cancellationToken: destroyCancellationToken);

            // 원 확대 (0 → 큰 원)
            await DOTween.To(() => _circleMask.sizeDelta, s => _circleMask.sizeDelta = s, new Vector2(maxSize, maxSize), _defaultDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(gameObject)
                .ToUniTask();

            _circleMask.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            transform.DOKill();

            if (_fadeOverlay != null)
                DOTween.Kill(_fadeOverlay);
            if (_wipeBar != null)
                DOTween.Kill(_wipeBar);
            if (_circleMask != null)
                DOTween.Kill(_circleMask);

            if (Instance == this)
                Instance = null;
        }
    }
}
