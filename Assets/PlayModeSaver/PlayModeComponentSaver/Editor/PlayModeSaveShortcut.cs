using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using System.Collections.Generic;
using Framework.Utils.Editor;

[InitializeOnLoad]
public class PlayModeSaveShortcut : Editor
{
    private static HashSet<int> existingObjectIds = new HashSet<int>();
    private static bool initialized = false;

    static PlayModeSaveShortcut()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            // Storeing all existing object IDs when entering play mode
            StoreExistingObjects();
            initialized = true;
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            initialized = false;
            existingObjectIds.Clear();
        }
    }

    private static void StoreExistingObjects()
    {
        existingObjectIds.Clear();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded)
            {
                GameObject[] rootObjects = scene.GetRootGameObjects();
                foreach (GameObject root in rootObjects)
                {
                    // Store the ID of this object and all its children
                    StoreGameObjectAndChildren(root);
                }
            }
        }
    }

    private static void StoreGameObjectAndChildren(GameObject obj)
    {
        existingObjectIds.Add(obj.GetInstanceID());
        foreach (Transform child in obj.transform)
        {
            StoreGameObjectAndChildren(child.gameObject);
        }
    }

    [MenuItem("Tools/Save All New Objects #o")] // Shift + O
    public static void SaveAllNewObjects()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("This can only be used in Play Mode");
            return;
        }

        if (!initialized)
        {
            Debug.LogWarning("Object tracking not initialized. All objects will be considered new.");
        }

        int savedCount = 0;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded)
            {
                GameObject[] rootObjects = scene.GetRootGameObjects();
                foreach (GameObject root in rootObjects)
                {
                    savedCount += SaveNewObjectsInHierarchy(root);
                }
            }
        }

        if (savedCount > 0)
        {
            Debug.Log($"Saved {savedCount} new objects");
            UnityPlayModeSaverWindow.Open(false);
        }
        else
        {
            Debug.Log("No new objects found to save");
        }
    }

    private static int SaveNewObjectsInHierarchy(GameObject obj)
    {
        int savedCount = 0;

        // Check if this is a new object
        if (!existingObjectIds.Contains(obj.GetInstanceID()))
        {
            // Only save if not already registered
            if (!UnityPlayModeSaver.IsObjectRegistered(obj, out _))
            {
                UnityPlayModeSaver.RegisterSavedObject(obj);
                savedCount++;
            }
        }

        // Process children
        foreach (Transform child in obj.transform)
        {
            savedCount += SaveNewObjectsInHierarchy(child.gameObject);
        }

        return savedCount;
    }

    // If we want to save entire hierarchy regardless if they are newly created or not
    [MenuItem("Tools/Save All Objects #s")] // Shift + S
    private static void SaveAllObjects()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("This can only be used in Play Mode");
            return;
        }

        int savedCount = 0;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded)
            {
                GameObject[] rootObjects = scene.GetRootGameObjects();
                foreach (GameObject root in rootObjects)
                {
                    savedCount += SaveEntireHierarchy(root);
                }
            }
        }

        if (savedCount > 0)
        {
            Debug.Log($"Saved {savedCount} objects");
            UnityPlayModeSaverWindow.Open(false);
        }
        else
        {
            Debug.Log("No objects found to save");
        }
    }

    private static int SaveEntireHierarchy(GameObject obj)
    {
        int savedCount = 0;

        // Save this object if not already registered
        if (!UnityPlayModeSaver.IsObjectRegistered(obj, out _))
        {
            UnityPlayModeSaver.RegisterSavedObject(obj);
            savedCount++;
        }

        // Save all children
        foreach (Transform child in obj.transform)
        {
            savedCount += SaveEntireHierarchy(child.gameObject);
        }

        return savedCount;
    }

    // This method is called from PlayModeManuelSaver
    public void SaveObjectFromRuntime(GameObject obj)
    {
        if (!Application.isPlaying)
            return;

        if (!UnityPlayModeSaver.IsObjectRegistered(obj, out _))
        {
            UnityPlayModeSaver.RegisterSavedObject(obj);
            UnityPlayModeSaverWindow.Open(false);
            Debug.Log($"Successfully saved object: {obj.name}");
        }
    }

    // calls manuelsaver left controller Y button press
    public static void SaveObjectFromRuntimeStatic(GameObject obj)
    {
        if (!Application.isPlaying)
            return;

        if (!UnityPlayModeSaver.IsObjectRegistered(obj, out _))
        {
            UnityPlayModeSaver.RegisterSavedObject(obj);
            UnityPlayModeSaverWindow.Open(false);
            Debug.Log($"Successfully saved object: {obj.name}");
        }
    }
}