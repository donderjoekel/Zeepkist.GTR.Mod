using System;
using System.Collections.Generic;
using System.Linq;
using TNRD.Zeepkist.GTR.Mod.Components.Ghosting.Readers;
using UnityEngine;
using ZeepSDK.Cosmetics;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting;

public partial class GhostVisuals : MonoBehaviourWithLogging
{
    private static NetworkedGhostSpawner _networkedGhostSpawner;

    private static NetworkedGhostSpawner NetworkedGhostSpawner
    {
        get
        {
            if (_networkedGhostSpawner == null)
            {
                _networkedGhostSpawner = FindObjectOfType<NetworkedGhostSpawner>();
            }

            return _networkedGhostSpawner;
        }
    }

    private readonly HashSet<MaterialData> materials = new();

    private SetupModelCar ghostModel;
    private DisplayPlayerName nameDisplay;
    private FakeRagdollManagerOnline fakeRagdoll;
    private Ghost_AnimateWheel[] ghostWheels;

    private Transform steerLeft;
    private Transform steerRight;

    private int colorId;

    private Vector3 cameraPosition;
    private Vector3 previousPosition;
    private float velocity;

    private bool setPrevious = true;
    private bool hasSetup;
    private bool IsGhost => Plugin.ConfigShowGhostTransparent.Value;

    protected override void Awake()
    {
        base.Awake();

        ghostModel = Instantiate(NetworkedGhostSpawner.zeepkistGhostPrefab.ghostModel, transform);

        ghostWheels = ghostModel.GetComponentsInChildren<Ghost_AnimateWheel>();
        foreach (Ghost_AnimateWheel ghostWheel in ghostWheels)
        {
            ghostWheel.enabled = false;
        }

        steerLeft = ghostModel.transform.Find("Wheels/Steer Left Front");
        steerRight = ghostModel.transform.Find("Wheels/Steer Right Front");

        nameDisplay = Instantiate(NetworkedGhostSpawner.zeepkistGhostPrefab.nameDisplay,
            NetworkedGhostSpawner.zeepkistGhostPrefab.nameDisplay.transform.position,
            NetworkedGhostSpawner.zeepkistGhostPrefab.nameDisplay.transform.rotation,
            transform);
        nameDisplay.enabled = false;

        if (!PlayerManager.Instance.instellingen.Settings.online_disable_physics)
        {
            fakeRagdoll = Instantiate(NetworkedGhostSpawner.zeepkistGhostPrefab.fakeRagdoll, transform);
            fakeRagdoll.physicalTransform = ghostModel.transform;
            fakeRagdoll.visualTop = ghostModel.transform.Find("Character/Armature/Top");
            fakeRagdoll.visualLeftArm = ghostModel.transform.Find("Character/Left Arm");
            fakeRagdoll.visualRightArm = ghostModel.transform.Find("Character/Right Arm");
            fakeRagdoll.visualBottom = ghostModel.transform.Find("Character/Armature/Bottom");
            fakeRagdoll.visualCharacter = ghostModel.transform.Find("Character").gameObject;

            fakeRagdoll.DoSetup();
            fakeRagdoll.SetAlive();
            fakeRagdoll.enabled = false;
        }

        Plugin.ConfigShowGhosts.SettingChanged += OnShowGhostsChanged;
        Plugin.ConfigShowGhostNames.SettingChanged += OnShowNamesChanged;
        Plugin.ConfigShowGhostTransparent.SettingChanged += OnShowGhostTransparentChanged;
    }

    private void OnDestroy()
    {
        Plugin.ConfigShowGhosts.SettingChanged -= OnShowGhostsChanged;
        Plugin.ConfigShowGhostNames.SettingChanged -= OnShowNamesChanged;
        Plugin.ConfigShowGhostTransparent.SettingChanged -= OnShowGhostTransparentChanged;
    }

    private void OnShowGhostsChanged(object sender, EventArgs e)
    {
        ghostModel.gameObject.SetActive(Plugin.ConfigShowGhosts.Value);
    }

    private void OnShowNamesChanged(object sender, EventArgs e)
    {
        nameDisplay.gameObject.SetActive(Plugin.ConfigShowGhostNames.Value);
    }

    private void OnShowGhostTransparentChanged(object sender, EventArgs e)
    {
        foreach (MaterialData materialData in materials)
        {
            materialData.SetGhost(Plugin.ConfigShowGhostTransparent.Value);
        }
    }

    public void Initialize(string name, string steamId, Color? color)
    {
        nameDisplay.DoSetup(name, steamId);
        if (color.HasValue) nameDisplay.theDisplayName.color = color.Value;
        nameDisplay.gameObject.SetActive(Plugin.ConfigShowGhostNames.Value);
    }

    public void Setup(int soapboxId, int hatId, int colorId)
    {
        this.colorId = colorId;

        ghostModel.DoCarSetup(
            CosmeticsApi.GetSoapbox(soapboxId, false),
            CosmeticsApi.GetHat(hatId, false),
            CosmeticsApi.GetColor(colorId, false),
            true,
            true,
            false);

        AddRenderersRecursive(ghostModel.transform);

        // Set the color of the ghosts once already
        Color color = new(1, 1, 1, 0);

        if (PlayerManager.Instance.objectsList.wardrobe.everyColor.ContainsKey(colorId))
        {
            Color skinColor = CosmeticsApi.GetColor(colorId, false).skinColor.color;
            color.r = skinColor.r;
            color.g = skinColor.g;
            color.b = skinColor.b;
        }

        foreach (MaterialData materialData in materials)
        {
            materialData.SetGhostColor(color);
        }

        OnShowGhostsChanged(this, null);
        OnShowNamesChanged(this, null);
        OnShowGhostTransparentChanged(this, null);
        hasSetup = true;
    }

    private void AddRenderersRecursive(Transform t)
    {
        foreach (Transform child in t)
        {
            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer != null)
            {
                MaterialData materialData = new(renderer);
                materialData.SetGhost(IsGhost);
                materials.Add(materialData);
            }

            if (child.childCount > 0) AddRenderersRecursive(child);
        }
    }

    public void ProcessFrame(FrameData frameData)
    {
        Vector3 currentPosition = frameData.Position;
        velocity = ((currentPosition - previousPosition) / Time.fixedDeltaTime).magnitude;

        if (setPrevious)
        {
            previousPosition = frameData.Position;
        }

        setPrevious = !setPrevious;

        ProcessSteering(frameData);
        ProcessRagdoll(frameData);
        ProcessWheels(frameData);
    }

    private void ProcessSteering(FrameData frameData)
    {
        steerLeft.localEulerAngles = new Vector3(0, frameData.Steering * 20, 0);
        steerRight.localEulerAngles = new Vector3(0, frameData.Steering * 20, 0);
    }

    private void ProcessRagdoll(FrameData frameData)
    {
        if (fakeRagdoll == null)
            return;

        Vector3 move = Vector3.Lerp(fakeRagdoll.baseRigidbody.position,
            fakeRagdoll.physicalTransform.position + fakeRagdoll.offset,
            0.2f * 60 * Time.fixedDeltaTime);

        float maxMoveDistance = 0.2f;

        if (Vector3.Distance(fakeRagdoll.baseRigidbody.position, move) > maxMoveDistance) // max distance
        {
            move = Vector3.ClampMagnitude(move - fakeRagdoll.baseRigidbody.position, maxMoveDistance) +
                   fakeRagdoll.baseRigidbody.position;
        }

        fakeRagdoll.baseRigidbody.MovePosition(move);
        fakeRagdoll.baseRigidbody.MoveRotation(fakeRagdoll.physicalTransform.rotation);

        if (!frameData.ArmsUp)
            return;

        if (fakeRagdoll.baseLeftArm != null)
        {
            fakeRagdoll.baseLeftArm.AddForce(ghostModel.transform.up * 30);
        }

        if (fakeRagdoll.baseRightArm != null)
        {
            fakeRagdoll.baseRightArm.AddForce(ghostModel.transform.up * 30);
        }
    }

    private void ProcessWheels(FrameData frameData)
    {
        foreach (Ghost_AnimateWheel ghostWheel in ghostWheels)
        {
            ProcessWheel(frameData, ghostWheel);
        }
    }

    private void ProcessWheel(FrameData frameData, Ghost_AnimateWheel ghostWheel)
    {
        if (ghostWheel.physicsScene.Raycast(ghostWheel.transform.position,
                -ghostModel.transform.up,
                out RaycastHit hit,
                ghostWheel.raycastLength + ghostWheel.wheelRadius,
                ghostWheel.raymask))
        {
            ghostWheel.isGrounded = true;
            ghostWheel.wheel.transform.position = hit.point + ghostModel.transform.up * ghostWheel.wheelRadius;
        }
        else
        {
            ghostWheel.isGrounded = false;
            ghostWheel.wheel.transform.localPosition = ghostWheel.defaultWheelPosition;
        }

        ghostWheel.wheel_soapy.transform.position = ghostWheel.wheel.transform.position;
        ghostWheel.rps = velocity / ghostWheel.wheelRadius * ghostWheel.toDegrees;

        if (frameData.IsBraking && ghostWheel.isBrakeWheel) ghostWheel.rps = 0;

        ghostWheel.rotation = new Vector3(ghostWheel.rotationAxis.x * ghostWheel.rps,
            ghostWheel.rotationAxis.y * ghostWheel.rps,
            ghostWheel.rotationAxis.z * ghostWheel.rps);

        if (float.IsInfinity(ghostWheel.rotation.z) || float.IsNaN(ghostWheel.rotation.z))
        {
            ghostWheel.rotation = new Vector3(0, 0, 0);
        }

        ghostWheel.wheel.Rotate(ghostWheel.rotation * Time.deltaTime);
        ghostWheel.suspensionRod.LookAt(ghostWheel.wheel, ghostModel.transform.up);
    }

    private void Update()
    {
        if (!hasSetup)
            return;

        UpdateNameDisplay();
        UpdateRagdoll();

        try
        {
            UpdateRenderers();
        }
        catch (ArgumentOutOfRangeException)
        {
            // This happens when the ghost gets destroyed
        }
    }

    private void UpdateNameDisplay()
    {
        UpdateCameraPosition();

        Transform nameDisplayTransform = nameDisplay.transform;

        nameDisplayTransform.position = transform.position + Vector3.up * 2.5f;
        nameDisplayTransform.LookAt(cameraPosition);
        nameDisplayTransform.LookAt(nameDisplayTransform.position -
                                    nameDisplayTransform.forward);
    }

    private void UpdateCameraPosition()
    {
        GameMaster currentMaster = PlayerManager.Instance.currentMaster;

        if (currentMaster == null)
            return;

        if (currentMaster.isPhotoMode)
        {
            cameraPosition = currentMaster.flyingCamera.GetCameraPosition();
        }
        else
        {
            if (currentMaster.carSetups.Count > 0)
            {
                cameraPosition = currentMaster.carSetups[0].theCamera.transform.position;
            }
        }
    }

    private void UpdateRagdoll()
    {
        if (fakeRagdoll == null)
            return;

        fakeRagdoll.visualTop.localRotation = fakeRagdoll.baseTop.localRotation;
        fakeRagdoll.visualLeftArm.localRotation = fakeRagdoll.baseLeftArm.transform.localRotation;
        fakeRagdoll.visualRightArm.localRotation = fakeRagdoll.baseRightArm.transform.localRotation;

        fakeRagdoll.visualTop.localPosition = fakeRagdoll.baseTop.localPosition;
        fakeRagdoll.visualLeftArm.localPosition = fakeRagdoll.baseLeftArm.transform.localPosition;
        fakeRagdoll.visualRightArm.localPosition = fakeRagdoll.baseRightArm.transform.localPosition;

        if (fakeRagdoll.baseLeftLeg != null)
        {
            fakeRagdoll.visualLeftLeg.localPosition = fakeRagdoll.baseLeftLeg.transform.localPosition;
            fakeRagdoll.visualLeftLeg.localRotation = fakeRagdoll.baseLeftLeg.transform.localRotation;
        }

        if (fakeRagdoll.baseRightLeg != null)
        {
            fakeRagdoll.visualRightLeg.localPosition = fakeRagdoll.baseRightLeg.transform.localPosition;
            fakeRagdoll.visualRightLeg.localRotation = fakeRagdoll.baseRightLeg.transform.localRotation;
        }

        if (fakeRagdoll.baseBottom != null)
        {
            fakeRagdoll.visualBottom.localPosition = fakeRagdoll.baseBottom.transform.localPosition;
            fakeRagdoll.visualBottom.localRotation = fakeRagdoll.baseBottom.transform.localRotation;
        }

        fakeRagdoll.visualCharacter.SetActive(true);
    }

    private void EnableRenderers()
    {
        foreach (MaterialData materialData in materials)
        {
            materialData.Enable();
        }
    }

    private void DisableRenderers()
    {
        foreach (MaterialData materialData in materials)
        {
            materialData.Disable();
        }
    }

    private void UpdateRenderers()
    {
        const float minDistance = 2.5f;
        const float maxDistance = 8f;
        float maxAlpha = IsGhost ? 0.3f : 1f;

        float playerDistance = PlayerManager.Instance.currentMaster.isPhotoMode
            ? 1000
            : Vector3.Distance(transform.position,
                PlayerManager.Instance.currentMaster.carSetups[0].transform.position);

        float inverseLerp = Mathf.InverseLerp(minDistance, maxDistance, playerDistance);
        float fadeAmount = Mathf.Lerp(0, maxAlpha, inverseLerp);

        nameDisplay.theDisplayName.color = nameDisplay.theDisplayName.color with
        {
            a = inverseLerp
        };

        if (!IsGhost)
            return;

        if (playerDistance < 3f)
            return;

        Color color = new(1, 1, 1, fadeAmount);

        if (PlayerManager.Instance.objectsList.wardrobe.everyColor.ContainsKey(colorId))
        {
            Color skinColor = CosmeticsApi.GetColor(colorId, false).skinColor.color;
            color.r = skinColor.r;
            color.g = skinColor.g;
            color.b = skinColor.b;
        }

        foreach (MaterialData materialData in materials)
        {
            materialData.SetGhostColor(color);
        }
    }
}
