using System.Collections.Generic;
using UnityEngine;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Combat;
using MkLike.Data;
using MkLike.Utils;

namespace MkLike.Growth
{
    /// <summary>
    /// 마스터리 트리 시스템.
    /// 4차 전직 달성 시 마스터리 포인트를 획득하고, 3분기 x 3단계 노드를 해금하여
    /// 영구 스탯 보너스를 얻는다. 리스펙 시 등반의 증표(ClimbToken)를 소모한다.
    /// </summary>
    public class MasteryManager : MonoBehaviour
    {
        public static MasteryManager Instance { get; private set; }

        [Header("마스터리 데이터")]
        [SerializeField] private MasteryDataSO[] _catalog;

        [Header("리스펙 비용")]
        [SerializeField] private int _respecClimbTokenCost = 50;

        /// <summary>리스펙 시 등반의 증표 소모량</summary>
        public int RespecCost => _respecClimbTokenCost;

        /// <summary>사용 가능한 마스터리 포인트</summary>
        public int AvailablePoints { get; private set; }

        /// <summary>총 획득한 마스터리 포인트</summary>
        public int TotalEarnedPoints { get; private set; }

        private readonly HashSet<string> _unlockedNodes = new();
        private CombatStats _playerStats;

        /// <summary>해금된 노드 ID 집합 (읽기 전용)</summary>
        public IReadOnlyCollection<string> UnlockedNodes => _unlockedNodes;

        /// <summary>전체 카탈로그</summary>
        public MasteryDataSO[] Catalog => _catalog;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<JobChangedEvent>(OnJobChanged);
            EventBus.Subscribe<LevelUpEvent>(OnLevelUp);
            EventBus.SubscribeSticky<BeforeSaveEvent>(OnBeforeSave);
            EventBus.SubscribeSticky<LoadCompletedEvent>(OnLoadCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<JobChangedEvent>(OnJobChanged);
            EventBus.Unsubscribe<LevelUpEvent>(OnLevelUp);
            EventBus.Unsubscribe<BeforeSaveEvent>(OnBeforeSave);
            EventBus.Unsubscribe<LoadCompletedEvent>(OnLoadCompleted);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnBeforeSave(BeforeSaveEvent evt)
        {
            SyncToSaveData();
        }

        private void OnLoadCompleted(LoadCompletedEvent evt)
        {
            LoadFromSaveData();
        }

        /// <summary>
        /// SaveData에서 마스터리 상태를 복원한다. LoadCompletedEvent 구독으로 자동 호출.
        /// </summary>
        private void LoadFromSaveData()
        {
            var saveData = SaveManager.Instance?.CurrentData;
            if (saveData == null)
            {
                Debug.Log("[MasteryManager] SaveData 없음 — 초기 상태");
                return;
            }

            var data = saveData.mastery;
            AvailablePoints = data.availablePoints;
            TotalEarnedPoints = data.totalEarnedPoints;

            _unlockedNodes.Clear();
            for (int i = 0; i < data.unlockedNodes.Count; i++)
            {
                _unlockedNodes.Add(data.unlockedNodes[i]);
            }

            // 복원 시 modifier 재적용
            ReapplyAllModifiers();

            Debug.Log($"[MasteryManager] 초기화 완료: 해금 {_unlockedNodes.Count}개, 잔여 포인트 {AvailablePoints}");
        }

        /// <summary>
        /// 마스터리 노드를 해금한다.
        /// </summary>
        /// <param name="masteryId">해금할 마스터리 ID</param>
        /// <returns>성공 여부</returns>
        public bool UnlockNode(string masteryId)
        {
            if (_unlockedNodes.Contains(masteryId))
                return false;

            var data = FindMastery(masteryId);
            if (data == null)
            {
                Debug.LogWarning($"[MasteryManager] 마스터리 '{masteryId}' 를 찾을 수 없음");
                return false;
            }

            // 직업 체크
            if (JobSystem.Instance != null && JobSystem.Instance.CurrentJob != data.requiredJob)
                return false;

            // 4차 전직 체크
            if (JobSystem.Instance != null && JobSystem.Instance.CurrentTier < 4)
                return false;

            // 선행 노드 체크
            if (!string.IsNullOrEmpty(data.prerequisiteId) && !_unlockedNodes.Contains(data.prerequisiteId))
                return false;

            // 포인트 체크
            if (AvailablePoints < data.pointCost)
                return false;

            AvailablePoints -= data.pointCost;
            _unlockedNodes.Add(masteryId);

            ApplyNodeModifier(data);
            SaveManager.Instance?.Save();

            EventBus.Publish(new MasteryUnlockedEvent
            {
                MasteryId = masteryId,
                BranchIndex = data.branchIndex,
                NodeLevel = data.nodeLevel,
                BonusStat = data.bonusStat
            });

            // 2026-04-23 FeedbackBus 누락 보완: 마스터리 해금은 사용자 비주얼 액션 → Toast 필수
            FeedbackBus.Emit(FeedbackKind.Generic, $"마스터리 해금: {data.displayName}", 2);

            // 2026-04-23 SFX 보완: 마스터리 해금 오디오
            AudioManager.Instance?.PlaySfx(SfxType.MasteryUnlock);

            Debug.Log($"[MasteryManager] 마스터리 해금: {data.displayName} (잔여 포인트: {AvailablePoints})");
            return true;
        }

        /// <summary>
        /// 마스터리를 리스펙한다. 등반의 증표를 소모하고 모든 노드를 초기화한다.
        /// </summary>
        /// <returns>성공 여부</returns>
        public bool Respec()
        {
            if (_unlockedNodes.Count == 0)
                return false;

            // 등반의 증표 소모
            var currencyMgr = FindCurrencyManager();
            if (currencyMgr == null)
                return false;

            if (!currencyMgr.Spend(CurrencyType.ClimbToken, _respecClimbTokenCost))
                return false;

            int refundedPoints = 0;
            for (int i = 0; i < _catalog.Length; i++)
            {
                if (_unlockedNodes.Contains(_catalog[i].id))
                    refundedPoints += _catalog[i].pointCost;
            }

            // modifier 전체 제거
            CachePlayerStats();
            if (_playerStats != null)
            {
                _playerStats.SetCpReason("마스터리");
                _playerStats.ClearModifiers(ModifierSource.Mastery);
            }

            _unlockedNodes.Clear();
            AvailablePoints += refundedPoints;
            SaveManager.Instance?.Save();

            EventBus.Publish(new MasteryRespecEvent
            {
                RefundedPoints = refundedPoints,
                ClimbTokenCost = _respecClimbTokenCost
            });

            // 2026-04-23 FeedbackBus 누락 보완: 리스펙은 증표 소모 액션 → 결과 Toast 필수
            FeedbackBus.Emit(FeedbackKind.Generic, $"마스터리 리스펙 완료: +{refundedPoints}P 환불", 2);

            // 2026-04-23 SFX 보완: 리스펙은 포인트 환불 → UiReward
            AudioManager.Instance?.PlaySfx(SfxType.UiReward);

            Debug.Log($"[MasteryManager] 리스펙 완료: {refundedPoints}포인트 환불, 등반의 증표 {_respecClimbTokenCost} 소모");
            return true;
        }

        /// <summary>
        /// 마스터리 포인트를 추가한다 (4차 전직/레벨업 보상 등).
        /// </summary>
        public void AddPoints(int amount)
        {
            if (amount <= 0) return;
            AvailablePoints += amount;
            TotalEarnedPoints += amount;
            SaveManager.Instance?.Save();
            Debug.Log($"[MasteryManager] 포인트 +{amount} (총 {AvailablePoints})");
        }

        /// <summary>
        /// 특정 노드가 해금 가능한지 확인한다.
        /// </summary>
        public bool CanUnlock(string masteryId)
        {
            if (_unlockedNodes.Contains(masteryId))
                return false;

            var data = FindMastery(masteryId);
            if (data == null) return false;

            if (JobSystem.Instance != null && JobSystem.Instance.CurrentJob != data.requiredJob)
                return false;

            if (JobSystem.Instance != null && JobSystem.Instance.CurrentTier < 4)
                return false;

            if (!string.IsNullOrEmpty(data.prerequisiteId) && !_unlockedNodes.Contains(data.prerequisiteId))
                return false;

            return AvailablePoints >= data.pointCost;
        }

        /// <summary>
        /// 특정 노드가 이미 해금되었는지 확인한다.
        /// </summary>
        public bool IsUnlocked(string masteryId)
        {
            return _unlockedNodes.Contains(masteryId);
        }

        // ── 이벤트 핸들러 ──

        private void OnJobChanged(JobChangedEvent evt)
        {
            // 4차 전직 달성 시 초기 마스터리 포인트 지급
            if (evt.CurrentTier == 4 && evt.PreviousTier == 3)
            {
                AddPoints(3);
                Debug.Log("[MasteryManager] 4차 전직 달성 — 마스터리 포인트 3 지급");
            }

            // 직업 변경 시 modifier 재적용 (직업별 마스터리 다름)
            CachePlayerStats();
            if (_playerStats != null)
            {
                _playerStats.SetCpReason("마스터리");
                _playerStats.ClearModifiers(ModifierSource.Mastery);
            }

            ReapplyAllModifiers();
        }

        private void OnLevelUp(LevelUpEvent evt)
        {
            // 160 이후 10레벨마다 마스터리 포인트 1 추가
            if (JobSystem.Instance == null || JobSystem.Instance.CurrentTier < 4)
                return;

            if (evt.CurrentLevel > 160 && evt.CurrentLevel % 10 == 0)
            {
                AddPoints(1);
            }
        }

        // ── 내부 메서드 ──

        private void ApplyNodeModifier(MasteryDataSO data)
        {
            CachePlayerStats();
            if (_playerStats == null) return;

            string modKey = $"mastery_{data.id}";
            string sourceId = $"mastery_node_{data.id}";

            _playerStats.SetCpReason("마스터리");
            _playerStats.AddModifier(modKey, new StatModifier(
                ModifierSource.Mastery, sourceId, data.bonusStat,
                data.flatBonus, data.percentBonus));
        }

        private void ReapplyAllModifiers()
        {
            CachePlayerStats();
            if (_playerStats == null) return;

            var currentJob = JobSystem.Instance?.CurrentJob ?? JobType.Warrior;

            for (int i = 0; i < _catalog.Length; i++)
            {
                var data = _catalog[i];
                if (data.requiredJob == currentJob && _unlockedNodes.Contains(data.id))
                {
                    ApplyNodeModifier(data);
                }
            }
        }

        private void CachePlayerStats()
        {
            if (_playerStats == null)
            {
                var player = FindFirstObjectByType<PlayerCharacter>();
                if (player != null)
                    _playerStats = player.GetComponent<CombatStats>();
            }
        }

        private MasteryDataSO FindMastery(string id)
        {
            if (_catalog == null) return null;
            for (int i = 0; i < _catalog.Length; i++)
            {
                if (_catalog[i].id == id)
                    return _catalog[i];
            }
            return null;
        }

        /// <summary>
        /// 런타임 상태를 CurrentData.mastery에 기록한다. BeforeSaveEvent에서 호출된다.
        /// 디스크 write는 SaveManager가 담당하므로 여기서는 CurrentData만 갱신.
        /// </summary>
        private void SyncToSaveData()
        {
            var saveData = SaveManager.Instance?.CurrentData;
            if (saveData == null) return;

            var data = saveData.mastery;
            data.availablePoints = AvailablePoints;
            data.totalEarnedPoints = TotalEarnedPoints;
            data.unlockedNodes.Clear();
            foreach (var nodeId in _unlockedNodes)
                data.unlockedNodes.Add(nodeId);
        }

        private Economy.CurrencyManager FindCurrencyManager()
        {
            return FindFirstObjectByType<Economy.CurrencyManager>();
        }
    }
}
