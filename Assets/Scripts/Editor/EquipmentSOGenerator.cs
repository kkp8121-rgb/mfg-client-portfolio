using UnityEditor;
using UnityEngine;
using MkLike.Core;
using MkLike.Data;

namespace MkLike.Editor
{
    /// <summary>
    /// 가챠 풀에 등록됐으나 EquipmentDataSO가 없는 아이템을 일괄 생성한다.
    /// 한 번 실행 후 삭제해도 됨.
    /// </summary>
    public static class EquipmentSOGenerator
    {
        private struct EquipDef
        {
            public string id;
            public string displayName;
            public EquipmentSlot slot;
            public int atk, hp, def;
            public float critRate, atkSpd;
        }

        [MenuItem("mkLike/Tools/Generate Missing Equipment SO", false, 500)]
        public static void Generate()
        {
            var items = new EquipDef[]
            {
                // ── 투구 ──
                new() { id = "helmet_mithril", displayName = "미스릴 투구", slot = EquipmentSlot.Helmet, atk = 0, hp = 30, def = 12, critRate = 0f, atkSpd = 0f },
                new() { id = "helmet_dragon", displayName = "용의 투구", slot = EquipmentSlot.Helmet, atk = 0, hp = 50, def = 20, critRate = 0f, atkSpd = 0f },
                new() { id = "helmet_arcane", displayName = "아케인 투구", slot = EquipmentSlot.Helmet, atk = 5, hp = 70, def = 28, critRate = 0.01f, atkSpd = 0f },

                // ── 상의 ──
                new() { id = "top_mithril", displayName = "미스릴 갑옷", slot = EquipmentSlot.Top, atk = 0, hp = 40, def = 15, critRate = 0f, atkSpd = 0f },
                new() { id = "top_dragon", displayName = "용의 갑옷", slot = EquipmentSlot.Top, atk = 0, hp = 65, def = 25, critRate = 0f, atkSpd = 0f },
                new() { id = "top_arcane", displayName = "아케인 갑옷", slot = EquipmentSlot.Top, atk = 8, hp = 90, def = 35, critRate = 0.01f, atkSpd = 0f },

                // ── 장갑 ──
                new() { id = "gloves_mithril", displayName = "미스릴 장갑", slot = EquipmentSlot.Gloves, atk = 5, hp = 10, def = 8, critRate = 0.02f, atkSpd = 0.03f },
                new() { id = "gloves_dragon", displayName = "용의 장갑", slot = EquipmentSlot.Gloves, atk = 10, hp = 20, def = 14, critRate = 0.03f, atkSpd = 0.05f },
                new() { id = "gloves_arcane", displayName = "아케인 장갑", slot = EquipmentSlot.Gloves, atk = 15, hp = 30, def = 20, critRate = 0.04f, atkSpd = 0.07f },

                // ── 신발 ──
                new() { id = "boots_mithril", displayName = "미스릴 부츠", slot = EquipmentSlot.Boots, atk = 0, hp = 20, def = 10, critRate = 0f, atkSpd = 0.05f },
                new() { id = "boots_dragon", displayName = "용의 부츠", slot = EquipmentSlot.Boots, atk = 0, hp = 35, def = 18, critRate = 0f, atkSpd = 0.08f },
                new() { id = "boots_arcane", displayName = "아케인 부츠", slot = EquipmentSlot.Boots, atk = 5, hp = 50, def = 25, critRate = 0.01f, atkSpd = 0.10f },

                // ── 반지 ──
                new() { id = "ring_gold", displayName = "금 반지", slot = EquipmentSlot.Ring, atk = 4, hp = 0, def = 0, critRate = 0.04f, atkSpd = 0.06f },
                new() { id = "ring_diamond", displayName = "다이아 반지", slot = EquipmentSlot.Ring, atk = 8, hp = 0, def = 0, critRate = 0.06f, atkSpd = 0.08f },
                new() { id = "ring_mythic", displayName = "신화의 반지", slot = EquipmentSlot.Ring, atk = 15, hp = 10, def = 5, critRate = 0.08f, atkSpd = 0.10f },

                // ── 목걸이 ──
                new() { id = "necklace_ruby", displayName = "루비 목걸이", slot = EquipmentSlot.Necklace, atk = 6, hp = 15, def = 0, critRate = 0.03f, atkSpd = 0f },
                new() { id = "necklace_sapphire", displayName = "사파이어 목걸이", slot = EquipmentSlot.Necklace, atk = 10, hp = 25, def = 5, critRate = 0.05f, atkSpd = 0f },
                new() { id = "necklace_dragon", displayName = "용의 목걸이", slot = EquipmentSlot.Necklace, atk = 15, hp = 40, def = 10, critRate = 0.07f, atkSpd = 0.03f },

                // ── 얼굴장식 ──
                new() { id = "face_monocle", displayName = "모노클", slot = EquipmentSlot.FaceAccessory, atk = 3, hp = 0, def = 0, critRate = 0.04f, atkSpd = 0f },
                new() { id = "face_mask", displayName = "전투 마스크", slot = EquipmentSlot.FaceAccessory, atk = 6, hp = 10, def = 5, critRate = 0.05f, atkSpd = 0.02f },
                new() { id = "face_crown", displayName = "왕관", slot = EquipmentSlot.FaceAccessory, atk = 12, hp = 20, def = 10, critRate = 0.07f, atkSpd = 0.05f },
            };

            string dir = "Assets/Data/SO/Equipment";
            string resourceDir = "Assets/Resources/Data/Equipment";
            int created = 0;

            foreach (var def in items)
            {
                string path = $"{dir}/Equipment_{def.id}.asset";
                if (AssetDatabase.LoadAssetAtPath<EquipmentDataSO>(path) != null)
                    continue;

                var so = ScriptableObject.CreateInstance<EquipmentDataSO>();
                so.id = def.id;
                so.displayName = def.displayName;
                so.slot = def.slot;
                so.baseAtk = def.atk;
                so.baseHp = def.hp;
                so.baseDef = def.def;
                so.baseCritRate = def.critRate;
                so.baseAtkSpd = def.atkSpd;

                AssetDatabase.CreateAsset(so, path);

                // Resources 폴더에도 복사
                string resPath = $"{resourceDir}/Equipment_{def.id}.asset";
                AssetDatabase.CopyAsset(path, resPath);

                created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[EquipmentSOGenerator] {created}개 EquipmentDataSO 생성 완료");
        }
    }
}
