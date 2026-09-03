using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using MkLike.Combat;
using MkLike.Data;
using MkLike.UI;

namespace MkLike.Editor
{
    /// <summary>
    /// R8 통합 연결용 에디터 유틸.
    /// 메뉴에서 실행하여 SO 기본값 설정 + 씬 오브젝트 참조 연결을 일괄 처리한다.
    /// </summary>
    public static class R8SetupEditor
    {
        [MenuItem("mkLike/R8 Setup — EliteSummon + HitStop 연결")]
        public static void SetupAll()
        {
            SetupEliteSummonSO();
            ConnectEliteSummonManager();
            ConnectEliteSummonPopupUI();
            ConnectHitStopSystem();

            Debug.Log("[R8SetupEditor] 전체 설정 완료!");
        }

        private static void SetupEliteSummonSO()
        {
            string[] guids = AssetDatabase.FindAssets("t:EliteSummonSO");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[R8SetupEditor] EliteSummonSO 에셋을 찾을 수 없음");
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var so = AssetDatabase.LoadAssetAtPath<EliteSummonSO>(path);
            if (so == null) return;

            so.SetupDefaults();
            EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();

            Debug.Log($"[R8SetupEditor] EliteSummonSO 기본값 설정 완료: {path}");
        }

        private static void ConnectEliteSummonManager()
        {
            var manager = Object.FindFirstObjectByType<EliteSummonManager>();
            if (manager == null)
            {
                Debug.LogWarning("[R8SetupEditor] EliteSummonManager를 씬에서 찾을 수 없음");
                return;
            }

            // SO 연결
            string[] guids = AssetDatabase.FindAssets("t:EliteSummonSO");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var so = AssetDatabase.LoadAssetAtPath<EliteSummonSO>(path);
                var serialized = new SerializedObject(manager);
                serialized.FindProperty("_config").objectReferenceValue = so;

                // MonsterSpawner 연결
                var spawner = Object.FindFirstObjectByType<MonsterSpawner>();
                if (spawner != null)
                    serialized.FindProperty("_spawner").objectReferenceValue = spawner;

                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(manager);
            }

            Debug.Log("[R8SetupEditor] EliteSummonManager 참조 연결 완료");
        }

        private static void ConnectEliteSummonPopupUI()
        {
            var popup = Object.FindFirstObjectByType<EliteSummonPopupUI>(FindObjectsInactive.Include);
            if (popup == null)
            {
                Debug.LogWarning("[R8SetupEditor] EliteSummonPopupUI를 씬에서 찾을 수 없음");
                return;
            }

            // UIDocument에 UXML 할당
            var doc = popup.GetComponent<UIDocument>();
            if (doc != null)
            {
                string[] uxmlGuids = AssetDatabase.FindAssets("PopupEliteSummon t:VisualTreeAsset");
                if (uxmlGuids.Length > 0)
                {
                    string uxmlPath = AssetDatabase.GUIDToAssetPath(uxmlGuids[0]);
                    var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
                    var serialized = new SerializedObject(doc);
                    var treeProp = serialized.FindProperty("sourceAsset");
                    if (treeProp == null) treeProp = serialized.FindProperty("m_VisualTreeAsset");
                    if (treeProp != null) treeProp.objectReferenceValue = uxml;

                    var sortProp = serialized.FindProperty("m_SortingOrder");
                    if (sortProp != null) sortProp.floatValue = 100f;

                    serialized.ApplyModifiedProperties();
                    EditorUtility.SetDirty(doc);
                }
            }

            // 초기 비활성화 (다른 팝업과 동일 패턴)
            popup.gameObject.SetActive(false);
            EditorUtility.SetDirty(popup.gameObject);

            Debug.Log("[R8SetupEditor] EliteSummonPopupUI UXML + sortOrder 연결 완료");
        }

        private static void ConnectHitStopSystem()
        {
            var hitStop = Object.FindFirstObjectByType<HitStopSystem>();
            if (hitStop == null)
            {
                Debug.LogWarning("[R8SetupEditor] HitStopSystem을 씬에서 찾을 수 없음");
                return;
            }

            // 카메라 참조 연결
            var cam = Camera.main;
            if (cam != null)
            {
                var serialized = new SerializedObject(hitStop);
                var camProp = serialized.FindProperty("_camera");
                if (camProp != null)
                {
                    camProp.objectReferenceValue = cam;
                    serialized.ApplyModifiedProperties();
                    EditorUtility.SetDirty(hitStop);
                }
            }

            Debug.Log("[R8SetupEditor] HitStopSystem 카메라 연결 완료");
        }
    }
}
