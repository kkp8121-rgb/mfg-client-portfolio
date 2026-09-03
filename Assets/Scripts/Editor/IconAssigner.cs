using UnityEditor;
using UnityEngine;
using MkLike.Data;

namespace MkLike.Editor
{
    /// <summary>
    /// SO에 아이콘 스프라이트를 자동 할당하는 에디터 유틸.
    /// </summary>
    public static class IconAssigner
    {
        [MenuItem("mkLike/Assets/Assign All Icons", false, 600)]
        public static void AssignAll()
        {
            int count = 0;
            count += AssignWeaponIcons();
            count += AssignDungeonIcons();
            count += AssignDungeonMonsterPacks();
            Debug.Log($"[IconAssigner] 총 {count}개 할당 완료");
            AssetDatabase.SaveAssets();
        }

        [MenuItem("mkLike/Assets/Assign Weapon Icons", false, 601)]
        public static int AssignWeaponIcons()
        {
            var map = new (string soName, string spritePath)[]
            {
                ("weapon_steel_sword",   "Assets/Folder_Assets/Admurin's Pixel Items/PixelItems/Armory/Singles/Weapon Singles/Steel/Steel_Weapon1.png"),
                ("weapon_flame_blade",   "Assets/Folder_Assets/Admurin's Pixel Items/PixelItems/Armory/Singles/Weapon Singles/Crimson/Crimson_Weapon1.png"),
                ("weapon_ice_bow",       "Assets/Folder_Assets/Admurin's Pixel Items/PixelItems/Armory/Singles/Weapon Singles/Altair/Altair_Weapon1.png"),
                ("weapon_dragon_blade",  "Assets/Folder_Assets/Admurin's Pixel Items/PixelItems/Armory/Singles/Weapon Singles/Fateful/Fateful_Weapon1.png"),
                ("weapon_ancient_staff", "Assets/Folder_Assets/Admurin's Pixel Items/PixelItems/Armory/Singles/Weapon Singles/Gold/Gold_Weapon1.png"),
                ("weapon_storm_staff",   "Assets/Folder_Assets/Admurin's Pixel Items/PixelItems/Armory/Singles/Weapon Singles/Cobalt/Cobalt_Weapon1.png"),
            };

            int count = 0;
            foreach (var (soName, spritePath) in map)
            {
                var so = LoadSO<WeaponDataSO>($"Data/Weapons/{soName}");
                if (so == null) { Debug.LogWarning($"[IconAssigner] SO 없음: {soName}"); continue; }

                var sprite = LoadSprite(spritePath);
                if (sprite == null) { Debug.LogWarning($"[IconAssigner] 스프라이트 없음: {spritePath}"); continue; }

                var serialized = new SerializedObject(so);
                var iconProp = serialized.FindProperty("icon");
                if (iconProp != null)
                {
                    iconProp.objectReferenceValue = sprite;
                    serialized.ApplyModifiedProperties();
                    EditorUtility.SetDirty(so);
                    count++;
                    Debug.Log($"[IconAssigner] {soName} ← {sprite.name}");
                }
            }

            Debug.Log($"[IconAssigner] 무기 아이콘 {count}개 할당");
            return count;
        }

        [MenuItem("mkLike/Assets/Assign Dungeon Icons", false, 602)]
        public static int AssignDungeonIcons()
        {
            var map = new (string soName, string spritePath)[]
            {
                ("dungeon_weapon",    "Assets/Resources/Icons/Stone/icon_sword.png"),
                ("dungeon_exp",       "Assets/Resources/Icons/Stone/icon_star.png"),
                ("dungeon_equipment", "Assets/Resources/Icons/Stone/icon_shield.png"),
                ("dungeon_enhance",   "Assets/Resources/Icons/Stone/icon_hammer.png"),
                ("dungeon_climber",   "Assets/Resources/Icons/Stone/icon_crown.png"),
            };

            int count = 0;
            foreach (var (soName, spritePath) in map)
            {
                var so = LoadSO<DungeonDataSO>($"Data/Dungeons/{soName}");
                if (so == null) { Debug.LogWarning($"[IconAssigner] SO 없음: {soName}"); continue; }

                var sprite = LoadSprite(spritePath);
                if (sprite == null) { Debug.LogWarning($"[IconAssigner] 스프라이트 없음: {spritePath}"); continue; }

                var serialized = new SerializedObject(so);
                var iconProp = serialized.FindProperty("icon");
                if (iconProp != null)
                {
                    iconProp.objectReferenceValue = sprite;
                    serialized.ApplyModifiedProperties();
                    EditorUtility.SetDirty(so);
                    count++;
                    Debug.Log($"[IconAssigner] {soName} ← {sprite.name}");
                }
            }

            Debug.Log($"[IconAssigner] 던전 아이콘 {count}개 할당");
            return count;
        }

        [MenuItem("mkLike/Assets/Assign Dungeon Monster Packs", false, 603)]
        public static int AssignDungeonMonsterPacks()
        {
            // 던전별 몬스터팩 + 배경색 매핑
            var map = new (string soName, string pack, Color tint, Color bgBot, Color bgTop)[]
            {
                ("dungeon_weapon",    "Addons/RetroHeroes/2_Prefab",
                    new Color(1f, 0.7f, 0.7f),
                    new Color(0.20f, 0.05f, 0.05f), new Color(0.35f, 0.10f, 0.08f)),
                ("dungeon_exp",       "Addons/ModernPackVer1/2_Prefab",
                    new Color(0.7f, 0.9f, 1f),
                    new Color(0.05f, 0.10f, 0.20f), new Color(0.10f, 0.18f, 0.35f)),
                ("dungeon_equipment", "Addons/PaladinSet/2_Prefab",
                    new Color(1f, 0.9f, 0.6f),
                    new Color(0.15f, 0.12f, 0.05f), new Color(0.28f, 0.22f, 0.08f)),
                ("dungeon_enhance",   "Addons/Elf/2_Prefab",
                    new Color(0.8f, 0.7f, 1f),
                    new Color(0.10f, 0.05f, 0.18f), new Color(0.18f, 0.10f, 0.30f)),
                ("dungeon_climber",   "Addons/Undead/2_Prefab",
                    new Color(0.6f, 0.8f, 0.6f),
                    new Color(0.05f, 0.08f, 0.05f), new Color(0.10f, 0.15f, 0.10f)),
            };

            int count = 0;
            foreach (var (soName, pack, tint, bgBot, bgTop) in map)
            {
                var so = LoadSO<DungeonDataSO>($"Data/Dungeons/{soName}");
                if (so == null) continue;

                // public 필드 직접 설정
                so.monsterPrefabFolder = pack;
                so.monsterTint = tint;
                so.bgBottom = bgBot;
                so.bgTop = bgTop;
                EditorUtility.SetDirty(so);
                count++;
                Debug.Log($"[IconAssigner] {soName} ← 몬스터팩: {pack}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[IconAssigner] 던전 몬스터팩 {count}개 할당");
            return count;
        }

        private static T LoadSO<T>(string resourcePath) where T : ScriptableObject
        {
            return Resources.Load<T>(resourcePath);
        }

        private static Sprite LoadSprite(string assetPath)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }
    }
}
