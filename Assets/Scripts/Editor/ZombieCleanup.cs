using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using System.Collections.Generic;

namespace MkLike.Editor
{
    public static class ZombieCleanup
    {
        [MenuItem("mkLike/Cleanup Zombie Objects", false, 900)]
        public static void Run()
        {
            // 완전 삭제 대상 (삭제된 시스템)
            var zombieNames = new HashSet<string>
            {
                "[UITK] Companion", "[UITK] Costume", "[UITK] Arena", "[UITK] ArenaBattle",
                "[UITK] Guild", "[UITK] GuildBoss", "[UITK] Char_Ability",
                "[UITK] Popup_ArenaResult", "[UITK] Popup_GuildBoss"
            };

            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            int zombieCount = 0;
            int dupeCount = 0;

            // 1단계: 좀비 삭제
            foreach (var go in roots)
            {
                if (go == null) continue;
                if (zombieNames.Contains(go.name))
                {
                    Undo.DestroyObjectImmediate(go);
                    zombieCount++;
                }
            }

            // 2단계: 중복 오브젝트 정리 (동명 오브젝트 중 첫 번째만 유지)
            roots = scene.GetRootGameObjects(); // 삭제 후 재조회
            var seen = new Dictionary<string, GameObject>();
            foreach (var go in roots)
            {
                if (go == null) continue;
                if (seen.ContainsKey(go.name))
                {
                    Undo.DestroyObjectImmediate(go);
                    dupeCount++;
                }
                else
                {
                    seen[go.name] = go;
                }
            }

            Debug.Log($"[ZombieCleanup] 좀비 {zombieCount}개 + 중복 {dupeCount}개 삭제 완료");
        }
    }
}
