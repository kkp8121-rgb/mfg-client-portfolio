using UnityEditor;
using UnityEngine;

namespace MkLike.Editor
{
    /// <summary>
    /// UI 감사: 메뉴 클릭 → 씬에 Runner 오브젝트 배치 → Play 진입 → Runner가 자동 실행.
    /// </summary>
    public static class UIAuditSystem
    {
        private const string RUNNER_NAME = "[UIAuditRunner]";

        public static void RunUIAudit()
        {
            // 이미 Play 중이면 직접 생성
            if (EditorApplication.isPlaying)
            {
                if (Object.FindFirstObjectByType<MkLike.UI.UIAuditRunner>() == null)
                {
                    var runner = new GameObject(RUNNER_NAME);
                    runner.AddComponent<MkLike.UI.UIAuditRunner>();
                }
                return;
            }

            // 씬에 Runner 오브젝트를 미리 배치 (Play 시 자동 Awake/Start)
            var existing = GameObject.Find(RUNNER_NAME);
            if (existing != null)
                Object.DestroyImmediate(existing);

            var go = new GameObject(RUNNER_NAME);
            go.AddComponent<MkLike.UI.UIAuditRunner>();
            Undo.RegisterCreatedObjectUndo(go, "Create UIAuditRunner");

            Debug.Log("[UIAuditSystem] Runner를 씬에 배치했습니다. Play 버튼을 누르면 자동 감사가 시작됩니다.");

            // 자동으로 Play 모드 진입
            EditorApplication.isPlaying = true;
        }
    }
}
