using UnityEditor;
using UnityEngine;
using MkLike.Data;
using MkLike.Core;
using System.Collections.Generic;

namespace MkLike.Editor
{
    /// <summary>
    /// 가챠 풀에 있지만 EquipmentDataSO가 없는 아이템의 SO를 자동 생성한다.
    /// </summary>
    public static class MissingEquipSOEditor
    {
        [MenuItem("mkLike/Create Missing Equipment SOs")]
        public static void CreateMissing()
        {
            // 가챠 풀에서 모든 itemId 수집
            string[] poolGuids = AssetDatabase.FindAssets("t:GachaPoolSO");
            var poolItemIds = new HashSet<string>();
            foreach (string guid in poolGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var pool = AssetDatabase.LoadAssetAtPath<GachaPoolSO>(path);
                if (pool?.entries == null) continue;
                foreach (var entry in pool.entries)
                {
                    if (!string.IsNullOrEmpty(entry.itemId))
                        poolItemIds.Add(entry.itemId);
                }
            }

            // 카탈로그에 있는 id 수집
            string[] eqGuids = AssetDatabase.FindAssets("t:EquipmentDataSO");
            var existingIds = new HashSet<string>();
            foreach (string guid in eqGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<EquipmentDataSO>(path);
                if (so != null) existingIds.Add(so.id);
            }

            // 누락된 아이템 생성
            int created = 0;
            foreach (string itemId in poolItemIds)
            {
                if (existingIds.Contains(itemId)) continue;

                var newSO = ScriptableObject.CreateInstance<EquipmentDataSO>();
                newSO.id = itemId;
                newSO.displayName = GetDisplayName(itemId);
                newSO.slot = GuessSlot(itemId);

                // 기본 스탯 (무기는 ATK 위주, 방어구는 DEF/HP 위주)
                if (newSO.slot == EquipmentSlot.Weapon)
                {
                    newSO.baseAtk = GetBaseAtk(itemId);
                    newSO.baseHp = 0;
                    newSO.baseDef = 0;
                }
                else
                {
                    newSO.baseAtk = 5;
                    newSO.baseHp = 15;
                    newSO.baseDef = 8;
                }

                string assetPath = $"Assets/Data/SO/Equipment/Equipment_{itemId}.asset";
                AssetDatabase.CreateAsset(newSO, assetPath);
                created++;
                Debug.Log($"[MissingEquipSO] 생성: {itemId} → {assetPath} (slot: {newSO.slot})");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[MissingEquipSO] === {created}개 SO 생성 완료 ===");
        }

        private static EquipmentSlot GuessSlot(string itemId)
        {
            if (itemId.StartsWith("weapon_") || itemId.StartsWith("sword_") || itemId.StartsWith("bow_") || itemId.StartsWith("staff_"))
                return EquipmentSlot.Weapon;
            if (itemId.StartsWith("helmet_")) return EquipmentSlot.Helmet;
            if (itemId.StartsWith("top_") || itemId.StartsWith("armor_")) return EquipmentSlot.Top;
            if (itemId.StartsWith("gloves_")) return EquipmentSlot.Gloves;
            if (itemId.StartsWith("boots_") || itemId.StartsWith("foot_")) return EquipmentSlot.Boots;
            if (itemId.StartsWith("ring_")) return EquipmentSlot.Ring;
            if (itemId.StartsWith("necklace_")) return EquipmentSlot.Necklace;
            if (itemId.StartsWith("face_")) return EquipmentSlot.FaceAccessory;
            return EquipmentSlot.Weapon;
        }

        private static int GetBaseAtk(string itemId)
        {
            // 무기별 기본 ATK (이름 기반 등급 추정)
            if (itemId.Contains("genesis") || itemId.Contains("ancient")) return 50;
            if (itemId.Contains("excalibur") || itemId.Contains("artemis")) return 40;
            if (itemId.Contains("dragon") || itemId.Contains("phoenix") || itemId.Contains("void")) return 30;
            if (itemId.Contains("mithril") || itemId.Contains("arcane") || itemId.Contains("windforce")) return 25;
            if (itemId.Contains("steel") || itemId.Contains("elm") || itemId.Contains("mystic")) return 18;
            if (itemId.Contains("oak") || itemId.Contains("apprentice")) return 12;
            return 15;
        }

        private static string GetDisplayName(string itemId)
        {
            return itemId switch
            {
                "weapon_oak_bow" => "참나무 활",
                "weapon_apprentice_staff" => "견습 지팡이",
                "weapon_elm_longbow" => "느릅나무 장궁",
                "weapon_mystic_rod" => "신비의 지팡이",
                "weapon_arcane_staff" => "비전 지팡이",
                "weapon_windforce_bow" => "바람의 활",
                "weapon_dragon_blade" => "용의 검",
                "weapon_phoenix_bow" => "불사조 활",
                "weapon_void_scepter" => "공허의 홀",
                "weapon_excalibur" => "엑스칼리버",
                "weapon_artemis" => "아르테미스",
                "weapon_genesis" => "제네시스",
                _ => itemId.Replace("_", " ")
            };
        }
    }
}
