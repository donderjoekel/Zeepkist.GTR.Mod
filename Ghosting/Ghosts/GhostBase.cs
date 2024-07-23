using TNRD.Zeepkist.GTR.Ghosting.Playback;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public abstract class GhostBase : IGhost
{
    private int _updateFrame;
    private int _fixedUpdateFrame;
    private float _time;

    protected abstract int FrameCount { get; }

    protected GhostData Ghost { get; private set; }

    public abstract Color Color { get; }

    public void Initialize(GhostData ghost)
    {
        Ghost = ghost;
    }

    public abstract void ApplyCosmetics(string steamName);

    protected void SetupCosmetics(CosmeticsV16 cosmetics, string steamName, ulong steamId)
    {
        Ghost.Visuals.Cosmetics = cosmetics;
        Ghost.Visuals.GhostModel.DoCarSetup(Ghost.Visuals.Cosmetics, true, true, false);
        Ghost.Visuals.GhostModel.SetupParaglider(Ghost.Visuals.Cosmetics.GetParaglider());
        Ghost.Visuals.GhostModel.DisableParaglider();
        Ghost.Visuals.HornHolder.SetActive(false);
        Ghost.Visuals.NameDisplay.kingHat.gameObject.SetActive(false);
        Ghost.Visuals.NameDisplay.DoSetup(steamName, steamId.ToString(), Color);

        if (Ghost.Visuals.Cosmetics.horn != null)
        {
            Ghost.CurrentHornType = Ghost.Visuals.Cosmetics.horn.hornType;
            Ghost.CurrentHornIsOneShot = Ghost.CurrentHornType == FMOD_HornsIndex.HornType.fallback ||
                                         Ghost.Visuals.Cosmetics.horn.currentHornIsOneShot;
            Ghost.CurrentHornTone = Ghost.Visuals.Cosmetics.horn.tone;
        }
        else
        {
            Ghost.CurrentHornType = FMOD_HornsIndex.HornType.fallback;
            Ghost.CurrentHornIsOneShot = true;
            Ghost.CurrentHornTone = 0;
        }
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

    public void Update()
    {
        if (_updateFrame >= FrameCount - 1)
            return;

        _time += Time.deltaTime;

        IFrame previousFrame = null;
        IFrame nextFrame = null;

        for (int i = _updateFrame; i < FrameCount; i++)
        {
            IFrame frame = GetFrame(i);
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

        if (_updateFrame >= FrameCount - 1)
            return;

        float t = Mathf.InverseLerp(previousFrame.Time, nextFrame.Time, _time);
        Vector3 position = Vector3.Lerp(previousFrame.Position, nextFrame.Position, t);
        Quaternion rotation = Quaternion.Slerp(previousFrame.Rotation, nextFrame.Rotation, t);
        Ghost.GameObject.transform.SetPositionAndRotation(position, rotation);

        OnUpdate();
    }

    protected virtual void OnUpdate()
    {
    }

    public void FixedUpdate()
    {
        if (_fixedUpdateFrame >= FrameCount - 1)
            return;

        OnFixedUpdate(_fixedUpdateFrame);

        _fixedUpdateFrame++;
    }

    protected virtual void OnFixedUpdate(int fixedUpdateFrame)
    {
    }

    protected abstract IFrame GetFrame(int index);
}
