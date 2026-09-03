#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using MkLike.Core;
using MkLike.Data;
using MkLike.Quest;

namespace MkLike.Editor
{
    /// <summary>
    /// 업적/스킬/히든룸 SO 에셋을 에디터 메뉴로 자동 생성하는 스크립트.
    /// </summary>
    public static class ContentAssetGenerator
    {
        private const string SKILL_PATH = "Assets/Data/Skills";

        private const string ROUND2_PATH = "Assets/Data/Round2";

        public static void GenerateAll()
        {
            Generate4thJobSkills();
            GenerateRound2Content();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ContentAssetGenerator] 모든 콘텐츠 에셋 생성 완료");
        }

        /// <summary>
        /// 라운드 2 콘텐츠 SO를 일괄 생성한다.
        /// </summary>
        public static void GenerateRound2Content()
        {
            EnsureFolder(ROUND2_PATH);

            Debug.Log("[ContentAssetGenerator] 라운드 2 콘텐츠 에셋 생성 완료");
        }

        /// <summary>
        /// 확장 업적 데이터를 코드 기반으로 생성하여 반환한다.
        /// 8카테고리 x 5건 = 40건.
        /// AchievementSystem.Initialize()에 전달하여 사용.
        /// </summary>
        public static List<AchievementData> GenerateExpandedAchievements()
        {
            var list = new List<AchievementData>(40);

            // ── 시즌 (Season) ──
            list.Add(MakeAch("ach_season_01", "시즌 초보자", "시즌 보상 5회 수령",
                AchievementCategory.Season, AchievementCondition.SeasonRewardClaimed, 5,
                CurrencyType.Ruby, 100));
            list.Add(MakeAch("ach_season_02", "시즌 참여자", "시즌 보상 20회 수령",
                AchievementCategory.Season, AchievementCondition.SeasonRewardClaimed, 20,
                CurrencyType.Ruby, 300));
            list.Add(MakeAch("ach_season_03", "시즌 숙련자", "시즌 보상 50회 수령",
                AchievementCategory.Season, AchievementCondition.SeasonRewardClaimed, 50,
                CurrencyType.Ruby, 500));
            list.Add(MakeAch("ach_season_04", "시즌 전문가", "시즌 보상 100회 수령",
                AchievementCategory.Season, AchievementCondition.SeasonRewardClaimed, 100,
                CurrencyType.Ruby, 1000));
            list.Add(MakeAch("ach_season_05", "시즌 마스터", "시즌 보상 200회 수령",
                AchievementCategory.Season, AchievementCondition.SeasonRewardClaimed, 200,
                CurrencyType.Ruby, 2000, titleId: "title_season_master", titleName: "시즌의 지배자"));

            // ── PvP (아레나) ──
            list.Add(MakeAch("ach_pvp_01", "첫 승리", "PvP 1승 달성",
                AchievementCategory.Pvp, AchievementCondition.PvpVictory, 1,
                CurrencyType.Ruby, 50));
            list.Add(MakeAch("ach_pvp_02", "전장의 기사", "PvP 50승 달성",
                AchievementCategory.Pvp, AchievementCondition.PvpVictory, 50,
                CurrencyType.Ruby, 300));
            list.Add(MakeAch("ach_pvp_03", "무패의 전사", "PvP 연승 5회 달성",
                AchievementCategory.Pvp, AchievementCondition.PvpWinStreak, 5,
                CurrencyType.Ruby, 500));
            list.Add(MakeAch("ach_pvp_04", "아레나의 왕", "PvP 200승 달성",
                AchievementCategory.Pvp, AchievementCondition.PvpVictory, 200,
                CurrencyType.Ruby, 1000));
            list.Add(MakeAch("ach_pvp_05", "불멸의 챔피언", "PvP 연승 10회 달성",
                AchievementCategory.Pvp, AchievementCondition.PvpWinStreak, 10,
                CurrencyType.Ruby, 2000, titleId: "title_pvp_champion", titleName: "불멸의 챔피언"));

            // ── 펫 [제거됨 2026-04-23] Pet 시스템 소스 없음 → 업적 5개 진행 불가 → 삭제 ──

            // ── 길드 ──
            list.Add(MakeAch("ach_guild_01", "길드원", "길드 기여 10회",
                AchievementCategory.Guild, AchievementCondition.GuildContribute, 10,
                CurrencyType.Gold, 3000));
            list.Add(MakeAch("ach_guild_02", "길드 기사", "길드 기여 50회",
                AchievementCategory.Guild, AchievementCondition.GuildContribute, 50,
                CurrencyType.Ruby, 200));
            list.Add(MakeAch("ach_guild_03", "보스 사냥꾼", "길드 보스 5회 처치",
                AchievementCategory.Guild, AchievementCondition.GuildBossKill, 5,
                CurrencyType.Ruby, 500));
            list.Add(MakeAch("ach_guild_04", "길드의 기둥", "길드 기여 200회",
                AchievementCategory.Guild, AchievementCondition.GuildContribute, 200,
                CurrencyType.Ruby, 1000));
            list.Add(MakeAch("ach_guild_05", "길드 영웅", "길드 보스 20회 처치",
                AchievementCategory.Guild, AchievementCondition.GuildBossKill, 20,
                CurrencyType.Ruby, 2000, titleId: "title_guild_hero", titleName: "길드의 영웅"));

            // ── 코스튬 (2026-04-23 제거): Costume 시스템 2026-04-20 삭제 후 영원히 달성 불가한 dead 업적이었음.

            // ── 등반조합 (2026-04-23 제거): Climber 시스템 2026-04-20 삭제 후 영원히 달성 불가한 dead 업적이었음.
            // 실제 생성된 ach_climb_* SO 파일이 있다면 사용자 업적 탭에서 수동 정리 필요.

            // ── 도감 ──
            list.Add(MakeAch("ach_compendium_ext_01", "수집가 입문", "도감 10종 수집",
                AchievementCategory.Compendium, AchievementCondition.CollectionTotal, 10,
                CurrencyType.Gold, 5000));
            list.Add(MakeAch("ach_compendium_ext_02", "수집의 즐거움", "도감 30종 수집",
                AchievementCategory.Compendium, AchievementCondition.CollectionTotal, 30,
                CurrencyType.Ruby, 200));
            list.Add(MakeAch("ach_compendium_ext_03", "박물관장", "도감 60종 수집",
                AchievementCategory.Compendium, AchievementCondition.CollectionTotal, 60,
                CurrencyType.Ruby, 500));
            list.Add(MakeAch("ach_compendium_ext_04", "만물 수집가", "도감 100종 수집",
                AchievementCategory.Compendium, AchievementCondition.CollectionTotal, 100,
                CurrencyType.Ruby, 1000));
            list.Add(MakeAch("ach_compendium_ext_05", "백과사전", "도감 200종 수집",
                AchievementCategory.Compendium, AchievementCondition.CollectionTotal, 200,
                CurrencyType.Ruby, 2000, titleId: "title_encyclopedia", titleName: "살아있는 백과사전"));

            Debug.Log($"[ContentAssetGenerator] 확장 업적 {list.Count}건 생성");
            return list;
        }

        private static AchievementData MakeAch(string id, string displayName, string description,
            AchievementCategory category, AchievementCondition condition, int required,
            CurrencyType rewardType, int rewardAmount,
            string titleId = null, string titleName = null)
        {
            return new AchievementData
            {
                id = id,
                displayName = displayName,
                description = description,
                category = category,
                condition = condition,
                requiredAmount = required,
                rewardType = rewardType,
                rewardAmount = rewardAmount,
                titleRewardId = titleId,
                titleDisplayName = titleName
            };
        }

        private static void Generate4thJobSkills()
        {
            EnsureFolder(SKILL_PATH);

            // 전사 4차: 드래곤 브레스 (범위, 쿨 15초, Atk*3.0)
            var dragonBreath = CreateInstance<SkillDataSO>("Skill_DragonBreath");
            dragonBreath.id = "skill_dragon_breath";
            dragonBreath.displayName = "드래곤 브레스";
            dragonBreath.description = "용의 숨결을 내뿜어 전방 광범위 적에게 ATK 300%의 화염 피해를 입힌다.";
            dragonBreath.skillType = SkillType.Awakening;
            dragonBreath.learnLevel = 1;
            dragonBreath.jobTier = 3;
            dragonBreath.cooldown = 15f;
            dragonBreath.damageMultiplier = 3.0f;
            dragonBreath.range = 4f;
            dragonBreath.hitCount = 1;
            dragonBreath.specialEffect = SkillEffect.Burn;
            dragonBreath.effectDuration = 3f;
            dragonBreath.dotDamageRate = 0.5f;
            SaveAsset(dragonBreath, $"{SKILL_PATH}/Skill_DragonBreath.asset");

            // 전사 4차: 철벽 방패 (버프, 쿨 30초, 무적 3초)
            var ironShield = CreateInstance<SkillDataSO>("Skill_IronShield");
            ironShield.id = "skill_iron_shield";
            ironShield.displayName = "철벽 방패";
            ironShield.description = "불멸의 방패를 전개하여 3초간 무적 상태가 된다.";
            ironShield.skillType = SkillType.Buff;
            ironShield.learnLevel = 1;
            ironShield.jobTier = 3;
            ironShield.cooldown = 30f;
            ironShield.duration = 3f;
            ironShield.buffDefRate = 1.0f;
            ironShield.specialEffect = SkillEffect.Revive;
            ironShield.effectDuration = 3f;
            SaveAsset(ironShield, $"{SKILL_PATH}/Skill_IronShield.asset");

            // 궁수 4차: 폭풍 화살비 (범위, 쿨 12초, Atk*2.5)
            var arrowStorm = CreateInstance<SkillDataSO>("Skill_ArrowStorm");
            arrowStorm.id = "skill_arrow_storm";
            arrowStorm.displayName = "폭풍 화살비";
            arrowStorm.description = "하늘에서 화살비를 내려 광범위 적에게 ATK 250%의 피해를 입힌다.";
            arrowStorm.skillType = SkillType.Awakening;
            arrowStorm.learnLevel = 1;
            arrowStorm.jobTier = 3;
            arrowStorm.cooldown = 12f;
            arrowStorm.damageMultiplier = 2.5f;
            arrowStorm.range = 5f;
            arrowStorm.hitCount = 3;
            arrowStorm.specialEffect = SkillEffect.MultiShot;
            SaveAsset(arrowStorm, $"{SKILL_PATH}/Skill_ArrowStorm.asset");

            // 궁수 4차: 바람의 날개 (버프, 쿨 25초, 이속 2배 5초)
            var windWings = CreateInstance<SkillDataSO>("Skill_WindWings");
            windWings.id = "skill_wind_wings";
            windWings.displayName = "바람의 날개";
            windWings.description = "바람의 힘을 빌려 5초간 이동속도가 2배로 증가한다.";
            windWings.skillType = SkillType.Buff;
            windWings.learnLevel = 1;
            windWings.jobTier = 3;
            windWings.cooldown = 25f;
            windWings.duration = 5f;
            windWings.buffMoveSpeedRate = 1.0f;
            windWings.specialEffect = SkillEffect.Dodge;
            windWings.effectDuration = 5f;
            SaveAsset(windWings, $"{SKILL_PATH}/Skill_WindWings.asset");

            // 마법사 4차: 메테오 (범위, 쿨 20초, Atk*4.0)
            var meteor = CreateInstance<SkillDataSO>("Skill_Meteor");
            meteor.id = "skill_meteor";
            meteor.displayName = "메테오";
            meteor.description = "거대한 운석을 소환하여 광범위 적에게 ATK 400%의 피해를 입힌다.";
            meteor.skillType = SkillType.Awakening;
            meteor.learnLevel = 1;
            meteor.jobTier = 3;
            meteor.cooldown = 20f;
            meteor.damageMultiplier = 4.0f;
            meteor.range = 6f;
            meteor.hitCount = 1;
            meteor.specialEffect = SkillEffect.Stun;
            meteor.effectDuration = 1.5f;
            SaveAsset(meteor, $"{SKILL_PATH}/Skill_Meteor.asset");

            // 마법사 4차: 마나 폭발 (범위, 쿨 10초, Atk*2.0+넉백)
            var manaExplosion = CreateInstance<SkillDataSO>("Skill_ManaExplosion");
            manaExplosion.id = "skill_mana_explosion";
            manaExplosion.displayName = "마나 폭발";
            manaExplosion.description = "축적된 마나를 폭발시켜 주변 적에게 ATK 200%의 피해를 입히고 넉백시킨다.";
            manaExplosion.skillType = SkillType.Awakening;
            manaExplosion.learnLevel = 1;
            manaExplosion.jobTier = 3;
            manaExplosion.cooldown = 10f;
            manaExplosion.damageMultiplier = 2.0f;
            manaExplosion.range = 3.5f;
            manaExplosion.hitCount = 1;
            manaExplosion.specialEffect = SkillEffect.Knockback;
            manaExplosion.effectValue = 3f;
            SaveAsset(manaExplosion, $"{SKILL_PATH}/Skill_ManaExplosion.asset");

            Debug.Log($"[ContentAssetGenerator] 4차 전직 스킬 에셋 생성 완료 — 6개");
        }

        private static T CreateInstance<T>(string name) where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            instance.name = name;
            return instance;
        }

        private static void SaveAsset(Object asset, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing != null)
            {
                Debug.Log($"[ContentAssetGenerator] 이미 존재하는 에셋 스킵: {path}");
                return;
            }

            AssetDatabase.CreateAsset(asset, path);
            Undo.RegisterCreatedObjectUndo(asset, $"Create {asset.name}");
        }

        private static void SetPrivateField(Object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(target, value);
                EditorUtility.SetDirty(target);
            }
            else
            {
                Debug.LogWarning($"[ContentAssetGenerator] 필드를 찾을 수 없음: {target.GetType().Name}.{fieldName}");
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
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
#endif
