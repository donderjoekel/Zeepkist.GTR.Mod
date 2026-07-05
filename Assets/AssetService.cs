using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Assets;

public class AssetService
{
    private readonly PluginInfo _info;
    private readonly ILogger<AssetService> _logger;

    private AssetBundle _assetBundle;
    private AssetBundle _shaderAssetBundle;
    private readonly Dictionary<string, Shader> _shaders = new();

    public AssetService(PluginInfo info, ILogger<AssetService> logger)
    {
        _info = info;
        _logger = logger;

        if (LoadUIAssembly())
            LoadAssetBundle("gtr-ui", assetBundle => _assetBundle = assetBundle, true);

        LoadAssetBundle("gtr-shaders", assetBundle => _shaderAssetBundle = assetBundle, false);
    }

    private bool LoadUIAssembly()
    {
        string dir = Path.GetDirectoryName(_info.Location);
        string assemblyPath = dir + "/TNRD.Zeepkist.GTR.UI.dll";
        try
        {
            Assembly asm = Assembly.LoadFile(assemblyPath);
            return asm != null;
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "Unable to load UI assembly");
            return false;
        }
    }

    private bool LoadAssetBundle(
        string assetBundleName,
        Action<AssetBundle> assign,
        bool required)
    {
        string dir = Path.GetDirectoryName(_info.Location);
        string assetBundlePath = Path.Combine(dir, assetBundleName);
        try
        {
            AssetBundle assetBundle = AssetBundle.LoadFromFile(assetBundlePath);
            if (assetBundle == null)
            {
                LogAssetBundleFailure(assetBundleName, required, null);
                return false;
            }

            assign(assetBundle);
            if (assetBundleName == "gtr-shaders")
                CacheShaders(assetBundle);

            return true;
        }
        catch (Exception e)
        {
            LogAssetBundleFailure(assetBundleName, required, e);
            return false;
        }
    }

    public Shader GetShader(string shaderName)
    {
        if (_shaderAssetBundle == null || string.IsNullOrWhiteSpace(shaderName))
            return null;

        if (_shaders.TryGetValue(shaderName, out Shader shader))
            return shader;

        string prefixedShaderName = $"GTR_{shaderName}";
        if (_shaders.TryGetValue(prefixedShaderName, out shader))
            return shader;

        _logger.LogWarning("Shader {ShaderName} was not found in gtr-shaders asset bundle", shaderName);
        return null;
    }

    private void CacheShaders(AssetBundle assetBundle)
    {
        foreach (Shader shader in assetBundle.LoadAllAssets<Shader>())
        {
            if (shader == null || string.IsNullOrWhiteSpace(shader.name))
                continue;

            _shaders[shader.name] = shader;
            _logger.LogDebug("Loaded bundled shader {Shader}", shader.name);
        }

        _logger.LogInformation("Loaded {Count} shaders from gtr-shaders asset bundle", _shaders.Count);
    }

    private void LogAssetBundleFailure(string assetBundleName, bool required, Exception exception)
    {
        if (required)
        {
            if (exception != null)
                _logger.LogCritical(exception, "Unable to load asset bundle {AssetBundle}", assetBundleName);
            else
                _logger.LogCritical("Unable to load asset bundle {AssetBundle}", assetBundleName);
            return;
        }

        if (exception != null)
            _logger.LogWarning(exception, "Unable to load optional asset bundle {AssetBundle}", assetBundleName);
        else
            _logger.LogWarning("Unable to load optional asset bundle {AssetBundle}", assetBundleName);
    }

    // TODO: Add methods for loading UI from asset bundle
}
