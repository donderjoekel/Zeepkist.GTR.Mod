using UnityEngine;

namespace TNRD.Zeepkist.GTR.Mod.Stats;

internal abstract class RacingDistanceTrackerBase : RacingTrackerBase
{
    protected float Distance { get; private set; }

    private Vector3? previousPosition;
    private bool previousTracking;

    public sealed override void Reset()
    {
        Distance = 0;
        previousPosition = null;
        OnReset();
    }

    protected virtual void OnReset()
    {
    }

    protected sealed override void OnTick()
    {
        bool previousShouldTrackDistance = previousTracking;
        bool shouldTrackDistance = ShouldTrackDistance();
        previousTracking = shouldTrackDistance;

        if (!shouldTrackDistance)
            return;

        if (!previousShouldTrackDistance)
            ResetValues();

        Vector3? previous = previousPosition;
        Vector3 current = GetPosition();
        previousPosition = current;

        if (SetupCar.cc.GetLocalVelocity().magnitude < 0.1f) // Arbitrary value
            return;

        if (!previous.HasValue)
            return;

        float delta = Vector3.Distance(previous.Value, current);

        if (delta > 2.5f)
        {
            Logger.LogWarning("Delta is too high! (" + delta + ")");
            return;
        }

        Distance += delta;
        OnDistanceChanged(delta, Distance);
    }

    protected abstract bool ShouldTrackDistance();

    protected virtual void ResetValues()
    {
    }

    protected virtual Vector3 GetPosition()
    {
        return SetupCar.transform.position;
    }

    protected virtual void OnDistanceChanged(float delta, float total)
    {
    }
}
