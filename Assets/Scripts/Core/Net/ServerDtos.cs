using System;
using System.Collections.Generic;

namespace MkLike.Core.Net
{
    // ── Auth ──

    [Serializable]
    public class LoginRequest
    {
        public string firebaseIdToken;
    }

    [Serializable]
    public class LoginResponse
    {
        public string playerId;
        public string nickname;
        public bool isNewPlayer;
        public string serverTime;
    }

    [Serializable]
    public class LogoutResponse
    {
        public long playerId;
        public string logoutTime;
        public string serverTime;
    }

    [Serializable]
    public class PlayerProfileUpdateRequest
    {
        public int level;
        public long combatPower;
        public string nickname;
        public string jobId;
    }

    [Serializable]
    public class PlayerProfileResponse
    {
        public long playerId;
        public string nickname;
        public int level;
        public long combatPower;
        public string jobId;
        public string serverTime;
    }

    // ── Gacha ──

    [Serializable]
    public class GachaPullRequest
    {
        public string poolType;
        public int pullCount;
    }

    [Serializable]
    public class GachaPullResponse
    {
        public GachaResultItem[] results;
        public CurrencySpentDto currencySpent;
        public long currencyRemaining;
        public int pityCount;
        public SummonLevelDto summonLevel;
    }

    [Serializable]
    public class GachaResultItem
    {
        public string itemId;
        public string grade;
        public int tier;
        public bool isNew;
    }

    [Serializable]
    public class CurrencySpentDto
    {
        public string type;
        public long amount;
    }

    [Serializable]
    public class SummonLevelDto
    {
        public string pool;
        public int totalPulls;
        public int level;
    }

    // ── Currency ──

    [Serializable]
    public class CurrencySpendRequest
    {
        public string currencyType;
        public long amount;
        public string reason;
        public string referenceId;
    }

    [Serializable]
    public class CurrencyEarnRequest
    {
        public string currencyType;
        public long amount;
        public string reason;
        public string referenceId;
    }

    [Serializable]
    public class CurrencyBalanceResponse
    {
        public CurrencyEntryDto[] currencies;
    }

    [Serializable]
    public class CurrencyEntryDto
    {
        public string type;
        public long amount;
    }

    [Serializable]
    public class CurrencyTransactionResponse
    {
        public string currencyType;
        public long amount;
        public long balanceAfter;
    }

    // ── Save ──

    [Serializable]
    public class SaveSyncRequest
    {
        public string saveData;
        public string clientTimestamp;
    }

    [Serializable]
    public class SaveSyncResponse
    {
        public string playerId;
        public int versionUpdated;
        public string serverTime;
        public int nextSyncWindow;
    }

    [Serializable]
    public class SaveLoadResponse
    {
        public string playerId;
        public string saveData;
        public int version;
        public string lastUpdatedAt;
        public string serverTime;
    }

    // 최초 로그인 시 로컬 SaveData.json → 서버 1회 업로드 (S251-01)
    [Serializable]
    public class SaveMigrateRequest
    {
        public string saveData;
        public string clientTimestamp;
    }

    [Serializable]
    public class SaveMigrateResponse
    {
        public string playerId;
        public bool migrated;
        public int version;
        public string serverTime;
        public string reason; // "already_exists" 등. migrated=true면 null
    }

    // ── Attendance ──

    [Serializable]
    public class AttendanceCheckResponse
    {
        public int consecutiveDays;
        public AttendanceRewardDto reward;
        public string serverDate;
        public string nextCheckTime;
    }

    [Serializable]
    public class AttendanceRewardDto
    {
        public string type;
        public int amount;
    }

    // ── Arena ──

    [Serializable]
    public class ArenaBattleRequest
    {
        public int candidateIndex;
        public long playerCp;
    }

    [Serializable]
    public class ArenaCandidatesResponse
    {
        public ArenaCandidateDto[] candidates;
        public int remainingEntries;
    }

    [Serializable]
    public class ArenaCandidateDto
    {
        public string name;
        public string job;
        public long cp;
        public int tier;
    }

    [Serializable]
    public class ArenaBattleResponse
    {
        public bool isVictory;
        public int ratingChange;
        public int newRating;
        public int newTier;
        public int currentWinStreak;
    }

    [Serializable]
    public class ArenaStatusResponse
    {
        public int currentTier;
        public int rating;
        public int totalVictories;
        public int totalDefeats;
        public int currentWinStreak;
        public int bestWinStreak;
        public int remainingEntries;
        public string seasonId;
        public ArenaRecordResponseDto[] recentRecords;
    }

    [Serializable]
    public class ArenaRecordResponseDto
    {
        public string opponentName;
        public string opponentJob;
        public long opponentCp;
        public bool isVictory;
        public int ratingChange;
        public string timestamp;
    }

    // ── Arena Leaderboard ──

    [Serializable]
    public class ArenaLeaderboardResponse
    {
        public ArenaLeaderboardEntry[] entries;
        public string serverTime;
    }

    [Serializable]
    public class ArenaLeaderboardEntry
    {
        public int rank;
        public string nickname;
        public int rating;
        public int tier;
        public int totalVictories;
        public int bestWinStreak;
    }

    // ── Guild ──

    [Serializable]
    public class GuildCreateRequest
    {
        public string guildName;
    }

    [Serializable]
    public class GuildJoinRequest
    {
        public long guildId;
    }

    [Serializable]
    public class GuildInfoResponse
    {
        public long guildId;
        public string guildName;
        public int level;
        public int exp;
        public int nextLevelExp;
        public int memberCount;
        public bool isBossDefeated;
    }

    [Serializable]
    public class GuildDonateRequest
    {
        public string donationType;
    }

    [Serializable]
    public class GuildDonateResponse
    {
        public string donationType;
        public int guildExpGained;
        public int remainingDonations;
        public long currencyRemaining;
    }

    [Serializable]
    public class GuildBossResultRequest
    {
        public long playerCp;
    }

    [Serializable]
    public class GuildBossResultResponse
    {
        public long damageDealt;
        public bool isBossDefeated;
        public float contributionPercent;
        public int contributionRank;
        public float rewardMultiplier;
        public int rewardRuby;
        public long rewardGold;
        public int remainingAttempts;
    }

    // ── HotDeal ──

    [Serializable]
    public class HotDealCatalogResponse
    {
        public HotDealInfoDto[] deals;
        public string serverTime;
    }

    [Serializable]
    public class HotDealInfoDto
    {
        public string dealId;
        public string category;
        public string name;
        public string condition;
        public int durationHours;
        public HotDealCostDto cost;
        public HotDealRewardDto[] rewards;
        public bool isPurchased;
        public string expiresAt;
    }

    [Serializable]
    public class HotDealCostDto
    {
        public string type;
        public long amount;
    }

    [Serializable]
    public class HotDealRewardDto
    {
        public string item;
        public long amount;
    }

    [Serializable]
    public class HotDealPurchaseRequest
    {
        public string dealId;
    }

    [Serializable]
    public class HotDealPurchaseResponse
    {
        public string dealId;
        public HotDealRewardDto[] rewardsGranted;
        public long currencyRemaining;
        public string serverTime;
    }

    // --- Phase 26 Sprint 26-1 Data Delivery ---
    // S261-01 GET /api/v1/data/version
    [Serializable]
    public class DataVersionResponse
    {
        public string visual;
        public string config;
        public string balance;
        public string catalog;
        public string serverTime;
    }

    // S261-02 GET /api/v1/data/config/latest?type=...
    [Serializable]
    public class DataConfigLatestResponse
    {
        public DataConfigEntry[] entries;
        public string serverTime;
    }

    [Serializable]
    public class DataConfigEntry
    {
        public string type;
        public string hash;
        public string url;
    }

    // S261-05 GET /api/v1/system/notice (Phase 27-6 강제 업데이트 선행 필드 포함)
    [Serializable]
    public class NoticeResponse
    {
        public bool active;
        public string title;
        public string body;
        public string startsAt;  // ISO 8601 or null
        public string endsAt;    // ISO 8601 or null
        public string serverTime;
        public string minClientVersion;    // 빈 문자열이면 강제 업데이트 없음
        public string forceUpdateMessage;  // 빈 문자열이면 클라 기본 문구
    }
}
