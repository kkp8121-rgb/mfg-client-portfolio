using UnityEngine;
using MkLike.Core;

namespace MkLike.Data
{
    /// <summary>
    /// 스킬 정적 데이터를 정의하는 ScriptableObject.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSkill", menuName = "MkLike/Data/Skill")]
    public class SkillDataSO : ScriptableObject
    {
        [Header("기본 정보")]
        public string id;
        public string displayName;
        [TextArea] public string description;
        public SkillType skillType;
        public Sprite icon;

        [Header("습득 조건")]
        [Tooltip("이 스킬을 배우는 레벨")]
        public int learnLevel = 1;
        [Tooltip("전직 단계 (0~4, 4=4차전직)")]
        public int jobTier;

        [Header("쿨타임/지속시간")]
        [Tooltip("스킬 쿨타임 (Active는 0)")]
        public float cooldown;
        [Tooltip("버프/특수효과 지속시간")]
        public float duration;

        [Header("데미지")]
        [Tooltip("ATK 대비 데미지 배율 (예: 2.0 = ATK 200%)")]
        public float damageMultiplier = 1f;
        [Tooltip("공격 범위 (0이면 단일 타겟)")]
        public float range;
        [Tooltip("다단히트 횟수")]
        public int hitCount = 1;

        [Header("버프 스탯")]
        [Tooltip("ATK 증가율 (0.1 = +10%)")]
        public float buffAtkRate;
        [Tooltip("DEF 증가율")]
        public float buffDefRate;
        [Tooltip("CritRate 증가량")]
        public float buffCritRate;
        [Tooltip("공격속도 증가율")]
        public float buffAtkSpdRate;

        [Header("특수 효과")]
        public SkillEffect specialEffect;
        [Tooltip("특수 효과 지속시간 (스턴 등)")]
        public float effectDuration;
        [Tooltip("특수 효과 수치 (흡혈 비율, 방깎 비율 등)")]
        public float effectValue;

        [Header("추가 버프")]
        [Tooltip("치명타 데미지 증가율")]
        public float buffCritDmgRate;
        [Tooltip("이동속도 증가율")]
        public float buffMoveSpeedRate;
        [Tooltip("쿨타임 감소율")]
        public float buffCooldownReduceRate;

        [Header("DoT/CC")]
        [Tooltip("DoT 총 데미지 비율 (스킬 데미지 대비)")]
        public float dotDamageRate;
        [Tooltip("DoT/CC 지속시간")]
        public float ccDuration;
        [Tooltip("감속률 (Slow 전용)")]
        public float slowRate;

        [Header("자힐")]
        [Tooltip("HP 회복 비율 (0.2 = MaxHP의 20%)")]
        public float healRatio;

        [Header("VFX")]
        [Tooltip("스킬 스프라이트 시트 VFX. null이면 기본 2D 도형 VFX 사용")]
        public SpriteSheetVfxSO vfxSheet;
        [Tooltip("버프 활성화 시 스프라이트 시트 VFX (루핑 오라 등). null이면 기본 버스트만 사용")]
        public SpriteSheetVfxSO buffVfxSheet;

        [SerializeField, Tooltip("스킬 시전 VFX (SpriteSheetVfx 직접 스폰). null이면 SkillVfx 폴백")]
        private SpriteSheetVfxSO _activeVfxSO;
        [SerializeField, Tooltip("히트 이펙트 VFX. null이면 SkillVfx 폴백")]
        private SpriteSheetVfxSO _hitVfxSO;

        public SpriteSheetVfxSO ActiveVfxSO => _activeVfxSO;
        public SpriteSheetVfxSO HitVfxSO => _hitVfxSO;

    }
}
