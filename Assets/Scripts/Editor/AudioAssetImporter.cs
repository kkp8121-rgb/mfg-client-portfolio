using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MkLike.Editor
{
    /// <summary>
    /// Folder_Assets/SFX/ 및 BGM/ 폴더의 오디오 파일을
    /// Resources/Audio/ 하위로 복사하여 AudioManager에서 사용 가능하게 한다.
    /// SFX_FILE_MAP / BGM_FILE_MAP 키 이름으로 리네이밍 복사한다.
    /// </summary>
    public static class AudioAssetImporter
    {
        private const string SFX_SOURCE_ROOT = "Assets/Folder_Assets/SFX";
        private const string BGM_SOURCE_ROOT = "Assets/Folder_Assets/BGM";
        private const string SFX_OUTPUT = "Assets/Resources/Audio/SFX";
        private const string BGM_OUTPUT = "Assets/Resources/Audio/BGM";

        // SFX: 출력 파일명 → 소스 상대 경로 (확장자 제외, 소스 루트 기준)
        // Fantasy_200 팩 기반 매핑 (RPG_Essentials 파일이 존재하지 않아 전환)
        private static readonly Dictionary<string, string> SFX_MAPPING = new()
        {
            // ── 공격 ──
            { "Attack/sfx_sword_swing", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Sword Attacks Hits and Blocks/Sword Attack 1" },
            { "Attack/sfx_sword_hit", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Sword Attacks Hits and Blocks/Sword Impact Hit 1" },
            { "Attack/sfx_bow_release", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Bow Attacks Hits and Blocks/Bow Attack 1" },
            { "Attack/sfx_arrow_hit", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Bow Attacks Hits and Blocks/Bow Impact Hit 1" },
            { "Attack/sfx_magic_cast", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Fireball 1" },
            { "Attack/sfx_magic_hit", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Spell Impact 1" },
            { "Attack/sfx_crit_hit", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Sword Attacks Hits and Blocks/Sword Attack 3" },

            // ── 몬스터 ──
            { "Monster/sfx_monster_hit", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Sword Attacks Hits and Blocks/Sword Impact Hit 2" },
            { "Monster/sfx_monster_die", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Sword Attacks Hits and Blocks/Sword Impact Hit 3" },
            { "Monster/sfx_monster_die_01", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Sword Attacks Hits and Blocks/Sword Impact Hit 3" },
            { "Monster/sfx_boss_roar", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Rock Meteor Swarm 1" },
            { "Monster/sfx_boss_die", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Spell Impact 3" },

            // ── 시스템 ──
            { "System/sfx_levelup", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 1" },
            { "System/sfx_job_advance", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 2" },
            { "System/sfx_enhance_success", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Chest Open 1" },
            { "System/sfx_enhance_fail", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Door Close 1" },
            { "System/sfx_enhance_destroy", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Door Close 2" },
            { "System/sfx_starforce_up", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 1" },
            { "System/sfx_potential_change", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Ice Freeze 1" },
            { "System/sfx_gold_pickup", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Lock Unlock" },
            { "System/sfx_exp_pickup", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Chest Close 1" },
            { "System/sfx_item_drop", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Sword Attacks Hits and Blocks/Sword Sheath 1" },
            { "System/sfx_equip", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Sword Attacks Hits and Blocks/Sword Unsheath 1" },
            { "System/sfx_unequip", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Sword Attacks Hits and Blocks/Sword Sheath 2" },

            // ── UI ──
            { "UI/sfx_ui_tap", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Lock Unlock" },
            { "UI/sfx_ui_back", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Door Close 1" },
            { "UI/sfx_ui_tab_switch", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Chest Close 2" },
            { "UI/sfx_ui_popup_open", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Door Open 1" },
            { "UI/sfx_ui_popup_close", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Door Close 2" },
            { "UI/sfx_ui_confirm", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Chest Open 2" },
            { "UI/sfx_ui_cancel", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Chest Close 1" },
            { "UI/sfx_ui_error", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Gate Close" },
            { "UI/sfx_ui_reward", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 2" },

            // ── 가챠 ──
            { "Gacha/sfx_gacha_spin", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Ice Throw 1" },
            { "Gacha/sfx_gacha_stop", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Ice Freeze 2" },
            { "Gacha/sfx_gacha_reveal_normal", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Chest Open 1" },
            { "Gacha/sfx_gacha_reveal_rare", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Ice Wall 1" },
            { "Gacha/sfx_gacha_reveal_epic", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 1" },
            { "Gacha/sfx_gacha_reveal_unique", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Fireball 2" },
            { "Gacha/sfx_gacha_reveal_legendary", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Fireball 3" },
            { "Gacha/sfx_gacha_reveal_mythic", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Rock Meteor Swarm 2" },
            { "Gacha/sfx_gacha_result", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Ice Barrage 1" },
            { "Gacha/sfx_gacha_multi_result", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Ice Barrage 2" },

            // ── 스킬 (공용) ──
            { "Skill/sfx_skill_slash", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Sword Attacks Hits and Blocks/Sword Attack 2" },
            { "Skill/sfx_skill_charge", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firespray 1" },
            { "Skill/sfx_skill_warcry", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Rock Meteor Swarm 1" },
            { "Skill/sfx_skill_bow_multi", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Bow Attacks Hits and Blocks/Bow Attack 2" },
            { "Skill/sfx_skill_bow_storm", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Bow Attacks Hits and Blocks/Bow Impact Hit 2" },
            { "Skill/sfx_skill_fire", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Fireball 2" },
            { "Skill/sfx_skill_ice", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Ice Barrage 1" },
            { "Skill/sfx_skill_lightning", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Spell Impact 2" },
            { "Skill/sfx_skill_arcane", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Waterspray 1" },
            { "Skill/sfx_skill_meteor", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Rock Meteor Throw 1" },
            { "Skill/sfx_skill_buff_activate", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 1" },
            { "Skill/sfx_skill_awakening", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 2" },

            // ── 배틀패스/시즌 ──
            { "System/sfx_battlepass_levelup", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 1" },
            { "System/sfx_reward", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Chest Open 2" },
            { "System/sfx_season_start", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Gate Open" },
            { "System/sfx_season_end", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Gate Close" },

            // ── 아레나 ──
            { "System/sfx_arena_match_start", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Portcullis Gate" },
            { "System/sfx_arena_victory", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 2" },
            { "System/sfx_arena_defeat", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Gate Close" },
            { "System/sfx_arena_rank_up", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 1" },
            { "System/sfx_arena_season_reward", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Chest Open 1" },

            // ── 전사 계열 스킬 ──
            { "Skill/sfx_skill_warrior_strike", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Sword Attacks Hits and Blocks/Sword Attack 1" },
            { "Skill/sfx_skill_warrior_fury", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Sword Attacks Hits and Blocks/Sword Attack 2" },
            { "Skill/sfx_skill_warrior_training", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Sword Attacks Hits and Blocks/Sword Parry 1" },
            { "Skill/sfx_skill_knight_judgment", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Sword Attacks Hits and Blocks/Sword Attack 3" },
            { "Skill/sfx_skill_knight_rally", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 1" },
            { "Skill/sfx_skill_knight_weakness", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Ice Freeze 1" },
            { "Skill/sfx_skill_titan_annihilate", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Rock Meteor Throw 2" },
            { "Skill/sfx_skill_titan_cataclysm", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Fireball 3" },
            { "Skill/sfx_skill_titan_immortal", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 2" },
            { "Skill/sfx_skill_titan_might", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 1" },
            { "Skill/sfx_skill_warlord_whirlwind", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Wave Attack 1" },
            { "Skill/sfx_skill_warlord_earthshatter", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Rock Wall 1" },
            { "Skill/sfx_skill_warlord_frenzy", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firespray 2" },
            { "Skill/sfx_skill_warlord_bloodpact", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Spell Impact 2" },

            // ── 궁수 계열 스킬 ──
            { "Skill/sfx_skill_archer_aimshot", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Bow Attacks Hits and Blocks/Bow Attack 1" },
            { "Skill/sfx_skill_archer_arrowrain", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Bow Attacks Hits and Blocks/Bow Attack 2" },
            { "Skill/sfx_skill_archer_keeneye", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Bow Attacks Hits and Blocks/Bow Take Out 1" },
            { "Skill/sfx_skill_archer_rapidfire", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Bow Attacks Hits and Blocks/Bow Impact Hit 2" },
            { "Skill/sfx_skill_scout_pierceshot", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Bow Attacks Hits and Blocks/Bow Impact Hit 3" },
            { "Skill/sfx_skill_scout_stormshot", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Bow Attacks Hits and Blocks/Bow Impact Hit 1" },
            { "Skill/sfx_skill_scout_windblessing", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Ice Wall 2" },
            { "Skill/sfx_skill_scout_agility", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Bow Attacks Hits and Blocks/Bow Put Away 1" },
            { "Skill/sfx_skill_hawkeye_eagleeye", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 1" },
            { "Skill/sfx_skill_hawkeye_extinction", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Bow Attacks Hits and Blocks/Bow Blocked 1" },
            { "Skill/sfx_skill_hawkeye_huntingtime", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Rock Meteor Swarm 1" },
            { "Skill/sfx_skill_hawkeye_judgment", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Bow Attacks Hits and Blocks/Bow Blocked 2" },
            { "Skill/sfx_skill_windwalker_gale", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Wave Attack 1" },
            { "Skill/sfx_skill_windwalker_typhon", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Wave Attack 2" },
            { "Skill/sfx_skill_windwalker_afterimage", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Ice Throw 2" },
            { "Skill/sfx_skill_windwalker_windpierce", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Bow Attacks Hits and Blocks/Bow Blocked 3" },

            // ── 마법사 계열 스킬 ──
            { "Skill/sfx_skill_mage_magicbolt", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Waterspray 1" },
            { "Skill/sfx_skill_mage_fireburst", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Fireball 1" },
            { "Skill/sfx_skill_mage_concentration", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Ice Freeze 1" },
            { "Skill/sfx_skill_mage_manacharge", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Ice Freeze 2" },
            { "Skill/sfx_skill_sorcerer_fireball", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Fireball 2" },
            { "Skill/sfx_skill_sorcerer_chainlightning", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Spell Impact 2" },
            { "Skill/sfx_skill_sorcerer_froststorm", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Ice Barrage 2" },
            { "Skill/sfx_skill_sorcerer_elemental", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Waterspray 2" },
            { "Skill/sfx_skill_sage_arcanemissile", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Spell Impact 1" },
            { "Skill/sfx_skill_sage_meteorshower", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Rock Meteor Swarm 2" },
            { "Skill/sfx_skill_sage_dimensionrift", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Ice Wall 1" },
            { "Skill/sfx_skill_sage_timewarp", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Ice Wall 2" },
            { "Skill/sfx_skill_runemaster_runeblast", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Fireball 3" },
            { "Skill/sfx_skill_runemaster_apocalypse", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Rock Meteor Swarm 1" },
            { "Skill/sfx_skill_runemaster_absolutedomain", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Rock Wall 2" },
            { "Skill/sfx_skill_runemaster_runemastery", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 1" },

            // ── 4차 전직 스킬 ──
            { "Skill/sfx_skill_dragonslayer_dragonblade", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Sword Attacks Hits and Blocks/Sword Attack 2" },
            { "Skill/sfx_skill_dragonslayer_dragonrage", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firespray 1" },
            { "Skill/sfx_skill_dragonslayer_apocalypse", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Fireball 3" },
            { "Skill/sfx_skill_dragonslayer_dragonblood", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 2" },
            { "Skill/sfx_skill_dragonslayer_dragonheart", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 1" },
            { "Skill/sfx_skill_dragonslayer_dragonscale", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Sword Attacks Hits and Blocks/Sword Blocked 1" },
            { "Skill/sfx_skill_dragonslayer_dragonaura", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firespray 2" },
            { "Skill/sfx_skill_stormbringer_tempest", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Wave Attack 2" },
            { "Skill/sfx_skill_stormbringer_chainlightning", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Spell Impact 3" },
            { "Skill/sfx_skill_stormbringer_thunderpierce", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Spell Impact 2" },
            { "Skill/sfx_skill_stormbringer_ragnarok", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Rock Meteor Swarm 2" },
            { "Skill/sfx_skill_stormbringer_stormmastery", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 1" },
            { "Skill/sfx_skill_stormbringer_stormshield", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Ice Wall 1" },
            { "Skill/sfx_skill_stormbringer_windwalking", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Wave Attack 1" },
            { "Skill/sfx_skill_archmage_voidbolt", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Waterspray 2" },
            { "Skill/sfx_skill_archmage_bigbang", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Fireball 3" },
            { "Skill/sfx_skill_archmage_cosmicfield", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Rock Wall 1" },
            { "Skill/sfx_skill_archmage_transcendence", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 2" },
            { "Skill/sfx_skill_archmage_elementalmastery", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 1" },
            { "Skill/sfx_skill_archmage_manashield", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Ice Wall 2" },
            { "Skill/sfx_skill_archmage_arcanemind", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Ice Freeze 2" },

            // ── 동료/펫 ──
            { "Companion/sfx_companion_summon", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 2" },
            { "Companion/sfx_companion_ultimate", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Rock Meteor Swarm 1" },
            { "Pet/sfx_pet_attack", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Sword Attacks Hits and Blocks/Sword Attack 1" },
            { "Pet/sfx_pet_ultimate", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Spell Impact 3" },
            { "Pet/sfx_pet_evolve", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 2" },

            // ── 던전/탑 ──
            { "Dungeon/sfx_dungeon_enter", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Gate Open" },
            { "Dungeon/sfx_dungeon_clear", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 2" },
            { "Dungeon/sfx_tower_floor_clear", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Chest Open 2" },
            { "Dungeon/sfx_secret_room_discover", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Door Open 2" },
            { "Dungeon/sfx_secret_room_enter", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Door Open 1" },

            // ── 길드 ──
            { "Guild/sfx_guild_boss_phase", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Rock Meteor Swarm 1" },
            { "Guild/sfx_guild_raid_clear", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 2" },
            { "Guild/sfx_guild_levelup", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 1" },

            // ── 성장/컬렉션 ──
            { "Growth/sfx_prestige", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Rock Meteor Throw 1" },
            { "Growth/sfx_mastery_unlock", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 1" },
            { "Growth/sfx_costume_obtain", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Chest Open 1" },
            { "Growth/sfx_costume_set_complete", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 2" },
            { "Growth/sfx_collection_register", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Chest Close 2" },
            { "Growth/sfx_collection_milestone", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 1" },
            { "Growth/sfx_title_unlock", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 2" },
            { "Growth/sfx_inscription_change", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Ice Freeze 1" },

            // ── 기타 ──
            { "Misc/sfx_combo_hit", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Attacks/Sword Attacks Hits and Blocks/Sword Impact Hit 1" },
            { "Misc/sfx_offline_reward", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Chest Open 2" },
            { "Misc/sfx_daily_allclear", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 1" },
            { "Misc/sfx_guide_quest", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Door Open 1" },
            { "Misc/sfx_attendance", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Door Open 2" },
            { "Misc/sfx_shop_purchase", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Lock Unlock" },
            { "Misc/sfx_ad_reward", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Chest Open 1" },
            { "Misc/sfx_chest_open", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Chest Open 1" },
            { "Misc/sfx_door_open", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Doors Gates and Chests/Door Open 1" },
            { "Misc/sfx_stage_clear", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Firebuff 2" },
            { "Boss/sfx_boss_warning", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Spells/Rock Meteor Swarm 2" },
            { "Misc/sfx_torch_light", "Fantasy_200/Free Fantasy SFX Pack By TomMusic/OGG Files/SFX/Torch/Light Torch 1" },
        };

        // BGM: 출력 파일명 → 소스 상대 경로 (BGM_SOURCE_ROOT 기준)
        private static readonly Dictionary<string, string> BGM_MAPPING = new()
        {
            // 기본
            { "bgm_lobby", "Fantasy_RPG/PGS Fantasy RPG Music Pack/Town-Village Theme 1" },
            { "bgm_chapter_forest", "Fantasy_RPG/PGS Fantasy RPG Music Pack/Town-Village Theme 2" },
            { "bgm_chapter_cave", "Fantasy_RPG/PGS Fantasy RPG Music Pack/Dungeon-Exploration Music 1" },
            { "bgm_chapter_volcano", "Fantasy_RPG/PGS Fantasy RPG Music Pack/Battle Music 1" },
            { "bgm_chapter_sky", "Fantasy_RPG/PGS Fantasy RPG Music Pack/Event Music 1" },
            { "bgm_chapter_abyss", "Fantasy_RPG/PGS Fantasy RPG Music Pack/Event Music 3" },

            // 보스 8종 (Loops 버전 — 게임용 루프)
            { "bgm_boss", "Boss_Battle/Loops/Ogg/1. Abyssal Tyrant (Loop)" },
            { "bgm_boss_abyssal_tyrant", "Boss_Battle/Loops/Ogg/1. Abyssal Tyrant (Loop)" },
            { "bgm_boss_soulrend_sovereign", "Boss_Battle/Loops/Ogg/2. Soulrend Sovereign (Loop)" },
            { "bgm_boss_veil_of_forsaken", "Boss_Battle/Loops/Ogg/3. Veil of the Forsaken (Loop)" },
            { "bgm_boss_ruinlord_ascendant", "Boss_Battle/Loops/Ogg/4. Ruinlord Ascendant (Loop)" },
            { "bgm_boss_unseen_monarch", "Boss_Battle/Loops/Ogg/5. The Unseen Monarch (Loop)" },
            { "bgm_boss_dread_requiem", "Boss_Battle/Loops/Ogg/6. Dread Requiem (Loop)" },
            { "bgm_boss_twilight_harbinger", "Boss_Battle/Loops/Ogg/7. Twilight of the Harbinger (Loop)" },
            { "bgm_boss_bloodbound_fight", "Boss_Battle/Loops/Ogg/8. Bloodbound Fight (Loop)" },

            // 던전/탑
            { "bgm_dungeon", "Feather_Falling/Free Fantasy Music Pack By TomMusic/OGG files/Full tracks/1 Exploration TomMusic" },
            { "bgm_dungeon_dark", "Dark_Dungeon/dark dungeon" },
            { "bgm_tower_elite", "JRPG_Battle/Legendary_JRPG_Battle_Music_Pack/02 Conflict/Assets_for_Unity/Battle-Conflict_loop" },
            { "bgm_tower_abyss", "JRPG_Battle/Legendary_JRPG_Battle_Music_Pack/03 Grief of Souls/Assets_for_Unity/Battle-Grief_loop" },
            { "bgm_tower_infinite", "JRPG_Battle/Legendary_JRPG_Battle_Music_Pack/07 Vampire Prayer/Assets_for_Unity/Battle-Vampire_loop" },

            // 전투 (JRPG 배틀 — 루프 버전)
            { "bgm_battle_furious", "JRPG_Battle/Legendary_JRPG_Battle_Music_Pack/01 Furious Battle/Assets_for_Unity/Battle-Furious-Gt_loop" },
            { "bgm_battle_conflict", "JRPG_Battle/Legendary_JRPG_Battle_Music_Pack/02 Conflict/Assets_for_Unity/Battle-Conflict_loop" },
            { "bgm_battle_grief", "JRPG_Battle/Legendary_JRPG_Battle_Music_Pack/03 Grief of Souls/Assets_for_Unity/Battle-Grief_loop" },
            { "bgm_battle_dawn", "JRPG_Battle/Legendary_JRPG_Battle_Music_Pack/04 Queen of the White Dawn/Assets_for_Unity/Battle-Dawn_loop" },
            { "bgm_battle_rapier", "JRPG_Battle/Legendary_JRPG_Battle_Music_Pack/05 The Girl with a Holy Rapier/Assets_for_Unity/Battle-rapier_loop" },
            { "bgm_battle_silvermoon", "JRPG_Battle/Legendary_JRPG_Battle_Music_Pack/06 The Sword of the Silver Moon/Assets_for_Unity/Battle-SilverMoon_loop" },
            { "bgm_battle_vampire", "JRPG_Battle/Legendary_JRPG_Battle_Music_Pack/07 Vampire Prayer/Assets_for_Unity/Battle-Vampire_loop" },
            { "bgm_battle_samurai", "JRPG_Battle/Legendary_JRPG_Battle_Music_Pack/08 SAMURAI BLADE/Assets_for_Unity/Battle-SAMURAI_loop" },

            // 탐험/평화
            { "bgm_exploration", "Feather_Falling/Free Fantasy Music Pack By TomMusic/OGG files/Loops/1 Exploration LOOP TomMusic" },
            { "bgm_journey", "Feather_Falling/Free Fantasy Music Pack By TomMusic/OGG files/Loops/2 Journey LOOP TomMusic" },
            { "bgm_magic_forest", "Feather_Falling/Free Fantasy Music Pack By TomMusic/OGG files/Loops/3 A Magic Forest LOOP TomMusic" },
            { "bgm_safe_space", "Feather_Falling/Free Fantasy Music Pack By TomMusic/OGG files/Loops/5 A Safe Space LOOP TomMusic" },

            // 이벤트/특수
            { "bgm_gacha", "Fantasy_RPG/PGS Fantasy RPG Music Pack/Event Music 2" },
            { "bgm_worldboss", "Boss_Battle/Tracks/Ogg/2. Soulrend Sovereign" },
            { "bgm_arena", "JRPG_Battle/Legendary_JRPG_Battle_Music_Pack/01 Furious Battle/Assets_for_Unity/Battle-Furious-Gt_loop" },
            { "bgm_season", "Fantasy_RPG/PGS Fantasy RPG Music Pack/Event Music 4" },
            { "bgm_guild_raid", "JRPG_Battle/Legendary_JRPG_Battle_Music_Pack/08 SAMURAI BLADE/Assets_for_Unity/Battle-SAMURAI_loop" },
            { "bgm_secret_room", "Feather_Falling/Free Fantasy Music Pack By TomMusic/OGG files/Loops/3 A Magic Forest LOOP TomMusic" },
            { "bgm_challenge", "JRPG_Battle/Legendary_JRPG_Battle_Music_Pack/04 Queen of the White Dawn/Assets_for_Unity/Battle-Dawn_loop" },

            // 타운/이벤트
            { "bgm_town_2", "Fantasy_RPG/PGS Fantasy RPG Music Pack/Town-Village Theme 2" },
            { "bgm_town_3", "Fantasy_RPG/PGS Fantasy RPG Music Pack/Town-Village Theme 3" },
            { "bgm_event_1", "Fantasy_RPG/PGS Fantasy RPG Music Pack/Event Music 1" },
            { "bgm_event_2", "Fantasy_RPG/PGS Fantasy RPG Music Pack/Event Music 2" },
            { "bgm_event_3", "Fantasy_RPG/PGS Fantasy RPG Music Pack/Event Music 3" },
            { "bgm_event_4", "Fantasy_RPG/PGS Fantasy RPG Music Pack/Event Music 4" },
        };

        // 지원하는 오디오 확장자 (우선순위 순)
        private static readonly string[] AUDIO_EXTENSIONS = { ".ogg", ".wav", ".mp3", ".m4a" };

        [MenuItem("mkLike/Audio/Import All")]
        public static void ImportAll()
        {
            int sfxCount = ImportSfx();
            int bgmCount = ImportBgm();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AudioAssetImporter] 완료 — SFX: {sfxCount}개, BGM: {bgmCount}개 복사됨");
        }

        public static int ImportSfx()
        {
            if (!AssetDatabase.IsValidFolder(SFX_SOURCE_ROOT))
            {
                Debug.LogError($"[AudioAssetImporter] SFX 소스 폴더 없음: {SFX_SOURCE_ROOT}");
                return 0;
            }

            int copied = 0;

            foreach (var kvp in SFX_MAPPING)
            {
                string outputRelative = kvp.Key;    // e.g. "Attack/sfx_sword_swing"
                string sourceRelative = kvp.Value;   // e.g. "RPG_Essentials/..."

                string outputPath = $"{SFX_OUTPUT}/{outputRelative}";
                string sourcePath = $"{SFX_SOURCE_ROOT}/{sourceRelative}";

                if (CopyAudioFile(sourcePath, outputPath))
                    copied++;
            }

            return copied;
        }

        public static int ImportBgm()
        {
            if (!AssetDatabase.IsValidFolder(BGM_SOURCE_ROOT))
            {
                Debug.LogError($"[AudioAssetImporter] BGM 소스 폴더 없음: {BGM_SOURCE_ROOT}");
                return 0;
            }

            int copied = 0;

            foreach (var kvp in BGM_MAPPING)
            {
                string outputName = kvp.Key;         // e.g. "bgm_lobby"
                string sourceRelative = kvp.Value;    // e.g. "Fantasy_RPG/..."

                string outputPath = $"{BGM_OUTPUT}/{outputName}";
                string sourcePath = $"{BGM_SOURCE_ROOT}/{sourceRelative}";

                if (CopyAudioFile(sourcePath, outputPath))
                    copied++;
            }

            return copied;
        }

        /// <summary>
        /// 소스 경로(확장자 없음)에서 오디오 파일을 찾아 출력 경로로 복사한다.
        /// 이미 존재하면 스킵. AudioImporter 설정도 적용한다.
        /// </summary>
        private static bool CopyAudioFile(string sourcePathNoExt, string outputPathNoExt)
        {
            // 출력 경로에 이미 파일이 있는지 확인
            for (int i = 0; i < AUDIO_EXTENSIONS.Length; i++)
            {
                string existingPath = outputPathNoExt + AUDIO_EXTENSIONS[i];
                if (File.Exists(Path.GetFullPath(existingPath)))
                {
                    return false; // 이미 존재
                }
            }

            // 소스에서 오디오 파일 찾기
            string foundSourcePath = null;
            string foundExt = null;

            for (int i = 0; i < AUDIO_EXTENSIONS.Length; i++)
            {
                string candidate = sourcePathNoExt + AUDIO_EXTENSIONS[i];
                if (File.Exists(Path.GetFullPath(candidate)))
                {
                    foundSourcePath = candidate;
                    foundExt = AUDIO_EXTENSIONS[i];
                    break;
                }
            }

            if (foundSourcePath == null)
            {
                Debug.LogWarning($"[AudioAssetImporter] 소스 파일 없음: {sourcePathNoExt}.*");
                return false;
            }

            // 출력 디렉토리 생성
            string outputDir = Path.GetDirectoryName(outputPathNoExt + foundExt).Replace('\\', '/');
            string unityOutputDir = outputDir;
            EnsureFolder(unityOutputDir);

            // 파일 복사
            string finalOutputPath = outputPathNoExt + foundExt;
            if (!AssetDatabase.CopyAsset(foundSourcePath, finalOutputPath))
            {
                // AssetDatabase.CopyAsset 실패 시 File.Copy 시도
                string fullSrc = Path.GetFullPath(foundSourcePath);
                string fullDst = Path.GetFullPath(finalOutputPath);
                string dstDir = Path.GetDirectoryName(fullDst);
                if (!Directory.Exists(dstDir))
                    Directory.CreateDirectory(dstDir);

                File.Copy(fullSrc, fullDst, false);
                AssetDatabase.ImportAsset(finalOutputPath, ImportAssetOptions.ForceUpdate);
            }

            // AudioImporter 설정
            ConfigureAudioImporter(finalOutputPath);

            Debug.Log($"[AudioAssetImporter] 복사: {foundSourcePath} → {finalOutputPath}");
            return true;
        }

        /// <summary>
        /// AudioImporter 설정: loadInBackground=true, forceToMono=false.
        /// </summary>
        private static void ConfigureAudioImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer == null) return;

            bool needsReimport = false;

            if (importer.forceToMono)
            {
                importer.forceToMono = false;
                needsReimport = true;
            }

            if (!importer.loadInBackground)
            {
                importer.loadInBackground = true;
                needsReimport = true;
            }

            if (needsReimport)
                importer.SaveAndReimport();
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string[] parts = folderPath.Split('/');
            string current = parts[0]; // "Assets"

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
