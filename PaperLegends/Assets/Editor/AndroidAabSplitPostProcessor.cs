using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

public sealed class AndroidAabSplitPostProcessor : IPostGenerateGradleAndroidProject
{
    // Run after most default processors and third-party processors.
    public int callbackOrder => 1000;

    private const string AbiFiltersLine = "abiFilters \"armeabi-v7a\", \"arm64-v8a\"";

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        var unityLibraryDir = new DirectoryInfo(path);
        if (!unityLibraryDir.Exists || unityLibraryDir.Parent == null)
            return;

        string gradleRoot = unityLibraryDir.Parent.FullName;
        string launcherGradle = Path.Combine(gradleRoot, "launcher", "build.gradle");
        string unityLibraryGradle = Path.Combine(gradleRoot, "unityLibrary", "build.gradle");

        PatchAbiFilters(unityLibraryGradle);
        PatchAbiFilters(launcherGradle);
        PatchLauncherBundleSplits(launcherGradle);
    }

    private static void PatchAbiFilters(string gradleFile)
    {
        if (!File.Exists(gradleFile))
            return;

        string content = File.ReadAllText(gradleFile);
        string updated = Regex.Replace(
            content,
            @"(?m)^\s*abiFilters\s+.*$",
            match =>
            {
                string indent = Regex.Match(match.Value, @"^\s*").Value;
                return indent + AbiFiltersLine;
            });

        if (!ReferenceEquals(content, updated) && content != updated)
        {
            File.WriteAllText(gradleFile, updated);
            Debug.Log($"[AndroidAabSplitPostProcessor] Updated abiFilters in: {gradleFile}");
        }
    }

    private static void PatchLauncherBundleSplits(string launcherGradle)
    {
        if (!File.Exists(launcherGradle))
            return;

        string content = File.ReadAllText(launcherGradle);
        string updated = content;

        // Force Play delivery split behavior for app bundles.
        updated = Regex.Replace(updated, @"(?ms)(bundle\s*\{.*?language\s*\{[^}]*enableSplit\s*=\s*)(true|false)", "$1true");
        updated = Regex.Replace(updated, @"(?ms)(bundle\s*\{.*?density\s*\{[^}]*enableSplit\s*=\s*)(true|false)", "$1true");
        updated = Regex.Replace(updated, @"(?ms)(bundle\s*\{.*?abi\s*\{[^}]*enableSplit\s*=\s*)(true|false)", "$1true");

        if (content != updated)
        {
            File.WriteAllText(launcherGradle, updated);
            Debug.Log($"[AndroidAabSplitPostProcessor] Updated bundle splits in: {launcherGradle}");
        }
        else if (!Regex.IsMatch(content, @"(?ms)bundle\s*\{.*?abi\s*\{[^}]*enableSplit\s*=\s*true"))
        {
            Debug.LogWarning("[AndroidAabSplitPostProcessor] launcher/build.gradle does not contain expected bundle block. Please verify Gradle template manually.");
        }
    }
}
