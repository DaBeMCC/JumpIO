using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildScript
{
    const string WebGLOutput = "Builds/WebGL";

    [MenuItem("JumpIO/Build/WebGL")]
    public static void WebGL()
    {
        PlayerSettings.companyName = "DaBeMCC";
        PlayerSettings.productName = "JumpIO";
        PlayerSettings.runInBackground = true;
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        AssetDatabase.SaveAssets();

        if (Directory.Exists(WebGLOutput)) Directory.Delete(WebGLOutput, true);
        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = WebGLOutput,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        Debug.Log($"JUMPIO_BUILD {summary.result} target=WebGL scenes={options.scenes.Length} " +
                  $"size={summary.totalSize / (1024f * 1024f):0.0}MB time={summary.totalTime.TotalSeconds:0}s " +
                  $"errors={summary.totalErrors} warnings={summary.totalWarnings}");
        if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
