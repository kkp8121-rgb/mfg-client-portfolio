using UnityEngine;

namespace MkLike.Arena
{
    /// <summary>
    /// 아레나 전투 시뮬레이터.
    /// CP 비교 + 랜덤 팩터 방식으로 승패를 결정한다.
    /// </summary>
    public static class ArenaBattleSimulator
    {
        /// <summary>시뮬레이션 전투 결과.</summary>
        public struct BattleResult
        {
            public bool IsVictory;
            public float PlayerHpPercent;
            public float OpponentHpPercent;
            public long PlayerTotalDamage;
            public long OpponentTotalDamage;
        }

        /// <summary>
        /// CP 비교 기반 아레나 전투 시뮬레이션.
        /// </summary>
        public static BattleResult Simulate(long playerCp, long opponentCp)
        {
            // CP 비율로 승률 산출 (50% 기준 ± CP 차이)
            float cpRatio = opponentCp > 0 ? (float)playerCp / (playerCp + opponentCp) : 0.5f;
            // 랜덤 팩터 ±15%
            float roll = cpRatio + Random.Range(-0.15f, 0.15f);
            bool isVictory = roll >= 0.5f;

            // 잔여 HP 비율 시뮬레이션 (체감용)
            float winnerHp = Random.Range(0.3f, 0.9f);
            float playerHp = isVictory ? winnerHp : 0f;
            float opponentHp = isVictory ? 0f : winnerHp;

            // 데미지 추정값 (전적 기록용)
            long baseDmg = System.Math.Max(1L, (playerCp + opponentCp) / 2);
            long playerDmg = (long)(baseDmg * Random.Range(0.8f, 1.2f));
            long opponentDmg = (long)(baseDmg * Random.Range(0.8f, 1.2f));

            return new BattleResult
            {
                IsVictory = isVictory,
                PlayerHpPercent = playerHp,
                OpponentHpPercent = opponentHp,
                PlayerTotalDamage = playerDmg,
                OpponentTotalDamage = opponentDmg
            };
        }
    }
}
