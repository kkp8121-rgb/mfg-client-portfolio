using UnityEngine;
using UnityEditor;

namespace MkLike.Editor
{
    /// <summary>
    /// Main Camera에 CameraController를 자동 설정하는 에디터 도구.
    /// 탑다운 전환: 고정 bounds 대신 ArenaMap 참조.
    /// </summary>
    public static class CameraSetupEditor
    {
        public static void SetupCamera()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("[CameraSetup] Main Camera를 찾을 수 없습니다.");
                return;
            }

            var cameraObj = mainCamera.gameObject;

            Undo.RecordObject(mainCamera, "Setup Camera Properties");
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 8f;
            mainCamera.backgroundColor = new Color(0.15f, 0.15f, 0.2f, 1f);

            var existing = cameraObj.GetComponent<MkLike.Combat.CameraController>();
            if (existing != null)
            {
                Debug.Log("[CameraSetup] CameraController가 이미 존재합니다 — 건너뜀.");
                return;
            }

            var controller = Undo.AddComponent<MkLike.Combat.CameraController>(cameraObj);

            var so = new SerializedObject(controller);
            so.FindProperty("smoothSpeed").floatValue = 5f;
            so.FindProperty("offset").vector3Value = new Vector3(0f, 0f, -10f);

            // ArenaMap 연결
            var arenaMap = Object.FindFirstObjectByType<MkLike.Combat.ArenaMap>();
            if (arenaMap != null)
                so.FindProperty("arenaMap").objectReferenceValue = arenaMap;

            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = cameraObj;
            Debug.Log("[CameraSetup] Main Camera에 탑다운 CameraController 설정 완료!");
        }
    }
}
