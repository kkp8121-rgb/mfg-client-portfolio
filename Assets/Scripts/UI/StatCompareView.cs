using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MkLike.UI
{
    /// <summary>
    /// 공용 스탯 전후 비교 컴포넌트.
    /// 장비/무기/동료 등 다양한 곳에서 재사용한다.
    /// EquipComparePopup에서 비교 로직을 추출하여 공용화.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class StatCompareView : MonoBehaviour
    {
        [Header("UI 참조")]
        [SerializeField] private RectTransform _popupRoot;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Transform _rowContainer;
        [SerializeField] private TMP_Text _cpBeforeText;
        [SerializeField] private TMP_Text _cpAfterText;
        [SerializeField] private TMP_Text _cpDeltaText;
        [SerializeField] private Button _closeButton;

        [Header("색상")]
        [SerializeField] private Color _increaseColor = new Color(0.2f, 0.9f, 0.3f);
        [SerializeField] private Color _decreaseColor = new Color(0.9f, 0.2f, 0.2f);
        [SerializeField] private Color _neutralColor = new Color(0.7f, 0.7f, 0.7f);

        [Header("설정")]
        [SerializeField] private float _autoCloseDelay = 4f;
        [SerializeField] private float _rowHeight = 36f;

        private CanvasGroup _canvasGroup;
        private Sequence _showSeq;
        private Tween _autoCloseTween;

        private static readonly Color RowBgDark = new(0.14f, 0.13f, 0.12f, 0.8f);
        private static readonly Color RowBgLight = new(0.18f, 0.17f, 0.15f, 0.8f);
        private static readonly Color TextWhite = new(0.95f, 0.93f, 0.88f, 1f);

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Hide);

            HideImmediate();
        }

        /// <summary>
        /// 스탯 비교를 표시한다. 외부에서 호출하는 메인 API.
        /// </summary>
        public void Show(StatDelta[] deltas, long cpBefore, long cpAfter, string title = null)
        {
            KillTweens();

            if (_titleText != null)
                _titleText.text = title ?? "스탯 비교";

            BuildRows(deltas);
            UpdateCpDisplay(cpBefore, cpAfter);
            PlayShowAnimation(deltas);

            Core.AudioManager.Instance?.PlayUiSfx("sfx_ui_compare");

            _autoCloseTween = DOVirtual.DelayedCall(_autoCloseDelay, () => Hide(), true)
                .SetLink(gameObject);
        }

        /// <summary>
        /// 비교 뷰를 숨긴다.
        /// </summary>
        public void Hide()
        {
            KillTweens();

            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            var root = _popupRoot != null ? _popupRoot : (RectTransform)transform;

            DOTween.Sequence()
                .Append(DOTween.To(() => _canvasGroup.alpha, a => _canvasGroup.alpha = a, 0f, 0.15f))
                .Join(root.DOScale(0.9f, 0.12f).SetEase(Ease.InBack))
                .OnComplete(() =>
                {
                    root.gameObject.SetActive(false);
                    root.localScale = Vector3.one;
                })
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void BuildRows(StatDelta[] deltas)
        {
            if (_rowContainer == null) return;

            // 기존 행 제거
            for (int i = _rowContainer.childCount - 1; i >= 0; i--)
                Destroy(_rowContainer.GetChild(i).gameObject);

            if (deltas == null) return;

            for (int i = 0; i < deltas.Length; i++)
            {
                var delta = deltas[i];
                int diff = delta.After - delta.Before;
                Color rowBg = i % 2 == 0 ? RowBgDark : RowBgLight;

                var row = new GameObject($"Row_{i}");
                row.transform.SetParent(_rowContainer, false);
                var rowRT = row.AddComponent<RectTransform>();
                rowRT.sizeDelta = new Vector2(0, _rowHeight);

                var rowImg = row.AddComponent<Image>();
                rowImg.color = rowBg;

                var hlg = row.AddComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.MiddleLeft;
                hlg.childControlWidth = false;
                hlg.childControlHeight = true;
                hlg.childForceExpandHeight = true;
                hlg.spacing = 4;
                hlg.padding = new RectOffset(8, 8, 2, 2);

                // 스탯명
                CreateText(row.transform, delta.StatName, 14, TextWhite, 80, TextAlignmentOptions.Left);

                // Before
                CreateText(row.transform, delta.Before.ToString("N0"), 14, _neutralColor, 70, TextAlignmentOptions.Right);

                // 화살표
                var arrowObj = new GameObject("Arrow");
                arrowObj.transform.SetParent(row.transform, false);
                arrowObj.AddComponent<RectTransform>().sizeDelta = new Vector2(20, _rowHeight);
                var arrowTmp = arrowObj.AddComponent<TextMeshProUGUI>();
                arrowTmp.fontSize = 16;
                arrowTmp.alignment = TextAlignmentOptions.Center;
                if (diff > 0)
                {
                    arrowTmp.text = "\u25B2"; // ▲
                    arrowTmp.color = _increaseColor;
                }
                else if (diff < 0)
                {
                    arrowTmp.text = "\u25BC"; // ▼
                    arrowTmp.color = _decreaseColor;
                }
                else
                {
                    arrowTmp.text = "-";
                    arrowTmp.color = _neutralColor;
                    arrowObj.SetActive(false);
                }

                // After
                Color afterColor = diff > 0 ? _increaseColor : diff < 0 ? _decreaseColor : TextWhite;
                CreateText(row.transform, delta.After.ToString("N0"), 14, afterColor, 70, TextAlignmentOptions.Right);

                // Delta
                var deltaObj = new GameObject("Delta");
                deltaObj.transform.SetParent(row.transform, false);
                deltaObj.AddComponent<RectTransform>().sizeDelta = new Vector2(60, _rowHeight);
                var deltaTmp = deltaObj.AddComponent<TextMeshProUGUI>();
                deltaTmp.fontSize = 14;
                deltaTmp.fontStyle = FontStyles.Bold;
                deltaTmp.alignment = TextAlignmentOptions.Right;
                if (diff > 0)
                {
                    deltaTmp.text = $"+{diff:N0}";
                    deltaTmp.color = _increaseColor;
                }
                else if (diff < 0)
                {
                    deltaTmp.text = $"{diff:N0}";
                    deltaTmp.color = _decreaseColor;
                }
                else
                {
                    deltaTmp.text = "0";
                    deltaTmp.color = _neutralColor;
                }

                // CanvasGroup (순차 등장용)
                var cg = row.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
            }
        }

        private void UpdateCpDisplay(long cpBefore, long cpAfter)
        {
            long cpDelta = cpAfter - cpBefore;

            if (_cpBeforeText != null)
                _cpBeforeText.text = $"{cpBefore:N0}";

            if (_cpAfterText != null)
                _cpAfterText.text = $"{cpAfter:N0}";

            if (_cpDeltaText != null)
            {
                if (cpDelta > 0)
                {
                    string hex = ColorUtility.ToHtmlStringRGB(_increaseColor);
                    _cpDeltaText.text = $"<color=#{hex}>+{cpDelta:N0}</color>";
                }
                else if (cpDelta < 0)
                {
                    string hex = ColorUtility.ToHtmlStringRGB(_decreaseColor);
                    _cpDeltaText.text = $"<color=#{hex}>{cpDelta:N0}</color>";
                }
                else
                {
                    _cpDeltaText.text = "0";
                }
            }
        }

        private void PlayShowAnimation(StatDelta[] deltas)
        {
            var root = _popupRoot != null ? _popupRoot : (RectTransform)transform;
            root.gameObject.SetActive(true);
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            root.localScale = Vector3.one * 0.85f;

            _showSeq = DOTween.Sequence()
                .Append(DOTween.To(() => _canvasGroup.alpha, a => _canvasGroup.alpha = a, 1f, 0.2f))
                .Join(root.DOScale(1f, 0.25f).SetEase(Ease.OutBack))
                .OnComplete(() =>
                {
                    _canvasGroup.interactable = true;
                    _canvasGroup.blocksRaycasts = true;
                })
                .SetUpdate(true)
                .SetLink(gameObject);

            // 순차 행 등장
            if (_rowContainer != null && deltas != null)
            {
                for (int i = 0; i < _rowContainer.childCount && i < deltas.Length; i++)
                {
                    var rowObj = _rowContainer.GetChild(i).gameObject;
                    var cg = rowObj.GetComponent<CanvasGroup>();
                    if (cg == null) continue;

                    float delay = 0.1f + i * 0.08f;
                    DOTween.To(() => cg.alpha, a => cg.alpha = a, 1f, 0.2f)
                        .SetDelay(delay)
                        .SetUpdate(true)
                        .SetLink(gameObject);

                    // 증감이 있는 행은 델타 텍스트 펀치
                    int diff = deltas[i].After - deltas[i].Before;
                    if (diff != 0)
                    {
                        var deltaTextTf = rowObj.transform.Find("Delta");
                        if (deltaTextTf != null)
                        {
                            deltaTextTf.DOPunchScale(Vector3.one * 0.3f, 0.3f, 6, 0.5f)
                                .SetDelay(delay + 0.15f)
                                .SetUpdate(true)
                                .SetLink(gameObject);
                        }
                    }
                }
            }

            // CP 펀치
            long cpDelta = deltas != null ? 0 : 0; // dummy
            if (_cpAfterText != null)
            {
                _cpAfterText.transform.DOPunchScale(Vector3.one * 0.2f, 0.4f, 6, 0.5f)
                    .SetDelay(0.5f)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }
        }

        private void HideImmediate()
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            var root = _popupRoot != null ? _popupRoot : (RectTransform)transform;
            root.gameObject.SetActive(false);
        }

        private void KillTweens()
        {
            _showSeq?.Kill();
            _showSeq = null;
            _autoCloseTween?.Kill();
            _autoCloseTween = null;
        }

        private static void CreateText(Transform parent, string text, float fontSize, Color color,
            float width, TextAlignmentOptions alignment)
        {
            var obj = new GameObject("Text");
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>().sizeDelta = new Vector2(width, 0);
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
        }

        private void OnDestroy()
        {
            KillTweens();
            var root = _popupRoot != null ? _popupRoot : (RectTransform)transform;
            root.DOKill();
        }
    }
}
