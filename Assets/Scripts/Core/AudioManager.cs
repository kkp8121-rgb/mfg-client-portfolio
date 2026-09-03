using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MkLike.Utils;

namespace MkLike.Core
{
    /// <summary>
    /// SFX 클립 엔트리. Inspector에서 키-클립 쌍을 직접 할당할 수 있다.
    /// Resources.Load 폴백보다 우선 사용된다.
    /// </summary>
    [Serializable]
    public struct SfxClipEntry
    {
        public string key;
        public AudioClip clip;
    }

    /// <summary>
    /// BGM 클립 엔트리. Inspector에서 키-클립-루프 설정을 직접 할당할 수 있다.
    /// </summary>
    [Serializable]
    public struct BgmClipEntry
    {
        public string key;
        public AudioClip clip;
        public bool loop;
    }

    /// <summary>
    /// 오디오 매니저. BGM 크로스페이드, SFX 풀링, 볼륨 제어를 담당한다.
    /// SettingsManager와 연동하여 저장된 볼륨을 반영한다.
    /// Inspector에서 직접 클립을 할당하거나, Resources.Load로 자동 로딩한다.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("BGM")]
        [SerializeField] private AudioSource _bgmSourceA;
        [SerializeField] private AudioSource _bgmSourceB;
        [SerializeField] private float _bgmVolume = 0.7f;
        [SerializeField] private BgmClipEntry[] _bgmClips;

        [Header("SFX")]
        [SerializeField] private int _sfxPoolSize = 10;
        [SerializeField] private float _sfxVolume = 1f;
        [SerializeField] private SfxClipEntry[] _sfxClips;

        [Header("UI SFX")]
        [SerializeField] private int _uiPoolSize = 3;
        [SerializeField] private float _uiVolume = 0.6f;

        private AudioSource[] _sfxPool;
        private int _sfxPoolIndex;
        private AudioSource[] _uiPool;
        private int _uiPoolIndex;

        private readonly Dictionary<string, AudioClip> _clipCache = new();
        private readonly Dictionary<string, AudioClip> _sfxOverrides = new();
        private readonly Dictionary<string, AudioClip> _bgmOverrides = new();
        private readonly Dictionary<string, bool> _bgmLoopOverrides = new();

        // 누락 클립 경고를 1회만 출력하기 위한 캐시
        private readonly HashSet<string> _missingClipWarned = new();

        private bool _isSourceA = true;
        private AudioSource CurrentBgmSource => _isSourceA ? _bgmSourceA : _bgmSourceB;
        private AudioSource NextBgmSource => _isSourceA ? _bgmSourceB : _bgmSourceA;

        private string _currentBgmName;
        private Tween _bgmTween;

        // SfxType → Resources 파일명 매핑
        private static readonly Dictionary<SfxType, string> SFX_FILE_MAP = new()
        {
            // 공격
            { SfxType.SwordSwing, "sfx_sword_swing" },
            { SfxType.BowRelease, "sfx_bow_release" },
            { SfxType.MagicCast, "sfx_magic_cast" },
            { SfxType.SwordHit, "sfx_sword_hit" },
            { SfxType.ArrowHit, "sfx_arrow_hit" },
            { SfxType.MagicHit, "sfx_magic_hit" },
            { SfxType.CritHit, "sfx_crit_hit" },
            // 몬스터
            { SfxType.MonsterHit, "sfx_monster_hit" },
            { SfxType.MonsterDie, "sfx_monster_die" },
            { SfxType.BossRoar, "sfx_boss_roar" },
            { SfxType.BossDie, "sfx_boss_die" },
            // 시스템
            { SfxType.LevelUp, "sfx_levelup" },
            { SfxType.JobAdvance, "sfx_job_advance" },
            { SfxType.EnhanceSuccess, "sfx_enhance_success" },
            { SfxType.EnhanceFail, "sfx_enhance_fail" },
            { SfxType.EnhanceDestroy, "sfx_enhance_destroy" },
            { SfxType.StarforceUp, "sfx_starforce_up" },
            { SfxType.PotentialChange, "sfx_potential_change" },
            { SfxType.GoldPickup, "sfx_gold_pickup" },
            { SfxType.ExpPickup, "sfx_exp_pickup" },
            { SfxType.ItemDrop, "sfx_item_drop" },
            { SfxType.Equip, "sfx_equip" },
            { SfxType.Unequip, "sfx_unequip" },
            // UI
            { SfxType.UiTap, "sfx_ui_tap" },
            { SfxType.UiBack, "sfx_ui_back" },
            { SfxType.UiTabSwitch, "sfx_ui_tab_switch" },
            { SfxType.UiPopupOpen, "sfx_ui_popup_open" },
            { SfxType.UiPopupClose, "sfx_ui_popup_close" },
            { SfxType.UiConfirm, "sfx_ui_confirm" },
            { SfxType.UiCancel, "sfx_ui_cancel" },
            { SfxType.UiError, "sfx_ui_error" },
            { SfxType.UiReward, "sfx_ui_reward" },
            // 가챠
            { SfxType.GachaSpin, "sfx_gacha_spin" },
            { SfxType.GachaStop, "sfx_gacha_stop" },
            { SfxType.GachaRevealNormal, "sfx_gacha_reveal_normal" },
            { SfxType.GachaRevealRare, "sfx_gacha_reveal_rare" },
            { SfxType.GachaRevealEpic, "sfx_gacha_reveal_epic" },
            { SfxType.GachaRevealUnique, "sfx_gacha_reveal_unique" },
            { SfxType.GachaRevealLegendary, "sfx_gacha_reveal_legendary" },
            { SfxType.GachaRevealMythic, "sfx_gacha_reveal_mythic" },
            { SfxType.GachaMultiResult, "sfx_gacha_multi_result" },
            // 스킬 (기본)
            { SfxType.SkillSlash, "sfx_skill_slash" },
            { SfxType.SkillCharge, "sfx_skill_charge" },
            { SfxType.SkillWarcry, "sfx_skill_warcry" },
            { SfxType.SkillBowMulti, "sfx_skill_bow_multi" },
            { SfxType.SkillBowStorm, "sfx_skill_bow_storm" },
            { SfxType.SkillFire, "sfx_skill_fire" },
            { SfxType.SkillIce, "sfx_skill_ice" },
            { SfxType.SkillLightning, "sfx_skill_lightning" },
            { SfxType.SkillArcane, "sfx_skill_arcane" },
            { SfxType.SkillMeteor, "sfx_skill_meteor" },
            { SfxType.SkillBuffActivate, "sfx_skill_buff_activate" },
            { SfxType.SkillAwakening, "sfx_skill_awakening" },
            // 스킬 (전사 계열)
            { SfxType.SkillWarriorStrike, "sfx_skill_warrior_strike" },
            { SfxType.SkillWarriorFury, "sfx_skill_warrior_fury" },
            { SfxType.SkillWarriorTraining, "sfx_skill_warrior_training" },
            { SfxType.SkillKnightJudgment, "sfx_skill_knight_judgment" },
            { SfxType.SkillKnightRally, "sfx_skill_knight_rally" },
            { SfxType.SkillKnightWeakness, "sfx_skill_knight_weakness" },
            { SfxType.SkillTitanAnnihilate, "sfx_skill_titan_annihilate" },
            { SfxType.SkillTitanCataclysm, "sfx_skill_titan_cataclysm" },
            { SfxType.SkillTitanImmortal, "sfx_skill_titan_immortal" },
            { SfxType.SkillTitanMight, "sfx_skill_titan_might" },
            { SfxType.SkillWarlordWhirlwind, "sfx_skill_warlord_whirlwind" },
            { SfxType.SkillWarlordEarthshatter, "sfx_skill_warlord_earthshatter" },
            { SfxType.SkillWarlordFrenzy, "sfx_skill_warlord_frenzy" },
            { SfxType.SkillWarlordBloodpact, "sfx_skill_warlord_bloodpact" },
            // 스킬 (궁수 계열)
            { SfxType.SkillArcherAimshot, "sfx_skill_archer_aimshot" },
            { SfxType.SkillArcherArrowrain, "sfx_skill_archer_arrowrain" },
            { SfxType.SkillArcherKeeneye, "sfx_skill_archer_keeneye" },
            { SfxType.SkillArcherRapidfire, "sfx_skill_archer_rapidfire" },
            { SfxType.SkillScoutPierceshot, "sfx_skill_scout_pierceshot" },
            { SfxType.SkillScoutStormshot, "sfx_skill_scout_stormshot" },
            { SfxType.SkillScoutWindblessing, "sfx_skill_scout_windblessing" },
            { SfxType.SkillScoutAgility, "sfx_skill_scout_agility" },
            { SfxType.SkillHawkeyeEagleeye, "sfx_skill_hawkeye_eagleeye" },
            { SfxType.SkillHawkeyeExtinction, "sfx_skill_hawkeye_extinction" },
            { SfxType.SkillHawkeyeHuntingtime, "sfx_skill_hawkeye_huntingtime" },
            { SfxType.SkillHawkeyeJudgment, "sfx_skill_hawkeye_judgment" },
            { SfxType.SkillWindwalkerGale, "sfx_skill_windwalker_gale" },
            { SfxType.SkillWindwalkerTyphon, "sfx_skill_windwalker_typhon" },
            { SfxType.SkillWindwalkerAfterimage, "sfx_skill_windwalker_afterimage" },
            { SfxType.SkillWindwalkerWindpierce, "sfx_skill_windwalker_windpierce" },
            // 스킬 (마법사 계열)
            { SfxType.SkillMageMagicbolt, "sfx_skill_mage_magicbolt" },
            { SfxType.SkillMageFireburst, "sfx_skill_mage_fireburst" },
            { SfxType.SkillMageConcentration, "sfx_skill_mage_concentration" },
            { SfxType.SkillMageManacharge, "sfx_skill_mage_manacharge" },
            { SfxType.SkillSorcererFireball, "sfx_skill_sorcerer_fireball" },
            { SfxType.SkillSorcererChainlightning, "sfx_skill_sorcerer_chainlightning" },
            { SfxType.SkillSorcererFroststorm, "sfx_skill_sorcerer_froststorm" },
            { SfxType.SkillSorcererElemental, "sfx_skill_sorcerer_elemental" },
            { SfxType.SkillSageArcanemissile, "sfx_skill_sage_arcanemissile" },
            { SfxType.SkillSageMeteorshower, "sfx_skill_sage_meteorshower" },
            { SfxType.SkillSageDimensionrift, "sfx_skill_sage_dimensionrift" },
            { SfxType.SkillSageTimewarp, "sfx_skill_sage_timewarp" },
            { SfxType.SkillRunemasterRuneblast, "sfx_skill_runemaster_runeblast" },
            { SfxType.SkillRunemasterApocalypse, "sfx_skill_runemaster_apocalypse" },
            { SfxType.SkillRunemasterAbsolutedomain, "sfx_skill_runemaster_absolutedomain" },
            { SfxType.SkillRunemasterRunemastery, "sfx_skill_runemaster_runemastery" },
            // 스킬 (4차 전직)
            { SfxType.SkillDragonslayerDragonblade, "sfx_skill_dragonslayer_dragonblade" },
            { SfxType.SkillDragonslayerDragonrage, "sfx_skill_dragonslayer_dragonrage" },
            { SfxType.SkillDragonslayerApocalypse, "sfx_skill_dragonslayer_apocalypse" },
            { SfxType.SkillDragonslayerDragonblood, "sfx_skill_dragonslayer_dragonblood" },
            { SfxType.SkillDragonslayerDragonheart, "sfx_skill_dragonslayer_dragonheart" },
            { SfxType.SkillDragonslayerDragonscale, "sfx_skill_dragonslayer_dragonscale" },
            { SfxType.SkillDragonslayerDragonaura, "sfx_skill_dragonslayer_dragonaura" },
            { SfxType.SkillStormbringerTempest, "sfx_skill_stormbringer_tempest" },
            { SfxType.SkillStormbringerChainlightning, "sfx_skill_stormbringer_chainlightning" },
            { SfxType.SkillStormbringerThunderpierce, "sfx_skill_stormbringer_thunderpierce" },
            { SfxType.SkillStormbringerRagnarok, "sfx_skill_stormbringer_ragnarok" },
            { SfxType.SkillStormbringerStormmastery, "sfx_skill_stormbringer_stormmastery" },
            { SfxType.SkillStormbringerStormshield, "sfx_skill_stormbringer_stormshield" },
            { SfxType.SkillStormbringerWindwalking, "sfx_skill_stormbringer_windwalking" },
            { SfxType.SkillArchmageVoidbolt, "sfx_skill_archmage_voidbolt" },
            { SfxType.SkillArchmageBigbang, "sfx_skill_archmage_bigbang" },
            { SfxType.SkillArchmageCosmicfield, "sfx_skill_archmage_cosmicfield" },
            { SfxType.SkillArchmageTranscendence, "sfx_skill_archmage_transcendence" },
            { SfxType.SkillArchmageElementalmastery, "sfx_skill_archmage_elementalmastery" },
            { SfxType.SkillArchmageManashield, "sfx_skill_archmage_manashield" },
            { SfxType.SkillArchmageArcanemind, "sfx_skill_archmage_arcanemind" },
            // 배틀패스/시즌
            { SfxType.BattlePassLevelUp, "sfx_battlepass_levelup" },
            { SfxType.BattlePassRewardClaim, "sfx_reward" },
            { SfxType.SeasonStart, "sfx_season_start" },
            { SfxType.SeasonEnd, "sfx_season_end" },
            // 던전/탑
            { SfxType.DungeonEnter, "sfx_dungeon_enter" },
            { SfxType.DungeonClear, "sfx_dungeon_clear" },
            { SfxType.TowerFloorClear, "sfx_tower_floor_clear" },
            // 성장/컬렉션
            { SfxType.PrestigeExecute, "sfx_prestige" },
            { SfxType.MasteryUnlock, "sfx_mastery_unlock" },
            // CostumeObtain / CostumeSetComplete: 2026-04-20 Costume 제거 후 오디오 매핑도 삭제
            { SfxType.CollectionRegister, "sfx_collection_register" },
            { SfxType.CollectionMilestone, "sfx_collection_milestone" },
            { SfxType.TitleUnlock, "sfx_title_unlock" },
            { SfxType.InscriptionChange, "sfx_inscription_change" },
            // 기타
            { SfxType.ComboHit, "sfx_combo_hit" },
            { SfxType.OfflineRewardClaim, "sfx_offline_reward" },
            { SfxType.DailyChecklistAllClear, "sfx_daily_allclear" },
            { SfxType.GuideQuestComplete, "sfx_guide_quest" },
            { SfxType.AttendanceCheck, "sfx_attendance" },
            { SfxType.ShopPurchase, "sfx_shop_purchase" },
            { SfxType.AdRewardClaim, "sfx_ad_reward" },
            { SfxType.Teleport, "sfx_teleport" },
            { SfxType.ChestOpen, "sfx_chest_open" },
            { SfxType.DoorOpen, "sfx_door_open" },
            { SfxType.StageClear, "sfx_stage_clear" },
            { SfxType.BossWarning, "sfx_boss_warning" },
        };

        // BgmType → Resources 파일명 매핑
        private static readonly Dictionary<BgmType, string> BGM_FILE_MAP = new()
        {
            // 기본
            { BgmType.Lobby, "bgm_lobby" },
            { BgmType.ChapterForest, "bgm_chapter_forest" },
            { BgmType.ChapterCave, "bgm_chapter_cave" },
            { BgmType.ChapterVolcano, "bgm_chapter_volcano" },
            { BgmType.ChapterSky, "bgm_chapter_sky" },
            { BgmType.ChapterAbyss, "bgm_chapter_abyss" },
            // 보스 (8종)
            { BgmType.Boss, "bgm_boss" },
            { BgmType.BossAbyssalTyrant, "bgm_boss_abyssal_tyrant" },
            { BgmType.BossSoulrendSovereign, "bgm_boss_soulrend_sovereign" },
            { BgmType.BossVeilOfForsaken, "bgm_boss_veil_of_forsaken" },
            { BgmType.BossRuinlordAscendant, "bgm_boss_ruinlord_ascendant" },
            { BgmType.BossUnseenMonarch, "bgm_boss_unseen_monarch" },
            { BgmType.BossDreadRequiem, "bgm_boss_dread_requiem" },
            { BgmType.BossTwilightHarbinger, "bgm_boss_twilight_harbinger" },
            { BgmType.BossBloodboundFight, "bgm_boss_bloodbound_fight" },
            // 던전/탑
            { BgmType.Dungeon, "bgm_dungeon" },
            { BgmType.DungeonDark, "bgm_dungeon_dark" },
            { BgmType.TowerElite, "bgm_tower_elite" },
            { BgmType.TowerAbyss, "bgm_tower_abyss" },
            { BgmType.TowerInfinite, "bgm_tower_infinite" },
            // 전투 (JRPG)
            { BgmType.BattleFurious, "bgm_battle_furious" },
            { BgmType.BattleConflict, "bgm_battle_conflict" },
            { BgmType.BattleGrief, "bgm_battle_grief" },
            { BgmType.BattleDawn, "bgm_battle_dawn" },
            { BgmType.BattleRapier, "bgm_battle_rapier" },
            { BgmType.BattleSilverMoon, "bgm_battle_silvermoon" },
            { BgmType.BattleVampire, "bgm_battle_vampire" },
            { BgmType.BattleSamurai, "bgm_battle_samurai" },
            // 탐험/평화
            { BgmType.Exploration, "bgm_exploration" },
            { BgmType.Journey, "bgm_journey" },
            { BgmType.MagicForest, "bgm_magic_forest" },
            { BgmType.SafeSpace, "bgm_safe_space" },
            // 이벤트/특수
            { BgmType.Gacha, "bgm_gacha" },
            { BgmType.WorldBoss, "bgm_worldboss" },
            { BgmType.Season, "bgm_season" },
            // 타운/이벤트
            { BgmType.Town2, "bgm_town_2" },
            { BgmType.Town3, "bgm_town_3" },
            { BgmType.Event1, "bgm_event_1" },
            { BgmType.Event2, "bgm_event_2" },
            { BgmType.Event3, "bgm_event_3" },
            { BgmType.Event4, "bgm_event_4" },
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildOverrideMaps();
            InitBgmSources();
            InitSfxPool();
            InitUiPool();
        }

        private void Start()
        {
            LoadVolumeFromSettings();
            SubscribeSfxEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeSfxEvents();
            _bgmTween?.Kill();
            transform.DOKill();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ════════════════════════════════════════
        //  초기화
        // ════════════════════════════════════════

        private void BuildOverrideMaps()
        {
            if (_sfxClips != null)
            {
                for (int i = 0; i < _sfxClips.Length; i++)
                {
                    var entry = _sfxClips[i];
                    if (!string.IsNullOrEmpty(entry.key) && entry.clip != null)
                        _sfxOverrides[entry.key] = entry.clip;
                }
            }

            if (_bgmClips != null)
            {
                for (int i = 0; i < _bgmClips.Length; i++)
                {
                    var entry = _bgmClips[i];
                    if (!string.IsNullOrEmpty(entry.key) && entry.clip != null)
                    {
                        _bgmOverrides[entry.key] = entry.clip;
                        _bgmLoopOverrides[entry.key] = entry.loop;
                    }
                }
            }
        }

        private void InitBgmSources()
        {
            if (_bgmSourceA == null)
            {
                var goA = new GameObject("BGM_Source_A");
                goA.transform.SetParent(transform);
                _bgmSourceA = goA.AddComponent<AudioSource>();
            }

            if (_bgmSourceB == null)
            {
                var goB = new GameObject("BGM_Source_B");
                goB.transform.SetParent(transform);
                _bgmSourceB = goB.AddComponent<AudioSource>();
            }

            ConfigureBgmSource(_bgmSourceA);
            ConfigureBgmSource(_bgmSourceB);
        }

        private void ConfigureBgmSource(AudioSource source)
        {
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0f;
            source.priority = 0;
        }

        private void InitSfxPool()
        {
            _sfxPool = new AudioSource[_sfxPoolSize];
            for (int i = 0; i < _sfxPoolSize; i++)
            {
                var go = new GameObject($"SFX_Source_{i}");
                go.transform.SetParent(transform);
                _sfxPool[i] = go.AddComponent<AudioSource>();
                _sfxPool[i].playOnAwake = false;
                _sfxPool[i].loop = false;
                _sfxPool[i].priority = 128;
            }
        }

        private void InitUiPool()
        {
            _uiPool = new AudioSource[_uiPoolSize];
            for (int i = 0; i < _uiPoolSize; i++)
            {
                var go = new GameObject($"UI_SFX_Source_{i}");
                go.transform.SetParent(transform);
                _uiPool[i] = go.AddComponent<AudioSource>();
                _uiPool[i].playOnAwake = false;
                _uiPool[i].loop = false;
                _uiPool[i].priority = 64;
            }
        }

        private void LoadVolumeFromSettings()
        {
            if (SettingsManager.Instance == null) return;

            _bgmVolume = SettingsManager.Instance.BgmVolume;
            _sfxVolume = SettingsManager.Instance.SfxVolume;

            // 현재 재생 중인 BGM 볼륨 즉시 반영
            CurrentBgmSource.volume = _bgmVolume;
        }

        // ════════════════════════════════════════
        //  BGM
        // ════════════════════════════════════════

        /// <summary>
        /// BGM을 이름으로 재생한다. 이미 같은 BGM이 재생 중이면 무시.
        /// </summary>
        public void PlayBgm(string bgmName, float fadeDuration = 1.5f)
        {
            if (_currentBgmName == bgmName) return;

            var clip = LoadBgmClip(bgmName);
            if (clip == null)
            {
                if (_missingClipWarned.Add(bgmName))
                    Debug.LogWarning($"[AudioManager] BGM 클립을 찾을 수 없음: {bgmName}");
                return;
            }

            _currentBgmName = bgmName;
            CrossfadeBgmInternal(clip, fadeDuration);
        }

        /// <summary>
        /// BgmType으로 BGM을 재생한다.
        /// </summary>
        public void PlayBgm(BgmType bgmType, float fadeDuration = 1.5f)
        {
            if (BGM_FILE_MAP.TryGetValue(bgmType, out string fileName))
            {
                PlayBgm(fileName, fadeDuration);
            }
            else
            {
                Debug.LogWarning($"[AudioManager] BGM 매핑 없음: {bgmType}");
            }
        }

        /// <summary>
        /// 챕터 번호에 해당하는 BGM을 재생한다.
        /// </summary>
        public void PlayChapterBgm(int chapter)
        {
            var bgmType = GetChapterBgmType(chapter);
            PlayBgm(bgmType);
        }

        /// <summary>
        /// 크로스페이드 BGM 전환. DOTween 시퀀스 사용.
        /// </summary>
        public void CrossfadeBgm(AudioClip newClip, float duration = 1.5f)
        {
            if (newClip == null) return;
            CrossfadeBgmInternal(newClip, duration);
        }

        private void CrossfadeBgmInternal(AudioClip newClip, float duration)
        {
            _bgmTween?.Kill();

            var fadeOut = CurrentBgmSource;
            var fadeIn = NextBgmSource;

            fadeIn.clip = newClip;
            fadeIn.volume = 0f;
            fadeIn.Play();

            var seq = DOTween.Sequence();
            seq.Append(DOTween.To(() => fadeOut.volume, v => fadeOut.volume = v, 0f, duration * 0.5f));
            seq.Join(DOTween.To(() => fadeIn.volume, v => fadeIn.volume = v, _bgmVolume, duration));
            seq.AppendCallback(() =>
            {
                fadeOut.Stop();
                fadeOut.clip = null;
            });
            seq.SetLink(gameObject);
            _bgmTween = seq;

            _isSourceA = !_isSourceA;
        }

        /// <summary>
        /// 보스 BGM 전환. 페이드아웃 → 무음 긴장 → 보스 BGM 페이드인.
        /// </summary>
        public void BossBgmTransition(AudioClip bossClip)
        {
            if (bossClip == null) return;

            _bgmTween?.Kill();

            var fadeOut = CurrentBgmSource;
            var fadeIn = NextBgmSource;

            var seq = DOTween.Sequence();
            seq.Append(DOTween.To(() => fadeOut.volume, v => fadeOut.volume = v, 0f, 0.5f));
            seq.AppendCallback(() =>
            {
                fadeOut.Stop();
                fadeOut.clip = null;
            });
            seq.AppendInterval(1.0f);
            seq.AppendCallback(() =>
            {
                fadeIn.clip = bossClip;
                fadeIn.volume = 0f;
                fadeIn.Play();
            });
            seq.Append(DOTween.To(() => fadeIn.volume, v => fadeIn.volume = v, _bgmVolume, 0.5f));
            seq.SetLink(gameObject);
            _bgmTween = seq;

            _isSourceA = !_isSourceA;
            _currentBgmName = bossClip.name;
        }

        /// <summary>
        /// 보스 BGM 전환 (BgmType 사용).
        /// </summary>
        public void BossBgmTransition()
        {
            var clip = LoadBgmClip(BGM_FILE_MAP[BgmType.Boss]);
            if (clip != null)
            {
                BossBgmTransition(clip);
            }
        }

        /// <summary>
        /// BGM을 페이드아웃하며 정지한다.
        /// </summary>
        public void StopBgm(float fadeDuration = 0.5f)
        {
            _bgmTween?.Kill();
            _currentBgmName = null;

            var source = CurrentBgmSource;
            if (!source.isPlaying) return;

            _bgmTween = DOTween.To(() => source.volume, v => source.volume = v, 0f, fadeDuration)
                .OnComplete(() =>
                {
                    source.Stop();
                    source.clip = null;
                })
                .SetLink(gameObject);
        }

        /// <summary>
        /// BGM 볼륨을 설정한다. 재생 중인 BGM에 즉시 반영.
        /// </summary>
        public void SetBgmVolume(float volume)
        {
            _bgmVolume = Mathf.Clamp01(volume);
            CurrentBgmSource.volume = _bgmVolume;
        }

        /// <summary>
        /// 가챠 팝업 진입: 전투 BGM 볼륨을 30%로 덕킹.
        /// </summary>
        public void DuckBgm(float targetRatio = 0.3f, float duration = 0.5f)
        {
            _bgmTween?.Kill();
            _bgmTween = DOTween.To(
                () => CurrentBgmSource.volume,
                v => CurrentBgmSource.volume = v,
                _bgmVolume * targetRatio,
                duration
            ).SetLink(gameObject);
        }

        /// <summary>
        /// 가챠 팝업 종료: BGM 볼륨 복귀.
        /// </summary>
        public void UnduckBgm(float duration = 0.5f)
        {
            _bgmTween?.Kill();
            _bgmTween = DOTween.To(
                () => CurrentBgmSource.volume,
                v => CurrentBgmSource.volume = v,
                _bgmVolume,
                duration
            ).SetLink(gameObject);
        }

        // ════════════════════════════════════════
        //  SFX
        // ════════════════════════════════════════

        /// <summary>
        /// SFX를 이름(문자열)으로 재생한다.
        /// </summary>
        public void PlaySfx(string sfxName, float pitchVariation = 0f)
        {
            if (string.IsNullOrEmpty(sfxName)) return;

            var clip = LoadSfxClip(sfxName);
            if (clip == null)
            {
                if (_missingClipWarned.Add(sfxName))
                    Debug.LogWarning($"[AudioManager] SFX 클립을 찾을 수 없음: {sfxName}");
                return;
            }

            var source = GetNextSfxSource();
            source.clip = clip;
            source.volume = _sfxVolume;
            source.pitch = pitchVariation > 0f
                ? 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation)
                : 1f;
            source.spatialBlend = 0f;
            source.Play();
        }

        /// <summary>
        /// SfxType으로 SFX를 재생한다.
        /// </summary>
        public void PlaySfx(SfxType sfxType, float pitchVariation = 0f)
        {
            if (SFX_FILE_MAP.TryGetValue(sfxType, out string fileName))
            {
                PlaySfx(fileName, pitchVariation);
            }
        }

        /// <summary>
        /// 3D 위치에서 SFX를 재생한다 (공간감 적용).
        /// </summary>
        public void PlaySfxAtPosition(string sfxName, Vector3 position, float pitchVariation = 0f)
        {
            if (string.IsNullOrEmpty(sfxName)) return;

            var clip = LoadSfxClip(sfxName);
            if (clip == null)
            {
                if (_missingClipWarned.Add(sfxName))
                    Debug.LogWarning($"[AudioManager] SFX 클립을 찾을 수 없음: {sfxName}");
                return;
            }

            var source = GetNextSfxSource();
            source.transform.position = position;
            source.clip = clip;
            source.volume = _sfxVolume;
            source.pitch = pitchVariation > 0f
                ? 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation)
                : 1f;
            source.spatialBlend = 0.7f;
            source.Play();
        }

        /// <summary>
        /// SfxType + 3D 위치로 SFX를 재생한다.
        /// </summary>
        public void PlaySfxAtPosition(SfxType sfxType, Vector3 position, float pitchVariation = 0f)
        {
            if (SFX_FILE_MAP.TryGetValue(sfxType, out string fileName))
            {
                PlaySfxAtPosition(fileName, position, pitchVariation);
            }
        }

        /// <summary>
        /// UI 전용 SFX를 재생한다. 별도 풀 사용.
        /// </summary>
        public void PlayUiSfx(string sfxName)
        {
            if (string.IsNullOrEmpty(sfxName)) return;

            var clip = LoadSfxClip(sfxName);
            if (clip == null)
            {
                if (_missingClipWarned.Add(sfxName))
                    Debug.LogWarning($"[AudioManager] UI SFX 클립을 찾을 수 없음: {sfxName}");
                return;
            }

            var source = GetNextUiSource();
            source.clip = clip;
            source.volume = _uiVolume;
            source.pitch = 1f;
            source.spatialBlend = 0f;
            source.Play();
        }

        /// <summary>
        /// SfxType으로 UI SFX를 재생한다.
        /// </summary>
        public void PlayUiSfx(SfxType sfxType)
        {
            if (SFX_FILE_MAP.TryGetValue(sfxType, out string fileName))
            {
                PlayUiSfx(fileName);
            }
        }

        /// <summary>
        /// SFX 볼륨을 설정한다.
        /// </summary>
        public void SetSfxVolume(float volume)
        {
            _sfxVolume = Mathf.Clamp01(volume);
        }

        /// <summary>
        /// UI SFX 볼륨을 설정한다.
        /// </summary>
        public void SetUiVolume(float volume)
        {
            _uiVolume = Mathf.Clamp01(volume);
        }

        // ════════════════════════════════════════
        //  챕터 BGM 매핑
        // ════════════════════════════════════════

        /// <summary>
        /// 챕터 번호에 해당하는 BgmType을 반환한다.
        /// 1-3: 숲, 4-6: 동굴, 7-9: 화산, 10-12: 하늘, 13+: 심연
        /// </summary>
        public BgmType GetChapterBgmType(int chapter)
        {
            return chapter switch
            {
                <= 3 => BgmType.ChapterForest,
                <= 6 => BgmType.ChapterCave,
                <= 9 => BgmType.ChapterVolcano,
                <= 12 => BgmType.ChapterSky,
                _ => BgmType.ChapterAbyss
            };
        }

        /// <summary>
        /// 챕터 번호에 해당하는 BGM 파일명을 반환한다.
        /// </summary>
        public string GetChapterBgm(int chapter)
        {
            var bgmType = GetChapterBgmType(chapter);
            return BGM_FILE_MAP.TryGetValue(bgmType, out string fileName) ? fileName : "bgm_chapter_forest";
        }

        // ════════════════════════════════════════
        //  내부: 클립 로딩 + 풀 관리
        // ════════════════════════════════════════

        private AudioClip LoadSfxClip(string name)
        {
            if (_clipCache.TryGetValue(name, out var cached))
                return cached;

            // Inspector 오버라이드 우선
            if (_sfxOverrides.TryGetValue(name, out var overrideClip))
            {
                _clipCache[name] = overrideClip;
                return overrideClip;
            }

            // Audio/SFX/ 하위 폴더에서 검색
            var clip = Resources.Load<AudioClip>($"Audio/SFX/{name}");

            // 서브폴더 검색
            if (clip == null)
            {
                string[] subFolders = {
                    "Attack", "Skill", "Monster", "System", "Gacha", "UI",
                    "Dungeon", "Guild", "Growth", "Misc"
                };
                for (int i = 0; i < subFolders.Length; i++)
                {
                    clip = Resources.Load<AudioClip>($"Audio/SFX/{subFolders[i]}/{name}");
                    if (clip != null) break;
                }
            }

            if (clip != null)
                _clipCache[name] = clip;

            return clip;
        }

        private AudioClip LoadBgmClip(string name)
        {
            if (_clipCache.TryGetValue(name, out var cached))
                return cached;

            // Inspector 오버라이드 우선
            if (_bgmOverrides.TryGetValue(name, out var overrideClip))
            {
                _clipCache[name] = overrideClip;
                return overrideClip;
            }

            var clip = Resources.Load<AudioClip>($"Audio/BGM/{name}");
            if (clip != null)
                _clipCache[name] = clip;

            return clip;
        }

        private AudioSource GetNextSfxSource()
        {
            var source = _sfxPool[_sfxPoolIndex];
            _sfxPoolIndex = (_sfxPoolIndex + 1) % _sfxPool.Length;
            return source;
        }

        private AudioSource GetNextUiSource()
        {
            var source = _uiPool[_uiPoolIndex];
            _uiPoolIndex = (_uiPoolIndex + 1) % _uiPool.Length;
            return source;
        }

        // ════════════════════════════════════════
        //  SFX 이벤트 자동 연동
        // ════════════════════════════════════════

        private void SubscribeSfxEvents()
        {
            EventBus<MonsterDiedEvent>.Subscribe(OnMonsterDied);
            EventBus<LevelUpEvent>.Subscribe(OnLevelUp);
            EventBus<EquipmentChangedEvent>.Subscribe(OnEquipmentChanged);
            EventBus<GachaResultEvent>.Subscribe(OnGachaResult);
            EventBus<GoldGainedEvent>.Subscribe(OnGoldGained);
            EventBus<SkillUsedEvent>.Subscribe(OnSkillUsed);
            EventBus<JobChangedEvent>.Subscribe(OnJobChanged);
            EventBus<QuestCompletedEvent>.Subscribe(OnQuestCompleted);
            EventBus<StageChangedEvent>.Subscribe(OnStageChanged);
            EventBus<FarmingModeEvent>.Subscribe(OnFarmingMode);
            EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
            EventBus<BattlePassLevelUpEvent>.Subscribe(OnBattlePassLevelUp);
            EventBus<BattlePassRewardClaimedEvent>.Subscribe(OnBattlePassRewardClaimed);
            EventBus<SeasonStartedEvent>.Subscribe(OnSeasonStarted);
            EventBus<SeasonEndedEvent>.Subscribe(OnSeasonEnded);
            // 던전
            EventBus<DungeonCompletedEvent>.Subscribe(OnDungeonCompleted);
            // 성장/컬렉션
            EventBus<PrestigeExecutedEvent>.Subscribe(OnPrestigeExecuted);
            EventBus<MasteryUnlockedEvent>.Subscribe(OnMasteryUnlocked);
            EventBus<CollectionEntryRegisteredEvent>.Subscribe(OnCollectionRegistered);
            EventBus<CollectionMilestoneClaimedEvent>.Subscribe(OnCollectionMilestone);
            EventBus<TitleUnlockedEvent>.Subscribe(OnTitleUnlocked);
            // InscriptionChangedEvent: 2026-04-20 Inscription 시스템 제거
            // 기타
            EventBus<ComboEvent>.Subscribe(OnCombo);
            EventBus<OfflineRewardClaimedEvent>.Subscribe(OnOfflineReward);
            // DailyChecklistAllClearEvent: 2026-04-20 DailyChecklist 시스템 제거
            EventBus<GuideQuestCompletedEvent>.Subscribe(OnGuideQuestCompleted);
            EventBus<AttendanceCheckedEvent>.Subscribe(OnAttendanceChecked);
            EventBus<ShopPurchaseEvent>.Subscribe(OnShopPurchase);
            // AdRewardEvent: 2026-04-20 AdRewardManager 시스템 제거
        }

        private void UnsubscribeSfxEvents()
        {
            EventBus<MonsterDiedEvent>.Unsubscribe(OnMonsterDied);
            EventBus<LevelUpEvent>.Unsubscribe(OnLevelUp);
            EventBus<EquipmentChangedEvent>.Unsubscribe(OnEquipmentChanged);
            EventBus<GachaResultEvent>.Unsubscribe(OnGachaResult);
            EventBus<GoldGainedEvent>.Unsubscribe(OnGoldGained);
            EventBus<SkillUsedEvent>.Unsubscribe(OnSkillUsed);
            EventBus<JobChangedEvent>.Unsubscribe(OnJobChanged);
            EventBus<QuestCompletedEvent>.Unsubscribe(OnQuestCompleted);
            EventBus<StageChangedEvent>.Unsubscribe(OnStageChanged);
            EventBus<FarmingModeEvent>.Unsubscribe(OnFarmingMode);
            EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);
            EventBus<BattlePassLevelUpEvent>.Unsubscribe(OnBattlePassLevelUp);
            EventBus<BattlePassRewardClaimedEvent>.Unsubscribe(OnBattlePassRewardClaimed);
            EventBus<SeasonStartedEvent>.Unsubscribe(OnSeasonStarted);
            EventBus<SeasonEndedEvent>.Unsubscribe(OnSeasonEnded);
            // 던전
            EventBus<DungeonCompletedEvent>.Unsubscribe(OnDungeonCompleted);
            // 성장/컬렉션
            EventBus<PrestigeExecutedEvent>.Unsubscribe(OnPrestigeExecuted);
            EventBus<MasteryUnlockedEvent>.Unsubscribe(OnMasteryUnlocked);
            EventBus<CollectionEntryRegisteredEvent>.Unsubscribe(OnCollectionRegistered);
            EventBus<CollectionMilestoneClaimedEvent>.Unsubscribe(OnCollectionMilestone);
            EventBus<TitleUnlockedEvent>.Unsubscribe(OnTitleUnlocked);
            // InscriptionChangedEvent: 2026-04-20 Inscription 시스템 제거
            // 기타
            EventBus<ComboEvent>.Unsubscribe(OnCombo);
            EventBus<OfflineRewardClaimedEvent>.Unsubscribe(OnOfflineReward);
            // DailyChecklistAllClearEvent: 2026-04-20 DailyChecklist 시스템 제거
            EventBus<GuideQuestCompletedEvent>.Unsubscribe(OnGuideQuestCompleted);
            EventBus<AttendanceCheckedEvent>.Unsubscribe(OnAttendanceChecked);
            EventBus<ShopPurchaseEvent>.Unsubscribe(OnShopPurchase);
            // AdRewardEvent: 2026-04-20 AdRewardManager 시스템 제거
        }

        // ── 기존 이벤트 핸들러 ──
        private void OnMonsterDied(MonsterDiedEvent e) => PlaySfx("sfx_monster_die_01");
        private void OnLevelUp(LevelUpEvent e) => PlaySfx("sfx_levelup");
        private void OnEquipmentChanged(EquipmentChangedEvent e) => PlaySfx(e.IsEquipped ? "sfx_equip" : "sfx_unequip");
        private void OnGachaResult(GachaResultEvent e) => PlaySfx(GetGradeRevealSfx(e.Grade));
        private void OnGoldGained(GoldGainedEvent e) => PlaySfx("sfx_gold_pickup");
        private void OnJobChanged(JobChangedEvent e) => PlaySfx("sfx_job_advance");
        private void OnQuestCompleted(QuestCompletedEvent e) => PlaySfx("sfx_ui_reward");
        private void OnBattlePassLevelUp(BattlePassLevelUpEvent e) => PlaySfx(SfxType.BattlePassLevelUp);
        private void OnBattlePassRewardClaimed(BattlePassRewardClaimedEvent e) => PlaySfx(SfxType.BattlePassRewardClaim);
        private void OnSeasonStarted(SeasonStartedEvent e) => PlaySfx(SfxType.SeasonStart);
        private void OnSeasonEnded(SeasonEndedEvent e) => PlaySfx(SfxType.SeasonEnd);

        // ── 던전/탑 ──
        private void OnDungeonCompleted(DungeonCompletedEvent e) => PlaySfx(SfxType.DungeonClear);

        // ── 성장/컬렉션 ──
        private void OnPrestigeExecuted(PrestigeExecutedEvent e) => PlaySfx(SfxType.PrestigeExecute);
        private void OnMasteryUnlocked(MasteryUnlockedEvent e) => PlaySfx(SfxType.MasteryUnlock);
        private void OnCollectionRegistered(CollectionEntryRegisteredEvent e) => PlaySfx(SfxType.CollectionRegister);
        private void OnCollectionMilestone(CollectionMilestoneClaimedEvent e) => PlaySfx(SfxType.CollectionMilestone);
        private void OnTitleUnlocked(TitleUnlockedEvent e) => PlaySfx(SfxType.TitleUnlock);
        // OnInscriptionChanged: 2026-04-20 Inscription 시스템 제거

        // ── 기타 ──
        private void OnCombo(ComboEvent e)
        {
            if (e.ComboCount > 0 && e.ComboCount % 10 == 0)
                PlaySfx(SfxType.ComboHit);
        }
        private void OnOfflineReward(OfflineRewardClaimedEvent e) => PlaySfx(SfxType.OfflineRewardClaim);
        // OnDailyAllClear: 2026-04-20 DailyChecklist 시스템 제거
        private void OnGuideQuestCompleted(GuideQuestCompletedEvent e) => PlaySfx(SfxType.GuideQuestComplete);
        private void OnAttendanceChecked(AttendanceCheckedEvent e) => PlaySfx(SfxType.AttendanceCheck);
        private void OnShopPurchase(ShopPurchaseEvent e) => PlaySfx(SfxType.ShopPurchase);
        // OnAdReward: 2026-04-20 AdRewardManager 시스템 제거

        /// <summary>
        /// 스킬 사용 시 직업+스킬명에 맞는 SFX를 재생한다.
        /// 매핑에 없으면 기본 sfx_skill_buff_activate로 폴백.
        /// </summary>
        private void OnSkillUsed(SkillUsedEvent e)
        {
            string skillKey = e.SkillId?.ToLower();
            if (string.IsNullOrEmpty(skillKey))
            {
                PlaySfx(SfxType.SkillBuffActivate);
                return;
            }

            string sfxName = $"sfx_skill_{skillKey}";
            var clip = LoadSfxClip(sfxName);
            if (clip != null)
            {
                var source = GetNextSfxSource();
                source.clip = clip;
                source.volume = _sfxVolume;
                source.pitch = 1f;
                source.spatialBlend = 0f;
                source.Play();
            }
            else
            {
                PlaySfx(SfxType.SkillBuffActivate);
            }
        }

        // ════════════════════════════════════════
        //  BGM 자동 전환 (스테이지/파밍)
        // ════════════════════════════════════════

        private void OnStageChanged(StageChangedEvent e)
        {
            // 보스 스테이지 진입 — 챕터별 다른 보스 BGM
            if (e.DisplayName != null && e.DisplayName.Contains("BOSS"))
            {
                var bossBgm = GetChapterBossBgm(e.Chapter);
                string bossBgmName = BGM_FILE_MAP.TryGetValue(bossBgm, out string name) ? name : "bgm_boss";
                if (_currentBgmName != bossBgmName)
                {
                    _currentBgmName = bossBgmName;
                    var clip = LoadBgmClip(bossBgmName);
                    if (clip != null)
                        BossBgmTransition(clip);
                    else
                        BossBgmTransition();
                }
                return;
            }

            // 일반 스테이지 — 챕터별 BGM
            string bgmName = GetChapterBgm(e.Chapter);
            if (bgmName != _currentBgmName)
            {
                PlayBgm(bgmName);
            }
        }

        /// <summary>
        /// 챕터별 보스 BGM을 반환한다. 8종 보스곡을 챕터에 순환 배정.
        /// </summary>
        private BgmType GetChapterBossBgm(int chapter)
        {
            return ((chapter - 1) % 8) switch
            {
                0 => BgmType.BossAbyssalTyrant,
                1 => BgmType.BossSoulrendSovereign,
                2 => BgmType.BossVeilOfForsaken,
                3 => BgmType.BossRuinlordAscendant,
                4 => BgmType.BossUnseenMonarch,
                5 => BgmType.BossDreadRequiem,
                6 => BgmType.BossTwilightHarbinger,
                7 => BgmType.BossBloodboundFight,
                _ => BgmType.Boss
            };
        }

        private void OnFarmingMode(FarmingModeEvent e)
        {
            // 파밍 모드에서는 현재 BGM 유지 (전환 없음)
        }

        /// <summary>
        /// 게임 상태 변경 시 자동 BGM 전환.
        /// Lobby → bgm_lobby, Dungeon → bgm_dungeon, Gacha → bgm_gacha 등.
        /// </summary>
        private void OnGameStateChanged(GameStateChangedEvent e)
        {
            string bgmKey = e.CurrentState?.ToLower() switch
            {
                "lobby" or "town" or "idle" => "bgm_lobby",
                "dungeon" => "bgm_dungeon",
                "dungeon_dark" => "bgm_dungeon_dark",
                "gacha" => "bgm_gacha",
                "worldboss" => "bgm_worldboss",
                "boss" => "bgm_boss",
                "season" or "battlepass" => "bgm_season",
                "challenge" => "bgm_challenge",
                "secret_room" or "secretroom" => "bgm_secret_room",
                "exploration" => "bgm_exploration",
                _ => null
            };

            if (bgmKey != null && bgmKey != _currentBgmName)
            {
                PlayBgm(bgmKey);
            }
        }

        private string GetGradeRevealSfx(string grade)
        {
            return grade?.ToLower() switch
            {
                "mythic" => "sfx_gacha_reveal_mythic",
                "legendary" => "sfx_gacha_reveal_legendary",
                "unique" => "sfx_gacha_reveal_unique",
                "epic" => "sfx_gacha_reveal_epic",
                "rare" => "sfx_gacha_reveal_rare",
                _ => "sfx_gacha_reveal_normal"
            };
        }
    }
}
