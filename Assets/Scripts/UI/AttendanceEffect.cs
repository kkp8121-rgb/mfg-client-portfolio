using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// 출석 체크 스탬프/보상 연출.
    /// AttendanceCheckedEvent 구독하여 스탬프 찍기 + 보상 텍스트 + 7일 완주 연출.
    /// GF-14: 스탬프 드롭 + ScreenShake + "Day X 출석!" 금색 텍스트 + 보상 표시 + 2초 페이드아웃.
    /// </summary>
    public class AttendanceEffect : MonoBehaviour
    {
        [SerializeField] private Image _stampImage;
        [SerializeField] private TMP_Text _rewardText;
        [SerializeField] private TMP_Text _dayText;
        [SerializeField] private CanvasGroup _rootCanvasGroup;
        [SerializeField] private GameObject _specialEffectObject;
        [SerializeField] private int _completionDay = 7;

        [Header("연출 설정")]
        [SerializeField] private float _stampDropHeight = 200f;
        [SerializeField] private float _stampDropDuration = 0.35f;
        [SerializeField] private float _fadeOutDelay = 2f;
        [SerializeField] private float _fadeOutDuration = 0.5f;

        private Sequence _mainSequence;

        private void Awake()
        {
            EnsureComponents();
        }

        private void OnEnable()
        {
            EventBus<AttendanceCheckedEvent>.Subscribe(OnAttendanceChecked);
        }

        private void OnDisable()
        {
            EventBus<AttendanceCheckedEvent>.Unsubscribe(OnAttendanceChecked);
        }

        private void OnAttendanceChecked(AttendanceCheckedEvent evt)
        {
            gameObject.SetActive(true);
            _mainSequence?.Kill();

            if (_rootCanvasGroup != null)
                _rootCanvasGroup.alpha = 1f;

            PlayStampDrop(evt);
            PlayDayText(evt);
            PlayRewardEffect(evt);

            if (evt.Day >= _completionDay)
                PlayCompletionEffect();

            ScheduleFadeOut();
        }

        private void PlayStampDrop(AttendanceCheckedEvent evt)
        {
            if (_stampImage == null) return;

            var rt = _stampImage.rectTransform;
            var targetPos = rt.anchoredPosition;
            rt.anchoredPosition = new Vector2(targetPos.x, targetPos.y + _stampDropHeight);
            _stampImage.transform.localScale = Vector3.one * 1.5f;

            DOTween.To(
                () => rt.anchoredPosition,
                v => rt.anchoredPosition = v,
                targetPos,
                _stampDropDuration
            ).SetEase(Ease.InQuad).SetUpdate(true).SetLink(gameObject);

            _stampImage.transform.DOScale(1f, _stampDropDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    _stampImage.transform.DOPunchScale(Vector3.one * 0.3f, 0.25f, 8, 0.5f)
                        .SetUpdate(true)
                        .SetLink(gameObject);

                    if (Combat.ScreenShakeManager.Instance != null)
                        Combat.ScreenShakeManager.Instance.ShakeMedium();

                    if (AudioManager.Instance != null)
                        AudioManager.Instance.PlaySfx(SfxType.UiReward);
                });
        }

        private void PlayDayText(AttendanceCheckedEvent evt)
        {
            if (_dayText == null) return;

            _dayText.text = $"Day {evt.Day} 출석!";
            _dayText.color = new Color(1f, 0.85f, 0.2f, 1f);
            _dayText.transform.localScale = Vector3.zero;

            _dayText.transform.DOScale(1f, 0.3f)
                .SetEase(Ease.OutBack)
                .SetDelay(0.2f)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void PlayRewardEffect(AttendanceCheckedEvent evt)
        {
            if (_rewardText == null) return;

            _rewardText.text = $"+{NumberFormatter.FormatKorean(evt.RewardAmount)} {evt.RewardType}";
            _rewardText.transform.localScale = Vector3.zero;

            var canvasGroup = _rewardText.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                DOTween.To(() => canvasGroup.alpha, x => canvasGroup.alpha = x, 1f, 0.25f)
                    .SetDelay(0.35f)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }

            _rewardText.transform.DOScale(1f, 0.3f)
                .SetEase(Ease.OutBack)
                .SetDelay(0.35f)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void PlayCompletionEffect()
        {
            if (_specialEffectObject == null) return;

            _specialEffectObject.SetActive(true);
            _specialEffectObject.transform.localScale = Vector3.one;

            DOTween.Sequence()
                .Append(_specialEffectObject.transform.DOScale(1.15f, 0.4f).SetEase(Ease.InOutSine))
                .Append(_specialEffectObject.transform.DOScale(1f, 0.4f).SetEase(Ease.InOutSine))
                .SetLoops(-1)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void ScheduleFadeOut()
        {
            if (_rootCanvasGroup == null) return;

            _mainSequence = DOTween.Sequence()
                .AppendInterval(_fadeOutDelay)
                .Append(
                    DOTween.To(
                        () => _rootCanvasGroup.alpha,
                        x => _rootCanvasGroup.alpha = x,
                        0f,
                        _fadeOutDuration
                    )
                )
                .SetUpdate(true)
                .SetLink(gameObject)
                .OnComplete(() => gameObject.SetActive(false));
        }

        private void EnsureComponents()
        {
            if (GetComponentInParent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 210;
                gameObject.AddComponent<CanvasScaler>();
                gameObject.AddComponent<GraphicRaycaster>();
            }

            // --- Root: full-screen dimmed overlay ---
            var rootRt = GetComponent<RectTransform>();
            if (rootRt != null)
            {
                rootRt.anchorMin = Vector2.zero;
                rootRt.anchorMax = Vector2.one;
                rootRt.offsetMin = Vector2.zero;
                rootRt.offsetMax = Vector2.zero;
            }

            var rootImg = GetComponent<Image>();
            if (rootImg == null)
            {
                rootImg = gameObject.AddComponent<Image>();
                rootImg.color = new Color(0f, 0f, 0f, 0.7f);
                rootImg.raycastTarget = true;
            }

            if (_rootCanvasGroup == null)
            {
                _rootCanvasGroup = GetComponent<CanvasGroup>();
                if (_rootCanvasGroup == null) _rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // --- Popup panel: centered dark background ---
            Transform panelTransform;
            var existingPanel = transform.Find("AttendancePanel");
            if (existingPanel != null)
            {
                panelTransform = existingPanel;
            }
            else
            {
                var panelGo = new GameObject("AttendancePanel", typeof(RectTransform));
                panelGo.transform.SetParent(transform, false);
                var panelImg = panelGo.AddComponent<Image>();
                panelImg.color = Color.white;
                var tm = UIThemeManager.Instance;
                if (tm != null) tm.ApplyPanelBackground(panelImg);
                else panelImg.color = new Color(0.12f, 0.1f, 0.2f, 0.95f);
                panelImg.raycastTarget = false;
                var panelRt = panelGo.GetComponent<RectTransform>();
                panelRt.anchorMin = new Vector2(0.5f, 0.5f);
                panelRt.anchorMax = new Vector2(0.5f, 0.5f);
                panelRt.pivot = new Vector2(0.5f, 0.5f);
                panelRt.anchoredPosition = Vector2.zero;
                panelRt.sizeDelta = new Vector2(380f, 340f);
                panelTransform = panelGo.transform;
            }

            // --- Title text ---
            var titleTransform = panelTransform.Find("TitleText");
            if (titleTransform == null)
            {
                var go = new GameObject("TitleText", typeof(RectTransform));
                go.transform.SetParent(panelTransform, false);
                var tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.text = "출석 체크";
                tmp.fontSize = 28f;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -12f);
                rt.sizeDelta = new Vector2(0f, 50f);
            }

            // --- Stamp image: centered in panel ---
            if (_stampImage == null)
            {
                var go = new GameObject("StampImage", typeof(RectTransform));
                go.transform.SetParent(panelTransform, false);
                _stampImage = go.AddComponent<Image>();
                _stampImage.color = new Color(0.9f, 0.2f, 0.2f, 0.9f);
                _stampImage.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, 20f);
                rt.sizeDelta = new Vector2(90f, 90f);
            }

            // --- Day text: above stamp ---
            if (_dayText == null)
            {
                var go = new GameObject("DayText", typeof(RectTransform));
                go.transform.SetParent(panelTransform, false);
                _dayText = go.AddComponent<TextMeshProUGUI>();
                _dayText.fontSize = 30f;
                _dayText.fontStyle = FontStyles.Bold;
                _dayText.alignment = TextAlignmentOptions.Center;
                _dayText.color = new Color(1f, 0.85f, 0.2f);
                _dayText.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -66f);
                rt.sizeDelta = new Vector2(0f, 44f);
            }

            // --- Reward text: below stamp ---
            if (_rewardText == null)
            {
                var go = new GameObject("RewardText", typeof(RectTransform));
                go.transform.SetParent(panelTransform, false);
                _rewardText = go.AddComponent<TextMeshProUGUI>();
                _rewardText.fontSize = 22f;
                _rewardText.alignment = TextAlignmentOptions.Center;
                _rewardText.color = Color.white;
                _rewardText.raycastTarget = false;
                var cg = go.AddComponent<CanvasGroup>();
                cg.alpha = 1f;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 60f);
                rt.sizeDelta = new Vector2(0f, 40f);
            }

            // --- Special effect object: fills panel for 7-day completion ---
            if (_specialEffectObject == null)
            {
                var go = new GameObject("SpecialEffect", typeof(RectTransform));
                go.transform.SetParent(panelTransform, false);
                var img = go.AddComponent<Image>();
                img.color = new Color(1f, 0.85f, 0.2f, 0.15f);
                img.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                _specialEffectObject = go;
                go.SetActive(false);
            }
        }

        private GameObject CreateUIChild(string childName)
        {
            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            return go;
        }

        private void OnDestroy()
        {
            _mainSequence?.Kill();
            transform.DOKill();
            if (_stampImage != null) _stampImage.transform.DOKill();
            if (_rewardText != null) _rewardText.transform.DOKill();
            if (_dayText != null) _dayText.transform.DOKill();
            if (_specialEffectObject != null) _specialEffectObject.transform.DOKill();
        }
    }
}
