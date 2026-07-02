using ProtoBuf;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording.Data;

[ProtoContract]
public class DeltaFrame
{
    public DeltaFrame()
    {
    }

    public DeltaFrame(
        float time,
        Vector3Int position,
        Vector3Int rotation,
        byte speed,
        byte steering,
        InputFlags inputFlags,
        SoapboxFlags soapboxFlags)
        : this(
            time,
            position,
            rotation,
            speed,
            steering,
            inputFlags,
            soapboxFlags,
            GroundedWheelState.HasNone,
            SlippingWheelState.HasNone,
            SurfaceState.Tarmac,
            new Vector3Int(),
            new Vector3Int(),
            new Vector2Int(),
            false,
            false)
    {
    }

    public DeltaFrame(
        float time,
        Vector3Int position,
        Vector3Int rotation,
        byte speed,
        byte steering,
        InputFlags inputFlags,
        SoapboxFlags soapboxFlags,
        GroundedWheelState groundedWheelState,
        SlippingWheelState slippingWheelState,
        SurfaceState surfaceState,
        Vector3Int localVelocity,
        Vector3Int localAngularVelocity,
        Vector2Int localGForce,
        bool parkingBlockState,
        bool monorailState)
    {
        Time = time;
        Position = position;
        Rotation = rotation;
        Speed = speed;
        Steering = steering;
        InputFlags = inputFlags;
        SoapboxFlags = soapboxFlags;
        GroundedWheelState = groundedWheelState;
        SlippingWheelState = slippingWheelState;
        SurfaceState = surfaceState;
        LocalVelocity = localVelocity;
        LocalAngularVelocity = localAngularVelocity;
        LocalGForce = localGForce;
        ParkingBlockState = parkingBlockState;
        MonorailState = monorailState;
    }

    [ProtoMember(1)] public float Time { get; set; }
    [ProtoMember(2)] public Vector3Int Position { get; set; }
    [ProtoMember(3)] public Vector3Int Rotation { get; set; }
    [ProtoMember(4)] public byte Speed { get; set; }
    [ProtoMember(5)] public byte Steering { get; set; }
    [ProtoMember(6)] public InputFlags InputFlags { get; set; }
    [ProtoMember(7)] public SoapboxFlags SoapboxFlags { get; set; }
    [ProtoMember(8)] public GroundedWheelState GroundedWheelState { get; set; }
    [ProtoMember(9)] public SlippingWheelState SlippingWheelState { get; set; }
    [ProtoMember(10)] public SurfaceState SurfaceState { get; set; }
    [ProtoMember(11)] public Vector3Int LocalVelocity { get; set; }
    [ProtoMember(12)] public Vector3Int LocalAngularVelocity { get; set; }
    [ProtoMember(13)] public Vector2Int LocalGForce { get; set; }
    [ProtoMember(14)] public bool ParkingBlockState { get; set; }
    [ProtoMember(15)] public bool MonorailState { get; set; }
}
