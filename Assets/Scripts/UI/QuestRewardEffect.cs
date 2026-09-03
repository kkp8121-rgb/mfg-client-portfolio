using Cysharp.Threading.Tasks;
using DG.Tweening;
using MkLike.Core;
using MkLike.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MkLike.UI
{
    /// <summary>
    /// 퀘스트 보상 획득 연출. QuestRewardClaimedEvent 구독.
    /// 오버레이 + 빛 줄기 회전 + 아이콘 스케일 + 수량 카운트업 → 자동 페이드아웃.
    /// </summary>
    public class QuestRewardEffect : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _overlay;
        [SerializeField] private Image _rewardIcon;
        [SerializeField] private TMP_Text _rewardAmountText;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private RectTransform _lightRays;

        private Tween _lightTween;

        private void OnEnable()
        {
            EventBus<QuestRewardClaimedEvent>.Subscribe(OnQuestRewardClaimed);
        }

        private void OnDisable()
        {
            EventBus<QuestRewardClaimedEvent>.Unsubscribe(OnQuestRewardClaimed);
        }

        private void OnQuestRewardClaimed(QuestRewardClaimedEvent evt)
        {
            PlayRewardEffect(evt).Forget();
        }

        private async UniTaskVoid PlayRewardEffect(QuestRewardClaimedEvent evt)
        {
            var token = this.GetCancellationTokenOnDestroy();

            KillAllTweens();

            // 초기 상태
            if (_overlay != null)
            {
                _overlay.alpha = 0f;
                _overlay.blocksRaycasts = true;
                _overlay.gameObject.SetActive(true);
            }
            if (_titleText != null)
                _titleText.transform.localScale = Vector3.zero;
            if (_rewardIcon != null)
                _rewardIcon.transform.localScale = Vector3.zero;
            if (_rewardAmountText != null)
                _rewardAmountText.text = "0";

            // 보상 SFX
            AudioManager.Instance?.PlaySfx(SfxType.UiReward);

            // 오버레이 페이드인 (0→0.6)
            if (_overlay != null)
            {
                DOTween.To(() => _overlay.alpha, x => _overlay.alpha = x, 0.6f, 0.2f)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }

            // 빛 줄기 회전 (360도, 3초, 무한 루프)
            if (_lightRays != null)
            {
                _lightRays.localRotation = Quaternion.identity;
                _lightTween = _lightRays
                    .DORotate(new Vector3(0f, 0f, 360f), 3f, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Restart)
                    .SetUpdate(true)
                    .SetLink(gameObject);
            }

            // "보상 획득!" 타이틀 스케일 (0→1.2→1.0, OutBack)
            if (_titleText != null)
            {
                _titleText.text = "보상 획득!";
                _titleText.transform
                    .DOScale(1.2f, 0.3f)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true)
                    .SetLink(gameObject)
                    .OnComplete(() =>
                    {
                        if (_titleText != null)
                        {
                            _titleText.transform
                                .DOScale(1f, 0.15f)
                                .SetUpdate(true)
                                .SetLink(gameObject);
                        }
                    });
            }

            await UniTask.Delay(300, cancellationToken: token, ignoreTimeScale: true);

            // 재화 아이콘 스케일 (0→1.3→1.0) + DOPunchScale
            if (_rewardIcon != null)
            {
                _rewardIcon.transform
                    .DOScale(1.3f, 0.25f)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true)
                    .SetLink(gameObject)
                    .OnComplete(() =>
                    {
                        if (_rewardIcon != null)
                        {
                            _rewardIcon.transform
                                .DOScale(1f, 0.15f)
                                .SetUpdate(true)
                                .SetLink(gameObject);
                            _rewardIcon.transform
                                .DOPunchScale(Vector3.one * 0.15f, 0.3f, 6, 0.5f)
                                .SetUpdate(true)
                                .SetLink(gameObject);
                        }
                    });
            }

            // 재화 수량 카운트업
            if (_rewardAmountText != null)
            {
                int targetAmount = evt.RewardAmount;
                DOVirtual.Float(0f, targetAmount, 0.6f, value =>
                {
                    if (_rewardAmountText != null)
                        _rewardAmountText.text = $"+{Mathf.RoundToInt(value):N0}";
                })
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .SetLink(gameObject);
            }

            // 2초 대기 후 페이드아웃
            await UniTask.Delay(2000, cancellationToken: token, ignoreTimeScale: true);

            // 페이드아웃
            if (_overlay != null)
            {
                DOTween.To(() => _overlay.alpha, x => _overlay.alpha = x, 0f, 0.3f)
                    .SetUpdate(true)
                    .SetLink(gameObject)
                    .OnComplete(() =>
                    {
                        _lightTween?.Kill();
                        _lightTween = null;
                        if (_overlay != null)
                        {
                            _overlay.blocksRaycasts = false;
                            _overlay.gameObject.SetActive(false);
                        }
                    });
            }
        }

        private void KillAllTweens()
        {
            _lightTween?.Kill();
            _lightTween = null;
            if (_overlay != null) DOTween.Kill(_overlay);
            if (_rewardIcon != null) _rewardIcon.transform.DOKill();
            if (_rewardAmountText != null) _rewardAmountText.transform.DOKill();
            if (_titleText != null) _titleText.transform.DOKill();
            if (_lightRays != null) DOTween.Kill(_lightRays);
        }

        private void OnDestroy()
        {
            KillAllTweens();
        }
    }
}
