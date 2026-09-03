using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

namespace MkLike.Editor
{
    /// <summary>
    /// 현재 씬의 하이라키를 텍스트로 출력하는 에디터 도구.
    /// 메뉴: MkLike > Debug > Dump Hierarchy
    /// 결과는 콘솔 + 클립보드에 복사된다.
    /// </summary>
    public static class HierarchyDumper
    {
        [MenuItem("mkLike/Debug/Dump Hierarchy (클립보드 복사)")]
        public static void DumpHierarchy()
        {
            var sb = new StringBuilder();
            var scene = SceneManager.GetActiveScene();
            sb.AppendLine($"=== Scene: {scene.name} ===");
            sb.AppendLine();

            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                DumpGameObject(root, sb, 0);
            }

            string result = sb.ToString();
            GUIUtility.systemCopyBuffer = result;
            Debug.Log($"[HierarchyDumper]\n{result}");
            Debug.Log("<color=cyan>[HierarchyDumper] 클립보드에 복사 완료! Ctrl+V로 붙여넣기 가능</color>");
        }

        private static void DumpGameObject(GameObject obj, StringBuilder sb, int depth)
        {
            string indent = new string(' ', depth * 2);
            string activeTag = obj.activeSelf ? "" : " [비활성]";
            string layerName = LayerMask.LayerToName(obj.layer);
            string layerTag = (obj.layer != 0) ? $" <{layerName}>" : "";

            // 위치 정보 (루트 또는 Transform이 의미있는 경우)
            string posInfo = "";
            if (depth <= 1 || obj.GetComponent<Collider2D>() != null || obj.GetComponent<Rigidbody2D>() != null)
            {
                var pos = obj.transform.position;
                var scale = obj.transform.localScale;
                posInfo = $" pos({pos.x:F1},{pos.y:F1})";
                if (scale != Vector3.one)
                    posInfo += $" scale({scale.x:F1},{scale.y:F1})";
            }

            // 컴포넌트 목록 (Transform 제외)
            var components = obj.GetComponents<Component>();
            var compList = new StringBuilder();
            foreach (var comp in components)
            {
                if (comp == null) continue;
                string typeName = comp.GetType().Name;
                if (typeName == "Transform" || typeName == "RectTransform") continue;
                if (compList.Length > 0) compList.Append(", ");
                compList.Append(typeName);
            }

            string compStr = compList.Length > 0 ? $" [{compList}]" : "";

            sb.AppendLine($"{indent}{obj.name}{activeTag}{layerTag}{posInfo}{compStr}");

            // 자식
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                DumpGameObject(obj.transform.GetChild(i).gameObject, sb, depth + 1);
            }
        }
    }
}
