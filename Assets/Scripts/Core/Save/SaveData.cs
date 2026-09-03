using System.Collections.Generic;

namespace MkLike.Core.Save
{
    /// <summary>
    /// 게임 저장 데이터의 최상위 컨테이너.
    /// 모든 저장 대상 데이터를 포함한다.
    /// </summary>
    [System.Serializable]
    public class SaveData
    {
        public PlayerData player = new PlayerData();
        public CurrencyData currency = new CurrencyData();
        public ProgressData progress = new ProgressData();
        public EquipmentSaveData equipment = new EquipmentSaveData();
        // RelicSaveData relic: 2026-04-20 유물 시스템 완전 제거 (기존 저장 필드는 JsonUtility가 무시)
        public WeaponSaveData weapon = new WeaponSaveData();
        public DungeonSaveData dungeon = new DungeonSaveData();
        public AttendanceSaveData attendance = new AttendanceSaveData();
        public MailboxSaveData mailbox = new MailboxSaveData();
        public QuestSaveData quest = new QuestSaveData();
        public AchievementSaveData achievement = new AchievementSaveData();
        // climberPowerLevel: 2026-04-20 Climber 시스템 삭제 (JsonUtility가 기존 값 무시)
        public BattlePassSaveData battlePass = new BattlePassSaveData();
        public PetSaveData pet = new PetSaveData();
        public TutorialSaveData tutorial = new TutorialSaveData();
        public MasterySaveData mastery = new MasterySaveData();
        // heroPower / ability / artifact / inscription / costume: 2026-04-20 SHALLOW 대청소 삭제
        public BoosterSaveData booster = new BoosterSaveData();
        public GachaPitySaveData gachaPity = new GachaPitySaveData();
        public PotentialPitySaveData potentialPity = new PotentialPitySaveData();
        public SkillLevelSaveData skillLevel = new SkillLevelSaveData();
        public EventDungeonSaveData eventDungeon = new EventDungeonSaveData();
        public ArenaSaveData arena = new ArenaSaveData();
        public GuildSaveData guild = new GuildSaveData();

        /// <summary>
        /// 서버로 최초 마이그레이션 완료 여부 (P1-10).
        /// true면 이후 MigrateAsync를 재호출하지 않는다.
        /// </summary>
        public bool migratedToServer;
    }

    /// <summary>
    /// 플레이어 기본 정보.
    /// </summary>
    [System.Serializable]
    public class PlayerData
    {
        /// <summary>플레이어 닉네임 (온보딩에서 최초 1회 설정)</summary>
        public string nickname = "";
        /// <summary>플레이어 레벨</summary>
        public int level = 1;
        /// <summary>누적 경험치</summary>
        public long exp = 0;
        /// <summary>직업 ID</summary>
        public string jobId = "";
        /// <summary>직업 티어 (전직 단계)</summary>
        public int jobTier = 0;
        /// <summary>스탯 데이터</summary>
        public StatData stats = new StatData();
    }

    /// <summary>
    /// 플레이어 스탯 데이터.
    /// </summary>
    [System.Serializable]
    public class StatData
    {
        /// <summary>공격력</summary>
        public int atk = 0;
        /// <summary>방어력</summary>
        public int def = 0;
        /// <summary>체력</summary>
        public int hp = 0;
        /// <summary>치명타</summary>
        public int crit = 0;
        /// <summary>명중률</summary>
        public int accuracy = 0;
    }

    /// <summary>
    /// 재화 데이터.
    /// BigNumber 전환: 기존 long 필드는 구세이브 마이그레이션 용으로 유지하고,
    /// `*_big` 문자열 필드(BigNumber 직렬화)를 우선 사용한다.
    /// `*_big`이 비어있으면 long 필드 fallback. 저장 시 양쪽 모두 기록한다.
    /// </summary>
    [System.Serializable]
    public class CurrencyData
    {
        // ── 레거시 long 필드 (구세이브 호환용) ──
        public long gold = 10000;
        public long ruby = 10000;
        public long blueDiamond = 10000;
        public long weaponTicket = 100;
        public long runeFragment = 10000;
        public long starCrystal = 10000;
        public long potentialStone = 10000;
        public long superPotentialStone = 10000;
        public long climbToken = 10000;
        public long huntPoint = 10000;
        public long weaponStone = 10000;

        // ── BigNumber 직렬화 문자열 (신규, 우선 사용) ──
        public string gold_big = "";
        public string ruby_big = "";
        public string blueDiamond_big = "";
        public string weaponTicket_big = "";
        public string runeFragment_big = "";
        public string starCrystal_big = "";
        public string potentialStone_big = "";
        public string superPotentialStone_big = "";
        public string climbToken_big = "";
        public string huntPoint_big = "";
        public string weaponStone_big = "";
    }

    /// <summary>
    /// 장비 저장 데이터.
    /// </summary>
    [System.Serializable]
    public class EquipmentSaveData
    {
        public System.Collections.Generic.List<EquipmentInstance> inventory = new();
        public System.Collections.Generic.List<EquippedSlotData> equippedSlots = new();
        /// <summary>슬롯별 강화 데이터 (장비가 아닌 슬롯에 귀속)</summary>
        public System.Collections.Generic.List<SlotEnhancementSaveData> slotEnhancements = new();
    }

    /// <summary>
    /// 장착 슬롯 저장 데이터.
    /// </summary>
    [System.Serializable]
    public class EquippedSlotData
    {
        public string slot;
        public string instanceId;
    }

    /// <summary>
    /// 슬롯 강화 저장 데이터.
    /// </summary>
    [System.Serializable]
    public class SlotEnhancementSaveData
    {
        public string slot;
        public SlotEnhancementData enhancement = new();
    }

    /// <summary>
    /// 진행 상황 데이터.
    /// </summary>
    [System.Serializable]
    public class ProgressData
    {
        /// <summary>현재 층</summary>
        public int currentFloor = 1;
        /// <summary>최고 도달 층</summary>
        public int maxFloor = 1;
        /// <summary>마지막 로그인 시각 (ISO 8601 문자열)</summary>
        public string lastLoginTime = "";
        /// <summary>마지막 로그아웃 시각 (ISO 8601 문자열). 앱 일시정지/종료 시 기록.</summary>
        public string lastLogoutUtc = "";
    }

    /// <summary>
    /// 퀘스트 저장 데이터.
    /// </summary>
    [System.Serializable]
    public class QuestSaveData
    {
        public List<QuestProgressData> activeQuests = new();
        /// <summary>일일 퀘스트 마지막 리셋 시각 (ISO 8601)</summary>
        public string lastDailyReset = "";
        /// <summary>주간 퀘스트 마지막 리셋 시각 (ISO 8601)</summary>
        public string lastWeeklyReset = "";
        /// <summary>완료된 가이드 퀘스트 체인 인덱스 (-1 = 미시작)</summary>
        public int completedGuideIndex = -1;
        /// <summary>순환 퀘스트 현재 순서 인덱스 (0부터 시작)</summary>
        public int cyclingCurrentOrder;
        /// <summary>순환 퀘스트 회차 (1부터 시작, 0 = 미시작)</summary>
        public int cyclingRound;
    }

    /// <summary>
    /// 개별 퀘스트 진행 데이터.
    /// </summary>
    [System.Serializable]
    public class QuestProgressData
    {
        public string questId;
        public int currentAmount;
        public bool isCompleted;
        public bool isRewardClaimed;
    }

    /// <summary>
    /// 업적 저장 데이터.
    /// </summary>
    [System.Serializable]
    public class AchievementSaveData
    {
        public List<AchievementProgressData> progress = new();
        /// <summary>업적 카운터 (Dictionary 대신 List로 직렬화)</summary>
        public List<AchievementCounterData> counters = new();
    }

    /// <summary>
    /// 개별 업적 진행 데이터.
    /// </summary>
    [System.Serializable]
    public class AchievementProgressData
    {
        public string achievementId;
        public int currentAmount;
        public bool isCompleted;
        public bool isClaimed;
    }

    /// <summary>
    /// 업적 카운터 키-값 쌍 (Dictionary 직렬화 대체).
    /// </summary>
    [System.Serializable]
    public class AchievementCounterData
    {
        public string key;
        public int value;
    }

    // RelicSaveData, RelicInstanceData: 2026-04-20 유물 시스템 완전 제거

    [System.Serializable]
    public class WeaponSaveData
    {
        public List<WeaponInstance> inventory = new();
        public string equippedInstanceId = "";
    }

    [System.Serializable]
    public class DungeonSaveData
    {
        public int currentKeys = 3;
        public string lastRechargeDate = "";
        public int keysUsedToday;
        /// <summary>클리어(score >= 1.0) 완료한 던전 ID 목록 (소탕 해금용)</summary>
        public List<string> clearedDungeonIds = new();
        /// <summary>보스 레이드 주간 사용 횟수 (주 3회 리셋)</summary>
        public int bossRaidWeeklyAttemptsUsed;
    }

    [System.Serializable]
    public class AttendanceSaveData
    {
        public int consecutiveDays;
        public string lastCheckDate = "";
    }

    [System.Serializable]
    public class MailboxSaveData
    {
        public List<MailItemData> mails = new();
    }

    [System.Serializable]
    public class MailItemData
    {
        public string id;
        public string sender;
        public string title;
        public string message;
        public string rewardType;
        public int rewardAmount;
        public string expiryDate;
        public bool isRead;
        public bool isClaimed;
    }

    // ── Phase 2 저장 구조 (배틀패스/PvP/펫) ──

    [System.Serializable]
    public class BattlePassSaveData
    {
        public string seasonId = "";
        public int currentLevel;
        public int currentBxp;
        public bool isPremium;
        public List<int> claimedFreeLevels = new();
        public List<int> claimedPremiumLevels = new();
        /// <summary>BattlePassSystem 호환 필드 (level 별칭)</summary>
        public int level { get => currentLevel; set => currentLevel = value; }
        /// <summary>수령 완료 무료 보상 레벨 (BattlePassSystem 호환)</summary>
        public List<int> claimedFreeRewards { get => claimedFreeLevels; set => claimedFreeLevels = value; }
        /// <summary>수령 완료 프리미엄 보상 레벨 (BattlePassSystem 호환)</summary>
        public List<int> claimedPremiumRewards { get => claimedPremiumLevels; set => claimedPremiumLevels = value; }
        /// <summary>시즌 시작 시각 (ISO 8601)</summary>
        public string seasonStartTime = "";
        /// <summary>시즌 종료 시각 (ISO 8601)</summary>
        public string seasonEndTime = "";
    }

    [System.Serializable]
    public class PetSaveData
    {
        public List<PetInstanceData> owned = new();
        public string equippedPetId = "";
    }

    [System.Serializable]
    public class PetInstanceData
    {
        public string petId;
        public string grade;
        public int level = 1;
        public int duplicateCount;
    }

    // ArtifactSaveData / ArtifactInstanceData: 2026-04-20 Artifact 시스템 완전 제거
    // AbilitySaveData: 2026-04-20 Ability 시스템 완전 제거

    /// <summary>
    /// 부스터 저장 데이터 (활성 부스터 + 보유 수량).
    /// </summary>
    [System.Serializable]
    public class BoosterSaveData
    {
        public List<ActiveBoosterEntry> activeBoosters = new();
        public int totalUsed;
        /// <summary>보유 수량: [0]=ExpBoost, [1]=GoldBoost, [2]=DropRateBoost</summary>
        public List<int> stock = new() { 0, 0, 0 };
    }

    /// <summary>
    /// 활성 부스터 엔트리.
    /// </summary>
    [System.Serializable]
    public class ActiveBoosterEntry
    {
        public string boosterId;
        public float remainingSeconds;
    }

    // HeroPowerSaveData: 2026-04-20 HeroPower 시스템 완전 제거

    /// <summary>
    /// 마스터리 저장 데이터.
    /// </summary>
    [System.Serializable]
    public class MasterySaveData
    {
        /// <summary>해금된 마스터리 노드 ID 목록</summary>
        public List<string> unlockedNodes = new();
        /// <summary>사용 가능한 마스터리 포인트</summary>
        public int availablePoints;
        /// <summary>총 획득한 마스터리 포인트</summary>
        public int totalEarnedPoints;
    }

    // InscriptionSaveData: 2026-04-20 Inscription 시스템 완전 제거

    /// <summary>
    /// 가챠 풀별 누적 뽑기 수 저장 데이터.
    /// (기존 pity 필드는 마이그레이션 호환용으로 유지)
    /// </summary>
    [System.Serializable]
    public class GachaPitySaveData
    {
        public int equipmentPity;
        public int weaponPity;
        // relicPity: 2026-04-20 유물 시스템 완전 제거 (필드 삭제, JsonUtility가 무시)

        // 소환 레벨 시스템: 누적 뽑기 수
        public int equipmentTotalPulls;
        public int weaponTotalPulls;
        // relicTotalPulls: 2026-04-20 유물 시스템 완전 제거
        // 마이그레이션 완료 플래그
        public bool isMigrated;
    }

    /// <summary>
    /// 잠재능력 천장 카운터 저장 데이터 (장비 인스턴스별).
    /// </summary>
    [System.Serializable]
    public class PotentialPitySaveData
    {
        public List<PotentialPityEntry> main = new();
        public List<PotentialPityEntry> sub = new();
    }

    /// <summary>
    /// 개별 잠재능력 천장 엔트리.
    /// </summary>
    [System.Serializable]
    public class PotentialPityEntry
    {
        public string instanceId;
        public int count;
    }

    // CostumeSaveData / CostumeInstanceData: 2026-04-20 Costume 시스템 완전 제거

    /// <summary>
    /// 스킬 레벨 저장 데이터.
    /// 2026-04-23 Phase B 스킬 슬롯 재설계 준비:
    /// 기존 entries (skillId → level 딕셔너리)는 유지 (하위 호환).
    /// 신규 slotLevels[4] 필드 추가 — 향후 ActiveSkillSystem에서 사용.
    /// 마이그레이션: MigrateToSlotLevels() 호출 시 entries의 상위 4개 level을 slotLevels로 이전.
    /// </summary>
    [System.Serializable]
    public class SkillLevelSaveData
    {
        public List<SkillLevelEntry> entries = new();

        /// <summary>액티브 슬롯 4개의 레벨 (0=기본공격/1~3=액티브). 0 = 미해금/Lv.1.</summary>
        public int[] slotLevels = new int[4];

        /// <summary>마이그레이션 완료 플래그 (중복 실행 방지).</summary>
        public bool slotMigrated = false;

        /// <summary>
        /// 기존 entries(skillId 기반 레벨)를 slotLevels[4]로 마이그레이션한다.
        /// 가장 높은 level 4개를 슬롯 0~3에 배치 (1회만 실행).
        /// </summary>
        public void MigrateToSlotLevels()
        {
            if (slotMigrated) return;
            if (entries == null || entries.Count == 0)
            {
                slotMigrated = true;
                return;
            }

            // 레벨 내림차순 정렬 후 상위 4개 추출
            var top4 = new List<int>();
            foreach (var e in entries)
                top4.Add(e.level);
            top4.Sort((a, b) => b.CompareTo(a));

            for (int i = 0; i < slotLevels.Length; i++)
                slotLevels[i] = i < top4.Count ? top4[i] : 0;

            slotMigrated = true;
        }
    }

    /// <summary>
    /// 개별 스킬 레벨 엔트리 (레거시 — Phase B 슬롯 재설계 완전 이행 전까지 유지).
    /// </summary>
    [System.Serializable]
    public class SkillLevelEntry
    {
        public string skillId;
        public int level;
    }

    // ── 이벤트 던전 저장 (Phase 13) ──

    /// <summary>이벤트 던전 저장 데이터.</summary>
    [System.Serializable]
    public class EventDungeonSaveData
    {
        public int usedFreeEntries;
        public string lastResetDate = "";
        public List<EventDungeonGradeEntry> bestGrades = new();
    }

    /// <summary>이벤트 던전 등급 기록 엔트리.</summary>
    [System.Serializable]
    public class EventDungeonGradeEntry
    {
        /// <summary>"dungeonId_difficultyIndex" 형식</summary>
        public string key;
        /// <summary>ClearGrade enum 이름 (S/A/B/C)</summary>
        public string grade;
    }

    // ── 아레나 저장 (Phase 13) ──

    /// <summary>아레나 PvP 저장 데이터.</summary>
    [System.Serializable]
    public class ArenaSaveData
    {
        /// <summary>현재 티어 인덱스 (0=Bronze, 1=Silver, 2=Gold, 3=Diamond)</summary>
        public int currentTier;
        /// <summary>현재 레이팅 점수</summary>
        public int rating;
        /// <summary>총 승리 횟수</summary>
        public int totalVictories;
        /// <summary>총 패배 횟수</summary>
        public int totalDefeats;
        /// <summary>현재 연승 수</summary>
        public int currentWinStreak;
        /// <summary>최고 연승 기록</summary>
        public int bestWinStreak;
        /// <summary>오늘 사용한 무료 도전 횟수</summary>
        public int usedFreeEntries;
        /// <summary>마지막 일일 리셋 날짜 (yyyy-MM-dd)</summary>
        public string lastResetDate = "";
        /// <summary>현재 시즌 ID</summary>
        public string seasonId = "";
        /// <summary>시즌 시작 시각 (ISO 8601)</summary>
        public string seasonStartTime = "";
        /// <summary>전적 기록 (최근 50전)</summary>
        public List<ArenaRecordData> records = new();
    }

    /// <summary>아레나 전적 기록 항목.</summary>
    [System.Serializable]
    public class ArenaRecordData
    {
        public string opponentName;
        public string opponentJob;
        public int opponentElo;
        public bool isVictory;
        public int eloChange;
        public long playerDamage;
        public long opponentDamage;
        public string timestamp;
    }

    // ── 길드 저장 (Phase 13) ──

    /// <summary>길드 저장 데이터.</summary>
    [System.Serializable]
    public class GuildSaveData
    {
        /// <summary>가입한 길드 이름 (빈 문자열 = 미가입)</summary>
        public string guildName = "";
        /// <summary>길드 레벨</summary>
        public int guildLevel = 1;
        /// <summary>길드 경험치</summary>
        public int guildExp;
        /// <summary>오늘 기부 횟수 (골드)</summary>
        public int goldDonateCount;
        /// <summary>오늘 기부 횟수 (루비)</summary>
        public int rubyDonateCount;
        /// <summary>오늘 기부 횟수 (소탕권)</summary>
        public int ticketDonateCount;
        /// <summary>마지막 기부 리셋 날짜 (yyyy-MM-dd)</summary>
        public string lastDonateResetDate = "";
        /// <summary>이번 주 길드 보스 도전 횟수</summary>
        public int bossAttemptsThisWeek;
        /// <summary>마지막 보스 리셋 날짜 (yyyy-MM-dd, 월요일)</summary>
        public string lastBossResetDate = "";
        /// <summary>길드 보스 누적 데미지</summary>
        public long bossTotalDamage;
        /// <summary>길드 보스 처치 여부</summary>
        public bool isBossDefeated;
        /// <summary>가입 여부</summary>
        public bool isJoined;
        /// <summary>이번 주 토벌전 사용 여부 (Phase 14)</summary>
        public bool raidUsedThisWeek;
    }
}
