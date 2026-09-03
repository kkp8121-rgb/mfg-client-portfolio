#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using MkLike.Core;
using MkLike.Data;
using System.Collections.Generic;

namespace MkLike.Editor
{
    /// <summary>
    /// 가이드 퀘스트 350개 + 일일 퀘스트 11개 + 순환 퀘스트 14개 SO 자동 생성.
    /// 14종 순환 패턴 + 지수적 요구량 + 콘텐츠 해금 삽입.
    /// 기획: Docs/Planning/balance/guide-quest-design.md
    /// </summary>
    public static class GuideQuestGenerator
    {
        private const string QUEST_PATH = "Assets/Data/SO/Quest";
        private const int TOTAL_GUIDE = 350;
        private const int EARLY_END = 56;
        private const int PER_CYCLE = 14;

        // 생성 시 시스템 상한 캐시 (GenerateGuideQuests에서 resolve)
        private static int _weaponMaxLevel = int.MaxValue;
        private static int _eliteMaxLevel = int.MaxValue;

        // ============================================================
        // 14종 순환 슬롯 정의
        // ============================================================

        private struct SlotDef
        {
            public QuestCondition Condition;
            public string NameFmt;
            public string DescFmt;
            public CurrencyType Reward;
            public int BaseReward;
            public CurrencyType Bonus;
            public int BaseBonus;
            public int AvailAfter; // 이 퀘스트 번호 이후 사용 가능 (0=항상)
            public int Sub;        // 미해금 시 대체 슬롯 (-1=대체 없음)
        }

        private static readonly SlotDef[] SLOTS =
        {
            /* 0  ClearStage           */ new() { Condition = QuestCondition.ClearStage,             NameFmt = "스테이지 {0} 돌파",     DescFmt = "스테이지 {0}을 클리어하라",       Reward = CurrencyType.Ruby,         BaseReward = 150,  Bonus = CurrencyType.WeaponTicket,    BaseBonus = 2, AvailAfter = 0,   Sub = -1 },
            /* 1  KillMonsters         */ new() { Condition = QuestCondition.KillMonsters,           NameFmt = "몬스터 {0}마리 처치",   DescFmt = "몬스터 {0}마리를 처치하라",       Reward = CurrencyType.Gold,         BaseReward = 5000, Bonus = CurrencyType.Gold,            BaseBonus = 0, AvailAfter = 0,   Sub = -1 },
            /* 2  EliteSummon          */ new() { Condition = QuestCondition.EliteSummon,            NameFmt = "엘리트 소환 {0}회",     DescFmt = "엘리트 소환을 {0}회 실행하라",    Reward = CurrencyType.Ruby,         BaseReward = 80,   Bonus = CurrencyType.Gold,            BaseBonus = 0, AvailAfter = 96,  Sub = 4  },
            /* 3  WeaponGacha          */ new() { Condition = QuestCondition.WeaponGacha,            NameFmt = "무기 소환 {0}회",       DescFmt = "무기를 {0}회 소환하라",           Reward = CurrencyType.Ruby,         BaseReward = 80,   Bonus = CurrencyType.Gold,            BaseBonus = 0, AvailAfter = 0,   Sub = -1 },
            /* 4  GachaPull            */ new() { Condition = QuestCondition.GachaPull,              NameFmt = "소환 {0}회",           DescFmt = "소환을 {0}회 실행하라",           Reward = CurrencyType.Ruby,         BaseReward = 80,   Bonus = CurrencyType.Gold,            BaseBonus = 0, AvailAfter = 0,   Sub = -1 },
            /* 5  BossRaidEntry        */ new() { Condition = QuestCondition.BossRaidEntry,          NameFmt = "보스 레이드 {0}회",     DescFmt = "보스 레이드에 {0}회 입장하라",    Reward = CurrencyType.Ruby,         BaseReward = 120,  Bonus = CurrencyType.Gold,            BaseBonus = 3000, AvailAfter = 58, Sub = 0 },
            /* 6  QuickHunt            */ new() { Condition = QuestCondition.QuickHunt,              NameFmt = "소탕 {0}회",           DescFmt = "소탕을 {0}회 실행하라",           Reward = CurrencyType.Gold,         BaseReward = 8000, Bonus = CurrencyType.QuickHuntTicket,  BaseBonus = 2, AvailAfter = 75,  Sub = 1  },
            /* 7  ClearDungeon(무기)   */ new() { Condition = QuestCondition.ClearDungeon,           NameFmt = "무기 던전 {0}회",       DescFmt = "무기 던전을 {0}회 클리어하라",    Reward = CurrencyType.RuneFragment, BaseReward = 10,   Bonus = CurrencyType.Gold,            BaseBonus = 0, AvailAfter = 0,   Sub = -1 },
            /* 8  ClearDungeon(강화)   */ new() { Condition = QuestCondition.ClearDungeon,           NameFmt = "강화 던전 {0}회",       DescFmt = "강화 던전을 {0}회 클리어하라",    Reward = CurrencyType.RuneFragment, BaseReward = 10,   Bonus = CurrencyType.Gold,            BaseBonus = 0, AvailAfter = 0,   Sub = -1 },
            /* 9  ClearDungeon(보조1) */ new() { Condition = QuestCondition.ClearDungeon,           NameFmt = "추가 던전 {0}회",       DescFmt = "던전을 {0}회 클리어하라",         Reward = CurrencyType.Ruby,         BaseReward = 60,   Bonus = CurrencyType.Gold,            BaseBonus = 0, AvailAfter = 0,   Sub = -1 },
            /* 10 EliteSummon(보조2)  */ new() { Condition = QuestCondition.EliteSummon,            NameFmt = "정예 소환 {0}회",       DescFmt = "엘리트 소환을 {0}회 실행하라",    Reward = CurrencyType.Ruby,         BaseReward = 60,   Bonus = CurrencyType.Gold,            BaseBonus = 0, AvailAfter = 96,  Sub = 7  },
            /* 11 ArenaMatch           */ new() { Condition = QuestCondition.ArenaMatch,             NameFmt = "아레나 {0}회",         DescFmt = "아레나에 {0}회 도전하라",         Reward = CurrencyType.Ruby,         BaseReward = 100,  Bonus = CurrencyType.Gold,            BaseBonus = 0, AvailAfter = 247, Sub = 8  },
            /* 12 WeaponSummonLevel    */ new() { Condition = QuestCondition.WeaponSummonLevelReach, NameFmt = "무기 소환 Lv.{0}",     DescFmt = "무기 소환 레벨 {0}을 달성하라",   Reward = CurrencyType.Ruby,         BaseReward = 100,  Bonus = CurrencyType.WeaponTicket,    BaseBonus = 3, AvailAfter = 0,   Sub = -1 },
            /* 13 EliteSummonLevel     */ new() { Condition = QuestCondition.EliteSummonLevelReach,  NameFmt = "엘리트 소환 Lv.{0}",   DescFmt = "엘리트 소환 레벨 {0}을 달성하라", Reward = CurrencyType.Ruby,         BaseReward = 100,  Bonus = CurrencyType.WeaponTicket,    BaseBonus = 3, AvailAfter = 96,  Sub = 12 },
        };

        // ============================================================
        // 콘텐츠 해금 오버라이드 (순환 패턴 중 특정 퀘스트를 해금 퀘스트로 대체)
        // ============================================================

        private struct Unlock
        {
            public QuestCondition Cond;
            public int Amt;
            public string Name;
            public string Desc;
            public CurrencyType Rwd;
            public int RwdAmt;
            public CurrencyType Bns;
            public int BnsAmt;
        }

        private static readonly Dictionary<int, Unlock> UNLOCK_MAP = new()
        {
            [58]  = new() { Cond = QuestCondition.BossRaidEntry,      Amt = 1,  Name = "보스 레이드 입장", Desc = "보스 레이드에 1회 입장하라",        Rwd = CurrencyType.Ruby, RwdAmt = 300,  Bns = CurrencyType.Gold,            BnsAmt = 10000 },
            [67]  = new() { Cond = QuestCondition.StarGradeEnhance,   Amt = 1,  Name = "성급 강화 첫 성공", Desc = "성급 강화를 1회 성공하라",         Rwd = CurrencyType.Ruby, RwdAmt = 200,  Bns = CurrencyType.StarCrystal,     BnsAmt = 10 },
            [75]  = new() { Cond = QuestCondition.QuickHunt,          Amt = 1,  Name = "첫 소탕",         Desc = "소탕을 1회 실행하라",              Rwd = CurrencyType.Ruby, RwdAmt = 300,  Bns = CurrencyType.QuickHuntTicket, BnsAmt = 5 },
            [79]  = new() { Cond = QuestCondition.HeroPowerMilestone, Amt = 1,  Name = "영웅의 각성",     Desc = "영웅의 힘 마일스톤 1개를 달성하라", Rwd = CurrencyType.Ruby, RwdAmt = 400,  Bns = CurrencyType.Gold,            BnsAmt = 20000 },
            [89]  = new() { Cond = QuestCondition.UseBooster,         Amt = 1,  Name = "부스터 첫 사용",   Desc = "부스터를 1회 사용하라",            Rwd = CurrencyType.Ruby, RwdAmt = 300,  Bns = CurrencyType.Gold,            BnsAmt = 15000 },
            [96]  = new() { Cond = QuestCondition.EliteSummon,        Amt = 1,  Name = "엘리트 소환 해금", Desc = "엘리트 소환을 1회 실행하라",        Rwd = CurrencyType.Ruby, RwdAmt = 400,  Bns = CurrencyType.Gold,            BnsAmt = 20000 },
            [113] = new() { Cond = QuestCondition.UnlockAbility,      Amt = 1,  Name = "어빌리티 해금",   Desc = "어빌리티를 1개 해금하라",          Rwd = CurrencyType.Ruby, RwdAmt = 500,  Bns = CurrencyType.Gold,            BnsAmt = 30000 },
            [131] = new() { Cond = QuestCondition.StarForceReach,     Amt = 5,  Name = "스타포스 5성",    Desc = "스타포스 5성을 달성하라",           Rwd = CurrencyType.Ruby, RwdAmt = 600,  Bns = CurrencyType.StarCrystal,     BnsAmt = 10 },
            [140] = new() { Cond = QuestCondition.GuildJoin,          Amt = 1,  Name = "길드 가입",       Desc = "길드에 가입하라",                 Rwd = CurrencyType.Ruby, RwdAmt = 500,  Bns = CurrencyType.Gold,            BnsAmt = 50000 },
            [239] = new() { Cond = QuestCondition.StarForceReach,     Amt = 20, Name = "스타포스 20성",   Desc = "스타포스 20성을 달성하라",          Rwd = CurrencyType.Ruby, RwdAmt = 1500, Bns = CurrencyType.StarCrystal,     BnsAmt = 30 },
            [247] = new() { Cond = QuestCondition.ArenaMatch,         Amt = 1,  Name = "아레나 해금",     Desc = "아레나에 1회 도전하라",            Rwd = CurrencyType.Ruby, RwdAmt = 800,  Bns = CurrencyType.Gold,            BnsAmt = 50000 },
            [347] = new() { Cond = QuestCondition.EquipArtifact,      Amt = 1,  Name = "아티팩트 장착",   Desc = "아티팩트를 1개 장착하라",          Rwd = CurrencyType.Ruby, RwdAmt = 2000, Bns = CurrencyType.Gold,            BnsAmt = 100000 },
        };

        // ============================================================
        // Public API
        // ============================================================

        /// <summary>전체 퀘스트 SO 재생성 (가이드 350 + 일일 11 + 순환 14)</summary>
        public static void GenerateAll()
        {
            EnsureFolder(QUEST_PATH);
            CleanupLegacyAssets();
            GenerateGuideQuests();
            GenerateDailyQuests();
            GenerateCyclingQuests();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[GuideQuestGenerator] 가이드 {TOTAL_GUIDE} + 일일 11 + 순환 {PER_CYCLE} = {TOTAL_GUIDE + 11 + PER_CYCLE}개 SO 생성 완료");

            // 생성 직후 자동 검증 — 시스템 상한 초과 req 경고 출력
            ValidateAll();
        }

        /// <summary>가이드 퀘스트 350개 생성</summary>
        public static void GenerateGuideQuests()
        {
            EnsureFolder(QUEST_PATH);

            // 시스템 상한 캐시 — 레벨 기반 퀘스트 req를 이 값으로 clamp하여 돌파 불가 방지
            _weaponMaxLevel = ResolveWeaponPoolMaxSummonLevel();
            _eliteMaxLevel = ResolveEliteSummonMaxLevel();
            Debug.Log($"[GuideQuestGenerator] 생성 컨텍스트: WeaponMax={_weaponMaxLevel}, EliteMax={_eliteMaxLevel}");

            GenerateEarlyQuests();
            GenerateAlgorithmicQuests();
            Debug.Log($"[GuideQuestGenerator] 가이드 퀘스트 {TOTAL_GUIDE}개 생성 완료");
        }

        // ============================================================
        // Early Quests (Q1-56): 튜토리얼 + 시스템 해금
        // ============================================================

        private static void GenerateEarlyQuests()
        {
            // ── Cycle 1 (Q1-14): 입문 ──
            CG(1,  "첫 전투",          "몬스터를 1마리 처치하라",       QuestCondition.KillMonsters,    1,    CurrencyType.Gold, 300);
            CG(2,  "스테이지 3 돌파",   "스테이지 3을 클리어하라",      QuestCondition.ClearStage,      3,    CurrencyType.Ruby, 50,    CurrencyType.Gold, 500);
            CG(3,  "사냥 연습",         "몬스터 10마리를 처치하라",     QuestCondition.KillMonsters,    10,   CurrencyType.Gold, 800);
            CG(4,  "첫 소환",          "가챠를 1회 실행하라",          QuestCondition.GachaPull,       1,    CurrencyType.Ruby, 100);
            CG(5,  "장비 장착",         "장비를 1개 장착하라",          QuestCondition.EquipItem,       1,    CurrencyType.Gold, 500,   CurrencyType.RuneFragment, 5);
            CG(6,  "스테이지 5 돌파",   "스테이지 5를 클리어하라",      QuestCondition.ClearStage,      5,    CurrencyType.Ruby, 100,   CurrencyType.WeaponTicket, 1);
            CG(7,  "몬스터 30마리",     "몬스터 30마리를 처치하라",     QuestCondition.KillMonsters,    30,   CurrencyType.Gold, 1000);
            CG(8,  "스킬 발동",         "스킬을 1회 사용하라",         QuestCondition.SkillUse,        1,    CurrencyType.Ruby, 100);
            CG(9,  "스테이지 8 돌파",   "스테이지 8을 클리어하라",      QuestCondition.ClearStage,      8,    CurrencyType.Ruby, 100);
            CG(10, "몬스터 50마리",     "몬스터 50마리를 처치하라",     QuestCondition.KillMonsters,    50,   CurrencyType.Gold, 1500);
            CG(11, "소환 3회",          "가챠를 3회 실행하라",          QuestCondition.GachaPull,       3,    CurrencyType.Ruby, 80);
            CG(12, "챕터 1 클리어",     "스테이지 10을 클리어하라",     QuestCondition.ClearStage,      10,   CurrencyType.Ruby, 150,   CurrencyType.WeaponTicket, 1);
            CG(13, "무기 소환",         "무기를 1회 소환하라",          QuestCondition.WeaponGacha,     1,    CurrencyType.Ruby, 100,   CurrencyType.WeaponTicket, 2);
            CG(14, "몬스터 100마리",    "몬스터 100마리를 처치하라",    QuestCondition.KillMonsters,    100,  CurrencyType.Gold, 2000);

            // ── Cycle 2 (Q15-28): 초반 확장 ──
            CG(15, "스테이지 15 돌파",  "스테이지 15를 클리어하라",     QuestCondition.ClearStage,      15,   CurrencyType.Ruby, 150);
            CG(16, "몬스터 200마리",    "몬스터 200마리를 처치하라",    QuestCondition.KillMonsters,    200,  CurrencyType.Gold, 3000);
            CG(17, "소환 5회",          "가챠를 5회 실행하라",          QuestCondition.GachaPull,       5,    CurrencyType.Ruby, 100);
            CG(18, "무기 소환 3회",     "무기를 3회 소환하라",          QuestCondition.WeaponGacha,     3,    CurrencyType.Ruby, 100);
            CG(19, "Lv.15 달성",       "레벨 15를 달성하라",           QuestCondition.LevelUp,         15,   CurrencyType.Ruby, 150);
            CG(20, "장비 강화 3회",     "장비를 3회 강화하라",          QuestCondition.EnhanceEquipment, 3,   CurrencyType.Gold, 2000,  CurrencyType.RuneFragment, 10);
            CG(21, "스테이지 20 돌파",  "스테이지 20을 클리어하라",     QuestCondition.ClearStage,      20,   CurrencyType.Ruby, 200,   CurrencyType.WeaponTicket, 2);
            CG(22, "몬스터 300마리",    "몬스터 300마리를 처치하라",    QuestCondition.KillMonsters,    300,  CurrencyType.Gold, 4000);
            CG(23, "장비 강화 5회",     "장비를 5회 강화하라",          QuestCondition.EnhanceEquipment, 5,   CurrencyType.Gold, 2000);
            CG(24, "소환 10회",         "가챠를 10회 실행하라",         QuestCondition.GachaPull,       10,   CurrencyType.Ruby, 150);
            CG(25, "스킬 사용 10회",    "스킬을 10회 사용하라",        QuestCondition.SkillUse,        10,   CurrencyType.Ruby, 200);
            CG(26, "무기 소환 5회",     "무기를 5회 소환하라",          QuestCondition.WeaponGacha,     5,    CurrencyType.Ruby, 150,   CurrencyType.WeaponTicket, 2);
            CG(27, "스탯 배분 10",      "스탯 포인트 10을 배분하라",    QuestCondition.AllocateStats,   10,   CurrencyType.Gold, 3000);
            CG(28, "스테이지 25 돌파",  "스테이지 25를 클리어하라",     QuestCondition.ClearStage,      25,   CurrencyType.Ruby, 250,   CurrencyType.WeaponTicket, 3);

            // ── Cycle 3 (Q29-42): 중반 진입 ──
            CG(29, "몬스터 500마리",    "몬스터 500마리를 처치하라",    QuestCondition.KillMonsters,    500,  CurrencyType.Gold, 5000);
            CG(30, "1차 전직",          "1차 전직을 달성하라",          QuestCondition.JobAdvance,      1,    CurrencyType.Ruby, 500,   CurrencyType.WeaponTicket, 5);
            CG(31, "소환 15회",         "가챠를 15회 실행하라",         QuestCondition.GachaPull,       15,   CurrencyType.Ruby, 200);
            CG(32, "무기 소환 10회",    "무기를 10회 소환하라",         QuestCondition.WeaponGacha,     10,   CurrencyType.Ruby, 200);
            CG(33, "스킬 레벨업 3",     "스킬을 3회 레벨업하라",         QuestCondition.SkillLevelUp,    3,    CurrencyType.Ruby, 300);
            CG(34, "던전 입장",         "던전을 1회 클리어하라",        QuestCondition.ClearDungeon,    1,    CurrencyType.Ruby, 200,   CurrencyType.RuneFragment, 15);
            CG(35, "스테이지 30 돌파",  "스테이지 30을 클리어하라",     QuestCondition.ClearStage,      30,   CurrencyType.Ruby, 300,   CurrencyType.WeaponTicket, 3);
            CG(36, "몬스터 1000마리",   "몬스터 1000마리를 처치하라",   QuestCondition.KillMonsters,    1000, CurrencyType.Gold, 8000);
            CG(37, "장비 강화 10회",    "장비를 10회 강화하라",         QuestCondition.EnhanceEquipment, 10,  CurrencyType.Gold, 5000,  CurrencyType.RuneFragment, 20);
            CG(38, "던전 3회",          "던전을 3회 클리어하라",        QuestCondition.ClearDungeon,    3,    CurrencyType.RuneFragment, 20);
            CG(39, "스킬 사용 30회",    "스킬을 30회 사용하라",        QuestCondition.SkillUse,        30,   CurrencyType.Gold, 5000);
            CG(40, "무기 소환 15회",    "무기를 15회 소환하라",         QuestCondition.WeaponGacha,     15,   CurrencyType.Ruby, 200);
            CG(41, "소환 20회",         "가챠를 20회 실행하라",         QuestCondition.GachaPull,       20,   CurrencyType.Ruby, 200);
            CG(42, "스테이지 40 돌파",  "스테이지 40을 클리어하라",     QuestCondition.ClearStage,      40,   CurrencyType.Ruby, 400,   CurrencyType.WeaponTicket, 5);

            // ── Cycle 4 (Q43-56): 중반 확립 ──
            CG(43, "몬스터 2000마리",   "몬스터 2000마리를 처치하라",   QuestCondition.KillMonsters,    2000, CurrencyType.Gold, 10000);
            CG(44, "던전 5회",          "던전을 5회 클리어하라",        QuestCondition.ClearDungeon,    5,    CurrencyType.RuneFragment, 25);
            CG(45, "던전 7회",          "던전을 7회 클리어하라",        QuestCondition.ClearDungeon,    7,    CurrencyType.Ruby, 300);
            CG(46, "장비 강화 20회",    "장비를 20회 강화하라",         QuestCondition.EnhanceEquipment, 20,  CurrencyType.Gold, 8000,  CurrencyType.RuneFragment, 30);
            CG(47, "소환 30회",         "가챠를 30회 실행하라",         QuestCondition.GachaPull,       30,   CurrencyType.Ruby, 300);
            CG(48, "무기 소환 20회",    "무기를 20회 소환하라",         QuestCondition.WeaponGacha,     20,   CurrencyType.Ruby, 200);
            CG(49, "스킬 레벨업 6",     "스킬을 6회 레벨업하라",         QuestCondition.SkillLevelUp,    6,    CurrencyType.Ruby, 300);
            CG(50, "스테이지 50 돌파",  "스테이지 50을 클리어하라",     QuestCondition.ClearStage,      50,   CurrencyType.Ruby, 500,   CurrencyType.WeaponTicket, 5);
            CG(51, "몬스터 3000마리",   "몬스터 3000마리를 처치하라",   QuestCondition.KillMonsters,    3000, CurrencyType.Gold, 15000);
            CG(52, "던전 10회",         "던전을 10회 클리어하라",       QuestCondition.ClearDungeon,    10,   CurrencyType.RuneFragment, 30);
            CG(53, "던전 15회",         "던전을 15회 클리어하라",       QuestCondition.ClearDungeon,    15,   CurrencyType.Ruby, 200);
            CG(54, "Lv.50 달성",       "레벨 50을 달성하라",           QuestCondition.LevelUp,         50,   CurrencyType.Ruby, 400);
            CG(55, "스탯 배분 15",      "스탯 포인트 15을 배분하라",    QuestCondition.AllocateStats,   15,   CurrencyType.Gold, 10000);
            CG(56, "스테이지 60 돌파",  "스테이지 60을 클리어하라",     QuestCondition.ClearStage,      60,   CurrencyType.Ruby, 500,   CurrencyType.WeaponTicket, 5);
        }

        // ============================================================
        // Algorithmic Quests (Q57-350): 14종 순환 + 해금 삽입
        // ============================================================

        private static void GenerateAlgorithmicQuests()
        {
            for (int q = EARLY_END + 1; q <= TOTAL_GUIDE; q++)
            {
                // 콘텐츠 해금 오버라이드
                if (UNLOCK_MAP.TryGetValue(q, out var u))
                {
                    CG(q, u.Name, u.Desc, u.Cond, u.Amt, u.Rwd, u.RwdAmt, u.Bns, u.BnsAmt);
                    continue;
                }

                int cycle = (q - 1) / PER_CYCLE + 1;
                int slot = (q - 1) % PER_CYCLE;
                int eff = ResolveSlot(slot, q);
                var s = SLOTS[eff];

                int req = GetRequirement(eff, cycle);
                int rwd = ScaleReward(s.BaseReward, cycle);
                int bns = ScaleReward(s.BaseBonus, cycle);

                string name = string.Format(s.NameFmt, req);
                string desc = string.Format(s.DescFmt, req);

                CG(q, name, desc, s.Condition, req, s.Reward, rwd, s.Bonus, bns);
            }
        }

        // ============================================================
        // Cycling Quests (14종, 가이드 완료 후 무한 반복)
        // ============================================================

        /// <summary>순환 퀘스트 14개 생성 (가이드 퀘스트 350 완료 후 활성화)</summary>
        public static void GenerateCyclingQuests()
        {
            EnsureFolder(QUEST_PATH);

            CreateCycling(0,  "cycling_00", "스테이지 돌파",       "다음 스테이지를 클리어하라",       QuestCondition.ClearStage,             1,   CurrencyType.Ruby, 100);
            CreateCycling(1,  "cycling_01", "몬스터 처치",         "몬스터 200마리를 처치하라",        QuestCondition.KillMonsters,           200, CurrencyType.Gold, 5000);
            CreateCycling(2,  "cycling_02", "엘리트 소환",         "엘리트 소환을 1회 실행하라",       QuestCondition.EliteSummon,            1,   CurrencyType.Ruby, 60);
            CreateCycling(3,  "cycling_03", "무기 소환",           "무기를 1회 소환하라",              QuestCondition.WeaponGacha,            1,   CurrencyType.Ruby, 60);
            CreateCycling(4,  "cycling_04", "소환 실행",           "소환을 1회 실행하라",              QuestCondition.GachaPull,              1,   CurrencyType.Ruby, 60);
            CreateCycling(5,  "cycling_05", "보스 레이드",         "보스 레이드에 1회 입장하라",       QuestCondition.BossRaidEntry,          1,   CurrencyType.Ruby, 80);
            CreateCycling(6,  "cycling_06", "소탕",               "소탕을 1회 실행하라",              QuestCondition.QuickHunt,              1,   CurrencyType.Gold, 5000);
            CreateCycling(7,  "cycling_07", "무기 던전",           "무기 던전을 1회 클리어하라",       QuestCondition.ClearDungeon,           1,   CurrencyType.RuneFragment, 10);
            CreateCycling(8,  "cycling_08", "강화 던전",           "강화 던전을 1회 클리어하라",       QuestCondition.ClearDungeon,           1,   CurrencyType.RuneFragment, 10);
            CreateCycling(9,  "cycling_09", "스킬 레벨업",         "스킬을 1회 레벨업하라",             QuestCondition.SkillLevelUp,           1,   CurrencyType.Ruby, 50);
            CreateCycling(10, "cycling_10", "장비 강화",           "장비를 1회 강화하라",               QuestCondition.EnhanceEquipment,       1,   CurrencyType.Ruby, 50);
            CreateCycling(11, "cycling_11", "아레나",             "아레나에 1회 도전하라",            QuestCondition.ArenaMatch,             1,   CurrencyType.Ruby, 80);
            CreateCycling(12, "cycling_12", "무기 소환 레벨",      "무기 소환 레벨 1을 달성하라",      QuestCondition.WeaponSummonLevelReach, 1,   CurrencyType.Ruby, 80);
            CreateCycling(13, "cycling_13", "엘리트 소환 레벨",    "엘리트 소환 레벨 1을 달성하라",    QuestCondition.EliteSummonLevelReach,  1,   CurrencyType.Ruby, 80);

            Debug.Log($"[GuideQuestGenerator] 순환 퀘스트 {PER_CYCLE}개 생성 완료");
        }

        // ============================================================
        // Daily Quests (11개, 변경 없음)
        // ============================================================

        /// <summary>일일 퀘스트 11개 생성 (3분 이내 전체 완료)</summary>
        public static void GenerateDailyQuests()
        {
            EnsureFolder(QUEST_PATH);

            CreateDaily("daily_01", "출석 보상 수령",     "오늘의 출석 보상을 수령하라",   QuestCondition.ReceiveAttendance,    1,   CurrencyType.Gold, 500);
            CreateDaily("daily_02", "오프라인 보상 수령", "오프라인 보상을 수령하라",      QuestCondition.ReceiveOfflineReward, 1,   CurrencyType.Gold, 500);
            CreateDaily("daily_03", "축복 받기",         "오늘의 축복을 받으세요",        QuestCondition.ReceiveBlessing,      1,   CurrencyType.Gold, 500);
            CreateDaily("daily_04", "던전 3회 클리어",   "던전을 3회 클리어하라",         QuestCondition.ClearDungeon,         3,   CurrencyType.RuneFragment, 2);
            CreateDaily("daily_05", "골드 5000 소비",    "골드를 5000 소비하라",          QuestCondition.SpendGold,            5000, CurrencyType.Gold, 1000);
            CreateDaily("daily_06", "몬스터 500마리 처치", "몬스터 500마리를 처치하라",    QuestCondition.KillMonsters,         500, CurrencyType.Gold, 2000);
            CreateDaily("daily_07", "가챠 1회 실행",     "가챠를 1회 실행하라",           QuestCondition.GachaPull,            1,   CurrencyType.WeaponTicket, 1);
            CreateDaily("daily_08", "장비 강화 1회",     "장비를 1회 강화하라",           QuestCondition.EnhanceEquipment,     1,   CurrencyType.RuneFragment, 3);
            CreateDaily("daily_09", "스테이지 5 클리어", "스테이지 5개를 클리어하라",     QuestCondition.ClearStage,           5,   CurrencyType.Ruby, 30);
            CreateDaily("daily_10", "퀘스트 3개 완료",   "퀘스트를 3개 완료하라",         QuestCondition.QuestComplete,        3,   CurrencyType.Gold, 3000);
            CreateDaily("daily_11", "스킬 사용 100회",   "스킬을 100회 사용하라",         QuestCondition.SkillUse,             100, CurrencyType.Gold, 2000);

            Debug.Log("[GuideQuestGenerator] 일일 퀘스트 11개 생성 완료");
        }

        // ============================================================
        // Requirement Scaling
        // ============================================================

        private static int GetRequirement(int slot, int cycle)
        {
            float x = Mathf.Max(0, cycle - 5);
            return slot switch
            {
                0  => (int)(70 + x * 20 + x * x * 2.5f),          // ClearStage: 70→1470
                1  => (int)(100 + x * 60 + x * x * 10),           // KillMonsters: 100→4500 (완화됨)
                2  => Mathf.Max(1, 1 + (int)(x / 3)),             // EliteSummon
                3  => Mathf.Max(1, 1 + (int)x),                   // WeaponGacha
                4  => Mathf.Max(1, 1 + (int)x),                   // GachaPull
                5  => Mathf.Max(1, 1 + (int)(x / 3)),             // BossRaidEntry
                6  => Mathf.Max(1, 1 + (int)(x / 3)),             // QuickHunt
                7  => Mathf.Max(1, 1 + (int)x),                   // ClearDungeon(무기)
                8  => Mathf.Max(1, 1 + (int)x),                   // ClearDungeon(강화)
                9  => Mathf.Max(1, 1 + (int)(x / 2)),             // EventDungeon(경험치)
                10 => Mathf.Max(1, 1 + (int)(x / 2)),             // EventDungeon(장비)
                11 => Mathf.Max(1, 1 + (int)(Mathf.Max(0, cycle - 18) / 3)), // Arena
                12 => Mathf.Clamp(3 + (int)(x * 2), 1, _weaponMaxLevel),                          // WeaponSummonLevel — WeaponPool.MaxSummonLevel로 clamp
                13 => Mathf.Clamp(3 + (int)(Mathf.Max(0, cycle - 7) * 2), 1, _eliteMaxLevel),      // EliteSummonLevel — EliteSummonSO.MaxLevel로 clamp
                _  => 1
            };
        }

        private static int ScaleReward(int baseAmount, int cycle)
        {
            if (baseAmount <= 0) return 0;
            return Mathf.Max(1, (int)(baseAmount * Mathf.Pow(1.15f, cycle - 1)));
        }

        private static int ResolveSlot(int slot, int questNumber)
        {
            if (slot < 0 || slot >= SLOTS.Length) return 0;
            var s = SLOTS[slot];
            if (s.AvailAfter > 0 && questNumber <= s.AvailAfter && s.Sub >= 0)
                return s.Sub;
            return slot;
        }

        // ============================================================
        // Asset Creation Helpers
        // ============================================================

        /// <summary>CG = CreateGuide 약어. Early/Algorithmic 공용.</summary>
        private static void CG(int chainIndex, string displayName, string description,
            QuestCondition condition, int requiredAmount,
            CurrencyType rewardType, int rewardAmount,
            CurrencyType bonusType = CurrencyType.Gold, int bonusAmount = 0)
        {
            string id = $"guide_{chainIndex:D3}";
            string nextId = chainIndex < TOTAL_GUIDE ? $"guide_{(chainIndex + 1):D3}" : "";
            string fileName = $"Quest_Guide_{chainIndex:D3}";
            string path = $"{QUEST_PATH}/{fileName}.asset";

            var quest = AssetDatabase.LoadAssetAtPath<QuestDataSO>(path);
            if (quest == null)
            {
                quest = ScriptableObject.CreateInstance<QuestDataSO>();
                quest.name = fileName;
                AssetDatabase.CreateAsset(quest, path);
            }

            quest.id = id;
            quest.displayName = displayName;
            quest.description = description;
            quest.questType = QuestType.Guide;
            quest.condition = condition;
            quest.requiredAmount = requiredAmount;
            quest.rewardType = rewardType;
            quest.rewardAmount = rewardAmount;
            quest.bonusRewardType = bonusType;
            quest.bonusRewardAmount = bonusAmount;
            quest.nextGuideQuestId = nextId;
            quest.guideChainIndex = chainIndex;
            quest.cyclingOrder = -1;

            EditorUtility.SetDirty(quest);
        }

        private static void CreateCycling(int order, string id, string displayName, string description,
            QuestCondition condition, int requiredAmount,
            CurrencyType rewardType, int rewardAmount)
        {
            string fileName = $"Quest_Cycling_{order:D2}";
            string path = $"{QUEST_PATH}/{fileName}.asset";

            var quest = AssetDatabase.LoadAssetAtPath<QuestDataSO>(path);
            if (quest == null)
            {
                quest = ScriptableObject.CreateInstance<QuestDataSO>();
                quest.name = fileName;
                AssetDatabase.CreateAsset(quest, path);
            }

            quest.id = id;
            quest.displayName = displayName;
            quest.description = description;
            quest.questType = QuestType.Cycling;
            quest.condition = condition;
            quest.requiredAmount = requiredAmount;
            quest.rewardType = rewardType;
            quest.rewardAmount = rewardAmount;
            quest.cyclingOrder = order;
            quest.nextGuideQuestId = "";
            quest.guideChainIndex = -1;

            EditorUtility.SetDirty(quest);
        }

        private static void CreateDaily(string id, string displayName, string description,
            QuestCondition condition, int requiredAmount,
            CurrencyType rewardType, int rewardAmount)
        {
            string fileName = $"Quest_Daily_{id.Replace("daily_", "")}";
            string path = $"{QUEST_PATH}/{fileName}.asset";

            var quest = AssetDatabase.LoadAssetAtPath<QuestDataSO>(path);
            if (quest == null)
            {
                quest = ScriptableObject.CreateInstance<QuestDataSO>();
                quest.name = fileName;
                AssetDatabase.CreateAsset(quest, path);
            }

            quest.id = id;
            quest.displayName = displayName;
            quest.description = description;
            quest.questType = QuestType.Daily;
            quest.condition = condition;
            quest.requiredAmount = requiredAmount;
            quest.rewardType = rewardType;
            quest.rewardAmount = rewardAmount;
            quest.nextGuideQuestId = "";
            quest.guideChainIndex = -1;

            EditorUtility.SetDirty(quest);
        }

        // ============================================================
        // Validation (시스템 상한 초과 req 검증)
        // ============================================================

        // 휴리스틱 상한 — SO 기반 상한이 없는 조건의 임시 컷오프
        private const int MAX_STAR_FORCE      = 30;     // 스타포스 일반 상한 (추후 SO 상한으로 대체)
        private const int MAX_HERO_POWER      = 10;     // 영웅의 힘 마일스톤 개수 상한 추정
        private const int MAX_JOB_ADVANCE     = 3;      // 1차/2차/3차 전직
        private const int MAX_CLEAR_STAGE     = 9999;   // 임의 대형 컷오프
        private const int MAX_KILL_MONSTERS   = 100000; // 임의 대형 컷오프

        /// <summary>Unity 메뉴: 모든 가이드/순환/일일 퀘스트 SO의 req가 시스템 상한을 초과하지 않는지 검증</summary>
        [MenuItem("mkLike/Quest/Validate Catalog")]
        public static void ValidateAllFromMenu()
        {
            ValidateAll();
        }

        /// <summary>모든 퀘스트 SO를 로드해 req가 시스템 상한을 초과하는지 검증 후 Console에 경고 출력</summary>
        public static void ValidateAll()
        {
            // 1) 시스템 상한 조회 — WeaponPool.MaxSummonLevel / EliteSummonSO.MaxLevel
            int weaponPoolMax = ResolveWeaponPoolMaxSummonLevel();
            int elitePoolMax = ResolveEliteSummonMaxLevel();

            // 2) 모든 QuestDataSO 로드 (Asset/Data/SO/Quest/ 하위)
            var warnings = new List<string>();
            var guids = AssetDatabase.FindAssets("t:QuestDataSO", new[] { QUEST_PATH });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var quest = AssetDatabase.LoadAssetAtPath<QuestDataSO>(path);
                if (quest == null) continue;

                int req = quest.requiredAmount;
                int max;
                string maxLabel;
                switch (quest.condition)
                {
                    case QuestCondition.WeaponSummonLevelReach:
                        max = weaponPoolMax;
                        maxLabel = "WeaponPool.MaxSummonLevel";
                        break;
                    case QuestCondition.EliteSummonLevelReach:
                        max = elitePoolMax;
                        maxLabel = "EliteSummonSO.MaxLevel";
                        break;
                    case QuestCondition.StarForceReach:
                        max = MAX_STAR_FORCE;
                        maxLabel = "max";
                        break;
                    case QuestCondition.HeroPowerMilestone:
                        max = MAX_HERO_POWER;
                        maxLabel = "max";
                        break;
                    case QuestCondition.JobAdvance:
                        max = MAX_JOB_ADVANCE;
                        maxLabel = "max";
                        break;
                    case QuestCondition.ClearStage:
                        max = MAX_CLEAR_STAGE;
                        maxLabel = "max";
                        break;
                    case QuestCondition.KillMonsters:
                        max = MAX_KILL_MONSTERS;
                        maxLabel = "max";
                        break;
                    default:
                        // 그 외 조건은 스킵
                        continue;
                }

                if (req > max)
                {
                    warnings.Add($"- {quest.id} ({quest.condition}) req={req} > {maxLabel}={max} ← 돌파 불가");
                }
            }

            // 3) 출력
            if (warnings.Count == 0)
            {
                Debug.Log("[GuideQuestGenerator] Validate: 경고 없음 (모든 req가 시스템 상한 내)");
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[GuideQuestGenerator] Validate: {warnings.Count}개 경고");
            for (int i = 0; i < warnings.Count; i++)
                sb.AppendLine(warnings[i]);
            Debug.LogWarning(sb.ToString());
        }

        /// <summary>GachaPool_Weapon.asset을 로드해 MaxSummonLevel 반환. 못 찾으면 int.MaxValue (검증 스킵).</summary>
        private static int ResolveWeaponPoolMaxSummonLevel()
        {
            // 파일명에 "Weapon"이 포함된 GachaPoolSO를 찾는다
            var guids = AssetDatabase.FindAssets("t:GachaPoolSO");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                // 무기 풀만 필터 (Equipment/Relic 제외)
                if (path.IndexOf("Weapon", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                var pool = AssetDatabase.LoadAssetAtPath<GachaPoolSO>(path);
                if (pool == null) continue;
                return pool.MaxSummonLevel;
            }
            Debug.LogWarning("[GuideQuestGenerator] GachaPool_Weapon.asset을 찾을 수 없어 WeaponSummonLevelReach 검증을 스킵한다");
            return int.MaxValue;
        }

        /// <summary>EliteSummon.asset을 로드해 MaxLevel 반환. 못 찾으면 int.MaxValue (검증 스킵).</summary>
        private static int ResolveEliteSummonMaxLevel()
        {
            var guids = AssetDatabase.FindAssets("t:EliteSummonSO");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var so = AssetDatabase.LoadAssetAtPath<EliteSummonSO>(path);
                if (so == null) continue;
                return so.MaxLevel;
            }
            Debug.LogWarning("[GuideQuestGenerator] EliteSummon.asset을 찾을 수 없어 EliteSummonLevelReach 검증을 스킵한다");
            return int.MaxValue;
        }

        // ============================================================
        // Cleanup & Utils
        // ============================================================

        /// <summary>구 2자리 가이드 SO + 초과 순환 SO 삭제</summary>
        private static void CleanupLegacyAssets()
        {
            // 구 2자리 가이드 퀘스트 삭제 (Quest_Guide_01 ~ Quest_Guide_100)
            for (int i = 1; i <= 100; i++)
            {
                string path = $"{QUEST_PATH}/Quest_Guide_{i:D2}.asset";
                if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                    AssetDatabase.DeleteAsset(path);
            }

            // 초과 순환 퀘스트 삭제 (14~17, 기존 18개 → 14개로 축소)
            for (int i = PER_CYCLE; i <= 20; i++)
            {
                string path = $"{QUEST_PATH}/Quest_Cycling_{i:D2}.asset";
                if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                    AssetDatabase.DeleteAsset(path);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
