using System.IO;
using UnityEditor;
using UnityEngine;

namespace MkLike.Editor
{
    /// <summary>
    /// GUI Kit - Dark Geo 스프라이트를 Resources/UI/DarkGeo/ 하위로 임포트하고
    /// TextureImporter 설정(픽셀아트, 9-슬라이스)을 자동 적용하는 에디터 도구.
    /// </summary>
    public static class GuiKitImporter
    {
        private const string SRC_ROOT =
            "Assets/Folder_Assets/GUI Kit - Dark Geo/ResourceData/Sprite/Component";
        private const string DST_ROOT = "Assets/Resources/UI/DarkGeo";

        private const int SLICE_BORDER = 16;
        private const int PIXELS_PER_UNIT = 100;

        // 소스 폴더명 → 대상 서브폴더명 매핑
        private static readonly (string src, string dst, bool needSlice)[] _categories =
        {
            ("Button",  "Button",  true),
            ("Frame",   "Frame",   true),
            ("Popup",   "Popup",   true),
            ("Slider",  "Slider",  false),
            ("UI_Etc",  "Etc",     false),
        };

        [MenuItem("mkLike/UI/Import GUI Kit")]
        public static void Import()
        {
            int copied = 0;
            int skipped = 0;

            foreach (var (src, dst, needSlice) in _categories)
            {
                string srcDir = $"{SRC_ROOT}/{src}";
                string dstDir = $"{DST_ROOT}/{dst}";

                if (!AssetDatabase.IsValidFolder(srcDir))
                {
                    Debug.LogWarning($"[GuiKitImporter] 소스 폴더 없음: {srcDir}");
                    continue;
                }

                // 대상 폴더 생성
                EnsureFolder(dstDir);

                // PNG 파일 검색
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { srcDir });
                foreach (string guid in guids)
                {
                    string srcPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (!srcPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                        continue;

                    string fileName = Path.GetFileName(srcPath);
                    string dstPath = $"{dstDir}/{fileName}";

                    // 이미 존재하면 스킵
                    if (File.Exists(dstPath))
                    {
                        skipped++;
                        continue;
                    }

                    AssetDatabase.CopyAsset(srcPath, dstPath);
                    copied++;

                    // TextureImporter 설정
                    ApplyImportSettings(dstPath, needSlice);
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"[GuiKitImporter] 완료 — 복사: {copied}개, 스킵: {skipped}개");
        }

        private static void ApplyImportSettings(string assetPath, bool needSlice)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = PIXELS_PER_UNIT;

            if (needSlice)
            {
                importer.spriteBorder = new Vector4(SLICE_BORDER, SLICE_BORDER, SLICE_BORDER, SLICE_BORDER);
            }

            importer.SaveAndReimport();
        }

        /// <summary>
        /// 중첩 폴더를 재귀적으로 생성. 예: "Assets/Resources/UI/DarkGeo/Button"
        /// </summary>
        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;

            string parent = Path.GetDirectoryName(folderPath).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            string folderName = Path.GetFileName(folderPath);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
