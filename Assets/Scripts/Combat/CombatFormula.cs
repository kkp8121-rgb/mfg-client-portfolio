using System;
using UnityEngine;
using MkLike.Core;

namespace MkLike.Combat
{
    /// <summary>
    /// 전투 관련 공식 모음 정적 클래스.
    /// 몬스터 스탯 스케일링, 보스 배율, 전투력(CP) 계산 등을 포함한다.
    /// RequiredExp/MonsterExp 데이터는 Core.ExpTable에 위치 (전 어셈블리 접근 가능).
    /// </summary>
    public static class CombatFormula
    {
        // ── 일반 몬스터 기본값 ──

        private const int MONSTER_HP_BASE = 18;
        private const int MONSTER_HP_PER_FLOOR = 15;
        private const int MONSTER_ATK_BASE = 3;
        private const int MONSTER_ATK_PER_FLOOR = 2;
        private const int MONSTER_DEF_BASE = 2;
        private const float MONSTER_DEF_PER_FLOOR = 1.5f;

        private const float MONSTER_GOLD_BASE = 5f;
        private const float MONSTER_GOLD_GROWTH = 1.12f;

        // ── 보스 배율 ──

        private const int BOSS_GOLD_MULTIPLIER = 10;
        private const int BOSS_EXP_MULTIPLIER = 10;

        // ── 일반 몬스터 층수 스케일링 ──

        public static int MonsterHp(int floor)
        {
            // 성장률 1.02x — Floor 200에서 약 52배 (HP ~157K)
            double hp = MONSTER_HP_BASE + (floor * MONSTER_HP_PER_FLOOR);
            hp *= System.Math.Pow(1.02, Mathf.Max(0, floor - 1));
            return (int)System.Math.Min(hp, int.MaxValue);
        }

        public static int MonsterAtk(int floor)
        {
            // 성장률 1.015x — Floor 200에서 약 19배 (ATK ~7.6K)
            double atk = MONSTER_ATK_BASE + (floor * MONSTER_ATK_PER_FLOOR);
            atk *= System.Math.Pow(1.015, Mathf.Max(0, floor - 1));
            return (int)System.Math.Min(atk, int.MaxValue);
        }

        public static int MonsterDef(int floor)
        {
            // 성장률 1.01x — Floor 200에서 약 7배 (DEF ~2.1K)
            double def = MONSTER_DEF_BASE + (floor * MONSTER_DEF_PER_FLOOR);
            def *= System.Math.Pow(1.01, Mathf.Max(0, floor - 1));
            return (int)System.Math.Min(def, int.MaxValue);
        }

        /// <summary>
        /// 일반 몬스터 골드 드롭. 지수적 성장.
        /// </summary>
        public static long MonsterGold(int floor) =>
            ClampToLong(MONSTER_GOLD_BASE * System.Math.Pow(MONSTER_GOLD_GROWTH, System.Math.Max(0, floor - 1)));

        /// <summary>
        /// 일반 몬스터 경험치 드롭. Core.ExpTable 위임.
        /// </summary>
        public static long MonsterExp(int floor) => ExpTable.MonsterExp(floor);

        // ── 보스 스탯 ──
        // 무한 진행 대응: double 중간 계산 후 int/long 상한 클램프. 오버플로우 방지.

        public static int BossHp(int floor) => ClampToInt((double)MonsterHp(floor) * BossHpMultiplier);
        public static int BossAtk(int floor) => ClampToInt((double)MonsterAtk(floor) * BossAtkMultiplier);
        public static int BossDef(int floor) => ClampToInt((double)MonsterDef(floor) * BossDefMultiplier);
        public static long BossGold(int floor) => ClampToLong((double)MonsterGold(floor) * BOSS_GOLD_MULTIPLIER);
        public static long BossExp(int floor) => ClampToLong((double)MonsterExp(floor) * BOSS_EXP_MULTIPLIER);

        private static int ClampToInt(double v)
        {
            if (double.IsNaN(v) || v <= 0) return 1;
            if (v >= int.MaxValue) return int.MaxValue;
            return (int)System.Math.Round(v);
        }

        private static long ClampToLong(double v)
        {
            if (double.IsNaN(v) || v <= 0) return 0;
            if (v >= long.MaxValue) return long.MaxValue;
            return (long)System.Math.Round(v);
        }

        /// <summary>구간 이름을 반환한다.</summary>
        public static string GetZoneName(int floor)
        {
            if (floor > 300) return "무한";
            if (floor >= 201) return "심연";
            if (floor >= 101) return "엘리트";
            return "일반";
        }

        // ── 챕터 보스 보상 ──

        private const int CHAPTER_BOSS_REWARD_MULTIPLIER = 50;

        public static long ChapterBossGold(int floor) => ClampToLong((double)MonsterGold(floor) * CHAPTER_BOSS_REWARD_MULTIPLIER);
        public static long ChapterBossExp(int floor) => ClampToLong((double)MonsterExp(floor) * CHAPTER_BOSS_REWARD_MULTIPLIER);

        // ── 강화 비용 공식 상수 ──

        private const float SCROLL_ENHANCE_GOLD_BASE = 100f;
        private const float SCROLL_ENHANCE_GOLD_GROWTH = 1.12f;
        private const int SCROLL_ENHANCE_RUNE_BASE = 3;

        private const float STAR_FORCE_GOLD_BASE = 500f;
        private const float STAR_FORCE_GOLD_GROWTH = 1.18f;
        private const int STAR_FORCE_CRYSTAL_BASE = 5;
        private const int STAR_FORCE_CRYSTAL_PER_STAR = 2;

        private const float POTENTIAL_GOLD_BASE = 200f;
        private const float POTENTIAL_GOLD_GROWTH = 1.15f;

        private const float WEAPON_LEVEL_GOLD_BASE = 50f;
        private const float WEAPON_LEVEL_GOLD_GROWTH = 1.08f;
        private const int WEAPON_LEVEL_STONE_BASE = 2;
        private const int WEAPON_LEVEL_STONE_DIVISOR = 5;

        private const int ELITE_SUMMON_BASE = 100;
        private const int ELITE_SUMMON_PER_TIER = 50;

        // ── 강화 비용 공식 ──

        /// <summary>주문서 강화 골드 비용</summary>
        public static long ScrollEnhanceGold(int level) =>
            Mathf.RoundToInt(SCROLL_ENHANCE_GOLD_BASE * Mathf.Pow(SCROLL_ENHANCE_GOLD_GROWTH, Mathf.Max(0, level - 1)));

        /// <summary>주문서 강화 룬 조각 비용</summary>
        public static int ScrollEnhanceRuneFragment(int level) => SCROLL_ENHANCE_RUNE_BASE + level;

        /// <summary>성급 강화 골드 비용</summary>
        public static long StarForceGold(int star) =>
            Mathf.RoundToInt(STAR_FORCE_GOLD_BASE * Mathf.Pow(STAR_FORCE_GOLD_GROWTH, star));

        /// <summary>성급 강화 별의 결정 비용</summary>
        public static int StarForceStarCrystal(int star) => STAR_FORCE_CRYSTAL_BASE + star * STAR_FORCE_CRYSTAL_PER_STAR;

        /// <summary>잠재능력 변경 골드 비용</summary>
        public static long PotentialChangeGold(int currentTier) =>
            Mathf.RoundToInt(POTENTIAL_GOLD_BASE * Mathf.Pow(POTENTIAL_GOLD_GROWTH, currentTier));

        /// <summary>무기 레벨업 골드 비용</summary>
        public static long WeaponLevelGold(int level) =>
            Mathf.RoundToInt(WEAPON_LEVEL_GOLD_BASE * Mathf.Pow(WEAPON_LEVEL_GOLD_GROWTH, Mathf.Max(0, level - 1)));

        /// <summary>무기 레벨업 무기강화석 비용</summary>
        public static int WeaponLevelStone(int level) => WEAPON_LEVEL_STONE_BASE + level / WEAPON_LEVEL_STONE_DIVISOR;

        /// <summary>정예 몬스터 소환 사냥 포인트 비용</summary>
        public static int EliteSummonHuntPoint(int equipTier) => ELITE_SUMMON_BASE + equipTier * ELITE_SUMMON_PER_TIER;

        // ── 오프라인 보상 ──

        /// <summary>오프라인 킬 속도 (마리/분)</summary>
        public const int OFFLINE_KILLS_PER_MINUTE = 50;

        /// <summary>오프라인 보상 효율 배율 (온라인 대비 60%)</summary>
        private const float OFFLINE_EFFICIENCY = 0.6f;

        /// <summary>최대 오프라인 누적 시간 (시간)</summary>
        public const int MAX_OFFLINE_HOURS = 24;

        /// <summary>최대 오프라인 누적 시간 (분)</summary>
        public const int MaxOfflineMinutes = MAX_OFFLINE_HOURS * 60;

        /// <summary>오프라인 분당 골드 (킬 수 × 몬스터 골드 × 효율)</summary>
        public static long OfflineGoldPerMin(int floor) =>
            (long)(MonsterGold(floor) * OFFLINE_KILLS_PER_MINUTE * OFFLINE_EFFICIENCY);

        /// <summary>오프라인 분당 경험치 (킬 수 × 몬스터 경험치 × 효율)</summary>
        public static long OfflineExpPerMin(int floor) =>
            (long)(MonsterExp(floor) * OFFLINE_KILLS_PER_MINUTE * OFFLINE_EFFICIENCY);

        /// <summary>오프라인 총 킬 수 계산</summary>
        public static int OfflineTotalKills(int elapsedMinutes) =>
            Mathf.Clamp(elapsedMinutes, 0, MaxOfflineMinutes) * OFFLINE_KILLS_PER_MINUTE;

        // ── 오프라인 아이템 드롭 확률 ──

        /// <summary>오프라인 사냥 포인트 드롭 (킬당 1)</summary>
        public const int OFFLINE_HUNT_POINT_PER_KILL = 1;

        /// <summary>오프라인 룬 조각 드롭 확률 (킬당)</summary>
        public const float OFFLINE_RUNE_FRAGMENT_RATE = 0.05f;

        /// <summary>오프라인 별의 결정 드롭 확률 (킬당)</summary>
        public const float OFFLINE_STAR_CRYSTAL_RATE = 0.03f;

        /// <summary>오프라인 루비 드롭 확률 (킬당)</summary>
        public const float OFFLINE_RUBY_RATE = 0.005f;

        // ── CP 공식 가중치 ──

        private const int CP_ATK_WEIGHT = 4;
        private const int CP_DEF_WEIGHT = 3;
        private const int CP_HP_DIVISOR = 5;
        private const float CP_CRIT_WEIGHT = 500f;

        /// <summary>
        /// 해당 레벨에서 다음 레벨까지 필요한 경험치를 반환한다. Core.ExpTable 위임.
        /// </summary>
        public static long RequiredExp(int level) => ExpTable.RequiredExp(level);

        /// <summary>
        /// 전투력(Combat Power)을 계산한다.
        /// CP = (ATK * 4) + (DEF * 3) + (HP / 5) + (CritRate * 500)
        /// </summary>
        public static int CalculateCP(int atk, int def, int hp, float critRate)
        {
            return (atk * CP_ATK_WEIGHT) + (def * CP_DEF_WEIGHT) + (hp / CP_HP_DIVISOR) + Mathf.RoundToInt(critRate * CP_CRIT_WEIGHT);
        }

        // ── 몬스터 층수 스케일링 (지수적) ──

        private const float MONSTER_HP_SCALE_GROWTH = 1.05f;
        private const float MONSTER_ATK_SCALE_GROWTH = 1.04f;

        /// <summary>
        /// 몬스터 HP 층수 스케일링. 기본 HP에 곱할 배율.
        /// </summary>
        public static float MonsterHpScale(int floor) =>
            Mathf.Pow(MONSTER_HP_SCALE_GROWTH, Mathf.Max(0, floor - 1));

        /// <summary>
        /// 몬스터 ATK 층수 스케일링. 기본 ATK에 곱할 배율.
        /// </summary>
        public static float MonsterAtkScale(int floor) =>
            Mathf.Pow(MONSTER_ATK_SCALE_GROWTH, Mathf.Max(0, floor - 1));

        /// <summary>보스 HP 배율 상수. 일반 몬스터 대비 6배.</summary>
        public const float BossHpMultiplier = 6f;

        /// <summary>보스 ATK 배율 상수. 일반 몬스터 대비 1.3배.</summary>
        public const float BossAtkMultiplier = 1.3f;

        /// <summary>보스 DEF 배율 상수. 일반 몬스터 대비 1.2배.</summary>
        public const float BossDefMultiplier = 1.2f;

        // ── 스킬/등반 비용 ──

        private const long SKILL_LEVELUP_BASE = 100;
        private const int CLIMBER_POWER_BASE = 5;
        private const int CLIMBER_POWER_PER_LEVEL = 2;

        /// <summary>
        /// 스킬 레벨업 골드 비용.
        /// </summary>
        public static long SkillLevelUpCost(int level) => SKILL_LEVELUP_BASE * level * level;

        /// <summary>
        /// 등반자의 힘 레벨업 ClimbToken 비용.
        /// </summary>
        public static int ClimberPowerCost(int level) => CLIMBER_POWER_BASE + level * CLIMBER_POWER_PER_LEVEL;

        // ── 전투력(CP) 통합 공식 ──

        // ── 통합 CP 공식 가중치 ──

        private const float CP2_ATK_WEIGHT = 3f;
        private const float CP2_HP_WEIGHT = 0.5f;
        private const float CP2_DEF_WEIGHT = 2f;
        private const float CP2_CRIT_WEIGHT = 500f;
        private const float CP2_ATKSPD_WEIGHT = 100f;

        /// <summary>
        /// CombatStats 컴포넌트로부터 전투력(CP)을 계산한다.
        /// </summary>
        public static long CalculateCP(CombatStats stats)
        {
            if (stats == null) return 0;
            return (long)(
                stats.Atk * CP2_ATK_WEIGHT +
                stats.MaxHp * CP2_HP_WEIGHT +
                stats.Def * CP2_DEF_WEIGHT +
                stats.CritRate * CP2_CRIT_WEIGHT +
                stats.AttackSpeed * CP2_ATKSPD_WEIGHT
            );
        }

        // ── 오프라인 보상 계산 ──

        /// <summary>
        /// 오프라인 보상 총 골드를 계산한다. 경과 시간(분)은 MaxOfflineMinutes로 제한.
        /// </summary>
        public static long CalculateOfflineGold(int floor, int elapsedMinutes)
        {
            int clampedMin = Mathf.Clamp(elapsedMinutes, 0, MaxOfflineMinutes);
            return OfflineGoldPerMin(floor) * clampedMin;
        }

        /// <summary>
        /// 오프라인 보상 총 경험치를 계산한다. 경과 시간(분)은 MaxOfflineMinutes로 제한.
        /// </summary>
        public static long CalculateOfflineExp(int floor, int elapsedMinutes)
        {
            int clampedMin = Mathf.Clamp(elapsedMinutes, 0, MaxOfflineMinutes);
            return OfflineExpPerMin(floor) * clampedMin;
        }
    }
}
