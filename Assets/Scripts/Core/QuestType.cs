namespace MkLike.Core
{
    /// <summary>
    /// 퀘스트 종류 열거형.
    /// </summary>
    public enum QuestType
    {
        Main,   // 메인 퀘스트 (순차 진행)
        Daily,  // 일일 퀘스트 (매일 리셋)
        Weekly, // 주간 퀘스트 (매주 리셋)
        Guide,   // 가이드 퀘스트 (영구 체인 진행)
        Cycling  // 순환 퀘스트 (가이드 완료 후 무한 반복)
    }

    /// <summary>
    /// 퀘스트 완료 조건 열거형.
    /// 제거된 값(13 CompanionDeploy, 18 ChallengeStars, 34 SkillMastery, 40 ClearEventDungeon)의
    /// 번호는 고정. 남은 값은 본래 번호 유지.
    /// </summary>
    public enum QuestCondition
    {
        KillMonsters = 0,      // 몬스터 N마리 처치
        ClearStage = 1,        // 스테이지 N 클리어
        LevelUp = 2,           // 레벨 N 달성
        SpendGold = 3,         // 골드 N 소비
        EnhanceEquipment = 4,  // 장비 강화 N회
        GachaPull = 5,         // 가챠 N회
        ClearDungeon = 6,      // 던전 N회 클리어
        AllocateStats = 7,     // 스탯 포인트 N 분배
        ClearChallenge = 8,    // 챌린지 N회 클리어
        SkillLevelUp = 9,      // 스킬 레벨업 N회
        ArenaMatch = 10,       // 아레나 N회 도전
        TowerFloorClear = 11,  // 탑 N층 이상 돌파
        CostumeEquip = 12,     // 코스튬 N회 장착
        // 13: CompanionDeploy [제거됨 — 펫 시스템 삭제]
        QuestComplete = 14,    // 퀘스트 N개 완료
        EquipItem = 15,        // 장비 장착 N회
        StarGradeEnhance = 16, // 성급 강화 N회
        CollectionRate = 17,   // 도감 N% 달성
        // 18: ChallengeStars [제거됨 — 챌린지 시스템 삭제]
        ClimberPower = 19,     // 등반자의 힘 N단계
        JobAdvance = 20,       // N차 전직 달성
        SkillUse = 21,         // 스킬 사용 N회
        ReceiveAttendance = 22, // 출석 보상 수령
        ReceiveOfflineReward = 23, // 오프라인 보상 수령
        ReceiveBlessing = 24,  // 축복 받기
        AchievementClear = 25, // 업적 N개 달성
        GuildJoin = 26,        // 길드 가입
        WeaponGacha = 27,      // 무기 가챠 N회
        ArenaTierReach = 28,   // 아레나 티어 N 달성
        CollectionCount = 29,  // 도감 N종 달성
        CostumeSetComplete = 30, // 코스튬 세트 완성 N회
        // 31: RelicEquip [제거됨 — 유물 시스템 완전 제거 2026-04-20]
        PotentialSet = 32,     // 잠재능력 세팅 N회
        QuickHunt = 33,        // 소탕 N회 실행
        // 34: SkillMastery [제거됨 — SkillMasteryManager 삭제]
        HeroPowerMilestone = 35, // 영웅의 힘 마일스톤 N개 달성
        UseBooster = 36,       // 부스터 N회 사용
        UnlockAbility = 37,    // 어빌리티 N개 해금
        StarForceReach = 38,   // 스타포스 N성 달성
        EquipArtifact = 39,    // 아티팩트 N개 장착
        // 40: ClearEventDungeon [제거됨 — 이벤트 던전 시스템 삭제]
        EliteSummon = 41,           // 엘리트 소환 N회
        BossRaidEntry = 42,         // 보스 레이드 N회 입장
        WeaponSummonLevelReach = 43, // 무기 소환 레벨 N 달성
        EliteSummonLevelReach = 44   // 엘리트 소환 레벨 N 달성
    }
}
