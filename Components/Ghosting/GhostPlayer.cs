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
        PlayerManager.Instance.currentMaster.PlayersReady.FirstOrDefault()?.ticker.what_ticker + 0.05f ?? 0;

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
        int ghostId,
        string ghostUrl,
        GhostVisuals ghostVisuals
    )
    {
        this.ghostVisuals = ghostVisuals;
        GetGhost(ghostId, ghostUrl).Forget();
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
        GetGhost(mediaResponseModel.Id, mediaResponseModel.GhostUrl).Forget();

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

    // private async UniTaskVoid GetGhost(MediaResponseModel mediaResponseModel)
    private async UniTaskVoid GetGhost(int ghostId, string ghostUrl)
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Zeepkist",
            "GTR",
            "Ghosts");

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        string filename = Path.Combine(folder, $"{ghostId}.bin");

        byte[] buffer;

        if (File.Exists(filename))
        {
            this.Logger().LogInfo($"Reading ghost from disk: {filename}");
            buffer = await File.ReadAllBytesAsync(filename);
        }
        else
        {
            this.Logger().LogInfo($"Download ghost: {ghostUrl}");
            using (HttpClient client = new())
            {
                HttpResponseMessage response = await client.GetAsync(ghostUrl);
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
}
