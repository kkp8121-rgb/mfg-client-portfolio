using UnityEngine;
using MkLike.Core;

namespace MkLike.Data
{
    /// <summary>
    /// 영웅의 힘 마일스톤 설정.
    /// CP 마일스톤 달성 시 영구 패시브 보너스를 정의한다.
    /// </summary>
    [CreateAssetMenu(fileName = "HeroPowerConfig", menuName = "MkLike/Data/Hero Power Config")]
    public class HeroPowerDataSO : ScriptableObject
    {
        [Header("해금 조건")]
        [Tooltip("영웅의 힘 시스템 해금 레벨")]
        [SerializeField] private int _unlockLevel = 79;

        [Header("마일스톤 테이블")]
        [SerializeField] private HeroPowerMilestone[] _milestones = new HeroPowerMilestone[]
        {
            new() { requiredCp = 5000, bonusStat = StatType.Atk, bonusPercent = 2f },
            new() { requiredCp = 10000, bonusStat = StatType.MaxHp, bonusPercent = 5f },
            new() { requiredCp = 20000, bonusStat = StatType.Def, bonusPercent = 3f },
            new() { requiredCp = 35000, bonusStat = StatType.CritRate, bonusPercent = 2f },
            new() { requiredCp = 50000, bonusStat = StatType.Atk, bonusPercent = 4f },
            new() { requiredCp = 75000, bonusStat = StatType.MaxHp, bonusPercent = 8f },
            new() { requiredCp = 100000, bonusStat = StatType.AttackSpeed, bonusPercent = 3f },
            new() { requiredCp = 150000, bonusStat = StatType.Def, bonusPercent = 5f },
            new() { requiredCp = 200000, bonusStat = StatType.Atk, bonusPercent = 6f },
            new() { requiredCp = 300000, bonusStat = StatType.CritDamage, bonusPercent = 10f },
        };

        public int UnlockLevel => _unlockLevel;
        public int MilestoneCount => _milestones.Length;

        /// <summary>특정 마일스톤 데이터 반환</summary>
        public HeroPowerMilestone GetMilestone(int index)
        {
            if (index < 0 || index >= _milestones.Length) return default;
            return _milestones[index];
        }

        /// <summary>CP 기준으로 달성 가능한 마일스톤 수 계산</summary>
        public int CalculateUnlockedCount(long currentCp)
        {
            int count = 0;
            for (int i = 0; i < _milestones.Length; i++)
            {
                if (currentCp >= _milestones[i].requiredCp)
                    count = i + 1;
                else
                    break;
            }
            return count;
        }

        /// <summary>다음 마일스톤까지 필요한 CP (달성 완료 시 0)</summary>
        public long GetNextMilestoneCp(int currentUnlocked)
        {
            if (currentUnlocked >= _milestones.Length) return 0;
            return _milestones[currentUnlocked].requiredCp;
        }
    }

    /// <summary>
    /// 영웅의 힘 개별 마일스톤 데이터.
    /// </summary>
    [System.Serializable]
    public struct HeroPowerMilestone
    {
        [Tooltip("달성 필요 전투력")]
        public long requiredCp;
        [Tooltip("보너스 스탯 종류")]
        public StatType bonusStat;
        [Tooltip("보너스 퍼센트 (2 = +2%)")]
        public float bonusPercent;
    }
}
