using System.Collections.Generic;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.Ghosting.Recording;
using UnityEngine;
using ZeepkistNetworking;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public class V5Ghost : IGhost
{
    private readonly string _taggedUsername;
    private readonly Color _color;
    private readonly ulong _steamId;
    private readonly CosmeticIDs _cosmeticIds;
    private readonly List<Frame> _frames;

    private int _updateFrame;
    private int _fixedUpdateFrame;
    private float _time;

    private GhostData _ghost;

    public V5Ghost(string taggedUsername, Color color, ulong steamId, CosmeticIDs cosmeticIds, List<Frame> frames)
    {
        _taggedUsername = taggedUsername;
        _color = color;
        _steamId = steamId;
        _cosmeticIds = cosmeticIds;
        _frames = frames;
    }

    public Color Color => _color;

    public void Initialize(GhostData ghost)
    {
        _ghost = ghost;
    }

    public void Start()
    {
        _time = 0;
        _updateFrame = 0;
        _fixedUpdateFrame = 0;
    }

    public void Stop()
    {
        _time = 0;
        _updateFrame = 0;
        _fixedUpdateFrame = 0;
    }

    public void ApplyCosmetics(string steamName)
    {
        _ghost.Visuals.Cosmetics.IDsToCosmeticsWithSteamID(_cosmeticIds, _steamId);
        _ghost.Visuals.GhostModel.DoCarSetup(_ghost.Visuals.Cosmetics, true, true, false);
        _ghost.Visuals.GhostModel.SetupParaglider(_ghost.Visuals.Cosmetics.GetParaglider());
        _ghost.Visuals.GhostModel.DisableParaglider();
        _ghost.Visuals.HornHolder.SetActive(false);
        _ghost.Visuals.NameDisplay.kingHat.gameObject.SetActive(false);
        _ghost.Visuals.NameDisplay.DoSetup(_taggedUsername, _steamId.ToString(), _color);

        if (_ghost.Visuals.Cosmetics.horn != null)
        {
            _ghost.CurrentHornType = _ghost.Visuals.Cosmetics.horn.hornType;
            _ghost.CurrentHornIsOneShot = _ghost.CurrentHornType == FMOD_HornsIndex.HornType.fallback ||
                                          _ghost.Visuals.Cosmetics.horn.currentHornIsOneShot;
            _ghost.CurrentHornTone = _ghost.Visuals.Cosmetics.horn.tone;
        }
        else
        {
            _ghost.CurrentHornType = FMOD_HornsIndex.HornType.fallback;
            _ghost.CurrentHornIsOneShot = true;
            _ghost.CurrentHornTone = 0;
        }
    }

    public void Update()
    {
        if (_updateFrame >= _frames.Count - 1)
            return;

        _time += Time.deltaTime;

        Frame previousFrame = null;
        Frame nextFrame = null;

        for (int i = _updateFrame; i < _frames.Count; i++)
        {
            Frame frame = _frames[i];
            if (frame.Time < _time)
            {
                previousFrame = frame;
                _updateFrame = i;
            }
            else
            {
                nextFrame = frame;
                break;
            }
        }

        if (previousFrame == null || nextFrame == null)
            return;

        if (_updateFrame >= _frames.Count - 1)
            return;

        float t = Mathf.InverseLerp(previousFrame.Time, nextFrame.Time, _time);
        Vector3 position = Vector3.Lerp(previousFrame.Position, nextFrame.Position, t);
        Quaternion rotation = Quaternion.Slerp(previousFrame.Rotation, nextFrame.Rotation, t);
        _ghost.GameObject.transform.SetPositionAndRotation(position, rotation);
    }

    public void FixedUpdate()
    {
        if (_fixedUpdateFrame >= _frames.Count - 1)
            return;

        if (_fixedUpdateFrame > 0)
        {
            Frame previousFrame = _frames[_fixedUpdateFrame - 1];
            Frame frame = _frames[_fixedUpdateFrame];

            HandleHorn(previousFrame, frame);
            HandleNone(previousFrame, frame);
            HandleSoap(previousFrame, frame);
            HandleOffroad(previousFrame, frame);
            HandleParaglider(previousFrame, frame);
        }

        _fixedUpdateFrame++;
    }

    private void HandleHorn(Frame previousFrame, Frame frame)
    {
        bool currentHorn = frame.InputFlags.HasFlagFast(InputFlags.Horn);
        bool previousHorn = previousFrame.InputFlags.HasFlagFast(InputFlags.Horn);
        _ghost.Visuals.HornHolder.SetActive(currentHorn);

        if (currentHorn == previousHorn)
            return;

        if (currentHorn)
        {
            if (_ghost.CurrentHornIsOneShot)
            {
                _ghost.CurrentHorn?.Cleanup();
                _ghost.CurrentHorn?.Stop();
            }

            _ghost.CurrentHorn = PlayerManager.Instance.hornsIndex.PlayHornPlayback(
                _ghost.CurrentHornType,
                _ghost.Visuals.GhostModel.transform,
                _ghost.CurrentHornTone);
        }
        else
        {
            if (_ghost.CurrentHornIsOneShot)
                return;

            _ghost.CurrentHorn?.Stop();
            _ghost.CurrentHorn?.Cleanup();
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
            _ghost.Visuals.GhostModel.EnableParaglider();
        }
        else
        {
            _ghost.Visuals.GhostModel.DisableParaglider();
        }
    }

    public class Frame
    {
        public Frame(
            float time,
            Vector3 position,
            Quaternion rotation,
            float speed,
            float steering,
            InputFlags inputFlags,
            SoapboxFlags soapboxFlags)
        {
            Time = time;
            Position = position;
            Rotation = rotation;
            Speed = speed;
            Steering = steering;
            InputFlags = inputFlags;
            SoapboxFlags = soapboxFlags;
        }

        public float Time { get; private set; }
        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; }
        public float Speed { get; private set; }
        public float Steering { get; private set; }
        public InputFlags InputFlags { get; private set; }
        public SoapboxFlags SoapboxFlags { get; private set; }
    }
}
