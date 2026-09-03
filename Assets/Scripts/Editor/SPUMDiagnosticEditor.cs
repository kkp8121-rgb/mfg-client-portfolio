using UnityEngine;
using UnityEditor;
using System.Text;

namespace MkLike.Editor
{
    /// <summary>
    /// SPUM 프리팹의 SpriteRenderer 계층 구조를 진단하는 에디터 도구.
    /// SerializedObject를 통해 SPUM_SpriteList에 접근 (어셈블리 참조 불필요).
    /// </summary>
    public static class SPUMDiagnosticEditor
    {
        [MenuItem("mkLike/Debug/SPUM SpriteRenderer 이름 덤프")]
        public static void DumpSPUMSpriteRenderers()
        {
            string[] prefabPaths = {
                "Assets/Folder_Assets/SPUM/Prefab/AnimationSample/SwordMan.prefab",
                "Assets/Folder_Assets/SPUM/Prefab/AnimationSample/BowMan.prefab",
                "Assets/Folder_Assets/SPUM/Prefab/AnimationSample/MagicianMan.prefab"
            };

            foreach (string path in prefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogWarning($"[SPUMDiagnosticEditor] 프리팹 없음: {path}");
                    continue;
                }

                var sb = new StringBuilder();
                sb.AppendLine($"=== {prefab.name} SpriteRenderer 목록 ===");

                var renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (var sr in renderers)
                {
                    string hierarchy = GetHierarchyPath(sr.transform, prefab.transform);
                    string spriteName = sr.sprite != null ? sr.sprite.name : "(null)";
                    sb.AppendLine($"  [{sr.gameObject.name}] sprite={spriteName}  path={hierarchy}");
                }

                sb.AppendLine($"  총 {renderers.Length}개 SpriteRenderer");

                // Animator 정보
                var animator = prefab.GetComponentInChildren<Animator>(true);
                if (animator != null)
                {
                    sb.AppendLine($"  Animator: {animator.gameObject.name}");
                    if (animator.runtimeAnimatorController != null)
                    {
                        sb.AppendLine($"  Controller: {animator.runtimeAnimatorController.name}");
                        var clips = animator.runtimeAnimatorController.animationClips;
                        sb.AppendLine($"  Clips ({clips.Length}):");
                        foreach (var clip in clips)
                            sb.AppendLine($"    - {clip.name}");
                    }
                    else
                    {
                        sb.AppendLine("  Controller: (null)");
                    }
                }

                // SPUM_SpriteList 정보 (SerializedObject 경유)
                Component spriteList = FindComponentByTypeName(prefab, "SPUM_SpriteList");
                if (spriteList != null)
                {
                    sb.AppendLine($"  SPUM_SpriteList: {spriteList.gameObject.name}");
                    var so = new SerializedObject(spriteList);
                    DumpList(sb, so, "_hairList");
                    DumpList(sb, so, "_clothList");
                    DumpList(sb, so, "_armorList");
                    DumpList(sb, so, "_pantList");
                    DumpList(sb, so, "_weaponList");
                    DumpList(sb, so, "_backList");
                    DumpList(sb, so, "_eyeList");
                    DumpList(sb, so, "_bodyList");
                }

                Debug.Log($"[SPUMDiagnosticEditor]\n{sb}");
            }
        }

        private static void DumpList(StringBuilder sb, SerializedObject so, string listName)
        {
            var prop = so.FindProperty(listName);
            if (prop == null || !prop.isArray || prop.arraySize == 0)
            {
                sb.AppendLine($"    {listName}: (비어있음)");
                return;
            }
            for (int i = 0; i < prop.arraySize; i++)
            {
                var elem = prop.GetArrayElementAtIndex(i);
                var sr = elem.objectReferenceValue as SpriteRenderer;
                if (sr != null)
                    sb.AppendLine($"    {listName}[{i}]: GO={sr.gameObject.name}  sprite={(sr.sprite != null ? sr.sprite.name : "(null)")}");
                else
                    sb.AppendLine($"    {listName}[{i}]: (null ref)");
            }
        }

        private static Component FindComponentByTypeName(GameObject go, string typeName)
        {
            foreach (var comp in go.GetComponentsInChildren<Component>(true))
            {
                if (comp != null && comp.GetType().Name == typeName)
                    return comp;
            }
            return null;
        }

        private static string GetHierarchyPath(Transform child, Transform root)
        {
            var sb = new StringBuilder();
            Transform current = child;
            while (current != null && current != root)
            {
                if (sb.Length > 0) sb.Insert(0, "/");
                sb.Insert(0, current.name);
                current = current.parent;
            }
            return sb.ToString();
        }

        [MenuItem("mkLike/Debug/현재 씬 Player 진단")]
        public static void DiagnoseScenePlayer()
        {
            var player = Object.FindFirstObjectByType<Combat.PlayerCharacter>();
            if (player == null)
            {
                Debug.LogError("[진단] PlayerCharacter가 씬에 없습니다!");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== 씬 Player 진단 ===");

            // CharacterAnimBridge
            var animBridge = player.GetComponent<Combat.CharacterAnimBridge>();
            if (animBridge != null)
                sb.AppendLine($"  CharacterAnimBridge: initialized={animBridge.IsInitialized}, currentClip={animBridge.CurrentClipKey}");
            else
                sb.AppendLine("  CharacterAnimBridge: 없음!");

            // Animator
            var animator = player.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                sb.AppendLine($"  Animator: {animator.gameObject.name}");
                sb.AppendLine($"  Controller: {(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "NULL!")}");
                sb.AppendLine($"  Enabled: {animator.enabled}");
            }
            else
            {
                sb.AppendLine("  Animator: 없음!");
            }

            // CharacterVisual
            var visual = player.GetComponent<Combat.CharacterVisual>();
            if (visual != null)
                sb.AppendLine($"  CharacterVisual: designId={visual.DesignId}");

            // SPUMVisual
            var spumVisual = player.transform.Find("SPUMVisual");
            if (spumVisual != null)
            {
                sb.AppendLine($"  SPUMVisual: 있음 (childCount={spumVisual.childCount})");
                var srs = spumVisual.GetComponentsInChildren<SpriteRenderer>(true);
                int visibleCount = 0;
                foreach (var sr in srs)
                {
                    if (sr.sprite != null) visibleCount++;
                }
                sb.AppendLine($"  SpriteRenderers: 총 {srs.Length}개, 스프라이트 있는 것: {visibleCount}개");
            }
            else
            {
                sb.AppendLine("  SPUMVisual: 없음!");
            }

            Debug.Log($"[SPUMDiagnosticEditor]\n{sb}");
        }
    }
}
