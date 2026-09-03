using Cysharp.Threading.Tasks;
using DG.Tweening;
using MkLike.Core;
using MkLike.Utils;
using TMPro;
using UnityEngine;

namespace MkLike.Combat
{
    /// <summary>
    /// 환생(프레스티지) 실행 시 화면 플래시 + 텍스트 연출을 재생한다.
    /// EventBus&lt;PrestigeExecutedEvent&gt;를 구독하여 자동 트리거된다.
    /// </summary>
    public class PrestigeEffect : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _flashOverlay;
        [SerializeField] private TMP_Text _prestigeText;
        [SerializeField] private TMP_Text _bonusText;

        private void Awake()
        {
            if (_flashOverlay != null)
                _flashOverlay.alpha = 0f;
        }

        private void OnEnable()
        {
            EventBus<PrestigeExecutedEvent>.Subscribe(OnPrestigeExecuted);
        }

        private void OnDisable()
        {
            EventBus<PrestigeExecutedEvent>.Unsubscribe(OnPrestigeExecuted);
        }

        private void OnPrestigeExecuted(PrestigeExecutedEvent e)
        {
            PlayPrestigeEffect(e).Forget();
        }

        private async UniTaskVoid PlayPrestigeEffect(PrestigeExecutedEvent e)
        {
            if (_flashOverlay == null) return;

            var ct = destroyCancellationToken;

            // 환생 SFX
            AudioManager.Instance?.PlaySfx("sfx_prestige");

            // 1) 화면 전체 흰색 플래시 (alpha 0→1→0, 0.8초)
            _flashOverlay.gameObject.SetActive(true);
            _flashOverlay.alpha = 0f;

            var flashComplete = false;
            var flashSeq = DOTween.Sequence()
                .Append(DOTween.To(() => _flashOverlay.alpha, x => _flashOverlay.alpha = x, 1f, 0.3f))
                .Append(DOTween.To(() => _flashOverlay.alpha, x => _flashOverlay.alpha = x, 0f, 0.5f))
                .SetLink(gameObject)
                .OnComplete(() => flashComplete = true);

            await UniTask.WaitUntil(() => flashComplete, cancellationToken: ct);

            // 2) "환생 #{count} 완료!" DOScale + DOFade
            if (_prestigeText != null)
            {
                _prestigeText.text = $"환생 #{e.PrestigeCount} 완료!";
                _prestigeText.alpha = 0f;
                _prestigeText.transform.localScale = Vector3.zero;
                _prestigeText.gameObject.SetActive(true);

                var textComplete = false;
                var textSeq = DOTween.Sequence()
                    .Join(DOTween.To(() => _prestigeText.alpha, x => _prestigeText.alpha = x, 1f, 0.3f))
                    .Join(_prestigeText.transform.DOScale(1.2f, 0.3f).SetEase(Ease.OutBack))
                    .Append(_prestigeText.transform.DOScale(1f, 0.15f))
                    .SetLink(gameObject)
                    .OnComplete(() => textComplete = true);

                await UniTask.WaitUntil(() => textComplete, cancellationToken: ct);
            }

            // 3) "영구 보너스: +{bonus:P0}" 텍스트
            if (_bonusText != null)
            {
                _bonusText.text = $"영구 보너스: +{e.TotalBonus:P0}";
                _bonusText.alpha = 0f;
                _bonusText.transform.localScale = Vector3.zero;
                _bonusText.gameObject.SetActive(true);

                var bonusComplete = false;
                var bonusSeq = DOTween.Sequence()
                    .Join(DOTween.To(() => _bonusText.alpha, x => _bonusText.alpha = x, 1f, 0.3f))
                    .Join(_bonusText.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack))
                    .SetLink(gameObject)
                    .OnComplete(() => bonusComplete = true);

                await UniTask.WaitUntil(() => bonusComplete, cancellationToken: ct);
            }

            // 4) 잠시 유지
            await UniTask.Delay(2000, cancellationToken: ct);

            // 5) 페이드아웃
            var fadeOutComplete = false;
            var fadeOut = DOTween.Sequence();

            if (_prestigeText != null)
                fadeOut.Join(DOTween.To(() => _prestigeText.alpha, x => _prestigeText.alpha = x, 0f, 0.4f));

            if (_bonusText != null)
                fadeOut.Join(DOTween.To(() => _bonusText.alpha, x => _bonusText.alpha = x, 0f, 0.4f));

            fadeOut.SetLink(gameObject)
                .OnComplete(() => fadeOutComplete = true);
            await UniTask.WaitUntil(() => fadeOutComplete, cancellationToken: ct);

            _flashOverlay.gameObject.SetActive(false);
            if (_prestigeText != null) _prestigeText.gameObject.SetActive(false);
            if (_bonusText != null) _bonusText.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            transform.DOKill();
            if (_prestigeText != null)
                _prestigeText.transform.DOKill();
            if (_bonusText != null)
                _bonusText.transform.DOKill();
        }
    }
}
