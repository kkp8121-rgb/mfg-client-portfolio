using UnityEngine;
using MkLike.Core;

namespace MkLike.Data
{
    /// <summary>
    /// 마스터리 노드 데이터. 3분기 x 3단계 = 9노드 per 직업.
    /// </summary>
    [CreateAssetMenu(fileName = "NewMastery", menuName = "MkLike/Data/Mastery")]
    public class MasteryDataSO : ScriptableObject
    {
        [Header("기본 정보")]
        public string id;
        public string displayName;
        [TextArea] public string description;

        [Header("분기/단계")]
        [Tooltip("분기 인덱스 (0~2)")]
        public int branchIndex;
        [Tooltip("분기 내 단계 (0~2)")]
        public int nodeLevel;

        [Header("대상 직업")]
        public JobType requiredJob;

        [Header("선행 노드")]
        [Tooltip("이 노드를 찍기 위해 필요한 선행 마스터리 ID (단계 0은 비어있음)")]
        public string prerequisiteId;

        [Header("스탯 보너스")]
        public StatType bonusStat;
        [Tooltip("고정 보너스 (flatBonus)")]
        public float flatBonus;
        [Tooltip("비율 보너스 (percentBonus, 0.1 = +10%)")]
        public float percentBonus;

        [Header("비용")]
        [Tooltip("마스터리 포인트 소모량")]
        public int pointCost = 1;
    }
}
