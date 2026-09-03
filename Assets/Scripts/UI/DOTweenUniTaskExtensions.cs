using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace MkLike.UI
{
    /// <summary>
    /// DOTween ↔ UniTask 브릿지 확장 메서드.
    /// UniTask 패키지의 DOTween 지원이 활성화되지 않는 환경(DOTween Pro를 Plugins/에 직접 설치)에서
    /// .ToUniTask() 호환 API를 제공한다.
    /// </summary>
    public static class DOTweenUniTaskExtensions
    {
        public static async UniTask ToUniTask(
            this Tween tween,
            CancellationToken cancellationToken = default)
        {
            if (tween == null || !tween.IsActive()) return;
            while (tween.IsActive() && !tween.IsComplete())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    tween.Kill();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                await UniTask.Yield(cancellationToken);
            }
        }
    }
}
