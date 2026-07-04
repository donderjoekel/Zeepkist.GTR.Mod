using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.PlayerLoop;
using UnityEngine;
using UnityEngine.Rendering;
using ZeepSDK.Utilities;
using Object = UnityEngine.Object;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public sealed class BulkGhostRenderService : IEagerService
{
    private readonly ILogger<BulkGhostRenderService> _logger;
    private readonly ConfigService _configService;
    private readonly HashSet<Transform> _instances = new();
    private readonly Dictionary<GhostCharacterPlaybackPose, HashSet<Transform>> _characterInstances = new()
    {
        [GhostCharacterPlaybackPose.Seated] = new HashSet<Transform>(),
        [GhostCharacterPlaybackPose.SeatedArmsUp] = new HashSet<Transform>(),
        [GhostCharacterPlaybackPose.Ragdoll] = new HashSet<Transform>()
    };
    private readonly Matrix4x4[] _matrices = new Matrix4x4[BulkGhostBatching.MaximumInstancesPerBatch];

    private readonly List<RenderPart> _renderParts = new();
    private readonly Dictionary<GhostCharacterPlaybackPose, List<RenderPart>> _characterRenderParts = new()
    {
        [GhostCharacterPlaybackPose.Seated] = new List<RenderPart>(),
        [GhostCharacterPlaybackPose.SeatedArmsUp] = new List<RenderPart>(),
        [GhostCharacterPlaybackPose.Ragdoll] = new List<RenderPart>()
    };
    public Quaternion RagdollRotationOffset { get; private set; } = Quaternion.identity;

    private bool _initializationAttempted;
    private bool _initialized;

    public BulkGhostRenderService(
        ILogger<BulkGhostRenderService> logger,
        ConfigService configService,
        PlayerLoopService playerLoopService)
    {
        _logger = logger;
        _configService = configService;
        playerLoopService.SubscribeLateUpdate(Draw);
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

    public void RegisterCharacter(Transform transform, GhostCharacterPlaybackPose pose)
    {
        if (transform != null)
            _characterInstances[pose].Add(transform);
    }

    public void UnregisterCharacter(Transform transform, GhostCharacterPlaybackPose pose)
    {
        if (transform != null)
            _characterInstances[pose].Remove(transform);
    }

    private void Draw()
    {
        if (!_initialized ||
            !_configService.ShowGhosts.Value ||
            !_configService.ShowGlobalPersonalBest.Value)
        {
            return;
        }

        DrawInstances(_instances, _renderParts);
        foreach (KeyValuePair<GhostCharacterPlaybackPose, HashSet<Transform>> characterInstances in _characterInstances)
            DrawInstances(characterInstances.Value, _characterRenderParts[characterInstances.Key]);
    }

    private void DrawInstances(IEnumerable<Transform> instances, IReadOnlyList<RenderPart> renderParts)
    {
        int count = 0;
        foreach (Transform instance in instances)
        {
            if (instance == null || !instance.gameObject.activeInHierarchy)
                continue;

            _matrices[count++] = instance.localToWorldMatrix;
            if (count != BulkGhostBatching.MaximumInstancesPerBatch)
                continue;

            DrawBatch(renderParts, count);
            count = 0;
        }

        if (count > 0)
            DrawBatch(renderParts, count);
    }

    private void DrawBatch(IReadOnlyList<RenderPart> renderParts, int count)
    {
        foreach (RenderPart part in renderParts)
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
            RagdollRotationOffset = GhostCharacterRig.GetRagdollRotationOffset(model);

            var soapboxMaterialGroups = new Dictionary<Material, List<CombineInstance>>();
            var characterMaterialGroups = new Dictionary<Material, List<CombineInstance>>();
            var armsUpCharacterMaterialGroups = new Dictionary<Material, List<CombineInstance>>();
            var ragdollCharacterMaterialGroups = new Dictionary<Material, List<CombineInstance>>();

            AddModelRenderersByMaterial(
                model,
                rootInverse,
                soapboxMaterialGroups,
                characterMaterialGroups,
                bakedMeshes);

            SetupModelCar armsUpModel = Object.Instantiate(
                ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab.ghostModel,
                templateRoot.transform);
            GhostVisuals.ConfigureBulkModel(armsUpModel);
            GhostCharacterRig.ApplySeatedArmsUpPose(armsUpModel);
            AddCharacterRenderersByMaterial(
                armsUpModel,
                rootInverse,
                armsUpCharacterMaterialGroups,
                bakedMeshes);

            SetupModelCar ragdollModel = Object.Instantiate(
                ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab.ghostModel,
                templateRoot.transform);
            GhostVisuals.ConfigureBulkModel(ragdollModel);
            GhostCharacterRig.ApplyStandingRagdollPose(ragdollModel);
            AddCharacterRenderersByMaterial(
                ragdollModel,
                rootInverse,
                ragdollCharacterMaterialGroups,
                bakedMeshes);

            if (soapboxMaterialGroups.Count == 0)
                throw new InvalidOperationException("Bulk ghost template contains no active meshes.");

            AddRenderParts(_renderParts, soapboxMaterialGroups, "GTR Instanced Bulk Ghost");
            if (characterMaterialGroups.Count > 0)
                AddRenderParts(_characterRenderParts[GhostCharacterPlaybackPose.Seated], characterMaterialGroups, "GTR Instanced Bulk Character");
            if (armsUpCharacterMaterialGroups.Count > 0)
                AddRenderParts(
                    _characterRenderParts[GhostCharacterPlaybackPose.SeatedArmsUp],
                    armsUpCharacterMaterialGroups,
                    "GTR Instanced Bulk Arms Up Character");
            if (ragdollCharacterMaterialGroups.Count > 0)
                AddRenderParts(
                    _characterRenderParts[GhostCharacterPlaybackPose.Ragdoll],
                    ragdollCharacterMaterialGroups,
                    "GTR Instanced Bulk Ragdoll Character");
        }
        finally
        {
            foreach (Mesh bakedMesh in bakedMeshes)
                Object.Destroy(bakedMesh);

            templateRoot.SetActive(false);
            Object.Destroy(templateRoot);
        }
    }

    private static bool IsBatchHatRenderer(Renderer renderer, SetupModelCar model)
    {
        if (renderer == null)
            return false;

        if (model?.hatParent != null && renderer.transform.IsChildOf(model.hatParent))
            return true;

        Transform current = renderer.transform;
        while (current != null && current != model?.transform)
        {
            if (current.name.IndexOf("hat", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            current = current.parent;
        }

        return false;
    }

    private static void AddModelRenderersByMaterial(
        SetupModelCar model,
        Matrix4x4 rootInverse,
        IDictionary<Material, List<CombineInstance>> soapboxMaterialGroups,
        IDictionary<Material, List<CombineInstance>> characterMaterialGroups,
        ICollection<Mesh> bakedMeshes)
    {
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(false))
        {
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || !renderer.enabled || filter.sharedMesh == null)
                continue;
            if (IsBatchHatRenderer(renderer, model))
                continue;

            if (GhostCharacterRenderers.IsCharacterRenderer(renderer, model))
                AddMeshFilterByMaterial(characterMaterialGroups, filter, rootInverse, renderer.sharedMaterials);
            else
                AddMeshFilterByMaterial(soapboxMaterialGroups, filter, rootInverse, renderer.sharedMaterials);
        }

        foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (IsBatchHatRenderer(renderer, model))
                continue;

            if (GhostCharacterRenderers.IsCharacterRenderer(renderer, model))
            {
                AddCorrectedSkinnedRendererByMaterial(
                    characterMaterialGroups,
                    renderer,
                    rootInverse,
                    bakedMeshes,
                    true);
            }
            else
            {
                AddCorrectedSkinnedRendererByMaterial(
                    soapboxMaterialGroups,
                    renderer,
                    rootInverse,
                    bakedMeshes,
                    true);
            }
        }
    }

    private static void AddCharacterRenderersByMaterial(
        SetupModelCar model,
        Matrix4x4 rootInverse,
        IDictionary<Material, List<CombineInstance>> characterMaterialGroups,
        ICollection<Mesh> bakedMeshes)
    {
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(false))
        {
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || !renderer.enabled || filter.sharedMesh == null)
                continue;
            if (IsBatchHatRenderer(renderer, model))
                continue;
            if (!GhostCharacterRenderers.IsCharacterRenderer(renderer, model))
                continue;

            AddMeshFilterByMaterial(characterMaterialGroups, filter, rootInverse, renderer.sharedMaterials);
        }

        foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (IsBatchHatRenderer(renderer, model))
                continue;
            if (!GhostCharacterRenderers.IsCharacterRenderer(renderer, model))
                continue;

            AddCorrectedSkinnedRendererByMaterial(
                characterMaterialGroups,
                renderer,
                rootInverse,
                bakedMeshes,
                true);
        }
    }

    private static void AddMeshFilterByMaterial(
        IDictionary<Material, List<CombineInstance>> destination,
        MeshFilter filter,
        Matrix4x4 rootInverse,
        IReadOnlyList<Material> materials)
    {
        AddSubMeshesByMaterial(
            destination,
            filter.sharedMesh,
            rootInverse * filter.transform.localToWorldMatrix,
            materials);
    }

    private static void AddCorrectedSkinnedRendererByMaterial(
        IDictionary<Material, List<CombineInstance>> destination,
        SkinnedMeshRenderer renderer,
        Matrix4x4 rootInverse,
        ICollection<Mesh> bakedMeshes,
        bool applyScaleCorrection)
    {
        if (renderer == null || !renderer.enabled || renderer.sharedMesh == null)
            return;

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
        if (applyScaleCorrection)
        {
            float correction = BulkGhostMeshScale.CalculateUniformScale(
                MaximumComponent(renderer.bounds.size),
                MaximumComponent(bakedWorldBounds.size));
            transform *= Matrix4x4.Scale(Vector3.one * correction);
        }

        AddSubMeshesByMaterial(
            destination,
            bakedMesh,
            transform,
            renderer.sharedMaterials);
    }

    private static void AddRenderParts(
        ICollection<RenderPart> destination,
        IReadOnlyDictionary<Material, List<CombineInstance>> materialGroups,
        string name)
    {
        int partIndex = 0;
        foreach (KeyValuePair<Material, List<CombineInstance>> materialGroup in materialGroups)
        {
            var mesh = new Mesh
            {
                name = $"{name} {partIndex++}",
                indexFormat = IndexFormat.UInt32
            };
            mesh.CombineMeshes(materialGroup.Value.ToArray(), true, true, false);
            mesh.RecalculateBounds();

            var material = new Material(materialGroup.Key)
            {
                enableInstancing = true,
                name = $"GTR Instanced {materialGroup.Key.name}"
            };
            destination.Add(new RenderPart(mesh, material));
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

        foreach (List<RenderPart> renderParts in _characterRenderParts.Values)
        {
            foreach (RenderPart part in renderParts)
            {
                Object.Destroy(part.Mesh);
                Object.Destroy(part.Material);
            }

            renderParts.Clear();
        }
        _initialized = false;
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
