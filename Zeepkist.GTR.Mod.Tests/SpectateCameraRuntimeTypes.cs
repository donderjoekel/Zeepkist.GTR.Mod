// Native camera adapter: tests supply native/patch target rebuilding through RebuildTargets.
public sealed class SpectatorZeepkistTarget
{
    public UnityEngine.Transform transform;
    public string name;
    public object ghost;
}

public sealed class FlyingCameraScript
{
    public readonly List<SpectatorZeepkistTarget> targetList = new();
    public SpectatorZeepkistTarget currentTarget;
    public Action RebuildTargets;
    public void SetCurrentZeepkist() => RebuildTargets();
}
