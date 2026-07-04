[CmdletBinding()]
param(
    [string]$UnityPath,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$CopyToSideload
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$bundleName = "gtr-shaders"
$projectPath = Join-Path $PSScriptRoot ".tmp\gtr-shader-bundle-project"
$projectAssets = Join-Path $projectPath "Assets"
$projectShaders = Join-Path $projectAssets "Shaders"
$projectEditor = Join-Path $projectAssets "Editor"
$sourceShaders = Join-Path $PSScriptRoot "Shaders"
$bundleOutputDirectory = $sourceShaders
$bundleOutputPath = Join-Path $bundleOutputDirectory $bundleName
$buildOutputDirectory = Join-Path $PSScriptRoot "bin\$Configuration\net472"
$buildOutputPath = Join-Path $buildOutputDirectory $bundleName
$sideloadDirectory = "D:\SteamLibrary\steamapps\common\Zeepkist\BepInEx\plugins\Sideloaded\Plugins"

function Find-Unity {
    if ($UnityPath) {
        if (-not (Test-Path -LiteralPath $UnityPath)) {
            throw "UnityPath not found: $UnityPath"
        }

        return $UnityPath
    }

    $hubRoot = "C:\Program Files\Unity\Hub\Editor"
    if (-not (Test-Path -LiteralPath $hubRoot)) {
        throw "Unity Hub editor folder not found. Pass -UnityPath explicitly."
    }

    $unity = Get-ChildItem -LiteralPath $hubRoot -Directory |
        Where-Object { $_.Name -like "2021.*" } |
        Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName "Editor\Unity.exe" } |
        Where-Object { Test-Path -LiteralPath $_ } |
        Select-Object -First 1

    if (-not $unity) {
        throw "Unity 2021 editor not found. Pass -UnityPath explicitly."
    }

    return $unity
}

function Invoke-UnityBuild {
    param(
        [string]$UnityExe,
        [switch]$AllowFailure
    )

    & $UnityExe `
        -batchmode `
        -quit `
        -nographics `
        -projectPath $projectPath `
        -executeMethod BuildGtrShaderAssetBundle.Build `
        -logFile (Join-Path $projectPath "Logs\build-gtr-shaders.log")

    if ($LASTEXITCODE -ne 0 -and -not $AllowFailure) {
        throw "Unity shader bundle build failed with exit code $LASTEXITCODE."
    }

    return $LASTEXITCODE
}

if (-not (Test-Path -LiteralPath $sourceShaders)) {
    throw "Shader source folder not found: $sourceShaders"
}

$shaderFiles = Get-ChildItem -LiteralPath $sourceShaders -Filter "*.shader" -File
if ($shaderFiles.Count -eq 0) {
    throw "No shader files found in $sourceShaders"
}

New-Item -ItemType Directory -Force -Path $projectShaders, $projectEditor, $bundleOutputDirectory | Out-Null

foreach ($shaderFile in $shaderFiles) {
    Copy-Item -LiteralPath $shaderFile.FullName -Destination (Join-Path $projectShaders $shaderFile.Name) -Force
}

@'
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class BuildGtrShaderAssetBundle
{
    public static void Build()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string repoRoot = Directory.GetParent(Directory.GetParent(projectRoot).FullName).FullName;
        string outputPath = Path.Combine(repoRoot, "Shaders");
        Directory.CreateDirectory(outputPath);

        string[] shaderFiles = Directory.GetFiles(Path.Combine(Application.dataPath, "Shaders"), "*.shader");
        var builds = new AssetBundleBuild[1];
        builds[0].assetBundleName = "gtr-shaders";
        builds[0].assetNames = new string[shaderFiles.Length];
        builds[0].addressableNames = new string[shaderFiles.Length];

        for (int i = 0; i < shaderFiles.Length; i++)
        {
            string assetPath = "Assets/Shaders/" + Path.GetFileName(shaderFiles[i]);
            builds[0].assetNames[i] = assetPath;
            builds[0].addressableNames[i] = ReadShaderName(shaderFiles[i]);
        }

        BuildPipeline.BuildAssetBundles(
            outputPath,
            builds,
            BuildAssetBundleOptions.ForceRebuildAssetBundle,
            EditorUserBuildSettings.activeBuildTarget);
    }

    private static string ReadShaderName(string shaderFile)
    {
        string text = File.ReadAllText(shaderFile);
        Match match = Regex.Match(text, "Shader\\s+\\\"(?<name>[^\\\"]+)\\\"");
        if (!match.Success)
            throw new InvalidOperationException("Unable to read shader name from " + shaderFile);

        return match.Groups["name"].Value;
    }
}
'@ | Set-Content -LiteralPath (Join-Path $projectEditor "BuildGtrShaderAssetBundle.cs") -Encoding UTF8

$unityExe = Find-Unity
$exitCode = Invoke-UnityBuild -UnityExe $unityExe -AllowFailure

if ($exitCode -ne 0 -or -not (Test-Path -LiteralPath $bundleOutputPath)) {
    Invoke-UnityBuild -UnityExe $unityExe
}

if (-not (Test-Path -LiteralPath $bundleOutputPath)) {
    throw "Unity build completed but bundle was not found: $bundleOutputPath"
}

New-Item -ItemType Directory -Force -Path $buildOutputDirectory | Out-Null
Copy-Item -LiteralPath $bundleOutputPath -Destination $buildOutputPath -Force

if ($CopyToSideload) {
    New-Item -ItemType Directory -Force -Path $sideloadDirectory | Out-Null
    Copy-Item -LiteralPath $bundleOutputPath -Destination (Join-Path $sideloadDirectory $bundleName) -Force
}

Write-Host "Built shader asset bundle: $bundleOutputPath"
Write-Host "Copied shader asset bundle: $buildOutputPath"
