using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MkLike.Editor
{
    /// <summary>
    /// SPUM 에셋의 8개 Addon 폴더 안에 있는 **완성 캐릭터 프리팹**을 한 씬에 그리드로 배치해서
    /// 어떤 프리팹이 어떤 외형인지 시각적으로 확인할 수 있도록 하는 툴.
    /// 메뉴 "MkLike/Tools/SPUM Browser" 실행 → Assets/Scenes/SpumBrowser.unity 생성/갱신.
    /// 각 프리팹 아래 파일명 라벨, 폴더별 섹션 헤더.
    /// </summary>
    public static class SpumPrefabBrowserEditor
    {
        private const string ScenePath = "Assets/Scenes/SpumBrowser.unity";
        private const string RootName = "SPUM_Browser_Root";

        private static readonly string[] Folders =
        {
            "PaladinSet",
            "RetroHeroes",
            "Legacy",
            "Elf",
            "ChosunSet",
            "ModernPackVer1",
            "MS_Orc",
            "Undead",
        };

        private const int Cols = 10;
        private const float CellX = 1.6f;
        private const float CellY = 2.2f;
        private const float SectionSpacing = 2.5f;

        [MenuItem("MkLike/Tools/SPUM Browser — 전체 Addon 그리드 생성")]
        public static void BuildBrowserScene()
        {
            // 재진입 방어 — 동일 프레임 내 중복 호출 무시 (execute_menu_item 재시도 방지)
            if (_isBuilding)
            {
                Debug.LogWarning("[SpumBrowser] 이미 생성 중 — 중복 호출 무시");
                return;
            }
            _isBuilding = true;
            try
            {
                BuildInternal();
            }
            finally
            {
                _isBuilding = false;
            }
        }

        private static bool _isBuilding;

        private static void BuildInternal()
        {
            // 이미 현재 씬이 SpumBrowser면 재빌드 대신 열린 채로 둠
            var active = SceneManager.GetActiveScene();
            bool alreadyOpen = active.path == ScenePath;

            var scene = alreadyOpen
                ? active
                : EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            if (!alreadyOpen)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            else
            {
                // 기존 내용 전부 제거 후 새로 배치
                var existingRoot = GameObject.Find(RootName);
                if (existingRoot != null) Object.DestroyImmediate(existingRoot);
            }

            // 카메라 설정
            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 28f;
                cam.transform.position = new Vector3(0f, -30f, -10f);
                cam.backgroundColor = new Color(0.14f, 0.14f, 0.18f);
                cam.clearFlags = CameraClearFlags.SolidColor;
            }

            // 루트
            var root = new GameObject(RootName);

            float yCursor = 0f;
            int totalPrefabs = 0;

            foreach (var folder in Folders)
            {
                string folderPath = $"Assets/SPUM/Resources/Addons/{folder}/2_Prefab";
                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    Debug.LogWarning($"[SpumBrowser] 폴더 없음: {folderPath}");
                    continue;
                }

                var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
                var paths = guids
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => p.EndsWith(".prefab"))
                    .OrderBy(p => p)
                    .ToArray();
                if (paths.Length == 0) continue;

                // 섹션 헤더
                CreateLabel(root.transform,
                    $"▼ {folder} ({paths.Length}개)",
                    new Vector3(-(Cols - 1) * CellX * 0.5f, yCursor, 0f),
                    fontSize: 0.8f,
                    color: new Color(1f, 0.85f, 0.3f),
                    align: TextAlignmentOptions.Left);
                yCursor -= 1.2f;

                for (int i = 0; i < paths.Length; ++i)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                    if (prefab == null) continue;

                    int col = i % Cols;
                    int row = i / Cols;
                    var pos = new Vector3(
                        col * CellX - (Cols - 1) * CellX * 0.5f,
                        yCursor - row * CellY,
                        0f);

                    // SPUM 프리팹은 Awake에서 position을 리셋하므로 Wrapper로 감싸 위치 고정
                    var wrapper = new GameObject($"Cell_{folder}_{i:D3}");
                    wrapper.transform.SetParent(root.transform, false);
                    wrapper.transform.localPosition = pos;
                    wrapper.transform.localScale = Vector3.one * 0.75f;

                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, wrapper.transform);
                    inst.transform.localPosition = Vector3.zero;
                    inst.transform.localScale = Vector3.one;
                    EnsureUrpMaterials(inst);

                    // 이름 라벨 (SPUM_ 접두 제거 + 뒤 6자리만)
                    string raw = Path.GetFileNameWithoutExtension(paths[i]).Replace("SPUM_", "");
                    string shortName = raw.Length > 6 ? raw.Substring(raw.Length - 6) : raw;
                    CreateLabel(root.transform,
                        $"{folder[..Mathf.Min(3, folder.Length)]}-{i:D2}\n{shortName}",
                        pos + new Vector3(0f, -0.9f, 0f),
                        fontSize: 0.22f,
                        color: Color.white,
                        align: TextAlignmentOptions.Center);
                }

                int rows = (paths.Length + Cols - 1) / Cols;
                yCursor -= rows * CellY + SectionSpacing;
                totalPrefabs += paths.Length;
            }

            // 카메라 세로 범위에 맞게 조정
            if (cam != null)
            {
                float totalHeight = Mathf.Abs(yCursor);
                cam.orthographicSize = Mathf.Max(14f, totalHeight * 0.5f + 2f);
                cam.transform.position = new Vector3(0f, yCursor * 0.5f, -10f);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            // 모달 다이얼로그는 MCP execute_menu_item이 timeout 재시도를 유발하므로 사용하지 않음.
            Debug.Log($"[SpumBrowser] 완료 — {totalPrefabs}개 프리팹 배치 (씬: {ScenePath})");
        }

        private static TextMeshPro CreateLabel(Transform parent, string text, Vector3 pos,
            float fontSize, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject($"Label_{text[..Mathf.Min(8, text.Length)]}");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = fontSize * 20f; // TextMeshPro fontSize 스케일
            tmp.transform.localScale = Vector3.one * 0.05f;
            tmp.alignment = align;
            tmp.color = color;
            tmp.sortingOrder = 100;
            return tmp;
        }

        private static Material _urpMaterial;
        private static void EnsureUrpMaterials(GameObject root)
        {
            if (_urpMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                _urpMaterial = new Material(shader);
            }
            var rs = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < rs.Length; ++i)
            {
                if (rs[i] != null) rs[i].sharedMaterial = _urpMaterial;
            }
        }
    }
}
