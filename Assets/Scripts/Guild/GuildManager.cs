using System;
using System.Globalization;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Core.Net;
using MkLike.Core.Save;
using MkLike.Data;
using MkLike.Economy;
using MkLike.Combat;
using MkLike.Utils;

namespace MkLike.Guild
{
    /// <summary>
    /// 길드 매니저.
    /// 길드 생성/가입, 기부, 레벨, 버프, 보스전 관리.
    /// 해금: 스테이지 150 클리어.
    /// 로컬 저장 전용 (서버 연동 없음).
    /// </summary>
    public class GuildManager : MonoBehaviour
    {
        public static GuildManager Instance { get; private set; }

        [Header("길드 설정")]
        [SerializeField] private GuildConfigSO _config;

        // ── 런타임 상태 ──
        private string _guildName = "";
        private int _guildLevel = 1;
        private int _guildExp;
        private bool _isJoined;

        // 기부 카운터
        private int _goldDonateCount;
        private int _rubyDonateCount;
        private int _ticketDonateCount;
        private string _lastDonateResetDate = "";

        // 보스
        private int _bossAttemptsThisWeek;
        private string _lastBossResetDate = "";
        private long _bossTotalDamage;
        private bool _isBossDefeated;

        // 토벌전 (Phase 14)
        private bool _raidUsedThisWeek;

        /// <summary>시스템 해금 여부</summary>
        public bool IsUnlocked { get; private set; }

        /// <summary>길드 가입 여부</summary>
        public bool IsJoined => _isJoined;

        /// <summary>길드 이름</summary>
        public string GuildName => _guildName;

        /// <summary>길드 레벨</summary>
        public int GuildLevel => _guildLevel;

        /// <summary>길드 경험치</summary>
        public int GuildExp => _guildExp;

        /// <summary>다음 레벨까지 필요 경험치</summary>
        public int NextLevelExp
        {
            get
            {
                if (_config == null || _config.levels == null) return 0;
                int nextIdx = _guildLevel; // 0-based levels array, guildLevel starts at 1
                if (nextIdx >= _config.levels.Length) return 0; // max level
                return _config.levels[nextIdx].requiredExp;
            }
        }

        /// <summary>길드 보스 HP</summary>
        public long BossMaxHp => _config != null ? _guildLevel * _config.bossHpPerLevel : 1000000;

        /// <summary>보스 누적 데미지</summary>
        public long BossTotalDamage => _bossTotalDamage;

        /// <summary>보스 처치 여부</summary>
        public bool IsBossDefeated => _isBossDefeated;

        /// <summary>이번 주 남은 보스 도전 횟수</summary>
        public int RemainingBossAttempts => _config != null
            ? Mathf.Max(0, _config.weeklyBossAttempts - _bossAttemptsThisWeek)
            : 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            EventBus<BeforeSaveEvent>.SubscribeSticky(OnBeforeSave);
            EventBus<LoadCompletedEvent>.SubscribeSticky(OnLoadCompleted);
        }

        private void OnDisable()
        {
            EventBus<BeforeSaveEvent>.Unsubscribe(OnBeforeSave);
            EventBus<LoadCompletedEvent>.Unsubscribe(OnLoadCompleted);
        }

        private void OnBeforeSave(BeforeSaveEvent evt) => SaveToSaveData();

        private void OnLoadCompleted(LoadCompletedEvent evt)
        {
            LoadSaveData();
            CheckUnlock();
            ApplyGuildBuffs();
        }

        private void Start()
        {
            CheckUnlock();
            LoadSaveData();
            CheckDonateReset();
            CheckBossReset();
            ApplyGuildBuffs();
        }

        // ══════════════════════════════════════
        // 길드 생성/가입
        // ══════════════════════════════════════

        /// <summary>길드 생성 (루비 소비)</summary>
        public bool CreateGuild(string guildName)
        {
            if (_isJoined)
            {
                Debug.LogWarning("[GuildManager] 이미 길드에 가입되어 있습니다");
                return false;
            }

            if (string.IsNullOrEmpty(guildName))
            {
                Debug.LogWarning("[GuildManager] 길드 이름이 비어있습니다");
                return false;
            }

            int cost = _config != null ? _config.createCostRuby : 500;
            var cm = CurrencyManager.Instance;
            if (cm == null || cm.GetAmount(CurrencyType.Ruby) < cost)
            {
                Debug.LogWarning($"[GuildManager] 루비 부족 (필요: {cost})");
                return false;
            }

            cm.Add(CurrencyType.Ruby, -cost);
            _guildName = guildName;
            _guildLevel = 1;
            _guildExp = 0;
            _isJoined = true;

            SaveToSaveData();
            PublishJoinEvent();

            Debug.Log($"[GuildManager] 길드 생성 — '{guildName}' (루비 {cost} 소비)");
            return true;
        }

        /// <summary>길드 가입 (무료 — 로컬 시뮬레이션)</summary>
        public bool JoinGuild(string guildName)
        {
            if (_isJoined)
            {
                Debug.LogWarning("[GuildManager] 이미 길드에 가입되어 있습니다");
                return false;
            }

            _guildName = guildName;
            _guildLevel = 1;
            _guildExp = 0;
            _isJoined = true;

            SaveToSaveData();
            PublishJoinEvent();

            Debug.Log($"[GuildManager] 길드 가입 — '{guildName}'");
            return true;
        }

        private void PublishJoinEvent()
        {
            EventBus.Publish(new GuildJoinedEvent { GuildName = _guildName });

            // 2026-04-23 이슈 15 FeedbackBus: 길드 가입 Toast + 약한 쉐이크
            MkLike.Core.FeedbackBus.Emit(
                MkLike.Core.FeedbackKind.Generic,
                $"길드 '{_guildName}' 가입!",
                shakeIntensity: 1);
        }

        // ══════════════════════════════════════
        // 기부
        // ══════════════════════════════════════

        /// <summary>기부 가능 여부 확인</summary>
        public bool CanDonate(string donationType)
        {
            if (!_isJoined || _config == null) return false;

            var entry = FindDonation(donationType);
            if (entry == null) return false;

            int used = GetDonateCount(donationType);
            if (used >= entry.dailyLimit) return false;

            var cm = CurrencyManager.Instance;
            if (cm == null || cm.GetAmount(entry.currencyType) < entry.costAmount)
                return false;

            return true;
        }

        /// <summary>기부 실행</summary>
        public bool Donate(string donationType)
        {
            if (!CanDonate(donationType))
            {
                Debug.LogWarning($"[GuildManager] 기부 불가 — {donationType}");
                return false;
            }

            var entry = FindDonation(donationType);

            // 재화 소비
            CurrencyManager.Instance.Add(entry.currencyType, -entry.costAmount);

            // 기부 카운트 증가
            IncrementDonateCount(donationType);

            // 길드 경험치 증가
            AddGuildExp(entry.guildExpReward);

            SaveToSaveData();

            EventBus.Publish(new GuildDonatedEvent
            {
                DonationType = donationType,
                GuildExpGained = entry.guildExpReward
            });

            Debug.Log($"[GuildManager] 기부 — {entry.displayName} (EXP +{entry.guildExpReward})");
            return true;
        }

        /// <summary>기부 남은 횟수</summary>
        public int GetRemainingDonations(string donationType)
        {
            var entry = FindDonation(donationType);
            if (entry == null) return 0;
            return Mathf.Max(0, entry.dailyLimit - GetDonateCount(donationType));
        }

        private GuildDonationEntry FindDonation(string donationType)
        {
            if (_config == null || _config.donations == null) return null;
            for (int i = 0; i < _config.donations.Length; i++)
            {
                if (_config.donations[i].donationType == donationType)
                    return _config.donations[i];
            }
            return null;
        }

        private int GetDonateCount(string donationType)
        {
            return donationType switch
            {
                "gold" => _goldDonateCount,
                "ruby" => _rubyDonateCount,
                "ticket" => _ticketDonateCount,
                _ => 0
            };
        }

        private void IncrementDonateCount(string donationType)
        {
            switch (donationType)
            {
                case "gold": _goldDonateCount++; break;
                case "ruby": _rubyDonateCount++; break;
                case "ticket": _ticketDonateCount++; break;
            }
        }

        // ══════════════════════════════════════
        // 길드 레벨
        // ══════════════════════════════════════

        private void AddGuildExp(int amount)
        {
            _guildExp += amount;
            CheckLevelUp();
        }

        private void CheckLevelUp()
        {
            if (_config == null || _config.levels == null) return;

            while (_guildLevel < _config.levels.Length)
            {
                int nextIdx = _guildLevel; // levels[1] = level 2 requirements
                if (nextIdx >= _config.levels.Length) break;
                if (_guildExp < _config.levels[nextIdx].requiredExp) break;

                _guildLevel++;
                ApplyGuildBuffs();
                Debug.Log($"[GuildManager] 길드 레벨업! Lv.{_guildLevel}");
            }
        }

        // ══════════════════════════════════════
        // 길드 버프
        // ══════════════════════════════════════

        private void ApplyGuildBuffs()
        {
            if (!_isJoined || _config == null || _config.levels == null) return;

            // 기존 길드 버프 제거
            var player = GameObject.FindWithTag("Player");
            if (player == null) return;
            var stats = player.GetComponent<CombatStats>();
            if (stats == null) return;

            stats.ClearModifiers(ModifierSource.Guild);

            // 현재 레벨까지 누적 버프 적용
            for (int i = 1; i < _guildLevel && i < _config.levels.Length; i++)
            {
                var entry = _config.levels[i];
                if (string.IsNullOrEmpty(entry.buffType) || entry.buffValue <= 0) continue;

                ApplyBuff(stats, entry.buffType, entry.buffValue, i);
            }
        }

        private void ApplyBuff(CombatStats stats, string buffType, float value, int levelIdx)
        {
            string sourceId = $"guild_lv{levelIdx}";
            switch (buffType)
            {
                case "atk":
                    stats.AddModifier(sourceId, new StatModifier(ModifierSource.Guild, sourceId, StatType.Atk, 0, value));
                    break;
                case "exp":
                    // 경험치 버프는 LevelSystem에서 처리해야 하므로 여기서는 스킵
                    break;
                case "gold":
                    // 골드 버프는 CurrencyManager에서 처리해야 하므로 스킵
                    break;
                case "dropRate":
                    // 드롭률은 별도 시스템 필요 — 스킵
                    break;
                case "all":
                    stats.AddModifier(sourceId + "_atk", new StatModifier(ModifierSource.Guild, sourceId + "_atk", StatType.Atk, 0, value));
                    stats.AddModifier(sourceId + "_def", new StatModifier(ModifierSource.Guild, sourceId + "_def", StatType.Def, 0, value));
                    stats.AddModifier(sourceId + "_hp", new StatModifier(ModifierSource.Guild, sourceId + "_hp", StatType.MaxHp, 0, value));
                    break;
            }
        }

        // ══════════════════════════════════════
        // 길드 보스
        // ══════════════════════════════════════

        /// <summary>길드 보스 도전 가능 여부</summary>
        public bool CanChallengeBoss()
        {
            if (!_isJoined) return false;
            if (_isBossDefeated) return false;
            return RemainingBossAttempts > 0;
        }

        /// <summary>
        /// 길드 보스 전투 결과 처리.
        /// damageDealt: 이번 도전에서 입힌 데미지.
        /// </summary>
        public void ProcessBossResult(long damageDealt)
        {
            if (!_isJoined) return;

            _bossAttemptsThisWeek++;
            _bossTotalDamage += damageDealt;

            bool defeated = _bossTotalDamage >= BossMaxHp;
            if (defeated && !_isBossDefeated)
            {
                _isBossDefeated = true;
                GrantBossReward();
            }

            SaveToSaveData();

            EventBus.Publish(new GuildBossCompletedEvent
            {
                DamageDealt = damageDealt,
                IsBossDefeated = _isBossDefeated
            });

            // 2026-04-23 이슈 15 FeedbackBus: 길드 보스 공격 결과
            if (_isBossDefeated)
            {
                MkLike.Core.FeedbackBus.Emit(
                    MkLike.Core.FeedbackKind.Generic,
                    "길드 보스 처치! 보상 수령",
                    shakeIntensity: 3);
            }
            else
            {
                MkLike.Core.FeedbackBus.Emit(
                    MkLike.Core.FeedbackKind.Generic,
                    $"길드 보스 +{damageDealt:N0} 데미지");
            }

            Debug.Log($"[GuildManager] 보스 전투 — 데미지: {damageDealt:N0}, 누적: {_bossTotalDamage:N0}/{BossMaxHp:N0}");
        }

        /// <summary>CP 기반 보스 전투 시뮬레이션.</summary>
        public GuildBossBattleSimulator.RaidResult SimulateBossDetailed()
        {
            long playerCp = GetPlayerCp();
            return GuildBossBattleSimulator.Simulate(playerCp, BossMaxHp);
        }

        /// <summary>마지막 보스전 기여도 (0~1).</summary>
        public float LastContributionPercent { get; private set; }
        /// <summary>마지막 보스전 기여도 순위 (1~10).</summary>
        public int LastContributionRank { get; private set; }
        /// <summary>마지막 보스전 보상 배율.</summary>
        public float LastRewardMultiplier { get; private set; }

        private void GrantBossReward()
        {
            var cm = CurrencyManager.Instance;
            if (cm == null || _config == null) return;

            // Phase 14: 기여도 기반 보상 분배
            float contribution = CalculateContribution();
            int rank = SimulateContributionRank(contribution);
            float rewardMultiplier = GetRankRewardMultiplier(rank);

            LastContributionPercent = contribution;
            LastContributionRank = rank;
            LastRewardMultiplier = rewardMultiplier;

            int baseRuby = _config.bossDefeatRewardRuby;
            int baseGold = _config.bossDefeatRewardGold;

            int finalRuby = Mathf.RoundToInt(baseRuby * rewardMultiplier);
            int finalGold = Mathf.RoundToInt(baseGold * rewardMultiplier);

            cm.Add(CurrencyType.Ruby, finalRuby);
            cm.Add(CurrencyType.Gold, finalGold);

            Debug.Log($"[GuildManager] 보스 처치 보상 — 순위: {rank}위 (기여도 {contribution * 100:F1}%), 배율: x{rewardMultiplier:F1}, 루비 {finalRuby}, 골드 {finalGold}");
        }

        private float CalculateContribution()
        {
            if (BossMaxHp <= 0) return 0f;
            // 기여도 = 플레이어 데미지 / 보스 최대 HP (상한 40%)
            float raw = (float)_bossTotalDamage / BossMaxHp;
            return Mathf.Clamp(raw, 0f, 0.4f);
        }

        /// <summary>NPC 길드원 기여도를 시뮬레이션하여 순위 산출.</summary>
        private int SimulateContributionRank(float playerContribution)
        {
            // NPC 9명의 기여도 시뮬레이션
            int rank = 1;
            float remaining = 1f - playerContribution;
            for (int i = 0; i < 9; i++)
            {
                float npcContrib = remaining * UnityEngine.Random.Range(0.05f, 0.2f);
                if (npcContrib > playerContribution)
                    rank++;
            }
            return Mathf.Clamp(rank, 1, 10);
        }

        private static float GetRankRewardMultiplier(int rank)
        {
            if (rank == 1) return 1.5f;   // 1위 MVP
            if (rank <= 3) return 1.2f;    // 2~3위
            if (rank <= 10) return 1.0f;   // 4~10위
            return 0.8f;                    // 11위+
        }

        // ══════════════════════════════════════
        // 토벌전 (Phase 14)
        // ══════════════════════════════════════

        /// <summary>토벌전 도전 가능 여부 (주 1회).</summary>
        public bool CanRaid => _isJoined && !_raidUsedThisWeek;

        /// <summary>마지막 토벌전 결과.</summary>
        public RaidWaveResult LastRaidResult { get; private set; }

        /// <summary>
        /// 토벌전 시뮬레이션 (5웨이브, 180초).
        /// </summary>
        public RaidWaveResult SimulateRaid()
        {
            if (!CanRaid) return default;

            var player = GameObject.FindWithTag("Player");
            long playerCp = 0;
            if (player != null)
            {
                var stats = player.GetComponent<CombatStats>();
                if (stats != null) playerCp = stats.PowerScore;
            }

            int totalKills = 0;
            int wavesCleared = 0;
            float duration = 0f;
            long damageDealt = 0;

            // 5웨이브 시뮬레이션
            int[] waveMonsterCounts = { 20, 15, 20, 10, 5 }; // 일반, 엘리트, 일반, 보스급, 최종
            float[] waveDifficulty = { 1f, 1.5f, 1.2f, 2f, 3f };

            for (int w = 0; w < 5; w++)
            {
                if (duration >= 180f) break;

                int monsterCount = waveMonsterCounts[w];
                float difficulty = waveDifficulty[w];

                // CP 기반 처치 속도 계산
                float killRate = Mathf.Max(1f, playerCp / (100f * difficulty * _guildLevel));
                float timePerMonster = Mathf.Max(0.5f, 3f / killRate);

                int killed = 0;
                for (int m = 0; m < monsterCount; m++)
                {
                    duration += timePerMonster;
                    if (duration >= 180f) break;
                    killed++;
                    totalKills++;

                    long monsterHp = (long)(50 * difficulty * _guildLevel);
                    damageDealt += monsterHp;
                }

                if (killed >= monsterCount)
                    wavesCleared++;
            }

            _raidUsedThisWeek = true;

            // 보상: 웨이브 클리어 수 기반
            var cm = CurrencyManager.Instance;
            if (cm != null)
            {
                int goldReward = wavesCleared * 10000 * _guildLevel;
                int rubyReward = wavesCleared * 10;
                cm.Add(CurrencyType.Gold, goldReward);
                cm.Add(CurrencyType.Ruby, rubyReward);
                Debug.Log($"[GuildManager] 토벌전 완료 — {totalKills}킬, {wavesCleared}웨이브, 골드 +{goldReward}, 루비 +{rubyReward}");
            }

            // 길드 경험치
            AddGuildExp(wavesCleared * 200);

            SaveToSaveData();

            var result = new RaidWaveResult
            {
                TotalKills = totalKills,
                WavesCleared = wavesCleared,
                Duration = duration,
                DamageDealt = damageDealt
            };
            LastRaidResult = result;
            return result;
        }

        // ══════════════════════════════════════
        // 리셋
        // ══════════════════════════════════════

        private void CheckDonateReset()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (_lastDonateResetDate != today)
            {
                _goldDonateCount = 0;
                _rubyDonateCount = 0;
                _ticketDonateCount = 0;
                _lastDonateResetDate = today;
                Debug.Log("[GuildManager] 일일 기부 리셋");
            }
        }

        private void CheckBossReset()
        {
            // 매주 월요일 리셋
            var now = DateTime.Now;
            int daysToMonday = ((int)now.DayOfWeek - 1 + 7) % 7;
            var thisMonday = now.AddDays(-daysToMonday).Date;
            string mondayStr = thisMonday.ToString("yyyy-MM-dd");

            if (_lastBossResetDate != mondayStr)
            {
                _bossAttemptsThisWeek = 0;
                _bossTotalDamage = 0;
                _isBossDefeated = false;
                _raidUsedThisWeek = false;
                _lastBossResetDate = mondayStr;
                Debug.Log("[GuildManager] 주간 길드 보스/토벌전 리셋");
            }
        }

        // ══════════════════════════════════════
        // 해금
        // ══════════════════════════════════════

        private void CheckUnlock()
        {
            if (IsUnlocked) return;
            int currentFloor = GetCurrentFloor();
            int requiredFloor = _config != null ? _config.unlockFloor : 150;
            if (currentFloor >= requiredFloor)
            {
                IsUnlocked = true;
                Debug.Log("[GuildManager] 길드 시스템 해금!");
            }
        }

        private static int GetCurrentFloor()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
                return 1;
            return Mathf.Max(1, SaveManager.Instance.CurrentData.progress.currentFloor);
        }

        private static long GetPlayerCp()
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null) return 0;
            var stats = player.GetComponent<CombatStats>();
            return stats != null ? stats.PowerScore : 0;
        }

        // ══════════════════════════════════════
        // 저장/로드
        // ══════════════════════════════════════

        private void LoadSaveData()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
                return;

            var data = SaveManager.Instance.CurrentData.guild;
            if (data == null) return;

            _guildName = data.guildName ?? "";
            _guildLevel = Mathf.Max(1, data.guildLevel);
            _guildExp = data.guildExp;
            _isJoined = data.isJoined;
            _goldDonateCount = data.goldDonateCount;
            _rubyDonateCount = data.rubyDonateCount;
            _ticketDonateCount = data.ticketDonateCount;
            _lastDonateResetDate = data.lastDonateResetDate ?? "";
            _bossAttemptsThisWeek = data.bossAttemptsThisWeek;
            _lastBossResetDate = data.lastBossResetDate ?? "";
            _bossTotalDamage = data.bossTotalDamage;
            _isBossDefeated = data.isBossDefeated;
            _raidUsedThisWeek = data.raidUsedThisWeek;

            CheckDonateReset();
            CheckBossReset();

            Debug.Log($"[GuildManager] 로드 — '{_guildName}' Lv.{_guildLevel} EXP:{_guildExp} 가입:{_isJoined}");
        }

        private void SaveToSaveData()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
                return;

            if (SaveManager.Instance.CurrentData.guild == null)
                SaveManager.Instance.CurrentData.guild = new GuildSaveData();

            var data = SaveManager.Instance.CurrentData.guild;
            data.guildName = _guildName;
            data.guildLevel = _guildLevel;
            data.guildExp = _guildExp;
            data.isJoined = _isJoined;
            data.goldDonateCount = _goldDonateCount;
            data.rubyDonateCount = _rubyDonateCount;
            data.ticketDonateCount = _ticketDonateCount;
            data.lastDonateResetDate = _lastDonateResetDate;
            data.bossAttemptsThisWeek = _bossAttemptsThisWeek;
            data.lastBossResetDate = _lastBossResetDate;
            data.bossTotalDamage = _bossTotalDamage;
            data.isBossDefeated = _isBossDefeated;
            data.raidUsedThisWeek = _raidUsedThisWeek;
        }

        // ── 서버/로컬 자동 분기 ──

        /// <summary>서버 모드 여부</summary>
        public bool IsServerMode => ApiClient.HasAuth;

        /// <summary>길드 생성 (서버/로컬 자동 분기)</summary>
        public async UniTask<bool> CreateGuildAsync(string guildName, CancellationToken ct)
        {
            if (IsServerMode)
                return await CreateGuildServerAsync(guildName, ct) != null;
            return CreateGuild(guildName);
        }

        /// <summary>기부 (서버/로컬 자동 분기)</summary>
        public async UniTask<bool> DonateAsync(string donationType, CancellationToken ct)
        {
            if (IsServerMode)
                return await DonateServerAsync(donationType, ct) != null;
            return Donate(donationType);
        }

        // ── 서버 연동 (내부) ──

        /// <summary>서버에서 길드를 생성한다.</summary>
        public async UniTask<GuildInfoResponse> CreateGuildServerAsync(string guildName, CancellationToken ct)
        {
            var request = new GuildCreateRequest { guildName = guildName };
            var response = await ApiClient.PostAsync<GuildInfoResponse>("guild/create", request, ct);
            if (!response.success || response.data == null)
            {
                Debug.LogWarning($"[GuildManager] 서버 길드 생성 실패: {response.error}");
                return null;
            }
            _guildName = response.data.guildName;
            _guildLevel = response.data.level;
            _guildExp = response.data.exp;
            _isJoined = true;
            SaveToSaveData();
            EventBus.Publish(new GuildJoinedEvent { GuildName = _guildName });
            return response.data;
        }

        /// <summary>서버에서 길드에 가입한다.</summary>
        public async UniTask<GuildInfoResponse> JoinGuildServerAsync(long guildId, CancellationToken ct)
        {
            var request = new GuildJoinRequest { guildId = guildId };
            var response = await ApiClient.PostAsync<GuildInfoResponse>("guild/join", request, ct);
            if (!response.success || response.data == null)
            {
                Debug.LogWarning($"[GuildManager] 서버 길드 가입 실패: {response.error}");
                return null;
            }
            _guildName = response.data.guildName;
            _guildLevel = response.data.level;
            _guildExp = response.data.exp;
            _isJoined = true;
            SaveToSaveData();
            EventBus.Publish(new GuildJoinedEvent { GuildName = _guildName });
            return response.data;
        }

        /// <summary>서버에서 기부한다.</summary>
        public async UniTask<GuildDonateResponse> DonateServerAsync(string donationType, CancellationToken ct)
        {
            var request = new GuildDonateRequest { donationType = donationType };
            var response = await ApiClient.PostAsync<GuildDonateResponse>("guild/donate", request, ct);
            if (!response.success || response.data == null)
            {
                Debug.LogWarning($"[GuildManager] 서버 기부 실패: {response.error}");
                return null;
            }
            _guildExp += response.data.guildExpGained;
            SaveToSaveData();
            EventBus.Publish(new GuildDonatedEvent
            {
                DonationType = donationType,
                GuildExpGained = response.data.guildExpGained
            });
            return response.data;
        }

        /// <summary>서버에서 보스 전투 결과를 처리한다.</summary>
        public async UniTask<GuildBossResultResponse> ProcessBossServerAsync(long playerCp, CancellationToken ct)
        {
            var request = new GuildBossResultRequest { playerCp = playerCp };
            var response = await ApiClient.PostAsync<GuildBossResultResponse>("guild/boss-result", request, ct);
            if (!response.success || response.data == null)
            {
                Debug.LogWarning($"[GuildManager] 서버 보스 결과 실패: {response.error}");
                return null;
            }
            var result = response.data;
            _isBossDefeated = result.isBossDefeated;
            SaveToSaveData();
            EventBus.Publish(new GuildBossCompletedEvent
            {
                DamageDealt = result.damageDealt,
                IsBossDefeated = result.isBossDefeated
            });
            return result;
        }

        /// <summary>서버에서 길드 정보를 가져와 동기화한다.</summary>
        public async UniTask SyncInfoFromServerAsync(CancellationToken ct)
        {
            var response = await ApiClient.GetAsync<GuildInfoResponse>("guild/info", ct);
            if (!response.success || response.data == null) return;

            var info = response.data;
            if (info.guildId > 0)
            {
                _guildName = info.guildName;
                _guildLevel = info.level;
                _guildExp = info.exp;
                _isJoined = true;
                _isBossDefeated = info.isBossDefeated;
            }
            SaveToSaveData();
            Debug.Log($"[GuildManager] 서버 길드 정보 동기화 — {_guildName} Lv.{_guildLevel}");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }

    /// <summary>토벌전 결과.</summary>
    [System.Serializable]
    public struct RaidWaveResult
    {
        public int TotalKills;
        public int WavesCleared;
        public float Duration;
        public long DamageDealt;
    }
}
