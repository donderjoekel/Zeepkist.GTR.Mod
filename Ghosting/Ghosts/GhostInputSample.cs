namespace TNRD.Zeepkist.GTR.Ghosting.Ghosts;

public readonly struct GhostInputSample
{
    public GhostInputSample(
        bool armsUp,
        bool braking,
        float steering,
        byte zeepkistState = 0,
        float speedKmh = 0f)
    {
        ArmsUp = armsUp;
        Braking = braking;
        Steering = steering;
        ZeepkistState = zeepkistState;
        SpeedKmh = speedKmh;
    }

    public bool ArmsUp { get; }
    public bool Braking { get; }
    public float Steering { get; }
    public byte ZeepkistState { get; }
    public float SpeedKmh { get; }
}
