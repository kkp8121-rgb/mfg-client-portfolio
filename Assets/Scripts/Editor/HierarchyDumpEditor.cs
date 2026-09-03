using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace MkLike.Editor
{
    /// <summary>
    /// 씬 하이라키 + 인스펙터 정보를 콘솔에 덤프.
    /// MkLike > Debug > Dump Hierarchy
    /// </summary>
    public static class HierarchyDumpEditor
    {
        [MenuItem("mkLike/Debug/Dump Hierarchy (Console)")]
        public static void DumpToConsole()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} ===\n");

            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in roots)
                DumpObject(root, 0, sb);

            Debug.Log($"[HierarchyDumpEditor]\n{sb}");
        }

        [MenuItem("mkLike/Debug/Dump Hierarchy (Clipboard)")]
        public static void DumpToClipboard()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== Scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} ===\n");

            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in roots)
                DumpObject(root, 0, sb);

            GUIUtility.systemCopyBuffer = sb.ToString();
            Debug.Log($"[HierarchyDump] 클립보드에 복사 완료! ({sb.Length} chars)");
        }

        private static void DumpObject(GameObject obj, int depth, System.Text.StringBuilder sb)
        {
            string indent = new string(' ', depth * 2);
            string active = obj.activeSelf ? "" : " [비활성]";

            // 기본 정보
            sb.Append($"{indent}{obj.name}{active}");

            // RectTransform 정보
            var rt = obj.GetComponent<RectTransform>();
            if (rt != null)
            {
                sb.Append($" rect(anc:{rt.anchorMin.x:F1},{rt.anchorMin.y:F1}~{rt.anchorMax.x:F1},{rt.anchorMax.y:F1}");
                sb.Append($" size:{rt.sizeDelta.x:F0}x{rt.sizeDelta.y:F0}");
                sb.Append($" pos:{rt.anchoredPosition.x:F0},{rt.anchoredPosition.y:F0})");
            }
            else
            {
                var tf = obj.transform;
                sb.Append($" pos({tf.localPosition.x:F1},{tf.localPosition.y:F1})");
            }

            // 컴포넌트 목록
            sb.Append(" [");
            var comps = obj.GetComponents<Component>();
            bool first = true;
            foreach (var c in comps)
            {
                if (c == null) continue;
                var type = c.GetType();
                // Transform/RectTransform, CanvasRenderer 생략
                if (type == typeof(Transform) || type == typeof(RectTransform) || type == typeof(CanvasRenderer))
                    continue;

                if (!first) sb.Append(", ");
                first = false;

                string typeName = type.Name;

                // Image 상세
                if (c is Image img)
                {
                    string spriteName = img.sprite != null ? img.sprite.name : "none";
                    sb.Append($"Image({spriteName}, {img.type}, color:{ColorStr(img.color)})");
                }
                // Slider 상세
                else if (c is Slider slider)
                {
                    string hasFill = slider.fillRect != null ? "fill:O" : "fill:X";
                    sb.Append($"Slider(val:{slider.value:F2}, {hasFill})");
                }
                // TMP 상세
                else if (c is TextMeshProUGUI tmp)
                {
                    string text = tmp.text.Length > 20 ? tmp.text.Substring(0, 20) + "..." : tmp.text;
                    string fontName = tmp.font != null ? tmp.font.name : "null";
                    sb.Append($"TMP(\"{text}\", size:{tmp.fontSize:F0}, font:{fontName}, color:{ColorStr(tmp.color)})");
                }
                // Button
                else if (c is Button)
                {
                    sb.Append("Button");
                }
                // LayoutGroup
                else if (c is HorizontalLayoutGroup hlg)
                {
                    sb.Append($"HLG(spacing:{hlg.spacing}, align:{hlg.childAlignment})");
                }
                else if (c is VerticalLayoutGroup vlg)
                {
                    sb.Append($"VLG(spacing:{vlg.spacing}, align:{vlg.childAlignment})");
                }
                // LayoutElement
                else if (c is LayoutElement le)
                {
                    sb.Append($"LE(prefH:{le.preferredHeight}, flexW:{le.flexibleWidth})");
                }
                // Shadow
                else if (c is Shadow shadow)
                {
                    sb.Append("Shadow");
                }
                // 기타
                else
                {
                    sb.Append(typeName);
                }
            }
            sb.AppendLine("]");

            // 자식 재귀
            for (int i = 0; i < obj.transform.childCount; i++)
                DumpObject(obj.transform.GetChild(i).gameObject, depth + 1, sb);
        }

        private static string ColorStr(Color c)
        {
            return $"#{ColorUtility.ToHtmlStringRGBA(c)}";
        }
    }
}
