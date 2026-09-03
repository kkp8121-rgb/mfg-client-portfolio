using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using MkLike.Core;
using MkLike.Core.Net;
using MkLike.Core.Save;
using MkLike.Data;
using MkLike.Utils;

namespace MkLike.Economy
{
    /// <summary>
    /// 가챠 풀 종류.
    /// </summary>
    public enum GachaPoolType
    {
        Equipment,
        Weapon
        // Relic: 2026-04-20 유물 시스템 완전 제거
    }

    /// <summary>
    /// 가챠 매니저.
    /// 소환 레벨 시스템: 뽑기 누적 → 레벨업 → 고등급 확률 증가.
    /// 10연차 보장을 지원한다.
    /// </summary>
    public class GachaManager : MonoBehaviour
    {
        public static GachaManager Instance { get; private set; }

        [Header("가챠 풀 SO")]
        [SerializeField] private GachaPoolSO _equipmentPool;
        [SerializeField] private GachaPoolSO _weaponPool;
        // _relicPool: 2026-04-20 유물 시스템 완전 제거

        [Header("무기 뽑기 비용")]
        [SerializeField] private int _weaponSinglePullTicketCost = 1;

        [Header("루비 비용")]
        [SerializeField] private int _singlePullCost = 50;
        [SerializeField] private int _tenPullCost = 450;

        [Header("루비 자동 충전")]
        [Tooltip("분당 자동 충전 루비량")]
        [SerializeField] private int _rubyPerMinute = 30;
        [Tooltip("자동 충전 최대 보유량 (이 이상이면 충전 정지)")]
        [SerializeField] private long _rubyAutoCapLimit = 10000;

        /// <summary>풀별 누적 뽑기 수</summary>
        private readonly Dictionary<GachaPoolType, int> _totalPulls = new();

        /// <summary>마지막 롤에서 결정된 티어 (0=없음, 1~4)</summary>
        private int _lastRolledTier;

        public int SinglePullCost => _singlePullCost;
        public int TenPullCost => _tenPullCost;
        public int RubyPerMinute => _rubyPerMinute;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _totalPulls[GachaPoolType.Equipment] = 0;
            _totalPulls[GachaPoolType.Weapon] = 0;
        }

        private void Start()
        {
            if (_weaponPool == null)
                Debug.LogWarning("[GachaManager] _weaponPool이 null입니다. Phase2Setup 에디터를 다시 실행하세요.");

            RechargeLoopAsync(destroyCancellationToken).Forget();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<QuestStateRefreshEvent>(OnQuestStateRefresh);
            EventBus.SubscribeSticky<BeforeSaveEvent>(OnBeforeSave);
            EventBus.SubscribeSticky<LoadCompletedEvent>(OnLoadCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<QuestStateRefreshEvent>(OnQuestStateRefresh);
            EventBus.Unsubscribe<BeforeSaveEvent>(OnBeforeSave);
            EventBus.Unsubscribe<LoadCompletedEvent>(OnLoadCompleted);
        }

        private void OnBeforeSave(BeforeSaveEvent evt) => SyncToSaveData();

        private void OnLoadCompleted(LoadCompletedEvent evt) => LoadFromSaveData();

        /// <summary>
        /// 가이드 퀘스트 활성화 시 WeaponSummonLevelReach 조건의 현재 상태를 재발행한다.
        /// 도메인 격리로 QuestManager가 직접 참조할 수 없으므로 이벤트 기반 동기화.
        /// </summary>
        private void OnQuestStateRefresh(QuestStateRefreshEvent evt)
        {
            if (evt.Condition != QuestCondition.WeaponSummonLevelReach) return;

            int level = GetSummonLevel(GachaPoolType.Weapon);
            if (level <= 0) return;

            EventBus.Publish(new SummonLevelUpEvent
            {
                PoolName = "Weapon",
                NewLevel = level,
                MaxLevel = GetMaxSummonLevel(GachaPoolType.Weapon)
            });
        }

        /// <summary>
        /// 루비 자동 충전 루프 (분당 _rubyPerMinute).
        /// </summary>
        private async UniTaskVoid RechargeLoopAsync(CancellationToken token)
        {
            if (_rubyPerMinute <= 0) return;

            float interval = 60f / _rubyPerMinute;

            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: token);

                if (CurrencyManager.Instance == null) continue;

                BigNumber current = CurrencyManager.Instance.GetAmount(CurrencyType.Ruby);
                if (current >= _rubyAutoCapLimit) continue;

                CurrencyManager.Instance.Add(CurrencyType.Ruby, 1);
            }
        }

        // ── 소환 레벨 조회 API ──

        /// <summary>풀의 현재 소환 레벨 (1-based)</summary>
        public int GetSummonLevel(GachaPoolType poolType)
        {
            var pool = GetPool(poolType);
            if (pool == null) return 1;
            return pool.GetSummonLevelForPulls(GetTotalPulls(poolType));
        }

        /// <summary>풀의 누적 뽑기 수</summary>
        public int GetTotalPulls(GachaPoolType poolType)
        {
            return _totalPulls.TryGetValue(poolType, out int count) ? count : 0;
        }

        /// <summary>다음 레벨까지 남은 뽑기 수 (-1 = 최대 레벨)</summary>
        public int GetPullsToNextLevel(GachaPoolType poolType)
        {
            var pool = GetPool(poolType);
            if (pool == null) return -1;
            return pool.GetPullsToNextLevel(GetTotalPulls(poolType));
        }

        /// <summary>풀의 최대 소환 레벨</summary>
        public int GetMaxSummonLevel(GachaPoolType poolType)
        {
            var pool = GetPool(poolType);
            return pool != null ? pool.MaxSummonLevel : 1;
        }

        // ── 하위 호환: 기존 API 유지 ──

        /// <summary>기존 천장 API — 소환 레벨 시스템에서는 -1 반환</summary>
        public int GetPityCount(GachaPoolType poolType) => GetTotalPulls(poolType);

        /// <summary>기존 천장 API — 소환 레벨 시스템에서는 다음 레벨까지 남은 횟수</summary>
        public int GetPityRemaining(GachaPoolType poolType) => GetPullsToNextLevel(poolType);

        // ── 뽑기 ──

        /// <summary>1회 뽑기 (루비 소비).</summary>
        public GachaEntry Pull(GachaPoolType poolType)
        {
            if (CurrencyManager.Instance == null) return null;

            if (!CurrencyManager.Instance.Spend(CurrencyType.Ruby, _singlePullCost))
            {
                Debug.LogWarning("[GachaManager] 루비 부족 (1회 뽑기)");
                return null;
            }

            var pool = GetPool(poolType);
            if (pool == null || pool.entries == null || pool.entries.Length == 0) return null;

            IncrementPulls(poolType, 1);

            GachaEntry result = RollWithSummonLevel(pool, poolType);

            PublishResult(result, poolType);
            SaveManager.Instance?.Save();
            return result;
        }

        /// <summary>10연차 뽑기. 마지막 1회는 tenPullGuaranteeGrade 이상 보장.</summary>
        public List<GachaEntry> PullTen(GachaPoolType poolType)
        {
            if (CurrencyManager.Instance == null) return null;

            if (!CurrencyManager.Instance.Spend(CurrencyType.Ruby, _tenPullCost))
            {
                Debug.LogWarning("[GachaManager] 루비 부족 (10연차)");
                return null;
            }

            var pool = GetPool(poolType);
            if (pool == null || pool.entries == null || pool.entries.Length == 0) return null;

            var results = new List<GachaEntry>(10);
            bool hasGuaranteed = false;

            for (int i = 0; i < 10; i++)
            {
                IncrementPulls(poolType, 1);

                GachaEntry result;

                // 10번째이고 아직 보장 등급을 못 뽑았으면 보장
                if (i == 9 && !hasGuaranteed && !string.IsNullOrEmpty(pool.tenPullGuaranteeGrade))
                {
                    result = RollGuaranteed(pool, pool.tenPullGuaranteeGrade, poolType);
                }
                else
                {
                    result = RollWithSummonLevel(pool, poolType);
                }

                // 10연차 보장 등급 이상인지 체크
                if (!hasGuaranteed && !string.IsNullOrEmpty(pool.tenPullGuaranteeGrade))
                {
                    if (IsGradeAtLeast(result.grade, pool.tenPullGuaranteeGrade))
                        hasGuaranteed = true;
                }

                results.Add(result);
                PublishResult(result, poolType);
            }

            SaveManager.Instance?.Save();
            return results;
        }

        /// <summary>무기 1회 뽑기. WeaponTicket 소비.</summary>
        public GachaEntry PullWeapon()
        {
            if (CurrencyManager.Instance == null) return null;

            if (!CurrencyManager.Instance.Spend(CurrencyType.WeaponTicket, _weaponSinglePullTicketCost))
            {
                Debug.LogWarning("[GachaManager] 무기소환권 부족");
                return null;
            }

            var pool = _weaponPool;
            if (pool == null || pool.entries == null || pool.entries.Length == 0) return null;

            IncrementPulls(GachaPoolType.Weapon, 1);

            GachaEntry result = RollWithSummonLevel(pool, GachaPoolType.Weapon);

            PublishResult(result, GachaPoolType.Weapon);
            SaveManager.Instance?.Save();
            return result;
        }

        // ── 서버/로컬 자동 분기 (async) ──

        /// <summary>서버 모드 여부.</summary>
        public bool IsServerMode => ApiClient.HasAuth;

        /// <summary>1회 뽑기 (서버/로컬 자동 분기)</summary>
        public async UniTask<GachaEntry> PullAsync(GachaPoolType poolType, CancellationToken ct)
        {
            if (IsServerMode)
            {
                var results = await PullServerAsync(poolType, 1, ct);
                return results is { Count: > 0 } ? results[0] : null;
            }
            return Pull(poolType);
        }

        /// <summary>10연차 뽑기 (서버/로컬 자동 분기)</summary>
        public async UniTask<List<GachaEntry>> PullTenAsync(GachaPoolType poolType, CancellationToken ct)
        {
            if (IsServerMode)
                return await PullServerAsync(poolType, 10, ct);
            return PullTen(poolType);
        }

        /// <summary>무기 뽑기 (서버/로컬 자동 분기)</summary>
        public async UniTask<GachaEntry> PullWeaponAsync(CancellationToken ct)
        {
            if (IsServerMode)
            {
                var results = await PullServerAsync(GachaPoolType.Weapon, 1, ct);
                return results is { Count: > 0 } ? results[0] : null;
            }
            return PullWeapon();
        }

        // ── 서버 연동 뽑기 (내부) ──

        /// <summary>서버 1회/10연차 뽑기. 서버가 확률 계산 + 재화 차감.</summary>
        public async UniTask<List<GachaEntry>> PullServerAsync(GachaPoolType poolType, int pullCount, CancellationToken ct)
        {
            var request = new GachaPullRequest
            {
                poolType = poolType.ToString(),
                pullCount = pullCount
            };

            var response = await ApiClient.PostAsync<GachaPullResponse>("gacha/pull", request, ct);

            if (!response.success || response.data == null)
            {
                Debug.LogWarning($"[GachaManager] 서버 뽑기 실패: {response.error}");
                return null;
            }

            var serverData = response.data;

            // 서버 결과로 로컬 상태 동기화
            _totalPulls[poolType] = serverData.summonLevel.totalPulls;

            // 재화 동기화 (서버 잔액 반영)
            if (CurrencyManager.Instance != null)
            {
                var rubyType = serverData.currencySpent.type == "Ruby" ? CurrencyType.Ruby : CurrencyType.Ruby;
                CurrencyManager.Instance.SyncAmountFromServer(rubyType, serverData.currencyRemaining);
            }

            // GachaEntry 리스트로 변환
            var pool = GetPool(poolType);
            var results = new List<GachaEntry>();

            foreach (var item in serverData.results)
            {
                // 서버 itemId로 풀에서 매칭 시도
                GachaEntry matched = null;
                if (pool != null && pool.entries != null)
                {
                    foreach (var entry in pool.entries)
                    {
                        if (entry.itemId == item.itemId)
                        {
                            matched = entry;
                            break;
                        }
                    }
                }

                // 매칭 실패 시 임시 엔트리 생성
                if (matched == null)
                {
                    matched = new GachaEntry { itemId = item.itemId, grade = item.grade, weight = 1f };
                }

                results.Add(matched);
                _lastRolledTier = item.tier;
                PublishResult(matched, poolType);
            }

            SaveManager.Instance?.Save();
            return results;
        }

        /// <summary>풀 SO 조회 (UI 확률 표시용 public 접근)</summary>
        public GachaPoolSO GetPool(GachaPoolType type)
        {
            return type switch
            {
                GachaPoolType.Equipment => _equipmentPool,
                GachaPoolType.Weapon => _weaponPool,
                _ => null
            };
        }

        // ── 내부 로직 ──

        /// <summary>누적 뽑기 수 증가 + 레벨업 체크</summary>
        private void IncrementPulls(GachaPoolType poolType, int count)
        {
            int prevPulls = _totalPulls[poolType];
            _totalPulls[poolType] = prevPulls + count;
            int newPulls = _totalPulls[poolType];

            var pool = GetPool(poolType);
            if (pool == null) return;

            int prevLevel = pool.GetSummonLevelForPulls(prevPulls);
            int newLevel = pool.GetSummonLevelForPulls(newPulls);

            if (newLevel > prevLevel)
            {
                Debug.Log($"[GachaManager] {poolType} 소환 레벨업! Lv.{prevLevel} → Lv.{newLevel}");
                EventBus.Publish(new SummonLevelUpEvent
                {
                    PoolName = poolType.ToString(),
                    NewLevel = newLevel,
                    MaxLevel = pool.MaxSummonLevel
                });
            }
        }

        /// <summary>소환 레벨 기반 가중치 뽑기</summary>
        private GachaEntry RollWithSummonLevel(GachaPoolSO pool, GachaPoolType poolType)
        {
            int summonLevel = GetSummonLevel(poolType);

            // 등급+티어 시스템 우선 사용
            var levelData = pool.GetSummonLevelData(summonLevel);
            if (levelData != null && levelData.HasGradeTierWeights)
                return RollWithGradeTierWeights(pool, levelData.gradeTierWeights);

            // 기존 등급별 가중치
            float[] gradeWeights = pool.GetGradeWeightsForLevel(summonLevel);
            if (gradeWeights == null)
                return RollWeighted(pool);

            _lastRolledTier = 0;
            return RollWithGradeWeights(pool, gradeWeights);
        }

        /// <summary>등급+티어 조합 가중치 기반 뽑기 (무기 가챠용)</summary>
        private GachaEntry RollWithGradeTierWeights(GachaPoolSO pool, GradeTierEntry[] weights)
        {
            // 2026-04-23 가드: 빈 배열 / totalWeight=0 fallback
            if (weights == null || weights.Length == 0)
                return RollWeighted(pool);

            // 1단계: 등급+티어 결정
            float totalWeight = 0f;
            for (int i = 0; i < weights.Length; i++)
                totalWeight += weights[i].weight;

            if (totalWeight <= 0f)
                return RollWeighted(pool);

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;
            string selectedGrade = weights[0].grade;
            int selectedTier = weights[0].tier;

            for (int i = 0; i < weights.Length; i++)
            {
                cumulative += weights[i].weight;
                if (roll <= cumulative)
                {
                    selectedGrade = weights[i].grade;
                    selectedTier = weights[i].tier;
                    break;
                }
            }

            _lastRolledTier = selectedTier;

            // 2단계: 해당 등급 내에서 아이템 선택
            var candidates = new List<GachaEntry>();
            float candidateWeight = 0f;

            for (int i = 0; i < pool.entries.Length; i++)
            {
                if (pool.entries[i].grade == selectedGrade)
                {
                    candidates.Add(pool.entries[i]);
                    candidateWeight += pool.entries[i].weight;
                }
            }

            if (candidates.Count == 0 || candidateWeight <= 0f)
                return RollWeighted(pool);

            float itemRoll = UnityEngine.Random.Range(0f, candidateWeight);
            cumulative = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                cumulative += candidates[i].weight;
                if (itemRoll <= cumulative)
                    return candidates[i];
            }

            return candidates[candidates.Count - 1];
        }

        /// <summary>등급별 가중치 배열 기반 뽑기 (장비/유물용 레거시)</summary>
        private GachaEntry RollWithGradeWeights(GachaPoolSO pool, float[] gradeWeights)
        {
            // 1단계: 등급 결정
            float totalGradeWeight = 0f;
            for (int i = 0; i < gradeWeights.Length; i++)
                totalGradeWeight += gradeWeights[i];

            float gradeRoll = UnityEngine.Random.Range(0f, totalGradeWeight);
            float cumulative = 0f;
            string selectedGrade = GachaPoolSO.GradeNames[0];

            for (int i = 0; i < gradeWeights.Length && i < GachaPoolSO.GradeNames.Length; i++)
            {
                cumulative += gradeWeights[i];
                if (gradeRoll <= cumulative)
                {
                    selectedGrade = GachaPoolSO.GradeNames[i];
                    break;
                }
            }

            // 2단계: 해당 등급 내에서 아이템 선택
            var candidates = new List<GachaEntry>();
            float candidateWeight = 0f;

            for (int i = 0; i < pool.entries.Length; i++)
            {
                if (pool.entries[i].grade == selectedGrade)
                {
                    candidates.Add(pool.entries[i]);
                    candidateWeight += pool.entries[i].weight;
                }
            }

            // 해당 등급 아이템이 없으면 전체 풀에서 가중치 뽑기
            if (candidates.Count == 0)
                return RollWeighted(pool);

            // 등급 내 균등 or 가중치 선택
            float itemRoll = UnityEngine.Random.Range(0f, candidateWeight);
            cumulative = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                cumulative += candidates[i].weight;
                if (itemRoll <= cumulative)
                    return candidates[i];
            }

            return candidates[candidates.Count - 1];
        }

        /// <summary>가중치 기반 랜덤 뽑기 (fallback)</summary>
        private GachaEntry RollWeighted(GachaPoolSO pool)
        {
            if (pool.entries == null || pool.entries.Length == 0)
            {
                Debug.LogError("[GachaManager] RollWeighted — pool.entries 비어 있음");
                return null;
            }

            float totalWeight = 0f;
            foreach (var e in pool.entries)
                totalWeight += e.weight;

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var e in pool.entries)
            {
                cumulative += e.weight;
                if (roll <= cumulative)
                    return e;
            }

            return pool.entries[pool.entries.Length - 1];
        }

        /// <summary>보장 등급 이상에서만 뽑기</summary>
        private GachaEntry RollGuaranteed(GachaPoolSO pool, string minGrade, GachaPoolType poolType)
        {
            int summonLevel = GetSummonLevel(poolType);
            int minIdx = GradeToInt(minGrade);

            // 등급+티어 시스템인 경우 보장 등급 이상 항목만 필터
            var levelData = pool.GetSummonLevelData(summonLevel);
            if (levelData != null && levelData.HasGradeTierWeights)
            {
                var filtered = new List<GradeTierEntry>();
                for (int i = 0; i < levelData.gradeTierWeights.Length; i++)
                {
                    if (GradeToInt(levelData.gradeTierWeights[i].grade) >= minIdx)
                        filtered.Add(levelData.gradeTierWeights[i]);
                }

                if (filtered.Count > 0)
                    return RollWithGradeTierWeights(pool, filtered.ToArray());
            }

            // 기존 등급별 가중치
            float[] gradeWeights = pool.GetGradeWeightsForLevel(summonLevel);

            if (gradeWeights != null)
            {
                // 보장 등급 이상의 가중치만 사용
                float[] filteredGrade = new float[gradeWeights.Length];
                for (int i = 0; i < gradeWeights.Length; i++)
                    filteredGrade[i] = i >= minIdx ? gradeWeights[i] : 0f;

                return RollWithGradeWeights(pool, filteredGrade);
            }

            // fallback: 기존 방식
            float totalWeight = 0f;
            var candidates = new List<GachaEntry>();

            foreach (var e in pool.entries)
            {
                if (IsGradeAtLeast(e.grade, minGrade))
                {
                    candidates.Add(e);
                    totalWeight += e.weight;
                }
            }

            if (candidates.Count == 0)
            {
                Debug.LogWarning($"[GachaManager] {minGrade} 이상 항목 없음 — 일반 뽑기 수행");
                return RollWithSummonLevel(pool, poolType);
            }

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var e in candidates)
            {
                cumulative += e.weight;
                if (roll <= cumulative)
                    return e;
            }

            return candidates[candidates.Count - 1];
        }

        private void PublishResult(GachaEntry entry, GachaPoolType poolType)
        {
            if (entry == null) return;

            int tier = _lastRolledTier;

            EventBus.Publish(new GachaResultEvent
            {
                ItemId = entry.itemId,
                Grade = entry.grade,
                PoolName = poolType.ToString(),
                Tier = tier
            });

            string tierStr = tier > 0 ? $" T{tier}" : "";
            Debug.Log($"[GachaManager] {poolType} 뽑기 결과: {entry.itemId} ({entry.grade}{tierStr})");
        }

        /// <summary>
        /// 등급 비교. grade가 minGrade 이상인지 반환.
        /// Normal < Rare < Epic < Unique < Legendary < Mythic
        /// </summary>
        private static bool IsGradeAtLeast(string grade, string minGrade)
        {
            return GradeToInt(grade) >= GradeToInt(minGrade);
        }

        private static int GradeToInt(string grade)
        {
            return grade switch
            {
                "Normal" => 0,
                "Rare" => 1,
                "Epic" => 2,
                "Unique" => 3,
                "Legendary" => 4,
                "Mythic" => 5,
                "Ancient" => 6,
                _ => 0
            };
        }

        // ── 세이브 규약 (BeforeSaveEvent + LoadCompletedEvent) ──

        private void LoadFromSaveData()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null) return;

            var data = SaveManager.Instance.CurrentData.gachaPity;
            if (data == null) return;

            if (data.isMigrated)
            {
                _totalPulls[GachaPoolType.Equipment] = data.equipmentTotalPulls;
                _totalPulls[GachaPoolType.Weapon] = data.weaponTotalPulls;
            }
            else
            {
                // 기존 pity → 소환 레벨 1회 마이그레이션 (디스크 즉시 반영)
                _totalPulls[GachaPoolType.Equipment] = data.equipmentPity;
                _totalPulls[GachaPoolType.Weapon] = data.weaponPity;

                data.isMigrated = true;
                data.equipmentTotalPulls = data.equipmentPity;
                data.weaponTotalPulls = data.weaponPity;

                SaveManager.Instance.Save();
                Debug.Log("[GachaManager] 기존 pity 데이터를 소환 레벨 시스템으로 마이그레이션 완료");
            }

            Debug.Log($"[GachaManager] 소환 데이터 로드 — 장비:{_totalPulls[GachaPoolType.Equipment]} 무기:{_totalPulls[GachaPoolType.Weapon]}");
        }

        /// <summary>
        /// 런타임 상태를 CurrentData.gachaPity에 기록한다. BeforeSaveEvent에서 호출된다.
        /// 디스크 write는 SaveManager가 담당하므로 여기서는 CurrentData만 갱신.
        /// </summary>
        private void SyncToSaveData()
        {
            if (SaveManager.Instance == null || SaveManager.Instance.CurrentData == null) return;

            var data = SaveManager.Instance.CurrentData.gachaPity;
            if (data == null)
            {
                data = new GachaPitySaveData();
                SaveManager.Instance.CurrentData.gachaPity = data;
            }

            data.isMigrated = true;
            data.equipmentTotalPulls = _totalPulls[GachaPoolType.Equipment];
            data.weaponTotalPulls = _totalPulls[GachaPoolType.Weapon];

            // 하위 호환: pity 필드도 동기화
            data.equipmentPity = data.equipmentTotalPulls;
            data.weaponPity = data.weaponTotalPulls;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
