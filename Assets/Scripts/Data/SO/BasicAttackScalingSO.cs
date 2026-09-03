using UnityEngine;

namespace MkLike.Data
{
    /// <summary>
    /// 전직 티어별 기본공격 스케일링 데이터.
    /// </summary>
    [CreateAssetMenu(fileName = "BasicAttackScaling", menuName = "MkLike/Data/BasicAttackScaling")]
    public class BasicAttackScalingSO : ScriptableObject
    {
        [System.Serializable]
        public struct TierScaling
        {
            [Tooltip("전직 티어 (0~4)")]
            public int tier;
            [Tooltip("범위 공격 시 최대 타겟 수")]
            public int maxTargets;
            [Tooltip("히트 수")]
            public int hitCount;
            [Tooltip("데미지 배율 보정 (1.0 = 기본)")]
            public float damageMultiplierBonus;
        }

        [Header("티어별 스케일링")]
        [Tooltip("tier 오름차순으로 설정. 매칭 안 되면 기본값 사용")]
        [SerializeField] private TierScaling[] _tierScalings = new[]
        {
            new TierScaling { tier = 0, maxTargets = 3, hitCount = 1, damageMultiplierBonus = 1f },
            new TierScaling { tier = 1, maxTargets = 3, hitCount = 2, damageMultiplierBonus = 1f },
            new TierScaling { tier = 2, maxTargets = 5, hitCount = 3, damageMultiplierBonus = 1f },
            new TierScaling { tier = 3, maxTargets = 6, hitCount = 5, damageMultiplierBonus = 1f },
            new TierScaling { tier = 4, maxTargets = 6, hitCount = 5, damageMultiplierBonus = 1.2f },
        };

        /// <summary>
        /// 해당 티어의 스케일링 데이터를 반환한다.
        /// 매칭되는 티어가 없으면 가장 가까운 하위 티어를 반환한다.
        /// </summary>
        public TierScaling GetScaling(int tier)
        {
            TierScaling result = _tierScalings[0];
            for (int i = 0; i < _tierScalings.Length; i++)
            {
                if (_tierScalings[i].tier <= tier)
                    result = _tierScalings[i];
                else
                    break;
            }
            return result;
        }
    }
}
