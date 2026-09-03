using UnityEngine;
using MkLike.Core;

namespace MkLike.Data
{
    /// <summary>
    /// 장비 특수 옵션 타입. 에픽 이상 등급에서 활성화된다.
    /// </summary>
    public enum EquipmentSpecialOption
    {
        None,
        CooldownReduce,      // 스킬 쿨타임 감소
        AttackTargetCount,   // 기본 공격 대상 수 증가
        FinalDamage,         // 최종 데미지 증가
        CritDamage,          // 크리티컬 데미지 증가
        ArmorPenetration,    // 방어구 관통력
        BuffDuration,        // 버프 지속시간 증가
        CompanionDuration    // 동료 소환 지속시간 증가
    }

    /// <summary>
    /// 장비 종류 정의. 기본 스탯은 Normal 등급 기준.
    /// 상위 등급은 GradeMultiplier를 곱하여 계산한다.
    /// </summary>
    [CreateAssetMenu(fileName = "Equipment_", menuName = "mkLike/Equipment Data")]
    public class EquipmentDataSO : ScriptableObject
    {
        [Header("기본 정보")]
        public string id;
        public string displayName;
        public EquipmentSlot slot;
        public Sprite icon;

        [Header("기본 스탯 (Normal 등급 기준)")]
        public int baseAtk;
        public int baseHp;
        public int baseDef;
        [Range(0f, 1f)] public float baseCritRate;
        public float baseAtkSpd;

        [Header("추가 스탯")]
        [Tooltip("기본 명중률")] public float baseAccuracy;
        [Tooltip("기본 회피율")] public float baseDodge;
        [Tooltip("기본 MaxMP")] public int baseMaxMp;

        [Header("특수 옵션 (에픽 이상 등급)")]
        [Tooltip("특수 옵션 타입")] public EquipmentSpecialOption specialOption;
        [Tooltip("특수 옵션 기본값 (Normal 기준)")] public float specialOptionValue;

        /// <summary>
        /// 등급별 스탯 배율을 반환한다.
        /// </summary>
        public static float GradeMultiplier(string grade)
        {
            return grade switch
            {
                "Normal" => 1.0f,
                "Rare" => 1.5f,
                "Epic" => 2.5f,
                "Unique" => 4.0f,
                "Legendary" => 7.0f,
                "Mythic" => 12.0f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// 지정 등급에서의 최종 ATK.
        /// </summary>
        public int GetAtk(string grade) => Mathf.RoundToInt(baseAtk * GradeMultiplier(grade));
        public int GetHp(string grade) => Mathf.RoundToInt(baseHp * GradeMultiplier(grade));
        public int GetDef(string grade) => Mathf.RoundToInt(baseDef * GradeMultiplier(grade));
        public float GetCritRate(string grade) => baseCritRate * GradeMultiplier(grade);
        public float GetAtkSpd(string grade) => baseAtkSpd * GradeMultiplier(grade);

        public float GetAccuracy(string grade) => baseAccuracy * GradeMultiplier(grade);
        public float GetDodge(string grade) => baseDodge * GradeMultiplier(grade);
        public int GetMaxMp(string grade) => Mathf.RoundToInt(baseMaxMp * GradeMultiplier(grade));

        /// <summary>
        /// 에픽 이상에서만 특수 옵션 활성화.
        /// </summary>
        public bool HasSpecialOption(string grade) => specialOption != EquipmentSpecialOption.None
            && GradeToInt(grade) >= 2; // Epic 이상

        /// <summary>
        /// 등급별 특수 옵션 수치 (등급이 높을수록 강해짐).
        /// </summary>
        public float GetSpecialOptionValue(string grade)
        {
            if (!HasSpecialOption(grade)) return 0f;
            return specialOptionValue * SpecialOptionGradeMultiplier(grade);
        }

        private static float SpecialOptionGradeMultiplier(string grade) => grade switch
        {
            "Epic" => 1.0f,
            "Unique" => 1.5f,
            "Legendary" => 2.0f,
            "Mythic" => 3.0f,
            _ => 0f
        };

        private static int GradeToInt(string grade) => grade switch
        {
            "Normal" => 0,
            "Rare" => 1,
            "Epic" => 2,
            "Unique" => 3,
            "Legendary" => 4,
            "Mythic" => 5,
            _ => 0
        };

        /// <summary>
        /// 주문서 강화 레벨에 따른 추가 ATK.
        /// </summary>
        public static int ScrollBonusAtk(int scrollLevel) => scrollLevel * 3;

        /// <summary>
        /// 성급 강화에 따른 추가 ATK 비율.
        /// </summary>
        public static float StarForceAtkMultiplier(int starForce) => 1f + starForce * 0.02f;
    }
}
