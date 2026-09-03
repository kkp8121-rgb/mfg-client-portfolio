using UnityEngine;
using MkLike.Utils;

namespace MkLike.Core
{
    /// <summary>
    /// 재화 변경 이벤트. BigNumber 전환 후: long/int 리터럴은 암시적 변환으로 그대로 대입 가능.
    /// </summary>
    public struct CurrencyChangedEvent : IEvent
    {
        public CurrencyType Type;
        public BigNumber PreviousAmount;
        public BigNumber CurrentAmount;
    }

    /// <summary>
    /// 재화 부족 이벤트. CurrencyManager.Spend() 실패 시 발행.
    /// CurrencyShortagePopup이 구독하여 팝업을 표시한다.
    /// </summary>
    public struct CurrencyShortageEvent : IEvent
    {
        public CurrencyType Type;
        public BigNumber Required;
        public BigNumber Current;
    }

    /// <summary>
    /// 레벨업 이벤트.
    /// 플레이어 레벨이 상승할 때 발행된다.
    /// </summary>
    public struct LevelUpEvent : IEvent
    {
        /// <summary>이전 레벨</summary>
        public int PreviousLevel;
        /// <summary>현재 레벨</summary>
        public int CurrentLevel;
        /// <summary>이번 레벨업으로 증가한 HP</summary>
        public int HpGain;
        /// <summary>이번 레벨업으로 증가한 ATK</summary>
        public int AtkGain;
        /// <summary>이번 레벨업으로 증가한 DEF</summary>
        public int DefGain;
        /// <summary>획득한 자유 스탯 포인트</summary>
        public int StatPoints;
    }

    /// <summary>
    /// 스테이지 변경 이벤트.
    /// 챕터-스테이지 전환 시 발행된다.
    /// </summary>
    public struct StageChangedEvent : IEvent
    {
        /// <summary>현재 챕터</summary>
        public int Chapter;
        /// <summary>현재 스테이지 인덱스 (1~9: 일반, 10+: 보스)</summary>
        public int StageIndex;
        /// <summary>표시명 (예: "1-3", "2-BOSS")</summary>
        public string DisplayName;
    }

    /// <summary>
    /// 스테이지 킬 진행도 이벤트.
    /// 몬스터 처치 시마다 발행. HUD 진행바 등에서 사용.
    /// </summary>
    public struct StageProgressEvent : IEvent
    {
        public int KillCount;
        public int RequiredKills;
        public string StageName;
    }

    /// <summary>
    /// 게임 상태 변경 이벤트.
    /// 게임의 전체 상태(로비, 전투, 일시정지 등)가 전환될 때 발행된다.
    /// </summary>
    public struct GameStateChangedEvent : IEvent
    {
        /// <summary>이전 상태</summary>
        public string PreviousState;
        /// <summary>현재 상태</summary>
        public string CurrentState;
    }

    /// <summary>
    /// 세이브 완료 이벤트.
    /// 게임 데이터 저장이 완료되었을 때 발행된다.
    /// </summary>
    public struct BeforeSaveEvent : IEvent { }
    public struct SaveCompletedEvent : IEvent { }

    /// <summary>
    /// 로드 완료 이벤트.
    /// 게임 데이터 로딩이 완료되었을 때 발행된다.
    /// </summary>
    public struct LoadCompletedEvent : IEvent { }

    /// <summary>
    /// 서버 연결 완료 이벤트.
    /// ServerBootstrap가 로그인 + SaveProvider 전환을 완료했을 때 발행된다.
    /// </summary>
    public struct ServerConnectedEvent : IEvent { }

    /// <summary>
    /// 경험치 획득 이벤트.
    /// 몬스터 처치 등으로 경험치를 획득했을 때 발행된다.
    /// </summary>
    public struct ExpGainedEvent : IEvent
    {
        /// <summary>획득한 경험치 양</summary>
        public long Amount;
    }

    /// <summary>
    /// 골드 획득 이벤트.
    /// 몬스터 처치 등으로 골드를 획득했을 때 발행된다. 연출용 위치 정보를 포함한다.
    /// </summary>
    public struct GoldGainedEvent : IEvent
    {
        /// <summary>획득한 골드 양</summary>
        public long Amount;
        /// <summary>골드 획득 위치 (연출용)</summary>
        public Vector3 Position;
    }

    /// <summary>
    /// 몬스터 사망 이벤트.
    /// </summary>
    public struct MonsterDiedEvent : IEvent
    {
        public GameObject Monster;
        public Vector3 Position;
    }

    /// <summary>
    /// 전리품 드롭 이벤트. 확률적 재화 드롭 시 발행 (연출용).
    /// </summary>
    public struct LootDroppedEvent : IEvent
    {
        public CurrencyType Type;
        public long Amount;
        public Vector3 Position;
        /// <summary>드롭 등급 (0=일반, 1=희귀, 2=에픽, 3=전설)</summary>
        public int Grade;
    }

    /// <summary>
    /// 가챠 결과 이벤트.
    /// </summary>
    public struct GachaResultEvent : IEvent
    {
        public string ItemId;
        public string Grade;
        public string PoolName;
        /// <summary>티어 (0=없음, 1=T1 최고, 4=T4 보통)</summary>
        public int Tier;
    }

    /// <summary>
    /// 소환 레벨 변경 이벤트.
    /// </summary>
    public struct SummonLevelUpEvent : IEvent
    {
        public string PoolName;
        public int NewLevel;
        public int MaxLevel;
    }

    /// <summary>
    /// 파밍 모드 전환 이벤트.
    /// 미니보스 실패 → 무한 파밍, 재도전 시 해제.
    /// </summary>
    public struct FarmingModeEvent : IEvent
    {
        public bool IsActive;
        public string StageName;
        /// <summary>true이면 플레이어가 사망하여 재도전 대기 상태</summary>
        public bool IsPlayerDead;
    }

    /// <summary>
    /// 스킬 습득 이벤트.
    /// </summary>
    public struct SkillLearnedEvent : IEvent
    {
        public string SkillId;
        public string SkillName;
        public SkillType SkillType;
    }

    /// <summary>
    /// 스킬 사용 이벤트 (연출/UI용).
    /// </summary>
    public struct SkillUsedEvent : IEvent
    {
        public string SkillId;
        public string SkillName;
        public SkillType SkillType;
    }

    /// <summary>
    /// 스킬 레벨업 이벤트.
    /// </summary>
    public struct SkillLevelUpEvent : IEvent
    {
        public string SkillId;
        public int NewLevel;
    }

    /// <summary>
    /// 강제 레벨업 이벤트 (테스트용).
    /// LevelSystem이 구독하여 처리한다.
    /// </summary>
    public struct ForceLevelUpEvent : IEvent
    {
        public int Levels;
    }

    /// <summary>
    /// 장비 장착/해제 이벤트.
    /// </summary>
    public struct EquipmentChangedEvent : IEvent
    {
        public EquipmentSlot Slot;
        public string InstanceId;
        public string EquipmentId;
        public string Grade;
        public bool IsEquipped;
    }

    /// <summary>
    /// 장비 인벤토리 변경 이벤트 (획득/삭제).
    /// </summary>
    public struct EquipmentInventoryChangedEvent : IEvent
    {
        public string InstanceId;
        public string EquipmentId;
        public string Grade;
        public bool IsAdded;
    }

    /// <summary>
    /// 장비 각성(중복 겹치기) 이벤트.
    /// </summary>
    public struct EquipmentAwakenedEvent : IEvent
    {
        public string InstanceId;
        public string EquipmentId;
        public string Grade;
        public int NewAwakeningStars;
    }

    /// <summary>주문서 강화 결과 이벤트.</summary>
    public struct ScrollEnhanceEvent : IEvent
    {
        public string InstanceId;
        public bool IsSuccess;
        public int NewScrollLevel;
    }

    /// <summary>스타포스 강화 결과 이벤트.</summary>
    public struct StarForceEvent : IEvent
    {
        public string InstanceId;
        public bool IsSuccess;
        public int NewStarForce;
        /// <summary>결과 상세: Success, Fail, Downgrade, Destroy</summary>
        public StarForceResult Result;
    }

    /// <summary>스타포스 강화 결과 타입.</summary>
    public enum StarForceResult
    {
        Success,    // 성공 (+1)
        Fail,       // 실패 (유지)
        Downgrade,  // 하락 (-1, 15성 이상)
        Destroy     // 파괴 (12성으로 리셋, 20성 이상)
    }

    /// <summary>더 좋은 장비 추천 알림 이벤트.</summary>
    public struct EquipmentRecommendationEvent : IEvent
    {
        public int RecommendationCount;
        public int TopCpGain;
    }

    /// <summary>
    /// 능력치 분배 이벤트.
    /// </summary>
    public struct StatAllocatedEvent : IEvent
    {
        public string StatName;
        public int PointsSpent;
        public int TotalAllocated;
    }

    /// <summary>
    /// 직업 변경 이벤트.
    /// </summary>
    public struct JobChangedEvent : IEvent
    {
        public string PreviousJobId;
        public string CurrentJobId;
        public int PreviousTier;
        public int CurrentTier;
    }

    // RelicObtainedEvent / RelicEquippedEvent: 2026-04-20 유물 시스템 완전 제거

    /// <summary>
    /// 던전 입장 이벤트. 던전 진입 연출에 사용.
    /// </summary>
    public struct DungeonEnteredEvent : IEvent
    {
        public string DungeonId;
        public string DungeonName;
        public string DungeonType;
    }

    /// <summary>던전 전투 시작 이벤트. HUD 타이머 표시용.</summary>
    public struct DungeonBattleStartedEvent : IEvent
    {
        public string DungeonId;
        public string DungeonName;
        public float Duration;
        public int TargetKills;
    }

    /// <summary>던전 전투 진행 이벤트. HUD 킬카운터 갱신용.</summary>
    public struct DungeonBattleProgressEvent : IEvent
    {
        public int KillCount;
        public int TargetKills;
        public float RemainingTime;
    }

    /// <summary>던전 전투 종료 이벤트. 결과 팝업용.</summary>
    public struct DungeonBattleEndedEvent : IEvent
    {
        public string DungeonId;
        public string DungeonName;
        public int KillCount;
        public int TargetKills;
        public float Score;
        public bool IsSuccess;
    }

    /// <summary>
    /// 던전 완료 이벤트.
    /// </summary>
    public struct DungeonCompletedEvent : IEvent
    {
        public string DungeonId;
        public string DungeonType;
        public int RewardAmount;
    }

    // ── EventDungeonEnteredEvent / EventDungeonCompletedEvent 제거 — 이벤트 던전 시스템 삭제 ──

    /// <summary>
    /// 무기 장착/해제 이벤트.
    /// </summary>
    public struct WeaponChangedEvent : IEvent
    {
        public string InstanceId;
        public string WeaponId;
        public string Grade;
        public bool IsEquipped;
    }

    /// <summary>
    /// 무기 인벤토리 변경 이벤트 (획득/삭제).
    /// </summary>
    public struct WeaponInventoryChangedEvent : IEvent
    {
        public string InstanceId;
        public string WeaponId;
        public string Grade;
        public bool IsAdded;
    }

    /// <summary>
    /// 무기 레벨업 이벤트.
    /// </summary>
    public struct WeaponLevelUpEvent : IEvent
    {
        public string InstanceId;
        public string WeaponId;
        public int OldLevel;
        public int NewLevel;
    }

    /// <summary>
    /// 무기 각성 이벤트.
    /// </summary>
    public struct WeaponAwakeningEvent : IEvent
    {
        public string InstanceId;
        public string WeaponId;
        public int OldStars;
        public int NewStars;
    }

    /// <summary>
    /// 무기 승급 이벤트.
    /// </summary>
    public struct WeaponPromoteEvent : IEvent
    {
        public string InstanceId;
        public string WeaponId;
        public string OldGrade;
        public string NewGrade;
    }

    /// <summary>
    /// 퀘스트 완료 이벤트.
    /// </summary>
    public struct QuestCompletedEvent : IEvent
    {
        public string QuestId;
        public QuestType QuestType;
    }

    /// <summary>
    /// 퀘스트 보상 수령 이벤트.
    /// </summary>
    public struct QuestRewardClaimedEvent : IEvent
    {
        public string QuestId;
        public CurrencyType RewardType;
        public int RewardAmount;
    }

    /// <summary>
    /// 출석 체크 완료 이벤트.
    /// </summary>
    public struct AttendanceCheckedEvent : IEvent
    {
        public int Day;
        public CurrencyType RewardType;
        public int RewardAmount;
    }

    /// <summary>
    /// 메일 수신 이벤트.
    /// </summary>
    public struct MailReceivedEvent : IEvent
    {
        public string MailId;
        public string Sender;
        public string Title;
    }

    /// <summary>
    /// 메일 보상 수령 이벤트.
    /// </summary>
    public struct MailClaimedEvent : IEvent
    {
        public string MailId;
        public CurrencyType RewardType;
        public int RewardAmount;
    }

    // ── Phase 2 이벤트 (배틀패스/PvP/펫) ──

    public struct BattlePassLevelUpEvent : IEvent
    {
        public int NewLevel;
        public bool IsPremium;
        /// <summary>BattlePassSystem 호환 필드 (NewLevel 별칭)</summary>
        public int Level { get => NewLevel; set => NewLevel = value; }
        /// <summary>마일스톤 여부 (10레벨 단위)</summary>
        public bool IsMilestone;
    }

    public struct BattlePassRewardClaimedEvent : IEvent
    {
        public int Level;
        public bool IsPremiumTrack;
        /// <summary>BattlePassSystem 호환 필드 (IsPremiumTrack 별칭)</summary>
        public bool IsPremium { get => IsPremiumTrack; set => IsPremiumTrack = value; }
        public CurrencyType RewardType;
        public int RewardAmount;
    }

    public struct AchievementCompletedEvent : IEvent
    {
        public string AchievementId;
    }

    public struct AchievementClaimedEvent : IEvent
    {
        public string AchievementId;
        public CurrencyType RewardType;
        public int RewardAmount;
    }

    public struct ShopPurchaseEvent : IEvent
    {
        public string ItemId;
        public CurrencyType CostType;
        public int CostAmount;
        public CurrencyType RewardType;
        public int RewardAmount;
    }

    // ── 시즌 이벤트 ──

    public struct SeasonStartedEvent : IEvent
    {
        public string SeasonId;
        public int DurationWeeks;
    }

    public struct SeasonEndedEvent : IEvent
    {
        public string SeasonId;
    }

    // ── 배틀패스 추가 이벤트 ──

    public struct BattlePassBxpGainedEvent : IEvent
    {
        public int Amount;
        public int CurrentBxp;
        public int CurrentLevel;
    }

    public struct BattlePassPremiumActivatedEvent : IEvent { }

    public struct SeasonPointChangedEvent : IEvent
    {
        public int PreviousAmount;
        public int CurrentAmount;
    }

    public struct SeasonShopPurchaseEvent : IEvent
    {
        public string ItemId;
        public int RemainingPurchases;
    }

    // ── 튜토리얼 이벤트 ──

    public struct TutorialStepStartedEvent : IEvent
    {
        public TutorialStep Step;
    }

    public struct TutorialStepCompletedEvent : IEvent
    {
        public TutorialStep Step;
    }

    public struct TutorialPauseCombatEvent : IEvent { }

    public struct TutorialResumeCombatEvent : IEvent { }

    // ── 오프라인 보상 이벤트 ──

    /// <summary>
    /// 오프라인 보상 수령 이벤트.
    /// 앱 복귀 시 오프라인 보상이 지급되면 발행된다. UI에서 팝업 표시용으로 구독한다.
    /// </summary>
    public struct OfflineRewardClaimedEvent : IEvent
    {
        public long GoldAmount;
        public long ExpAmount;
        public int ElapsedMinutes;
        public int TotalKills;
        public int HuntPoint;
        public int RuneFragment;
        public int StarCrystal;
        public int Ruby;
        /// <summary>장비 드롭 수 (Phase 13)</summary>
        public int EquipDropCount;
        /// <summary>적용된 레벨 배율 (Phase 13)</summary>
        public float LevelMultiplier;
    }

    /// <summary>
    /// 오프라인 보상 장비 드롭 요청 이벤트 (Phase 13).
    /// Combat → Equipment 순환 참조 방지를 위해 이벤트 기반으로 장비 드롭을 요청한다.
    /// EquipmentManager에서 구독하여 실제 장비를 생성한다.
    /// </summary>
    public struct OfflineEquipDropRequestEvent : IEvent
    {
        /// <summary>드롭할 장비 수</summary>
        public int Count;
        /// <summary>최소 등급</summary>
        public string MinGrade;
    }

    // InscriptionChangedEvent: 2026-04-20 Inscription 시스템 완전 제거

    // ── IAP 이벤트 ──

    /// <summary>
    /// IAP 구매 성공 이벤트.
    /// IAPManager에서 결제 완료 시 발행한다.
    /// </summary>
    public struct IAPPurchaseEvent : IEvent
    {
        public string ProductId;
        public string ProductType;
    }

    // ── 프레스티지(환생) 이벤트 ──

    /// <summary>
    /// 프레스티지 실행 이벤트.
    /// 환생 시 영구 보너스와 토큰 정보를 포함한다.
    /// </summary>
    public struct PrestigeExecutedEvent : IEvent
    {
        public int PrestigeCount;
        public float TotalBonus;
        public int TokensEarned;
        public int FloorAtPrestige;
    }

    // ── 축복 이벤트 ──

    /// <summary>
    /// 축복 변경 이벤트.
    /// 축복이 부여되거나 재롤되었을 때 발행된다.
    /// </summary>
    public struct BlessingChangedEvent : IEvent
    {
        /// <summary>축복 유형</summary>
        public BlessingType Type;
        /// <summary>재롤에 의한 변경인지 여부</summary>
        public bool IsReroll;
    }

    // AdRewardEvent: 2026-04-20 AdRewardManager 시스템 완전 제거 (SDK 미통합 stub)

    // ── 칭호 이벤트 ──

    /// <summary>
    /// 칭호 해금 이벤트.
    /// </summary>
    public struct TitleUnlockedEvent : IEvent
    {
        public string TitleId;
        public string TitleName;
        public string AchievementId;
    }

    /// <summary>
    /// 칭호 장착 변경 이벤트.
    /// </summary>
    public struct TitleEquippedEvent : IEvent
    {
        public string TitleId;
        public bool IsEquipped;
    }

    // ── 업적 확장용 이벤트 ──

    /// <summary>
    /// 시즌 보상 수령 이벤트 (업적 추적용).
    /// </summary>
    public struct SeasonRewardClaimedEvent : IEvent
    {
        public string SeasonId;
        public int ClaimedCount;
    }

    /// <summary>
    /// PvP 승리 이벤트 (업적 추적용).
    /// </summary>
    public struct PvpVictoryEvent : IEvent
    {
        public int TotalVictories;
        public int CurrentStreak;
    }

    // ClimbingPartyCompletedEvent: 2026-04-23 제거 — Climber 시스템 2026-04-20 삭제 이후 Publish 부재 dead code였음.

    // ── 이벤트 콘텐츠 이벤트 ──

    /// <summary>
    /// 이벤트 콘텐츠 시작 이벤트.
    /// </summary>
    public struct EventContentStartedEvent : IEvent
    {
        public string EventId;
        public EventContentType EventType;
        public int DurationDays;
    }

    /// <summary>
    /// 이벤트 콘텐츠 종료 이벤트.
    /// </summary>
    public struct EventContentEndedEvent : IEvent
    {
        public string EventId;
        public EventContentType EventType;
    }

    /// <summary>
    /// 이벤트 콘텐츠 완료 이벤트 (보상 지급 시).
    /// </summary>
    public struct EventContentCompletedEvent : IEvent
    {
        public string EventId;
        public CurrencyType RewardType;
        public int RewardAmount;
    }

    // ── 도감 이벤트 ──

    /// <summary>
    /// 도감 엔트리 등록 이벤트.
    /// 새로운 몬스터/장비/무기 등을 최초로 수집했을 때 발행된다.
    /// </summary>
    public struct CollectionEntryRegisteredEvent : IEvent
    {
        public CollectionCategory Category;
        public string EntryId;
        public int TotalCollected;
    }

    /// <summary>
    /// 도감 마일스톤 보상 수령 이벤트.
    /// </summary>
    public struct CollectionMilestoneClaimedEvent : IEvent
    {
        public CollectionCategory Category;
        public int MilestoneIndex;
        public int Threshold;
    }

    /// <summary>
    /// 도감 칭호 변경 이벤트.
    /// 수집 진행에 따라 칭호가 승급되면 발행된다.
    /// </summary>
    public struct CollectionTitleChangedEvent : IEvent
    {
        public string PreviousTitle;
        public string NewTitle;
        public int TotalCollected;
    }

    // ── 콤보 이벤트 ──

    /// <summary>
    /// 콤보 발생 이벤트. UI 콤보 로그 표시용.
    /// </summary>
    public struct ComboEvent : IEvent
    {
        public int ComboCount;
        public Vector3 Position;
    }

    // ── 마스터리 이벤트 ──

    /// <summary>
    /// 마스터리 노드 해금 이벤트.
    /// </summary>
    public struct MasteryUnlockedEvent : IEvent
    {
        public string MasteryId;
        public int BranchIndex;
        public int NodeLevel;
        public StatType BonusStat;
    }

    /// <summary>
    /// 마스터리 리스펙 이벤트.
    /// </summary>
    public struct MasteryRespecEvent : IEvent
    {
        public int RefundedPoints;
        public int ClimbTokenCost;
    }

    // DailyChecklistAllClearEvent: 2026-04-20 DailyChecklist 시스템 완전 제거

    /// <summary>
    /// CP 변화 이벤트. 전투력 변동 시 원인과 변화량을 포함한다.
    /// </summary>
    public struct CpChangedEvent : IEvent
    {
        public long PreviousCp;
        public long CurrentCp;
        public long Delta;
        public string Reason;
    }

    // CostumeObtainedEvent / CostumeSetCompletedEvent / CostumeEquippedEvent: 2026-04-20 Costume 시스템 완전 제거
    // ClimberPowerUpEvent: 2026-04-20 Climber 시스템 완전 제거

    // ── 타워/챌린지/아레나 이벤트 ──

    /// <summary>
    /// 타워 층 도달 이벤트.
    /// </summary>
    public struct TowerFloorReachedEvent : IEvent
    {
        public int Floor;
        public string ZoneName;
        public bool IsNewHighest;
    }

    /// <summary>
    /// 아레나 매치 이벤트.
    /// </summary>
    public struct ArenaMatchEvent : IEvent
    {
        public bool IsVictory;
        public int ArenaRank;
    }

    // ── 길드 이벤트 ──

    /// <summary>
    /// 길드 가입 이벤트.
    /// </summary>
    public struct GuildJoinedEvent : IEvent
    {
        public string GuildName;
    }

    /// <summary>
    /// 길드 기부 이벤트.
    /// </summary>
    public struct GuildDonatedEvent : IEvent
    {
        public string DonationType; // "gold", "ruby", "ticket"
        public int GuildExpGained;
    }

    /// <summary>
    /// 길드 보스 전투 완료 이벤트.
    /// </summary>
    public struct GuildBossCompletedEvent : IEvent
    {
        public long DamageDealt;
        public bool IsBossDefeated;
    }

    // ── 가이드 퀘스트 이벤트 ──

    /// <summary>
    /// 가이드 퀘스트 완료 이벤트.
    /// 체인 퀘스트 완료 시 보상 팝업 + 다음 퀘스트 전환에 사용된다.
    /// </summary>
    public struct GuideQuestCompletedEvent : IEvent
    {
        public string QuestId;
        public string NextQuestId;
        public int ChainIndex;
        public CurrencyType RewardType;
        public int RewardAmount;
        public CurrencyType BonusRewardType;
        public int BonusRewardAmount;
        public string DisplayName;
    }

    /// <summary>
    /// 가이드 퀘스트 활성화 이벤트.
    /// 새로운 가이드 퀘스트가 활성화되면 위젯이 갱신된다.
    /// </summary>
    public struct GuideQuestActivatedEvent : IEvent
    {
        public string QuestId;
        public int ChainIndex;
        public string DisplayName;
        public string Description;
        public int RequiredAmount;
        public QuestCondition Condition;
    }

    // ── 순환 퀘스트 이벤트 ──

    /// <summary>
    /// 순환 퀘스트 활성화 이벤트. 가이드 완료 후 무한 반복 퀘스트.
    /// </summary>
    public struct CyclingQuestActivatedEvent : IEvent
    {
        public string QuestId;
        public int CyclingOrder;
        public int Round;
        public string DisplayName;
        public string Description;
        public int RequiredAmount;
        public QuestCondition Condition;
    }

    /// <summary>
    /// 순환 퀘스트 완료 이벤트.
    /// </summary>
    public struct CyclingQuestCompletedEvent : IEvent
    {
        public string QuestId;
        public int CyclingOrder;
        public int Round;
        public CurrencyType RewardType;
        public int RewardAmount;
        public string DisplayName;
    }

    // ── 소탕(Quick Hunt) 이벤트 ──

    /// <summary>
    /// 소탕 완료 이벤트. 소탕권 사용 후 결과를 전달한다.
    /// </summary>
    public struct QuickHuntCompletedEvent : IEvent
    {
        public int TicketsUsed;
        public long GoldEarned;
        public long ExpEarned;
        public int EquipmentDrops;
    }

    // ReturneeGuideEvent / ReturneeBonus / ReturneeKeyGrantEvent: 2026-04-20 ReturneeGuide 시스템 완전 제거
    // ArtifactEquippedEvent / ArtifactObtainedEvent: 2026-04-20 Artifact 시스템 완전 제거

    // ── 잠재능력 이벤트 ──

    /// <summary>
    /// 잠재능력 변경 이벤트.
    /// </summary>
    public struct PotentialChangedEvent : IEvent
    {
        public EquipmentSlot Slot;
        public PotentialGrade NewGrade;
        /// <summary>"Upgraded", "Rerolled", "Failed"</summary>
        public string Result;
        public int TotalPotentialSets;
    }

    // AbilityUnlockedEvent / AbilityResetEvent: 2026-04-20 Ability 시스템 완전 제거

    // ── 부스터 이벤트 ──

    /// <summary>
    /// 부스터 사용 이벤트.
    /// 부스터 아이템을 사용하여 시간제 버프가 적용되면 발행된다.
    /// </summary>
    public struct BoosterUsedEvent : IEvent
    {
        public string BoosterId;
        public BoosterType Type;
        public float Multiplier;
        public float DurationSeconds;
    }

    /// <summary>
    /// 부스터 만료 이벤트.
    /// </summary>
    public struct BoosterExpiredEvent : IEvent
    {
        public string BoosterId;
        public BoosterType Type;
    }

    /// <summary>
    /// 부스터 보유 수량 추가 요청 이벤트.
    /// 출석/퀘스트 등 다른 어셈블리에서 BoosterManager에 수량 추가를 요청할 때 사용.
    /// </summary>
    public struct BoosterStockAddEvent : IEvent
    {
        public BoosterType Type;
        public int Amount;
    }

    // HeroPowerMilestoneEvent: 2026-04-20 HeroPower 시스템 완전 제거

    // ── 엘리트 소환 이벤트 ──

    /// <summary>
    /// 엘리트 몬스터 소환 이벤트. 소환 시 발행.
    /// </summary>
    public struct EliteSummonedEvent : IEvent
    {
        public int SummonLevel;
        public int CostPaid;
        public Vector3 SpawnPosition;
    }

    /// <summary>
    /// 엘리트 몬스터 처치 이벤트. 드롭 처리용.
    /// </summary>
    public struct EliteKilledEvent : IEvent
    {
        public int SummonLevel;
        public Vector3 Position;
        public string DroppedEquipmentId;
        public string DroppedGrade;
    }

    /// <summary>
    /// 엘리트 소환 레벨업 이벤트.
    /// </summary>
    public struct EliteSummonLevelUpEvent : IEvent
    {
        public int PreviousLevel;
        public int NewLevel;
        public string NewMaxGrade;
    }

    /// <summary>
    /// 보스 레이드 입장 이벤트. 도전 시 발행 (성공/실패 무관).
    /// </summary>
    public struct BossRaidEnteredEvent : IEvent
    {
        public bool IsSuccess;
    }

    /// <summary>
    /// 퀘스트 상태 리프레시 요청 이벤트.
    /// 새 퀘스트 활성화 시 발행 → 각 시스템이 자신의 현재 값을 재발행.
    /// SetProgress 기반 조건(LevelUp, ClearStage 등)이 퀘스트 활성화 시점에
    /// 이미 만족된 경우 진행도가 0에 머무는 버그 방지.
    /// </summary>
    public struct QuestStateRefreshEvent : IEvent
    {
        public QuestCondition Condition;
    }

    /// <summary>
    /// 로그인 완료 이벤트. LoginManager가 서버 /auth/login 성공 시 발행.
    /// OnboardingFlow가 구독하여 isNewPlayer 분기로 닉네임/직업 팝업 또는 Main 전환을 결정한다.
    /// </summary>
    public struct LoginCompletedEvent : IEvent
    {
        public string PlayerId;
        public string Nickname;
        public bool IsNewPlayer;
    }

    /// <summary>
    /// 온보딩 완료 이벤트. 닉네임 + 직업 선택 후 Main 씬 진입 직전 발행.
    /// </summary>
    public struct OnboardingCompletedEvent : IEvent
    {
        public string Nickname;
        public string JobId;
    }
}
