using DG.Tweening;
using TMPro;
using UnityEngine;

namespace MkLike.Utils
{
    /// <summary>
    /// DOTween.To&lt;Color&gt; 대신 float 보간으로 색상 트윈.
    /// WebGL IL2CPP/AOT에서 Color valuetype 제네릭 크래시 방지.
    /// </summary>
    public static class ColorTweenHelper
    {
        /// <summary>SpriteRenderer 색상 트윈</summary>
        public static Tweener To(SpriteRenderer sr, Color target, float duration)
        {
            Color start = sr.color;
            return DOTween.To(() => 0f, t => sr.color = Color.Lerp(start, target, t), 1f, duration);
        }

        /// <summary>TMP_Text 색상 트윈</summary>
        public static Tweener To(TMP_Text text, Color target, float duration)
        {
            Color start = text.color;
            return DOTween.To(() => 0f, t => text.color = Color.Lerp(start, target, t), 1f, duration);
        }

        /// <summary>UnityEngine.UI.Image 색상 트윈</summary>
        public static Tweener To(UnityEngine.UI.Image img, Color target, float duration)
        {
            Color start = img.color;
            return DOTween.To(() => 0f, t => img.color = Color.Lerp(start, target, t), 1f, duration);
        }
    }
}
