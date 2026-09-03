using UnityEngine;

namespace MkLike.Guild
{
    /// <summary>
    /// 길드 보스 전투 시뮬레이터.
    /// CP 기반 데미지 비율 산출 방식.
    /// </summary>
    public static class GuildBossBattleSimulator
    {
        /// <summary>보스 전투 결과.</summary>
        public struct RaidResult
        {
            public long TotalDamage;
            public bool IsBossDefeated;
            public float DamagePercent;
        }

        /// <summary>
        /// CP 기반 길드 보스 전투 시뮬레이션.
        /// </summary>
        public static RaidResult Simulate(long playerCp, long bossMaxHp)
        {
            // CP 기반 데미지 산출: CP × 랜덤(5~15) — 보스 HP 대비 비율
            long damage = System.Math.Max(1L, (long)(playerCp * Random.Range(5f, 15f)));
            bool defeated = damage >= bossMaxHp;
            float dmgPercent = bossMaxHp > 0 ? (float)damage / bossMaxHp : 0f;

            return new RaidResult
            {
                TotalDamage = damage,
                IsBossDefeated = defeated,
                DamagePercent = dmgPercent
            };
        }
    }
}
