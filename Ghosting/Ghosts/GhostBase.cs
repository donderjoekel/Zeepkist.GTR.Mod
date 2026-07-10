using TNRD.Zeepkist.GTR.Ghosting.Playback;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public abstract class GhostBase : IGhost
{
    private readonly GhostTimingService _timingService;
    private int _updateFrame;
    private int _fixedUpdateFrame;

    protected abstract int FrameCount { get; }

    protected GhostData Ghost { get; private set; }

    public abstract Color Color { get; }

    public float Duration => FrameCount > 0 ? GetFrame(FrameCount - 1).Time : 0f;

    protected GhostBase(GhostTimingService timingService)
    {
        _timingService = timingService;
    }

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

    public virtual void Start()
    {
        _updateFrame = 0;
        _fixedUpdateFrame = 0;

        IFrame frame = GetFrame(0);
        Ghost.GameObject.transform.SetPositionAndRotation(frame.Position, frame.Rotation);
        AlignBulkCharacterToGhost();
        Ghost.SetPlaybackVisible(true);
    }

    public virtual void Stop()
    {
        _updateFrame = 0;
        _fixedUpdateFrame = 0;
        Ghost.SetPlaybackVisible(false);
    }

    public void Seek(float time)
    {
        _fixedUpdateFrame = 0;

        if (FrameCount == 0)
            return;

        ApplyPositionAtTime(time);
    }

    public void Update()
    {
        if (_updateFrame >= FrameCount - 1)
            return;

        IFrame previousFrame = null;
        IFrame nextFrame = null;

        for (int i = _updateFrame; i < FrameCount; i++)
        {
            IFrame frame = GetFrame(i);
            if (frame.Time < _timingService.CurrentTime)
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

        float t = Mathf.InverseLerp(previousFrame.Time, nextFrame.Time, _timingService.CurrentTime);
        ApplyInterpolatedTransform(previousFrame, nextFrame, _timingService.CurrentTime);
        AlignBulkCharacterToGhost();
        Ghost.SetPlaybackVisible(true);

        OnUpdate(previousFrame, nextFrame, t);
    }

    protected virtual void OnUpdate(IFrame previousFrame, IFrame nextFrame, float t)
    {
    }

    protected void AlignBulkCharacterToGhost()
    {
        if (Ghost == null)
            return;

        AlignBulkCharacterTransform(Ghost.BulkCharacterGameObject?.transform);
        AlignBulkCharacterTransform(Ghost.BulkArmsUpCharacterGameObject?.transform);
    }

    private void AlignBulkCharacterTransform(Transform transform)
    {
        if (transform == null)
            return;

        transform.SetPositionAndRotation(
            Ghost.GameObject.transform.position,
            Ghost.GameObject.transform.rotation);
    }

    public void FixedUpdate()
    {
        if (_fixedUpdateFrame >= FrameCount - 1)
            return;

        if (Ghost.VisualProfile == GhostVisualProfile.Full)
            OnFixedUpdate(_fixedUpdateFrame);

        _fixedUpdateFrame++;
    }

    protected virtual void OnFixedUpdate(int fixedUpdateFrame)
    {
    }

    protected abstract IFrame GetFrame(int index);

    private void ApplyPositionAtTime(float time)
    {
        if (FrameCount == 0)
            return;

        IFrame firstFrame = GetFrame(0);
        if (time <= firstFrame.Time)
        {
            Ghost.GameObject.transform.SetPositionAndRotation(firstFrame.Position, firstFrame.Rotation);
            _updateFrame = 0;
            AlignBulkCharacterToGhost();
            return;
        }

        IFrame lastFrame = GetFrame(FrameCount - 1);
        if (time >= lastFrame.Time)
        {
            Ghost.GameObject.transform.SetPositionAndRotation(lastFrame.Position, lastFrame.Rotation);
            _updateFrame = FrameCount - 1;
            AlignBulkCharacterToGhost();
            return;
        }

        int nextIndex = GhostFrameSearch.FindFirstFrameIndexAtOrAfterTime(
            FrameCount,
            time,
            index => GetFrame(index).Time);
        IFrame previousFrame = GetFrame(nextIndex - 1);
        IFrame nextFrame = GetFrame(nextIndex);
        _updateFrame = nextIndex - 1;

        ApplyInterpolatedTransform(previousFrame, nextFrame, time);
        AlignBulkCharacterToGhost();
    }

    private void ApplyInterpolatedTransform(IFrame previousFrame, IFrame nextFrame, float time)
    {
        float t = Mathf.InverseLerp(previousFrame.Time, nextFrame.Time, time);
        Vector3 position = Vector3.Lerp(previousFrame.Position, nextFrame.Position, t);
        Quaternion rotation = Quaternion.Slerp(previousFrame.Rotation, nextFrame.Rotation, t);
        Ghost.GameObject.transform.SetPositionAndRotation(position, rotation);
    }
}
