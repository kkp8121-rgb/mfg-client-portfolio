using System.Collections.Generic;
using UnityEngine;
using MkLike.Core;
using MkLike.Core.Save;
using MkLike.Data;
using MkLike.Economy;
using MkLike.Utils;

namespace MkLike.Combat
{
    /// <summary>
    /// 어빌리티 시스템.
    /// 영웅의 힘 하위 시스템 — 3갈래 선택형 특성 트리.
    /// 해금: 레벨 113 (가이드 퀘스트로 유도).
    /// </summary>
    public class AbilityManager : MonoBehaviour
    {
        public static AbilityManager Instance { get; private set; }

        [Header("어빌리티 설정")]
        [SerializeField] private AbilityTreeConfigSO _config;

        /// <summary>시스템 해금 여부</summary>
        public bool IsUnlocked => _isUnlocked;
        private bool _isUnlocked;

        /// <summary>사용 가능한 포인트</summary>
        public int AvailablePoints => _availablePoints;
        private int _availablePoints;

        /// <summary>해금된 노드 수</summary>
        public int UnlockedCount => _unlockedNodeIds.Count;

        private readonly HashSet<string> _unlockedNodeIds = new();

        /// <summary>설정 SO</summary>
        public AbilityTreeConfigSO Config => _config;

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
            EventBus<LevelUpEvent>.Subscribe(OnLevelUp);
            EventBus<QuestStateRefreshEvent>.Subscribe(OnQuestStateRefresh);
        }

        private void OnDisable()
        {
            EventBus<LevelUpEvent>.Unsubscribe(OnLevelUp);
            EventBus<QuestStateRefreshEvent>.Unsubscribe(OnQuestStateRefresh);
        }

        /// <summary>
        /// 가이드 퀘스트 활성화 시 UnlockAbility 조건의 현재 상태를 재발행한다.
        /// 누적 해금 수를 TotalUnlocked로 통지 (QuestManager는 이 값을 SetProgress에 사용).
        /// </summary>
        private void OnQuestStateRefresh(QuestStateRefreshEvent evt)
        {
            if (evt.Condition != QuestCondition.UnlockAbility) return;
            if (_unlockedNodeIds.Count <= 0) return;

            string lastNodeId = null;
            int lastBranchIndex = 0;
            foreach (var nodeId in _unlockedNodeIds)
            {
                lastNodeId = nodeId;
                var node = _config != null ? _config.FindNode(nodeId) : null;
                if (node != null) lastBranchIndex = node.branchIndex;
            }

            EventBus.Publish(new AbilityUnlockedEvent
            {
                NodeId = lastNodeId,
                BranchIndex = lastBranchIndex,
                TotalUnlocked = _unlockedNodeIds.Count
            });
        }

        private void OnLevelUp(LevelUpEvent evt)
        {
            CheckUnlock();
            // 레벨업 시 포인트 지급 (해금 이후)
            if (_isUnlocked)
            {
                _availablePoints++;
            }
        }

        private void CheckUnlock()
        {
            if (_isUnlocked) return;
            if (_config == null) return;

            var levelSystem = Object.FindFirstObjectByType<LevelSystem>();
            if (levelSystem == null) return;

            if (levelSystem.CurrentLevel >= _config.UnlockLevel)
            {
                _isUnlocked = true;
                _availablePoints = 3; // 초기 포인트 지급
                Debug.Log("[AbilityManager] 어빌리티 시스템 해금! (초기 3포인트)");
            }
        }

        /// <summary>노드를 해금한다.</summary>
        public bool UnlockNode(string nodeId)
        {
            if (!_isUnlocked) return false;
            if (_unlockedNodeIds.Contains(nodeId)) return false;

            var node = _config.FindNode(nodeId);
            if (node == null) return false;

            // 선행 조건 체크
            if (!string.IsNullOrEmpty(node.prerequisiteNodeId) && !_unlockedNodeIds.Contains(node.prerequisiteNodeId))
            {
                Debug.LogWarning($"[AbilityManager] 선행 노드 미해금: {node.prerequisiteNodeId}");
                return false;
            }

            // 포인트 체크
            if (_availablePoints < node.cost)
            {
                Debug.LogWarning("[AbilityManager] 어빌리티 포인트 부족");
                return false;
            }

            _availablePoints -= node.cost;
            _unlockedNodeIds.Add(nodeId);
            ApplyNodeBonus(node);

            EventBus.Publish(new AbilityUnlockedEvent
            {
                NodeId = nodeId,
                BranchIndex = node.branchIndex,
                TotalUnlocked = _unlockedNodeIds.Count
            });

            Debug.Log($"[AbilityManager] 어빌리티 해금: {node.displayName} ({node.bonusStat} +{node.bonusPercent}%)");
            return true;
        }

        /// <summary>모든 어빌리티를 리셋한다.</summary>
        public bool ResetAll()
        {
            if (!_isUnlocked) return false;
            if (_unlockedNodeIds.Count == 0) return false;

            var cm = CurrencyManager.Instance;
            if (cm == null) return false;

            if (!cm.Spend(CurrencyType.Gold, _config.ResetCostGold))
                return false;

            // 포인트 환불
            int refunded = 0;
            foreach (var nodeId in _unlockedNodeIds)
            {
                var node = _config.FindNode(nodeId);
                if (node != null) refunded += node.cost;
            }

            // 보너스 제거
            var player = Object.FindFirstObjectByType<PlayerCharacter>();
            if (player != null)
            {
                var stats = player.GetComponent<CombatStats>();
                if (stats != null)
                    stats.ClearModifiers(ModifierSource.Mastery); // Ability도 Mastery 소스 사용
            }

            _unlockedNodeIds.Clear();
            _availablePoints += refunded;

            EventBus.Publish(new AbilityResetEvent { RefundedPoints = refunded });
            Debug.Log($"[AbilityManager] 어빌리티 리셋 완료: {refunded}포인트 환불");
            return true;
        }

        /// <summary>노드 해금 여부</summary>
        public bool IsNodeUnlocked(string nodeId) => _unlockedNodeIds.Contains(nodeId);

        /// <summary>해금된 노드 ID 목록</summary>
        public IReadOnlyCollection<string> UnlockedNodeIds => _unlockedNodeIds;

        private void ApplyNodeBonus(AbilityNodeSO node)
        {
            var player = Object.FindFirstObjectByType<PlayerCharacter>();
            if (player == null) return;

            var stats = player.GetComponent<CombatStats>();
            if (stats == null) return;

            string modKey = $"ability_{node.id}";
            stats.AddModifier(modKey, new StatModifier
            {
                source = ModifierSource.Mastery,
                sourceId = modKey,
                statType = node.bonusStat,
                flatBonus = node.bonusFlat,
                percentBonus = node.bonusPercent / 100f
            });
            stats.SetCpReason("어빌리티");
        }

        /// <summary>세이브 데이터 로드</summary>
        public void LoadFromSave(AbilitySaveData data)
        {
            if (data == null) return;
            _availablePoints = data.availablePoints;
            _unlockedNodeIds.Clear();

            foreach (var nodeId in data.unlockedNodeIds)
            {
                _unlockedNodeIds.Add(nodeId);
                var node = _config.FindNode(nodeId);
                if (node != null) ApplyNodeBonus(node);
            }
        }

        /// <summary>세이브 데이터 생성</summary>
        public AbilitySaveData ToSaveData()
        {
            var data = new AbilitySaveData
            {
                availablePoints = _availablePoints
            };
            data.unlockedNodeIds.AddRange(_unlockedNodeIds);
            return data;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
