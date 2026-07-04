using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Assets;
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
    private readonly AssetService _assetService;
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
    private static readonly int ColorTintId = Shader.PropertyToID("_ColorTint");
    private static readonly int ModeId = Shader.PropertyToID("_Mode");
    private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
    private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MatricesId = Shader.PropertyToID("_Matrices");
    private const float BulkGhostAlpha = 1.0f;
    private const string BulkGhostDepthShaderName = "GTR/BulkGhostDepth";
    private const CameraEvent DepthPrepassCameraEvent = CameraEvent.BeforeImageEffects;
    private const int BulkGhostColorRenderQueue = (int)RenderQueue.Overlay - 50;
    private static readonly Color CharacterBaseColor = Color.white with { a = BulkGhostAlpha };
    private static readonly Color CharacterDetailColor = Color.black with { a = BulkGhostAlpha };
    private readonly Matrix4x4[] _matrices = new Matrix4x4[BulkGhostBatching.MaximumInstancesPerBatch];
    private readonly MaterialPropertyBlock _propertyBlock = new();
    private readonly CommandBuffer _depthCommandBuffer = new()
    {
        name = "GTR Bulk Ghost Depth"
    };
    private readonly List<ComputeBuffer> _matrixBuffers = new();
    private int _matrixBufferIndex;
    private Camera _depthCamera;

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
    private bool _badDrawShaderLogged;
    private bool _bulkShaderFromBundle;
    private Shader _bulkShader;
    private Material _depthMaterial;

    public BulkGhostRenderService(
        ILogger<BulkGhostRenderService> logger,
        ConfigService configService,
        AssetService assetService,
        PlayerLoopService playerLoopService)
    {
        _logger = logger;
        _configService = configService;
        _assetService = assetService;
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
            _depthCommandBuffer.Clear();
            return;
        }

        PrepareDepthPrepass();
        _matrixBufferIndex = 0;
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

    private void PrepareDepthPrepass()
    {
        _depthCommandBuffer.Clear();
        if (_depthMaterial == null)
            return;

        Camera camera = Camera.main;
        if (camera == null)
            return;

        if (_depthCamera == camera)
            return;

        if (_depthCamera != null)
            _depthCamera.RemoveCommandBuffer(DepthPrepassCameraEvent, _depthCommandBuffer);

        _depthCamera = camera;
        _depthCamera.AddCommandBuffer(DepthPrepassCameraEvent, _depthCommandBuffer);
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

    private void DrawBatch(
        IReadOnlyList<RenderPart> renderParts,
        int count,
        int? tintBucket = null)
    {
        foreach (RenderPart part in renderParts)
            DrawPart(part, count, tintBucket);
    }

    private void DrawPart(
        RenderPart part,
        int count,
        int? tintBucket = null)
    {
        Material material = GetMaterial(part, tintBucket);
        if (material == null || IsIncompatibleDrawShader(material.shader))
        {
            if (!_badDrawShaderLogged)
            {
                _badDrawShaderLogged = true;
                _logger.LogError(
                    "Skipping bulk ghost draw because material {Material} uses incompatible shader {Shader}. Check that gtr-shaders is beside the mod DLL and loaded.",
                    material != null ? material.name : "null",
                    material?.shader != null ? material.shader.name : "null");
            }

            return;
        }

        _propertyBlock.Clear();
        ComputeBuffer matrixBuffer = GetMatrixBuffer();
        matrixBuffer.SetData(_matrices, 0, 0, count);
        _propertyBlock.SetBuffer(MatricesId, matrixBuffer);
        _propertyBlock.SetColor(
            ColorId,
            material.HasProperty(ColorId) ? material.GetColor(ColorId) : Color.white);

        DrawDepthPart(part, count);

        Graphics.DrawMeshInstancedProcedural(
            part.Mesh,
            0,
            material,
            CalculateProceduralBounds(part.Mesh.bounds, count),
            count,
            _propertyBlock,
            ShadowCastingMode.Off,
            false,
            0,
            null,
            LightProbeUsage.Off,
            null);
    }

    private void DrawDepthPart(RenderPart part, int count)
    {
        if (_depthMaterial == null)
            return;

        _depthCommandBuffer.DrawMeshInstanced(
            part.Mesh,
            0,
            _depthMaterial,
            0,
            _matrices,
            count);
    }

    private ComputeBuffer GetMatrixBuffer()
    {
        if (_matrixBufferIndex == _matrixBuffers.Count)
            _matrixBuffers.Add(new ComputeBuffer(BulkGhostBatching.MaximumInstancesPerBatch, sizeof(float) * 16));

        return _matrixBuffers[_matrixBufferIndex++];
    }

    private Bounds CalculateProceduralBounds(Bounds meshBounds, int count)
    {
        Vector3 min = _matrices[0].GetColumn(3);
        Vector3 max = min;
        for (int i = 1; i < count; i++)
        {
            Vector3 position = _matrices[i].GetColumn(3);
            min = Vector3.Min(min, position);
            max = Vector3.Max(max, position);
        }

        float meshRadius = meshBounds.extents.magnitude;
        return new Bounds(
            (min + max) * 0.5f,
            (max - min) + Vector3.one * Math.Max(100f, meshRadius * 4f));
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

            var soapboxMeshes = new SoapboxMeshGroups();
            var characterMeshes = new CharacterMeshGroups();
            var armsUpCharacterMeshes = new CharacterMeshGroups();
            var ragdollCharacterMeshes = new CharacterMeshGroups();

            AddModelRenderersByMaterial(
                model,
                rootInverse,
                soapboxMeshes,
                characterMeshes,
                bakedMeshes,
                GetTintBucketCount());

            SetupModelCar armsUpModel = Object.Instantiate(
                ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab.ghostModel,
                templateRoot.transform);
            GhostVisuals.ConfigureBulkModel(armsUpModel);
            GhostCharacterRig.ApplySeatedArmsUpPose(armsUpModel);
            AddCharacterRenderersByMaterial(
                armsUpModel,
                rootInverse,
                armsUpCharacterMeshes,
                bakedMeshes);

            SetupModelCar ragdollModel = Object.Instantiate(
                ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab.ghostModel,
                templateRoot.transform);
            GhostVisuals.ConfigureBulkModel(ragdollModel);
            GhostCharacterRig.ApplyStandingRagdollPose(ragdollModel);
            AddCharacterRenderersByMaterial(
                ragdollModel,
                rootInverse,
                ragdollCharacterMeshes,
                bakedMeshes);

            if (soapboxMeshes.Count == 0)
                throw new InvalidOperationException("Bulk ghost template contains no active meshes.");

            _bulkShader = ResolveBulkShader();
            if (_bulkShader == null)
                throw new InvalidOperationException("No compatible shader found for instanced bulk ghost rendering.");

            _depthMaterial = CreateDepthMaterial();

            AddSoapboxRenderParts(_renderParts, soapboxMeshes, "GTR Instanced Bulk Ghost");
            AddCharacterRenderParts(
                _characterRenderParts[GhostCharacterPlaybackPose.Seated],
                characterMeshes,
                "GTR Instanced Bulk Character");
            AddCharacterRenderParts(
                _characterRenderParts[GhostCharacterPlaybackPose.SeatedArmsUp],
                armsUpCharacterMeshes,
                "GTR Instanced Bulk Arms Up Character");
            AddCharacterRenderParts(
                _characterRenderParts[GhostCharacterPlaybackPose.Ragdoll],
                ragdollCharacterMeshes,
                "GTR Instanced Bulk Ragdoll Character");

            LogRenderPartCounts();
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
        SoapboxMeshGroups soapboxMeshes,
        CharacterMeshGroups characterMeshes,
        ICollection<Mesh> bakedMeshes,
        int colorBucketCount)
    {
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(false))
        {
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || !renderer.enabled || filter.sharedMesh == null)
                continue;
            if (IsBatchHatRenderer(renderer, model))
                continue;

            if (GhostCharacterRenderers.IsCharacterRenderer(renderer, model))
                AddMeshFilterToCharacterGroups(characterMeshes, filter, rootInverse, renderer.sharedMaterials, renderer.name);
            else
                AddMeshFilterToSoapboxGroups(
                    soapboxMeshes,
                    filter,
                    rootInverse,
                    renderer.sharedMaterials,
                    colorBucketCount);
        }

        foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (IsBatchHatRenderer(renderer, model))
                continue;

            if (GhostCharacterRenderers.IsCharacterRenderer(renderer, model))
            {
                AddCorrectedSkinnedRendererByMaterial(
                    characterMeshes,
                    renderer,
                    rootInverse,
                    bakedMeshes,
                    true);
            }
            else
            {
                AddCorrectedSkinnedRendererByMaterial(
                    soapboxMeshes,
                    renderer,
                    rootInverse,
                    bakedMeshes,
                    true,
                    colorBucketCount);
            }
        }
    }

    private static void AddCharacterRenderersByMaterial(
        SetupModelCar model,
        Matrix4x4 rootInverse,
        CharacterMeshGroups characterMeshes,
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

            AddMeshFilterToCharacterGroups(characterMeshes, filter, rootInverse, renderer.sharedMaterials, renderer.name);
        }

        foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (IsBatchHatRenderer(renderer, model))
                continue;
            if (!GhostCharacterRenderers.IsCharacterRenderer(renderer, model))
                continue;

            AddCorrectedSkinnedRendererByMaterial(
                characterMeshes,
                renderer,
                rootInverse,
                bakedMeshes,
                true);
        }
    }

    private static void AddMeshFilterToGroup(
        ICollection<CombineInstance> destination,
        MeshFilter filter,
        Matrix4x4 rootInverse)
    {
        AddSubMeshesToGroup(destination, filter.sharedMesh, rootInverse * filter.transform.localToWorldMatrix);
    }

    private static void AddMeshFilterToSoapboxGroups(
        SoapboxMeshGroups destination,
        MeshFilter filter,
        Matrix4x4 rootInverse,
        IReadOnlyList<Material> materials,
        int colorBucketCount)
    {
        AddSubMeshesToSoapboxGroups(
            destination,
            filter.sharedMesh,
            rootInverse * filter.transform.localToWorldMatrix,
            materials,
            colorBucketCount);
    }

    private static void AddMeshFilterToCharacterGroups(
        CharacterMeshGroups destination,
        MeshFilter filter,
        Matrix4x4 rootInverse,
        IReadOnlyList<Material> materials,
        string rendererName)
    {
        AddSubMeshesToCharacterGroups(
            destination,
            filter.sharedMesh,
            rootInverse * filter.transform.localToWorldMatrix,
            materials,
            rendererName);
    }

    private static void AddCorrectedSkinnedRendererByMaterial(
        SoapboxMeshGroups destination,
        SkinnedMeshRenderer renderer,
        Matrix4x4 rootInverse,
        ICollection<Mesh> bakedMeshes,
        bool applyScaleCorrection,
        int colorBucketCount)
    {
        AddCorrectedSkinnedRenderer(
            renderer,
            rootInverse,
            bakedMeshes,
            applyScaleCorrection,
            (mesh, transform) => AddSubMeshesToSoapboxGroups(
                destination,
                mesh,
                transform,
                renderer.sharedMaterials,
                colorBucketCount));
    }

    private static void AddCorrectedSkinnedRendererByMaterial(
        CharacterMeshGroups destination,
        SkinnedMeshRenderer renderer,
        Matrix4x4 rootInverse,
        ICollection<Mesh> bakedMeshes,
        bool applyScaleCorrection)
    {
        AddCorrectedSkinnedRenderer(
            renderer,
            rootInverse,
            bakedMeshes,
            applyScaleCorrection,
            (mesh, transform) => AddSubMeshesToCharacterGroups(destination, mesh, transform, renderer.sharedMaterials, renderer.name));
    }

    private static void AddCorrectedSkinnedRenderer(
        SkinnedMeshRenderer renderer,
        Matrix4x4 rootInverse,
        ICollection<Mesh> bakedMeshes,
        bool applyScaleCorrection,
        Action<Mesh, Matrix4x4> addSubMeshes)
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

        addSubMeshes(bakedMesh, transform);
    }

    private void AddCharacterRenderParts(
        ICollection<RenderPart> destination,
        CharacterMeshGroups meshGroups,
        string name)
    {
        AddRenderPart(destination, meshGroups.Tintable, $"{name} Body", CharacterBaseColor, true);
        AddRenderPart(destination, meshGroups.Details, $"{name} Details", CharacterDetailColor, false);
    }

    private void AddSoapboxRenderParts(
        ICollection<RenderPart> destination,
        SoapboxMeshGroups meshGroups,
        string name)
    {
        int partIndex = 0;
        foreach (KeyValuePair<SoapboxMaterialKey, SoapboxMeshGroup> group in meshGroups.Groups)
        {
            AddRenderPart(
                destination,
                group.Value.Instances,
                $"{name} {partIndex++}",
                group.Value.Color,
                false,
                group.Value.Texture);
        }
    }

    private void AddRenderPart(
        ICollection<RenderPart> destination,
        IReadOnlyList<CombineInstance> instances,
        string name,
        Color color,
        bool tintable)
    {
        AddRenderPart(destination, instances, name, color, tintable, null);
    }

    private void AddRenderPart(
        ICollection<RenderPart> destination,
        IReadOnlyList<CombineInstance> instances,
        string name,
        Color color,
        bool tintable,
        Texture texture)
    {
        if (instances.Count == 0)
            return;

        var mesh = new Mesh
        {
            name = name,
            indexFormat = IndexFormat.UInt32
        };
        var combineInstances = new CombineInstance[instances.Count];
        for (int i = 0; i < instances.Count; i++)
            combineInstances[i] = instances[i];

        mesh.CombineMeshes(combineInstances, true, true, false);
        mesh.RecalculateBounds();

        destination.Add(new RenderPart(
            mesh,
            CreateInstancedBulkMaterial(color, name, texture),
            tintable));
    }

    private static bool IsTintableCharacterMaterial(Material material, string rendererName)
    {
        if (material == null)
            return false;

        if (IsCharacterDetailName(material.name) || IsCharacterDetailName(rendererName))
        {
            return false;
        }

        return true;
    }

    private static bool IsCharacterDetailName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        string lowerName = name.ToLowerInvariant();
        return lowerName.Contains("face") ||
               lowerName.Contains("smile") ||
               lowerName.Contains("mouth") ||
               lowerName.Contains("eye") ||
               lowerName.Contains("heart");
    }

    private Material CreateInstancedBulkMaterial(Color color, string name, Texture texture = null)
    {
        var material = new Material(_bulkShader)
        {
            enableInstancing = true,
            name = name
        };

        ConfigureInstancedMaterial(material);
        if (texture != null && material.HasProperty(MainTexId))
            material.SetTexture(MainTexId, texture);

        SetMaterialColor(material, color);
        return material;
    }

    private Shader ResolveBulkShader()
    {
        Material ghostMaterial = ComponentCache.Get<NetworkedGhostSpawner>().zeepkistGhostPrefab.ghostFader.fadeThisMaterial;
        Shader shader = ghostMaterial != null ? ghostMaterial.shader : null;
        if (shader != null)
        {
            Shader bundledShader = _assetService.GetShader(shader.name);
            if (bundledShader != null)
            {
                _bulkShaderFromBundle = true;
                _logger.LogInformation(
                    "Using bundled bulk ghost shader {Shader} instead of runtime shader {RuntimeShader}",
                    bundledShader.name,
                    shader.name);
                return bundledShader;
            }
        }

        if (shader != null && !IsBadBulkShader(shader))
        {
            _bulkShaderFromBundle = false;
            _logger.LogDebug("Using bulk ghost fader shader: {Shader}", shader.name);
            return shader;
        }

        _logger.LogWarning(
            "No compatible bulk ghost shader found. Fader shader was {Shader}. Bulk instancing disabled to avoid DrawMeshInstanced shader spam.",
            shader != null ? shader.name : "null");
        return null;
    }

    private Material CreateDepthMaterial()
    {
        Shader depthShader = _assetService.GetShader(BulkGhostDepthShaderName);
        if (depthShader == null)
        {
            _logger.LogWarning(
                "Bulk ghost depth prepass disabled because shader {Shader} was not found in gtr-shaders.",
                BulkGhostDepthShaderName);
            return null;
        }

        var material = new Material(depthShader)
        {
            enableInstancing = true,
            name = "GTR Instanced Bulk Ghost Depth"
        };
        return material;
    }

    private static bool IsBadBulkShader(Shader shader)
    {
        if (shader == null)
            return true;

        string shaderName = shader.name;
        return shaderName == "Standard" ||
               shaderName == "Hidden/InternalErrorShader" ||
               shaderName == "Unlit/Color" ||
               shaderName == "Unlit/Texture" ||
               shaderName == "Sprites/Default" ||
               shaderName.StartsWith("UVFree/", StringComparison.Ordinal);
    }

    private bool IsIncompatibleDrawShader(Shader shader)
    {
        if (shader == null)
            return true;

        if (_bulkShaderFromBundle && ReferenceEquals(shader, _bulkShader))
            return false;

        return IsBadBulkShader(shader);
    }

    private static void ConfigureInstancedMaterial(Material material)
    {
        if (material.HasProperty(ModeId))
            material.SetFloat(ModeId, 0f);
        if (material.HasProperty(SrcBlendId))
            material.SetInt(SrcBlendId, (int)BlendMode.One);
        if (material.HasProperty(DstBlendId))
            material.SetInt(DstBlendId, (int)BlendMode.Zero);
        if (material.HasProperty(ZWriteId))
            material.SetInt(ZWriteId, 1);

        SetMaterialAlpha(material, BulkGhostAlpha);
        if (material.HasProperty(MainTexId))
            material.SetTexture(MainTexId, Texture2D.whiteTexture);

        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.SetOverrideTag("RenderType", "Opaque");
        material.SetOverrideTag("Queue", "Overlay-50");
        material.renderQueue = BulkGhostColorRenderQueue;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        SetMaterialColor(material, ColorId, color);
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
    }

    private static void SetMaterialAlpha(Material material, int propertyId, float alpha)
    {
        if (!material.HasProperty(propertyId))
            return;

        Color color = material.GetColor(propertyId);
        color.a = alpha;
        material.SetColor(propertyId, color);
    }
    private static void AddSubMeshesToGroup(
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

    private static void AddSubMeshesToSoapboxGroups(
        SoapboxMeshGroups destination,
        Mesh mesh,
        Matrix4x4 transform,
        IReadOnlyList<Material> materials,
        int colorBucketCount)
    {
        for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
        {
            Material material = materials.Count > 0
                ? materials[Math.Min(subMesh, materials.Count - 1)]
                : null;
            int bucket = GetSoapboxColorBucket(material, colorBucketCount);
            Texture texture = GetSoapboxMaterialTexture(material);
            destination.Add(
                new SoapboxMaterialKey(texture, bucket),
                GetSoapboxMaterialColor(material),
                texture,
                new CombineInstance
                {
                    mesh = mesh,
                    subMeshIndex = subMesh,
                    transform = transform
                });
        }
    }

    private static void AddSubMeshesToCharacterGroups(
        CharacterMeshGroups destination,
        Mesh mesh,
        Matrix4x4 transform,
        IReadOnlyList<Material> materials,
        string rendererName)
    {
        for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
        {
            if (materials.Count == 0)
                continue;

            Material material = materials[Math.Min(subMesh, materials.Count - 1)];
            if (material == null)
                continue;

            ICollection<CombineInstance> group = IsTintableCharacterMaterial(material, rendererName)
                ? destination.Tintable
                : destination.Details;
            group.Add(new CombineInstance
            {
                mesh = mesh,
                subMeshIndex = subMesh,
                transform = transform
            });
        }
    }

    private static int GetSoapboxColorBucket(Material material, int colorBucketCount)
    {
        colorBucketCount = Math.Max(1, colorBucketCount);
        Color color = GetSoapboxMaterialColor(material);
        int r = Mathf.Clamp(Mathf.RoundToInt(color.r * 3f), 0, 3);
        int g = Mathf.Clamp(Mathf.RoundToInt(color.g * 3f), 0, 3);
        int b = Mathf.Clamp(Mathf.RoundToInt(color.b * 3f), 0, 3);
        return ((r * 4 + g) * 4 + b) % colorBucketCount;
    }

    private static Color GetSoapboxMaterialColor(Material material)
    {
        if (material == null)
            return Color.white with { a = BulkGhostAlpha };

        if (material.HasProperty(ColorId))
            return NormalizeBulkColor(material.GetColor(ColorId));
        if (material.HasProperty(BaseColorId))
            return NormalizeBulkColor(material.GetColor(BaseColorId));
        if (material.HasProperty(TintColorId))
            return NormalizeBulkColor(material.GetColor(TintColorId));
        if (material.HasProperty(ColorTintId))
            return NormalizeBulkColor(material.GetColor(ColorTintId));

        if (TryGetAverageTextureColor(material, MainTexId, out Color mainTextureColor))
            return mainTextureColor;
        if (TryGetAverageTextureColor(material, BaseMapId, out Color baseMapColor))
            return baseMapColor;

        return Color.white with { a = BulkGhostAlpha };
    }

    private static Texture GetSoapboxMaterialTexture(Material material)
    {
        if (material == null)
            return null;

        if (material.HasProperty(MainTexId) && material.GetTexture(MainTexId) != null)
            return material.GetTexture(MainTexId);
        if (material.HasProperty(BaseMapId) && material.GetTexture(BaseMapId) != null)
            return material.GetTexture(BaseMapId);

        return null;
    }

    private static Color NormalizeBulkColor(Color color)
    {
        if (color.a <= 0.001f)
            color.a = BulkGhostAlpha;

        color.a = BulkGhostAlpha;
        return color;
    }

    private static bool TryGetAverageTextureColor(Material material, int texturePropertyId, out Color color)
    {
        color = Color.white with { a = BulkGhostAlpha };
        if (!material.HasProperty(texturePropertyId))
            return false;

        if (material.GetTexture(texturePropertyId) is not Texture2D texture)
            return false;

        try
        {
            Color32[] pixels = texture.GetPixels32();
            if (pixels.Length == 0)
                return false;

            long red = 0;
            long green = 0;
            long blue = 0;
            long alpha = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                red += pixel.r;
                green += pixel.g;
                blue += pixel.b;
                alpha += pixel.a;
            }

            float scale = 1f / (pixels.Length * 255f);
            color = new Color(
                red * scale,
                green * scale,
                blue * scale,
                Math.Max(alpha * scale, BulkGhostAlpha)) with
            {
                a = BulkGhostAlpha
            };
            return true;
        }
        catch (UnityException)
        {
            return false;
        }
    }

    private void LogRenderPartCounts()
    {
        int maxCharacterParts = 0;
        foreach (List<RenderPart> renderParts in _characterRenderParts.Values)
            maxCharacterParts = Math.Max(maxCharacterParts, renderParts.Count);

        int estimatedDrawCallsFor1000Ghosts = _renderParts.Count + maxCharacterParts * GetTintBucketCount();
        _logger.LogDebug(
            "Bulk ghost render parts: shader={Shader}, soapbox={SoapboxParts}, seated={SeatedParts}, armsUp={ArmsUpParts}, ragdoll={RagdollParts}, tintBuckets={TintBuckets}, estimatedDrawCallsFor1000Ghosts={EstimatedDrawCalls}",
            _bulkShader != null ? _bulkShader.name : "null",
            _renderParts.Count,
            _characterRenderParts[GhostCharacterPlaybackPose.Seated].Count,
            _characterRenderParts[GhostCharacterPlaybackPose.SeatedArmsUp].Count,
            _characterRenderParts[GhostCharacterPlaybackPose.Ragdoll].Count,
            GetTintBucketCount(),
            estimatedDrawCallsFor1000Ghosts);
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
        if (_depthCamera != null)
        {
            _depthCamera.RemoveCommandBuffer(DepthPrepassCameraEvent, _depthCommandBuffer);
            _depthCamera = null;
        }

        _depthCommandBuffer.Clear();
        if (_depthMaterial != null)
        {
            Object.Destroy(_depthMaterial);
            _depthMaterial = null;
        }

        foreach (RenderPart part in _renderParts)
            part.Dispose();

        _renderParts.Clear();

        foreach (List<RenderPart> renderParts in _characterRenderParts.Values)
        {
            foreach (RenderPart part in renderParts)
                part.Dispose();

            renderParts.Clear();
        }

        foreach (ComputeBuffer matrixBuffer in _matrixBuffers)
            matrixBuffer.Release();

        _matrixBuffers.Clear();
        _matrixBufferIndex = 0;
        _initialized = false;
    }

    private sealed class CharacterMeshGroups
    {
        public readonly List<CombineInstance> Tintable = new();
        public readonly List<CombineInstance> Details = new();
    }

    private sealed class SoapboxMeshGroups
    {
        public readonly Dictionary<SoapboxMaterialKey, SoapboxMeshGroup> Groups = new();

        public int Count { get; private set; }

        public void Add(SoapboxMaterialKey key, Color color, Texture texture, CombineInstance instance)
        {
            if (!Groups.TryGetValue(key, out SoapboxMeshGroup group))
            {
                group = new SoapboxMeshGroup(texture);
                Groups.Add(key, group);
            }

            group.AddColor(color);
            group.Instances.Add(instance);
            Count++;
        }
    }

    private sealed class SoapboxMeshGroup
    {
        public readonly List<CombineInstance> Instances = new();
        public readonly Texture Texture;
        private Color _colorSum;
        private int _colorCount;

        public SoapboxMeshGroup(Texture texture)
        {
            Texture = texture;
        }

        public Color Color => _colorCount > 0
            ? (_colorSum / _colorCount) with { a = BulkGhostAlpha }
            : Color.white with { a = BulkGhostAlpha };

        public void AddColor(Color color)
        {
            _colorSum += color;
            _colorCount++;
        }
    }

    private readonly struct SoapboxMaterialKey : IEquatable<SoapboxMaterialKey>
    {
        private readonly int _textureId;
        private readonly int _colorBucket;

        public SoapboxMaterialKey(Texture texture, int colorBucket)
        {
            _textureId = texture != null ? texture.GetInstanceID() : 0;
            _colorBucket = colorBucket;
        }

        public bool Equals(SoapboxMaterialKey other) =>
            _textureId == other._textureId && _colorBucket == other._colorBucket;

        public override bool Equals(object obj) =>
            obj is SoapboxMaterialKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (_textureId * 397) ^ _colorBucket;
            }
        }
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
