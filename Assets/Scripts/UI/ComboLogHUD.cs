using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// 좌측 상단 콤보 스크롤 로그 HUD.
    /// ComboEvent 구독하여 "x5 COMBO!" 형태의 텍스트를 스크롤 표시.
    /// </summary>
    public class ComboLogHUD : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private int _maxEntries = 8;
        [SerializeField] private float _displayDuration = 2f;
        [SerializeField] private float _slideInDuration = 0.2f;
        [SerializeField] private float _fadeOutDuration = 0.4f;
        [SerializeField] private float _entryHeight = 28f;
        [SerializeField] private float _slideInOffset = -120f;
        [SerializeField] private int _fontSize = 16;

        private RectTransform _container;
        private TMP_Text[] _poolTexts;
        private RectTransform[] _poolRects;
        private CanvasGroup[] _poolGroups;
        private float[] _timers;
        private int[] _comboCounts;
        private bool[] _isActive;
        private int _nextIndex;

        private static readonly Color ColorComboLow = new Color(1f, 0.6f, 0f, 1f);
        private static readonly Color ColorComboHigh = new Color(1f, 0.1f, 0.1f, 1f);

        private void Awake()
        {
            EnsureComponents();
            _container = GetComponent<RectTransform>();
            if (_container == null)
                _container = gameObject.AddComponent<RectTransform>() as RectTransform;

            InitPool();
        }

        private void OnEnable()
        {
            EventBus<ComboEvent>.Subscribe(OnCombo);
        }

        private void OnDisable()
        {
            EventBus<ComboEvent>.Unsubscribe(OnCombo);
        }

        private void InitPool()
        {
            _poolTexts = new TMP_Text[_maxEntries];
            _poolRects = new RectTransform[_maxEntries];
            _poolGroups = new CanvasGroup[_maxEntries];
            _timers = new float[_maxEntries];
            _comboCounts = new int[_maxEntries];
            _isActive = new bool[_maxEntries];

            for (int i = 0; i < _maxEntries; i++)
            {
                var entryObj = new GameObject($"ComboEntry_{i}");
                entryObj.transform.SetParent(_container, false);

                var rect = entryObj.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(200f, _entryHeight);
                rect.anchoredPosition = new Vector2(0f, -i * _entryHeight);

                var group = entryObj.AddComponent<CanvasGroup>();
                group.alpha = 0f;

                var tmp = entryObj.AddComponent<TextMeshProUGUI>();
                tmp.fontSize = _fontSize;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Left;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.raycastTarget = false;

                _poolTexts[i] = tmp;
                _poolRects[i] = rect;
                _poolGroups[i] = group;
                _isActive[i] = false;

                entryObj.SetActive(false);
            }
        }

        private void OnCombo(ComboEvent evt)
        {
            if (evt.ComboCount < 2) return;

            // 팝업/패널이 열려있으면 콤보 표시 억제
            if (UIState.IsAnyPanelOpen) return;

            // 이전 활성 항목을 모두 즉시 숨김 — 최신 콤보만 표시
            DismissAllEntries();

            int idx = _nextIndex % _maxEntries;

            // 이전 트윈 정리
            DOTween.Kill(_poolRects[idx]);
            DOTween.Kill(_poolGroups[idx]);

            _comboCounts[idx] = evt.ComboCount;
            _timers[idx] = _displayDuration;
            _isActive[idx] = true;

            // 콤보 수에 따른 색상 (low=오렌지 → high=빨강, 20 이상이면 최대)
            float t = Mathf.Clamp01(evt.ComboCount / 20f);
            Color entryColor = Color.Lerp(ColorComboLow, ColorComboHigh, t);

            // 콤보 수에 따른 스케일 (1.0 ~ 1.5)
            float scale = Mathf.Min(1f + evt.ComboCount * 0.02f, 1.5f);

            var text = _poolTexts[idx];
            text.text = $"{evt.ComboCount} 콤보!";
            text.color = entryColor;
            text.fontSize = _fontSize * scale;

            var rect = _poolRects[idx];
            rect.anchoredPosition = new Vector2(_slideInOffset, 0f);
            rect.gameObject.SetActive(true);
            rect.localScale = Vector3.one;

            var group = _poolGroups[idx];
            group.alpha = 0f;

            // 슬라이드 인 + 페이드 인
            DOTween.To(() => rect.anchoredPosition, v => rect.anchoredPosition = v, new Vector2(10f, 0f), _slideInDuration).SetEase(Ease.OutBack).SetLink(gameObject);
            DOTween.To(() => group.alpha, a => group.alpha = a, 1f, _slideInDuration).SetLink(gameObject);

            _nextIndex++;
        }

        /// <summary>
        /// 모든 활성 항목을 즉시 숨김 (새 콤보가 이전 것을 대체).
        /// </summary>
        private void DismissAllEntries()
        {
            for (int i = 0; i < _maxEntries; i++)
            {
                if (!_isActive[i]) continue;

                _isActive[i] = false;
                DOTween.Kill(_poolRects[i]);
                DOTween.Kill(_poolGroups[i]);
                _poolGroups[i].alpha = 0f;
                _poolRects[i].gameObject.SetActive(false);
            }
        }

        private void ShiftExistingEntries()
        {
            // 활성 항목들을 위로 한 칸씩 이동
            for (int i = 0; i < _maxEntries; i++)
            {
                if (!_isActive[i]) continue;

                var rect = _poolRects[i];
                DOTween.Kill(rect);
                float targetY = rect.anchoredPosition.y - _entryHeight;
                DOTween.To(() => rect.anchoredPosition, v => rect.anchoredPosition = v, new Vector2(rect.anchoredPosition.x, targetY), 0.15f).SetEase(Ease.OutQuad).SetLink(gameObject);

                // 오래된 항목(화면 밖으로 나간 것)은 즉시 퇴장
                if (targetY < -(_maxEntries * _entryHeight))
                {
                    FadeOutEntry(i, 0.1f);
                }
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // 패널이 열려있으면 활성 콤보 엔트리를 즉시 숨김
            if (UIState.IsAnyPanelOpen)
            {
                DismissAllEntries();
                return;
            }

            for (int i = 0; i < _maxEntries; i++)
            {
                if (!_isActive[i]) continue;

                _timers[i] -= dt;
                if (_timers[i] <= 0f)
                {
                    FadeOutEntry(i, _fadeOutDuration);
                }
            }
        }

        private void FadeOutEntry(int idx, float duration)
        {
            _isActive[idx] = false;

            var rect = _poolRects[idx];
            var group = _poolGroups[idx];

            DOTween.Kill(rect);
            DOTween.Kill(group);

            // 위로 슬라이드 + 페이드 아웃
            float targetY = rect.anchoredPosition.y - _entryHeight;
            DOTween.To(() => rect.anchoredPosition, v => rect.anchoredPosition = v, new Vector2(rect.anchoredPosition.x, targetY), duration).SetEase(Ease.InQuad).SetLink(gameObject);
            DOTween.To(() => group.alpha, a => group.alpha = a, 0f, duration)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    rect.gameObject.SetActive(false);
                });
        }

        private void EnsureComponents()
        {
            if (GetComponentInParent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 5;
                gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
                gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            var rt = transform as RectTransform;
            if (rt == null)
                rt = gameObject.AddComponent<RectTransform>() as RectTransform;

            // 좌측 상단 고정
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(10f, -215f);
            rt.sizeDelta = new Vector2(200f, _maxEntries * _entryHeight + 8f);

            // 배경 없음 — 텍스트만 표시
            var bg = GetComponent<Image>();
            if (bg != null)
            {
                bg.enabled = false;
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _maxEntries; i++)
            {
                if (_poolRects != null && _poolRects[i] != null)
                    DOTween.Kill(_poolRects[i]);
                if (_poolGroups != null && _poolGroups[i] != null)
                    DOTween.Kill(_poolGroups[i]);
            }
        }
    }
}
