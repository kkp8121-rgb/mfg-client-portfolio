using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem.UI;

namespace MkLike.Editor
{
    /// <summary>
    /// 원클릭 전체 셋업 (초기화 + 생성 통합).
    /// 메뉴: MkLike > Full Setup
    /// </summary>
    public static class MasterSetupEditor
    {
        public static void FullSetup()
        {
            // 1. 씬 초기화
            SceneClearEditor.ClearAll();

            // 2. 한글 폰트
            FontSetupEditor.SetupKoreanFont();

            // 3. 카메라
            EnsureMainCamera();

            // 4. 매니저 + UI
            SceneSetupEditor.CreateAll();

            // 5. EventSystem InputModule 보정
            FixEventSystemInputModule();

            // 6. Phase 2 전투 루프 (VFX SO 생성 포함)
            Phase2SetupEditor.SetupAll();

            // 8. 에셋 임포트 (VFX + 오디오)
            VfxPackImporter.ImportAllVfxPacks();
            VfxPackImporter.RefreshExistingFrames();
            VfxPackImporter.AutoMapSkillVfx();
            AudioAssetImporter.ImportAll();

            // 9. 콘텐츠 SO 생성 (코스튬/도전/스킬 + 라운드2)
            ContentAssetGenerator.GenerateAll();

            // 10. 가이드 퀘스트 50개 + 일일 퀘스트 11개 생성
            GuideQuestGenerator.GenerateAll();

            // 11. 스프라이트 아틀라스
            SpriteAtlasSetupEditor.SetupAtlases();

            // 12. Stone GUI Kit 9-slice Border 자동 설정
            SpriteSliceSetupEditor.SetupAllBorders();

            // 13. SO 에셋을 Resources/Data/로 복사 (DataManager가 로드할 수 있도록)
            CopySOsToResources();

            // 14. Stone GUI Kit 스프라이트를 Resources/로 복사
            UIThemeApplier.CopyStoneKitToResources();

            // 15. UI Toolkit 전체 배치 + uGUI 비활성화
            UIToolkitSetupEditor.SetupAll();
            UIToolkitToggleEditor.SwitchToUIToolkit();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=lime>[MasterSetup] === 전체 셋업 완료! (UI Toolkit 모드) Play 버튼을 누르세요. ===</color>");
        }

        private static void EnsureMainCamera()
        {
            if (Camera.main != null) return;

            var camObj = new GameObject("Main Camera");
            Undo.RegisterCreatedObjectUndo(camObj, "Create Main Camera");
            camObj.tag = "MainCamera";
            var cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 1f);
            camObj.AddComponent<AudioListener>();
            Debug.Log("[MasterSetup] Main Camera 생성");
        }

        private static void FixEventSystemInputModule()
        {
            var es = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (es == null) return;

            var standalone = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (standalone != null)
            {
                Object.DestroyImmediate(standalone);
                es.gameObject.AddComponent<InputSystemUIInputModule>();
                Debug.Log("[MasterSetup] StandaloneInputModule → InputSystemUIInputModule 교체");
            }

            if (es.GetComponent<InputSystemUIInputModule>() == null)
                es.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        /// <summary>
        /// Assets/Data/SO/ 의 SO 에셋을 Assets/Resources/Data/ 하위로 복사한다.
        /// DataManager가 Resources.LoadAll()로 로드할 수 있도록.
        /// </summary>
        private static void CopySOsToResources()
        {
            var mappings = new (string srcFolder, string dstFolder, string soType)[]
            {
                ("Assets/Data/SO", "Assets/Resources/Data/Characters", "CharacterDataSO"),
                ("Assets/Data/SO", "Assets/Resources/Data/Monsters", "MonsterDataSO"),
                ("Assets/Data/SO", "Assets/Resources/Data/Stages", "StageDataSO"),
                ("Assets/Data/SO/Skills", "Assets/Resources/Data/Skills", "SkillDataSO"),
                ("Assets/Data/SO/Equipment", "Assets/Resources/Data/Equipment", "EquipmentDataSO"),
                ("Assets/Data/SO/Weapon", "Assets/Resources/Data/Weapon", "WeaponDataSO"),
                ("Assets/Data/SO/Dungeon", "Assets/Resources/Data/Dungeon", "DungeonDataSO"),
                ("Assets/Data/SO/Quest", "Assets/Resources/Data/Quest", "QuestDataSO"),
                // Relic: 2026-04-20 유물 시스템 완전 제거
            };

            int totalCopied = 0;

            foreach (var (srcFolder, dstFolder, soType) in mappings)
            {
                if (!AssetDatabase.IsValidFolder(srcFolder)) continue;

                // 대상 폴더 생성
                EnsureResourcesFolder(dstFolder);

                // 소스 폴더의 모든 .asset 파일 검색
                var guids = AssetDatabase.FindAssets($"t:{soType}", new[] { srcFolder });
                foreach (var guid in guids)
                {
                    string srcPath = AssetDatabase.GUIDToAssetPath(guid);
                    string fileName = System.IO.Path.GetFileName(srcPath);
                    string dstPath = $"{dstFolder}/{fileName}";

                    if (!AssetDatabase.LoadAssetAtPath<Object>(dstPath))
                    {
                        AssetDatabase.CopyAsset(srcPath, dstPath);
                        totalCopied++;
                    }
                }
            }

            if (totalCopied > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=yellow>[MasterSetup] SO 에셋 {totalCopied}개를 Resources/Data/로 복사 완료</color>");
            }
            else
            {
                Debug.Log("[MasterSetup] SO 에셋 복사: 이미 최신 상태");
            }
        }

        private static void EnsureResourcesFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parts = path.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
