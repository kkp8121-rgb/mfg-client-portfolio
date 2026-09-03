using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MkLike.Core;
using MkLike.Quest;
using MkLike.Utils;

namespace MkLike.UI
{
    /// <summary>
    /// 업적 달성 토스트 배너.
    /// AchievementCompletedEvent(Quest) 구독 → 화면 상단 슬라이드 인 → 업적명 + 보상 표시 → 3초 후 슬라이드 아웃.
    /// </summary>
    public class AchievementToast : MonoBehaviour
    {
        [SerializeField] private RectTransform _bannerRect;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _rewardText;
        [SerializeField] private CanvasGroup _bannerCanvasGroup;

        [Header("연출 설정")]
        [SerializeField] private float _slideInDuration = 0.4f;
        [SerializeField] private float _stayDuration = 3f;
        [SerializeField] private float _slideOutDuration = 0.3f;
        [SerializeField] private float _offscreenY = 150f;

        private Sequence _toastSequence;
        private float _onscreenY;

        private void Awake()
        {
            EnsureComponents();
            if (_bannerRect != null)
                _onscreenY = _bannerRect.anchoredPosition.y;
        }

        private void OnEnable()
        {
            EventBus<MkLike.Quest.AchievementCompletedEvent>.Subscribe(OnAchievementCompleted);
        }

        private void OnDisable()
        {
            EventBus<MkLike.Quest.AchievementCompletedEvent>.Unsubscribe(OnAchievementCompleted);
        }

        private void OnAchievementCompleted(MkLike.Quest.AchievementCompletedEvent evt)
        {
            ShowToast(evt.DisplayName, evt.AchievementId);
        }

        private void ShowToast(string displayName, string achievementId)
        {
            gameObject.SetActive(true);
            _toastSequence?.Kill();

            if (_titleText != null)
                _titleText.text = displayName ?? "업적 달성!";

            if (_rewardText != null)
            {
                string rewardDesc = GetRewardDescription(achievementId);
                _rewardText.text = rewardDesc;
            }

            if (_bannerRect == null) return;

            var pos = _bannerRect.anchoredPosition;
            pos.y = _onscreenY + _offscreenY;
            _bannerRect.anchoredPosition = pos;

            if (_bannerCanvasGroup != null)
                _bannerCanvasGroup.alpha = 1f;

            _toastSequence = DOTween.Sequence()
                .Append(
                    DOTween.To(
                        () => _bannerRect.anchoredPosition,
                        v => _bannerRect.anchoredPosition = v,
                        new Vector2(pos.x, _onscreenY),
                        _slideInDuration
                    ).SetEase(Ease.OutBack)
                )
                .AppendInterval(_stayDuration)
                .Append(
                    DOTween.To(
                        () => _bannerRect.anchoredPosition,
                        v => _bannerRect.anchoredPosition = v,
                        new Vector2(pos.x, _onscreenY + _offscreenY),
                        _slideOutDuration
                    ).SetEase(Ease.InCubic)
                )
                .SetUpdate(true)
                .SetLink(gameObject)
                .OnComplete(() => gameObject.SetActive(false));

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(SfxType.UiReward);
        }

        private string GetRewardDescription(string achievementId)
        {
            if (AchievementSystem.Instance == null) return "";

            var data = AchievementSystem.Instance.GetData(achievementId);
            if (data == null || data.rewardAmount <= 0) return "";

            return $"보상: {data.rewardType} x{data.rewardAmount:N0}";
        }

        private void EnsureComponents()
        {
            if (GetComponentInParent<Canvas>() == null)
            {
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 240;
                gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
                gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            // ── 배너 루트 (배경 이미지 포함) ──
            if (_bannerRect == null)
            {
                var go = new GameObject("Banner", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                _bannerRect = go.GetComponent<RectTransform>();
                _bannerRect.anchorMin = new Vector2(0.5f, 1f);
                _bannerRect.anchorMax = new Vector2(0.5f, 1f);
                _bannerRect.pivot = new Vector2(0.5f, 1f);
                _bannerRect.sizeDelta = new Vector2(420f, 70f);
                _bannerRect.anchoredPosition = new Vector2(0f, -30f);

                var bgImg = go.AddComponent<Image>();
                bgImg.raycastTarget = false;

                var tm = UIThemeManager.Instance;
                if (tm != null)
                    tm.ApplyFrameBackground(bgImg);
                else
                    bgImg.color = new Color(0.1f, 0.08f, 0.2f, 0.9f);
            }

            // ── CanvasGroup (페이드 애니메이션용) ──
            if (_bannerCanvasGroup == null)
            {
                _bannerCanvasGroup = _bannerRect.GetComponent<CanvasGroup>();
                if (_bannerCanvasGroup == null)
                    _bannerCanvasGroup = _bannerRect.gameObject.AddComponent<CanvasGroup>();
            }

            // ── 업적 타이틀 텍스트 (상단 영역) ──
            if (_titleText == null)
            {
                var go = new GameObject("TitleText", typeof(RectTransform));
                go.transform.SetParent(_bannerRect, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0.45f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = new Vector2(12f, 0f);
                rt.offsetMax = new Vector2(-12f, -6f);

                _titleText = go.AddComponent<TextMeshProUGUI>();
                _titleText.fontSize = 18f;
                _titleText.alignment = TextAlignmentOptions.Center;
                _titleText.color = new Color(1f, 0.85f, 0.1f);
                _titleText.raycastTarget = false;
            }

            // ── 보상 텍스트 (하단 영역) ──
            if (_rewardText == null)
            {
                var go = new GameObject("RewardText", typeof(RectTransform));
                go.transform.SetParent(_bannerRect, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0.45f);
                rt.offsetMin = new Vector2(12f, 4f);
                rt.offsetMax = new Vector2(-12f, 0f);

                _rewardText = go.AddComponent<TextMeshProUGUI>();
                _rewardText.fontSize = 14f;
                _rewardText.alignment = TextAlignmentOptions.Center;
                _rewardText.color = new Color(0.85f, 0.85f, 0.9f);
                _rewardText.raycastTarget = false;
            }
        }

        private void OnDestroy()
        {
            _toastSequence?.Kill();
            if (_bannerRect != null) DOTween.Kill(_bannerRect);
        }
    }
}
