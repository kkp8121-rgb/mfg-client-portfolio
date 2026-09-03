using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace MkLike.Editor
{
    /// <summary>
    /// uGUI ↔ UI Toolkit 전환 토글.
    /// 테스트 시 uGUI를 끄고 UI Toolkit만 활성화하거나 복원한다.
    /// </summary>
    public static class UIToolkitToggleEditor
    {
        [MenuItem("mkLike/UI Toolkit/Switch to UI Toolkit (uGUI 끄기)", false, 520)]
        public static void SwitchToUIToolkit()
        {
            // 1. 기존 uGUI Canvas 전부 비활성화 (월드스페이스 제외)
            int disabledCount = 0;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                if (canvas.GetComponent<UIDocument>() != null) continue;
                if (canvas.renderMode == RenderMode.WorldSpace) continue;

                // 부모가 이미 Canvas인 하위 Canvas도 비활성화
                canvas.gameObject.SetActive(false);
                disabledCount++;
            }

            // AchievementToast 등 독립 오브젝트도 비활성화
            string[] forceDisable = { "AchievementToast", "SkillUnlockEffect", "BattlePassLevelUpEffect", "SceneTransitionManager" };
            foreach (var name in forceDisable)
            {
                var go = GameObject.Find(name);
                if (go != null) { go.SetActive(false); disabledCount++; }
            }

            // 2. UI Toolkit 오브젝트 활성화
            int enabledCount = 0;
            var docs = Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var doc in docs)
            {
                if (!doc.gameObject.activeSelf && doc.gameObject.name.StartsWith("[UITK]"))
                {
                    // HUD와 TabBar만 기본 활성화
                    if (doc.gameObject.name.Contains("HUD") || doc.gameObject.name.Contains("TabBar") || doc.gameObject.name.Contains("Toast"))
                    {
                        doc.gameObject.SetActive(true);
                        enabledCount++;
                    }
                }
            }

            Debug.Log($"[UIToolkit] uGUI {disabledCount}개 비활성화, UI Toolkit {enabledCount}개 활성화");
        }

        [MenuItem("mkLike/UI Toolkit/Switch to uGUI (UI Toolkit 끄기)", false, 521)]
        public static void SwitchToUGUI()
        {
            // 1. UI Toolkit 오브젝트 비활성화
            int disabledCount = 0;
            var docs = Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var doc in docs)
            {
                if (doc.gameObject.name.StartsWith("[UITK]") && doc.gameObject.activeSelf)
                {
                    doc.gameObject.SetActive(false);
                    disabledCount++;
                }
            }

            // 2. 기존 uGUI Canvas 복원
            int enabledCount = 0;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                if (canvas.GetComponent<UIDocument>() != null) continue;
                if (canvas.renderMode == RenderMode.WorldSpace) continue;

                if (!canvas.gameObject.activeSelf)
                {
                    canvas.gameObject.SetActive(true);
                    enabledCount++;
                }
            }

            Debug.Log($"[UIToolkit] UI Toolkit {disabledCount}개 비활성화, uGUI {enabledCount}개 복원");
        }
    }
}
