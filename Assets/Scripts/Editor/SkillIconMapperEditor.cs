using UnityEditor;
using UnityEngine;
using MkLike.Data;

namespace MkLike.Editor
{
    /// <summary>
    /// SkillDataSO 에셋에 아이콘 스프라이트를 자동 매핑하는 에디터 도구.
    /// </summary>
    public static class SkillIconMapperEditor
    {
        private const string ICON_FOLDER = "Assets/Resources/Icons/Skills";

        [MenuItem("mkLike/Auto Map/Skill Icons", false, 350)]
        public static void AutoMapSkillIcons()
        {
            string[] guids = AssetDatabase.FindAssets("t:SkillDataSO");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[SkillIconMapper] SkillDataSO 에셋을 찾을 수 없습니다.");
                return;
            }

            // 아이콘 폴더의 모든 스프라이트를 이름(소문자) → Sprite 딕셔너리로 캐싱
            string[] iconGuids = AssetDatabase.FindAssets("t:Sprite", new[] { ICON_FOLDER });
            var iconMap = new System.Collections.Generic.Dictionary<string, Sprite>(iconGuids.Length);
            foreach (string iconGuid in iconGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(iconGuid);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                {
                    iconMap[sprite.name.ToLowerInvariant()] = sprite;
                }
            }

            int mapped = 0;
            int skipped = 0;
            int alreadySet = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<SkillDataSO>(path);
                if (so == null) continue;

                // 매칭 키: SO 에셋 이름 → id 필드 순서로 시도
                string assetName = so.name.ToLowerInvariant();
                string idName = string.IsNullOrEmpty(so.id) ? null : so.id.ToLowerInvariant();

                Sprite icon = null;
                if (iconMap.TryGetValue(assetName, out var found))
                {
                    icon = found;
                }
                else if (idName != null && iconMap.TryGetValue(idName, out var foundById))
                {
                    icon = foundById;
                }

                if (icon == null)
                {
                    skipped++;
                    Debug.LogWarning($"[SkillIconMapper] 아이콘 미발견: {so.name} (id: {so.id})");
                    continue;
                }

                if (so.icon == icon)
                {
                    alreadySet++;
                    continue;
                }

                so.icon = icon;
                EditorUtility.SetDirty(so);
                mapped++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[SkillIconMapper] 완료 — 매핑: {mapped}, 이미설정: {alreadySet}, 미발견: {skipped} (전체 SO: {guids.Length})");
        }
    }
}
