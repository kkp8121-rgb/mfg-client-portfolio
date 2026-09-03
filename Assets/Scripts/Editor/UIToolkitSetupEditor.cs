using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MkLike.Editor
{
    /// <summary>
    /// UI Toolkit 씬 셋업. 모든 UI Toolkit 패널을 씬에 배치한다.
    /// 중복 방지: GameObject.Find 대신 씬 루트 전체 순회 (비활성 포함)로 동명 오브젝트 모두 제거 후 생성.
    /// </summary>
    public static class UIToolkitSetupEditor
    {
        private const string PANEL_SETTINGS_PATH = "Assets/UI Toolkit/GamePanelSettings.asset";
        private const string VIEWS = "Assets/UI Toolkit/Views/";

        // 패널 정의: (GameObject 이름, UXML 경로, C# 컨트롤러 타입, 기본 활성 여부)
        private static readonly (string name, string uxml, System.Type controller, bool active)[] PANELS =
        {
            // HUD — 항상 활성
            ("[UITK] HUD", VIEWS + "HUD.uxml", typeof(MkLike.UI.HudUI), true),
            // 탭 바 — 항상 활성
            ("[UITK] TabBar", VIEWS + "TabBar.uxml", typeof(MkLike.UI.TabBarUI), true),
            // 캐릭터 패널 탭들 — 기본 비활성 (탭 바에서 활성화)
            ("[UITK] Char_Stat", VIEWS + "CharacterStatTab.uxml", typeof(MkLike.UI.CharacterStatTabUI), false),
            ("[UITK] Char_Equip", VIEWS + "CharacterEquipTab.uxml", typeof(MkLike.UI.EquipmentTabUI), false),
            ("[UITK] Char_Skill", VIEWS + "CharacterSkillTab.uxml", typeof(MkLike.UI.SkillTabUI), false),
            ("[UITK] Char_Job", VIEWS + "CharacterJobTab.uxml", typeof(MkLike.UI.JobTabUI), false),
            // Char_Relic / Char_Climber / Char_HeroPower / Char_Ability: 2026-04-20 SHALLOW 대청소 삭제
            // 소환 탭 — 기본 비활성 (탭 바에서 활성화)
            // 2026-04-23 무기 탭 제거: 장비 탭이 무기 슬롯을 포함. WeaponTabUI.cs 삭제됨.
            ("[UITK] Summon", VIEWS + "SummonTab.uxml", typeof(MkLike.UI.SummonTabUI), false),
            // 던전 패널
            ("[UITK] Dungeon", VIEWS + "DungeonPanel.uxml", typeof(MkLike.UI.DungeonPanelUI), false),
            // 상점 패널
            ("[UITK] Shop", VIEWS + "ShopPanel.uxml", typeof(MkLike.UI.ShopPanelUI), false),
            // 추가 패널 — 기본 비활성 (탭 바 또는 이벤트로 활성화)
            ("[UITK] CollectionBook", VIEWS + "CollectionBookPanel.uxml", typeof(MkLike.UI.CollectionBookPanelUI), false),
            ("[UITK] BattlePass", VIEWS + "BattlePassPanel.uxml", typeof(MkLike.UI.BattlePassPanelUI), false),
            // 팝업들 — 기본 비활성 (이벤트로 활성화)
            ("[UITK] Popup_OfflineReward", VIEWS + "PopupOfflineReward.uxml", typeof(MkLike.UI.OfflineRewardPopupUI), false),
            ("[UITK] Popup_CurrencyShortage", VIEWS + "PopupCurrencyShortage.uxml", typeof(MkLike.UI.CurrencyShortagePopupUI), false),
            ("[UITK] Popup_GachaResult", VIEWS + "PopupGachaResult.uxml", typeof(MkLike.UI.GachaResultPopupUI), false),
            ("[UITK] Popup_Settings", VIEWS + "PopupSettings.uxml", typeof(MkLike.UI.SettingsPopupUI), false),
            ("[UITK] Popup_EquipCompare", VIEWS + "PopupEquipCompare.uxml", typeof(MkLike.UI.EquipComparePopupUI), false),
            // 정예 소환 팝업
            ("[UITK] Popup_EliteSummon", VIEWS + "PopupEliteSummon.uxml", typeof(MkLike.UI.EliteSummonPopupUI), false),
            // 코스튬 패널: 2026-04-20 Costume 시스템 완전 제거
            // 토스트
            ("[UITK] Toast", VIEWS + "Toast.uxml", typeof(MkLike.UI.ToastUI), true),
        };

        [MenuItem("mkLike/UI Toolkit/Setup All (전체 배치)", false, 500)]
        public static void SetupAll()
        {
            var panelSettings = GetOrCreatePanelSettings();
            int created = 0;

            foreach (var (name, uxmlPath, controller, active) in PANELS)
            {
                var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
                if (uxml == null)
                {
                    Debug.LogWarning($"[UIToolkit] UXML 없음, 스킵: {uxmlPath}");
                    continue;
                }

                // 동명 루트 전부 제거 (비활성 포함) — 중복 누적 방지
                RemoveAllRootsWithName(name);

                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, $"Create {name}");

                var doc = go.AddComponent<UIDocument>();
                doc.panelSettings = panelSettings;
                doc.visualTreeAsset = uxml;

                if (controller != null)
                    go.AddComponent(controller);

                go.SetActive(active);
                created++;
            }

            // ── UXML 없는 동적 UI (GachaSummonEffect 등) ──
            const string SUMMON_NAME = "[UITK] GachaSummonEffect";
            RemoveAllRootsWithName(SUMMON_NAME);

            var summonGo = new GameObject(SUMMON_NAME);
            Undo.RegisterCreatedObjectUndo(summonGo, $"Create {SUMMON_NAME}");
            var summonDoc = summonGo.AddComponent<UIDocument>();
            summonDoc.panelSettings = panelSettings;
            summonDoc.sortingOrder = 200;
            summonGo.AddComponent<MkLike.UI.GachaSummonEffect>();
            summonGo.SetActive(true);
            created++;

            // ── UGUI 팝업 (BasePopup 기반) ──
            SetupUGUIPopup<MkLike.UI.QuickHuntPopup>("QuickHuntPopup", ref created);
            SetupUGUIPopup<MkLike.UI.BoosterPopup>("BoosterPopup", ref created);

            Debug.Log($"[UIToolkit] 전체 셋업 완료: {created}개 UI 배치됨 (UITK + UGUI)");
        }

        /// <summary>
        /// 활성 씬에서 해당 이름의 모든 루트 GameObject를 제거한다 (비활성 포함).
        /// GameObject.Find는 활성 오브젝트만 찾아 중복 누적 원인이었음.
        /// </summary>
        private static int RemoveAllRootsWithName(string name)
        {
            int removed = 0;
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in roots)
            {
                if (root != null && root.name == name)
                {
                    Object.DestroyImmediate(root);
                    removed++;
                }
            }
            return removed;
        }

        /// <summary>
        /// UGUI(Canvas) 기반 BasePopup GO를 씬에 배치한다.
        /// </summary>
        private static void SetupUGUIPopup<T>(string goName, ref int count) where T : MonoBehaviour
        {
            // 동명 루트 전부 제거 (비활성 포함)
            RemoveAllRootsWithName(goName);
            // 컴포넌트 기반 검색으로 이름이 다르더라도 중복 제거
            var all = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var comp in all)
            {
                if (comp != null && comp.gameObject != null)
                    Object.DestroyImmediate(comp.gameObject);
            }

            var go = new GameObject(goName);
            Undo.RegisterCreatedObjectUndo(go, $"Create {goName}");

            // Canvas + CanvasGroup 추가 (BasePopup 필수)
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            go.AddComponent<UnityEngine.UI.CanvasScaler>();
            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            go.AddComponent<CanvasGroup>();
            go.AddComponent<T>();
            go.SetActive(false);
            count++;
        }

        [MenuItem("mkLike/UI Toolkit/Setup CharacterPanel (테스트)", false, 501)]
        public static void SetupCharacterPanel()
        {
            var panelSettings = GetOrCreatePanelSettings();
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(VIEWS + "CharacterStatTab.uxml");
            if (uxml == null) { Debug.LogError("[UIToolkit] CharacterStatTab.uxml 없음"); return; }

            const string GO_NAME = "[UITK] Char_Stat";
            var existing = GameObject.Find(GO_NAME);
            if (existing != null) Object.DestroyImmediate(existing);

            var go = new GameObject(GO_NAME);
            Undo.RegisterCreatedObjectUndo(go, "Create CharacterStatTab");
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panelSettings;
            doc.visualTreeAsset = uxml;
            go.AddComponent<MkLike.UI.CharacterStatTabUI>();

            Debug.Log("[UIToolkit] CharacterPanel 셋업 완료");
            Selection.activeGameObject = go;
        }

        [MenuItem("mkLike/UI Toolkit/Remove All (전체 제거)", false, 510)]
        public static void RemoveAll()
        {
            int removed = 0;
            foreach (var (name, _, _, _) in PANELS)
                removed += RemoveAllRootsWithName(name);

            removed += RemoveAllRootsWithName("[UITK] GachaSummonEffect");
            removed += RemoveAllRootsWithName("[UITK] Toast");
            removed += RemoveAllRootsWithName("[UIToolkit] CharacterPanel"); // legacy name
            removed += RemoveAllRootsWithName("QuickHuntPopup");
            removed += RemoveAllRootsWithName("BoosterPopup");

            Debug.Log($"[UIToolkit] {removed}개 오브젝트 제거됨 (비활성 포함)");
        }

        [MenuItem("mkLike/UI Toolkit/Cleanup Scene Residue (Monster/Vfx/DamageText 등)", false, 511)]
        public static void CleanupSceneResidue()
        {
            int removed = 0;
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in roots)
            {
                if (root == null) continue;
                string n = root.name;
                // 런타임 스폰 잔재 패턴
                bool isResidue =
                    n.StartsWith("Monster_") ||
                    n.StartsWith("Vfx_") ||
                    n == "DamageText_Prefab" ||
                    n.StartsWith("BattlePassLevelUpEffect(") ||
                    n.StartsWith("SkillUnlockEffect(") ||
                    n.EndsWith("(Clone)");
                if (isResidue)
                {
                    Object.DestroyImmediate(root);
                    removed++;
                }
            }
            Debug.Log($"[UIToolkit] 씬 런타임 잔재 {removed}개 제거");
        }

        [MenuItem("mkLike/UI Toolkit/Rebuild Scene (Remove + Cleanup + Setup)", false, 512)]
        public static void RebuildScene()
        {
            RemoveAll();
            CleanupSceneResidue();
            SetupAll();
            Debug.Log("[UIToolkit] 씬 재구축 완료 — RemoveAll + CleanupResidue + SetupAll 순차 실행");
        }

        private static PanelSettings GetOrCreatePanelSettings()
        {
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(PANEL_SETTINGS_PATH);
            if (ps != null) return ps;

            ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.referenceResolution = new Vector2Int(1080, 1920);
            ps.match = 0.5f;
            ps.sortingOrder = 200;
            AssetDatabase.CreateAsset(ps, PANEL_SETTINGS_PATH);
            AssetDatabase.SaveAssets();
            Debug.Log("[UIToolkit] PanelSettings 생성");
            return ps;
        }
    }
}
