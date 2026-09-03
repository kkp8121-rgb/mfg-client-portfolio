using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace MkLike.Editor
{
    // ════════════════════════════════════════════════════════
    //  SceneView 툴바 오버레이 — 원클릭 버튼
    // ════════════════════════════════════════════════════════

    [Overlay(typeof(SceneView), "mkLike", defaultDisplay = true)]
    public class MkLikeToolbarOverlay : ToolbarOverlay
    {
        MkLikeToolbarOverlay() : base(
            GuideSkipButton.Id,
            GuideAutoRunButton.Id,
            GuideStatusButton.Id,
            SpawnBossButton.Id
        )
        { }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    sealed class GuideSkipButton : EditorToolbarButton
    {
        public const string Id = "mklike-guide-skip";

        public GuideSkipButton()
        {
            text = "Guide ▶";
            tooltip = "현재 가이드 퀘스트 강제 완료 + 수령";
            clicked += () => PlayTestHelper.ForceCompleteAndClaim();
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    sealed class GuideAutoRunButton : EditorToolbarButton
    {
        public const string Id = "mklike-guide-autorun";

        public GuideAutoRunButton()
        {
            text = "Guide ▶▶";
            tooltip = "가이드 퀘스트 전체 자동 진행";
            clicked += () => PlayTestHelper.AutoRunAll();
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    sealed class GuideStatusButton : EditorToolbarButton
    {
        public const string Id = "mklike-guide-status";

        public GuideStatusButton()
        {
            text = "?";
            tooltip = "현재 가이드 퀘스트 상태";
            clicked += () => PlayTestHelper.GuideStatus();
        }
    }

    [EditorToolbarElement(Id, typeof(SceneView))]
    sealed class SpawnBossButton : EditorToolbarButton
    {
        public const string Id = "mklike-spawn-boss";

        public SpawnBossButton()
        {
            text = "Boss";
            tooltip = "챕터 보스 스폰 (Floor 10)";
            clicked += () => PlayTestHelper.SpawnBoss();
        }
    }

    // ════════════════════════════════════════════════════════
    //  퀵 패널 — 도킹 가능한 EditorWindow
    // ════════════════════════════════════════════════════════

    public class MkLikeQuickPanel : EditorWindow
    {
        private Vector2 _scroll;

        [MenuItem("mkLike/Quick Panel %#q", false, -100)]
        public static void Open()
        {
            var win = GetWindow<MkLikeQuickPanel>("mkLike");
            win.minSize = new Vector2(200, 300);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // ── 가이드 퀘스트 ──
            EditorGUILayout.LabelField("Guide Quest", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Status (현재 상태)"))
                    PlayTestHelper.GuideStatus();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Force Complete"))
                    PlayTestHelper.ForceCompleteCurrent();
                if (GUILayout.Button("Claim"))
                    PlayTestHelper.ClaimGuideReward();
                EditorGUILayout.EndHorizontal();

                GUI.backgroundColor = new Color(0.3f, 0.9f, 0.4f);
                if (GUILayout.Button("Complete + Claim (원클릭)", GUILayout.Height(30)))
                    PlayTestHelper.ForceCompleteAndClaim();
                GUI.backgroundColor = Color.white;

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Auto Run", EditorStyles.miniLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("#5")) PlayTestHelper.AutoRunToIndex(5);
                if (GUILayout.Button("#10")) PlayTestHelper.AutoRunToIndex(10);
                if (GUILayout.Button("#20")) PlayTestHelper.AutoRunToIndex(20);
                if (GUILayout.Button("ALL")) PlayTestHelper.AutoRunAll();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(8);

            // ── 탭 조작 ──
            EditorGUILayout.LabelField("Tab", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("소환")) PlayTestHelper.OpenTab0();
                if (GUILayout.Button("캐릭터")) PlayTestHelper.OpenTab1();
                if (GUILayout.Button("장비")) PlayTestHelper.OpenTab2();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("스킬")) PlayTestHelper.OpenTab3();
                if (GUILayout.Button("무기")) PlayTestHelper.OpenTab4();
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Close All"))
                    PlayTestHelper.CloseAllTabs();
            }

            EditorGUILayout.Space(8);

            // ── 기타 ──
            EditorGUILayout.LabelField("Misc", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Spawn Boss"))
                    PlayTestHelper.SpawnBoss();

                EditorGUILayout.Space(4);
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("Reset Save"))
                {
                    if (EditorUtility.DisplayDialog("Reset Save",
                        "세이브를 초기화하시겠습니까?\n다시 Play해야 적용됩니다.", "초기화", "취소"))
                    {
                        PlayTestHelper.ResetSave();
                    }
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
