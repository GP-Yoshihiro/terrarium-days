using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using TerrariumDays.UI;

namespace TerrariumDays.Editor
{
    /// <summary>
    /// Idempotent wiring of the Terrarium screen's UIDocument into the Terrarium scene.
    /// Invoke from Unity CLI after import, same pattern as ProjectBootstrap.
    /// </summary>
    public static class TerrariumSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Terrarium.unity";
        private const string PanelSettingsPath = "Assets/UI/TerrariumPanelSettings.asset";
        private const string VisualTreeAssetPath = "Assets/UI/Terrarium.uxml";
        private const string RootObjectName = "TerrariumUI";

        public static void Configure()
        {
            ProjectBootstrap.Configure();
            EnsurePanelSettings();

            // Open the scene before loading the assets that get assigned into it: a
            // C# reference to a ScriptableObject asset loaded before an OpenScene call
            // can turn into a Unity "fake null" once the scene switch runs, even though
            // the asset itself is untouched on disk. Loading after OpenScene avoids that.
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(VisualTreeAssetPath);

            var root = GameObject.Find(RootObjectName);
            if (root == null)
            {
                root = new GameObject(RootObjectName);
            }

            var uiDocument = root.GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                uiDocument = root.AddComponent<UIDocument>();
            }

            uiDocument.panelSettings = panelSettings;
            uiDocument.visualTreeAsset = visualTreeAsset;

            if (root.GetComponent<TerrariumView>() == null)
            {
                root.AddComponent<TerrariumView>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsurePanelSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null)
            {
                return;
            }

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            // iPhone logical points (not pixels), so 1 USS px ≈ 1 pt and 44px meets the
            // minimum touch target. 1080x1920 made every control roughly a third too small.
            panelSettings.referenceResolution = new Vector2Int(390, 844);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;

            AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
