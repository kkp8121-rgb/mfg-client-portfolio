using UnityEngine;
using UnityEditor;

namespace MkLike.Editor
{
    /// <summary>
    /// TestManager 커스텀 인스펙터. 버튼으로 테스트 기능 실행.
    /// </summary>
    [CustomEditor(typeof(Economy.TestManager))]
    public class TestManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var mgr = (Economy.TestManager)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("테스트 도구", EditorStyles.boldLabel);

            GUI.backgroundColor = new Color(0.8f, 0.3f, 0.4f);
            if (GUILayout.Button("루비 +10,000", GUILayout.Height(36)))
                mgr.AddRuby10000();

            GUI.backgroundColor = new Color(1f, 0.85f, 0.2f);
            if (GUILayout.Button("골드 +100,000", GUILayout.Height(36)))
                mgr.AddGold100000();

            EditorGUILayout.Space(5);
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.5f);
            if (GUILayout.Button("레벨업 +1", GUILayout.Height(36)))
                mgr.ForceLevelUp();

            GUI.backgroundColor = new Color(0.2f, 0.6f, 0.9f);
            if (GUILayout.Button("레벨업 +10", GUILayout.Height(36)))
                mgr.ForceLevelUp10();

            EditorGUILayout.Space(5);
            GUI.backgroundColor = new Color(0.6f, 0.4f, 0.8f);
            if (GUILayout.Button("테스트 장비 3종 추가", GUILayout.Height(36)))
                mgr.AddTestEquipment();

            GUI.backgroundColor = Color.white;
        }
    }
}
