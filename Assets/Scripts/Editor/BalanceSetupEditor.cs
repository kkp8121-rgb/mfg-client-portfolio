using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MkLike.Data;
using MkLike.Equipment;

namespace MkLike.Editor
{
    /// <summary>
    /// 밸런스 관련 SO 데이터를 일괄 설정한다.
    /// - GachaPoolSO summonLevels (17레벨, 레퍼런스 JSON 기반)
    /// - EquipmentManager 카탈로그 자동 연결
    /// </summary>
    public static class BalanceSetupEditor
    {
        [MenuItem("mkLike/Balance Setup — Gacha Levels + Equipment Catalog")]
        public static void SetupAll()
        {
            SetupGachaSummonLevels();
            SetupEquipmentCatalog();
            AssetDatabase.SaveAssets();
            Debug.Log("[BalanceSetup] === 전체 설정 완료 ===");
        }

        // ── 가챠 소환 레벨 (17레벨, 레퍼런스 기반) ──

        /// <summary>누적 뽑기 수 임계값 (레벨 1~17)</summary>
        private static readonly int[] Thresholds =
        {
            0, 20, 50, 100, 160, 240, 340, 460, 600, 760,
            940, 1140, 1360, 1600, 1860, 2140, 2440
        };

        /// <summary>
        /// 레벨별 등급+티어 가중치 (합계 10,000,000 = 100%).
        /// 키: "Grade Tier" (e.g. "Normal T4"), 값: 가중치.
        /// 출처: Docs/Planning/reference/gacha-summon-levels.json
        /// </summary>
        private static readonly Dictionary<string, float>[] LevelWeights =
        {
            // Lv.1
            new() {
                ["Normal T4"]=3680000, ["Normal T3"]=2760000, ["Normal T2"]=1840000, ["Normal T1"]=920000,
                ["Rare T4"]=560000, ["Rare T3"]=240000
            },
            // Lv.2
            new() {
                ["Normal T4"]=3520000, ["Normal T3"]=2640000, ["Normal T2"]=1760000, ["Normal T1"]=880000,
                ["Rare T4"]=600000, ["Rare T3"]=360000, ["Rare T2"]=180000, ["Rare T1"]=60000
            },
            // Lv.3
            new() {
                ["Normal T4"]=3368000, ["Normal T3"]=2526000, ["Normal T2"]=1684000, ["Normal T1"]=842000,
                ["Rare T4"]=600000, ["Rare T3"]=450000, ["Rare T2"]=300000, ["Rare T1"]=150000,
                ["Epic T4"]=56000, ["Epic T3"]=24000
            },
            // Lv.4
            new() {
                ["Normal T4"]=3224000, ["Normal T3"]=2418000, ["Normal T2"]=1612000, ["Normal T1"]=806000,
                ["Rare T4"]=704000, ["Rare T3"]=528000, ["Rare T2"]=352000, ["Rare T1"]=176000,
                ["Epic T4"]=72000, ["Epic T3"]=54000, ["Epic T2"]=36000, ["Epic T1"]=18000
            },
            // Lv.5
            new() {
                ["Normal T4"]=3094000, ["Normal T3"]=2320500, ["Normal T2"]=1547000, ["Normal T1"]=773500,
                ["Rare T4"]=806000, ["Rare T3"]=604500, ["Rare T2"]=403000, ["Rare T1"]=201500,
                ["Epic T4"]=92000, ["Epic T3"]=69000, ["Epic T2"]=46000, ["Epic T1"]=23000,
                ["Unique T4"]=14000, ["Unique T3"]=6000
            },
            // Lv.6
            new() {
                ["Normal T4"]=2964000, ["Normal T3"]=2223000, ["Normal T2"]=1482000, ["Normal T1"]=741000,
                ["Rare T4"]=908000, ["Rare T3"]=681000, ["Rare T2"]=454000, ["Rare T1"]=227000,
                ["Epic T4"]=104000, ["Epic T3"]=78000, ["Epic T2"]=52000, ["Epic T1"]=26000,
                ["Unique T4"]=30000, ["Unique T3"]=18000, ["Unique T2"]=9000, ["Unique T1"]=3000
            },
            // Lv.7
            new() {
                ["Normal T4"]=2810800, ["Normal T3"]=2108100, ["Normal T2"]=1405400, ["Normal T1"]=702700,
                ["Rare T4"]=1028000, ["Rare T3"]=771000, ["Rare T2"]=514000, ["Rare T1"]=257000,
                ["Epic T4"]=124000, ["Epic T3"]=93000, ["Epic T2"]=62000, ["Epic T1"]=31000,
                ["Unique T4"]=36000, ["Unique T3"]=27000, ["Unique T2"]=18000, ["Unique T1"]=9000,
                ["Legendary T4"]=3000
            },
            // Lv.8
            new() {
                ["Normal T4"]=2656800, ["Normal T3"]=1992600, ["Normal T2"]=1328400, ["Normal T1"]=664200,
                ["Rare T4"]=1148000, ["Rare T3"]=861000, ["Rare T2"]=574000, ["Rare T1"]=287000,
                ["Epic T4"]=144000, ["Epic T3"]=108000, ["Epic T2"]=72000, ["Epic T1"]=36000,
                ["Unique T4"]=48000, ["Unique T3"]=36000, ["Unique T2"]=24000, ["Unique T1"]=12000,
                ["Legendary T4"]=5600, ["Legendary T3"]=2400
            },
            // Lv.9
            new() {
                ["Normal T4"]=2501600, ["Normal T3"]=1876200, ["Normal T2"]=1250800, ["Normal T1"]=625400,
                ["Rare T4"]=1268000, ["Rare T3"]=951000, ["Rare T2"]=634000, ["Rare T1"]=317000,
                ["Epic T4"]=164000, ["Epic T3"]=123000, ["Epic T2"]=82000, ["Epic T1"]=41000,
                ["Unique T4"]=60000, ["Unique T3"]=45000, ["Unique T2"]=30000, ["Unique T1"]=15000,
                ["Legendary T4"]=9600, ["Legendary T3"]=4800, ["Legendary T2"]=1600
            },
            // Lv.10
            new() {
                ["Normal T4"]=2346400, ["Normal T3"]=1759800, ["Normal T2"]=1173200, ["Normal T1"]=586600,
                ["Rare T4"]=1388000, ["Rare T3"]=1041000, ["Rare T2"]=694000, ["Rare T1"]=347000,
                ["Epic T4"]=184000, ["Epic T3"]=138000, ["Epic T2"]=92000, ["Epic T1"]=46000,
                ["Unique T4"]=72000, ["Unique T3"]=54000, ["Unique T2"]=36000, ["Unique T1"]=18000,
                ["Legendary T4"]=12000, ["Legendary T3"]=7200, ["Legendary T2"]=3600, ["Legendary T1"]=1200
            },
            // Lv.11
            new() {
                ["Normal T4"]=2190800, ["Normal T3"]=1643100, ["Normal T2"]=1095400, ["Normal T1"]=547700,
                ["Rare T4"]=1508000, ["Rare T3"]=1131000, ["Rare T2"]=754000, ["Rare T1"]=377000,
                ["Epic T4"]=204000, ["Epic T3"]=153000, ["Epic T2"]=102000, ["Epic T1"]=51000,
                ["Unique T4"]=84000, ["Unique T3"]=63000, ["Unique T2"]=42000, ["Unique T1"]=21000,
                ["Legendary T4"]=12800, ["Legendary T3"]=10560, ["Legendary T2"]=6400, ["Legendary T1"]=2240,
                ["Mythic T4"]=1000
            },
            // Lv.12
            new() {
                ["Normal T4"]=2036800, ["Normal T3"]=1527600, ["Normal T2"]=1018400, ["Normal T1"]=509200,
                ["Rare T4"]=1628000, ["Rare T3"]=1221000, ["Rare T2"]=814000, ["Rare T1"]=407000,
                ["Epic T4"]=224000, ["Epic T3"]=168000, ["Epic T2"]=112000, ["Epic T1"]=56000,
                ["Unique T4"]=96000, ["Unique T3"]=72000, ["Unique T2"]=48000, ["Unique T1"]=24000,
                ["Legendary T4"]=14400, ["Legendary T3"]=11880, ["Legendary T2"]=7200, ["Legendary T1"]=2520,
                ["Mythic T4"]=1400, ["Mythic T3"]=600
            },
            // Lv.13
            new() {
                ["Normal T4"]=1882000, ["Normal T3"]=1411500, ["Normal T2"]=941000, ["Normal T1"]=470500,
                ["Rare T4"]=1748000, ["Rare T3"]=1311000, ["Rare T2"]=874000, ["Rare T1"]=437000,
                ["Epic T4"]=244000, ["Epic T3"]=183000, ["Epic T2"]=122000, ["Epic T1"]=61000,
                ["Unique T4"]=108000, ["Unique T3"]=81000, ["Unique T2"]=54000, ["Unique T1"]=27000,
                ["Legendary T4"]=16000, ["Legendary T3"]=13200, ["Legendary T2"]=8000, ["Legendary T1"]=2800,
                ["Mythic T4"]=3000, ["Mythic T3"]=1500, ["Mythic T2"]=500
            },
            // Lv.14
            new() {
                ["Normal T4"]=1729200, ["Normal T3"]=1296900, ["Normal T2"]=864600, ["Normal T1"]=432300,
                ["Rare T4"]=1868000, ["Rare T3"]=1401000, ["Rare T2"]=934000, ["Rare T1"]=467000,
                ["Epic T4"]=264000, ["Epic T3"]=198000, ["Epic T2"]=132000, ["Epic T1"]=66000,
                ["Unique T4"]=120000, ["Unique T3"]=90000, ["Unique T2"]=60000, ["Unique T1"]=30000,
                ["Legendary T4"]=16000, ["Legendary T3"]=13200, ["Legendary T2"]=8000, ["Legendary T1"]=2800,
                ["Mythic T4"]=3500, ["Mythic T3"]=2100, ["Mythic T2"]=1050, ["Mythic T1"]=350
            },
            // Lv.15
            new() {
                ["Normal T4"]=1575480, ["Normal T3"]=1181610, ["Normal T2"]=787740, ["Normal T1"]=393870,
                ["Rare T4"]=1988000, ["Rare T3"]=1491000, ["Rare T2"]=994000, ["Rare T1"]=497000,
                ["Epic T4"]=284000, ["Epic T3"]=213000, ["Epic T2"]=142000, ["Epic T1"]=71000,
                ["Unique T4"]=132000, ["Unique T3"]=99000, ["Unique T2"]=66000, ["Unique T1"]=33000,
                ["Legendary T4"]=16000, ["Legendary T3"]=13200, ["Legendary T2"]=8000, ["Legendary T1"]=2800,
                ["Mythic T4"]=5500, ["Mythic T3"]=3300, ["Mythic T2"]=1430, ["Mythic T1"]=770,
                ["Ancient T4"]=300
            },
            // Lv.16
            new() {
                ["Normal T4"]=1573680, ["Normal T3"]=1180260, ["Normal T2"]=786840, ["Normal T1"]=393420,
                ["Rare T4"]=1988000, ["Rare T3"]=1491000, ["Rare T2"]=994000, ["Rare T1"]=497000,
                ["Epic T4"]=284000, ["Epic T3"]=213000, ["Epic T2"]=142000, ["Epic T1"]=71000,
                ["Unique T4"]=132000, ["Unique T3"]=99000, ["Unique T2"]=66000, ["Unique T1"]=33000,
                ["Legendary T4"]=16000, ["Legendary T3"]=12000, ["Legendary T2"]=8000, ["Legendary T1"]=4000,
                ["Mythic T4"]=6000, ["Mythic T3"]=4950, ["Mythic T2"]=3000, ["Mythic T1"]=1050,
                ["Ancient T4"]=600, ["Ancient T3"]=200
            },
            // Lv.17
            new() {
                ["Normal T4"]=1571880, ["Normal T3"]=1178910, ["Normal T2"]=785940, ["Normal T1"]=392970,
                ["Rare T4"]=1988000, ["Rare T3"]=1491000, ["Rare T2"]=994000, ["Rare T1"]=497000,
                ["Epic T4"]=284000, ["Epic T3"]=213000, ["Epic T2"]=142000, ["Epic T1"]=71000,
                ["Unique T4"]=132000, ["Unique T3"]=99000, ["Unique T2"]=66000, ["Unique T1"]=33000,
                ["Legendary T4"]=16000, ["Legendary T3"]=12000, ["Legendary T2"]=8000, ["Legendary T1"]=4000,
                ["Mythic T4"]=7600, ["Mythic T3"]=6270, ["Mythic T2"]=3800, ["Mythic T1"]=1330,
                ["Ancient T4"]=845, ["Ancient T3"]=325, ["Ancient T2"]=130
            },
        };

        private static void SetupGachaSummonLevels()
        {
            string[] guids = AssetDatabase.FindAssets("t:GachaPoolSO");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var pool = AssetDatabase.LoadAssetAtPath<GachaPoolSO>(path);
                if (pool == null) continue;

                var so = new SerializedObject(pool);
                var levelsProp = so.FindProperty("summonLevels");
                if (levelsProp == null) continue;

                int levelCount = LevelWeights.Length;
                levelsProp.arraySize = levelCount;

                for (int i = 0; i < levelCount; i++)
                {
                    var elem = levelsProp.GetArrayElementAtIndex(i);
                    elem.FindPropertyRelative("requiredPulls").intValue = Thresholds[i];

                    // gradeTierWeights 설정 (등급+티어 세분화)
                    var tierWeightsProp = elem.FindPropertyRelative("gradeTierWeights");
                    var entries = LevelWeights[i];
                    tierWeightsProp.arraySize = entries.Count;

                    int idx = 0;
                    foreach (var kv in entries)
                    {
                        ParseGradeTier(kv.Key, out string grade, out int tier);
                        var entry = tierWeightsProp.GetArrayElementAtIndex(idx);
                        entry.FindPropertyRelative("grade").stringValue = grade;
                        entry.FindPropertyRelative("tier").intValue = tier;
                        entry.FindPropertyRelative("weight").floatValue = kv.Value;
                        idx++;
                    }

                    // gradeWeights도 폴백용으로 등급 합산 설정
                    var gw = elem.FindPropertyRelative("gradeWeights");
                    float[] gradeSum = SumByGrade(entries);
                    gw.arraySize = gradeSum.Length;
                    for (int j = 0; j < gradeSum.Length; j++)
                        gw.GetArrayElementAtIndex(j).floatValue = gradeSum[j];
                }

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(pool);
                Debug.Log($"[BalanceSetup] summonLevels 설정: {path} ({levelCount}레벨, 레퍼런스 기반)");
            }
        }

        /// <summary>"Normal T4" → grade="Normal", tier=4</summary>
        private static void ParseGradeTier(string key, out string grade, out int tier)
        {
            int lastSpace = key.LastIndexOf(' ');
            grade = key.Substring(0, lastSpace);
            string tierStr = key.Substring(lastSpace + 2); // "T4" → "4"
            tier = int.Parse(tierStr);
        }

        /// <summary>등급+티어 가중치를 등급별로 합산 (7등급: Normal~Ancient)</summary>
        private static float[] SumByGrade(Dictionary<string, float> entries)
        {
            string[] grades = { "Normal", "Rare", "Epic", "Unique", "Legendary", "Mythic", "Ancient" };
            float[] result = new float[grades.Length];
            float total = 0f;

            foreach (var kv in entries)
            {
                ParseGradeTier(kv.Key, out string grade, out _);
                for (int i = 0; i < grades.Length; i++)
                {
                    if (grades[i] == grade)
                    {
                        result[i] += kv.Value;
                        total += kv.Value;
                        break;
                    }
                }
            }

            // 정규화 (100% 기준)
            if (total > 0f)
            {
                for (int i = 0; i < result.Length; i++)
                    result[i] = result[i] / total * 100f;
            }

            return result;
        }

        // ── 장비 카탈로그 ──

        private static void SetupEquipmentCatalog()
        {
            // 모든 EquipmentDataSO 찾기
            string[] eqGuids = AssetDatabase.FindAssets("t:EquipmentDataSO");
            var catalog = new List<EquipmentDataSO>();
            foreach (string guid in eqGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var eqSo = AssetDatabase.LoadAssetAtPath<EquipmentDataSO>(path);
                if (eqSo != null) catalog.Add(eqSo);
            }

            // EquipmentManager에 카탈로그 할당
            var eqMgr = Object.FindFirstObjectByType<EquipmentManager>();
            if (eqMgr == null)
            {
                Debug.LogWarning("[BalanceSetup] EquipmentManager를 찾을 수 없음");
                return;
            }

            var serialized = new SerializedObject(eqMgr);
            var catalogProp = serialized.FindProperty("_catalog");
            if (catalogProp == null)
            {
                Debug.LogWarning("[BalanceSetup] _catalog 프로퍼티를 찾을 수 없음");
                return;
            }

            catalogProp.arraySize = catalog.Count;
            for (int i = 0; i < catalog.Count; i++)
                catalogProp.GetArrayElementAtIndex(i).objectReferenceValue = catalog[i];

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(eqMgr);
            Debug.Log($"[BalanceSetup] EquipmentManager 카탈로그: {catalog.Count}개 SO 연결");
        }
    }
}
