using UnityEngine;

namespace MkLike.Data
{
    /// <summary>
    /// 무기 종류 정의. 기본 스탯은 Normal 등급 Lv1 기준.
    /// 상위 등급은 GradeMultiplier, 레벨은 레벨 배율, 각성은 AwakeningMultiplier를 곱하여 계산한다.
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon_", menuName = "mkLike/Weapon Data")]
    public class WeaponDataSO : ScriptableObject
    {
        [Header("기본 정보")]
        public string id;
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;

        [Header("기본 스탯 (Normal Lv1 기준)")]
        public int baseAtk;
        [Range(0f, 1f)] public float baseCritRate;
        public float baseAtkSpeed;

        [Header("보유 효과 (보유만으로 적용)")]
        [Tooltip("보유 효과 설명")] public string passiveEffectDesc;
        [Tooltip("보유 시 ATK % 증가")] public float passiveAtkPercent;

        [Header("장착 효과")]
        [Tooltip("장착 시 추가 ATK %")] public float equipAtkPercent;
        [Tooltip("장착 시 추가 CritRate")] public float equipCritRate;

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
                "Ancient" => 20.0f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// 티어별 스탯 배율. T1(최고)=1.0, T4(보통)=0.7.
        /// tier=0은 레거시 (배율 1.0).
        /// </summary>
        public static float TierMultiplier(int tier)
        {
            return tier switch
            {
                1 => 1.0f,
                2 => 0.9f,
                3 => 0.8f,
                4 => 0.7f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// 등급 + 레벨 + 티어에 따른 ATK.
        /// 레벨당 5% 증가.
        /// </summary>
        public int GetAtk(string grade, int level, int tier = 0)
        {
            return Mathf.RoundToInt(baseAtk * GradeMultiplier(grade) * TierMultiplier(tier) * (1f + (level - 1) * 0.05f));
        }

        /// <summary>
        /// 등급 + 티어에 따른 CritRate.
        /// </summary>
        public float GetCritRate(string grade, int tier = 0)
        {
            return baseCritRate * GradeMultiplier(grade) * TierMultiplier(tier);
        }

        /// <summary>
        /// 등급 + 티어에 따른 공격속도.
        /// </summary>
        public float GetAtkSpeed(string grade, int tier = 0)
        {
            return baseAtkSpeed * GradeMultiplier(grade) * TierMultiplier(tier);
        }

        /// <summary>
        /// 각성 단계에 따른 추가 배율.
        /// 별 하나당 10% 증가.
        /// </summary>
        public static float AwakeningMultiplier(int stars)
        {
            return 1f + stars * 0.1f;
        }

        /// <summary>
        /// 등급 + 레벨 + 각성 + 티어를 모두 반영한 최종 ATK.
        /// </summary>
        public int GetFinalAtk(string grade, int level, int awakeningStars, int tier = 0)
        {
            return Mathf.RoundToInt(GetAtk(grade, level, tier) * AwakeningMultiplier(awakeningStars));
        }
    }
}
