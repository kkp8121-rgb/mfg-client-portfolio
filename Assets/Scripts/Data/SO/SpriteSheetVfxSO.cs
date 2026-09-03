using UnityEngine;

namespace MkLike.Data
{
    /// <summary>
    /// 스프라이트 시트 VFX 정의. 프레임 배열 + 재생 설정.
    /// Unity 스프라이트 에디터에서 시트를 슬라이스한 Sprite[]를 할당한다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewVfx", menuName = "MkLike/Data/SpriteSheetVfx")]
    public class SpriteSheetVfxSO : ScriptableObject
    {
        [Header("프레임")]
        [Tooltip("스프라이트 시트에서 슬라이스한 프레임 배열 (순서대로)")]
        public Sprite[] frames;

        [Header("재생 설정")]
        [Tooltip("초당 프레임 수")]
        [Range(4, 60)] public int fps = 12;

        [Tooltip("반복 재생 여부 (버프 오라 등)")]
        public bool loop;

        [Header("비주얼")]
        [Tooltip("기본 색조 (스킬별 오버라이드 가능)")]
        public Color defaultTint = Color.white;

        [Tooltip("기본 스케일")]
        public float defaultScale = 1f;

        [Tooltip("Additive 블렌딩 사용 (발광 이펙트용)")]
        public bool additive;

        [Tooltip("방향 무관 이펙트 — flipX 무시 (원형 이펙트용)")]
        public bool ignoreFlip;

        [Tooltip("Y축 반전 (위아래 뒤집기 — 내려치기→올려치기 등)")]
        public bool flipY;

        [Header("폴백")]
        [Tooltip("frames가 비어있을 때 프로시져럴 이펙트에 사용할 색상. (0,0,0,0)이면 스킬 타입에서 자동 추론")]
        public Color fallbackColor = new Color(0f, 0f, 0f, 0f);

        /// <summary>폴백 색상이 명시적으로 설정되었는지 여부.</summary>
        public bool HasFallbackColor => fallbackColor.a > 0.01f;

        /// <summary>전체 재생 시간 (초). 루핑이면 1회 주기.</summary>
        public float Duration => frames != null && frames.Length > 0 && fps > 0
            ? (float)frames.Length / fps
            : 0f;
    }
}
