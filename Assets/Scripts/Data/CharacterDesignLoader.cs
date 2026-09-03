using System.Collections.Generic;
using UnityEngine;

namespace MkLike.Data
{
    /// <summary>
    /// SPUM 캐릭터에 CharacterDesignData를 적용하는 로더.
    /// 에디터에서는 AssetDatabase로 스프라이트를 로드하고,
    /// 런타임에서는 이미 프리팹에 설정된 스프라이트를 사용한다.
    /// 향후 CDN 전환 시 Addressables로 교체 예정.
    /// </summary>
    public static class CharacterDesignLoader
    {
        private const string SPUM_ITEMS = "Assets/Folder_Assets/SPUM/SPUM_Sprites/Items/";
        private const string SPUM_BODY = "Assets/Folder_Assets/SPUM/SPUM_Sprites/BodySource/Species/";

        private static readonly Dictionary<string, CharacterDesignData> _designCache = new();

        public static void LoadAllDesigns()
        {
            _designCache.Clear();
            var jsonFiles = Resources.LoadAll<TextAsset>("CharacterDesigns");
            foreach (var json in jsonFiles)
            {
                try
                {
                    var design = JsonUtility.FromJson<CharacterDesignData>(json.text);
                    if (design != null && !string.IsNullOrEmpty(design.id))
                        _designCache[design.id] = design;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[CharacterDesignLoader] JSON 파싱 실패: {json.name} — {e.Message}");
                }
            }

            Debug.Log($"[CharacterDesignLoader] {_designCache.Count}개 디자인 로드 완료");
        }

        public static CharacterDesignData GetDesign(string id)
        {
            if (_designCache.Count == 0)
                LoadAllDesigns();

            _designCache.TryGetValue(id, out var design);
            return design;
        }

        /// <summary>
        /// 에디터 전용: AssetDatabase를 통해 스프라이트를 로드하여 적용.
        /// </summary>
        public static string GetItemAssetPath(string itemRelativePath)
        {
            if (string.IsNullOrEmpty(itemRelativePath)) return null;
            return SPUM_ITEMS + itemRelativePath + ".png";
        }

        public static string GetBodyAssetPath(string speciesRelativePath)
        {
            if (string.IsNullOrEmpty(speciesRelativePath)) return null;
            return SPUM_BODY + speciesRelativePath + ".png";
        }

        public static void ClearCache()
        {
            _designCache.Clear();
        }
    }
}
