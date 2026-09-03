using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace MkLike.UI
{
    /// <summary>
    /// 런타임에서 uGUI를 비활성화하고 UI Toolkit만 사용하도록 전환한다.
    /// 씬에 배치된 [UITK] 오브젝트가 하나라도 있으면 자동으로 uGUI를 끈다.
    /// </summary>
    public class UIToolkitBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            // UI Toolkit 오브젝트가 있는지 확인
            var docs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool hasUIToolkit = false;
            foreach (var doc in docs)
            {
                if (doc.gameObject.name.StartsWith("[UITK]"))
                {
                    hasUIToolkit = true;
                    break;
                }
            }

            if (!hasUIToolkit) return;

            // uGUI Canvas 비활성화 (월드스페이스 제외)
            int disabled = 0;
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                if (canvas.GetComponent<UIDocument>() != null) continue;
                if (canvas.renderMode == RenderMode.WorldSpace) continue;
                if (canvas.gameObject.name == "JoystickCanvas") continue;

                canvas.gameObject.SetActive(false);
                disabled++;
            }

            // 독립 uGUI 오브젝트 비활성화
            string[] forceDisable = {
                "AchievementToast", "SkillUnlockEffect", "BattlePassLevelUpEffect",
                "SceneTransitionManager"
            };
            foreach (var name in forceDisable)
            {
                var go = GameObject.Find(name);
                if (go != null) go.SetActive(false);
            }

            // Canvas 자식 uGUI 요소 비활성화 (UIManager는 유지)
            var mainCanvas = GameObject.Find("Canvas");
            if (mainCanvas != null)
            {
                for (int i = 0; i < mainCanvas.transform.childCount; i++)
                {
                    var child = mainCanvas.transform.GetChild(i);
                    child.gameObject.SetActive(false);
                    disabled++;
                }
            }

            Debug.Log($"[UIToolkitBootstrap] uGUI {disabled}개 비활성화, UI Toolkit 모드 활성화");
        }
    }
}
