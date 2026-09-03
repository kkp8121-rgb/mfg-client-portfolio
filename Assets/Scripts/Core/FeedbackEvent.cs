using UnityEngine;
using MkLike.Utils;

namespace MkLike.Core
{
    /// <summary>
    /// 인게임 액션 피드백 이벤트.
    /// FeedbackBus.Emit() 호출 시 Publish되어 FeedbackBusSubscriber(UI)에서 3-tier 피드백으로 발화된다.
    /// - Tier 1 Toast: Message 표시
    /// - Tier 2 VFX: WorldPosition 있으면 해당 위치에 DamageText, ScreenShake
    /// - Tier 3 수치: AmountText 있으면 데미지텍스트로 표기
    /// Phase B feedback-standard.md 기획 참조.
    /// </summary>
    public struct FeedbackEvent : IEvent
    {
        /// <summary>피드백 종류 (발화 정책 결정)</summary>
        public FeedbackKind Kind;
        /// <summary>Toast 메시지 (비면 Toast 생략)</summary>
        public string Message;
        /// <summary>VFX 위치 (null이면 VFX 생략)</summary>
        public Vector3? WorldPosition;
        /// <summary>수치 텍스트 (비면 생략). 예: "+1M 골드"</summary>
        public string AmountText;
        /// <summary>쉐이크 강도: 0=없음, 1=약, 2=중, 3=강</summary>
        public int ShakeIntensity;
    }

    public enum FeedbackKind
    {
        /// <summary>일반 피드백</summary>
        Generic,
        /// <summary>소환 가챠 결과</summary>
        Gacha,
        /// <summary>소탕 완료</summary>
        QuickHunt,
        /// <summary>장비 강화</summary>
        EquipEnhance,
        /// <summary>정예 소환</summary>
        EliteSummon,
        /// <summary>부스터 활성</summary>
        Booster,
        /// <summary>스탯 분배</summary>
        StatAllocate,
        /// <summary>재화 부족 등 부정 피드백</summary>
        Negative
    }
}
