using System;
using UnityEngine;

namespace MkLike.Data
{
    /// <summary>
    /// 오프라인 보상 설정 SO.
    /// 레벨별 배율, 장비 드롭 확률, 최대 축적 시간 등을 데이터로 관리한다.
    /// </summary>
    [CreateAssetMenu(fileName = "OfflineRewardConfig", menuName = "MkLike/Data/OfflineRewardConfig")]
    public class OfflineRewardConfigSO : ScriptableObject
    {
        [Header("레벨별 보상 배율")]
        [Tooltip("레벨 구간별 오프라인 보상 배율. 가장 높은 조건을 충족하는 항목이 적용됨.")]
        [SerializeField] private LevelMultiplierEntry[] _levelMultipliers = new[]
        {
            new LevelMultiplierEntry { requiredLevel = 1, multiplier = 1.0f },
            new LevelMultiplierEntry { requiredLevel = 100, multiplier = 1.5f },
            new LevelMultiplierEntry { requiredLevel = 200, multiplier = 2.0f },
        };

        [Header("장비 드롭")]
        [Tooltip("시간당 장비 드롭 확률 (0.1 = 10%)")]
        [SerializeField] private float _equipDropRatePerHour = 0.1f;

        [Tooltip("드롭 장비 최소 등급 (ItemGrade 이름)")]
        [SerializeField] private string _minDropGrade = "Rare";

        [Header("부스터 연동")]
        [Tooltip("오프라인 시 부스터 잔여 시간 적용 여부")]
        [SerializeField] private bool _applyBoosterCarryover = true;

        /// <summary>시간당 장비 드롭 확률</summary>
        public float EquipDropRatePerHour => _equipDropRatePerHour;

        /// <summary>드롭 장비 최소 등급</summary>
        public string MinDropGrade => _minDropGrade;

        /// <summary>부스터 잔여 시간 연동 여부</summary>
        public bool ApplyBoosterCarryover => _applyBoosterCarryover;

        /// <summary>
        /// 주어진 레벨에 해당하는 보상 배율을 반환한다.
        /// 가장 높은 requiredLevel 조건을 만족하는 항목을 선택한다.
        /// </summary>
        public float GetMultiplier(int level)
        {
            float best = 1f;
            for (int i = 0; i < _levelMultipliers.Length; i++)
            {
                if (level >= _levelMultipliers[i].requiredLevel
                    && _levelMultipliers[i].multiplier > best)
                {
                    best = _levelMultipliers[i].multiplier;
                }
            }
            return best;
        }

        /// <summary>
        /// 오프라인 시간(분) 기반으로 장비 드롭 수를 결정론적으로 계산한다.
        /// 정수부 확정 + 소수부를 확률 추가 1개.
        /// </summary>
        public int CalculateEquipDropCount(int minutesAway)
        {
            float hours = minutesAway / 60f;
            float expected = hours * _equipDropRatePerHour;
            int guaranteed = (int)expected;
            float fractional = expected - guaranteed;

            if (fractional > 0f && UnityEngine.Random.value < fractional)
                guaranteed++;

            return guaranteed;
        }

        [Serializable]
        public struct LevelMultiplierEntry
        {
            [Tooltip("이 배율이 적용되는 최소 레벨")]
            public int requiredLevel;
            [Tooltip("보상 배율 (1.0 = 기본, 1.5 = 50% 증가)")]
            public float multiplier;
        }
    }
}
