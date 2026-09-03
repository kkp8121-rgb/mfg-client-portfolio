using UnityEngine;
using MkLike.Core;

namespace MkLike.Data
{
    /// <summary>
    /// 아레나 티어 데이터 SO.
    /// 티어별 보상, 승점, 색상 등을 정의한다.
    /// </summary>
    [CreateAssetMenu(fileName = "ArenaTier_", menuName = "mkLike/Arena Tier Data")]
    public class ArenaDataSO : ScriptableObject
    {
        [Header("티어 기본 정보")]
        public string id;
        public string displayName;
        public int tierIndex; // 0=Bronze, 1=Silver, 2=Gold, 3=Diamond

        [Header("승격/강등 레이팅")]
        [Tooltip("이 티어 진입에 필요한 최소 레이팅")]
        public int minRating;
        [Tooltip("이 티어의 최대 레이팅 (다음 티어 진입 전)")]
        public int maxRating;

        [Header("승패 레이팅 변동")]
        public int winRating = 30;
        public int loseRating = 15;

        [Header("주간 보상")]
        public int rewardGold;
        public int rewardRuby;
        public int rewardSummonTicket;

        [Header("비주얼")]
        public Color tierColor = Color.white;
    }

    /// <summary>
    /// 아레나 설정 SO — 전역 설정.
    /// </summary>
    [CreateAssetMenu(fileName = "ArenaConfig", menuName = "mkLike/Arena Config")]
    public class ArenaConfigSO : ScriptableObject
    {
        [Header("해금 조건")]
        [Tooltip("아레나 해금에 필요한 스테이지")]
        public int unlockFloor = 100;

        [Header("일일 도전")]
        public int dailyFreeEntries = 5;

        [Header("전투 설정")]
        [Tooltip("전투 제한 시간 (초)")]
        public float battleTimeLimit = 60f;
        [Tooltip("매칭 CP 범위 비율 (±%)")]
        [Range(0.05f, 0.5f)]
        public float matchCpRange = 0.2f;
        [Tooltip("후보 표시 수")]
        public int candidateCount = 3;

        [Header("시즌")]
        [Tooltip("시즌 기간 (일)")]
        public int seasonDurationDays = 14;
        [Tooltip("시즌 종료 시 티어 강등 수")]
        public int tierDemoteOnSeasonEnd = 1;

        [Header("티어 데이터")]
        public ArenaDataSO[] tiers;
    }
}
