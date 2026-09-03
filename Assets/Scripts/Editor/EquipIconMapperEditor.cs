using System.Collections.Generic;
using System.IO;
using System.Linq;
using MkLike.Core;
using MkLike.Data;
using UnityEditor;
using UnityEngine;

namespace MkLike.Editor
{
    /// <summary>
    /// Admurin 픽셀 아이템 에셋에서 EquipmentDataSO.icon을 자동 매핑하는 에디터 도구.
    /// </summary>
    public static class EquipIconMapperEditor
    {
        // Admurin 에셋 루트
        private const string ADMURIN_ROOT =
            "Assets/Folder_Assets/Admurin's Pixel Items/PixelItems";

        // EquipmentSlot → (폴더 경로, 파일 키워드) 매핑
        // Armory/Singles/Weapon Singles/{Material}/{Material}_Weapon*.png
        // Armory/Singles/Armor Singles/{Material}/{Material}_Helmet*.png 등
        private static readonly Dictionary<EquipmentSlot, (string subPath, string keyword)> SlotSpriteMap = new()
        {
            { EquipmentSlot.Weapon,        ("Armory/Singles/Weapon Singles", "Weapon") },
            { EquipmentSlot.Helmet,        ("Armory/Singles/Armor Singles", "Helmet") },
            { EquipmentSlot.Top,           ("Armory/Singles/Armor Singles", "Chestplate") },
            { EquipmentSlot.Gloves,        ("Armory/Singles/Armor Singles", "Gloves") },
            { EquipmentSlot.Boots,         ("Armory/Singles/Armor Singles", "Boots") },
            { EquipmentSlot.Ring,          ("Rings/Singles", null) },
            { EquipmentSlot.Necklace,      ("Gems/Singles", null) },
            { EquipmentSlot.FaceAccessory, ("Gems/Singles", null) },
        };

        // Admurin 소재 티어 (낮은 등급→높은 등급 순서)
        private static readonly string[] MaterialTiers =
        {
            "Wooden", "Copper", "Iron", "Steel", "Silver",
            "Cobalt", "Gold", "Crimson", "Platinum",
            "Altair", "Fateful", "Adamantine", "Angelic"
        };

        [MenuItem("mkLike/Auto Map/Equipment Icons")]
        public static void AutoMapEquipmentIcons()
        {
            string[] guids = AssetDatabase.FindAssets("t:EquipmentDataSO");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[EquipIconMapper] EquipmentDataSO 에셋을 찾을 수 없습니다.");
                return;
            }

            int mapped = 0;
            int skipped = 0;
            int failed = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<EquipmentDataSO>(path);
                if (so == null)
                {
                    failed++;
                    continue;
                }

                // 이미 아이콘이 할당되어 있으면 스킵
                if (so.icon != null)
                {
                    skipped++;
                    continue;
                }

                Sprite sprite = FindSpriteForEquipment(so);
                if (sprite != null)
                {
                    so.icon = sprite;
                    EditorUtility.SetDirty(so);
                    mapped++;
                    Debug.Log($"[EquipIconMapper] {so.name} → {sprite.name}");
                }
                else
                {
                    failed++;
                    Debug.LogWarning($"[EquipIconMapper] {so.name} (slot={so.slot}): 매칭 스프라이트를 찾지 못했습니다.");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[EquipIconMapper] 완료 — 매핑: {mapped}, 스킵(기존 아이콘): {skipped}, 실패: {failed}");
        }

        [MenuItem("mkLike/Auto Map/Equipment Icons (Force Overwrite)")]
        public static void AutoMapEquipmentIconsForce()
        {
            string[] guids = AssetDatabase.FindAssets("t:EquipmentDataSO");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[EquipIconMapper] EquipmentDataSO 에셋을 찾을 수 없습니다.");
                return;
            }

            int mapped = 0;
            int failed = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<EquipmentDataSO>(path);
                if (so == null)
                {
                    failed++;
                    continue;
                }

                Sprite sprite = FindSpriteForEquipment(so);
                if (sprite != null)
                {
                    so.icon = sprite;
                    EditorUtility.SetDirty(so);
                    mapped++;
                    Debug.Log($"[EquipIconMapper] {so.name} → {sprite.name}");
                }
                else
                {
                    failed++;
                    Debug.LogWarning($"[EquipIconMapper] {so.name} (slot={so.slot}): 매칭 스프라이트를 찾지 못했습니다.");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[EquipIconMapper] 강제 덮어쓰기 완료 — 매핑: {mapped}, 실패: {failed}");
        }

        /// <summary>
        /// SO의 slot과 id를 기반으로 Admurin 에셋에서 적절한 스프라이트를 찾는다.
        /// id에서 소재 힌트를 추출하고, 없으면 해시 기반 선택.
        /// </summary>
        private static Sprite FindSpriteForEquipment(EquipmentDataSO so)
        {
            if (!SlotSpriteMap.TryGetValue(so.slot, out var mapping))
                return null;

            string folderPath = $"{ADMURIN_ROOT}/{mapping.subPath}";

            // Armory 계열: 소재별 하위 폴더가 있음
            if (mapping.subPath.Contains("Armory"))
            {
                return FindArmorySprite(so, folderPath, mapping.keyword);
            }

            // 비-Armory 계열 (Rings, Gems, Clothes 등): Singles 폴더에 직접 PNG
            return FindFlatSprite(so, folderPath);
        }

        /// <summary>
        /// Armory 계열: {folderPath}/{Material}/{Material}_{keyword}{N}.png 패턴
        /// SO id에서 소재 힌트 추출 → 매칭 티어 폴더에서 선택
        /// </summary>
        private static Sprite FindArmorySprite(EquipmentDataSO so, string folderPath, string keyword)
        {
            // SO id에서 소재 힌트 추출 (예: Equipment_weapon_iron_sword → "iron")
            string materialHint = ExtractMaterialHint(so.id);
            string targetMaterial = MatchMaterialTier(materialHint);

            string materialFolder = $"{folderPath}/{targetMaterial}";
            if (!AssetDatabase.IsValidFolder(materialFolder))
            {
                // 폴백: Iron 사용
                materialFolder = $"{folderPath}/Iron";
                if (!AssetDatabase.IsValidFolder(materialFolder))
                    return null;
            }

            // 폴더에서 키워드 매칭 스프라이트 검색
            string[] spriteGuids = AssetDatabase.FindAssets("t:Sprite", new[] { materialFolder });
            var candidates = new List<(string path, Sprite sprite)>();

            foreach (string guid in spriteGuids)
            {
                string spritePath = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(spritePath);

                // 키워드 필터 (Weapon, Helmet, Chestplate 등)
                if (!string.IsNullOrEmpty(keyword) && !fileName.Contains(keyword))
                    continue;

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite != null)
                    candidates.Add((spritePath, sprite));
            }

            if (candidates.Count == 0)
                return null;

            // id 해시로 결정론적 선택 (같은 SO는 항상 같은 아이콘)
            int index = Mathf.Abs(so.id.GetHashCode()) % candidates.Count;
            return candidates[index].sprite;
        }

        /// <summary>
        /// 비-Armory 폴더에서 스프라이트 검색. 하위 폴더 포함.
        /// </summary>
        private static Sprite FindFlatSprite(EquipmentDataSO so, string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
                return null;

            string[] spriteGuids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
            if (spriteGuids.Length == 0)
                return null;

            // 스프라이트 로드
            var candidates = new List<Sprite>();
            foreach (string guid in spriteGuids)
            {
                string spritePath = AssetDatabase.GUIDToAssetPath(guid);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite != null)
                    candidates.Add(sprite);
            }

            if (candidates.Count == 0)
                return null;

            int index = Mathf.Abs(so.id.GetHashCode()) % candidates.Count;
            return candidates[index];
        }

        /// <summary>
        /// SO id에서 소재 힌트를 추출한다.
        /// 예: "Equipment_weapon_iron_sword" → "iron"
        ///     "Equipment_armor_leather" → "leather"
        /// </summary>
        private static string ExtractMaterialHint(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "";

            // "Equipment_{slot}_{material}_{variant}" 패턴에서 3번째 토큰
            string[] parts = id.Split('_');
            if (parts.Length >= 3)
                return parts[2].ToLowerInvariant();

            return "";
        }

        /// <summary>
        /// 소재 힌트를 Admurin 소재 티어에 매칭한다.
        /// 직접 매칭 안 되면 유사 소재로 폴백.
        /// </summary>
        private static string MatchMaterialTier(string hint)
        {
            if (string.IsNullOrEmpty(hint))
                return "Iron";

            // 직접 매칭 (대소문자 무시)
            foreach (string tier in MaterialTiers)
            {
                if (tier.ToLowerInvariant() == hint)
                    return tier;
            }

            // 소재 별칭 매핑
            return hint switch
            {
                "leather" => "Copper",
                "cloth"   => "Wooden",
                "chain"   => "Steel",
                "mithril" => "Platinum",
                "steel"   => "Steel",
                "iron"    => "Iron",
                "bronze"  => "Copper",
                "sandals" => "Wooden",
                _         => "Iron"
            };
        }
    }
}
