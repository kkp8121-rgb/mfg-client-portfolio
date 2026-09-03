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
    /// 누락된 장비 아이콘을 팔레트 스왑으로 자동 생성하는 에디터 도구.
    /// 같은 슬롯의 기존 아이콘을 소스로 사용하여 색조 변환 후 PNG 저장.
    /// </summary>
    public static class EquipmentIconPaletteSwap
    {
        private const string OUTPUT_DIR = "Assets/Resources/Icons/Equipment";
        private const string LOG_PREFIX = "[EquipIconPaletteSwap]";

        /// <summary>
        /// 장비 변종별 틴트 색상 정의.
        /// id 키워드 → (틴트 색상, 밝기 보정)
        /// </summary>
        private static readonly Dictionary<string, (Color tint, float brightnessBoost)> VariantTintMap = new()
        {
            // 방어구 변종 (arcane/dragon/mithril)
            { "arcane",   (new Color(0.70f, 0.30f, 0.90f), 0.15f) },  // 보라/바이올렛
            { "dragon",   (new Color(0.95f, 0.30f, 0.15f), 0.10f) },  // 빨강/오렌지
            { "mithril",  (new Color(0.70f, 0.80f, 0.95f), 0.25f) },  // 실버/라이트 블루

            // 얼굴 장식 변종
            { "crown",    (new Color(1.00f, 0.85f, 0.20f), 0.20f) },  // 골드
            { "mask",     (new Color(0.40f, 0.40f, 0.45f), 0.05f) },  // 다크 실버
            { "monocle",  (new Color(0.85f, 0.75f, 0.50f), 0.15f) },  // 앤틱 골드

            // 반지 변종
            { "gold",     (new Color(1.00f, 0.85f, 0.20f), 0.20f) },  // 골드
            { "diamond",  (new Color(0.80f, 0.92f, 1.00f), 0.30f) },  // 다이아몬드 (밝은 하늘색)
            { "mythic",   (new Color(0.90f, 0.50f, 1.00f), 0.20f) },  // 미식 (보라+분홍)

            // 목걸이 변종
            { "ruby",     (new Color(0.95f, 0.15f, 0.25f), 0.10f) },  // 루비 (진홍)
            { "sapphire", (new Color(0.20f, 0.35f, 0.95f), 0.10f) },  // 사파이어 (진청)
            // "dragon"은 위에서 이미 정의됨 → 목걸이에도 재사용
        };

        [MenuItem("mkLike/Tools/Generate Equipment Icons")]
        public static void GenerateEquipmentIcons()
        {
            // 출력 디렉토리 생성
            EnsureDirectoryExists(OUTPUT_DIR);

            // 모든 EquipmentDataSO 로드
            string[] guids = AssetDatabase.FindAssets("t:EquipmentDataSO");
            if (guids.Length == 0)
            {
                Debug.LogWarning($"{LOG_PREFIX} EquipmentDataSO 에셋을 찾을 수 없습니다.");
                return;
            }

            var allSOs = new List<EquipmentDataSO>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<EquipmentDataSO>(path);
                if (so != null)
                    allSOs.Add(so);
            }

            // 중복 제거 (Resources + Data/SO 양쪽에 같은 SO가 있을 수 있음)
            var uniqueSOs = allSOs
                .GroupBy(so => so.id)
                .Select(g => g.First())
                .ToList();

            // 슬롯별 소스 아이콘 맵 구축 (아이콘이 있는 SO → 소스)
            var sourceBySlot = new Dictionary<EquipmentSlot, EquipmentDataSO>();
            foreach (var so in uniqueSOs)
            {
                if (so.icon != null && !sourceBySlot.ContainsKey(so.slot))
                    sourceBySlot[so.slot] = so;
            }

            int generated = 0;
            int skipped = 0;
            int failed = 0;

            // 아이콘 없는 SO 처리
            foreach (var so in uniqueSOs)
            {
                if (so.icon != null)
                {
                    skipped++;
                    continue;
                }

                // 같은 슬롯에서 소스 아이콘 찾기
                if (!sourceBySlot.TryGetValue(so.slot, out var sourceSO))
                {
                    Debug.LogWarning($"{LOG_PREFIX} {so.name}: 슬롯 {so.slot}에 소스 아이콘이 없습니다.");
                    failed++;
                    continue;
                }

                // 변종 키워드 추출
                string variantKey = ExtractVariantKey(so.id);
                if (!VariantTintMap.TryGetValue(variantKey, out var tintInfo))
                {
                    Debug.LogWarning($"{LOG_PREFIX} {so.name}: 변종 '{variantKey}'에 대한 틴트 정보가 없습니다.");
                    failed++;
                    continue;
                }

                // 팔레트 스왑 실행
                Sprite newSprite = CreatePaletteSwappedIcon(
                    sourceSO.icon,
                    so.id,
                    tintInfo.tint,
                    tintInfo.brightnessBoost
                );

                if (newSprite != null)
                {
                    // 모든 동일 id SO에 아이콘 할당 (Resources + Data/SO 양쪽)
                    foreach (var dupSO in allSOs.Where(s => s.id == so.id))
                    {
                        dupSO.icon = newSprite;
                        EditorUtility.SetDirty(dupSO);
                    }
                    generated++;
                    Debug.Log($"{LOG_PREFIX} 생성: {so.name} ← {sourceSO.icon.name} (틴트: {variantKey})");
                }
                else
                {
                    failed++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"{LOG_PREFIX} 완료 — 생성: {generated}, 스킵(기존 아이콘): {skipped}, 실패: {failed}");
        }

        /// <summary>
        /// 소스 스프라이트에 팔레트 스왑을 적용하여 새 PNG 스프라이트를 생성한다.
        /// </summary>
        private static Sprite CreatePaletteSwappedIcon(
            Sprite sourceSprite,
            string targetId,
            Color tintColor,
            float brightnessBoost)
        {
            // 소스 텍스처 읽기 가능 확인
            Texture2D sourceTexture = MakeReadable(sourceSprite.texture);
            if (sourceTexture == null)
            {
                Debug.LogError($"{LOG_PREFIX} 소스 텍스처를 읽을 수 없습니다: {sourceSprite.name}");
                return null;
            }

            // 스프라이트 영역 추출 (텍스처 아틀라스 대응)
            Rect spriteRect = sourceSprite.rect;
            int x = Mathf.FloorToInt(spriteRect.x);
            int y = Mathf.FloorToInt(spriteRect.y);
            int w = Mathf.FloorToInt(spriteRect.width);
            int h = Mathf.FloorToInt(spriteRect.height);

            Color[] sourcePixels = sourceTexture.GetPixels(x, y, w, h);

            // 팔레트 스왑 적용
            Color[] tintedPixels = ApplyPaletteSwap(sourcePixels, tintColor, brightnessBoost);

            // 새 텍스처 생성
            var newTexture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            newTexture.SetPixels(tintedPixels);
            newTexture.Apply();

            // PNG 저장
            string fileName = $"icon_{targetId}.png";
            string filePath = $"{OUTPUT_DIR}/{fileName}";
            byte[] pngBytes = newTexture.EncodeToPNG();
            Object.DestroyImmediate(newTexture);

            string absolutePath = Path.Combine(Application.dataPath, "..", filePath);
            absolutePath = Path.GetFullPath(absolutePath);
            File.WriteAllBytes(absolutePath, pngBytes);

            // 임포트 설정 적용
            AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceUpdate);
            ConfigureTextureImporter(filePath);
            AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceUpdate);

            // 스프라이트 로드
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(filePath);
            if (sprite == null)
                Debug.LogError($"{LOG_PREFIX} 스프라이트 로드 실패: {filePath}");

            return sprite;
        }

        /// <summary>
        /// 팔레트 스왑 알고리즘.
        /// HSV 색조 회전 + 틴트 블렌딩으로 원본의 명암/디테일을 유지하면서 색상 변경.
        /// </summary>
        private static Color[] ApplyPaletteSwap(Color[] pixels, Color tintColor, float brightnessBoost)
        {
            // 틴트 색상의 HSV 추출
            Color.RGBToHSV(tintColor, out float tintH, out float tintS, out float _);

            var result = new Color[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color src = pixels[i];

                // 투명 픽셀은 그대로
                if (src.a < 0.01f)
                {
                    result[i] = src;
                    continue;
                }

                // 원본 HSV
                Color.RGBToHSV(src, out float srcH, out float srcS, out float srcV);

                // 채도가 낮은 픽셀 (회색/흰색/검정 테두리)은 약하게만 틴트
                float saturationFactor = Mathf.Clamp01(srcS * 2.0f); // 채도 비례 블렌딩
                float blendFactor = Mathf.Lerp(0.15f, 0.7f, saturationFactor);

                // 색조 회전: 원본 색조를 틴트 색조로 대체
                float newH = Mathf.Lerp(srcH, tintH, blendFactor);
                // 채도: 틴트 채도로 블렌딩
                float newS = Mathf.Lerp(srcS, tintS, blendFactor * 0.8f);
                // 밝기: 약간 부스트
                float newV = Mathf.Clamp01(srcV + brightnessBoost * blendFactor);

                Color newColor = Color.HSVToRGB(newH, newS, newV);
                newColor.a = src.a; // 알파 유지

                result[i] = newColor;
            }

            return result;
        }

        /// <summary>
        /// 텍스처를 읽기 가능한 복사본으로 변환한다.
        /// 원본 텍스처가 Read/Write 비활성이면 RenderTexture를 통해 읽는다.
        /// </summary>
        private static Texture2D MakeReadable(Texture2D source)
        {
            // 먼저 isReadable 확인 후 직접 읽기 시도
            if (source.isReadable)
                return source;

            // Read/Write 비활성 → 임포터 설정 변경
            string assetPath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(assetPath))
                return ReadViaRenderTexture(source);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return ReadViaRenderTexture(source);

            // 임시로 Read/Write 활성화하여 픽셀 복사 후 복구
            importer.isReadable = true;
            importer.SaveAndReimport();

            var reloaded = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

            // 픽셀 데이터를 독립 텍스처로 복사 (RGBA32로 통일 — 압축 포맷 대응)
            var copy = new Texture2D(reloaded.width, reloaded.height, TextureFormat.RGBA32, false);
            copy.SetPixels(reloaded.GetPixels());
            copy.Apply();

            // 원상 복구
            importer.isReadable = false;
            importer.SaveAndReimport();

            return copy;
        }

        /// <summary>
        /// RenderTexture를 통해 텍스처 픽셀을 읽는 폴백 방법.
        /// </summary>
        private static Texture2D ReadViaRenderTexture(Texture2D source)
        {
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rt);

            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            return readable;
        }

        /// <summary>
        /// 생성된 PNG의 TextureImporter 설정을 픽셀 아트용으로 구성한다.
        /// </summary>
        private static void ConfigureTextureImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;

            // 픽셀 퍼펙트를 위한 PPU 설정 (소스와 동일하게)
            importer.spritePixelsPerUnit = 16;

            importer.SaveAndReimport();
        }

        /// <summary>
        /// SO id에서 변종 키워드를 추출한다.
        /// 예: "boots_arcane" → "arcane", "necklace_ruby" → "ruby"
        /// </summary>
        private static string ExtractVariantKey(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "";

            // id 형식: "{slot}_{variant}" 또는 "{slot}_{variant}_{sub}"
            string[] parts = id.Split('_');
            if (parts.Length >= 2)
                return parts[parts.Length - 1].ToLowerInvariant();

            return "";
        }

        /// <summary>
        /// 출력 디렉토리를 재귀적으로 생성한다.
        /// </summary>
        private static void EnsureDirectoryExists(string assetPath)
        {
            string[] segments = assetPath.Split('/');
            string current = segments[0]; // "Assets"

            for (int i = 1; i < segments.Length; i++)
            {
                string next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }
    }
}
