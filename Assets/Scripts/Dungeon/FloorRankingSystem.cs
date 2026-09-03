using UnityEngine;
using System;
using System.Collections.Generic;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Economy;
using MkLike.Utils;

namespace MkLike.Dungeon
{
    public enum RankingType
    {
        Overall,
        Weekly,
        JobWarrior,
        JobArcher,
        JobMage
    }

    [Serializable]
    public struct RankingEntry
    {
        public string playerName;
        public int highestFloor;
        public int weeklyFloors;
        public string jobName;
        public bool isPlayer;
    }

    /// <summary>
    /// 층 정복 랭킹 시스템.
    /// 최고 도달 층수 기준 전체/주간/직업별 랭킹을 관리한다.
    /// 현재는 로컬 전용 — NPC 더미 데이터로 랭킹 시뮬레이션.
    /// </summary>
    public class FloorRankingSystem : MonoBehaviour
    {
        public static FloorRankingSystem Instance { get; private set; }

        private const int MAX_RANKING_SIZE = 100;
        private const string PREFS_KEY_PLAYER = "FloorRanking_Player";
        private const string PREFS_KEY_WEEKLY_CLAIMED = "FloorRanking_WeeklyClaimed";
        private const string PREFS_KEY_WEEKLY_RESET_DATE = "FloorRanking_WeeklyResetDate";
        private const string PREFS_KEY_NPC_SEED = "FloorRanking_NpcSeed";

        private static readonly string[] NPC_JOBS = { "전사", "궁수", "마법사" };

        private RankingEntry _playerEntry;
        private readonly List<RankingEntry> _overallRanking = new();
        private readonly List<RankingEntry> _weeklyRanking = new();
        private readonly List<RankingEntry> _npcEntries = new();
        private bool _isWeeklyRewardClaimed;
        private int _weeklyStartFloor;

        public RankingEntry PlayerEntry => _playerEntry;
        public List<RankingEntry> OverallRanking => _overallRanking;
        public List<RankingEntry> WeeklyRanking => _weeklyRanking;
        public bool IsWeeklyRewardClaimed => _isWeeklyRewardClaimed;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            CheckWeeklyReset();
            LoadPlayerData();
            GenerateNpcRankings();
            RebuildRankings();

            EventBus<TowerFloorReachedEvent>.Subscribe(OnTowerFloorReached);
        }

        private void OnDisable()
        {
            EventBus<TowerFloorReachedEvent>.Unsubscribe(OnTowerFloorReached);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── 이벤트 핸들러 ──

        private void OnTowerFloorReached(TowerFloorReachedEvent e)
        {
            UpdatePlayerRanking();
        }

        // ── 공개 API ──

        /// <summary>플레이어 랭킹 갱신 (SaveData 기준)</summary>
        public void UpdatePlayerRanking()
        {
            int currentFloor = GetPlayerHighestFloor();
            if (currentFloor <= 0) return;

            string jobName = GetPlayerJobName();

            if (currentFloor > _playerEntry.highestFloor)
            {
                int gained = currentFloor - _playerEntry.highestFloor;
                _playerEntry.weeklyFloors += gained;
                _playerEntry.highestFloor = currentFloor;
            }

            _playerEntry.jobName = jobName;
            _playerEntry.playerName = "플레이어";
            _playerEntry.isPlayer = true;

            SavePlayerData();
            ScaleNpcRankings(currentFloor);
            RebuildRankings();
        }

        /// <summary>지정 랭킹 타입에서 플레이어 순위 반환 (1-based, 미등록 시 -1)</summary>
        public int GetPlayerRank(RankingType type)
        {
            List<RankingEntry> list = GetRankingList(type);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].isPlayer) return i + 1;
            }
            return -1;
        }

        /// <summary>주간 보상 수령</summary>
        public void ClaimWeeklyReward()
        {
            if (_isWeeklyRewardClaimed)
            {
                Debug.Log("[FloorRankingSystem] 이미 주간 보상을 수령했습니다.");
                return;
            }

            int rank = GetPlayerRank(RankingType.Weekly);
            if (rank < 0)
            {
                Debug.Log("[FloorRankingSystem] 주간 랭킹에 등록되지 않았습니다.");
                return;
            }

            GetWeeklyReward(rank, out int ruby, out int weaponTicket);

            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.Add(CurrencyType.Ruby, ruby);
                CurrencyManager.Instance.Add(CurrencyType.WeaponTicket, weaponTicket);
            }

            _isWeeklyRewardClaimed = true;
            PlayerPrefs.SetInt(PREFS_KEY_WEEKLY_CLAIMED, 1);
            PlayerPrefs.Save();

            Debug.Log($"[FloorRankingSystem] 주간 보상 수령 (순위 {rank}): 루비 {ruby}, 무기소환권 {weaponTicket}");
        }

        /// <summary>주간 리셋 (주간 층수 초기화, 보상 수령 상태 초기화)</summary>
        public void WeeklyReset()
        {
            _playerEntry.weeklyFloors = 0;
            _isWeeklyRewardClaimed = false;
            _weeklyStartFloor = _playerEntry.highestFloor;

            // NPC 주간 층수도 초기화
            for (int i = 0; i < _npcEntries.Count; i++)
            {
                var entry = _npcEntries[i];
                entry.weeklyFloors = 0;
                _npcEntries[i] = entry;
            }

            // 리셋 날짜 저장 (다음 월요일까지)
            string nextMonday = GetNextMondayDateString();
            PlayerPrefs.SetString(PREFS_KEY_WEEKLY_RESET_DATE, nextMonday);
            PlayerPrefs.SetInt(PREFS_KEY_WEEKLY_CLAIMED, 0);
            PlayerPrefs.Save();

            SavePlayerData();
            RebuildRankings();

            Debug.Log("[FloorRankingSystem] 주간 랭킹 리셋 완료");
        }

        /// <summary>지정 랭킹 타입의 랭킹 리스트 반환</summary>
        public List<RankingEntry> GetRankingList(RankingType type)
        {
            switch (type)
            {
                case RankingType.Overall:
                    return _overallRanking;
                case RankingType.Weekly:
                    return _weeklyRanking;
                case RankingType.JobWarrior:
                    return FilterByJob(_overallRanking, "전사");
                case RankingType.JobArcher:
                    return FilterByJob(_overallRanking, "궁수");
                case RankingType.JobMage:
                    return FilterByJob(_overallRanking, "마법사");
                default:
                    return _overallRanking;
            }
        }

        // ── 보상 테이블 ──

        // 주간 보상: 1위
        private const int WEEKLY_RANK1_RUBY = 3000;
        private const int WEEKLY_RANK1_WEAPON_TICKET = 10;
        // 주간 보상: 2~10위
        private const int WEEKLY_RANK10_RUBY = 1500;
        private const int WEEKLY_RANK10_WEAPON_TICKET = 5;
        // 주간 보상: 11~50위
        private const int WEEKLY_RANK50_RUBY = 800;
        private const int WEEKLY_RANK50_WEAPON_TICKET = 3;
        // 주간 보상: 51위 이하
        private const int WEEKLY_RANK_DEFAULT_RUBY = 500;
        private const int WEEKLY_RANK_DEFAULT_WEAPON_TICKET = 2;

        /// <summary>순위별 주간 보상 반환</summary>
        public static void GetWeeklyReward(int rank, out int ruby, out int weaponTicket)
        {
            if (rank == 1)
            {
                ruby = WEEKLY_RANK1_RUBY;
                weaponTicket = WEEKLY_RANK1_WEAPON_TICKET;
            }
            else if (rank <= 10)
            {
                ruby = WEEKLY_RANK10_RUBY;
                weaponTicket = WEEKLY_RANK10_WEAPON_TICKET;
            }
            else if (rank <= 50)
            {
                ruby = WEEKLY_RANK50_RUBY;
                weaponTicket = WEEKLY_RANK50_WEAPON_TICKET;
            }
            else
            {
                ruby = WEEKLY_RANK_DEFAULT_RUBY;
                weaponTicket = WEEKLY_RANK_DEFAULT_WEAPON_TICKET;
            }
        }

        // ── NPC 더미 데이터 생성 ──

        /// <summary>100개 NPC 엔트리 자동 생성</summary>
        public void GenerateNpcRankings()
        {
            _npcEntries.Clear();

            int playerFloor = Mathf.Max(_playerEntry.highestFloor, 10);
            int seed = PlayerPrefs.GetInt(PREFS_KEY_NPC_SEED, 0);
            if (seed == 0)
            {
                seed = UnityEngine.Random.Range(1, 999999);
                PlayerPrefs.SetInt(PREFS_KEY_NPC_SEED, seed);
                PlayerPrefs.Save();
            }

            var rng = new System.Random(seed);

            for (int i = 0; i < MAX_RANKING_SIZE; i++)
            {
                // 정규분포 근사 (Box-Muller)
                float u1 = (float)rng.NextDouble();
                float u2 = (float)rng.NextDouble();
                if (u1 < 0.0001f) u1 = 0.0001f;
                float normalValue = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);

                // 플레이어 ±30% 범위 스케일링
                float mean = playerFloor;
                float stdDev = playerFloor * 0.3f;
                int floor = Mathf.Max(1, Mathf.RoundToInt(mean + normalValue * stdDev));

                // 주간 상승량: 최고 층의 5~20%
                int weeklyGain = Mathf.Max(0, Mathf.RoundToInt(floor * (0.05f + (float)rng.NextDouble() * 0.15f)));

                string jobName = NPC_JOBS[rng.Next(NPC_JOBS.Length)];
                string npcName = $"탐험가_{(i + 1):D3}";

                _npcEntries.Add(new RankingEntry
                {
                    playerName = npcName,
                    highestFloor = floor,
                    weeklyFloors = weeklyGain,
                    jobName = jobName,
                    isPlayer = false
                });
            }
        }

        // ── 내부 유틸리티 ──

        private void RebuildRankings()
        {
            // 전체 랭킹 구축
            _overallRanking.Clear();
            _overallRanking.AddRange(_npcEntries);
            _overallRanking.Add(_playerEntry);
            _overallRanking.Sort((a, b) => b.highestFloor.CompareTo(a.highestFloor));
            if (_overallRanking.Count > MAX_RANKING_SIZE)
                _overallRanking.RemoveRange(MAX_RANKING_SIZE, _overallRanking.Count - MAX_RANKING_SIZE);

            // 주간 랭킹 구축
            _weeklyRanking.Clear();
            _weeklyRanking.AddRange(_npcEntries);
            _weeklyRanking.Add(_playerEntry);
            _weeklyRanking.Sort((a, b) => b.weeklyFloors.CompareTo(a.weeklyFloors));
            if (_weeklyRanking.Count > MAX_RANKING_SIZE)
                _weeklyRanking.RemoveRange(MAX_RANKING_SIZE, _weeklyRanking.Count - MAX_RANKING_SIZE);
        }

        private List<RankingEntry> FilterByJob(List<RankingEntry> source, string jobName)
        {
            var filtered = new List<RankingEntry>();
            for (int i = 0; i < source.Count; i++)
            {
                if (string.Equals(source[i].jobName, jobName, StringComparison.Ordinal))
                    filtered.Add(source[i]);
            }
            if (filtered.Count > MAX_RANKING_SIZE)
                filtered.RemoveRange(MAX_RANKING_SIZE, filtered.Count - MAX_RANKING_SIZE);
            return filtered;
        }

        /// <summary>플레이어 층수 변화에 따라 NPC 층수를 동적 스케일링</summary>
        private void ScaleNpcRankings(int playerFloor)
        {
            if (playerFloor <= 10) return;

            // 기존 NPC 평균 대비 플레이어가 너무 앞서면 NPC도 끌어올림
            float npcAvg = 0f;
            for (int i = 0; i < _npcEntries.Count; i++)
                npcAvg += _npcEntries[i].highestFloor;
            npcAvg /= Mathf.Max(1, _npcEntries.Count);

            // 플레이어가 NPC 평균의 2배 이상이면 스케일링
            if (playerFloor > npcAvg * 2f)
            {
                var rng = new System.Random(_playerEntry.highestFloor);
                for (int i = 0; i < _npcEntries.Count; i++)
                {
                    var entry = _npcEntries[i];
                    float u1 = (float)rng.NextDouble();
                    float u2 = (float)rng.NextDouble();
                    if (u1 < 0.0001f) u1 = 0.0001f;
                    float normalValue = Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);

                    float mean = playerFloor;
                    float stdDev = playerFloor * 0.3f;
                    entry.highestFloor = Mathf.Max(entry.highestFloor, Mathf.RoundToInt(mean + normalValue * stdDev));
                    entry.weeklyFloors = Mathf.Max(0, Mathf.RoundToInt(entry.highestFloor * (0.05f + (float)rng.NextDouble() * 0.15f)));
                    _npcEntries[i] = entry;
                }
            }
        }

        private int GetPlayerHighestFloor()
        {
            int saved = SaveManager.Instance?.CurrentData?.progress?.maxFloor ?? 0;
            return saved > 0 ? saved : _playerEntry.highestFloor;
        }

        private string GetPlayerJobName()
        {
            if (Growth.JobSystem.Instance != null)
                return Growth.JobSystem.Instance.GetCurrentDisplayName();
            return "전사";
        }

        // ── 저장/불러오기 ──

        private void SavePlayerData()
        {
            string json = JsonUtility.ToJson(_playerEntry);
            PlayerPrefs.SetString(PREFS_KEY_PLAYER, json);
            PlayerPrefs.Save();
        }

        private void LoadPlayerData()
        {
            string json = PlayerPrefs.GetString(PREFS_KEY_PLAYER, "");
            if (!string.IsNullOrEmpty(json))
            {
                _playerEntry = JsonUtility.FromJson<RankingEntry>(json);
                _playerEntry.isPlayer = true;
            }
            else
            {
                _playerEntry = new RankingEntry
                {
                    playerName = "플레이어",
                    highestFloor = GetPlayerHighestFloor(),
                    weeklyFloors = 0,
                    jobName = GetPlayerJobName(),
                    isPlayer = true
                };
            }

            _isWeeklyRewardClaimed = PlayerPrefs.GetInt(PREFS_KEY_WEEKLY_CLAIMED, 0) == 1;
        }

        // ── 주간 리셋 체크 ──

        private void CheckWeeklyReset()
        {
            string savedDate = PlayerPrefs.GetString(PREFS_KEY_WEEKLY_RESET_DATE, "");
            if (string.IsNullOrEmpty(savedDate))
            {
                // 최초 실행 — 다음 월요일 저장
                PlayerPrefs.SetString(PREFS_KEY_WEEKLY_RESET_DATE, GetNextMondayDateString());
                PlayerPrefs.Save();
                return;
            }

            if (DateTime.TryParse(savedDate, out DateTime resetDate))
            {
                if (DateTime.Now >= resetDate)
                {
                    Debug.Log("[FloorRankingSystem] 주간 리셋 시간 도달 — 자동 리셋 실행");
                    WeeklyReset();
                }
            }
        }

        private static string GetNextMondayDateString()
        {
            DateTime now = DateTime.Now;
            int daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0) daysUntilMonday = 7;
            DateTime nextMonday = now.Date.AddDays(daysUntilMonday);
            return nextMonday.ToString("yyyy-MM-dd");
        }
    }
}
