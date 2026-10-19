using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

// One-click preparation of the VR delivery scene:
//  1. EventSystem uses the Input System UI module (the project only enables the new Input System).
//  2. The free-teleport NavMesh ignores the rig, hotspots and teleport markers, treats the grabbable
//     objects as non-walkable, and is baked into an asset next to the scene.
public static class VRPrepareScene
{
    const string VRRoot = "VR";

    [MenuItem("VR/Prepare Scene (EventSystem + NavMesh)")]
    static void Prepare()
    {
        int swapped = FixEventSystems();
        string navResult = BakeTeleportNavMesh();
        Debug.Log($"[VR] EventSystems fixed: {swapped}. {navResult} Save the scene (Ctrl+S) to keep the changes.");
    }

    [MenuItem("VR/Bake Teleport NavMesh")]
    static void BakeOnly()
    {
        Debug.Log("[VR] " + BakeTeleportNavMesh() + " Save the scene (Ctrl+S) to keep the changes.");
    }

    static int FixEventSystems()
    {
        int swapped = 0;
        foreach (var es in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var legacy = es.GetComponent<StandaloneInputModule>();
            if (legacy == null && es.GetComponent<InputSystemUIInputModule>() != null)
                continue;

            if (legacy != null)
                Undo.DestroyObjectImmediate(legacy);

            if (es.GetComponent<InputSystemUIInputModule>() == null)
            {
                var module = Undo.AddComponent<InputSystemUIInputModule>(es.gameObject);
                module.AssignDefaultActions();
            }

            EditorSceneManager.MarkSceneDirty(es.gameObject.scene);
            swapped++;
        }
        return swapped;
    }

    static string BakeTeleportNavMesh()
    {
        var surface = Object.FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>();
        if (surface == null)
            return "No NavMeshSurface found in the open scene.";

        var scene = surface.gameObject.scene;
        if (string.IsNullOrEmpty(scene.path))
            return "Save the scene first so the NavMesh asset has a folder.";

        // Rig, hotspots and teleport markers are moving or already teleport targets: keep them out of the bake.
        // Grabbable objects are baked as not walkable so the teleport blocker rejects their area.
        var vr = GameObject.Find(VRRoot);
        if (vr != null)
        {
            foreach (Transform child in vr.transform)
            {
                bool grabbables = child.name == "Interactables";
                bool excluded = child.name.Contains("Camera Rig") || child.name == "HotSpots" || child.name == "Teleports";
                if (grabbables)
                    SetModifier(child.gameObject, ignore: false, notWalkable: true);
                else if (excluded)
                    SetModifier(child.gameObject, ignore: true, notWalkable: false);
            }
        }

        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        surface.layerMask = ~0;
        surface.defaultArea = 0; // Walkable
        EditorUtility.SetDirty(surface);

        surface.RemoveData();
        surface.BuildNavMesh();

        NavMeshData data = surface.navMeshData;
        if (data == null)
            return "NavMesh bake produced no data. Check that the Plane has a MeshRenderer.";

        string folder = Path.Combine(Path.GetDirectoryName(scene.path), Path.GetFileNameWithoutExtension(scene.path))
            .Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(scene.path).Replace('\\', '/'),
                Path.GetFileNameWithoutExtension(scene.path));

        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/NavMesh-{surface.name}.asset");
        if (!AssetDatabase.Contains(data))
            AssetDatabase.CreateAsset(data, assetPath);

        surface.RemoveData();
        surface.navMeshData = data;
        surface.AddData();

        EditorUtility.SetDirty(surface);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        return $"NavMesh baked to {AssetDatabase.GetAssetPath(data)}.";
    }

    static void SetModifier(GameObject go, bool ignore, bool notWalkable)
    {
        var mod = go.GetComponent<NavMeshModifier>();
        if (mod == null)
            mod = Undo.AddComponent<NavMeshModifier>(go);

        mod.ignoreFromBuild = ignore;
        mod.overrideArea = notWalkable;
        if (notWalkable)
            mod.area = 1; // Not Walkable
        mod.applyToChildren = true;
        EditorUtility.SetDirty(mod);
    }
}
