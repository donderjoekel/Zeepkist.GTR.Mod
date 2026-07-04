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
    private readonly Dictionary<GhostCharacterPlaybackPose, Dictionary<int, HashSet<Transform>>> _characterInstances = new()
    {
        [GhostCharacterPlaybackPose.Seated] = CreateTintBuckets(),
        [GhostCharacterPlaybackPose.SeatedArmsUp] = CreateTintBuckets(),
        [GhostCharacterPlaybackPose.Ragdoll] = CreateTintBuckets()
    };
    private readonly Dictionary<int, Color> _tintBucketColors = new();
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int ModeId = Shader.PropertyToID("_Mode");
    private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
    private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
    private const float BulkGhostAlpha = 1.0f;
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

    public void RegisterCharacter(Transform transform, GhostCharacterPlaybackPose pose, Color color)
    {
        if (transform == null)
            return;

        int tintBucket = GetTintBucket(color);
        if (!_tintBucketColors.ContainsKey(tintBucket))
            _tintBucketColors.Add(tintBucket, color with { a = BulkGhostAlpha });

        if (!_characterInstances[pose].TryGetValue(tintBucket, out HashSet<Transform> instances))
        {
            instances = new HashSet<Transform>();
            _characterInstances[pose].Add(tintBucket, instances);
        }

        instances.Add(transform);
    }

    public void UnregisterCharacter(Transform transform, GhostCharacterPlaybackPose pose)
    {
        if (transform == null)
            return;

        foreach (HashSet<Transform> instances in _characterInstances[pose].Values)
            instances.Remove(transform);
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
        foreach (KeyValuePair<GhostCharacterPlaybackPose, Dictionary<int, HashSet<Transform>>> characterInstances in _characterInstances)
        {
            foreach (KeyValuePair<int, HashSet<Transform>> tintBucket in characterInstances.Value)
                DrawInstances(
                    tintBucket.Value,
                    _characterRenderParts[characterInstances.Key],
                    tintBucket.Key);
        }
    }

    private void DrawInstances(
        IEnumerable<Transform> instances,
        IReadOnlyList<RenderPart> renderParts,
        int? tintBucket = null)
    {
        int count = 0;
        foreach (Transform instance in instances)
        {
            if (instance == null || !instance.gameObject.activeInHierarchy)
                continue;

            _matrices[count++] = instance.localToWorldMatrix;
            if (count != BulkGhostBatching.MaximumInstancesPerBatch)
                continue;

            DrawBatch(renderParts, count, tintBucket);
            count = 0;
        }

        if (count > 0)
            DrawBatch(renderParts, count, tintBucket);
    }

    private void DrawBatch(IReadOnlyList<RenderPart> renderParts, int count, int? tintBucket = null)
    {
        foreach (RenderPart part in renderParts)
            DrawPart(part, count, tintBucket);
    }

    private void DrawPart(RenderPart part, int count, int? tintBucket = null)
    {
        Graphics.DrawMeshInstanced(
            part.Mesh,
            0,
            GetMaterial(part, tintBucket),
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

    private Material GetMaterial(RenderPart part, int? tintBucket)
    {
        if (!tintBucket.HasValue || !part.Tintable)
            return part.Material;

        return part.GetTintedMaterial(tintBucket.Value, GetTintBucketColor(tintBucket.Value));
    }

    private static Dictionary<int, HashSet<Transform>> CreateTintBuckets()
    {
        return new Dictionary<int, HashSet<Transform>>();
    }

    private int GetTintBucket(Color color)
    {
        int bucketCount = GetTintBucketCount();
        if (bucketCount <= 1)
            return 0;

        Color.RGBToHSV(color, out float hue, out float saturation, out float value);
        if (saturation < 0.1f || value < 0.1f)
            return 0;

        return 1 + Mathf.FloorToInt(hue * (bucketCount - 1)) % (bucketCount - 1);
    }

    private Color GetTintBucketColor(int bucket)
    {
        return _tintBucketColors.TryGetValue(bucket, out Color color)
            ? color
            : Color.white with { a = BulkGhostAlpha };
    }

    private int GetTintBucketCount()
    {
        return Math.Max(1, _configService.MaximumGhostColours.Value);
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

            destination.Add(new RenderPart(
                mesh,
                CreateInstancedBulkMaterial(materialGroup.Key),
                IsTintableCharacterMaterial(materialGroup.Key)));
        }
    }


    private static bool IsTintableCharacterMaterial(Material material)
    {
        if (material == null)
            return false;

        string name = material.name.ToLowerInvariant();
        if (name.Contains("face") ||
            name.Contains("smile") ||
            name.Contains("mouth") ||
            name.Contains("eye") ||
            name.Contains("heart"))
        {
            return false;
        }

        Color color = GetMaterialColor(material);
        return color.maxColorComponent > 0.08f;
    }

    private static Color GetMaterialColor(Material material)
    {
        if (material.HasProperty(ColorId))
            return material.GetColor(ColorId);
        if (material.HasProperty(BaseColorId))
            return material.GetColor(BaseColorId);
        if (material.HasProperty(TintColorId))
            return material.GetColor(TintColorId);

        return Color.white;
    }
    private static Material CreateInstancedBulkMaterial(Material source)
    {
        var material = new Material(source)
        {
            enableInstancing = true,
            name = $"GTR Instanced {source.name}"
        };

        ConfigureInstancedMaterial(material);
        return material;
    }
    private static void ConfigureInstancedMaterial(Material material)
    {
        if (material.HasProperty(ModeId))
            material.SetFloat(ModeId, 3f);
        if (material.HasProperty(SrcBlendId))
            material.SetInt(SrcBlendId, (int)BlendMode.One);
        if (material.HasProperty(DstBlendId))
            material.SetInt(DstBlendId, (int)BlendMode.Zero);
        if (material.HasProperty(ZWriteId))
            material.SetInt(ZWriteId, 1);

        SetMaterialAlpha(material, BulkGhostAlpha);
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)RenderQueue.Geometry;
    }


    private static void SetMaterialColor(Material material, Color color)
    {
        SetMaterialColor(material, ColorId, color);
        SetMaterialColor(material, BaseColorId, color);
        SetMaterialColor(material, TintColorId, color);
    }

    private static void SetMaterialColor(Material material, int propertyId, Color color)
    {
        if (!material.HasProperty(propertyId))
            return;

        Color existing = material.GetColor(propertyId);
        existing.r = color.r;
        existing.g = color.g;
        existing.b = color.b;
        existing.a = color.a;
        material.SetColor(propertyId, existing);
    }
    private static void SetMaterialAlpha(Material material, float alpha)
    {
        SetMaterialAlpha(material, ColorId, alpha);
        SetMaterialAlpha(material, BaseColorId, alpha);
        SetMaterialAlpha(material, TintColorId, alpha);
    }

    private static void SetMaterialAlpha(Material material, int propertyId, float alpha)
    {
        if (!material.HasProperty(propertyId))
            return;

        Color color = material.GetColor(propertyId);
        color.a = alpha;
        material.SetColor(propertyId, color);
    }

    private static void CopyColor(Material source, Material destination, int propertyId)
    {
        if (source == null || destination == null)
            return;
        if (!source.HasProperty(propertyId) || !destination.HasProperty(propertyId))
            return;

        Color sourceColor = source.GetColor(propertyId);
        Color destinationColor = destination.GetColor(propertyId);
        destinationColor.r = sourceColor.r;
        destinationColor.g = sourceColor.g;
        destinationColor.b = sourceColor.b;
        destination.SetColor(propertyId, destinationColor);
    }

    private static void CopyTexture(Material source, Material destination, int propertyId)
    {
        if (source == null || destination == null)
            return;
        if (!source.HasProperty(propertyId) || !destination.HasProperty(propertyId))
            return;

        destination.SetTexture(propertyId, source.GetTexture(propertyId));
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
            part.Dispose();

        _renderParts.Clear();

        foreach (List<RenderPart> renderParts in _characterRenderParts.Values)
        {
            foreach (RenderPart part in renderParts)
                part.Dispose();

            renderParts.Clear();
        }
        _initialized = false;
    }
    private sealed class RenderPart : IDisposable
    {
        private readonly Dictionary<int, Material> _tintedMaterials = new();

        public RenderPart(Mesh mesh, Material material, bool tintable)
        {
            Mesh = mesh;
            Material = material;
            Tintable = tintable;
        }

        public Mesh Mesh { get; }
        public Material Material { get; }
        public bool Tintable { get; }

        public Material GetTintedMaterial(int bucket, Color tint)
        {
            if (_tintedMaterials.TryGetValue(bucket, out Material material))
                return material;

            material = new Material(Material)
            {
                enableInstancing = true,
                name = $"{Material.name} Tint {bucket}"
            };
            SetMaterialColor(material, tint);
            _tintedMaterials.Add(bucket, material);
            return material;
        }

        public void Dispose()
        {
            Object.Destroy(Mesh);
            Object.Destroy(Material);

            foreach (Material tintedMaterial in _tintedMaterials.Values)
                Object.Destroy(tintedMaterial);

            _tintedMaterials.Clear();
        }
    }
}
