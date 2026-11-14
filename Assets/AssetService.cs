using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Assets;

public class AssetService : IDisposable
{
    private readonly PluginInfo _info;
    private readonly ILogger<AssetService> _logger;

    private AssetBundle _shaderAssetBundle;
    private readonly Dictionary<string, Shader> _shaders = new();
    private readonly Dictionary<int, Sprite> _medals = new();
    private static readonly string[] MedalNames = { "Bronze", "Silver", "Gold", "Author", "Expert", "Apex" };

    public AssetService(PluginInfo info, ILogger<AssetService> logger)
    {
        _info = info;
        _logger = logger;

        LoadAssetBundle("gtr-shaders", assetBundle => _shaderAssetBundle = assetBundle, false);
    }

    public Sprite GetMedalSprite(int tier)
    {
        if (tier < 0 || tier >= MedalNames.Length) return null;
        if (_medals.TryGetValue(tier, out Sprite cached)) return cached;
        Texture2D texture = null;
        try
        {
            string name = $"TNRD.Zeepkist.GTR.RuntimeAssets.Medals.MED_{MedalNames[tier]}.png";
            using Stream stream = typeof(AssetService).Assembly.GetManifestResourceStream(name);
            if (stream == null) throw new FileNotFoundException("Embedded medal missing", name);
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.name = $"GTR Medal {MedalNames[tier]}";
            if (!ImageConversion.LoadImage(texture, buffer.ToArray()))
                throw new InvalidDataException("Embedded medal PNG could not be decoded");
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            // PNG compression saves disk space only. Compress alpha-bearing GPU data separately.
            if (SystemInfo.SupportsTextureFormat(TextureFormat.DXT5))
            {
                texture.Compress(true);
                if (texture.format != TextureFormat.DXT5)
                    _logger.LogWarning("Medal GPU compression unavailable; retaining {Format}", texture.format);
            }
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
            _medals[tier] = sprite;
            return sprite;
        }
        catch (Exception e)
        {
            if (texture != null) UnityEngine.Object.Destroy(texture);
            _logger.LogWarning(e, "Unable to load medal {Medal}", MedalNames[tier]);
            _medals[tier] = null;
            return null;
        }
    }

    public void Dispose()
    {
        foreach (Sprite sprite in _medals.Values)
        {
            if (sprite == null) continue;
            UnityEngine.Object.Destroy(sprite.texture);
            UnityEngine.Object.Destroy(sprite);
        }
        _medals.Clear();
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
}
