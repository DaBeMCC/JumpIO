using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Builds de entrega. Cada destino se compila en su carpeta y se comprime en un .zip listo para subir.
public static class BuildScript
{
    const string Root = "Builds";

    [MenuItem("JumpIO/Build/WebGL")]
    public static void WebGL()
    {
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        // Gzip con descompresión de respaldo: funciona aunque el servidor no envíe la cabecera Content-Encoding (por ejemplo itch.io o un servidor local).
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        Build(BuildTarget.WebGL, "WebGL", "");
    }

    [MenuItem("JumpIO/Build/Windows")]
    public static void Windows() => Build(BuildTarget.StandaloneWindows64, "Windows", "JumpIO.exe");

    [MenuItem("JumpIO/Build/Linux")]
    public static void Linux() => Build(BuildTarget.StandaloneLinux64, "Linux", "JumpIO.x86_64");

    [MenuItem("JumpIO/Build/All")]
    public static void All()
    {
        WebGL();
        Windows();
        Linux();
    }

    static void Build(BuildTarget target, string name, string executable)
    {
        PlayerSettings.companyName = "DaBeMCC";
        PlayerSettings.productName = "JumpIO";
        PlayerSettings.runInBackground = true;
        AssetDatabase.SaveAssets();

        string folder = Path.Combine(Root, name);
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = executable.Length > 0 ? Path.Combine(folder, executable) : folder,
            target = target,
            options = BuildOptions.None,
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        string zip = Path.Combine(Root, "JumpIO_" + name + ".zip");
        if (summary.result == BuildResult.Succeeded)
        {
            // Los símbolos de depuración no hacen falta para jugar y casi duplican el tamaño del .zip.
            foreach (var debug in Directory.GetDirectories(folder, "*DontShip*")) Directory.Delete(debug, true);
            if (File.Exists(zip)) File.Delete(zip);
            ZipFile.CreateFromDirectory(folder, zip, System.IO.Compression.CompressionLevel.Optimal, false);
        }
        Debug.Log($"JUMPIO_BUILD {summary.result} target={name} scenes={options.scenes.Length} " +
                  $"size={summary.totalSize / (1024f * 1024f):0.0}MB time={summary.totalTime.TotalSeconds:0}s " +
                  $"errors={summary.totalErrors} warnings={summary.totalWarnings} zip={zip}");
        if (Application.isBatchMode && summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
