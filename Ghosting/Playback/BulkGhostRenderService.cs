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
    private readonly ILogger<BulkGhostRenderService> _logger;
    private readonly ConfigService _configService;
    private readonly PlayerLoopService _playerLoopService;
    private readonly PlayerLoopSubscription _lateUpdateSubscription;
    private readonly HashSet<Transform> _instances = new();
    private readonly Matrix4x4[] _matrices = new Matrix4x4[BulkGhostBatching.MaximumInstancesPerBatch];

    private readonly List<RenderPart> _renderParts = new();
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

        int count = 0;
        foreach (Transform instance in _instances)
        {
            if (instance == null || !instance.gameObject.activeInHierarchy)
                continue;

            _matrices[count++] = instance.localToWorldMatrix;
            if (count != BulkGhostBatching.MaximumInstancesPerBatch)
                continue;

            DrawBatch(count);
            count = 0;
        }

        if (count > 0)
            DrawBatch(count);
    }

    private void DrawBatch(int count)
    {
        foreach (RenderPart part in _renderParts)
        {
            Graphics.DrawMeshInstanced(
                part.Mesh,
                0,
                part.Material,
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
            var materialGroups = new Dictionary<Material, List<CombineInstance>>();

            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(false))
            {
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled || filter.sharedMesh == null)
                    continue;

                AddSubMeshesByMaterial(
                    materialGroups,
                    filter.sharedMesh,
                    rootInverse * filter.transform.localToWorldMatrix,
                    renderer.sharedMaterials);
            }

            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if (!renderer.enabled || renderer.sharedMesh == null)
                    continue;

                var bakedMesh = new Mesh();
                renderer.BakeMesh(bakedMesh, true);
                bakedMeshes.Add(bakedMesh);

                Matrix4x4 transform = rootInverse * Matrix4x4.TRS(
                    renderer.transform.position,
                    renderer.transform.rotation,
                    Vector3.one);
                Bounds bakedWorldBounds = TransformBounds(
                    bakedMesh.bounds,
                    Matrix4x4.TRS(
                        renderer.transform.position,
                        renderer.transform.rotation,
                        Vector3.one));
                float correction = BulkGhostMeshScale.CalculateUniformScale(
                    MaximumComponent(renderer.bounds.size),
                    MaximumComponent(bakedWorldBounds.size));
                transform *= Matrix4x4.Scale(Vector3.one * correction);

                AddSubMeshesByMaterial(
                    materialGroups,
                    bakedMesh,
                    transform,
                    renderer.sharedMaterials);
            }

            if (materialGroups.Count == 0)
                throw new InvalidOperationException("Bulk ghost template contains no active meshes.");

            int partIndex = 0;
            foreach (KeyValuePair<Material, List<CombineInstance>> materialGroup in materialGroups)
            {
                var mesh = new Mesh
                {
                    name = $"GTR Instanced Bulk Ghost {partIndex++}",
                    indexFormat = IndexFormat.UInt32
                };
                mesh.CombineMeshes(materialGroup.Value.ToArray(), true, true, false);
                mesh.RecalculateBounds();

                var material = new Material(materialGroup.Key)
                {
                    enableInstancing = true,
                    name = $"GTR Instanced {materialGroup.Key.name}"
                };
                _renderParts.Add(new RenderPart(mesh, material));
            }
        }
        finally
        {
            foreach (Mesh bakedMesh in bakedMeshes)
                Object.Destroy(bakedMesh);

            templateRoot.SetActive(false);
            Object.Destroy(templateRoot);
        }
    }

    private static void AddSubMeshesByMaterial(
        IDictionary<Material, List<CombineInstance>> destination,
        Mesh mesh,
        Matrix4x4 transform,
        IReadOnlyList<Material> materials)
    {
        for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
        {
            if (materials.Count == 0)
                continue;

            Material material = materials[Math.Min(subMesh, materials.Count - 1)];
            if (material == null)
                continue;

            if (!destination.TryGetValue(material, out List<CombineInstance> instances))
            {
                instances = new List<CombineInstance>();
                destination.Add(material, instances);
            }

            instances.Add(new CombineInstance
            {
                mesh = mesh,
                subMeshIndex = subMesh,
                transform = transform
            });
        }
    }

    private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
    {
        Vector3 center = matrix.MultiplyPoint3x4(bounds.center);
        Vector3 extents = bounds.extents;
        Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0, 0));
        Vector3 axisY = matrix.MultiplyVector(new Vector3(0, extents.y, 0));
        Vector3 axisZ = matrix.MultiplyVector(new Vector3(0, 0, extents.z));
        extents = new Vector3(
            Math.Abs(axisX.x) + Math.Abs(axisY.x) + Math.Abs(axisZ.x),
            Math.Abs(axisX.y) + Math.Abs(axisY.y) + Math.Abs(axisZ.y),
            Math.Abs(axisX.z) + Math.Abs(axisY.z) + Math.Abs(axisZ.z));
        return new Bounds(center, extents * 2);
    }

    private static float MaximumComponent(Vector3 vector) =>
        Math.Max(vector.x, Math.Max(vector.y, vector.z));

    private void DisposeResources()
    {
        foreach (RenderPart part in _renderParts)
        {
            Object.Destroy(part.Mesh);
            Object.Destroy(part.Material);
        }

        _renderParts.Clear();
        _initialized = false;
    }

    public void Dispose()
    {
        _playerLoopService.UnsubscribeLateUpdate(_lateUpdateSubscription);
        _instances.Clear();
        DisposeResources();
    }

    private sealed class RenderPart
    {
        public RenderPart(Mesh mesh, Material material)
        {
            Mesh = mesh;
            Material = material;
        }

        public Mesh Mesh { get; }
        public Material Material { get; }
    }
}
