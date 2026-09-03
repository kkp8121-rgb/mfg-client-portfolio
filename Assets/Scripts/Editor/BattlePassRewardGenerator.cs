#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using MkLike.Core;
using MkLike.Data;

namespace MkLike.Editor
{
    /// <summary>
    /// BattlePassRewardSO 50레벨치를 일괄 생성/갱신한다.
    /// 10/20/30/40/50Lv은 마일스톤(재화 대신 특수 보상). 기본 스펙이 없으므로 합리적 Default 제공.
    /// </summary>
    public static class BattlePassRewardGenerator
    {
        private const string TARGET_DIR = "Assets/Resources/Data/BattlePass";
        private const int MAX_LEVEL = 50;

        [MenuItem("mkLike/BattlePass/Generate Default Rewards (50)", false, 600)]
        public static void GenerateAll()
        {
            if (!Directory.Exists(TARGET_DIR))
                Directory.CreateDirectory(TARGET_DIR);

            int created = 0, updated = 0;
            for (int lv = 1; lv <= MAX_LEVEL; lv++)
            {
                string path = $"{TARGET_DIR}/BPReward_{lv:D02}.asset";
                var so = AssetDatabase.LoadAssetAtPath<BattlePassRewardSO>(path);
                bool isNew = (so == null);
                if (isNew)
                {
                    so = ScriptableObject.CreateInstance<BattlePassRewardSO>();
                    AssetDatabase.CreateAsset(so, path);
                }

                var sObj = new SerializedObject(so);
                sObj.FindProperty("_level").intValue = lv;

                bool milestone = (lv % 10 == 0);
                sObj.FindProperty("_isMilestone").boolValue = milestone;

                // Free 트랙 — 주로 Gold/RuneFragment/StarCrystal 로테이션
                var (freeType, freeAmount) = GetFreeReward(lv);
                sObj.FindProperty("_freeRewardType").enumValueIndex = (int)freeType;
                sObj.FindProperty("_freeRewardAmount").intValue = freeAmount;

                // Premium 트랙 — Ruby/특수 재화 위주
                var (premType, premAmount) = GetPremiumReward(lv);
                sObj.FindProperty("_premiumRewardType").enumValueIndex = (int)premType;
                sObj.FindProperty("_premiumRewardAmount").intValue = premAmount;

                sObj.ApplyModifiedPropertiesWithoutUndo();

                if (isNew) created++;
                else updated++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[BPRewardGen] 생성 {created}, 갱신 {updated}, 경로 {TARGET_DIR}");
        }

        private static (CurrencyType, int) GetFreeReward(int lv)
        {
            if (lv % 10 == 0) return (CurrencyType.Gold, 10000 + lv * 500); // milestone: 큰 Gold
            int tier = lv % 5;
            return tier switch
            {
                1 => (CurrencyType.Gold, 500 + lv * 50),
                2 => (CurrencyType.RuneFragment, 5 + lv / 5),
                3 => (CurrencyType.Gold, 300 + lv * 30),
                4 => (CurrencyType.StarCrystal, 3 + lv / 10),
                _ => (CurrencyType.Gold, 400 + lv * 40),
            };
        }

        private static (CurrencyType, int) GetPremiumReward(int lv)
        {
            if (lv % 10 == 0) return (CurrencyType.Ruby, 200 + lv * 10); // milestone: 큰 Ruby
            int tier = lv % 5;
            return tier switch
            {
                1 => (CurrencyType.Ruby, 30 + lv),
                2 => (CurrencyType.WeaponTicket, 1),
                3 => (CurrencyType.Ruby, 40 + lv),
                4 => (CurrencyType.StarCrystal, 5 + lv / 5),
                _ => (CurrencyType.Ruby, 50 + lv * 2),
            };
        }
    }
}
#endif
