using UnityEngine;
using MkLike.Utils;

namespace MkLike.Core
{
    /// <summary>
    /// 인게임 액션 피드백 발행용 정적 헬퍼.
    /// EventBus&lt;FeedbackEvent&gt;.Publish 래퍼로 호출부 가독성을 높인다.
    /// 구독자: UI 어셈블리의 FeedbackBusSubscriber — 3-tier 피드백(Toast/VFX/수치)으로 변환.
    /// Phase B feedback-standard.md 기획 참조.
    /// </summary>
    public static class FeedbackBus
    {
        /// <summary>단순 피드백. Toast만 표시.</summary>
        public static void Emit(FeedbackKind kind, string message)
        {
            EventBus<FeedbackEvent>.Publish(new FeedbackEvent
            {
                Kind = kind,
                Message = message
            });
        }

        /// <summary>Toast + Shake (위치 없음, 전역 쉐이크).</summary>
        public static void Emit(FeedbackKind kind, string message, int shakeIntensity)
        {
            EventBus<FeedbackEvent>.Publish(new FeedbackEvent
            {
                Kind = kind,
                Message = message,
                ShakeIntensity = shakeIntensity
            });
        }

        /// <summary>위치 포함 피드백. Toast + VFX.</summary>
        public static void Emit(FeedbackKind kind, string message, Vector3 worldPos, int shakeIntensity = 0)
        {
            EventBus<FeedbackEvent>.Publish(new FeedbackEvent
            {
                Kind = kind,
                Message = message,
                WorldPosition = worldPos,
                ShakeIntensity = shakeIntensity
            });
        }

        /// <summary>풀 피드백. Toast + VFX + 수치.</summary>
        public static void Emit(FeedbackKind kind, string message, Vector3 worldPos, string amountText, int shakeIntensity = 0)
        {
            EventBus<FeedbackEvent>.Publish(new FeedbackEvent
            {
                Kind = kind,
                Message = message,
                WorldPosition = worldPos,
                AmountText = amountText,
                ShakeIntensity = shakeIntensity
            });
        }
    }
}
