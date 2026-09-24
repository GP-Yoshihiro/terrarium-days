using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TerrariumDays.Editor
{
    /// <summary>
    /// Idempotent setup for the empty Unity project. Invoke from Unity CLI after import.
    /// </summary>
    public static class ProjectBootstrap
    {
        private const string ScenePath = "Assets/Scenes/Terrarium.unity";

        public static void Configure()
        {
            EnsureFolders();

            PlayerSettings.companyName = "Terrarium Days Studio";
            PlayerSettings.productName = "Terrarium Days";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.terrariumdays.prototype");

            // iOS has no Mono option (it is always IL2CPP); set it explicitly so the
            // intended backend is documented here rather than left to the platform default.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;

            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("TerrariumBootstrap");
                root.AddComponent<Camera>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Scripts/Core");
            EnsureFolder("Assets/Scripts/Gameplay");
            EnsureFolder("Assets/Scripts/UI");
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Tests/EditMode");
            EnsureFolder("Assets/Tests/PlayMode");
        }

        private static void EnsureFolder(string assetPath)
        {
            var segments = assetPath.Split('/');
            var currentPath = segments[0];

            for (var index = 1; index < segments.Length; index++)
            {
                var nextPath = $"{currentPath}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segments[index]);
                }

                currentPath = nextPath;
            }
        }
    }
}
