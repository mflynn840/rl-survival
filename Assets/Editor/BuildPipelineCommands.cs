using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEngine;
using System;
using System.Text.RegularExpressions;

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
            var configured=System.Environment.GetEnvironmentVariable("RLSURVIVAL_WEBGL_OUTPUT");
            var output=Path.GetFullPath(string.IsNullOrEmpty(configured)?Path.Combine(Application.dataPath,"..","Builds","WebGL"):configured);
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/Main.unity" }, output, BuildTarget.WebGL, BuildOptions.None);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"WebGL build failed: {report.summary.result}; errors={report.summary.totalErrors}.");
            var stylePath=Path.Combine(output,"TemplateData","style.css");var style=File.ReadAllText(stylePath);
            const string oldResponsive="html,body{width:100%;height:100%;margin:0;overflow:hidden;background:#171e1d}#unity-container.unity-desktop{position:fixed;inset:0;width:100%;height:100%;left:0;top:0;transform:none}#unity-canvas{display:block;width:100%!important;height:100%!important;background:#171e1d}.unity-desktop #unity-footer{display:none}#unity-warning{z-index:5}";
            style=style.Replace(oldResponsive,string.Empty);
            File.WriteAllText(stylePath,style+"\n/* RL SURVIVAL RESPONSIVE START */\nhtml,body{width:100%;height:100%;margin:0;overflow:hidden;background:#171e1d}#unity-container.unity-desktop{position:fixed;inset:0;width:100%;height:100%;left:0;top:0;transform:none}#unity-canvas{display:block;width:100%!important;height:100%!important;background:#171e1d}.unity-desktop #unity-footer{display:none}#unity-warning{z-index:5}\n/* RL SURVIVAL RESPONSIVE END */\n");
            var index=Path.Combine(output,"index.html");var html=File.ReadAllText(index);
            html=Regex.Replace(html,"<meta name=\"viewport\"[^>]*>",string.Empty,RegexOptions.IgnoreCase);
            html=html.Replace("<meta charset=\"utf-8\">","<meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,height=device-height,initial-scale=1,maximum-scale=1,user-scalable=no\">");
            html=html.Replace("var container = document.querySelector(\"#unity-container\");","window.__unityReady=false;\n      window.__unityInstance=null;\n      var container = document.querySelector(\"#unity-container\");");
            html=html.Replace("loadingBar.style.display = \"none\";","loadingBar.style.display = \"none\";\n                window.__unityInstance=unityInstance;\n                window.__unityReady=true;");
            File.WriteAllText(index,html);
            Debug.Log($"RL Survival WebGL player built at {output} ({report.summary.totalSize} bytes).");
        }
    }
}
