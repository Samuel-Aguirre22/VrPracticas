using System.Collections.Generic;
using System.Linq;
using Oculus.Interaction.Editor.QuickActions;
using Oculus.Interaction.HandGrab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Adds Meta Interaction SDK hand grab to the Coffee Shop objects using the same
// Quick Action as right click > Interaction SDK > Add Grab Interaction.
// Each object gets a Rigidbody (kinematic, no gravity), a Grabbable and a HandGrabInteractable child.
public static class VRAddHandGrab
{
    const string MenuRoot = "VR/Add HandGrab/";

    [MenuItem(MenuRoot + "To All Products In Open Scenes")]
    static void ToAllProducts()
    {
        var targets = Object
            .FindObjectsByType<PW.ProductGameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Select(p => p.gameObject);
        Apply(targets, "products");
    }

    [MenuItem(MenuRoot + "To Selected Objects")]
    static void ToSelected()
    {
        Apply(Selection.gameObjects, "selected objects");
    }

    [MenuItem(MenuRoot + "To Selected Objects", true)]
    static bool ToSelectedValidate()
    {
        return Selection.gameObjects.Length > 0;
    }

    static void Apply(IEnumerable<GameObject> candidates, string label)
    {
        var all = candidates.Where(go => go != null && go.scene.IsValid()).Distinct().ToList();
        var allSet = new HashSet<GameObject>(all);

        int added = 0, already = 0, nested = 0, failed = 0;
        var touchedScenes = new HashSet<UnityEngine.SceneManagement.Scene>();

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        foreach (var go in all)
        {
            // Two grabbables on the same hierarchy would fight each other.
            if (HasAncestorIn(go, allSet))
            {
                nested++;
                continue;
            }

            if (go.GetComponentInChildren<HandGrabInteractable>(true) != null)
            {
                already++;
                continue;
            }

            try
            {
                QuickActionsAPI.AddGrabInteraction(go);
                touchedScenes.Add(go.scene);
                added++;
            }
            catch (System.Exception e)
            {
                failed++;
                Debug.LogWarning($"[VR] Could not add HandGrab to '{go.name}': {e.Message}", go);
            }
        }

        Undo.CollapseUndoOperations(undoGroup);

        foreach (var scene in touchedScenes)
            EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log($"[VR] HandGrab on {label}: {added} added, {already} already had it, " +
                  $"{nested} skipped (nested inside another target), {failed} failed. Save the scene to keep the changes.");
    }

    static bool HasAncestorIn(GameObject go, HashSet<GameObject> set)
    {
        for (Transform t = go.transform.parent; t != null; t = t.parent)
            if (set.Contains(t.gameObject))
                return true;
        return false;
    }
}
