using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Threading;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.UI
{
    public class TutorialHighlightMask : MonoBehaviour, ITutorialHighlight
    {
        [SerializeField] private CanvasGroup _maskCanvasGroup;
        [SerializeField] private RectTransform _cutoutHole;
        [SerializeField] private RectTransform _arrowIndicator;
        [SerializeField] private Image _maskOverlay;
        [SerializeField] private Image _glowBorder;
        [SerializeField] private float _arrowBounceDistance = 20f;
        [SerializeField] private float _arrowBounceDuration = 0.6f;
        [SerializeField] private float _glowPulseMin = 0.3f;
        [SerializeField] private float _glowPulseMax = 0.8f;
        [SerializeField] private float _glowPulseDuration = 0.8f;

        private Tween _arrowTween;
        private Tween _glowTween;

        private void Awake()
        {
            EnsureComponents();
            if (_maskCanvasGroup != null)
                _maskCanvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        public async UniTask HighlightTarget(RectTransform target, ArrowDirection direction = ArrowDirection.Down, CancellationToken token = default)
        {
            if (target == null) return;

            gameObject.SetActive(true);

            // 컷아웃 위치/크기를 target에 맞춤
            if (_cutoutHole != null)
            {
                _cutoutHole.position = target.position;
                _cutoutHole.sizeDelta = target.sizeDelta * 1.2f;
            }

            // 글로우 테두리 위치/크기 동기화
            if (_glowBorder != null)
            {
                _glowBorder.gameObject.SetActive(true);
                _glowBorder.rectTransform.position = target.position;
                _glowBorder.rectTransform.sizeDelta = target.sizeDelta * 1.3f;
                StartGlowPulse();
            }

            // 화살표 방향 설정
            if (_arrowIndicator != null)
            {
                _arrowIndicator.gameObject.SetActive(true);
                PositionArrow(target, direction);
                StartArrowBounce(direction);
            }

            // 마스크 페이드인
            if (_maskCanvasGroup != null)
            {
                _maskCanvasGroup.alpha = 0f;
                await DOTween.To(
                    () => _maskCanvasGroup.alpha,
                    x => _maskCanvasGroup.alpha = x,
                    1f, 0.3f)
                    .SetUpdate(true)
                    .ToUniTask(cancellationToken: token);
            }
        }

        // ── ITutorialHighlight 구현 ──

        UniTask ITutorialHighlight.ShowHighlight(RectTransform target, ArrowDirection direction, CancellationToken token)
        {
            return HighlightTarget(target, direction, token);
        }

        UniTask ITutorialHighlight.HideHighlight(CancellationToken token)
        {
            return Hide(token);
        }

        public async UniTask Hide(CancellationToken token = default)
        {
            _arrowTween?.Kill();
            _glowTween?.Kill();

            if (_maskCanvasGroup != null)
            {
                await DOTween.To(
                    () => _maskCanvasGroup.alpha,
                    x => _maskCanvasGroup.alpha = x,
                    0f, 0.2f)
                    .SetUpdate(true)
                    .ToUniTask(cancellationToken: token);
            }

            if (_glowBorder != null)
                _glowBorder.gameObject.SetActive(false);

            gameObject.SetActive(false);
        }

        private void PositionArrow(RectTransform target, ArrowDirection direction)
        {
            if (_arrowIndicator == null) return;

            float offset = target.sizeDelta.y * 0.5f + 30f;
            Vector3 pos = target.position;

            switch (direction)
            {
                case ArrowDirection.Up:
                    pos.y += offset;
                    _arrowIndicator.rotation = Quaternion.Euler(0, 0, 180);
                    break;
                case ArrowDirection.Down:
                    pos.y -= offset;
                    _arrowIndicator.rotation = Quaternion.identity;
                    break;
                case ArrowDirection.Left:
                    pos.x -= offset;
                    _arrowIndicator.rotation = Quaternion.Euler(0, 0, -90);
                    break;
                case ArrowDirection.Right:
                    pos.x += offset;
                    _arrowIndicator.rotation = Quaternion.Euler(0, 0, 90);
                    break;
            }

            _arrowIndicator.position = pos;
        }

        private void StartArrowBounce(ArrowDirection direction)
        {
            _arrowTween?.Kill();

            Vector3 bounceDir = direction switch
            {
                ArrowDirection.Up => Vector3.up,
                ArrowDirection.Down => Vector3.down,
                ArrowDirection.Left => Vector3.left,
                ArrowDirection.Right => Vector3.right,
                _ => Vector3.down
            };

            Vector3 startPos = _arrowIndicator.localPosition;
            _arrowTween = _arrowIndicator
                .DOLocalMove(startPos + bounceDir * _arrowBounceDistance, _arrowBounceDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void StartGlowPulse()
        {
            _glowTween?.Kill();
            if (_glowBorder == null) return;

            var c = _glowBorder.color;
            c.a = _glowPulseMin;
            _glowBorder.color = c;

            _glowTween = DOTween.To(
                () => _glowBorder.color.a,
                a =>
                {
                    var col = _glowBorder.color;
                    col.a = a;
                    _glowBorder.color = col;
                },
                _glowPulseMax,
                _glowPulseDuration
            ).SetLoops(-1, LoopType.Yoyo)
             .SetEase(Ease.InOutSine)
             .SetUpdate(true)
             .SetLink(gameObject);
        }

        private void EnsureComponents()
        {
            if (GetComponentInParent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 490;
                gameObject.AddComponent<CanvasScaler>();
                gameObject.AddComponent<GraphicRaycaster>();
            }

            if (_maskCanvasGroup == null)
                _maskCanvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            // 반투명 오버레이
            if (_maskOverlay == null)
            {
                var overlayObj = new GameObject("MaskOverlay", typeof(RectTransform));
                overlayObj.transform.SetParent(transform, false);
                _maskOverlay = overlayObj.AddComponent<Image>();
                _maskOverlay.color = new Color(0f, 0f, 0f, 0.6f);
                _maskOverlay.raycastTarget = true;
                var rt = overlayObj.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
            }

            // 컷아웃 영역 (실제 마스킹은 셰이더 의존, 여기서는 위치 마커)
            if (_cutoutHole == null)
            {
                var holeObj = new GameObject("CutoutHole", typeof(RectTransform));
                holeObj.transform.SetParent(transform, false);
                _cutoutHole = holeObj.GetComponent<RectTransform>();
                _cutoutHole.sizeDelta = new Vector2(100f, 100f);
            }

            // 글로우 테두리 (하이라이트 대상 주위 빛나는 테두리)
            if (_glowBorder == null)
            {
                var glowObj = new GameObject("GlowBorder", typeof(RectTransform));
                glowObj.transform.SetParent(transform, false);
                _glowBorder = glowObj.AddComponent<Image>();

                var theme = UIThemeManager.Instance;
                Color glowColor = theme != null ? theme.Theme.accentGold : new Color(1f, 0.84f, 0f);
                glowColor.a = _glowPulseMin;
                _glowBorder.color = glowColor;
                _glowBorder.raycastTarget = false;

                var rt = glowObj.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(120f, 120f);

                // Outline 컴포넌트로 테두리 효과
                var outline = glowObj.AddComponent<Outline>();
                outline.effectColor = new Color(glowColor.r, glowColor.g, glowColor.b, 0.8f);
                outline.effectDistance = new Vector2(3f, 3f);

                glowObj.SetActive(false);
            }

            // 프로시져럴 화살표 (삼각형)
            if (_arrowIndicator == null)
            {
                var arrowObj = new GameObject("ArrowGuide", typeof(RectTransform));
                arrowObj.transform.SetParent(transform, false);
                _arrowIndicator = arrowObj.GetComponent<RectTransform>();
                _arrowIndicator.sizeDelta = new Vector2(30f, 30f);

                var arrowImg = arrowObj.AddComponent<Image>();
                arrowImg.sprite = CreateTriangleSprite();
                arrowImg.raycastTarget = false;

                var theme = UIThemeManager.Instance;
                arrowImg.color = theme != null ? theme.Theme.accentGold : new Color(1f, 0.84f, 0f);

                arrowObj.SetActive(false);
            }
        }

        /// <summary>
        /// 프로시져럴 삼각형 스프라이트를 생성한다 (아래 방향 화살표).
        /// </summary>
        private static Sprite _cachedTriangleSprite;
        private static Sprite CreateTriangleSprite()
        {
            if (_cachedTriangleSprite != null) return _cachedTriangleSprite;

            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 아래쪽 꼭짓점 삼각형: (half, 0), (0, size), (size, size)
                    float ny = (float)y / size;
                    float halfWidth = ny * half;
                    float cx = Mathf.Abs(x - half);

                    if (cx <= halfWidth && y > 0)
                        tex.SetPixel(x, y, Color.white);
                    else
                        tex.SetPixel(x, y, Color.clear);
                }
            }

            tex.Apply();
            _cachedTriangleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _cachedTriangleSprite;
        }

        private void OnDestroy()
        {
            _arrowTween?.Kill();
            _glowTween?.Kill();
            transform.DOKill();
        }
    }
}
