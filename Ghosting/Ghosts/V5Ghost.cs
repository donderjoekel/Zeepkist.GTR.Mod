using System.Collections.Generic;
using TNRD.Zeepkist.GTR.Ghosting.Recording;
using UnityEngine;
using ZeepkistNetworking;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public partial class V5Ghost : GhostBase
{
    private readonly string _taggedUsername;
    private readonly Color _color;
    private readonly ulong _steamId;
    private readonly CosmeticIDs _cosmeticIds;
    private readonly List<Frame> _frames;

    public V5Ghost(string taggedUsername, Color color, ulong steamId, CosmeticIDs cosmeticIds, List<Frame> frames)
    {
        _taggedUsername = taggedUsername;
        _color = color;
        _steamId = steamId;
        _cosmeticIds = cosmeticIds;
        _frames = frames;
    }

    protected override int FrameCount => _frames.Count;
    public override Color Color => _color;

    public override void ApplyCosmetics(string steamName)
    {
        CosmeticsV16 cosmetics = new();
        cosmetics.IDsToCosmeticsWithSteamID(_cosmeticIds, _steamId);
        SetupCosmetics(cosmetics, steamName, _steamId);
    }

    protected override IFrame GetFrame(int index)
    {
        return _frames[index];
    }

    protected override void OnFixedUpdate(int fixedUpdateFrame)
    {
        if (fixedUpdateFrame <= 0)
            return;

        Frame previousFrame = _frames[fixedUpdateFrame - 1];
        Frame frame = _frames[fixedUpdateFrame];

        HandleHorn(previousFrame, frame);
        HandleNone(previousFrame, frame);
        HandleSoap(previousFrame, frame);
        HandleOffroad(previousFrame, frame);
        HandleParaglider(previousFrame, frame);
    }

    private void HandleHorn(Frame previousFrame, Frame frame)
    {
        bool currentHorn = frame.InputFlags.HasFlagFast(InputFlags.Horn);
        bool previousHorn = previousFrame.InputFlags.HasFlagFast(InputFlags.Horn);
        Ghost.Visuals.HornHolder.SetActive(currentHorn);

        if (currentHorn == previousHorn)
            return;

        if (currentHorn)
        {
            if (Ghost.CurrentHornIsOneShot)
            {
                Ghost.CurrentHorn?.Cleanup();
                Ghost.CurrentHorn?.Stop();
            }

            Ghost.CurrentHorn = PlayerManager.Instance.hornsIndex.PlayHornPlayback(
                Ghost.CurrentHornType,
                Ghost.Visuals.GhostModel.transform,
                Ghost.CurrentHornTone);
        }
        else
        {
            if (Ghost.CurrentHornIsOneShot)
                return;

            Ghost.CurrentHorn?.Stop();
            Ghost.CurrentHorn?.Cleanup();
        }
    }

    private void HandleNone(Frame previousFrame, Frame frame)
    {
        bool currentNone = !frame.SoapboxFlags.HasFlagFast(SoapboxFlags.Soap) &&
                           !frame.SoapboxFlags.HasFlagFast(SoapboxFlags.Offroad) &&
                           !frame.SoapboxFlags.HasFlagFast(SoapboxFlags.Paraglider);

        bool previousNone = !previousFrame.SoapboxFlags.HasFlagFast(SoapboxFlags.Soap) &&
                            !previousFrame.SoapboxFlags.HasFlagFast(SoapboxFlags.Offroad) &&
                            !previousFrame.SoapboxFlags.HasFlagFast(SoapboxFlags.Paraglider);

        if (currentNone == previousNone)
            return;

        if (currentNone)
        {
            foreach (Ghost_AnimateWheel_v16 wheel in Ghost.Visuals.Wheels)
            {
                wheel.offroadWheelModel.gameObject.SetActive(false);
                wheel.soapwheelModel.gameObject.SetActive(false);
                wheel.wheelModel.gameObject.SetActive(true);
            }
        }
    }

    private void HandleSoap(Frame previousFrame, Frame frame)
    {
        bool currentSoap = frame.SoapboxFlags.HasFlagFast(SoapboxFlags.Soap);
        bool previousSoap = previousFrame.SoapboxFlags.HasFlagFast(SoapboxFlags.Soap);

        if (currentSoap == previousSoap)
            return;

        if (currentSoap)
        {
            foreach (Ghost_AnimateWheel_v16 wheel in Ghost.Visuals.Wheels)
            {
                wheel.wheelModel.gameObject.SetActive(false);
                wheel.offroadWheelModel.gameObject.SetActive(false);
                wheel.soapwheelModel.gameObject.SetActive(true);
            }
        }
    }

    private void HandleOffroad(Frame previousFrame, Frame frame)
    {
        bool currentOffroad = frame.SoapboxFlags.HasFlagFast(SoapboxFlags.Offroad);
        bool previousOffroad = previousFrame.SoapboxFlags.HasFlagFast(SoapboxFlags.Offroad);

        if (currentOffroad == previousOffroad)
            return;

        if (currentOffroad)
        {
            foreach (Ghost_AnimateWheel_v16 wheel in Ghost.Visuals.Wheels)
            {
                wheel.wheelModel.gameObject.SetActive(false);
                wheel.soapwheelModel.gameObject.SetActive(false);
                wheel.offroadWheelModel.gameObject.SetActive(true);
            }
        }
    }

    private void HandleParaglider(Frame previousFrame, Frame frame)
    {
        bool currentParaglider = frame.SoapboxFlags.HasFlagFast(SoapboxFlags.Paraglider);
        bool previousParaglider = previousFrame.SoapboxFlags.HasFlagFast(SoapboxFlags.Paraglider);

        if (currentParaglider == previousParaglider)
            return;

        if (currentParaglider)
        {
            Ghost.Visuals.GhostModel.EnableParaglider();
        }
        else
        {
            Ghost.Visuals.GhostModel.DisableParaglider();
        }
    }
}
