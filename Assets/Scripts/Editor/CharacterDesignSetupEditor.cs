using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using MkLike.Data;

namespace MkLike.Editor
{
    /// <summary>
    /// 에디터에서 CharacterDesignData(JSON)를 SPUM 프리팹에 적용하는 도구.
    /// SerializedObject를 통해 SPUM_SpriteList의 필드에 접근하여 어셈블리 참조 문제를 회피한다.
    /// </summary>
    public static class CharacterDesignSetupEditor
    {
        private const string SPUM_ITEMS = "Assets/Folder_Assets/SPUM/SPUM_Sprites/Items/";
        private const string SPUM_BODY = "Assets/Folder_Assets/SPUM/SPUM_Sprites/BodySource/Species/";

        // SPUM_SpriteList 인덱스 매핑:
        // _hairList: [0]=Hair, [1]=FaceHair, [2]=BackHair, [3]=Helmet
        // _clothList: [0]=Body, [1]=Left, [2]=Right
        // _armorList: [0]=Body, [1]=Left, [2]=Right
        // _pantList: [0]=Left, [1]=Right
        // _weaponList: [0]=P_Weapon(R), [1]=P_Shield(R), [2]=L_Weapon(L), [3]=L_Shield(L)
        // _backList: [0]=Back
        // _eyeList: [0]=LeftEye, [1]=RightEye
        // _bodyList: [0]=Head, [1]=Body, [2]=LeftArm, [3]=RightArm, [4]=Extra

        public static void ApplyDesignById(GameObject spumVisual, string designId)
        {
            var design = LoadDesignFromJson(designId);
            if (design == null)
            {
                Debug.LogWarning($"[CharacterDesignSetup] 디자인을 찾을 수 없음: {designId}");
                return;
            }
            ApplyDesign(spumVisual, design);
        }

        public static CharacterDesignData LoadDesignFromJson(string designId)
        {
            string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/Resources/CharacterDesigns" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (textAsset == null) continue;

                try
                {
                    var design = JsonUtility.FromJson<CharacterDesignData>(textAsset.text);
                    if (design != null && design.id == designId)
                        return design;
                }
                catch { /* skip invalid json */ }
            }
            return null;
        }

        public static void ApplyDesign(GameObject spumVisual, CharacterDesignData design)
        {
            if (spumVisual == null || design == null) return;

            // SPUM_SpriteList 컴포넌트를 타입 이름으로 찾기 (어셈블리 참조 불필요)
            Component spriteListComp = FindComponentByTypeName(spumVisual, "SPUM_SpriteList");
            if (spriteListComp == null)
            {
                Debug.LogWarning($"[CharacterDesignSetup] SPUM_SpriteList를 찾을 수 없음: {spumVisual.name}");
                return;
            }

            var so = new SerializedObject(spriteListComp);

            // 각 리스트에서 SpriteRenderer 배열 추출
            var hairList = GetSpriteRendererList(so, "_hairList");
            var clothList = GetSpriteRendererList(so, "_clothList");
            var armorList = GetSpriteRendererList(so, "_armorList");
            var pantList = GetSpriteRendererList(so, "_pantList");
            var weaponList = GetSpriteRendererList(so, "_weaponList");
            var backList = GetSpriteRendererList(so, "_backList");
            var eyeList = GetSpriteRendererList(so, "_eyeList");
            var bodyList = GetSpriteRendererList(so, "_bodyList");

            // 1. 머리카락 (hairList[0])
            SetSingleItemSprite(hairList, 0, design.hair);

            if (!string.IsNullOrEmpty(design.hairColor)
                && ColorUtility.TryParseHtmlString(design.hairColor, out Color hColor))
            {
                if (hairList.Count > 0 && hairList[0] != null)
                    hairList[0].color = hColor;
            }

            // 2. 수염 (hairList[1])
            SetSingleItemSprite(hairList, 1, design.faceHair);

            // 3. 투구 (hairList[3]) + 가시성 토글
            SetSingleItemSprite(hairList, 3, design.helmet);

            if (!string.IsNullOrEmpty(design.helmet))
            {
                if (hairList.Count > 0 && hairList[0] != null)
                    hairList[0].gameObject.SetActive(false);
                if (hairList.Count > 3 && hairList[3] != null)
                    hairList[3].gameObject.SetActive(true);
            }
            else
            {
                if (hairList.Count > 0 && hairList[0] != null)
                    hairList[0].gameObject.SetActive(true);
                if (hairList.Count > 3 && hairList[3] != null)
                    hairList[3].gameObject.SetActive(false);
            }

            // 4. 상의 (clothList — Body/Left/Right)
            SetMultiItemSprite(clothList, design.cloth);

            // 5. 갑옷
            SetMultiItemSprite(armorList, design.armor);

            // 6. 하의
            SetMultiItemSprite(pantList, design.pants);

            // 7. 등 장비
            SetSingleItemSprite(backList, 0, design.back);

            // 8. 오른손 무기 (weaponList[0]=무기, [1]=방패)
            ApplyWeapon(weaponList, 0, 1, design.weaponRight);

            // 9. 왼손 무기 (weaponList[2]=무기, [3]=방패)
            ApplyWeapon(weaponList, 2, 3, design.weaponLeft);

            // 10. 바디 (종족)
            ApplyBody(bodyList, design.species);

            // 11. 눈
            ApplyEyes(eyeList, design.eye, design.eyeColor);

            // 12. 전체 틴트
            if (!string.IsNullOrEmpty(design.tintColor) && design.tintColor != "#FFFFFF")
            {
                if (ColorUtility.TryParseHtmlString(design.tintColor, out Color tint))
                {
                    var allRenderers = spumVisual.GetComponentsInChildren<SpriteRenderer>(true);
                    foreach (var sr in allRenderers)
                        sr.color *= tint;
                }
            }

            // 13. 스케일
            if (design.scale > 0f && Mathf.Abs(design.scale - 1f) > 0.01f)
                spumVisual.transform.localScale = Vector3.one * design.scale;

            Debug.Log($"[CharacterDesignSetup] 디자인 적용 완료: {design.id} ({design.displayName})");
        }

        // ── SPUM_SpriteList 접근 ──

        private static Component FindComponentByTypeName(GameObject go, string typeName)
        {
            foreach (var comp in go.GetComponentsInChildren<Component>(true))
            {
                if (comp != null && comp.GetType().Name == typeName)
                    return comp;
            }
            return null;
        }

        private static List<SpriteRenderer> GetSpriteRendererList(SerializedObject so, string propertyName)
        {
            var result = new List<SpriteRenderer>();
            var prop = so.FindProperty(propertyName);
            if (prop == null || !prop.isArray) return result;

            for (int i = 0; i < prop.arraySize; i++)
            {
                var element = prop.GetArrayElementAtIndex(i);
                result.Add(element.objectReferenceValue as SpriteRenderer);
            }
            return result;
        }

        // ── 단일 스프라이트 ──

        private static void SetSingleItemSprite(List<SpriteRenderer> list, int index, string itemPath)
        {
            if (list == null || index >= list.Count || list[index] == null) return;
            if (string.IsNullOrEmpty(itemPath)) return;

            string fullPath = SPUM_ITEMS + itemPath + ".png";
            Sprite sprite = LoadFirstSprite(fullPath);
            if (sprite != null)
                list[index].sprite = sprite;
        }

        // ── 멀티 스프라이트 (cloth/armor/pant) ──

        private static void SetMultiItemSprite(List<SpriteRenderer> list, string itemPath)
        {
            if (list == null || list.Count == 0 || string.IsNullOrEmpty(itemPath)) return;

            string fullPath = SPUM_ITEMS + itemPath + ".png";
            Sprite[] sprites = LoadAllSprites(fullPath);
            if (sprites == null || sprites.Length == 0) return;

            if (sprites.Length == 1)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] != null)
                        list[i].sprite = sprites[0];
                }
                return;
            }

            var spriteMap = BuildSpriteMap(sprites);
            string[] subNames;

            if (list.Count >= 3)
                subNames = new[] { "Body", "Left", "Right" };
            else if (list.Count == 2)
                subNames = new[] { "Left", "Right" };
            else
                subNames = new[] { "Body" };

            for (int i = 0; i < list.Count && i < subNames.Length; i++)
            {
                if (list[i] != null && spriteMap.TryGetValue(subNames[i], out var sprite))
                    list[i].sprite = sprite;
            }
        }

        // ── 무기 ──

        private static void ApplyWeapon(List<SpriteRenderer> weaponList, int weaponIdx, int shieldIdx, string itemPath)
        {
            if (weaponList == null || string.IsNullOrEmpty(itemPath)) return;

            string fullPath = SPUM_ITEMS + itemPath + ".png";
            Sprite sprite = LoadFirstSprite(fullPath);
            if (sprite == null) return;

            bool isShield = itemPath.ToLower().Contains("shield");
            if (isShield)
            {
                if (shieldIdx < weaponList.Count && weaponList[shieldIdx] != null)
                    weaponList[shieldIdx].sprite = sprite;
            }
            else
            {
                if (weaponIdx < weaponList.Count && weaponList[weaponIdx] != null)
                    weaponList[weaponIdx].sprite = sprite;
            }
        }

        // ── 바디 (종족) ──

        private static void ApplyBody(List<SpriteRenderer> bodyList, string speciesPath)
        {
            if (bodyList == null || bodyList.Count == 0 || string.IsNullOrEmpty(speciesPath)) return;

            string fullPath = SPUM_BODY + speciesPath + ".png";
            Sprite[] sprites = LoadAllSprites(fullPath);
            if (sprites == null || sprites.Length == 0) return;

            var spriteMap = BuildSpriteMap(sprites);
            string[] bodyParts = { "Head", "Body", "Arm_L", "Arm_R" };
            for (int i = 0; i < bodyParts.Length && i < bodyList.Count; i++)
            {
                if (bodyList[i] != null && spriteMap.TryGetValue(bodyParts[i], out var sprite))
                    bodyList[i].sprite = sprite;
            }
        }

        // ── 눈 ──

        private static void ApplyEyes(List<SpriteRenderer> eyeList, string eyePath, string colorHex)
        {
            if (eyeList == null || eyeList.Count == 0 || string.IsNullOrEmpty(eyePath)) return;

            string fullPath = SPUM_BODY + eyePath + ".png";
            Sprite[] sprites = LoadAllSprites(fullPath);
            if (sprites == null || sprites.Length == 0) return;

            var spriteMap = BuildSpriteMap(sprites);
            Sprite eyeSprite = null;
            if (spriteMap.TryGetValue("Front", out var front))
                eyeSprite = front;
            else if (spriteMap.TryGetValue("Back", out var back))
                eyeSprite = back;
            else if (sprites.Length > 0)
                eyeSprite = sprites[0];

            if (eyeSprite != null)
            {
                for (int i = 0; i < eyeList.Count; i++)
                {
                    if (eyeList[i] != null)
                        eyeList[i].sprite = eyeSprite;
                }
            }

            if (!string.IsNullOrEmpty(colorHex) && ColorUtility.TryParseHtmlString(colorHex, out Color color))
            {
                for (int i = 0; i < eyeList.Count; i++)
                {
                    if (eyeList[i] != null)
                        eyeList[i].color = color;
                }
            }
        }

        // ── 스프라이트 로드 ──

        private static Sprite LoadFirstSprite(string assetPath)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null) return sprite;

            var objects = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var obj in objects)
            {
                if (obj is Sprite s) return s;
            }
            return null;
        }

        private static Sprite[] LoadAllSprites(string assetPath)
        {
            var objects = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var sprites = new List<Sprite>();
            foreach (var obj in objects)
            {
                if (obj is Sprite s) sprites.Add(s);
            }
            return sprites.ToArray();
        }

        private static Dictionary<string, Sprite> BuildSpriteMap(Sprite[] sprites)
        {
            var map = new Dictionary<string, Sprite>();
            foreach (var sp in sprites)
            {
                if (!map.ContainsKey(sp.name))
                    map[sp.name] = sp;
            }
            return map;
        }
    }
}
