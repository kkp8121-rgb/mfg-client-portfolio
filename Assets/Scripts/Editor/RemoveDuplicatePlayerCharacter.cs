using UnityEngine;
using UnityEditor;
using MkLike.Combat;

public class RemoveDuplicatePlayerCharacter
{
    [MenuItem("mkLike/Fix/Remove Duplicate PlayerCharacter")]
    public static void Execute()
    {
        var player = GameObject.Find("Player");
        if (player == null) { Debug.LogWarning("Player not found"); return; }
        var pcs = player.GetComponents<PlayerCharacter>();
        if (pcs.Length <= 1) { Debug.Log($"No duplicates. Count: {pcs.Length}"); return; }

        // 첫 번째(정상)를 유지하고 나머지 제거
        for (int i = pcs.Length - 1; i >= 1; i--)
        {
            Undo.DestroyObjectImmediate(pcs[i]);
        }
        Debug.Log($"Removed {pcs.Length - 1} duplicate(s). Remaining: {player.GetComponents<PlayerCharacter>().Length}");
        EditorUtility.SetDirty(player);
    }
}
