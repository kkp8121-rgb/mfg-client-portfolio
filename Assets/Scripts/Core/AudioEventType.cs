namespace MkLike.Core
{
    /// <summary>
    /// SFX 타입 열거형.
    /// Resources.Load 시 파일명 매핑에 사용된다.
    /// </summary>
    public enum SfxType
    {
        // ── 공격 ──
        SwordSwing,
        BowRelease,
        MagicCast,
        SwordHit,
        ArrowHit,
        MagicHit,
        CritHit,

        // ── 몬스터 ──
        MonsterHit,
        MonsterDie,
        BossRoar,
        BossDie,

        // ── 시스템 ──
        LevelUp,
        JobAdvance,
        EnhanceSuccess,
        EnhanceFail,
        EnhanceDestroy,
        StarforceUp,
        PotentialChange,
        GoldPickup,
        ExpPickup,
        ItemDrop,
        Equip,
        Unequip,

        // ── UI ──
        UiTap,
        UiBack,
        UiTabSwitch,
        UiPopupOpen,
        UiPopupClose,
        UiConfirm,
        UiCancel,
        UiError,
        UiReward,

        // ── 가차 ──
        GachaSpin,
        GachaStop,
        GachaRevealNormal,
        GachaRevealRare,
        GachaRevealEpic,
        GachaRevealUnique,
        GachaRevealLegendary,
        GachaRevealMythic,
        GachaMultiResult,

        // ── 스킬 (기본) ──
        SkillSlash,
        SkillCharge,
        SkillWarcry,
        SkillBowMulti,
        SkillBowStorm,
        SkillFire,
        SkillIce,
        SkillLightning,
        SkillArcane,
        SkillMeteor,
        SkillBuffActivate,
        SkillAwakening,

        // ── 스킬 (전사 계열) ──
        SkillWarriorStrike,
        SkillWarriorFury,
        SkillWarriorTraining,
        SkillKnightJudgment,
        SkillKnightRally,
        SkillKnightWeakness,
        SkillTitanAnnihilate,
        SkillTitanCataclysm,
        SkillTitanImmortal,
        SkillTitanMight,
        SkillWarlordWhirlwind,
        SkillWarlordEarthshatter,
        SkillWarlordFrenzy,
        SkillWarlordBloodpact,

        // ── 스킬 (궁수 계열) ──
        SkillArcherAimshot,
        SkillArcherArrowrain,
        SkillArcherKeeneye,
        SkillArcherRapidfire,
        SkillScoutPierceshot,
        SkillScoutStormshot,
        SkillScoutWindblessing,
        SkillScoutAgility,
        SkillHawkeyeEagleeye,
        SkillHawkeyeExtinction,
        SkillHawkeyeHuntingtime,
        SkillHawkeyeJudgment,
        SkillWindwalkerGale,
        SkillWindwalkerTyphon,
        SkillWindwalkerAfterimage,
        SkillWindwalkerWindpierce,

        // ── 스킬 (마법사 계열) ──
        SkillMageMagicbolt,
        SkillMageFireburst,
        SkillMageConcentration,
        SkillMageManacharge,
        SkillSorcererFireball,
        SkillSorcererChainlightning,
        SkillSorcererFroststorm,
        SkillSorcererElemental,
        SkillSageArcanemissile,
        SkillSageMeteorshower,
        SkillSageDimensionrift,
        SkillSageTimewarp,
        SkillRunemasterRuneblast,
        SkillRunemasterApocalypse,
        SkillRunemasterAbsolutedomain,
        SkillRunemasterRunemastery,

        // ── 스킬 (4차 전직) ──
        SkillDragonslayerDragonblade,
        SkillDragonslayerDragonrage,
        SkillDragonslayerApocalypse,
        SkillDragonslayerDragonblood,
        SkillDragonslayerDragonheart,
        SkillDragonslayerDragonscale,
        SkillDragonslayerDragonaura,
        SkillStormbringerTempest,
        SkillStormbringerChainlightning,
        SkillStormbringerThunderpierce,
        SkillStormbringerRagnarok,
        SkillStormbringerStormmastery,
        SkillStormbringerStormshield,
        SkillStormbringerWindwalking,
        SkillArchmageVoidbolt,
        SkillArchmageBigbang,
        SkillArchmageCosmicfield,
        SkillArchmageTranscendence,
        SkillArchmageElementalmastery,
        SkillArchmageManashield,
        SkillArchmageArcanemind,

        // ── 배틀패스/시즌 ──
        BattlePassLevelUp,
        BattlePassRewardClaim,
        SeasonStart,
        SeasonEnd,

        // ── 동료/펫 [제거됨] 직렬화 안전을 위해 유지 ──
        CompanionSummon,    // [제거됨]
        CompanionUltimate,  // [제거됨]
        PetAttack,          // [제거됨]
        PetUltimate,        // [제거됨]
        PetEvolve,          // [제거됨]

        // ── 던전/탑 ──
        DungeonEnter,
        DungeonClear,
        TowerFloorClear,
        SecretRoomDiscover,   // [제거됨] 직렬화 안전을 위해 유지
        SecretRoomEnter,      // [제거됨] 직렬화 안전을 위해 유지

        // ── 성장/컬렉션 ──
        PrestigeExecute,
        MasteryUnlock,
        CostumeObtain,        // [제거됨 2026-04-20 Costume 시스템] 직렬화 안전을 위해 유지
        CostumeSetComplete,   // [제거됨 2026-04-20 Costume 시스템] 직렬화 안전을 위해 유지
        CollectionRegister,
        CollectionMilestone,
        TitleUnlock,
        InscriptionChange,

        // ── 도전/챌린지 (제거됨 - 직렬화 안전을 위해 유지) ──
        ChallengeStart,
        ChallengeClear,
        ChallengeFail,
        ChallengeStarEarn,

        // ── 스테이지/보스 ──
        StageClear,
        BossWarning,

        // ── 기타 ──
        ComboHit,
        OfflineRewardClaim,
        DailyChecklistAllClear,
        GuideQuestComplete,
        AttendanceCheck,
        ShopPurchase,
        AdRewardClaim,
        Teleport,
        ChestOpen,
        DoorOpen,
    }

    /// <summary>
    /// BGM 타입 열거형.
    /// </summary>
    public enum BgmType
    {
        // ── 기본 ──
        Lobby,
        ChapterForest,
        ChapterCave,
        ChapterVolcano,
        ChapterSky,
        ChapterAbyss,

        // ── 보스 (8종 보스 BGM) ──
        Boss,
        BossAbyssalTyrant,
        BossSoulrendSovereign,
        BossVeilOfForsaken,
        BossRuinlordAscendant,
        BossUnseenMonarch,
        BossDreadRequiem,
        BossTwilightHarbinger,
        BossBloodboundFight,

        // ── 던전/탑 ──
        Dungeon,
        DungeonDark,
        TowerElite,
        TowerAbyss,
        TowerInfinite,

        // ── 전투 (JRPG 배틀) ──
        BattleFurious,
        BattleConflict,
        BattleGrief,
        BattleDawn,
        BattleRapier,
        BattleSilverMoon,
        BattleVampire,
        BattleSamurai,

        // ── 탐험/평화 ──
        Exploration,
        Journey,
        MagicForest,
        SafeSpace,

        // ── 이벤트/특수 ──
        Gacha,
        WorldBoss,
        Season,
        SecretRoom,   // [제거됨] 직렬화 안전을 위해 유지
        Challenge,    // [제거됨] 직렬화 안전을 위해 유지

        // ── 타운/이벤트 BGM ──
        Town2,
        Town3,
        Event1,
        Event2,
        Event3,
        Event4,
    }
}
