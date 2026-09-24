#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace TerrariumDays.Editor
{
    /// <summary>
    /// Patches the generated Xcode project so it builds with Xcode 26 without manual edits.
    /// </summary>
    public static class IosXcodePostProcess
    {
        [PostProcessBuild(1000)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            var projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            var targetGuids = new[]
            {
                project.ProjectGuid(),
                project.GetUnityMainTargetGuid(),
                project.GetUnityFrameworkTargetGuid(),
            };

            foreach (var guid in targetGuids)
            {
                // The IL2CPP "GameAssembly" run-script phase reads and writes outside the
                // declared inputs, which Xcode's user script sandbox blocks.
                project.SetBuildProperty(guid, "ENABLE_USER_SCRIPT_SANDBOXING", "NO");
                // Unity's framework headers are not modular; the verifier rejects them.
                project.SetBuildProperty(guid, "ENABLE_MODULE_VERIFIER", "NO");
            }

            File.WriteAllText(projectPath, project.WriteToString());
        }
    }
}
#endif
