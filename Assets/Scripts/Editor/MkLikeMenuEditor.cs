using UnityEditor;
using UnityEngine;

namespace MkLike.Editor
{
    /// <summary>
    /// MkLike 메뉴 통합 진입점.
    /// 모든 에디터 도구를 일관된 메뉴 구조로 제공한다.
    /// </summary>
    public static class MkLikeMenuEditor
    {
        // ── 최상위 (priority 0~9) ──

        [MenuItem("mkLike/Full Setup (원클릭 전체 셋업)", false, 0)]
        public static void FullSetup()
        {
            MasterSetupEditor.FullSetup();
        }

        [MenuItem("mkLike/Clear Scene", false, 1)]
        public static void ClearScene()
        {
            SceneClearEditor.ClearAll();
        }

        // ── VFX (priority 50~59) ──

        [MenuItem("mkLike/VFX/Extract VFX from Downloads", false, 50)]
        public static void ExtractVfxFromDownloads()
        {
            VfxPackImporter.ExtractVfxFromDownloads();
        }

        [MenuItem("mkLike/VFX/Import VFX Packs (SO 생성)", false, 51)]
        public static void ImportVfxPacks()
        {
            VfxPackImporter.ImportAllVfxPacks();
        }

        [MenuItem("mkLike/VFX/Generate Slash VFX SOs", false, 52)]
        public static void GenerateSlashVfx()
        {
            SlashVfxSetupEditor.GenerateSlashVfxSOs();
        }

        [MenuItem("mkLike/VFX/Refresh Existing VFX Frames", false, 53)]
        public static void RefreshVfxFrames()
        {
            VfxPackImporter.RefreshExistingFrames();
        }

        [MenuItem("mkLike/VFX/Auto-Map Skill VFX", false, 54)]
        public static void AutoMapSkillVfx()
        {
            VfxPackImporter.AutoMapSkillVfx();
        }

        // ── Audio (priority 100~109) ──

        [MenuItem("mkLike/Audio/Import All Audio", false, 100)]
        public static void ImportAudioAssets()
        {
            AudioAssetImporter.ImportAll();
        }

        [MenuItem("mkLike/Audio/Import SFX Only", false, 101)]
        public static void ImportSfxOnly()
        {
            int count = AudioAssetImporter.ImportSfx();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AudioAssetImporter] SFX: {count}개 복사됨");
        }

        [MenuItem("mkLike/Audio/Import BGM Only", false, 102)]
        public static void ImportBgmOnly()
        {
            int count = AudioAssetImporter.ImportBgm();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AudioAssetImporter] BGM: {count}개 복사됨");
        }

        // ── Content (priority 150~159) ──

        [MenuItem("mkLike/Content/Generate All Content Assets", false, 150)]
        public static void GenerateAllContent()
        {
            ContentAssetGenerator.GenerateAll();
        }

        [MenuItem("mkLike/Content/Generate Round 2 Only (비밀방+아레나+길드)", false, 151)]
        public static void GenerateRound2Content()
        {
            ContentAssetGenerator.GenerateRound2Content();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("mkLike/Content/Generate Guide Quests", false, 152)]
        public static void GenerateGuideQuests()
        {
            GuideQuestGenerator.GenerateAll();
            ReconnectQuestCatalog();
        }

        [MenuItem("mkLike/Content/Reconnect Quest Catalog", false, 153)]
        public static void ReconnectQuestCatalog()
        {
            var qm = Object.FindFirstObjectByType<Quest.QuestManager>();
            if (qm == null)
            {
                Debug.LogWarning("[MkLikeMenu] QuestManager를 찾을 수 없음");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:QuestDataSO", new[] { "Assets/Data/SO/Quest" });
            var quests = new Data.QuestDataSO[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                quests[i] = AssetDatabase.LoadAssetAtPath<Data.QuestDataSO>(path);
            }

            var so = new SerializedObject(qm);
            var prop = so.FindProperty("_questCatalog");
            prop.arraySize = quests.Length;
            for (int i = 0; i < quests.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = quests[i];
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(qm);

            Debug.Log($"[MkLikeMenu] QuestManager 카탈로그 재연결: {quests.Length}개 (순환 포함)");
        }

        [MenuItem("mkLike/Content/Recreate Weapon Gacha Pool (17레벨)", false, 154)]
        public static void RecreateWeaponGachaPool()
        {
            const string path = "Assets/Data/SO/GachaPool_Weapon.asset";
            if (AssetDatabase.LoadAssetAtPath<Data.GachaPoolSO>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
                Debug.Log("[MkLikeMenu] 기존 GachaPool_Weapon 삭제");
            }

            // Phase2Setup의 가챠 풀 생성 로직 호출
            Phase2SetupEditor.CreateGachaPools();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MkLikeMenu] GachaPool_Weapon 재생성 완료 (17 소환 레벨)");
        }

        // ── Atlas (priority 200~209) ──

        [MenuItem("mkLike/Atlas/Setup Sprite Atlases", false, 200)]
        public static void SetupAtlases()
        {
            SpriteAtlasSetupEditor.SetupAtlases();
        }

        // ── Sprite Slice (priority 250~259) ──

        [MenuItem("mkLike/Sprite/Setup 9-Slice Borders (Stone Kit)", false, 250)]
        public static void SetupSpriteSlice()
        {
            SpriteSliceSetupEditor.SetupAllBorders();
        }

        // ── Auto Map (priority 350~359) ──

        [MenuItem("mkLike/Auto Map/Skill Icons", false, 350)]
        public static void AutoMapSkillIcons()
        {
            SkillIconMapperEditor.AutoMapSkillIcons();
        }

        // ── Build (priority 400~409) ──

        [MenuItem("mkLike/Build/WebGL Build", false, 400)]
        public static void BuildWebGL()
        {
            WebGLBuildEditor.BuildWebGL();
        }

        [MenuItem("mkLike/Build/WebGL Build + Run", false, 401)]
        public static void BuildAndRun()
        {
            WebGLBuildEditor.BuildAndRun();
        }

        [MenuItem("mkLike/Build/Start Local Server (기존 빌드)", false, 402)]
        public static void StartLocalServer()
        {
            WebGLBuildEditor.StartLocalServer();
        }

        // ── Screenshot (priority 300~309) ──

        [MenuItem("mkLike/Screenshot/Run UI Audit", false, 300)]
        public static void RunUIAudit()
        {
            UIAuditSystem.RunUIAudit();
        }
    }
}
