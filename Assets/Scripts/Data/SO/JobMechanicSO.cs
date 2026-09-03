using UnityEngine;
using MkLike.Core;

namespace MkLike.Data
{
    /// <summary>
    /// 직업별 고유 메카닉 데이터.
    /// 전사: 콤보 (연속 공격 시 데미지 증가)
    /// 궁수: AS 비례 데미지 보너스
    /// 마법사: 원소 중첩 (동일 대상 연속 타격 시 데미지 누적)
    /// </summary>
    [CreateAssetMenu(fileName = "NewJobMechanic", menuName = "MkLike/Data/JobMechanic")]
    public class JobMechanicSO : ScriptableObject
    {
        [Header("대상 직업")]
        public JobType jobType;

        [Header("전사: 콤보 시스템")]
        [Tooltip("콤보 데미지 증가 주기 (N타마다)")]
        public int comboInterval = 3;
        [Tooltip("콤보 보너스 데미지 비율 (0.5 = +50%)")]
        public float comboBonusRate = 0.5f;
        [Tooltip("콤보 리셋 시간 (초)")]
        public float comboResetTime = 2f;

        [Header("궁수: 공격속도 비례 보너스")]
        [Tooltip("기준 공격속도 (이 값 초과분에 보너스 적용)")]
        public float baseAttackSpeed = 1f;
        [Tooltip("초과 AS 1당 데미지 보너스 비율 (0.15 = +15%/AS)")]
        public float asBonusRatePerUnit = 0.15f;
        [Tooltip("최대 AS 보너스 비율")]
        public float asMaxBonusRate = 1.5f;

        [Header("마법사: 원소 중첩")]
        [Tooltip("스택당 데미지 증가율 (0.1 = +10%)")]
        public float stackBonusRate = 0.1f;
        [Tooltip("최대 중첩 수")]
        public int maxStacks = 5;
        [Tooltip("중첩 유지 시간 (초)")]
        public float stackDuration = 3f;
    }
}
