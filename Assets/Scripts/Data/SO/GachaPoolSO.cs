using UnityEngine;

namespace MkLike.Data
{
    [System.Serializable]
    public class GachaEntry
    {
        public string itemId;
        [Tooltip("Normal, Rare, Epic, Unique, Legendary, Mythic, Ancient")]
        public string grade;
        [Min(0f)]
        public float weight;
    }

    /// <summary>
    /// 등급+티어 조합 가중치 항목. 무기 가챠에서 사용.
    /// </summary>
    [System.Serializable]
    public class GradeTierEntry
    {
        [Tooltip("등급명 (Normal~Ancient)")]
        public string grade;
        [Tooltip("티어 (1=T1 최고, 4=T4 보통)")]
        [Range(1, 4)] public int tier;
        [Tooltip("가중치 (상대값)")]
        [Min(0f)] public float weight;
    }

    /// <summary>
    /// 소환 레벨별 확률 데이터.
    /// </summary>
    [System.Serializable]
    public class SummonLevelData
    {
        [Tooltip("이 레벨 도달에 필요한 누적 뽑기 수")]
        public int requiredPulls;

        [Tooltip("등급별 가중치 (Normal, Rare, Epic, Unique, Legendary, Mythic, Ancient 순서)")]
        public float[] gradeWeights = { 50f, 30f, 15f, 4f, 0.9f, 0.09f, 0.01f };

        [Tooltip("등급+티어 세부 가중치 (있으면 gradeWeights 대신 사용)")]
        public GradeTierEntry[] gradeTierWeights;

        /// <summary>gradeTierWeights가 유효한지 확인</summary>
        public bool HasGradeTierWeights => gradeTierWeights != null && gradeTierWeights.Length > 0;
    }

    [CreateAssetMenu(fileName = "GachaPool_", menuName = "mkLike/Gacha Pool")]
    public class GachaPoolSO : ScriptableObject
    {
        [Tooltip("가챠 풀 항목")]
        public GachaEntry[] entries;

        [Tooltip("10연차 보장 최소 등급 (빈 문자열 = 보장 없음)")]
        public string tenPullGuaranteeGrade = "Rare";

        [Header("소환 레벨 시스템")]
        [Tooltip("소환 레벨별 확률 데이터 (비어있으면 entries의 기본 가중치 사용)")]
        public SummonLevelData[] summonLevels;

        /// <summary>등급 이름 순서 (인덱스 매핑용)</summary>
        public static readonly string[] GradeNames = { "Normal", "Rare", "Epic", "Unique", "Legendary", "Mythic", "Ancient" };

        /// <summary>
        /// 주어진 소환 레벨의 SummonLevelData를 반환.
        /// </summary>
        public SummonLevelData GetSummonLevelData(int summonLevel)
        {
            if (summonLevels == null || summonLevels.Length == 0)
                return null;

            int idx = Mathf.Clamp(summonLevel - 1, 0, summonLevels.Length - 1);
            return summonLevels[idx];
        }

        /// <summary>
        /// 주어진 소환 레벨에 맞는 등급별 가중치를 반환.
        /// summonLevels가 비어있으면 null 반환 (entries 기본 가중치 사용).
        /// </summary>
        public float[] GetGradeWeightsForLevel(int summonLevel)
        {
            if (summonLevels == null || summonLevels.Length == 0)
                return null;

            // 레벨 범위 클램프 (1-based → 0-based index)
            int idx = Mathf.Clamp(summonLevel - 1, 0, summonLevels.Length - 1);
            return summonLevels[idx].gradeWeights;
        }

        /// <summary>
        /// 주어진 누적 뽑기 수에 해당하는 소환 레벨 반환 (1-based).
        /// </summary>
        public int GetSummonLevelForPulls(int totalPulls)
        {
            if (summonLevels == null || summonLevels.Length == 0)
                return 1;

            int level = 1;
            for (int i = 0; i < summonLevels.Length; i++)
            {
                if (totalPulls >= summonLevels[i].requiredPulls)
                    level = i + 1;
                else
                    break;
            }
            return level;
        }

        /// <summary>최대 소환 레벨</summary>
        public int MaxSummonLevel => summonLevels != null && summonLevels.Length > 0 ? summonLevels.Length : 1;

        /// <summary>
        /// 다음 레벨까지 필요한 남은 뽑기 수. 최대 레벨이면 -1.
        /// </summary>
        public int GetPullsToNextLevel(int totalPulls)
        {
            if (summonLevels == null || summonLevels.Length <= 1)
                return -1;

            int currentLevel = GetSummonLevelForPulls(totalPulls);
            if (currentLevel >= summonLevels.Length)
                return -1;

            return summonLevels[currentLevel].requiredPulls - totalPulls;
        }
    }
}
