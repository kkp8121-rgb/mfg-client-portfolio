using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MkLike.Editor
{
    /// <summary>
    /// SPUM 프리팹에 기본 의상 스프라이트를 적용한다.
    /// 알몸 상태를 방지하기 위해 기본 옷/갑옷/바지/무기를 설정.
    /// </summary>
    public static class SpumDefaultOutfitEditor
    {
        private const string ITEMS_PATH = "Assets/Folder_Assets/SPUM/SPUM_Sprites/Items/";

        private static readonly string[] PREFAB_PATHS =
        {
            "Assets/Folder_Assets/SPUM/Prefab/AnimationSample/SwordMan.prefab",
            "Assets/Folder_Assets/SPUM/Prefab/AnimationSample/BowMan.prefab",
            "Assets/Folder_Assets/SPUM/Prefab/AnimationSample/MagicianMan.prefab",
        };

        [MenuItem("mkLike/Apply Default SPUM Outfit")]
        public static void ApplyDefaultOutfit()
        {
            foreach (string prefabPath in PREFAB_PATHS)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Debug.LogWarning($"[SpumOutfit] 프리팹 없음: {prefabPath}");
                    continue;
                }

                // 프리팹 인스턴스 열기
                string assetPath = AssetDatabase.GetAssetPath(prefab);
                var root = PrefabUtility.LoadPrefabContents(assetPath);

                var spriteList = FindSpumSpriteList(root);
                if (spriteList == null)
                {
                    Debug.LogWarning($"[SpumOutfit] SPUM_SpriteList 없음: {prefabPath}");
                    PrefabUtility.UnloadPrefabContents(root);
                    continue;
                }

                bool isArcher = prefabPath.Contains("BowMan");
                bool isMage = prefabPath.Contains("MagicianMan");

                // 의상 적용
                ApplyCloth(spriteList, "2_Cloth/Cloth_1");
                ApplyArmor(spriteList, "5_Armor/Armor_1");
                ApplyPant(spriteList, "3_Pant/Foot_1");
                ApplyHair(spriteList, "0_Hair/Hair_1");

                if (isArcher)
                    ApplyWeapon(spriteList, "6_Weapons/Bow_1");
                else if (isMage)
                    ApplyWeapon(spriteList, "6_Weapons/Spear_1");
                else
                    ApplyWeapon(spriteList, "6_Weapons/Sword_1");

                // 눈 + 바디 적용
                ApplyEyes(spriteList);
                ApplyBody(spriteList);

                // 프리팹 저장
                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                PrefabUtility.UnloadPrefabContents(root);

                Debug.Log($"[SpumOutfit] 기본 의상 적용 완료: {prefabPath}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[SpumOutfit] === 전체 프리팹 의상 적용 완료 ===");
        }

        private static void ApplyCloth(Component spriteList, string itemPath)
        {
            // _clothList: [0]=Body, [1]=Left, [2]=Right
            var renderers = GetList(spriteList, "_clothList");
            if (renderers == null) return;
            AssignMultiPartSprite(renderers, itemPath, new[] { "Body", "Left", "Right" });
        }

        private static void ApplyArmor(Component spriteList, string itemPath)
        {
            // _armorList: [0]=Body, [1]=Left, [2]=Right
            var renderers = GetList(spriteList, "_armorList");
            if (renderers == null) return;
            AssignMultiPartSprite(renderers, itemPath, new[] { "Body", "Left", "Right" });
        }

        private static void ApplyPant(Component spriteList, string itemPath)
        {
            // _pantList: [0]=Left, [1]=Right
            var renderers = GetList(spriteList, "_pantList");
            if (renderers == null) return;
            AssignMultiPartSprite(renderers, itemPath, new[] { "Left", "Right" });
        }

        private static void ApplyHair(Component spriteList, string itemPath)
        {
            // _hairList: [0]=Hair (단일 스프라이트)
            var renderers = GetList(spriteList, "_hairList");
            if (renderers == null || renderers.Count == 0) return;

            var sprites = LoadSprites(itemPath);
            if (sprites.Length > 0)
                renderers[0].sprite = sprites[0];
        }

        private static void ApplyEyes(Component spriteList)
        {
            // _eyeList: [0]=LeftEye, [1]=RightEye
            var renderers = GetList(spriteList, "_eyeList");
            if (renderers == null) return;

            string eyePath = "Assets/Folder_Assets/SPUM/SPUM_Sprites/BodySource/Species/0_Human/Eye/Eye0.png";
            var sprites = AssetDatabase.LoadAllAssetsAtPath(eyePath);
            Sprite eyeSprite = null;
            foreach (var obj in sprites)
            {
                if (obj is Sprite sp) { eyeSprite = sp; break; }
            }
            if (eyeSprite == null)
            {
                // 단일 스프라이트로 시도
                eyeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(eyePath);
            }
            if (eyeSprite == null) return;

            for (int i = 0; i < renderers.Count && i < 2; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].sprite = eyeSprite;
                    renderers[i].color = new Color(0.2f, 0.15f, 0.1f, 1f); // 갈색 눈
                }
            }
        }

        private static void ApplyBody(Component spriteList)
        {
            // _bodyList: [0]=Head, [1]=Body, [2]=LeftArm, [3]=RightArm, [4]=Extra
            var renderers = GetList(spriteList, "_bodyList");
            if (renderers == null) return;

            string bodyPath = "Assets/Folder_Assets/SPUM/SPUM_Sprites/BodySource/Species/0_Human/Human_1.png";
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(bodyPath);

            var spriteMap = new Dictionary<string, Sprite>();
            foreach (var obj in allAssets)
            {
                if (obj is Sprite sp)
                    spriteMap[sp.name] = sp;
            }

            // 서브스프라이트 이름 매핑 (Head, Body, LeftArm, RightArm)
            string[] partNames = { "Head", "Body", "LeftArm", "RightArm" };
            for (int i = 0; i < partNames.Length && i < renderers.Count; i++)
            {
                if (renderers[i] == null) continue;

                // 정확한 이름 매칭 시도
                if (spriteMap.TryGetValue(partNames[i], out var sp))
                {
                    renderers[i].sprite = sp;
                }
                else
                {
                    // 부분 매칭
                    foreach (var kv in spriteMap)
                    {
                        if (kv.Key.Contains(partNames[i]))
                        {
                            renderers[i].sprite = kv.Value;
                            break;
                        }
                    }
                }

                // 피부색 (밝은 살색)
                renderers[i].color = new Color(1f, 0.87f, 0.75f, 1f);
            }
        }

        private static void ApplyWeapon(Component spriteList, string itemPath)
        {
            // _weaponList: [0]=R_Main (단일 스프라이트)
            var renderers = GetList(spriteList, "_weaponList");
            if (renderers == null || renderers.Count == 0) return;

            var sprites = LoadSprites(itemPath);
            if (sprites.Length > 0)
                renderers[0].sprite = sprites[0];
        }

        private static void AssignMultiPartSprite(List<SpriteRenderer> renderers, string itemPath, string[] partNames)
        {
            var sprites = LoadSprites(itemPath);

            if (sprites.Length <= 1)
            {
                // 단일 스프라이트 → 첫 렌더러에만
                if (sprites.Length == 1 && renderers.Count > 0 && renderers[0] != null)
                    renderers[0].sprite = sprites[0];
                return;
            }

            // 멀티파트: Body/Left/Right 이름 매칭
            for (int i = 0; i < partNames.Length && i < renderers.Count; i++)
            {
                if (renderers[i] == null) continue;

                string partName = partNames[i];
                Sprite matched = null;
                foreach (var sp in sprites)
                {
                    if (sp.name.EndsWith(partName) || sp.name == partName)
                    {
                        matched = sp;
                        break;
                    }
                }

                if (matched != null)
                    renderers[i].sprite = matched;
                else if (sprites.Length > 0)
                    renderers[i].sprite = sprites[0]; // fallback
            }
        }

        private static Sprite[] LoadSprites(string itemPath)
        {
            string fullPath = ITEMS_PATH + itemPath + ".png";
            var sprites = AssetDatabase.LoadAllAssetsAtPath(fullPath);

            var result = new List<Sprite>();
            foreach (var obj in sprites)
            {
                if (obj is Sprite sp)
                    result.Add(sp);
            }
            return result.ToArray();
        }

        private static Component FindSpumSpriteList(GameObject root)
        {
            foreach (var comp in root.GetComponentsInChildren<Component>(true))
            {
                if (comp != null && comp.GetType().Name == "SPUM_SpriteList")
                    return comp;
            }
            return null;
        }

        private static List<SpriteRenderer> GetList(Component spriteList, string fieldName)
        {
            var field = spriteList.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (field == null) return null;
            return field.GetValue(spriteList) as List<SpriteRenderer>;
        }
    }
}
