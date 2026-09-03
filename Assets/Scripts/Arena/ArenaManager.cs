using System;
using System.Collections.Generic;
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

namespace MkLike.Arena
{
    /// <summary>
    /// PvP 아레나 관리자.
    /// 비동기 PvP: CP 비교 대전, 티어/레이팅, 시즌, 일일 무료 도전.
    /// 해금: 스테이지 100 클리어.
    /// </summary>
    public class ArenaManager : MonoBehaviour
    {
        public static ArenaManager Instance { get; private set; }

        [Header("아레나 설정")]
        [SerializeField] private ArenaConfigSO _config;
        [SerializeField] private ArenaRivalDataSO _rivalData;

        private const int MAX_RECORDS = 50;
        private const int ARENA_GOLD_PER_TIER = 5000;
        private const int ARENA_RUBY_PER_TIER = 5;
        private const int RIVAL_RATING_OFFSET = 175;

        // ── 런타임 상태 ──
        private int _currentTier;
        private int _rating;
        private int _totalVictories;
        private int _totalDefeats;
        private int _currentWinStreak;
        private int _bestWinStreak;
        private int _usedFreeEntries;
        private string _lastResetDate = "";
        private string _seasonId = "";
        private string _seasonStartTime = "";

        // ── 매칭 후보 ──
        private readonly List<ArenaOpponent> _currentCandidates = new();
        private readonly List<ArenaRecordEntry> _records = new();
        private ArenaRival[] _currentRivals; // 현재 매칭된 라이벌 원본 데이터

        /// <summary>시스템 해금 여부</summary>
        public bool IsUnlocked { get; private set; }

        /// <summary>현재 티어 인덱스 (0~3)</summary>
        public int CurrentTier => _currentTier;

        /// <summary>현재 레이팅</summary>
        public int Rating => _rating;

        /// <summary>총 승리</summary>
        public int TotalVictories => _totalVictories;

        /// <summary>총 패배</summary>
        public int TotalDefeats => _totalDefeats;

        /// <summary>현재 연승</summary>
        public int CurrentWinStreak => _currentWinStreak;

        /// <summary>최고 연승</summary>
        public int BestWinStreak => _bestWinStreak;

        /// <summary>남은 무료 도전 횟수</summary>
        public int RemainingFreeEntries => _config != null
            ? Mathf.Max(0, _config.dailyFreeEntries - _usedFreeEntries)
            : 0;

        /// <summary>일일 무료 도전 횟수</summary>
        public int DailyFreeEntries => _config != null ? _config.dailyFreeEntries : 5;

        /// <summary>현재 티어 데이터 SO</summary>
        public ArenaDataSO CurrentTierData =>
            _config != null && _currentTier >= 0 && _currentTier < _config.tiers.Length
                ? _config.tiers[_currentTier]
                : null;

        /// <summary>현재 매칭 후보 목록</summary>
        public IReadOnlyList<ArenaOpponent> CurrentCandidates => _currentCandidates;

        /// <summary>전적 기록 (최근 50전)</summary>
        public IReadOnlyList<ArenaRecordEntry> Records => _records;

        /// <summary>현재 매칭된 라이벌 원본 데이터 (쉬움/보통/어려움)</summary>
        public ArenaRival[] CurrentRivals => _currentRivals;

        /// <summary>승률 (0~1)</summary>
        public float WinRate
        {
            get
            {
                int total = _totalVictories + _totalDefeats;
                return total > 0 ? (float)_totalVictories / total : 0f;
            }
        }

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
        }

        private void Start()
        {
            CheckUnlock();
            CheckDailyReset();
            CheckSeasonReset();
            LoadSaveData();
        }

        // ══════════════════════════════════════
        // 매칭
        // ══════════════════════════════════════

        /// <summary>매칭 후보 3명 생성 (CP ±20% 범위)</summary>
        public List<ArenaOpponent> GenerateCandidates()
        {
            _currentCandidates.Clear();

            if (_config == null) return _currentCandidates;

            long playerCp = GetPlayerCp();
            float range = _config.matchCpRange;
            int count = _config.candidateCount;

            for (int i = 0; i < count; i++)
            {
                float cpVariance = UnityEngine.Random.Range(-range, range);
                long opponentCp = Math.Max(100L, (long)(playerCp * (1f + cpVariance)));

                var opponent = new ArenaOpponent
                {
                    Name = GenerateOpponentName(i),
                    PowerScore = opponentCp,
                    Level = EstimateLevelFromCp(opponentCp),
                    Tier = _currentTier,
                    Rating = _rating + UnityEngine.Random.Range(-50, 51)
                };
                _currentCandidates.Add(opponent);
            }

            Debug.Log($"[ArenaManager] 매칭 후보 {count}명 생성 (기준 CP: {playerCp:N0})");
            return _currentCandidates;
        }

        /// <summary>
        /// 라이벌 기반 매칭 후보 3명 생성 (쉬움/보통/어려움).
        /// ArenaRivalDataSO가 없으면 기존 GenerateCandidates() 폴백.
        /// </summary>
        public List<ArenaOpponent> GenerateRivalCandidates()
        {
            if (_rivalData == null || _rivalData.rivals == null || _rivalData.rivals.Length == 0)
                return GenerateCandidates();

            _currentCandidates.Clear();
            long playerCp = GetPlayerCp();

            _currentRivals = _rivalData.GetMatchCandidates(_rating);

            // 난이도별 스탯 배율: 쉬움 0.90, 보통 1.00, 어려움 1.10
            float[] scaleRanges = { 0.90f, 1.00f, 1.10f };

            for (int i = 0; i < 3; i++)
            {
                var rival = _currentRivals[i];
                if (rival == null) continue;

                float scale = rival.statScale * scaleRanges[i];
                long opponentCp = System.Math.Max(100L, (long)(playerCp * scale));

                var opponent = new ArenaOpponent
                {
                    Name = rival.displayName,
                    PowerScore = opponentCp,
                    Level = EstimateLevelFromCp(opponentCp),
                    Tier = _currentTier,
                    Rating = Mathf.Max(0, _rating + (i - 1) * RIVAL_RATING_OFFSET),
                    RivalId = rival.rivalId,
                    JobId = rival.jobId,
                    Title = rival.title,
                    Difficulty = i // 0=쉬움, 1=보통, 2=어려움
                };
                _currentCandidates.Add(opponent);
            }

            Debug.Log($"[ArenaManager] 라이벌 매칭 3명 생성 (기준 ELO: {_rating})");
            return _currentCandidates;
        }

        /// <summary>
        /// CP 비교 기반 전투 시뮬레이션.
        /// </summary>
        public ArenaBattleSimulator.BattleResult SimulateBattleDetailed(int candidateIndex)
        {
            if (candidateIndex < 0 || candidateIndex >= _currentCandidates.Count)
                return default;

            var opponent = _currentCandidates[candidateIndex];
            long playerCp = GetPlayerCp();

            var result = ArenaBattleSimulator.Simulate(playerCp, opponent.PowerScore);

            // 결과 처리
            var matchResult = ProcessMatchResult(result.IsVictory, opponent);

            // 전적 기록 추가
            AddRecord(new ArenaRecordEntry
            {
                OpponentName = opponent.Name,
                OpponentJob = opponent.JobId ?? "",
                OpponentElo = opponent.Rating,
                IsVictory = result.IsVictory,
                EloChange = matchResult.RatingChange,
                PlayerDamage = result.PlayerTotalDamage,
                OpponentDamage = result.OpponentTotalDamage,
                Timestamp = DateTime.Now.ToString("O")
            });

            // 연승 보너스 보상 (3연승마다 +50% 골드)
            if (result.IsVictory && _currentWinStreak > 0 && _currentWinStreak % 3 == 0)
            {
                int bonusGold = CalculateMatchRewardGold() / 2;
                CurrencyManager.Instance?.Add(CurrencyType.Gold, bonusGold);
                Debug.Log($"[ArenaManager] {_currentWinStreak}연승 보너스! +{bonusGold} 골드");
            }

            return result;
        }

        private void AddRecord(ArenaRecordEntry record)
        {
            _records.Add(record);
            if (_records.Count > MAX_RECORDS)
                _records.RemoveAt(0);
            SaveToSaveData();
        }

        // ══════════════════════════════════════
        // 전투
        // ══════════════════════════════════════

        /// <summary>도전 가능 여부 확인</summary>
        public bool CanEnter()
        {
            if (!IsUnlocked) return false;
            if (_config == null) return false;

            // 무료 도전 또는 아레나 티켓
            if (RemainingFreeEntries <= 0)
            {
                var cm = CurrencyManager.Instance;
                if (cm == null || cm.GetAmount(CurrencyType.ArenaTicket) <= 0)
                    return false;
            }

            return true;
        }

        /// <summary>입장 불가 사유</summary>
        public string GetDenyReason()
        {
            if (!IsUnlocked)
                return $"스테이지 {(_config != null ? _config.unlockFloor : 100)} 클리어 필요";
            if (_config == null)
                return "아레나 설정 데이터 없음";

            if (RemainingFreeEntries <= 0)
            {
                var cm = CurrencyManager.Instance;
                if (cm == null || cm.GetAmount(CurrencyType.ArenaTicket) <= 0)
                    return "무료 도전 소진 + 아레나 티켓 부족";
            }

            return null;
        }

        /// <summary>
        /// 아레나 전투 시작. 입장권 소비.
        /// </summary>
        public bool StartMatch(int candidateIndex)
        {
            if (!CanEnter())
            {
                Debug.LogWarning($"[ArenaManager] 도전 불가 — {GetDenyReason()}");
                return false;
            }

            if (candidateIndex < 0 || candidateIndex >= _currentCandidates.Count)
            {
                Debug.LogWarning("[ArenaManager] 잘못된 후보 인덱스");
                return false;
            }

            // 입장권 소비
            if (RemainingFreeEntries > 0)
            {
                _usedFreeEntries++;
            }
            else
            {
                CurrencyManager.Instance?.Add(CurrencyType.ArenaTicket, -1);
            }

            SaveToSaveData();

            var opponent = _currentCandidates[candidateIndex];
            EventBus.Publish(new ArenaStartedEvent
            {
                OpponentName = opponent.Name,
                OpponentCp = opponent.PowerScore
            });

            Debug.Log($"[ArenaManager] 전투 시작 — vs {opponent.Name} (CP: {opponent.PowerScore:N0})");
            return true;
        }

        /// <summary>
        /// 전투 결과를 시뮬레이션한다 (비동기 PvP).
        /// </summary>
        public ArenaMatchResult SimulateBattle(int candidateIndex)
        {
            if (candidateIndex < 0 || candidateIndex >= _currentCandidates.Count)
                return new ArenaMatchResult { IsVictory = false };

            var opponent = _currentCandidates[candidateIndex];
            long playerCp = GetPlayerCp();

            // CP 기반 승률 계산
            float cpRatio = playerCp > 0 ? (float)opponent.PowerScore / playerCp : 1f;
            float winChance = Mathf.Clamp01(1f - (cpRatio - 0.5f));
            winChance = Mathf.Clamp(winChance, 0.15f, 0.85f);

            bool isVictory = UnityEngine.Random.value < winChance;
            return ProcessMatchResult(isVictory, opponent);
        }

        /// <summary>
        /// 전투 결과 처리 (외부에서 직접 승패 결정 시 사용).
        /// </summary>
        public ArenaMatchResult ProcessMatchResult(bool isVictory, ArenaOpponent opponent)
        {
            var tierData = CurrentTierData;
            int ratingChange = 0;

            if (isVictory)
            {
                _totalVictories++;
                _currentWinStreak++;
                if (_currentWinStreak > _bestWinStreak)
                    _bestWinStreak = _currentWinStreak;

                ratingChange = tierData != null ? tierData.winRating : 30;
                // 연승 보너스 (+10% per streak, 최대 +50%)
                float streakBonus = 1f + Mathf.Min(_currentWinStreak - 1, 5) * 0.1f;
                ratingChange = Mathf.RoundToInt(ratingChange * streakBonus);
            }
            else
            {
                _totalDefeats++;
                _currentWinStreak = 0;
                ratingChange = -(tierData != null ? tierData.loseRating : 15);
            }

            _rating = Mathf.Max(0, _rating + ratingChange);

            // 티어 승격/강등 확인
            UpdateTier();

            // 보상 계산
            int rewardGold = isVictory ? CalculateMatchRewardGold() : 0;
            int rewardRuby = isVictory ? CalculateMatchRewardRuby() : 0;

            // 보상 지급
            if (isVictory)
            {
                var cm = CurrencyManager.Instance;
                if (cm != null)
                {
                    if (rewardGold > 0) cm.Add(CurrencyType.Gold, rewardGold);
                    if (rewardRuby > 0) cm.Add(CurrencyType.Ruby, rewardRuby);
                }
            }

            SaveToSaveData();

            // 이벤트 발행
            EventBus.Publish(new ArenaMatchEvent
            {
                IsVictory = isVictory,
                ArenaRank = _currentTier
            });

            if (isVictory)
            {
                EventBus.Publish(new PvpVictoryEvent
                {
                    TotalVictories = _totalVictories,
                    CurrentStreak = _currentWinStreak
                });

                // 2026-04-23 이슈 15 FeedbackBus: 아레나 승리 Toast + 쉐이크
                MkLike.Core.FeedbackBus.Emit(
                    MkLike.Core.FeedbackKind.Generic,
                    $"아레나 승리! {_currentWinStreak}연승",
                    shakeIntensity: 2);
            }
            else
            {
                // 2026-04-23 이슈 15 FeedbackBus: 아레나 패배 Toast
                MkLike.Core.FeedbackBus.Emit(
                    MkLike.Core.FeedbackKind.Negative,
                    "아레나 패배");
            }

            var result = new ArenaMatchResult
            {
                IsVictory = isVictory,
                RatingChange = ratingChange,
                NewRating = _rating,
                NewTier = _currentTier,
                RewardGold = rewardGold,
                RewardRuby = rewardRuby,
                OpponentName = opponent.Name,
                WinStreak = _currentWinStreak
            };

            Debug.Log($"[ArenaManager] 결과: {(isVictory ? "승리" : "패배")} | 레이팅: {_rating} ({(ratingChange >= 0 ? "+" : "")}{ratingChange}) | 티어: {_currentTier} | 연승: {_currentWinStreak}");
            return result;
        }

        // ══════════════════════════════════════
        // 티어 관리
        // ══════════════════════════════════════

        private void UpdateTier()
        {
            if (_config == null || _config.tiers == null || _config.tiers.Length == 0) return;

            for (int i = _config.tiers.Length - 1; i >= 0; i--)
            {
                if (_rating >= _config.tiers[i].minRating)
                {
                    if (i != _currentTier)
                    {
                        int oldTier = _currentTier;
                        _currentTier = i;
                        Debug.Log($"[ArenaManager] 티어 변경: {oldTier} → {_currentTier}");
                    }
                    break;
                }
            }
        }

        /// <summary>시즌 종료 시 티어 강등</summary>
        private void DemoteTier()
        {
            if (_config == null) return;
            int demote = _config.tierDemoteOnSeasonEnd;
            _currentTier = Mathf.Max(0, _currentTier - demote);
            if (_config.tiers != null && _currentTier < _config.tiers.Length)
                _rating = _config.tiers[_currentTier].minRating;
        }

        // ══════════════════════════════════════
        // 보상
        // ══════════════════════════════════════

        private int CalculateMatchRewardGold()
        {
            int baseGold = (_currentTier + 1) * ARENA_GOLD_PER_TIER;
            return baseGold;
        }

        private int CalculateMatchRewardRuby()
        {
            return _currentTier >= 1 ? (_currentTier) * ARENA_RUBY_PER_TIER : 0;
        }

        // ══════════════════════════════════════
        // 가상 상대 생성
        // ══════════════════════════════════════

        private static readonly string[] OPPONENT_PREFIXES = { "용사", "검사", "마법사", "궁수", "성기사", "암살자", "무도가", "주술사" };
        private static readonly string[] OPPONENT_SUFFIXES = { "세라", "카이", "류", "미르", "하늘", "불꽃", "번개", "바람", "얼음", "빛" };

        private string GenerateOpponentName(int index)
        {
            int prefixIdx = (index + _rating) % OPPONENT_PREFIXES.Length;
            int suffixIdx = (_totalVictories + index * 3) % OPPONENT_SUFFIXES.Length;
            return $"{OPPONENT_PREFIXES[prefixIdx]}{OPPONENT_SUFFIXES[suffixIdx]}";
        }

        // TODO: CP 구간/레벨 범위 매핑을 SO(ScriptableObject)로 전환 예정.
        // 현재는 하드코딩된 CP 구간별 추정 레벨 범위:
        //   CP < 1,000     → Lv 10~30   (초반)
        //   CP < 5,000     → Lv 30~60   (중반 초입)
        //   CP < 20,000    → Lv 60~100  (중반)
        //   CP < 100,000   → Lv 100~150 (후반)
        //   CP >= 100,000  → Lv 150~200 (엔드게임)
        private int EstimateLevelFromCp(long cp)
        {
            if (cp < 1000) return UnityEngine.Random.Range(10, 30);
            if (cp < 5000) return UnityEngine.Random.Range(30, 60);
            if (cp < 20000) return UnityEngine.Random.Range(60, 100);
            if (cp < 100000) return UnityEngine.Random.Range(100, 150);
            return UnityEngine.Random.Range(150, 200);
        }

        // ══════════════════════════════════════
        // 일일/시즌 리셋
        // ══════════════════════════════════════

        private void CheckDailyReset()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (_lastResetDate != today)
            {
                _usedFreeEntries = 0;
                _lastResetDate = today;
                Debug.Log("[ArenaManager] 일일 무료 도전 리셋");
            }
        }

        private void CheckSeasonReset()
        {
            if (_config == null) return;
            if (string.IsNullOrEmpty(_seasonStartTime))
            {
                StartNewSeason();
                return;
            }

            if (DateTime.TryParse(_seasonStartTime, out var seasonStart))
            {
                var elapsed = DateTime.Now - seasonStart;
                if (elapsed.TotalDays >= _config.seasonDurationDays)
                {
                    DemoteTier();
                    StartNewSeason();
                    Debug.Log("[ArenaManager] 시즌 종료 → 티어 강등 + 새 시즌 시작");
                }
            }
        }

        private void StartNewSeason()
        {
            _seasonId = $"S{DateTime.Now:yyyyMMdd}";
            _seasonStartTime = DateTime.Now.ToString("O");
            SaveToSaveData();
        }

        // ══════════════════════════════════════
        // 해금
        // ══════════════════════════════════════

        private void CheckUnlock()
        {
            if (IsUnlocked) return;
            int currentFloor = GetCurrentFloor();
            int requiredFloor = _config != null ? _config.unlockFloor : 100;
            if (currentFloor >= requiredFloor)
            {
                IsUnlocked = true;
                Debug.Log("[ArenaManager] 아레나 시스템 해금!");
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

            var data = SaveManager.Instance.CurrentData.arena;
            if (data == null) return;

            _currentTier = data.currentTier;
            _rating = data.rating;
            _totalVictories = data.totalVictories;
            _totalDefeats = data.totalDefeats;
            _currentWinStreak = data.currentWinStreak;
            _bestWinStreak = data.bestWinStreak;
            _usedFreeEntries = data.usedFreeEntries;
            _lastResetDate = data.lastResetDate ?? "";
            _seasonId = data.seasonId ?? "";
            _seasonStartTime = data.seasonStartTime ?? "";

            // 전적 로드
            _records.Clear();
            if (data.records != null)
            {
                for (int i = 0; i < data.records.Count; i++)
                {
                    var r = data.records[i];
                    _records.Add(new ArenaRecordEntry
                    {
                        OpponentName = r.opponentName ?? "",
                        OpponentJob = r.opponentJob ?? "",
                        OpponentElo = r.opponentElo,
                        IsVictory = r.isVictory,
                        EloChange = r.eloChange,
                        PlayerDamage = r.playerDamage,
                        OpponentDamage = r.opponentDamage,
                        Timestamp = r.timestamp ?? ""
                    });
                }
            }

            CheckDailyReset();
            CheckSeasonReset();
            CheckUnlock();

            Debug.Log($"[ArenaManager] 로드 완료 — 티어: {_currentTier}, 레이팅: {_rating}, 승: {_totalVictories}, 패: {_totalDefeats}");
        }

        private void SaveToSaveData()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null)
                return;

            if (SaveManager.Instance.CurrentData.arena == null)
                SaveManager.Instance.CurrentData.arena = new ArenaSaveData();

            var data = SaveManager.Instance.CurrentData.arena;
            data.currentTier = _currentTier;
            data.rating = _rating;
            data.totalVictories = _totalVictories;
            data.totalDefeats = _totalDefeats;
            data.currentWinStreak = _currentWinStreak;
            data.bestWinStreak = _bestWinStreak;
            data.usedFreeEntries = _usedFreeEntries;
            data.lastResetDate = _lastResetDate;
            data.seasonId = _seasonId;
            data.seasonStartTime = _seasonStartTime;

            // 전적 저장
            data.records.Clear();
            for (int i = 0; i < _records.Count; i++)
            {
                var r = _records[i];
                data.records.Add(new ArenaRecordData
                {
                    opponentName = r.OpponentName,
                    opponentJob = r.OpponentJob,
                    opponentElo = r.OpponentElo,
                    isVictory = r.IsVictory,
                    eloChange = r.EloChange,
                    playerDamage = r.PlayerDamage,
                    opponentDamage = r.OpponentDamage,
                    timestamp = r.Timestamp
                });
            }
        }

        // ── 서버/로컬 자동 분기 ──

        /// <summary>서버 모드 여부</summary>
        public bool IsServerMode => ApiClient.HasAuth;

        /// <summary>전투 시뮬레이션 (서버/로컬 자동 분기)</summary>
        public async UniTask<ArenaMatchResult> SimulateBattleAsync(int candidateIndex, CancellationToken ct)
        {
            if (IsServerMode)
            {
                long playerCp = GetPlayerCp();
                var serverResult = await ProcessBattleServerAsync(candidateIndex, playerCp, ct);
                if (serverResult == null) return new ArenaMatchResult { IsVictory = false };

                return new ArenaMatchResult
                {
                    IsVictory = serverResult.isVictory,
                    RatingChange = serverResult.ratingChange,
                    NewRating = serverResult.newRating,
                    NewTier = serverResult.newTier,
                    WinStreak = serverResult.currentWinStreak
                };
            }
            return SimulateBattle(candidateIndex);
        }

        // ── 서버 연동 (내부) ──

        /// <summary>서버에서 매칭 후보 3명을 가져온다.</summary>
        public async UniTask<ArenaCandidatesResponse> GetCandidatesServerAsync(long playerCp, CancellationToken ct)
        {
            var request = new ArenaBattleRequest { candidateIndex = 0, playerCp = playerCp };
            var response = await ApiClient.PostAsync<ArenaCandidatesResponse>("arena/match-candidates", request, ct);
            if (!response.success || response.data == null)
            {
                Debug.LogWarning($"[ArenaManager] 서버 매칭 실패: {response.error}");
                return null;
            }
            return response.data;
        }

        /// <summary>서버에서 전투 결과를 처리한다.</summary>
        public async UniTask<ArenaBattleResponse> ProcessBattleServerAsync(int candidateIndex, long playerCp, CancellationToken ct)
        {
            var request = new ArenaBattleRequest { candidateIndex = candidateIndex, playerCp = playerCp };
            var response = await ApiClient.PostAsync<ArenaBattleResponse>("arena/battle-result", request, ct);
            if (!response.success || response.data == null)
            {
                Debug.LogWarning($"[ArenaManager] 서버 전투 결과 실패: {response.error}");
                return null;
            }

            // 서버 결과로 로컬 상태 동기화
            var result = response.data;
            _currentTier = result.newTier;
            _rating = result.newRating;
            _currentWinStreak = result.currentWinStreak;
            if (result.isVictory) _totalVictories++;
            else _totalDefeats++;

            SaveToSaveData();
            return result;
        }

        /// <summary>서버에서 아레나 상태를 가져와 동기화한다.</summary>
        public async UniTask SyncStatusFromServerAsync(CancellationToken ct)
        {
            var response = await ApiClient.GetAsync<ArenaStatusResponse>("arena/status", ct);
            if (!response.success || response.data == null) return;

            var status = response.data;
            _currentTier = status.currentTier;
            _rating = status.rating;
            _totalVictories = status.totalVictories;
            _totalDefeats = status.totalDefeats;
            _currentWinStreak = status.currentWinStreak;
            _bestWinStreak = status.bestWinStreak;

            SaveToSaveData();
            Debug.Log($"[ArenaManager] 서버 상태 동기화 완료 — 티어:{_currentTier} 레이팅:{_rating}");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }

    // ══════════════════════════════════════
    // 데이터 구조체
    // ══════════════════════════════════════

    /// <summary>아레나 가상 상대 정보.</summary>
    [System.Serializable]
    public class ArenaOpponent
    {
        public string Name;
        public long PowerScore;
        public int Level;
        public int Tier;
        public int Rating;
        public string RivalId;
        public string JobId;
        public string Title;
        /// <summary>난이도 (0=쉬움, 1=보통, 2=어려움)</summary>
        public int Difficulty;
    }

    /// <summary>아레나 전적 기록 항목.</summary>
    [System.Serializable]
    public struct ArenaRecordEntry
    {
        public string OpponentName;
        public string OpponentJob;
        public int OpponentElo;
        public bool IsVictory;
        public int EloChange;
        public long PlayerDamage;
        public long OpponentDamage;
        public string Timestamp;
    }

    /// <summary>아레나 매치 결과.</summary>
    public struct ArenaMatchResult
    {
        public bool IsVictory;
        public int RatingChange;
        public int NewRating;
        public int NewTier;
        public int RewardGold;
        public int RewardRuby;
        public string OpponentName;
        public int WinStreak;
    }

    /// <summary>아레나 전투 시작 이벤트.</summary>
    public struct ArenaStartedEvent : IEvent
    {
        public string OpponentName;
        public long OpponentCp;
    }
}
