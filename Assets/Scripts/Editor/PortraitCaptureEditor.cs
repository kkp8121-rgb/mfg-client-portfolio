using System.IO;
using UnityEditor;
using UnityEngine;
using MkLike.Core;
using MkLike.Data;

namespace MkLike.Editor
{
    /// <summary>
    /// SPUM 프리팹을 임시 카메라로 렌더링하여 초상화 PNG를 생성하는 에디터 도구.
    /// CharacterDesignData(JSON)로 장비 파츠를 적용한 후 캡처한다.
    /// 메뉴: MkLike/Auto Map/Capture Portraits
    /// </summary>
    public static class PortraitCaptureEditor
    {
        private const int PORTRAIT_SIZE = 256;
        private const string OUTPUT_DIR = "Assets/Resources/Icons/Portraits";

        // 2026-04-20 SPUM 단일 원천 통일 — JobOutfitDatabaseSO를 직업 프리팹의 유일한 원천으로 사용.
        // 기존 SwordMan/BowMan/MagicianMan SPUM 샘플 하드코딩은 런타임 Player와 불일치를 일으켰음.
        private static readonly (string jobKey, JobType job, string designId)[] JOB_ENTRIES =
        {
            ("warrior", JobType.Warrior, "warrior"),
            ("archer",  JobType.Archer,  "archer"),
            ("mage",    JobType.Mage,    "mage"),
        };

        [MenuItem("mkLike/Auto Map/Capture Portraits")]
        public static void CaptureAll()
        {
            if (!Directory.Exists(OUTPUT_DIR))
                Directory.CreateDirectory(OUTPUT_DIR);

            var outfitDb = JobOutfitDatabaseSO.Load();
            if (outfitDb == null)
            {
                Debug.LogError("[PortraitCapture] JobOutfitDatabaseSO 로드 실패 — 캡처 중단");
                return;
            }

            int captured = 0;
            foreach (var (jobKey, job, designId) in JOB_ENTRIES)
            {
                // Tier 0 기본 프리팹 — 런타임 Player 최초 상태와 동일 원천
                var prefab = outfitDb.GetPrefab(job, 0);
                if (prefab == null)
                {
                    Debug.LogWarning($"[PortraitCapture] JobOutfitDatabase에 {job} Tier 0 프리팹 없음 — 스킵");
                    continue;
                }

                var png = CapturePortrait(prefab, designId);
                if (png == null) continue;

                string filePath = $"{OUTPUT_DIR}/portrait_{jobKey}.png";
                File.WriteAllBytes(filePath, png);
                Debug.Log($"[PortraitCapture] 저장: {filePath} (source={prefab.name})");
                captured++;
            }

            AssetDatabase.Refresh();

            // 저장된 텍스처를 Sprite로 임포트 설정
            foreach (var (jobKey, _, _) in JOB_ENTRIES)
            {
                string filePath = $"{OUTPUT_DIR}/portrait_{jobKey}.png";
                ConfigureSpriteImport(filePath);
            }

            Debug.Log($"[PortraitCapture] 완료: {captured}개 초상화 생성");
        }

        private static byte[] CapturePortrait(GameObject prefab, string designId)
        {
            // 임시 씬 오브젝트 생성
            var instance = Object.Instantiate(prefab);
            instance.transform.position = new Vector3(100f, 100f, 0f); // 메인 씬과 겹치지 않는 위치

            // 2026-04-20 SPUM 단일 원천 통일 — 런타임 Player와 동일 비주얼 보장을 위해
            // CharacterDesignData JSON 덮어쓰기 비활성화. 프리팹 원본 그대로 캡처.
            // (필요 시 JobOutfitDatabase 프리팹에 장비 파츠를 미리 베이킹할 것.)

            // SPUM 캐릭터의 바운드 계산
            var bounds = CalculateBounds(instance);

            // 카메라 설정
            var camGo = new GameObject("_PortraitCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // 투명 배경
            cam.cullingMask = ~0; // 모든 레이어

            // 카메라 위치: 캐릭터 중심에서 약간 위를 바라봄 (상반신 중심)
            float centerY = bounds.center.y + bounds.extents.y * 0.1f;
            cam.transform.position = new Vector3(bounds.center.x, centerY, -10f);
            cam.orthographicSize = bounds.extents.y * 1.2f; // 약간 여유

            // 렌더 텍스처
            var rt = new RenderTexture(PORTRAIT_SIZE, PORTRAIT_SIZE, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            // Texture2D로 읽기
            RenderTexture.active = rt;
            var tex = new Texture2D(PORTRAIT_SIZE, PORTRAIT_SIZE, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, PORTRAIT_SIZE, PORTRAIT_SIZE), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            byte[] png = tex.EncodeToPNG();

            // 정리
            Object.DestroyImmediate(tex);
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(instance);

            return png;
        }

        /// <summary>
        /// CharacterDesignData JSON에서 장비 파츠를 읽어 SPUM 인스턴스에 적용한다.
        /// </summary>
        private static void ApplyEquipmentDesign(GameObject instance, string designId)
        {
            if (string.IsNullOrEmpty(designId)) return;

            var design = CharacterDesignSetupEditor.LoadDesignFromJson(designId);
            if (design == null)
            {
                Debug.LogWarning($"[PortraitCapture] 디자인 데이터 없음: {designId}");
                return;
            }

            CharacterDesignSetupEditor.ApplyDesign(instance, design);
            Debug.Log($"[PortraitCapture] 장비 파츠 적용: {designId} ({design.displayName})");
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.one);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }

        private static void ConfigureSpriteImport(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = PORTRAIT_SIZE;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
    }
}
