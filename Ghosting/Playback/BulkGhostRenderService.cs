using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.PlayerLoop;
using UnityEngine;
using UnityEngine.Rendering;
using ZeepSDK.Utilities;
using Object = UnityEngine.Object;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public sealed class BulkGhostRenderService : IEagerService, IDisposable
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private readonly ILogger<BulkGhostRenderService> _logger;
    private readonly ConfigService _configService;
    private readonly PlayerLoopService _playerLoopService;
    private readonly PlayerLoopSubscription _lateUpdateSubscription;
    private readonly HashSet<Transform> _instances = new();
    private readonly Matrix4x4[] _matrices = new Matrix4x4[BulkGhostBatching.MaximumInstancesPerBatch];

    private Mesh _mesh;
    private Material _opaqueMaterial;
    private Material _transparentMaterial;
    private bool _initializationAttempted;
    private bool _initialized;

    public BulkGhostRenderService(
        ILogger<BulkGhostRenderService> logger,
        ConfigService configService,
        PlayerLoopService playerLoopService)
    {
        _logger = logger;
        _configService = configService;
        _playerLoopService = playerLoopService;
        _lateUpdateSubscription = playerLoopService.SubscribeLateUpdate(Draw);
    }

    public bool CanUseInstancing()
    {
        if (_initializationAttempted)
            return _initialized;

        _initializationAttempted = true;
        if (!SystemInfo.supportsInstancing)
        {
            _logger.LogWarning("GPU instancing is unavailable. Falling back to bulk ghost renderers.");
            return false;
        }

        try
        {
            InitializeResources();
            _initialized = true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to initialize instanced bulk ghost rendering. Falling back.");
            DisposeResources();
        }

        return _initialized;
    }

    public void Register(Transform transform)
    {
        if (transform != null)
            _instances.Add(transform);
    }

    public void Unregister(Transform transform)
    {
        if (transform != null)
            _instances.Remove(transform);
    }

    private void Draw()
    {
        if (!_initialized ||
            !_configService.ShowGhosts.Value ||
            !_configService.ShowGlobalPersonalBest.Value)
        {
            return;
        }

        Material material = _configService.ShowGhostTransparent.Value
            ? _transparentMaterial
            : _opaqueMaterial;

        int count = 0;
        foreach (Transform instance in _instances)
        {
            if (instance == null || !instance.gameObject.activeInHierarchy)
                continue;

            _matrices[count++] = instance.localToWorldMatrix;
            if (count != BulkGhostBatching.MaximumInstancesPerBatch)
                continue;

            DrawBatch(material, count);
            count = 0;
        }

        if (count > 0)
            DrawBatch(material, count);
    }

    private void DrawBatch(Material material, int count)
    {
        Graphics.DrawMeshInstanced(
            _mesh,
            0,
            material,
            _matrices,
            count,
            null,
            ShadowCastingMode.Off,
            false,
            0,
            null,
            LightProbeUsage.Off,
            null);
    }

    private void InitializeResources()
    {
        GameObject templateRoot = new("GTR Bulk Ghost Mesh Template");
        var bakedMeshes = new List<Mesh>();

        try
        {
            SetupModelCar model = Object.Instantiate(
                ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab.ghostModel,
                templateRoot.transform);
            GhostVisuals.ConfigureBulkModel(model);

            Matrix4x4 rootInverse = templateRoot.transform.worldToLocalMatrix;
            var combineInstances = new List<CombineInstance>();

            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(false))
            {
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled || filter.sharedMesh == null)
                    continue;

                AddSubMeshes(
                    combineInstances,
                    filter.sharedMesh,
                    rootInverse * filter.transform.localToWorldMatrix);
            }

            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if (!renderer.enabled || renderer.sharedMesh == null)
                    continue;

                var bakedMesh = new Mesh();
                renderer.BakeMesh(bakedMesh);
                bakedMeshes.Add(bakedMesh);
                AddSubMeshes(
                    combineInstances,
                    bakedMesh,
                    rootInverse * renderer.transform.localToWorldMatrix);
            }

            if (combineInstances.Count == 0)
                throw new InvalidOperationException("Bulk ghost template contains no active meshes.");

            _mesh = new Mesh
            {
                name = "GTR Instanced Bulk Ghost",
                indexFormat = IndexFormat.UInt32
            };
            _mesh.CombineMeshes(combineInstances.ToArray(), true, true, false);
            _mesh.RecalculateBounds();

            Material source =
                ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab.ghostFader.fadeThisMaterial;
            _opaqueMaterial = CreateMaterial(source, 1f);
            _transparentMaterial = CreateMaterial(source, 0.3f);
        }
        finally
        {
            foreach (Mesh bakedMesh in bakedMeshes)
                Object.Destroy(bakedMesh);

            templateRoot.SetActive(false);
            Object.Destroy(templateRoot);
        }
    }

    private static void AddSubMeshes(
        ICollection<CombineInstance> destination,
        Mesh mesh,
        Matrix4x4 transform)
    {
        for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
        {
            destination.Add(new CombineInstance
            {
                mesh = mesh,
                subMeshIndex = subMesh,
                transform = transform
            });
        }
    }

    private static Material CreateMaterial(Material source, float alpha)
    {
        var material = new Material(source)
        {
            enableInstancing = true
        };

        if (material.HasProperty(ColorId))
            material.color = Color.white with { a = alpha };

        return material;
    }

    private void DisposeResources()
    {
        if (_mesh != null)
            Object.Destroy(_mesh);
        if (_opaqueMaterial != null)
            Object.Destroy(_opaqueMaterial);
        if (_transparentMaterial != null)
            Object.Destroy(_transparentMaterial);

        _mesh = null;
        _opaqueMaterial = null;
        _transparentMaterial = null;
        _initialized = false;
    }

    public void Dispose()
    {
        _playerLoopService.UnsubscribeLateUpdate(_lateUpdateSubscription);
        _instances.Clear();
        DisposeResources();
    }
}
