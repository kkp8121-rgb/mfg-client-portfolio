using UnityEngine;
using MkLike.Core;

namespace MkLike.Data
{
    /// <summary>
    /// 도감 마일스톤 보상 정의.
    /// 카테고리별 수집 임계값 달성 시 부여되는 영구 스탯 보너스.
    /// </summary>
    [System.Serializable]
    public class CollectionMilestone
    {
        [Tooltip("마일스톤 달성에 필요한 수집 수")]
        public int threshold;

        [Tooltip("보상 스탯 타입")]
        public StatType statType;

        [Tooltip("고정 보너스 (0이면 미적용)")]
        public float flatBonus;

        [Tooltip("비율 보너스 (0.1 = +10%, 0이면 미적용)")]
        public float percentBonus;

        [Tooltip("보상 설명 텍스트")]
        public string description;
    }

    /// <summary>
    /// 도감 칭호 정의.
    /// 전체 수집률에 따라 부여되는 칭호.
    /// </summary>
    [System.Serializable]
    public class CollectionTitle
    {
        [Tooltip("칭호 이름")]
        public string titleName;

        [Tooltip("칭호 획득 조건: 전체 수집 수")]
        public int requiredTotal;

        [Tooltip("칭호 설명")]
        public string description;

        [Tooltip("칭호 색상 (리치 텍스트용)")]
        public Color titleColor = Color.white;
    }

    /// <summary>
    /// 도감 카테고리별 마일스톤 보상 + 칭호 데이터를 정의하는 ScriptableObject.
    /// </summary>
    [CreateAssetMenu(fileName = "CollectionData", menuName = "mkLike/Collection Data")]
    public class CollectionDataSO : ScriptableObject
    {
        [Header("카테고리별 마일스톤")]
        [Tooltip("몬스터 도감 마일스톤")]
        public CollectionMilestone[] monsterMilestones;

        [Tooltip("장비 도감 마일스톤")]
        public CollectionMilestone[] equipmentMilestones;

        [Tooltip("무기 도감 마일스톤")]
        public CollectionMilestone[] weaponMilestones;

        [Tooltip("동료 도감 마일스톤")]
        public CollectionMilestone[] companionMilestones;

        [Tooltip("유물 도감 마일스톤")]
        public CollectionMilestone[] relicMilestones;

        [Tooltip("펫 도감 마일스톤")]
        public CollectionMilestone[] petMilestones;

        [Tooltip("코스튬 도감 마일스톤")]
        public CollectionMilestone[] costumeMilestones;

        [Header("카테고리별 전체 수")]
        public int totalMonsters = 50;
        public int totalEquipment = 30;
        public int totalWeapons = 30;
        public int totalCompanions = 20;
        public int totalRelics = 20;
        public int totalPets = 15;
        public int totalCostumes = 30;

        [Header("칭호")]
        public CollectionTitle[] titles;

        /// <summary>
        /// 카테고리에 해당하는 마일스톤 배열을 반환한다.
        /// </summary>
        public CollectionMilestone[] GetMilestones(CollectionCategory category)
        {
            return category switch
            {
                CollectionCategory.Monster => monsterMilestones,
                CollectionCategory.Equipment => equipmentMilestones,
                CollectionCategory.Weapon => weaponMilestones,
                CollectionCategory.Companion => companionMilestones,
                CollectionCategory.Relic => null, // 2026-04-20 유물 시스템 완전 제거
                CollectionCategory.Pet => petMilestones,
                CollectionCategory.Costume => costumeMilestones,
                _ => null
            };
        }

        /// <summary>
        /// 카테고리의 전체 수를 반환한다.
        /// </summary>
        public int GetTotal(CollectionCategory category)
        {
            return category switch
            {
                CollectionCategory.Monster => totalMonsters,
                CollectionCategory.Equipment => totalEquipment,
                CollectionCategory.Weapon => totalWeapons,
                CollectionCategory.Companion => totalCompanions,
                CollectionCategory.Relic => 0, // 2026-04-20 유물 시스템 완전 제거
                CollectionCategory.Pet => totalPets,
                CollectionCategory.Costume => totalCostumes,
                _ => 0
            };
        }

        /// <summary>
        /// 전체 수집 수 기준으로 달성한 최고 칭호를 반환한다.
        /// </summary>
        public CollectionTitle GetCurrentTitle(int totalCollected)
        {
            if (titles == null || titles.Length == 0) return null;

            CollectionTitle best = null;
            for (int i = 0; i < titles.Length; i++)
            {
                if (totalCollected >= titles[i].requiredTotal)
                {
                    if (best == null || titles[i].requiredTotal > best.requiredTotal)
                        best = titles[i];
                }
            }
            return best;
        }
    }
}
