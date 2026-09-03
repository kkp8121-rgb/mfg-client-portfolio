using System.IO;
using UnityEditor;
using UnityEngine;
using MkLike.Data;

namespace MkLike.Editor
{
    /// <summary>
    /// 모든 MonsterDataSO를 순회하여 SPUM 프리팹 기반 초상화 PNG를 자동 생성하는 에디터 도구.
    /// 메뉴: MkLike/Auto Map/Capture Monster Portraits
    /// </summary>
    public static class MonsterPortraitEditor
    {
        private const int PORTRAIT_SIZE = 256;
        private const string OUTPUT_DIR = "Assets/Resources/Icons/Monsters";

        [MenuItem("mkLike/Auto Map/Capture Monster Portraits")]
        public static void CaptureAll()
        {
            // MonsterDataSO 전체 검색
            string[] guids = AssetDatabase.FindAssets("t:MonsterDataSO");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[MonsterPortrait] MonsterDataSO 에셋을 찾을 수 없습니다.");
                return;
            }

            if (!Directory.Exists(OUTPUT_DIR))
                Directory.CreateDirectory(OUTPUT_DIR);

            int captured = 0;

            foreach (string guid in guids)
            {
                string soPath = AssetDatabase.GUIDToAssetPath(guid);
                var monsterData = AssetDatabase.LoadAssetAtPath<MonsterDataSO>(soPath);
                if (monsterData == null) continue;

                // 2026-04-20 SPUM 단일 원천 통일 — data.prefab (GUID) 우선.
                // spumPrefabPath는 legacy fallback만 제공.
                GameObject prefab = monsterData.prefab;
                if (prefab == null && !string.IsNullOrEmpty(monsterData.spumPrefabPath))
                {
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(monsterData.spumPrefabPath);
                }
                if (prefab == null)
                {
                    Debug.LogWarning($"[MonsterPortrait] {monsterData.name}: prefab 필드 비어있음, 스킵");
                    continue;
                }

                // 초상화 캡처
                byte[] png = CapturePortrait(prefab, monsterData.visualTint, monsterData.visualScale);
                if (png == null) continue;

                // 파일명: monster_{id}.png (id가 비어있으면 SO 이름 사용)
                string safeName = !string.IsNullOrEmpty(monsterData.id) ? monsterData.id : monsterData.name;
                string filePath = $"{OUTPUT_DIR}/monster_{safeName}.png";

                File.WriteAllBytes(filePath, png);
                Debug.Log($"[MonsterPortrait] 저장: {filePath}");
                captured++;
            }

            AssetDatabase.Refresh();

            // Sprite 임포트 설정 + SO에 icon 자동 할당
            foreach (string guid in guids)
            {
                string soPath = AssetDatabase.GUIDToAssetPath(guid);
                var monsterData = AssetDatabase.LoadAssetAtPath<MonsterDataSO>(soPath);
                if (monsterData == null) continue;

                string safeName = !string.IsNullOrEmpty(monsterData.id) ? monsterData.id : monsterData.name;
                string filePath = $"{OUTPUT_DIR}/monster_{safeName}.png";

                if (!File.Exists(filePath)) continue;

                ConfigureSpriteImport(filePath);

                // SO의 icon 필드에 자동 할당
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(filePath);
                if (sprite != null && monsterData.icon != sprite)
                {
                    monsterData.icon = sprite;
                    EditorUtility.SetDirty(monsterData);
                    Debug.Log($"[MonsterPortrait] {monsterData.name}.icon 할당 완료");
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[MonsterPortrait] 완료: {captured}개 몬스터 초상화 생성");
        }

        private static byte[] CapturePortrait(GameObject prefab, Color tint, float scale)
        {
            // 임시 인스턴스 생성 (메인 씬과 겹치지 않는 위치)
            var instance = Object.Instantiate(prefab);
            instance.transform.position = new Vector3(200f, 200f, 0f);

            // 비주얼 스케일 적용
            if (scale > 0f && !Mathf.Approximately(scale, 1f))
                instance.transform.localScale = Vector3.one * scale;

            // 틴트 적용
            if (tint != Color.white)
            {
                var renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                        renderers[i].color = tint;
                }
            }

            // 바운드 계산
            var bounds = CalculateBounds(instance);

            // 카메라 설정
            var camGo = new GameObject("_MonsterPortraitCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // 투명 배경
            cam.cullingMask = ~0;

            // 카메라를 캐릭터 중심에 배치
            float centerY = bounds.center.y + bounds.extents.y * 0.1f;
            cam.transform.position = new Vector3(bounds.center.x, centerY, -10f);
            cam.orthographicSize = bounds.extents.y * 1.2f;

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
