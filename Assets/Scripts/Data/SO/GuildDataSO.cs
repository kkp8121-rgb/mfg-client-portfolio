using UnityEngine;
using MkLike.Core;

namespace MkLike.Data
{
    /// <summary>
    /// 길드 레벨별 버프/요구EXP 데이터.
    /// </summary>
    [System.Serializable]
    public class GuildLevelEntry
    {
        public int level;
        public int requiredExp;
        public string buffDescription;
        /// <summary>버프 타입: "exp", "gold", "atk", "dropRate"</summary>
        public string buffType;
        /// <summary>버프 수치 (0.05 = +5%)</summary>
        public float buffValue;
    }

    /// <summary>
    /// 길드 기부 설정.
    /// </summary>
    [System.Serializable]
    public class GuildDonationEntry
    {
        public string donationType; // "gold", "ruby", "ticket"
        public string displayName;
        public CurrencyType currencyType;
        public int costAmount;
        public int dailyLimit;
        public int guildExpReward;
    }

    /// <summary>
    /// 길드 설정 SO.
    /// </summary>
    [CreateAssetMenu(fileName = "GuildConfig", menuName = "mkLike/Guild Config")]
    public class GuildConfigSO : ScriptableObject
    {
        [Header("해금 조건")]
        public int unlockFloor = 150;

        [Header("길드 생성")]
        public int createCostRuby = 500;
        public int maxMembers = 30;

        [Header("길드 레벨")]
        public GuildLevelEntry[] levels = new GuildLevelEntry[]
        {
            new() { level = 1, requiredExp = 0,     buffDescription = "없음",       buffType = "",         buffValue = 0f },
            new() { level = 2, requiredExp = 5000,   buffDescription = "경험치 +5%", buffType = "exp",      buffValue = 0.05f },
            new() { level = 3, requiredExp = 15000,  buffDescription = "골드 +5%",   buffType = "gold",     buffValue = 0.05f },
            new() { level = 4, requiredExp = 30000,  buffDescription = "공격력 +3%", buffType = "atk",      buffValue = 0.03f },
            new() { level = 5, requiredExp = 60000,  buffDescription = "드롭률 +5%", buffType = "dropRate", buffValue = 0.05f },
            new() { level = 6, requiredExp = 100000, buffDescription = "경험치 +8%", buffType = "exp",      buffValue = 0.08f },
            new() { level = 7, requiredExp = 150000, buffDescription = "골드 +8%",   buffType = "gold",     buffValue = 0.08f },
            new() { level = 8, requiredExp = 220000, buffDescription = "공격력 +5%", buffType = "atk",      buffValue = 0.05f },
            new() { level = 9, requiredExp = 300000, buffDescription = "드롭률 +8%", buffType = "dropRate", buffValue = 0.08f },
            new() { level = 10, requiredExp = 400000, buffDescription = "전체 +10%", buffType = "all",      buffValue = 0.10f },
        };

        [Header("기부 설정")]
        public GuildDonationEntry[] donations = new GuildDonationEntry[]
        {
            new() { donationType = "gold",   displayName = "골드 기부",   currencyType = CurrencyType.Gold,            costAmount = 100000, dailyLimit = 3, guildExpReward = 100 },
            new() { donationType = "ruby",   displayName = "루비 기부",   currencyType = CurrencyType.Ruby,            costAmount = 50,     dailyLimit = 1, guildExpReward = 300 },
            new() { donationType = "ticket", displayName = "소탕권 기부", currencyType = CurrencyType.QuickHuntTicket, costAmount = 5,      dailyLimit = 2, guildExpReward = 150 },
        };

        [Header("길드 보스")]
        /// <summary>주당 보스 도전 횟수</summary>
        public int weeklyBossAttempts = 3;
        /// <summary>보스 HP = 길드 레벨 × 이 값</summary>
        public long bossHpPerLevel = 1000000;
        /// <summary>보스 처치 보상 (루비)</summary>
        public int bossDefeatRewardRuby = 200;
        /// <summary>보스 처치 보상 (골드)</summary>
        public int bossDefeatRewardGold = 500000;
    }
}
