using MkLike.Core;
using UnityEngine;

namespace MkLike.Data
{
    /// <summary>
    /// 직업×전직 tier(0~4)별 SPUM 프리팹을 매핑하는 SO.
    /// 전직 차수에 따라 캐릭터 외형을 교체하기 위해 사용.
    /// - tier 0: 기본 (견습)
    /// - tier 1: 1차 전직
    /// - tier 2: 2차 전직
    /// - tier 3: 3차 전직
    /// - tier 4: 4차 전직 (최종, Title 화면에서 표시)
    /// </summary>
    [CreateAssetMenu(menuName = "MkLike/Data/Job Outfit Database", fileName = "JobOutfitDatabase")]
    public class JobOutfitDatabaseSO : ScriptableObject
    {
        [System.Serializable]
        public class JobTiers
        {
            [Tooltip("tier 0~4 프리팹 (인덱스 == 전직 차수)")]
            public GameObject[] tiers = new GameObject[5];
        }

        [Header("직업별 tier 프리팹 (0=기본, 4=최종 전직)")]
        [SerializeField] private JobTiers _warrior;
        [SerializeField] private JobTiers _archer;
        [SerializeField] private JobTiers _mage;

        public GameObject GetPrefab(JobType job, int tier)
        {
            var tiers = job switch
            {
                JobType.Warrior => _warrior,
                JobType.Archer => _archer,
                JobType.Mage => _mage,
                _ => null,
            };
            if (tiers == null || tiers.tiers == null || tiers.tiers.Length == 0) return null;

            int clampedTier = Mathf.Clamp(tier, 0, tiers.tiers.Length - 1);
            // 해당 tier가 비어있으면 아래 tier로 fallback (최대 tier 0까지)
            for (int t = clampedTier; t >= 0; --t)
            {
                if (tiers.tiers[t] != null) return tiers.tiers[t];
            }
            return null;
        }

        /// <summary>최종 전직(tier=4) 프리팹. Title JobSelect 화면 등에서 사용.</summary>
        public GameObject GetFinalTierPrefab(JobType job) => GetPrefab(job, 4);

        /// <summary>런타임에 Resources에서 단일 DB 에셋을 로드.</summary>
        public static JobOutfitDatabaseSO Load()
        {
            return Resources.Load<JobOutfitDatabaseSO>("Data/JobOutfitDatabase");
        }
    }
}
