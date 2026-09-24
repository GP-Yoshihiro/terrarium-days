using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace TerrariumDays.Editor
{
    /// <summary>
    /// CLI entry points. Keep this class free of gameplay behavior.
    /// </summary>
    public static class BuildAutomation
    {
        public static void BuildIosDevelopment()
        {
            var outputPath = GetRequiredArgument("-buildOutput");

            // For BuildTarget.iOS, locationPathName is a directory Unity writes an
            // Xcode project into (not a runnable binary) — Windows can generate the
            // project but cannot compile/sign/install it; that final step needs Xcode
            // on macOS. Ensure the directory itself exists, not its parent.
            Directory.CreateDirectory(outputPath);

            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0)
            {
                throw new BuildFailedException("No scenes are configured in Editor Build Settings.");
            }

            var enabledScenePaths = Array.FindAll(scenes, scene => scene.enabled);
            if (enabledScenePaths.Length == 0)
            {
                throw new BuildFailedException("No enabled scenes are configured in Editor Build Settings.");
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Array.ConvertAll(enabledScenePaths, scene => scene.path),
                locationPathName = outputPath,
                target = BuildTarget.iOS,
                options = BuildOptions.Development
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"iOS build failed: {report.summary.result}");
            }
        }

        private static string GetRequiredArgument(string argumentName)
        {
            var arguments = Environment.GetCommandLineArgs();
            var argumentIndex = Array.IndexOf(arguments, argumentName);

            if (argumentIndex < 0 || argumentIndex == arguments.Length - 1)
            {
                throw new ArgumentException($"Missing required command-line argument: {argumentName}");
            }

            return arguments[argumentIndex + 1];
        }
    }
}
