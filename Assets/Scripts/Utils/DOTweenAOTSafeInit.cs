using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using UnityEngine;

namespace MkLike.Utils
{
    /// <summary>
    /// WebGL IL2CPP/AOT 빌드에서 DOTween 제네릭 메서드 사전등록.
    /// RuntimeInitializeOnLoadMethod로 자동 실행되며,
    /// 실제 트윈은 생성하지 않고 AOT 컴파일러 힌트만 제공한다.
    /// </summary>
    public static class DOTweenAOTSafeInit
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            // 이 메서드의 내용은 실행되지 않음 (early return)
            // AOT 컴파일러가 제네릭 인스턴스를 발견하도록 코드에 존재만 시킴
            if (Application.isPlaying) return;

            // float (가장 많이 사용)
            DOTween.To(null as DOGetter<float>, null as DOSetter<float>, 0f, 0f);

            // Vector2
            DOTween.To(null as DOGetter<Vector2>, null as DOSetter<Vector2>, Vector2.zero, 0f);

            // Vector3
            DOTween.To(null as DOGetter<Vector3>, null as DOSetter<Vector3>, Vector3.zero, 0f);

            // Vector4
            DOTween.To(null as DOGetter<Vector4>, null as DOSetter<Vector4>, Vector4.zero, 0f);

            // int
            DOTween.To(null as DOGetter<int>, null as DOSetter<int>, 0, 0f);

            // long
            DOTween.To(null as DOGetter<long>, null as DOSetter<long>, 0L, 0f);

            // Color (ColorTweenHelper에서 float 분해 사용하지만 AOT 힌트용)
            DOTween.To(null as DOGetter<Color>, null as DOSetter<Color>, Color.white, 0f);
        }
    }
}
