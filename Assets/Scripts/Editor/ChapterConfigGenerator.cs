using UnityEditor;
using UnityEngine;
using MkLike.Data;

namespace MkLike.Editor
{
    /// <summary>
    /// 8개 챕터 ChapterMonsterConfigSO를 자동 생성하는 에디터.
    /// </summary>
    public static class ChapterConfigGenerator
    {
        private const string OUTPUT_DIR = "Assets/Resources/Data/Chapters";

        // 챕터 정의: (번호, 테마명, 몬스터폴더, 보스폴더, 몬스터틴트, 보스틴트, 배경하단, 배경상단, 원거리비율)
        private static readonly ChapterDef[] CHAPTERS =
        {
            new(1, "숲 — 초보 모험",
                "Addons/Legacy/2_Prefab", "",
                Color.white, new Color(1f, 0.5f, 0.5f),
                new Color(0.08f, 0.18f, 0.08f), new Color(0.15f, 0.30f, 0.12f), 0.1f),

            new(2, "오크 소굴",
                "Addons/MS_Orc/2_Prefab", "",
                new Color(0.8f, 1f, 0.7f), new Color(1f, 0.4f, 0.3f),
                new Color(0.12f, 0.08f, 0.05f), new Color(0.22f, 0.15f, 0.08f), 0.15f),

            new(3, "언데드 성",
                "Addons/Undead/2_Prefab", "",
                new Color(0.7f, 0.8f, 1f), new Color(0.6f, 1f, 0.6f),
                new Color(0.25f, 0.05f, 0.02f), new Color(0.40f, 0.15f, 0.05f), 0.3f),

            new(4, "엘프 숲",
                "Addons/Elf/2_Prefab", "",
                new Color(0.9f, 1f, 0.85f), new Color(1f, 0.7f, 1f),
                new Color(0.10f, 0.18f, 0.30f), new Color(0.20f, 0.35f, 0.55f), 0.4f),

            new(5, "조선 왕국",
                "Addons/ChosunSet/2_Prefab", "",
                Color.white, new Color(1f, 0.85f, 0.4f),
                new Color(0.15f, 0.05f, 0.22f), new Color(0.25f, 0.10f, 0.35f), 0.25f),

            new(6, "팔라딘 성전",
                "Addons/PaladinSet/2_Prefab", "",
                new Color(1f, 1f, 0.8f), new Color(1f, 0.9f, 0.3f),
                new Color(0.18f, 0.15f, 0.05f), new Color(0.35f, 0.30f, 0.10f), 0.2f),

            new(7, "레트로 월드",
                "Addons/RetroHeroes/2_Prefab", "",
                Color.white, new Color(0.4f, 1f, 1f),
                new Color(0.08f, 0.12f, 0.18f), new Color(0.15f, 0.25f, 0.35f), 0.35f),

            new(8, "모던 차원",
                "Addons/ModernPackVer1/2_Prefab", "",
                Color.white, new Color(1f, 0.5f, 1f),
                new Color(0.12f, 0.05f, 0.15f), new Color(0.22f, 0.12f, 0.28f), 0.3f),
        };

        [MenuItem("mkLike/Assets/Generate Chapter Configs", false, 610)]
        public static void Generate()
        {
            if (!AssetDatabase.IsValidFolder(OUTPUT_DIR))
            {
                AssetDatabase.CreateFolder("Assets/Resources/Data", "Chapters");
            }

            int count = 0;
            foreach (var def in CHAPTERS)
            {
                string path = $"{OUTPUT_DIR}/Chapter_{def.number}.asset";

                // 기존 SO가 있으면 업데이트, 없으면 생성
                var so = AssetDatabase.LoadAssetAtPath<ChapterMonsterConfigSO>(path);
                if (so == null)
                {
                    so = ScriptableObject.CreateInstance<ChapterMonsterConfigSO>();
                    AssetDatabase.CreateAsset(so, path);
                }

                var serialized = new SerializedObject(so);
                serialized.FindProperty("_chapterNumber").intValue = def.number;
                serialized.FindProperty("_themeName").stringValue = def.theme;
                serialized.FindProperty("_monsterPrefabFolder").stringValue = def.monsterFolder;
                serialized.FindProperty("_bossPrefabFolder").stringValue = def.bossFolder;
                serialized.FindProperty("_monsterTint").colorValue = def.monsterTint;
                serialized.FindProperty("_monsterScale").floatValue = 1f;
                serialized.FindProperty("_bossTint").colorValue = def.bossTint;
                serialized.FindProperty("_bossScale").floatValue = 1.5f;
                serialized.FindProperty("_miniBossTint").colorValue = Color.Lerp(def.monsterTint, def.bossTint, 0.5f);
                serialized.FindProperty("_miniBossScale").floatValue = 1.3f;
                serialized.FindProperty("_bgBottom").colorValue = def.bgBottom;
                serialized.FindProperty("_bgTop").colorValue = def.bgTop;
                serialized.FindProperty("_rangedRatio").floatValue = def.rangedRatio;
                serialized.ApplyModifiedProperties();

                EditorUtility.SetDirty(so);
                count++;
                Debug.Log($"[ChapterConfig] Chapter {def.number}: {def.theme} — {def.monsterFolder}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ChapterConfig] {count}개 챕터 설정 생성 완료");
        }

        private readonly struct ChapterDef
        {
            public readonly int number;
            public readonly string theme;
            public readonly string monsterFolder;
            public readonly string bossFolder;
            public readonly Color monsterTint;
            public readonly Color bossTint;
            public readonly Color bgBottom;
            public readonly Color bgTop;
            public readonly float rangedRatio;

            public ChapterDef(int num, string thm, string mf, string bf,
                Color mt, Color bt, Color bb, Color btop, float rr)
            {
                number = num; theme = thm;
                monsterFolder = mf; bossFolder = bf;
                monsterTint = mt; bossTint = bt;
                bgBottom = bb; bgTop = btop;
                rangedRatio = rr;
            }
        }
    }
}
