using UnityEngine;
using MkLike.Core;

namespace MkLike.Data
{
    [System.Serializable]
    public class LootEntry
    {
        public CurrencyType currencyType;
        [Range(0f, 1f)]
        public float dropRate = 1f;
        public long minAmount = 1;
        public long maxAmount = 1;
    }

    [CreateAssetMenu(fileName = "LootTable_", menuName = "mkLike/Loot Table")]
    public class LootTableSO : ScriptableObject
    {
        [Tooltip("드롭 항목 목록")]
        public LootEntry[] entries;
    }
}
