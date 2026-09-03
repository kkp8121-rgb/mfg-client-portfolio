using UnityEngine;
using MkLike.Core;

namespace MkLike.Data
{
    /// <summary>
    /// 어빌리티 트리 노드 정의.
    /// 3갈래 트리(공격/방어/유틸)의 개별 노드.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAbilityNode", menuName = "MkLike/Data/Ability Node")]
    public class AbilityNodeSO : ScriptableObject
    {
        [Header("기본 정보")]
        public string id;
        public string displayName;
        public string description;
        public Sprite icon;

        [Header("트리 구조")]
        [Tooltip("0=공격, 1=방어, 2=유틸")]
        public int branchIndex;
        [Tooltip("트리 내 깊이 (0=루트)")]
        public int depth;
        [Tooltip("해금에 필요한 선행 노드 ID (없으면 루트)")]
        public string prerequisiteNodeId;
        [Tooltip("해금에 필요한 어빌리티 포인트")]
        public int cost = 1;

        [Header("효과")]
        public StatType bonusStat;
        [Tooltip("퍼센트 보너스 (5 = +5%)")]
        public float bonusPercent;
        [Tooltip("고정 보너스")]
        public float bonusFlat;
    }

    /// <summary>
    /// 어빌리티 트리 전체 설정.
    /// </summary>
    [CreateAssetMenu(fileName = "AbilityTreeConfig", menuName = "MkLike/Data/Ability Tree Config")]
    public class AbilityTreeConfigSO : ScriptableObject
    {
        [Header("해금 조건")]
        [Tooltip("어빌리티 시스템 해금 레벨")]
        [SerializeField] private int _unlockLevel = 113;

        [Header("트리 노드")]
        [SerializeField] private AbilityNodeSO[] _nodes;

        [Header("리셋 비용")]
        [SerializeField] private int _resetCostGold = 50000;

        public int UnlockLevel => _unlockLevel;
        public AbilityNodeSO[] Nodes => _nodes;
        public int ResetCostGold => _resetCostGold;

        /// <summary>ID로 노드 검색</summary>
        public AbilityNodeSO FindNode(string id)
        {
            if (_nodes == null) return null;
            for (int i = 0; i < _nodes.Length; i++)
            {
                if (_nodes[i] != null && _nodes[i].id == id)
                    return _nodes[i];
            }
            return null;
        }

        /// <summary>특정 브랜치의 노드들</summary>
        public void GetBranchNodes(int branchIndex, System.Collections.Generic.List<AbilityNodeSO> result)
        {
            result.Clear();
            if (_nodes == null) return;
            for (int i = 0; i < _nodes.Length; i++)
            {
                if (_nodes[i] != null && _nodes[i].branchIndex == branchIndex)
                    result.Add(_nodes[i]);
            }
        }
    }
}
