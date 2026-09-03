using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;

namespace MkLike.Editor
{
    /// <summary>
    /// Play 모드에서 모든 UITK 패널을 순차 활성화/비활성화하여 null 에러를 검출한다.
    /// 메뉴: mkLike/R8 Panel Test (Play Mode)
    /// </summary>
    public static class R8PanelTestEditor
    {
        [MenuItem("mkLike/R8 Panel Test (Play Mode)")]
        public static void RunPanelTest()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[R8PanelTest] Play 모드에서만 실행 가능합니다.");
                return;
            }

            RunTestAsync().Forget();
        }

        private static async UniTaskVoid RunTestAsync()
        {
            Debug.LogWarning("[R8PanelTest] === UI 패널 순회 테스트 시작 ===");

            var docs = Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int passed = 0;
            int failed = 0;

            foreach (var doc in docs)
            {
                string name = doc.gameObject.name;
                bool wasActive = doc.gameObject.activeSelf;

                try
                {
                    // 활성화
                    Debug.LogWarning($"[R8PanelTest] 테스트 중: {name}");
                    doc.gameObject.SetActive(true);
                    await UniTask.DelayFrame(2);

                    // rootVisualElement 접근 검증
                    var root = doc.rootVisualElement;
                    if (root == null)
                    {
                        Debug.LogWarning($"[R8PanelTest] FAIL: {name} — rootVisualElement null");
                        failed++;
                    }
                    else
                    {
                        Debug.LogWarning($"[R8PanelTest] PASS: {name} (children: {root.childCount})");
                        passed++;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[R8PanelTest] ERROR: {name} — {ex.Message}");
                    failed++;
                }
                finally
                {
                    // 원래 상태 복원
                    doc.gameObject.SetActive(wasActive);
                    await UniTask.DelayFrame(1);
                }
            }

            Debug.LogWarning($"[R8PanelTest] === 완료: {passed} PASS / {failed} FAIL (총 {docs.Length}개) ===");
        }
    }
}
