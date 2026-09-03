using System.Collections.Generic;
using UnityEngine;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Data;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 부스터 시스템.
    /// 시간제 버프 아이템을 관리한다.
    /// 해금: 레벨 89 (가이드 퀘스트로 유도).
    /// 레퍼런스: 메이플 키우기 — 온라인 사냥 시 EXP/골드/드롭률 배율 적용.
    /// </summary>
    public class BoosterManager : MonoBehaviour
    {
        public static BoosterManager Instance { get; private set; }

        [Header("부스터 카탈로그")]
        [SerializeField] private BoosterDataSO[] _boosterCatalog;

        [Header("해금 조건")]
        [SerializeField] private int _unlockLevel = 89;

        /// <summary>시스템 해금 여부</summary>
        public bool IsUnlocked => _isUnlocked;
        private bool _isUnlocked;

        /// <summary>총 부스터 사용 횟수</summary>
        public int TotalUsed => _totalUsed;
        private int _totalUsed;

        // 활성 부스터 (boosterId → 남은 시간)
        private readonly Dictionary<string, ActiveBooster> _activeBoosters = new();
        private readonly List<string> _expiredKeys = new();
        private readonly List<string> _keyBuffer = new();

        // 보유 수량 (BoosterType index → count)
        private readonly int[] _stock = new int[3];

        private struct ActiveBooster
        {
            public BoosterDataSO data;
            public float remainingSeconds;
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

        private void Start()
        {
            CheckUnlock();
        }

        // 2026-04-23 QA 감사 P1: Start에서 OnEnable로 Subscribe 이동 — OnDisable Unsubscribe와 쌍 유지 (Scene 재활성 시 먹통 방지)
        private void OnEnable()
        {
            EventBus<LevelUpEvent>.Subscribe(OnLevelUp);
            EventBus<BeforeSaveEvent>.SubscribeSticky(OnBeforeSave);
            EventBus<LoadCompletedEvent>.SubscribeSticky(OnLoadCompleted);
            EventBus<BoosterStockAddEvent>.Subscribe(OnBoosterStockAdd);
        }

        private void OnDisable()
        {
            EventBus<LevelUpEvent>.Unsubscribe(OnLevelUp);
            EventBus<BeforeSaveEvent>.Unsubscribe(OnBeforeSave);
            EventBus<LoadCompletedEvent>.Unsubscribe(OnLoadCompleted);
            EventBus<BoosterStockAddEvent>.Unsubscribe(OnBoosterStockAdd);
        }

        private void OnBoosterStockAdd(BoosterStockAddEvent evt)
        {
            AddStock(evt.Type, evt.Amount);
        }

        private void Update()
        {
            if (_activeBoosters.Count == 0) return;

            float dt = Time.deltaTime;
            _expiredKeys.Clear();

            _keyBuffer.Clear();
            _keyBuffer.AddRange(_activeBoosters.Keys);
            for (int i = 0; i < _keyBuffer.Count; i++)
            {
                string key = _keyBuffer[i];
                var booster = _activeBoosters[key];
                booster.remainingSeconds -= dt;

                if (booster.remainingSeconds <= 0f)
                {
                    _expiredKeys.Add(key);
                    EventBus.Publish(new BoosterExpiredEvent
                    {
                        BoosterId = key,
                        Type = booster.data.boosterType
                    });
                    Debug.Log($"[BoosterManager] {booster.data.displayName} 만료");
                }
                else
                {
                    _activeBoosters[key] = booster;
                }
            }

            for (int i = 0; i < _expiredKeys.Count; i++)
                _activeBoosters.Remove(_expiredKeys[i]);
        }

        // ── 보유 수량 관리 ──

        /// <summary>해당 타입의 보유 수량을 반환한다.</summary>
        public int GetStock(BoosterType type) => _stock[(int)type];

        /// <summary>부스터를 추가한다 (보상, 퀘스트, 출석 등).</summary>
        public void AddStock(BoosterType type, int amount)
        {
            if (amount <= 0) return;
            _stock[(int)type] += amount;
            Debug.Log($"[BoosterManager] {type} +{amount} (보유: {_stock[(int)type]})");
        }

        // ── 부스터 사용 ──

        /// <summary>부스터를 사용한다. 보유 수량 1개 차감 후 활성화.</summary>
        public bool UseBooster(string boosterId)
        {
            if (!_isUnlocked)
            {
                Debug.LogWarning("[BoosterManager] 부스터 미해금");
                return false;
            }

            var data = FindBoosterData(boosterId);
            if (data == null)
            {
                Debug.LogWarning($"[BoosterManager] 부스터 없음: {boosterId}");
                return false;
            }

            // 보유 수량 확인
            int typeIdx = (int)data.boosterType;
            if (_stock[typeIdx] <= 0)
            {
                Debug.LogWarning($"[BoosterManager] {data.displayName} 보유 수량 부족");
                return false;
            }

            // 수량 차감
            _stock[typeIdx]--;

            // 같은 타입 이미 활성이면 시간 연장
            if (_activeBoosters.TryGetValue(boosterId, out var existing))
            {
                existing.remainingSeconds += data.durationSeconds;
                _activeBoosters[boosterId] = existing;
                Debug.Log($"[BoosterManager] {data.displayName} 시간 연장 (+{data.DurationMinutes}분)");
            }
            else
            {
                _activeBoosters[boosterId] = new ActiveBooster
                {
                    data = data,
                    remainingSeconds = data.durationSeconds
                };
                Debug.Log($"[BoosterManager] {data.displayName} 활성화 ({data.DurationMinutes}분)");
            }

            _totalUsed++;

            EventBus.Publish(new BoosterUsedEvent
            {
                BoosterId = boosterId,
                Type = data.boosterType,
                Multiplier = data.multiplier,
                DurationSeconds = data.durationSeconds
            });

            // 2026-04-23 이슈 15 FeedbackBus 적용: 부스터 활성화 Toast + 약한 쉐이크
            int mins = Mathf.RoundToInt(data.durationSeconds / 60f);
            MkLike.Core.FeedbackBus.Emit(
                MkLike.Core.FeedbackKind.Booster,
                $"{data.displayName} x{data.multiplier:F1} ({mins}분) 활성화!",
                shakeIntensity: 1);

            return true;
        }

        /// <summary>특정 부스터 타입의 현재 배율 (비활성 시 1.0)</summary>
        public float GetMultiplier(BoosterType type)
        {
            float maxMultiplier = 1f;
            foreach (var kvp in _activeBoosters)
            {
                if (kvp.Value.data.boosterType == type && kvp.Value.data.multiplier > maxMultiplier)
                    maxMultiplier = kvp.Value.data.multiplier;
            }
            return maxMultiplier;
        }

        /// <summary>특정 부스터의 남은 시간 (초)</summary>
        public float GetRemainingSeconds(string boosterId)
        {
            if (_activeBoosters.TryGetValue(boosterId, out var booster))
                return booster.remainingSeconds;
            return 0f;
        }

        /// <summary>활성 부스터 목록 (UI용)</summary>
        public void GetActiveBoosters(List<(BoosterDataSO data, float remaining)> result)
        {
            result.Clear();
            foreach (var kvp in _activeBoosters)
                result.Add((kvp.Value.data, kvp.Value.remainingSeconds));
        }

        /// <summary>활성 부스터 수</summary>
        public int ActiveCount => _activeBoosters.Count;

        /// <summary>부스터 카탈로그</summary>
        public BoosterDataSO[] Catalog => _boosterCatalog;

        private BoosterDataSO FindBoosterData(string boosterId)
        {
            if (_boosterCatalog == null) return null;
            for (int i = 0; i < _boosterCatalog.Length; i++)
            {
                if (_boosterCatalog[i] != null && _boosterCatalog[i].id == boosterId)
                    return _boosterCatalog[i];
            }
            return null;
        }

        // ── 세이브/로드 ──

        private void OnBeforeSave(BeforeSaveEvent evt)
        {
            var save = SaveManager.Instance?.CurrentData;
            if (save == null) return;
            save.booster = ToSaveData();
        }

        private void OnLoadCompleted(LoadCompletedEvent evt)
        {
            var save = SaveManager.Instance?.CurrentData;
            if (save == null) return;
            LoadFromSave(save.booster);
            CheckUnlock();
        }

        public void LoadFromSave(BoosterSaveData data)
        {
            if (data == null) return;
            _totalUsed = data.totalUsed;
            _activeBoosters.Clear();

            // 보유 수량 복원
            for (int i = 0; i < 3; i++)
                _stock[i] = i < data.stock.Count ? data.stock[i] : 0;

            foreach (var entry in data.activeBoosters)
            {
                var boosterData = FindBoosterData(entry.boosterId);
                if (boosterData != null && entry.remainingSeconds > 0)
                {
                    _activeBoosters[entry.boosterId] = new ActiveBooster
                    {
                        data = boosterData,
                        remainingSeconds = entry.remainingSeconds
                    };
                }
            }
        }

        public BoosterSaveData ToSaveData()
        {
            var data = new BoosterSaveData { totalUsed = _totalUsed };

            // 보유 수량 저장
            data.stock.Clear();
            for (int i = 0; i < 3; i++)
                data.stock.Add(_stock[i]);

            foreach (var kvp in _activeBoosters)
            {
                data.activeBoosters.Add(new ActiveBoosterEntry
                {
                    boosterId = kvp.Key,
                    remainingSeconds = kvp.Value.remainingSeconds
                });
            }
            return data;
        }

        private void OnLevelUp(LevelUpEvent evt)
        {
            CheckUnlock();
        }

        private void CheckUnlock()
        {
            if (_isUnlocked) return;
            var levelSystem = Object.FindFirstObjectByType<LevelSystem>();
            if (levelSystem == null) return;

            if (levelSystem.CurrentLevel >= _unlockLevel)
            {
                _isUnlocked = true;
                Debug.Log("[BoosterManager] 부스터 시스템 해금!");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
