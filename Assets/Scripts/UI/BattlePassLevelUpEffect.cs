using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// 배틀패스 레벨업 연출.
    /// BattlePassLevelUpEvent 구독 → 진행 바 채움 + "Level UP!" 텍스트 + 마일스톤 강조.
    /// </summary>
    public class BattlePassLevelUpEffect : MonoBehaviour
    {
        [SerializeField] private Image _progressFill;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private TMP_Text _levelUpBanner;
        [SerializeField] private CanvasGroup _rootCanvasGroup;
        [SerializeField] private Transform _milestoneFlash;

        [Header("연출 설정")]
        [SerializeField] private float _fillDuration = 0.5f;
        [SerializeField] private float _bannerStayDuration = 1.5f;
        [SerializeField] private float _fadeOutDuration = 0.4f;

        private Sequence _mainSequence;

        private void Awake()
        {
            EnsureComponents();

            // 초기에는 숨김 — BattlePassLevelUpEvent 수신 시에만 표시
            if (_rootCanvasGroup != null)
            {
                _rootCanvasGroup.alpha = 0f;
                _rootCanvasGroup.interactable = false;
                _rootCanvasGroup.blocksRaycasts = false;
            }
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            EventBus<BattlePassLevelUpEvent>.Subscribe(OnBattlePassLevelUp);
        }

        private void OnDisable()
        {
            EventBus<BattlePassLevelUpEvent>.Unsubscribe(OnBattlePassLevelUp);
        }

        private void OnBattlePassLevelUp(BattlePassLevelUpEvent evt)
        {
            gameObject.SetActive(true);
            _mainSequence?.Kill();

            if (_rootCanvasGroup != null)
                _rootCanvasGroup.alpha = 1f;

            PlayFillAnimation();
            PlayLevelText(evt.NewLevel);
            PlayLevelUpBanner(evt.IsMilestone);

            if (evt.IsMilestone)
                PlayMilestoneFlash();

            ScheduleFadeOut();
        }

        private void PlayFillAnimation()
        {
            if (_progressFill == null) return;

            float startFill = _progressFill.fillAmount;
            DOTween.To(
                () => _progressFill.fillAmount,
                x => _progressFill.fillAmount = x,
                1f,
                _fillDuration
            ).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject)
            .OnComplete(() =>
            {
                _progressFill.fillAmount = 0f;
            });
        }

        private void PlayLevelText(int level)
        {
            if (_levelText == null) return;

            _levelText.text = $"Lv.{level}";
            _levelText.transform.localScale = Vector3.one * 1.5f;

            _levelText.transform.DOScale(1f, 0.3f)
                .SetEase(Ease.OutBack)
                .SetDelay(_fillDuration * 0.8f)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void PlayLevelUpBanner(bool isMilestone)
        {
            if (_levelUpBanner == null) return;

            _levelUpBanner.text = isMilestone ? "MILESTONE!" : "Level UP!";
            _levelUpBanner.color = isMilestone
                ? new Color(1f, 0.7f, 0.1f, 1f)
                : new Color(0.3f, 0.9f, 1f, 1f);

            _levelUpBanner.transform.localScale = Vector3.zero;

            _levelUpBanner.transform.DOScale(1f, 0.35f)
                .SetEase(Ease.OutBack)
                .SetDelay(_fillDuration)
                .SetUpdate(true)
                .SetLink(gameObject);

            _levelUpBanner.transform.DOPunchScale(Vector3.one * 0.15f, 0.2f, 6, 0.5f)
                .SetDelay(_fillDuration + 0.35f)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void PlayMilestoneFlash()
        {
            if (_milestoneFlash == null) return;

            _milestoneFlash.gameObject.SetActive(true);
            _milestoneFlash.localScale = Vector3.zero;

            _milestoneFlash.DOScale(1.2f, 0.3f)
                .SetEase(Ease.OutBack)
                .SetDelay(_fillDuration + 0.2f)
                .SetUpdate(true)
                .SetLink(gameObject);

            _milestoneFlash.DOScale(1f, 0.2f)
                .SetDelay(_fillDuration + 0.5f)
                .SetUpdate(true)
                .SetLink(gameObject);

            if (Combat.ScreenShakeManager.Instance != null)
                Combat.ScreenShakeManager.Instance.ShakeLight();
        }

        private void ScheduleFadeOut()
        {
            if (_rootCanvasGroup == null) return;

            float totalDelay = _fillDuration + _bannerStayDuration;

            _mainSequence = DOTween.Sequence()
                .AppendInterval(totalDelay)
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
                .OnComplete(() =>
                {
                    if (_milestoneFlash != null)
                        _milestoneFlash.gameObject.SetActive(false);
                    gameObject.SetActive(false);
                });
        }

        private void EnsureComponents()
        {
            if (GetComponentInParent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 210;
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                gameObject.AddComponent<GraphicRaycaster>();
            }

            if (_rootCanvasGroup == null)
            {
                _rootCanvasGroup = GetComponent<CanvasGroup>();
                if (_rootCanvasGroup == null) _rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // Semi-transparent overlay (effect/toast style - no input blocking)
            var overlay = CreateUIChild("Overlay");
            var overlayRt = overlay.GetComponent<RectTransform>();
            SetStretchAll(overlayRt);
            var overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.3f);
            overlayImg.raycastTarget = false;

            // Centered effect container
            var container = new GameObject("EffectContainer", typeof(RectTransform));
            container.transform.SetParent(overlay.transform, false);
            var containerRt = container.GetComponent<RectTransform>();
            containerRt.anchorMin = new Vector2(0.5f, 0.5f);
            containerRt.anchorMax = new Vector2(0.5f, 0.5f);
            containerRt.pivot = new Vector2(0.5f, 0.5f);
            containerRt.sizeDelta = new Vector2(400f, 160f);
            containerRt.anchoredPosition = new Vector2(0f, 100f);
            var containerBg = container.AddComponent<Image>();
            var tm = UIThemeManager.Instance;
            if (tm != null)
                tm.ApplyFrameBackground(containerBg);
            else
                containerBg.color = new Color(0.08f, 0.06f, 0.15f, 0.85f);

            var vlg = container.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(20, 20, 16, 16);

            // Level Up banner (top, large)
            if (_levelUpBanner == null)
            {
                var go = CreateLayoutChild(container.transform, "LevelUpBanner", 44f);
                _levelUpBanner = go.AddComponent<TextMeshProUGUI>();
                _levelUpBanner.fontSize = 32f;
                _levelUpBanner.alignment = TextAlignmentOptions.Center;
                _levelUpBanner.color = new Color(0.3f, 0.9f, 1f);
                _levelUpBanner.fontStyle = FontStyles.Bold;
            }

            // Level text
            if (_levelText == null)
            {
                var go = CreateLayoutChild(container.transform, "LevelText", 30f);
                _levelText = go.AddComponent<TextMeshProUGUI>();
                _levelText.fontSize = 24f;
                _levelText.alignment = TextAlignmentOptions.Center;
                _levelText.color = Color.white;
                _levelText.fontStyle = FontStyles.Bold;
            }

            // Progress bar
            if (_progressFill == null)
            {
                var barContainer = CreateLayoutChild(container.transform, "ProgressBarContainer", 18f);
                var barBg = barContainer.AddComponent<Image>();
                barBg.color = new Color(0.15f, 0.15f, 0.25f);

                var fillGo = new GameObject("ProgressFill", typeof(RectTransform));
                fillGo.transform.SetParent(barContainer.transform, false);
                var fillRt = fillGo.GetComponent<RectTransform>();
                SetStretchAll(fillRt);
                _progressFill = fillGo.AddComponent<Image>();
                _progressFill.sprite = CreateWhiteSprite();
                _progressFill.type = Image.Type.Filled;
                _progressFill.fillMethod = Image.FillMethod.Horizontal;
                _progressFill.fillAmount = 0f;
                _progressFill.color = new Color(0.3f, 0.7f, 1f);
            }

            // Milestone flash (overlay effect)
            if (_milestoneFlash == null)
            {
                var go = CreateUIChild("MilestoneFlash");
                _milestoneFlash = go.transform;
                var flashRt = go.GetComponent<RectTransform>();
                flashRt.anchorMin = new Vector2(0.5f, 0.5f);
                flashRt.anchorMax = new Vector2(0.5f, 0.5f);
                flashRt.pivot = new Vector2(0.5f, 0.5f);
                flashRt.sizeDelta = new Vector2(500f, 200f);
                flashRt.anchoredPosition = new Vector2(0f, 100f);
                var flashImg = go.AddComponent<Image>();
                flashImg.color = new Color(1f, 0.85f, 0.1f, 0.3f);
                flashImg.raycastTarget = false;
                go.SetActive(false);
            }
        }

        private GameObject CreateUIChild(string childName)
        {
            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            return go;
        }

        private static GameObject CreateLayoutChild(Transform parent, string childName, float height)
        {
            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            return go;
        }

        private static Sprite CreateWhiteSprite()
        {
            var tex = Texture2D.whiteTexture;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
        }

        private static void SetStretchAll(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        private void OnDestroy()
        {
            _mainSequence?.Kill();
            transform.DOKill();
            if (_levelText != null) _levelText.transform.DOKill();
            if (_levelUpBanner != null) _levelUpBanner.transform.DOKill();
            if (_milestoneFlash != null) _milestoneFlash.DOKill();
            if (_progressFill != null) DOTween.Kill(_progressFill);
        }
    }
}
