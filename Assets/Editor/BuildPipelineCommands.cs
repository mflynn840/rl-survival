using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEngine;

namespace RLSurvival.Editor
{
    public static class BuildPipelineCommands
    {
        public static void BuildLinux()
        {
            var output = Path.Combine(Application.dataPath, "..", "Builds", "Linux", "RL Survival.x86_64");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/Main.unity" }, output, BuildTarget.StandaloneLinux64, BuildOptions.None);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Linux player build failed: {report.summary.result}; errors={report.summary.totalErrors}.");
            Debug.Log($"RL Survival Linux player built at {output} ({report.summary.totalSize} bytes).");
        }

        public static void BuildWebGL()
        {
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "WebGL"));
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/Main.unity" }, output, BuildTarget.WebGL, BuildOptions.None);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"WebGL build failed: {report.summary.result}; errors={report.summary.totalErrors}.");
            Debug.Log($"RL Survival WebGL player built at {output} ({report.summary.totalSize} bytes).");
        }
    }
}
