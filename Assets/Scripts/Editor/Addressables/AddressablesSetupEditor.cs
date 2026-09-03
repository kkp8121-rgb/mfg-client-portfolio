#if UNITY_ADDRESSABLES
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.AddressableAssets.Build;
using UnityEngine;

namespace MkLike.Editor.Addressables
{
    /// <summary>
    /// Addressables 자동 세팅 커맨드 모음.
    /// "Setup All" 한 번으로 기본 설정 + 그룹 생성 + 폴더 규칙 분류 + 빌드까지 수행.
    /// </summary>
    public static class AddressablesSetupEditor
    {
        private const string MENU_ROOT = "mkLike/Addressables/";

        private const string GROUP_LOCAL_ESSENTIAL = "Local_Essential";
        private const string GROUP_LOCAL_CHARACTER = "Local_Character";
        private const string GROUP_REMOTE_AUDIO_BGM = "Remote_Audio_BGM";
        private const string GROUP_REMOTE_AUDIO_SFX = "Remote_Audio_SFX";
        private const string GROUP_REMOTE_DATA = "Remote_Data";
        private const string GROUP_REMOTE_COSTUMES = "Remote_Costumes";
        private const string GROUP_REMOTE_CHAPTERS = "Remote_Chapters";

        [MenuItem(MENU_ROOT + "Setup All", priority = 180)]
        public static void SetupAll()
        {
            Debug.Log("[Addressables] === Setup All 시작 ===");
            EnsureSettings();
            CreateAllGroups();
            ClassifyAssets();
            ScanResourcesLoadCalls();
            Debug.Log("[Addressables] === Setup All 완료 ===");
            EditorUtility.DisplayDialog("Addressables Setup",
                "기본 설정 + 그룹 생성 + 에셋 분류 + Resources.Load 스캔 완료.\n" +
                "다음: 'Build Bundles' 실행 시 번들 생성.",
                "확인");
        }

        [MenuItem(MENU_ROOT + "Ensure Settings", priority = 181)]
        public static void EnsureSettings()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                const string path = "Assets/AddressableAssetsData";
                if (!AssetDatabase.IsValidFolder(path))
                    AssetDatabase.CreateFolder("Assets", "AddressableAssetsData");
                settings = AddressableAssetSettings.Create(
                    AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                    AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName,
                    true, true);
                AddressableAssetSettingsDefaultObject.Settings = settings;
                Debug.Log("[Addressables] Settings 신규 생성");
            }
            else
            {
                Debug.Log("[Addressables] Settings 이미 존재");
            }
        }

        [MenuItem(MENU_ROOT + "Create All Groups", priority = 182)]
        public static void CreateAllGroups()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[Addressables] Settings 없음 — 'Ensure Settings' 먼저 실행");
                return;
            }

            EnsureGroup(settings, GROUP_LOCAL_ESSENTIAL, isLocal: true);
            EnsureGroup(settings, GROUP_LOCAL_CHARACTER, isLocal: true);
            EnsureGroup(settings, GROUP_REMOTE_AUDIO_BGM, isLocal: false);
            EnsureGroup(settings, GROUP_REMOTE_AUDIO_SFX, isLocal: false);
            EnsureGroup(settings, GROUP_REMOTE_DATA, isLocal: false);
            EnsureGroup(settings, GROUP_REMOTE_COSTUMES, isLocal: false);
            EnsureGroup(settings, GROUP_REMOTE_CHAPTERS, isLocal: false);

            AssetDatabase.SaveAssets();
            Debug.Log("[Addressables] 그룹 7종 준비 완료");
        }

        private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string name, bool isLocal)
        {
            var group = settings.FindGroup(name);
            if (group != null) return group;

            group = settings.CreateGroup(name, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));

            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema != null)
            {
                if (isLocal)
                {
                    schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                    schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
                }
                else
                {
                    schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
                    schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
                }
                schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
                schema.IncludeInBuild = true;
                schema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            }

            Debug.Log($"[Addressables] 그룹 생성: {name} ({(isLocal ? "Local" : "Remote")})");
            return group;
        }

        [MenuItem(MENU_ROOT + "Classify Assets (Auto)", priority = 183)]
        public static void ClassifyAssets()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[Addressables] Settings 없음");
                return;
            }

            int total = 0;
            // 오디오 (Remote — 용량 큰 편)
            total += AssignFolder(settings, "Assets/Resources/Audio/BGM", GROUP_REMOTE_AUDIO_BGM);
            total += AssignFolder(settings, "Assets/Resources/Audio/SFX", GROUP_REMOTE_AUDIO_SFX);

            // SPUM 프리팹 (Local — 게임 시작 필수)
            total += AssignFolder(settings, "Assets/Folder_Assets/SPUM/Prefab", GROUP_LOCAL_CHARACTER);

            // 챕터/스테이지 데이터 (Remote — 챕터 해금 시 로드)
            total += AssignFolder(settings, "Assets/Data/SO/Dungeon", GROUP_REMOTE_CHAPTERS);
            total += AssignFolder(settings, "Assets/ScriptableObjects/Monsters", GROUP_REMOTE_CHAPTERS);
            total += AssignFolder(settings, "Assets/ScriptableObjects/Stages", GROUP_REMOTE_CHAPTERS);

            // 일반 SO 데이터 (Remote — 상시 접근이지만 경량)
            total += AssignFolder(settings, "Assets/Data/SO/Skills", GROUP_REMOTE_DATA);
            total += AssignFolder(settings, "Assets/Data/SO/Equipment", GROUP_REMOTE_DATA);
            total += AssignFolder(settings, "Assets/Data/SO/Weapon", GROUP_REMOTE_DATA);
            // Assets/Data/SO/Relic: 2026-04-20 유물 시스템 완전 제거
            total += AssignFolder(settings, "Assets/Data/SO/Quest", GROUP_REMOTE_DATA);
            total += AssignFolder(settings, "Assets/Data/SO/HeroPower", GROUP_REMOTE_DATA);
            total += AssignFolder(settings, "Assets/Data/SO/Mastery", GROUP_REMOTE_DATA);
            total += AssignFolder(settings, "Assets/Data/SO/VFX", GROUP_REMOTE_DATA);
            total += AssignFolder(settings, "Assets/Data/SO/VFX_Imported", GROUP_REMOTE_DATA);
            total += AssignFolder(settings, "Assets/ScriptableObjects/Skills", GROUP_REMOTE_DATA);
            total += AssignFolder(settings, "Assets/ScriptableObjects/Equipment", GROUP_REMOTE_DATA);
            // Assets/ScriptableObjects/Relics: 2026-04-20 유물 시스템 완전 제거
            total += AssignFolder(settings, "Assets/ScriptableObjects/Boosters", GROUP_REMOTE_DATA);
            total += AssignFolder(settings, "Assets/ScriptableObjects/Companions", GROUP_REMOTE_DATA);

            // 캐릭터 데이터 (Local — 초기 선택에 필요)
            total += AssignFolder(settings, "Assets/ScriptableObjects/Characters", GROUP_LOCAL_ESSENTIAL);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Addressables] 자동 분류 완료: 총 {total}개 에셋 등록");
        }

        private static int AssignFolder(AddressableAssetSettings settings, string folderPath, string groupName)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.Log($"[Addressables] 폴더 스킵(없음): {folderPath}");
                return 0;
            }

            var group = settings.FindGroup(groupName);
            if (group == null)
            {
                Debug.LogWarning($"[Addressables] 그룹 없음: {groupName}");
                return 0;
            }

            var guids = AssetDatabase.FindAssets(string.Empty, new[] { folderPath });
            int count = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path)) continue;

                var entry = settings.CreateOrMoveEntry(guid, group, readOnly: false, postEvent: false);
                if (entry != null)
                {
                    entry.address = path;
                    count++;
                }
            }

            if (count > 0)
                Debug.Log($"[Addressables] {folderPath} → {groupName}: {count}개");
            return count;
        }

        [MenuItem(MENU_ROOT + "Scan Resources.Load Calls", priority = 184)]
        public static void ScanResourcesLoadCalls()
        {
            ResourcesLoadScanner.Run();
        }

        [MenuItem(MENU_ROOT + "Build Bundles", priority = 185)]
        public static void BuildBundles()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[Addressables] Settings 없음");
                return;
            }
            AddressableAssetSettings.BuildPlayerContent(out var result);
            if (!string.IsNullOrEmpty(result.Error))
                Debug.LogError($"[Addressables] 빌드 실패: {result.Error}");
            else
                Debug.Log($"[Addressables] 빌드 성공: {result.Duration:F1}s, 산출물 {result.OutputPath}");
        }

        [MenuItem(MENU_ROOT + "Clean Build Output", priority = 189)]
        public static void CleanBuildOutput()
        {
            AddressableAssetSettings.CleanPlayerContent();
            Debug.Log("[Addressables] 빌드 산출물 정리 완료");
        }
    }
}
#else
using UnityEditor;
using UnityEngine;

namespace MkLike.Editor.Addressables
{
    public static class AddressablesSetupEditor
    {
        [MenuItem("mkLike/Addressables/Install Package", priority = 181)]
        public static void InstallPackageHint()
        {
            EditorUtility.DisplayDialog("Addressables 미설치",
                "Packages/manifest.json에 com.unity.addressables 추가 후 재컴파일 필요.\n" +
                "또는 Window → Package Manager에서 Addressables 설치.",
                "확인");
        }
    }
}
#endif
