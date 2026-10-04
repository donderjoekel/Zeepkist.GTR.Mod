using System;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.Ghosting.Recording;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public abstract class GhostBase : IGhost
{
    protected enum FrameSampleKind
    {
        Start,
        Advance,
        Seek,
        Resume
    }

    private int _currentFrame = -1;
    private float _lastSampleTime;
    private bool _started;
    private bool _paused;
    private readonly Func<int, float> _getFrameTime;
    private BulkGhostModeState _bulkModeState;
    private GhostTimingService _timingService;

    protected abstract int FrameCount { get; }

    protected GhostData Ghost { get; private set; }

    public abstract Color Color { get; }

    public float Duration => FrameCount > 0 ? GetFrameTime(FrameCount - 1) : 0f;

    internal abstract GhostBase CreatePlayback();

    internal void Detach() => Ghost = null;

    protected GhostBase()
    {
        _getFrameTime = GetFrameTime;
    }

    internal bool TryGetAdjacentFrameTime(float currentTime, int direction, float timeEpsilon, out float adjacentTime)
    {
        return GhostFrameSearch.TryGetAdjacentFrameTime(
            FrameCount,
            currentTime,
            direction,
            timeEpsilon,
            _getFrameTime,
            out adjacentTime);
    }

    public void Initialize(
        GhostData ghost,
        BulkGhostModeState bulkModeState,
        GhostTimingService timingService)
    {
        Ghost = ghost;
        _bulkModeState = bulkModeState;
        _timingService = timingService;
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

    public virtual void Start(float time)
    {
        _started = true;
        _paused = false;
        _currentFrame = -1;
        ApplySample(time, FrameSampleKind.Start, true);
    }

    public virtual void Stop(float time)
    {
        _started = false;
        _paused = false;
        _currentFrame = -1;
        Ghost.SetPlaybackVisible(false);
        StopFullProfileEffects();
        OnStopped(time);
    }

    public void Seek(float time)
    {
        ApplySample(time, FrameSampleKind.Seek, true);
    }

    public void Pause(float time)
    {
        if (!_started || _paused)
            return;

        Sample(time);
        _paused = true;
        PauseFullProfileEffects();
        OnPaused(time);
    }

    public void Resume(float time)
    {
        if (!_started || !_paused)
            return;

        _paused = false;
        ApplySample(time, FrameSampleKind.Resume, true);
    }

    public void Sample(float time)
    {
        if (!_started || _paused)
            return;

        if (_currentFrame == FrameCount - 1 &&
            _currentFrame >= 0 &&
            _lastSampleTime >= GetFrameTime(_currentFrame) &&
            time >= GetFrameTime(_currentFrame))
        {
            return;
        }

        ApplySample(time, FrameSampleKind.Advance, false);
    }

    protected abstract void OnSample(int currentIndex, int nextIndex, float interpolation);

    protected virtual void OnFrameChanged(
        int previousFrameIndex,
        int currentFrameIndex,
        FrameSampleKind sampleKind)
    {
    }

    protected virtual void OnForwardFramesCrossed(int previousFrameIndex, int currentFrameIndex)
    {
    }

    protected virtual void OnPaused(float time)
    {
    }

    protected virtual void OnStopped(float time)
    {
    }

    protected void AlignBulkCharacterToGhost()
    {
        if (Ghost == null)
            return;

        AlignBulkCharacterTransform(Ghost.BulkCharacterGameObject?.transform);
        AlignBulkCharacterTransform(Ghost.BulkArmsUpCharacterGameObject?.transform);
    }

    protected void HandleHornEdge(bool previousHorn, bool currentHorn)
    {
        if (!TryGetFullVisuals(out GhostVisuals visuals))
            return;

        visuals.HornHolder.SetActive(currentHorn);
        if (currentHorn == previousHorn)
            return;

        if (currentHorn)
        {
            StopCurrentHorn();
            Ghost.CurrentHorn = PlayerManager.Instance.hornsIndex.PlayHornPlayback(
                Ghost.CurrentHornType,
                visuals.GhostModel.transform,
                Ghost.CurrentHornTone);
        }
        else if (!Ghost.CurrentHornIsOneShot)
        {
            StopCurrentHorn();
        }
    }

    protected bool HasFullVisuals =>
        Ghost?.VisualProfile == GhostVisualProfile.Full &&
        Ghost.Visuals != null &&
        !(_bulkModeState?.ShouldSkipFullProfilePlaybackEffects(
            _timingService?.IsManualPlaybackActive == true) ?? false);

    protected void SynchronizeHornState(bool hornHeld)
    {
        if (!TryGetFullVisuals(out GhostVisuals visuals))
            return;

        visuals.HornHolder.SetActive(hornHeld);
        StopCurrentHorn();
        if (hornHeld && IsPlaybackActive && !Ghost.CurrentHornIsOneShot)
        {
            Ghost.CurrentHorn = PlayerManager.Instance.hornsIndex.PlayHornPlayback(
                Ghost.CurrentHornType,
                visuals.GhostModel.transform,
                Ghost.CurrentHornTone);
        }
    }

    protected static byte GetWheelState(SoapboxFlags soapboxFlags)
    {
        if (soapboxFlags.HasFlagFast(SoapboxFlags.Offroad))
            return 2;

        return soapboxFlags.HasFlagFast(SoapboxFlags.Soap) ? (byte)1 : (byte)0;
    }

    protected void ApplyWheelState(SoapboxFlags soapboxFlags)
    {
        if (!TryGetFullVisuals(out GhostVisuals visuals))
            return;

        byte wheelState = GetWheelState(soapboxFlags);
        foreach (Ghost_AnimateWheel_v16 wheel in visuals.Wheels)
        {
            wheel.wheelModel.gameObject.SetActive(wheelState == 0);
            wheel.soapwheelModel.gameObject.SetActive(wheelState == 1);
            wheel.offroadWheelModel.gameObject.SetActive(wheelState == 2);
        }
    }

    protected void ApplyParagliderState(SoapboxFlags soapboxFlags)
    {
        if (!TryGetFullVisuals(out GhostVisuals visuals))
            return;

        if (soapboxFlags.HasFlagFast(SoapboxFlags.Paraglider))
            visuals.GhostModel.EnableParaglider();
        else
            visuals.GhostModel.DisableParaglider();
    }

    protected abstract float GetFrameTime(int index);

    protected abstract void ApplyFrameTransform(int currentIndex, int nextIndex, float interpolation);

    private bool IsPlaybackActive => _started && !_paused;

    private void ApplySample(float time, FrameSampleKind sampleKind, bool forceFrameChanged)
    {
        int previousFrameIndex = _currentFrame;
        bool monotonicAdvance = sampleKind == FrameSampleKind.Advance &&
                                previousFrameIndex >= 0 &&
                                time >= _lastSampleTime;
        GhostFrameSample sample;
        bool hasSample = monotonicAdvance
            ? GhostFrameSearch.TryGetFrameSample(
                FrameCount,
                time,
                previousFrameIndex,
                _getFrameTime,
                out sample)
            : GhostFrameSearch.TryGetFrameSample(
                FrameCount,
                time,
                _getFrameTime,
                out sample);
        if (!hasSample)
        {
            if (_started)
                Ghost.SetPlaybackVisible(false);
            return;
        }

        if (sampleKind == FrameSampleKind.Advance &&
            previousFrameIndex >= 0 &&
            (time < _lastSampleTime || sample.CurrentIndex < previousFrameIndex))
        {
            sampleKind = FrameSampleKind.Seek;
            forceFrameChanged = true;
        }

        ApplyFrameTransform(sample.CurrentIndex, sample.NextIndex, sample.Interpolation);
        AlignBulkCharacterToGhost();
        if (_started)
            Ghost.SetPlaybackVisible(true);

        bool frameChanged = forceFrameChanged || sample.CurrentIndex != previousFrameIndex;
        if (frameChanged &&
            sampleKind == FrameSampleKind.Advance &&
            previousFrameIndex >= 0 &&
            sample.CurrentIndex > previousFrameIndex)
        {
            OnForwardFramesCrossed(previousFrameIndex, sample.CurrentIndex);
        }

        if (frameChanged)
            OnFrameChanged(previousFrameIndex, sample.CurrentIndex, sampleKind);

        _currentFrame = sample.CurrentIndex;
        _lastSampleTime = time;
        OnSample(sample.CurrentIndex, sample.NextIndex, sample.Interpolation);
    }

    private void AlignBulkCharacterTransform(Transform transform)
    {
        if (transform == null)
            return;

        transform.SetPositionAndRotation(
            Ghost.GameObject.transform.position,
            Ghost.GameObject.transform.rotation);
    }

    private bool TryGetFullVisuals(out GhostVisuals visuals)
    {
        visuals = Ghost?.Visuals;
        return Ghost?.VisualProfile == GhostVisualProfile.Full && visuals != null;
    }

    private void PauseFullProfileEffects()
    {
        StopCurrentHorn();
    }

    private void StopFullProfileEffects()
    {
        if (TryGetFullVisuals(out GhostVisuals visuals))
            visuals.HornHolder.SetActive(false);

        StopCurrentHorn();
    }

    private void StopCurrentHorn()
    {
        Ghost?.CurrentHorn?.Stop();
        Ghost?.CurrentHorn?.Cleanup();
        if (Ghost != null)
            Ghost.CurrentHorn = null;
    }
}
