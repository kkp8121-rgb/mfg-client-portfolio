using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MkLike.Editor
{
    /// <summary>
    /// VFX_Packs 폴더 하위의 PNG 프레임들을 자동으로
    /// SpriteSheetVfxSO 에셋으로 변환하는 에디터 도구.
    /// 재귀 스캔하여 PNG가 있는 리프 폴더마다 SO를 생성한다.
    /// </summary>
    public static class VfxPackImporter
    {
        private const string VFX_PACKS_ROOT = "Assets/Folder_Assets/VFX_Packs";
        private const string OUTPUT_FOLDER = "Assets/Data/SO/VFX_Imported";
        private const int DEFAULT_FPS = 24;
        private const float DEFAULT_SCALE = 1f;

        // Frames/frames 폴더명 (대소문자 무시 비교)
        private static readonly HashSet<string> FRAMES_FOLDER_NAMES = new(StringComparer.OrdinalIgnoreCase)
        {
            "Frames", "frames"
        };

        // 스킵할 폴더명 (대소문자 무시)
        private static readonly HashSet<string> SKIP_FOLDER_NAMES = new(StringComparer.OrdinalIgnoreCase)
        {
            "sprite-sheet", "Sprite-sheet", "spritesheets", "aseprite",
            "Previews", "__MACOSX", "Font", "Icons"
        };

        // 스프라이트시트 파일명 패턴 (슬라이싱 필요)
        private static readonly Regex SPRITESHEET_PATTERN = new(@"_spritesheet\.png$", RegexOptions.IgnoreCase);

        // Downloads/ 폴더 내 VFX zip 패턴
        private const string DOWNLOADS_ROOT = "Downloads";

        private static readonly Dictionary<string, string> VFX_PACK_ZIP_MAP = new()
        {
            { "Pixel Art VFX - Blood Mage - FREE Version.zip", "Common/BloodMage" },
            { "Pixel Art VFX - Fire Mage - FREE Version.zip", "Mage/FireMage" },
            { "Pixel Art VFX - Frost Knight - FREE Version.zip", "Warrior/FrostKnight" },
            { "Pixel Art VFX - Necromancer - FREE Version.zip", "Common/Necromancer" },
            { "Pixel Art VFX - Priest - FREE Version.zip", "Common/Priest" },
            { "Pixel Art VFX - Rogue - FREE Version.zip", "Common/Rogue" },
            { "Pixel Art VFX - Smoke&Dust - Free Version.zip", "Common/SmokeDust" },
            { "Pixel Art VFX - Starcaller - FREE Version.zip", "Mage/Starcaller" },
            { "Pixel Art VFX - Vampire - FREE Version.zip", "Common/Vampire" },
            { "Pixel Art VFX - Warlock - FREE Version.zip", "Mage/Warlock" },
            { "Pixel Art VFX Impacts - FREE Version.zip", "Impacts_Explosions/PixelImpacts" },
            { "Pixel Art Skill Animations - Lightning.zip", "Mage/Lightning" },
        };

        /// <summary>
        /// Downloads/ 폴더의 VFX zip을 VFX_Packs/ 하위로 압축 해제한다.
        /// 이미 대상 폴더에 파일이 있으면 스킵한다.
        /// </summary>
        public static void ExtractVfxFromDownloads()
        {
            string projectRoot = Path.GetFullPath(Application.dataPath + "/..").Replace('\\', '/');
            string downloadsDir = $"{projectRoot}/{DOWNLOADS_ROOT}";

            if (!Directory.Exists(downloadsDir))
            {
                Debug.LogWarning($"[VfxPackImporter] Downloads 폴더 없음: {downloadsDir}");
                return;
            }

            int extracted = 0;
            int skipped = 0;

            foreach (var kvp in VFX_PACK_ZIP_MAP)
            {
                string zipFileName = kvp.Key;
                string targetSubFolder = kvp.Value;
                string zipPath = $"{downloadsDir}/{zipFileName}";

                if (!File.Exists(zipPath))
                {
                    Debug.LogWarning($"[VfxPackImporter] ZIP 없음: {zipPath}");
                    continue;
                }

                string targetDir = Path.GetFullPath($"{VFX_PACKS_ROOT}/{targetSubFolder}").Replace('\\', '/');

                // 이미 존재하면 스킵
                if (Directory.Exists(targetDir) && Directory.GetFiles(targetDir, "*.png", SearchOption.AllDirectories).Length > 0)
                {
                    skipped++;
                    continue;
                }

                try
                {
                    if (!Directory.Exists(targetDir))
                        Directory.CreateDirectory(targetDir);

                    // PowerShell로 압축 해제 (Windows)
                    string psCmd = $"Expand-Archive -Path '{zipPath.Replace('/', '\\')}' -DestinationPath '{targetDir.Replace('/', '\\')}' -Force";
                    var psi = new ProcessStartInfo("powershell", $"-NoProfile -Command \"{psCmd}\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true
                    };

                    using var proc = Process.Start(psi);
                    string stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(60000);

                    if (proc.ExitCode != 0)
                    {
                        Debug.LogError($"[VfxPackImporter] 압축 해제 실패: {zipFileName} — {stderr}");
                        continue;
                    }

                    extracted++;
                    Debug.Log($"[VfxPackImporter] 압축 해제: {zipFileName} → {targetSubFolder}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[VfxPackImporter] 압축 해제 실패: {zipFileName} — {e.Message}");
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"[VfxPackImporter] Downloads 추출 완료 — 추출: {extracted}, 스킵: {skipped}");
        }

        [MenuItem("mkLike/VFX/Import All VFX Packs")]
        public static void ImportAllVfxPacks()
        {
            if (!AssetDatabase.IsValidFolder(VFX_PACKS_ROOT))
            {
                Debug.LogError($"[VfxPackImporter] VFX_Packs 폴더 없음: {VFX_PACKS_ROOT}");
                return;
            }

            EnsureFolder(OUTPUT_FOLDER);

            int created = 0;
            int skipped = 0;
            var processedNames = new HashSet<string>();

            // 카테고리 폴더 (Warrior, Mage, Archer, Common, Impacts_Explosions)
            string fullRoot = Path.GetFullPath(VFX_PACKS_ROOT).Replace('\\', '/');
            string[] categoryDirs = Directory.GetDirectories(fullRoot);

            for (int i = 0; i < categoryDirs.Length; i++)
            {
                string categoryDir = categoryDirs[i].Replace('\\', '/');
                string categoryName = Path.GetFileName(categoryDir);

                if (ShouldSkipFolder(categoryName))
                    continue;

                // 서브폴더 (Warrior, FireMage, Lightning, etc.)
                string[] subDirs = Directory.GetDirectories(categoryDir);
                for (int j = 0; j < subDirs.Length; j++)
                {
                    string subDir = subDirs[j].Replace('\\', '/');
                    string subName = Path.GetFileName(subDir);

                    if (ShouldSkipFolder(subName))
                        continue;

                    // 이 서브폴더 하위를 재귀 탐색하여 PNG 리프 폴더 수집
                    ScanAndCreateSOs(
                        subDir, categoryName, subName,
                        ref created, ref skipped, processedNames);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[VfxPackImporter] 완료 -- 생성: {created}, 스킵(이미 존재): {skipped}, 총 SO: {created + skipped}");
        }

        /// <summary>
        /// 주어진 디렉토리에서 PNG 리프 폴더를 찾아 SO를 생성한다.
        /// VFX{N} 패턴 폴더 → Frames 서브폴더 or 직접 PNG
        /// part{N}(label) 패턴 → 각 파트별 별도 SO
        /// 숫자만 폴더 (1, 2, 3) → 각각 별도 SO
        /// *_spritesheet.png → 스프라이트시트 슬라이싱 후 개별 SO 생성
        /// </summary>
        private static void ScanAndCreateSOs(
            string dir, string category, string subName,
            ref int created, ref int skipped,
            HashSet<string> processedNames)
        {
            dir = dir.Replace('\\', '/');

            // 이 폴더에 직접 PNG가 있는지 확인
            var pngsHere = GetPngFiles(dir);

            // 스프라이트시트 파일과 일반 프레임 파일 분리
            var spritesheets = new List<string>();
            var normalPngs = new List<string>();

            for (int i = 0; i < pngsHere.Length; i++)
            {
                string fileName = Path.GetFileName(pngsHere[i]);
                if (SPRITESHEET_PATTERN.IsMatch(fileName))
                    spritesheets.Add(pngsHere[i]);
                else
                    normalPngs.Add(pngsHere[i]);
            }

            // 스프라이트시트가 있으면 각각 개별 SO 생성
            if (spritesheets.Count > 0)
            {
                for (int i = 0; i < spritesheets.Count; i++)
                {
                    string sheetPath = spritesheets[i].Replace('\\', '/');
                    string sheetFileName = Path.GetFileNameWithoutExtension(sheetPath);
                    // "10_weaponhit_spritesheet" → "weaponhit"
                    string cleanName = ExtractSpritesheetName(sheetFileName);
                    string soName = $"Vfx_{category.Replace("_", "")}_{subName}_{cleanName}";
                    soName = Regex.Replace(soName, @"[^a-zA-Z0-9_]", "");

                    CreateSOFromSpritesheet(sheetPath, soName, ref created, ref skipped, processedNames);
                }
                return;
            }

            // 하위 폴더 탐색
            string[] childDirs;
            try { childDirs = Directory.GetDirectories(dir); }
            catch { childDirs = Array.Empty<string>(); }

            // Frames/frames 폴더가 있으면 그 안의 PNG 사용
            string framesDir = FindFramesSubfolder(childDirs);

            if (framesDir != null)
            {
                // Frames 폴더 발견 → 이 VFX 폴더의 SO 생성
                string soName = BuildSOName(dir, category, subName);
                CreateSOFromFolder(framesDir, soName, ref created, ref skipped, processedNames);
                return;
            }

            // Frames 폴더 없고 직접 PNG가 있으면 이 폴더 사용
            if (normalPngs.Count > 0)
            {
                string soName = BuildSOName(dir, category, subName);
                CreateSOFromFolder(dir, soName, ref created, ref skipped, processedNames);
                return;
            }

            // PNG도 없고 Frames도 없으면 하위 폴더 재귀
            for (int i = 0; i < childDirs.Length; i++)
            {
                string childDir = childDirs[i].Replace('\\', '/');
                string childName = Path.GetFileName(childDir);

                if (ShouldSkipFolder(childName))
                    continue;

                ScanAndCreateSOs(childDir, category, subName, ref created, ref skipped, processedNames);
            }
        }

        /// <summary>
        /// 폴더 경로에서 SO 이름을 생성한다.
        /// 예: VFX_Packs/Warrior/Warrior/VFX 1/Frames → Vfx_Warrior_Warrior_1
        ///     VFX_Packs/Common/BloodMage/VFX1/part1(start)/frames → Vfx_Common_BloodMage_1_start
        ///     VFX_Packs/Impacts_Explosions/Explosions/1/frames → Vfx_ImpactsExplosions_Explosions_1
        /// </summary>
        private static string BuildSOName(string folderPath, string category, string subName)
        {
            folderPath = folderPath.Replace('\\', '/');

            // VFX_Packs/{category}/{subName}/ 이후의 상대 경로 추출
            string marker = $"/{subName}/";
            int idx = folderPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            string relativePart = "";
            if (idx >= 0)
                relativePart = folderPath.Substring(idx + marker.Length);

            // 카테고리에서 언더스코어 제거 (Impacts_Explosions → ImpactsExplosions)
            string cleanCategory = category.Replace("_", "");

            // 상대 경로에서 의미 있는 부분 추출
            var segments = new List<string>();

            if (!string.IsNullOrEmpty(relativePart))
            {
                string[] parts = relativePart.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i];

                    // frames/Frames 폴더는 스킵
                    if (FRAMES_FOLDER_NAMES.Contains(part))
                        continue;

                    // VFX{N} / VFX {N} / Effect{N} / FXpack{N} → 숫자만 추출
                    var vfxMatch = Regex.Match(part, @"^(?:VFX|Effect|FXpack)\s*(\d+)$", RegexOptions.IgnoreCase);
                    if (vfxMatch.Success)
                    {
                        segments.Add(vfxMatch.Groups[1].Value);
                        continue;
                    }

                    // part{N}(label) → label 추출
                    var partMatch = Regex.Match(part, @"^part\d+\((\w+)\)$", RegexOptions.IgnoreCase);
                    if (partMatch.Success)
                    {
                        segments.Add(partMatch.Groups[1].Value);
                        continue;
                    }

                    // V{N} (128X128) 같은 해상도 폴더 → V{N} 만
                    var versionMatch = Regex.Match(part, @"^(V\d+)\s*\(", RegexOptions.IgnoreCase);
                    if (versionMatch.Success)
                    {
                        segments.Add(versionMatch.Groups[1].Value);
                        continue;
                    }

                    // 숫자만 폴더 (1, 2, 3)
                    if (Regex.IsMatch(part, @"^\d+$"))
                    {
                        segments.Add(part);
                        continue;
                    }

                    // 그 외: 공백/특수문자 제거한 이름
                    string cleaned = Regex.Replace(part, @"[^a-zA-Z0-9]", "");
                    if (!string.IsNullOrEmpty(cleaned))
                        segments.Add(cleaned);
                }
            }

            string suffix = segments.Count > 0 ? "_" + string.Join("_", segments) : "";
            string soName = $"Vfx_{cleanCategory}_{subName}{suffix}";

            // 파일명에 사용 불가 문자 제거
            soName = Regex.Replace(soName, @"[^a-zA-Z0-9_]", "");

            return soName;
        }

        private static void CreateSOFromFolder(
            string pngFolder, string soName,
            ref int created, ref int skipped,
            HashSet<string> processedNames)
        {
            // 중복 이름 방지
            string uniqueName = soName;
            int dupIndex = 2;
            while (processedNames.Contains(uniqueName))
            {
                uniqueName = $"{soName}_{dupIndex}";
                dupIndex++;
            }
            soName = uniqueName;

            string assetPath = $"{OUTPUT_FOLDER}/{soName}.asset";

            // 이미 존재하면 fps/ignoreFlip만 갱신
            var existing = AssetDatabase.LoadAssetAtPath<Data.SpriteSheetVfxSO>(assetPath);
            if (existing != null)
            {
                bool changed = false;
                if (existing.fps < DEFAULT_FPS) { existing.fps = DEFAULT_FPS; changed = true; }
                bool shouldIgnore = ShouldIgnoreFlip(soName);
                if (existing.ignoreFlip != shouldIgnore) { existing.ignoreFlip = shouldIgnore; changed = true; }
                bool shouldFlipY = ShouldFlipY(soName);
                if (existing.flipY != shouldFlipY) { existing.flipY = shouldFlipY; changed = true; }
                if (changed) EditorUtility.SetDirty(existing);
                skipped++;
                processedNames.Add(soName);
                return;
            }

            // Unity 상대 경로로 변환
            string unityFolder = ToUnityPath(pngFolder);
            if (string.IsNullOrEmpty(unityFolder))
            {
                Debug.LogWarning($"[VfxPackImporter] Unity 경로 변환 실패: {pngFolder}");
                return;
            }

            var sprites = CollectSpritesFromFolder(unityFolder);
            if (sprites.Count == 0)
            {
                Debug.LogWarning($"[VfxPackImporter] 스프라이트 없음: {unityFolder}");
                return;
            }

            var so = ScriptableObject.CreateInstance<Data.SpriteSheetVfxSO>();
            so.frames = sprites.ToArray();
            so.fps = DEFAULT_FPS;
            so.loop = false;
            so.defaultScale = DEFAULT_SCALE;
            so.additive = false;
            so.defaultTint = Color.white;
            so.ignoreFlip = ShouldIgnoreFlip(soName);
            so.flipY = ShouldFlipY(soName);

            AssetDatabase.CreateAsset(so, assetPath);
            Undo.RegisterCreatedObjectUndo(so, $"Create VFX SO: {soName}");

            processedNames.Add(soName);
            created++;

            Debug.Log($"[VfxPackImporter] 생성: {soName} ({sprites.Count} frames) ← {unityFolder}");
        }

        /// <summary>
        /// 폴더 내 PNG를 스캔하여 자연순서 정렬된 Sprite 리스트를 반환한다.
        /// SlashVfxSetupEditor 패턴과 동일.
        /// </summary>
        private static List<Sprite> CollectSpritesFromFolder(string folderPath)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
            var spriteEntries = new List<(int sortKey, string secondaryKey, Sprite sprite)>();

            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);

                // 직접 하위만 (서브폴더 파일 제외)
                string assetDir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
                if (!string.Equals(assetDir, folderPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    continue;

                ConfigureTextureImporter(assetPath);

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite == null)
                {
                    Debug.LogWarning($"[VfxPackImporter] Sprite 로드 실패: {assetPath}");
                    continue;
                }

                string fileName = Path.GetFileNameWithoutExtension(assetPath);
                int sortKey = ExtractNaturalSortKey(fileName);
                spriteEntries.Add((sortKey, fileName, sprite));
            }

            // 자연 정렬: 숫자 기준 → 파일명 알파벳
            spriteEntries.Sort((a, b) =>
            {
                int cmp = a.sortKey.CompareTo(b.sortKey);
                return cmp != 0 ? cmp : NaturalStringCompare(a.secondaryKey, b.secondaryKey);
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

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                needsReimport = true;
            }

            if (needsReimport)
                importer.SaveAndReimport();
        }

        /// <summary>
        /// 파일명에서 자연순서 정렬용 숫자를 추출한다.
        /// "frame10" → 10, "warrior_skill1_frame3" → 3, "15" → 15
        /// </summary>
        private static int ExtractNaturalSortKey(string fileName)
        {
            // 파일명 끝의 숫자 추출
            var match = Regex.Match(fileName, @"(\d+)\s*$");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int num))
                return num;

            return 0;
        }

        /// <summary>
        /// 자연순서 문자열 비교 (숫자 부분을 수치로 비교).
        /// "frame2" < "frame10"
        /// </summary>
        private static int NaturalStringCompare(string a, string b)
        {
            int ia = 0, ib = 0;
            while (ia < a.Length && ib < b.Length)
            {
                if (char.IsDigit(a[ia]) && char.IsDigit(b[ib]))
                {
                    // 숫자 부분 추출 후 수치 비교
                    long numA = 0, numB = 0;
                    while (ia < a.Length && char.IsDigit(a[ia]))
                    {
                        numA = numA * 10 + (a[ia] - '0');
                        ia++;
                    }
                    while (ib < b.Length && char.IsDigit(b[ib]))
                    {
                        numB = numB * 10 + (b[ib] - '0');
                        ib++;
                    }
                    if (numA != numB)
                        return numA.CompareTo(numB);
                }
                else
                {
                    int cmp = char.ToLowerInvariant(a[ia]).CompareTo(char.ToLowerInvariant(b[ib]));
                    if (cmp != 0)
                        return cmp;
                    ia++;
                    ib++;
                }
            }
            return a.Length.CompareTo(b.Length);
        }

        private static string FindFramesSubfolder(string[] childDirs)
        {
            for (int i = 0; i < childDirs.Length; i++)
            {
                string dirName = Path.GetFileName(childDirs[i]);
                if (FRAMES_FOLDER_NAMES.Contains(dirName))
                    return childDirs[i].Replace('\\', '/');
            }
            return null;
        }

        private static string[] GetPngFiles(string dir)
        {
            try
            {
                return Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>원형/비방향성 이펙트인지 판별하여 ignoreFlip 설정.</summary>
        private static bool ShouldIgnoreFlip(string soName)
        {
            string lower = soName.ToLowerInvariant();
            return lower.Contains("impacts") ||
                   lower.Contains("explosion") ||
                   lower.Contains("priest") ||
                   lower.Contains("portal") ||
                   lower.Contains("blood");
        }

        /// <summary>위아래 뒤집기가 필요한 이펙트 판별 (내려치기→올려치기 변환).</summary>
        private static bool ShouldFlipY(string soName)
        {
            string lower = soName.ToLowerInvariant();
            return lower.Contains("frostknight") ||
                   lower.Contains("paladin");
        }

        private static bool ShouldSkipFolder(string folderName)
        {
            return SKIP_FOLDER_NAMES.Contains(folderName);
        }

        /// <summary>
        /// 절대 경로를 Assets/ 상대 Unity 경로로 변환한다.
        /// </summary>
        private static string ToUnityPath(string fullPath)
        {
            fullPath = fullPath.Replace('\\', '/');
            int assetsIdx = fullPath.IndexOf("Assets/", StringComparison.OrdinalIgnoreCase);
            if (assetsIdx < 0)
                return null;
            return fullPath.Substring(assetsIdx);
        }

        /// <summary>
        /// 스프라이트시트 파일명에서 효과 이름을 추출한다.
        /// "10_weaponhit_spritesheet" → "weaponhit"
        /// "1_magicspell_spritesheet" → "magicspell"
        /// </summary>
        private static string ExtractSpritesheetName(string fileName)
        {
            // {번호}_{이름}_spritesheet 패턴
            var match = Regex.Match(fileName, @"^\d+_(.+?)_spritesheet$", RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value;

            // _spritesheet 접미사만 제거
            string cleaned = Regex.Replace(fileName, @"_spritesheet$", "", RegexOptions.IgnoreCase);
            // 앞쪽 숫자+언더스코어 제거
            cleaned = Regex.Replace(cleaned, @"^\d+_", "");
            return string.IsNullOrEmpty(cleaned) ? fileName : cleaned;
        }

        /// <summary>
        /// 스프라이트시트 PNG를 Multiple 모드로 슬라이싱하여 SO를 생성한다.
        /// TextureImporter에서 자동 슬라이싱 → 서브스프라이트 로드 → SO frames[] 할당.
        /// </summary>
        private static void CreateSOFromSpritesheet(
            string sheetPath, string soName,
            ref int created, ref int skipped,
            HashSet<string> processedNames)
        {
            // 중복 이름 방지
            string uniqueName = soName;
            int dupIndex = 2;
            while (processedNames.Contains(uniqueName))
            {
                uniqueName = $"{soName}_{dupIndex}";
                dupIndex++;
            }
            soName = uniqueName;

            string assetPath = $"{OUTPUT_FOLDER}/{soName}.asset";

            // 이미 존재하면 스킵
            var existing = AssetDatabase.LoadAssetAtPath<Data.SpriteSheetVfxSO>(assetPath);
            if (existing != null)
            {
                // frames가 비어있으면 재갱신
                if (existing.frames != null && existing.frames.Length > 0)
                {
                    skipped++;
                    processedNames.Add(soName);
                    return;
                }
            }

            // Unity 상대 경로로 변환
            string unityPath = ToUnityPath(sheetPath);
            if (string.IsNullOrEmpty(unityPath))
            {
                Debug.LogWarning($"[VfxPackImporter] Unity 경로 변환 실패: {sheetPath}");
                return;
            }

            // TextureImporter를 Multiple 모드로 설정 + 자동 슬라이싱
            var importer = AssetImporter.GetAtPath(unityPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[VfxPackImporter] TextureImporter 없음: {unityPath}");
                return;
            }

            bool needsReimport = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                needsReimport = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Multiple)
            {
                importer.spriteImportMode = SpriteImportMode.Multiple;
                needsReimport = true;
            }

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

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                needsReimport = true;
            }

            // 서브스프라이트가 없으면 자동 슬라이싱 (그리드 기반)
            if (importer.spritesheet == null || importer.spritesheet.Length == 0)
            {
                // 텍스처 크기 읽기
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(unityPath);
                if (tex == null)
                {
                    Debug.LogWarning($"[VfxPackImporter] 텍스처 로드 실패: {unityPath}");
                    return;
                }

                int texW = tex.width;
                int texH = tex.height;

                // 정사각 셀 크기 추정: 높이가 한 줄이라고 가정 → cellSize = texH
                // CodeManu 스프라이트시트는 가로로 나열된 정사각 프레임
                int cellSize = texH;
                int cols = Mathf.Max(1, texW / cellSize);
                int rows = 1;

                // 여러 줄이면 행도 계산
                if (texW == texH)
                {
                    // 정사각 시트 → 그리드 추정 (예: 512x512, 64x64 셀 → 8x8)
                    cellSize = Mathf.Max(32, texH / Mathf.RoundToInt(Mathf.Sqrt(texW * texH / (64f * 64f))));
                    cols = Mathf.Max(1, texW / cellSize);
                    rows = Mathf.Max(1, texH / cellSize);
                }
                else if (texH > cellSize)
                {
                    rows = Mathf.Max(1, texH / cellSize);
                }

                var spriteData = new SpriteMetaData[cols * rows];
                int idx = 0;
                for (int row = 0; row < rows; row++)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        spriteData[idx] = new SpriteMetaData
                        {
                            name = $"{soName}_{idx}",
                            rect = new Rect(col * cellSize, (rows - 1 - row) * cellSize, cellSize, cellSize),
                            alignment = (int)SpriteAlignment.Center,
                            pivot = new Vector2(0.5f, 0.5f)
                        };
                        idx++;
                    }
                }

                importer.spritesheet = spriteData;
                needsReimport = true;
            }

            if (needsReimport)
                importer.SaveAndReimport();

            // 슬라이싱 후 서브스프라이트 로드
            var allObjects = AssetDatabase.LoadAllAssetsAtPath(unityPath);
            var sprites = new List<Sprite>();
            for (int i = 0; i < allObjects.Length; i++)
            {
                if (allObjects[i] is Sprite sprite)
                    sprites.Add(sprite);
            }

            if (sprites.Count == 0)
            {
                Debug.LogWarning($"[VfxPackImporter] 슬라이싱 후 스프라이트 없음: {unityPath}");
                return;
            }

            // 이름 기준 자연 정렬 (name_0, name_1, ..., name_9, name_10)
            sprites.Sort((a, b) =>
            {
                int keyA = ExtractNaturalSortKey(a.name);
                int keyB = ExtractNaturalSortKey(b.name);
                int cmp = keyA.CompareTo(keyB);
                return cmp != 0 ? cmp : NaturalStringCompare(a.name, b.name);
            });

            // SO 생성 또는 갱신
            Data.SpriteSheetVfxSO so;
            if (existing != null)
            {
                so = existing;
            }
            else
            {
                so = ScriptableObject.CreateInstance<Data.SpriteSheetVfxSO>();
                so.fps = DEFAULT_FPS;
                so.loop = false;
                so.defaultScale = DEFAULT_SCALE;
                so.additive = false;
                so.defaultTint = Color.white;
                so.ignoreFlip = ShouldIgnoreFlip(soName);
                so.flipY = false;
            }

            so.frames = sprites.ToArray();

            if (existing == null)
            {
                AssetDatabase.CreateAsset(so, assetPath);
                Undo.RegisterCreatedObjectUndo(so, $"Create VFX SO: {soName}");
                created++;
            }
            else
            {
                EditorUtility.SetDirty(so);
                skipped++;
            }

            processedNames.Add(soName);
            Debug.Log($"[VfxPackImporter] 스프라이트시트 SO: {soName} ({sprites.Count} frames) ← {unityPath}");
        }

        /// <summary>
        /// 기존 VFX_Imported SO들의 frames[]를 소스 PNG에서 재갱신한다.
        /// SO 이름으로 원본 폴더를 역추적하여 최신 스프라이트를 로드한다.
        /// </summary>
        public static void RefreshExistingFrames()
        {
            if (!AssetDatabase.IsValidFolder(OUTPUT_FOLDER))
            {
                Debug.LogError($"[VfxPackImporter] 출력 폴더 없음: {OUTPUT_FOLDER}");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:SpriteSheetVfxSO", new[] { OUTPUT_FOLDER });
            int refreshed = 0;
            int failed = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var so = AssetDatabase.LoadAssetAtPath<Data.SpriteSheetVfxSO>(path);
                if (so == null) continue;

                // 이미 frames가 채워져 있고 유효하면 스킵
                if (so.frames != null && so.frames.Length > 0 && so.frames[0] != null)
                    continue;

                // SO 이름에서 원본 폴더 경로 역추적 시도
                string soName = Path.GetFileNameWithoutExtension(path);
                var sourcePath = FindSourceFolderForSO(soName);
                if (sourcePath == null)
                {
                    failed++;
                    continue;
                }

                // 스프라이트시트인지 일반 폴더인지 판별
                if (File.Exists(sourcePath) && sourcePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    // 스프라이트시트 → 서브스프라이트 로드
                    string unityPath = ToUnityPath(sourcePath);
                    if (unityPath == null) { failed++; continue; }

                    var allObjects = AssetDatabase.LoadAllAssetsAtPath(unityPath);
                    var sprites = new List<Sprite>();
                    for (int j = 0; j < allObjects.Length; j++)
                    {
                        if (allObjects[j] is Sprite sprite)
                            sprites.Add(sprite);
                    }
                    if (sprites.Count == 0) { failed++; continue; }

                    sprites.Sort((a, b) =>
                    {
                        int cmp = ExtractNaturalSortKey(a.name).CompareTo(ExtractNaturalSortKey(b.name));
                        return cmp != 0 ? cmp : NaturalStringCompare(a.name, b.name);
                    });

                    so.frames = sprites.ToArray();
                }
                else if (Directory.Exists(sourcePath))
                {
                    // 일반 폴더 → 폴더 내 스프라이트 수집
                    string unityFolder = ToUnityPath(sourcePath);
                    if (unityFolder == null) { failed++; continue; }

                    var sprites = CollectSpritesFromFolder(unityFolder);
                    if (sprites.Count == 0) { failed++; continue; }

                    so.frames = sprites.ToArray();
                }
                else
                {
                    failed++;
                    continue;
                }

                EditorUtility.SetDirty(so);
                refreshed++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[VfxPackImporter] RefreshExistingFrames 완료 — 갱신: {refreshed}, 실패: {failed}");
        }

        /// <summary>
        /// SO 이름으로 원본 소스 경로를 역추적한다.
        /// Vfx_{Category}_{SubName}_{...} 패턴을 해석하여 VFX_Packs/ 하위 경로를 반환.
        /// </summary>
        private static string FindSourceFolderForSO(string soName)
        {
            // Vfx_ImpactsExplosions_CodeManu_magicspell → 스프라이트시트
            var spritesheetMatch = Regex.Match(soName, @"^Vfx_(\w+?)_CodeManu_(\w+)$", RegexOptions.IgnoreCase);
            if (spritesheetMatch.Success)
            {
                string effectName = spritesheetMatch.Groups[2].Value;
                string fullRoot = Path.GetFullPath(VFX_PACKS_ROOT).Replace('\\', '/');
                // CodeManu 폴더에서 이름이 매칭되는 스프라이트시트 파일 검색
                string codeManDir = $"{fullRoot}/Impacts_Explosions/CodeManu";
                if (Directory.Exists(codeManDir))
                {
                    var files = Directory.GetFiles(codeManDir, "*_spritesheet.png");
                    for (int i = 0; i < files.Length; i++)
                    {
                        string fName = Path.GetFileNameWithoutExtension(files[i]);
                        if (ExtractSpritesheetName(fName).Equals(effectName, StringComparison.OrdinalIgnoreCase))
                            return files[i].Replace('\\', '/');
                    }
                }
                return null;
            }

            // 일반 폴더 SO — VFX_Packs/ 재귀 스캔은 비용이 크므로 null 반환
            // RefreshExistingFrames는 frames가 비어있는 SO만 대상이므로 대부분 스프라이트시트
            return null;
        }

        /// <summary>
        /// 모든 SkillDataSO의 vfxSheet/buffVfxSheet를 SO 이름 기반으로 자동 매핑한다.
        /// 스킬 ID와 VFX SO 이름의 직업/스킬명 부분을 매칭한다.
        /// </summary>
        [MenuItem("mkLike/VFX/Auto Map Skill VFX")]
        public static void AutoMapSkillVfx()
        {
            // 모든 VFX SO 로드
            string[] vfxGuids = AssetDatabase.FindAssets("t:SpriteSheetVfxSO");
            var vfxByName = new Dictionary<string, Data.SpriteSheetVfxSO>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < vfxGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(vfxGuids[i]);
                var vfx = AssetDatabase.LoadAssetAtPath<Data.SpriteSheetVfxSO>(path);
                if (vfx == null) continue;
                string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                vfxByName[name] = vfx;
            }

            // 모든 SkillDataSO 로드
            string[] skillGuids = AssetDatabase.FindAssets("t:SkillDataSO");
            int mapped = 0;

            // 직업 키워드 → VFX 카테고리 매핑
            var jobToCategory = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { "warrior", new[] { "Warrior" } },
                { "knight", new[] { "Warrior" } },
                { "titan", new[] { "Warrior" } },
                { "warlord", new[] { "Warrior" } },
                { "dragonslayer", new[] { "Warrior" } },
                { "archer", new[] { "Archer" } },
                { "scout", new[] { "Archer" } },
                { "hawkeye", new[] { "Archer" } },
                { "windwalker", new[] { "Archer" } },
                { "stormbringer", new[] { "Archer" } },
                { "mage", new[] { "Mage" } },
                { "sorcerer", new[] { "Mage" } },
                { "runemaster", new[] { "Mage" } },
                { "sage", new[] { "Mage" } },
                { "archmage", new[] { "Mage" } },
            };

            for (int i = 0; i < skillGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(skillGuids[i]);
                var skill = AssetDatabase.LoadAssetAtPath<Data.SkillDataSO>(path);
                if (skill == null || skill.vfxSheet != null) continue;

                // 스킬 ID에서 직업 + 스킬명 추출: "warrior_strike" → job="warrior", skillName="strike"
                string skillId = skill.id;
                if (string.IsNullOrEmpty(skillId)) continue;

                string[] parts = skillId.Split('_');
                if (parts.Length < 2) continue;

                string jobKey = parts[0];
                string skillName = parts[1];

                // VFX SO 검색: 카테고리별로 이름에 스킬명이 포함된 SO 찾기
                if (!jobToCategory.TryGetValue(jobKey, out var categories)) continue;

                Data.SpriteSheetVfxSO bestMatch = null;

                foreach (var vfxKvp in vfxByName)
                {
                    string vfxName = vfxKvp.Key;
                    bool categoryMatch = false;

                    for (int c = 0; c < categories.Length; c++)
                    {
                        if (vfxName.Contains(categories[c].ToLowerInvariant()))
                        {
                            categoryMatch = true;
                            break;
                        }
                    }

                    // 카테고리 일치 + 스킬명 포함
                    if (categoryMatch && vfxName.Contains(skillName.ToLowerInvariant()))
                    {
                        bestMatch = vfxKvp.Value;
                        break;
                    }

                    // Common 카테고리도 폴백으로 체크
                    if (bestMatch == null && vfxName.Contains("common") && vfxName.Contains(skillName.ToLowerInvariant()))
                        bestMatch = vfxKvp.Value;
                }

                if (bestMatch != null)
                {
                    skill.vfxSheet = bestMatch;
                    EditorUtility.SetDirty(skill);
                    mapped++;
                    Debug.Log($"[VfxPackImporter] AutoMap: {skillId} → {bestMatch.name}");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[VfxPackImporter] AutoMapSkillVfx 완료 — 매핑: {mapped}");
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
