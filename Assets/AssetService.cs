using System;
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

    public AssetService(PluginInfo info, ILogger<AssetService> logger)
    {
        _info = info;
        _logger = logger;

        if (!LoadUIAssembly())
            return;

        if (!LoadAssetBundle())
            return;
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

    private bool LoadAssetBundle()
    {
        string dir = Path.GetDirectoryName(_info.Location);
        string assetBundlePath = dir + "/gtr-ui";
        try
        {
            _assetBundle = AssetBundle.LoadFromFile(assetBundlePath);
            return _assetBundle != null;
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, "Unable to load asset bundle");
            return false;
        }
    }

    // TODO: Add methods for loading UI from asset bundle
}
