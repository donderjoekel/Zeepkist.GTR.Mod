using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.DTOs.ResponseModels;
using TNRD.Zeepkist.GTR.Mod.Components.Ghosting.Readers;
using UnityEngine;
using UnityEngine.Rendering;
using ZeepSDK.Cosmetics;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting;

public class GhostPlayer : MonoBehaviourWithLogging
{
    private IGhostReader ghostReader;

    private int SoapboxId => ghostReader.SoapboxId;
    private int HatId => ghostReader.HatId;
    private int ColorId => ghostReader.ColorId;

    private GhostVisuals ghostVisuals;

    private bool hasSetMaterials;

    private bool downloadedGhost;
    private FrameData frameData;
    private Transform tr;

    private Vector3 positionVelocity;
    private Vector3 rotationVelocity;

    private Vector3 targetPosition;
    private Quaternion targetRotation;

    private static float Ticker =>
        PlayerManager.Instance.currentMaster.PlayersReady.FirstOrDefault()?.ticker.what_ticker ?? 0;

    protected override void Awake()
    {
        base.Awake();
        tr = transform;
        frameData = new FrameData();
    }

    public void Initialize(
        NetworkedZeepkistGhost original,
        string name,
        Color? color,
        MediaResponseModel mediaResponseModel,
        GhostVisuals ghostVisuals
    )
    {
        this.ghostVisuals = ghostVisuals;
        GetGhost(mediaResponseModel).Forget();

        // ghostModel = original.ghostModel;
        // cameraManModel = original.cameraManModel;
        // ghostFader = original.ghostFader;
        // nameDisplay = original.nameDisplay;
        // steerLeft = original.steerLeftTransform;
        // steerRight = original.steerRightTransform;
        //
        // original.ghostFader.gameObject.SetActive(true);
        // original.ghostFader.enabled = false;
        // original.nameDisplay.DoSetup(name, recordModel.User.SteamId);
        // if (color.HasValue)
        //     original.nameDisplay.theDisplayName.color = color.Value;
        //
        // original.isDead = false;
        // original.isPhotoMode = false;
        // original.frontLeftDead = false;
        // original.frontRightDead = false;
        // original.rearLeftDead = false;
        // original.rearRightDead = false;
        // original.armsUp = false;
        // original.brake = false;
    }

    private async UniTaskVoid GetGhost(MediaResponseModel mediaResponseModel)
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Zeepkist",
            "GTR",
            "Ghosts");

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        string filename = Path.Combine(folder, $"{mediaResponseModel.Id}.bin");

        byte[] buffer;

        if (File.Exists(filename))
        {
            this.Logger().LogInfo($"Reading ghost from disk: {filename}");
            buffer = await File.ReadAllBytesAsync(filename);
        }
        else
        {
            this.Logger().LogInfo($"Download ghost: {mediaResponseModel.GhostUrl}");
            using (HttpClient client = new())
            {
                HttpResponseMessage response = await client.GetAsync(mediaResponseModel.GhostUrl);
                if (response.IsSuccessStatusCode)
                {
                    buffer = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(filename, buffer);
                }
                else
                {
                    Logger.LogError($"Unable to download ghost: {response.StatusCode}");
                    return;
                }
            }
        }

        if (gameObject == null)
            return;

        ProcessGhost(buffer);
    }

    private void ProcessGhost(byte[] buffer)
    {
        Logger.LogInfo("Processing ghost");
        ghostReader = ReaderRepository.GetReader(buffer);
        ghostReader.Read(buffer);
        ghostReader.GetFrameData(0, ref frameData);

        if (frameData != null)
        {
            tr.position = frameData.Position;
            tr.rotation = frameData.Rotation;
        }

        if (gameObject == null)
            return;

        ghostVisuals.Setup(SoapboxId, HatId, ColorId);

        downloadedGhost = true;
    }

    public static Quaternion SmoothDampQuaternion(
        Quaternion current,
        Quaternion target,
        ref Vector3 currentVelocity,
        float smoothTime
    )
    {
        if (Time.deltaTime == 0) return current;
        if (smoothTime == 0) return target;
        Vector3 c = current.eulerAngles;
        Vector3 t = target.eulerAngles;
        return Quaternion.Euler(
            Mathf.SmoothDampAngle(c.x, t.x, ref currentVelocity.x, smoothTime),
            Mathf.SmoothDampAngle(c.y, t.y, ref currentVelocity.y, smoothTime),
            Mathf.SmoothDampAngle(c.z, t.z, ref currentVelocity.z, smoothTime)
        );
    }

    private void Update()
    {
        if (!downloadedGhost)
            return;

        ghostReader.GetFrameData(Ticker, ref frameData);

        if (frameData != null)
        {
            targetPosition = frameData.Position;
            targetRotation = frameData.Rotation;

            ghostVisuals.ProcessFrame(frameData);
        }

        tr.position = Vector3.SmoothDamp(tr.position, targetPosition, ref positionVelocity, 0.05f);
        tr.rotation = SmoothDampQuaternion(tr.rotation, targetRotation, ref rotationVelocity, 0.05f);
    }

    // private void UpdateGhostVisuals()
    // {
    //     const float minDistance = 2.5f;
    //     const float maxDistance = 8f;
    //     const float maxAlpha = 1;
    //
    //     float playerDist = PlayerManager.Instance.currentMaster.isPhotoMode
    //         ? 1000
    //         : Vector3.Distance(transform.position,
    //             PlayerManager.Instance.currentMaster.carSetups[0].transform.position);
    //
    //     if (playerDist < 3f)
    //     {
    //         DisableRenderers();
    //         return;
    //     }
    //
    //     EnableRenderers();
    //
    //     float fadeAmount = Mathf.Lerp(0f, maxAlpha, Mathf.InverseLerp(minDistance, maxDistance, playerDist));
    //
    //     nameDisplay.theDisplayName.color = nameDisplay.theDisplayName.color with
    //     {
    //         a = Mathf.InverseLerp(minDistance, maxDistance, playerDist)
    //     };
    //
    //     float r = 1;
    //     float g = 1;
    //     float b = 1;
    //
    //     if (PlayerManager.Instance.objectsList.wardrobe.everyColor.ContainsKey(ColorId))
    //     {
    //         r = ((CosmeticColor)PlayerManager.Instance.objectsList.wardrobe.everyColor[ColorId]).skinColor.color.r;
    //         g = ((CosmeticColor)PlayerManager.Instance.objectsList.wardrobe.everyColor[ColorId]).skinColor.color.g;
    //         b = ((CosmeticColor)PlayerManager.Instance.objectsList.wardrobe.everyColor[ColorId]).skinColor.color.b;
    //     }
    //
    //     if (!hasSetMaterials)
    //     {
    //         ghostFader.localMaterial = new Material(Shader.Find("Standard"));
    //         ToFadeMode(ghostFader.localMaterial);
    //     }
    //
    //     ghostFader.localMaterial.color = new Color(r, g, b, fadeAmount);
    //
    //     if (hasSetMaterials)
    //         return;
    //
    //     foreach (Renderer renderer in ghostFader.modelRenderers)
    //     {
    //         Material[] materials = renderer.sharedMaterials;
    //
    //         for (int i = 0; i < materials.Length; i++)
    //         {
    //             materials[i] = ghostFader.localMaterial;
    //         }
    //
    //         renderer.sharedMaterials = materials;
    //     }
    //
    //     hasSetMaterials = true;
    // }
    //
    // private static void ToFadeMode(Material material)
    // {
    //     material.SetOverrideTag("RenderType", "Transparent");
    //     material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
    //     material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
    //     material.SetInt("_ZWrite", 0);
    //     material.DisableKeyword("_ALPHATEST_ON");
    //     material.EnableKeyword("_ALPHABLEND_ON");
    //     material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    //     material.renderQueue = (int)RenderQueue.Transparent;
    // }
    //
    // private void DisableRenderers()
    // {
    //     foreach (Renderer renderer in ghostFader.modelRenderers)
    //     {
    //         renderer.enabled = false;
    //     }
    //
    //     nameDisplay.theDisplayName.color = nameDisplay.theDisplayName.color with
    //     {
    //         a = 0
    //     };
    // }
    //
    // private void EnableRenderers()
    // {
    //     foreach (Renderer renderer in ghostFader.modelRenderers)
    //     {
    //         renderer.enabled = true;
    //     }
    // }
}
