namespace TNRD.Zeepkist.GTR.Mod.Stats;

internal abstract class RacingTimeTrackerBase : RacingTrackerBase
{
    protected float Time { get; private set; }

    private bool previousTracking;

    public sealed override void Reset()
    {
        Time = 0;
        OnReset();
    }

    protected virtual void OnReset()
    {

    }

    protected sealed override void OnTick()
    {
        bool previousShouldTrack = previousTracking;
        bool shouldTrack = ShouldTrackTime();
        previousTracking = shouldTrack;

        if (!shouldTrack)
            return;

        if (!previousShouldTrack)
            ResetValues();

        float delta = UnityEngine.Time.deltaTime;
        Time += delta;
        OnTimeChanged(delta, Time);
    }

    protected abstract bool ShouldTrackTime();

    protected virtual void ResetValues()
    {
    }

    protected virtual void OnTimeChanged(float delta, float total)
    {
    }
}
