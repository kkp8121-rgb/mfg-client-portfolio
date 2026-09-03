using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MkLike.Core;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// 스킬 해금 연출.
    /// SkillLearnedEvent 구독 → 스킬 아이콘 회전 + 발광 + "새 스킬 해금!" 텍스트.
    /// </summary>
    public class SkillUnlockEffect : MonoBehaviour
    {
        [SerializeField] private Image _skillIcon;
        [SerializeField] private Image _glowImage;
        [SerializeField] private TMP_Text _unlockText;
        [SerializeField] private TMP_Text _skillNameText;
        [SerializeField] private CanvasGroup _rootCanvasGroup;

        [Header("연출 설정")]
        [SerializeField] private float _spinDuration = 0.6f;
        [SerializeField] private float _stayDuration = 2f;
        [SerializeField] private float _fadeOutDuration = 0.4f;
        [SerializeField] private int _spinRotations = 2;

        private Sequence _mainSequence;

        private void Awake()
        {
            EnsureComponents();
        }

        private void OnEnable()
        {
            EventBus<SkillLearnedEvent>.Subscribe(OnSkillLearned);
        }

        private void OnDisable()
        {
            EventBus<SkillLearnedEvent>.Unsubscribe(OnSkillLearned);
        }

        private void OnSkillLearned(SkillLearnedEvent evt)
        {
            gameObject.SetActive(true);
            _mainSequence?.Kill();

            if (_rootCanvasGroup != null)
                _rootCanvasGroup.alpha = 1f;

            PlayIconSpin();
            PlayGlow();
            PlayUnlockText();
            PlaySkillName(evt.SkillName);
            ScheduleFadeOut();

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(SfxType.UiReward);
        }

        private void PlayIconSpin()
        {
            if (_skillIcon == null) return;

            _skillIcon.transform.localScale = Vector3.zero;
            _skillIcon.transform.localRotation = Quaternion.identity;

            _skillIcon.transform.DOScale(1f, _spinDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .SetLink(gameObject);

            _skillIcon.transform.DORotate(
                new Vector3(0f, 0f, -360f * _spinRotations),
                _spinDuration,
                RotateMode.FastBeyond360
            ).SetEase(Ease.OutCubic).SetUpdate(true).SetLink(gameObject);
        }

        private void PlayGlow()
        {
            if (_glowImage == null) return;

            _glowImage.gameObject.SetActive(true);
            _glowImage.transform.localScale = Vector3.zero;
            _glowImage.transform.localRotation = Quaternion.identity;

            var glowColor = _glowImage.color;
            glowColor.a = 0f;
            _glowImage.color = glowColor;

            // 스케일 업 (펄스 느낌)
            _glowImage.transform.DOScale(1.2f, _spinDuration * 0.8f)
                .SetEase(Ease.OutQuad)
                .SetDelay(_spinDuration * 0.5f)
                .SetUpdate(true)
                .SetLink(gameObject);

            // 페이드 인
            DOTween.To(
                () => _glowImage.color.a,
                a =>
                {
                    var c = _glowImage.color;
                    c.a = a;
                    _glowImage.color = c;
                },
                0.5f,
                _spinDuration * 0.5f
            ).SetDelay(_spinDuration * 0.5f).SetUpdate(true).SetLink(gameObject);

            // 페이드 아웃
            DOTween.To(
                () => _glowImage.color.a,
                a =>
                {
                    var c = _glowImage.color;
                    c.a = a;
                    _glowImage.color = c;
                },
                0f,
                0.6f
            ).SetDelay(_spinDuration + 0.5f).SetUpdate(true).SetLink(gameObject);

            // 펄스 (스케일 반복) — 회전 대신
            _glowImage.transform.DOScale(1.4f, 0.8f)
                .SetLoops(4, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetDelay(_spinDuration * 0.8f)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void PlayUnlockText()
        {
            if (_unlockText == null) return;

            _unlockText.text = "새 스킬 해금!";
            _unlockText.color = new Color(1f, 0.9f, 0.3f, 1f);
            _unlockText.transform.localScale = Vector3.zero;

            _unlockText.transform.DOScale(1f, 0.3f)
                .SetEase(Ease.OutBack)
                .SetDelay(_spinDuration)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void PlaySkillName(string skillName)
        {
            if (_skillNameText == null) return;

            _skillNameText.text = skillName;
            _skillNameText.transform.localScale = Vector3.zero;

            _skillNameText.transform.DOScale(1f, 0.25f)
                .SetEase(Ease.OutBack)
                .SetDelay(_spinDuration + 0.15f)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void ScheduleFadeOut()
        {
            if (_rootCanvasGroup == null) return;

            float totalDelay = _spinDuration + _stayDuration;

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
                    if (_glowImage != null)
                        _glowImage.gameObject.SetActive(false);
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
                gameObject.AddComponent<CanvasScaler>();
                gameObject.AddComponent<GraphicRaycaster>();
            }

            if (_rootCanvasGroup == null)
            {
                _rootCanvasGroup = GetComponent<CanvasGroup>();
                if (_rootCanvasGroup == null) _rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // 반투명 배경 오버레이
            var rootRt = transform as RectTransform;
            if (rootRt != null && GetComponent<Image>() == null)
            {
                var dimBg = gameObject.AddComponent<Image>();
                dimBg.color = new Color(0f, 0f, 0f, 0.5f);
                dimBg.raycastTarget = false;
                rootRt.anchorMin = Vector2.zero;
                rootRt.anchorMax = Vector2.one;
                rootRt.offsetMin = Vector2.zero;
                rootRt.offsetMax = Vector2.zero;
            }

            // 스킬 아이콘 (중앙)
            if (_skillIcon == null)
            {
                var go = CreateUIChild("SkillIcon");
                _skillIcon = go.AddComponent<Image>();
                _skillIcon.color = new Color(0.3f, 0.5f, 0.9f);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(64f, 64f);
            }

            // 글로우 — 스프라이트 없이 사각형 대신 CanvasGroup 기반 페이드로 대체
            // 더 이상 큰 사각형을 회전하지 않음
            if (_glowImage == null)
            {
                var go = CreateUIChild("GlowImage");
                _glowImage = go.AddComponent<Image>();
                // 원형 느낌 — 작은 크기, 회전 제거 (PlayGlow에서 처리)
                _glowImage.color = new Color(1f, 0.9f, 0.3f, 0f);
                _glowImage.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(100f, 100f);
                go.SetActive(false);
            }

            // "새 스킬 해금!" 텍스트 (아이콘 위)
            if (_unlockText == null)
            {
                var go = CreateUIChild("UnlockText");
                _unlockText = go.AddComponent<TextMeshProUGUI>();
                _unlockText.fontSize = 24f;
                _unlockText.fontStyle = FontStyles.Bold;
                _unlockText.alignment = TextAlignmentOptions.Center;
                _unlockText.color = new Color(1f, 0.9f, 0.3f);
                _unlockText.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, 60f);
                rt.sizeDelta = new Vector2(400f, 40f);
            }

            // 스킬 이름 (아이콘 아래)
            if (_skillNameText == null)
            {
                var go = CreateUIChild("SkillNameText");
                _skillNameText = go.AddComponent<TextMeshProUGUI>();
                _skillNameText.fontSize = 18f;
                _skillNameText.alignment = TextAlignmentOptions.Center;
                _skillNameText.color = Color.white;
                _skillNameText.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, -55f);
                rt.sizeDelta = new Vector2(400f, 30f);
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
            if (_skillIcon != null) _skillIcon.transform.DOKill();
            if (_glowImage != null)
            {
                _glowImage.transform.DOKill();
                DOTween.Kill(_glowImage);
            }
            if (_unlockText != null) _unlockText.transform.DOKill();
            if (_skillNameText != null) _skillNameText.transform.DOKill();
        }
    }
}
