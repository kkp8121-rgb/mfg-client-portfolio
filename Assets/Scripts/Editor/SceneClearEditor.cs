using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

namespace MkLike.Editor
{
    /// <summary>
    /// 씬의 mkLike 관련 오브젝트를 자동으로 찾아 삭제하는 에디터 도구.
    /// Phase2SetupEditor가 생성하는 70+ 매니저를 빠짐없이 제거하기 위해
    /// 허용 목록(whitelist) 기반 catch-all 방식을 사용한다.
    /// </summary>
    public static class SceneClearEditor
    {
        // 절대 삭제하지 않을 루트 오브젝트 이름 목록 (Unity 기본 오브젝트)
        private static readonly string[] _preserveNames =
        {
            "Main Camera",
            "Directional Light",
            "Global Volume",
            "EventSystem",
        };

        public static void ClearAll()
        {
            int count = DestroyAllExceptPreserved();
            Debug.Log($"<color=yellow>[SceneClear] 씬 초기화 완료 — {count}개 오브젝트 삭제</color>");
        }

        public static void ClearPhase2()
        {
            int count = DestroyAllExceptPreserved();
            Debug.Log($"<color=yellow>[SceneClear] Phase 2 초기화 완료 — {count}개 오브젝트 삭제</color>");
        }

        /// <summary>
        /// 활성 씬의 모든 루트 오브젝트를 순회하며, 보존 목록에 없는 것을 전부 삭제한다.
        /// 이 방식은 Phase2SetupEditor가 새 매니저를 추가해도 별도 수정이 필요 없다.
        /// </summary>
        private static int DestroyAllExceptPreserved()
        {
            int count = 0;
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();

            foreach (var go in rootObjects)
            {
                if (go == null) continue;
                if (IsPreserved(go.name)) continue;

                string name = go.name;
                Undo.DestroyObjectImmediate(go);
                count++;
                Debug.Log($"[SceneClear] 삭제: {name}");
            }

            return count;
        }

        private static bool IsPreserved(string name)
        {
            for (int i = 0; i < _preserveNames.Length; i++)
            {
                if (string.Equals(name, _preserveNames[i], System.StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}
