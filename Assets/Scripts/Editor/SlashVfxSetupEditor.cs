using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MkLike.Editor
{
    /// <summary>
    /// 슬래시 이펙트 개별 PNG 프레임을 스캔하여
    /// SpriteSheetVfxSO 에셋을 자동 생성하는 에디터 도구.
    /// Pack 1 (Cartoon): PNG/{1~10}/ 폴더
    /// Pack 2 (Slash): slash{N}/png/ 폴더
    /// </summary>
    public static class SlashVfxSetupEditor
    {
        private const string PACK1_ROOT =
            "Assets/Folder_Assets/craftpix-net-501088-free-slash-sprite-cartoon-effects/PNG";

        private const string PACK2_ROOT =
            "Assets/Folder_Assets/craftpix-net-825597-free-slash-effects-sprite-pack";

        private const string OUTPUT_FOLDER = "Assets/Data/SO/VFX";

        private const int DEFAULT_FPS = 16;
        private const float DEFAULT_SCALE = 1f;

        public static void GenerateSlashVfxSOs()
        {
            EnsureFolder(OUTPUT_FOLDER);

            int created = 0;
            int updated = 0;

            // Pack 1 (Cartoon): PNG/1/ ~ PNG/10/
            for (int i = 1; i <= 10; i++)
            {
                string folderPath = $"{PACK1_ROOT}/{i}";
                string assetName = $"Vfx_Cartoon_{i}";

                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    Debug.LogWarning($"[SlashVfxSetup] 폴더 없음: {folderPath}");
                    continue;
                }

                var sprites = CollectSpritesFromFolder(folderPath);
                if (sprites.Count == 0)
                {
                    Debug.LogWarning($"[SlashVfxSetup] 스프라이트 없음: {folderPath}");
                    continue;
                }

                bool isNew = CreateOrUpdateSO(assetName, sprites);
                if (isNew) created++;
                else updated++;
            }

            // Pack 2 (Slash): slash{N}/png/ 폴더
            string[] pack2FolderNames =
            {
                "slash", "slash2", "slash3", "slash4", "slash5",
                "slash6", "slash7", "slash8", "slash9", "slash10"
            };

            for (int i = 0; i < pack2FolderNames.Length; i++)
            {
                string folderPath = $"{PACK2_ROOT}/{pack2FolderNames[i]}/png";
                string assetName = $"Vfx_Slash_{i + 1}";

                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    Debug.LogWarning($"[SlashVfxSetup] 폴더 없음: {folderPath}");
                    continue;
                }

                var sprites = CollectSpritesFromFolder(folderPath);
                if (sprites.Count == 0)
                {
                    Debug.LogWarning($"[SlashVfxSetup] 스프라이트 없음: {folderPath}");
                    continue;
                }

                bool isNew = CreateOrUpdateSO(assetName, sprites);
                if (isNew) created++;
                else updated++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SlashVfxSetup] 완료 — 생성: {created}, 업데이트: {updated}");
        }

        /// <summary>
        /// 폴더 내 모든 PNG를 스캔, 텍스처 임포트 설정을 적용한 뒤
        /// Sprite로 로드하여 자연순서 정렬 후 반환.
        /// </summary>
        private static List<Sprite> CollectSpritesFromFolder(string folderPath)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
            var spriteEntries = new List<(int sortKey, string path, Sprite sprite)>();

            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);

                // 직접 하위만 (재귀 탐색된 서브폴더 파일 제외)
                string assetDir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
                if (assetDir != folderPath)
                    continue;

                if (!assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                ConfigureTextureImporter(assetPath);

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite == null)
                {
                    Debug.LogWarning($"[SlashVfxSetup] Sprite 로드 실패: {assetPath}");
                    continue;
                }

                int sortKey = ExtractNaturalSortKey(Path.GetFileNameWithoutExtension(assetPath));
                spriteEntries.Add((sortKey, assetPath, sprite));
            }

            spriteEntries.Sort((a, b) =>
            {
                int cmp = a.sortKey.CompareTo(b.sortKey);
                return cmp != 0 ? cmp : string.Compare(a.path, b.path, System.StringComparison.Ordinal);
            });

            var result = new List<Sprite>(spriteEntries.Count);
            for (int i = 0; i < spriteEntries.Count; i++)
                result.Add(spriteEntries[i].sprite);

            return result;
        }

        /// <summary>
        /// TextureImporter 설정: Sprite 타입, Bilinear 필터, 무압축, 알파 투명.
        /// </summary>
        private static void ConfigureTextureImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return;

            bool needsReimport = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                needsReimport = true;
            }

            // Bilinear 필터 (카툰/벡터 스타일 스프라이트에 적합)
            if (importer.filterMode != FilterMode.Bilinear)
            {
                importer.filterMode = FilterMode.Bilinear;
                needsReimport = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                needsReimport = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                needsReimport = true;
            }

            // 알파 투명 보장
            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                needsReimport = true;
            }

            if (needsReimport)
                importer.SaveAndReimport();
        }

        private static bool CreateOrUpdateSO(string assetName, List<Sprite> sprites)
        {
            string assetPath = $"{OUTPUT_FOLDER}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Data.SpriteSheetVfxSO>(assetPath);

            if (existing != null)
            {
                existing.frames = sprites.ToArray();
                existing.fps = DEFAULT_FPS;
                EditorUtility.SetDirty(existing);
                return false;
            }

            var so = ScriptableObject.CreateInstance<Data.SpriteSheetVfxSO>();
            so.frames = sprites.ToArray();
            so.fps = DEFAULT_FPS;
            so.loop = false;
            so.defaultScale = DEFAULT_SCALE;
            so.additive = false;
            so.defaultTint = Color.white;

            AssetDatabase.CreateAsset(so, assetPath);
            return true;
        }

        private static int ExtractNaturalSortKey(string fileName)
        {
            var match = Regex.Match(fileName, @"(\d+)\s*$");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int num))
                return num;

            return 0;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string[] parts = folderPath.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
