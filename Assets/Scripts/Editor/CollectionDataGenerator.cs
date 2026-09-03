#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using MkLike.Core;
using MkLike.Data;

namespace MkLike.Editor
{
    /// <summary>
    /// CollectionDataSO 기본 에셋 1개를 생성한다 (카테고리별 마일스톤 + 칭호 포함).
    /// </summary>
    public static class CollectionDataGenerator
    {
        private const string TARGET_PATH = "Assets/Resources/Data/CollectionData.asset";

        [MenuItem("mkLike/Collection/Generate Default Data", false, 610)]
        public static void Generate()
        {
            var dir = Path.GetDirectoryName(TARGET_PATH);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var so = AssetDatabase.LoadAssetAtPath<CollectionDataSO>(TARGET_PATH);
            bool isNew = (so == null);
            if (isNew)
            {
                so = ScriptableObject.CreateInstance<CollectionDataSO>();
                AssetDatabase.CreateAsset(so, TARGET_PATH);
            }

            var s = new SerializedObject(so);

            // 카테고리 total 기본값 유지 (SO 기본값 50/30/30/20/20/15/30)

            // 마일스톤 3단계: 30%, 60%, 100%
            SetMilestones(s, "monsterMilestones", 50, StatType.Atk);
            SetMilestones(s, "equipmentMilestones", 30, StatType.Def);
            SetMilestones(s, "weaponMilestones", 30, StatType.Atk);
            SetMilestones(s, "companionMilestones", 20, StatType.MaxHp);
            // 2026-04-20 유물 시스템 제거 — relicMilestones 빈 배열로
            s.FindProperty("relicMilestones").arraySize = 0;
            SetMilestones(s, "petMilestones", 15, StatType.Atk);
            SetMilestones(s, "costumeMilestones", 30, StatType.MaxHp);

            // 칭호 5단계
            SetTitles(s);

            s.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CollectionDataGen] {(isNew ? "생성" : "갱신")} 완료: {TARGET_PATH}");
        }

        private static void SetMilestones(SerializedObject s, string propName, int total, StatType stat)
        {
            var prop = s.FindProperty(propName);
            prop.arraySize = 3;
            SetMilestone(prop.GetArrayElementAtIndex(0), Mathf.Max(1, total * 30 / 100), stat, 0.05f, $"{stat} +5%");
            SetMilestone(prop.GetArrayElementAtIndex(1), Mathf.Max(1, total * 60 / 100), stat, 0.10f, $"{stat} +10%");
            SetMilestone(prop.GetArrayElementAtIndex(2), total, stat, 0.20f, $"{stat} +20% (완주)");
        }

        private static void SetMilestone(SerializedProperty elem, int threshold, StatType stat, float percent, string desc)
        {
            elem.FindPropertyRelative("threshold").intValue = threshold;
            elem.FindPropertyRelative("statType").enumValueIndex = (int)stat;
            elem.FindPropertyRelative("flatBonus").floatValue = 0f;
            elem.FindPropertyRelative("percentBonus").floatValue = percent;
            elem.FindPropertyRelative("description").stringValue = desc;
        }

        private static void SetTitles(SerializedObject s)
        {
            var prop = s.FindProperty("titles");
            prop.arraySize = 5;
            SetTitle(prop.GetArrayElementAtIndex(0), "수집 초심자", 10, "10종 수집 달성", new Color(0.8f, 0.8f, 0.8f));
            SetTitle(prop.GetArrayElementAtIndex(1), "수집가", 50, "50종 수집 달성", new Color(0.3f, 0.9f, 0.3f));
            SetTitle(prop.GetArrayElementAtIndex(2), "열혈 수집가", 100, "100종 수집 달성", new Color(0.3f, 0.6f, 1f));
            SetTitle(prop.GetArrayElementAtIndex(3), "마스터 수집가", 150, "150종 수집 달성", new Color(0.9f, 0.4f, 0.9f));
            SetTitle(prop.GetArrayElementAtIndex(4), "수집의 제왕", 195, "195종 수집 달성 (올클)", new Color(1f, 0.85f, 0.2f));
        }

        private static void SetTitle(SerializedProperty elem, string name, int required, string desc, Color color)
        {
            elem.FindPropertyRelative("titleName").stringValue = name;
            elem.FindPropertyRelative("requiredTotal").intValue = required;
            elem.FindPropertyRelative("description").stringValue = desc;
            elem.FindPropertyRelative("titleColor").colorValue = color;
        }
    }
}
#endif
