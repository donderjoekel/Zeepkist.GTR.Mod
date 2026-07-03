using ProtoBuf;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording.Data;

[ProtoContract]
public class InitialFrame
{
    public InitialFrame()
    {
    }

    public InitialFrame(
        Vector3 position,
        Vector3 rotation,
        byte speed,
        byte steering,
        InputFlags inputFlags,
        SoapboxFlags soapboxFlags)
        : this(
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
            false,
            false,
            new Vector3Int(),
            new Vector3Int())
    {
    }

    public InitialFrame(
        Vector3 position,
        Vector3 rotation,
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
        bool monorailState,
        bool ragdollState,
        Vector3Int ragdollPosition,
        Vector3Int ragdollRotation)
    {
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
        RagdollState = ragdollState;
        RagdollPosition = ragdollPosition;
        RagdollRotation = ragdollRotation;
    }

    [ProtoMember(1)] public Vector3 Position { get; set; }
    [ProtoMember(2)] public Vector3 Rotation { get; set; }
    [ProtoMember(3)] public byte Speed { get; set; }
    [ProtoMember(4)] public byte Steering { get; set; }
    [ProtoMember(5)] public InputFlags InputFlags { get; set; }
    [ProtoMember(6)] public SoapboxFlags SoapboxFlags { get; set; }
    [ProtoMember(7)] public GroundedWheelState GroundedWheelState { get; set; }
    [ProtoMember(8)] public SlippingWheelState SlippingWheelState { get; set; }
    [ProtoMember(9)] public SurfaceState SurfaceState { get; set; }
    [ProtoMember(10)] public Vector3Int LocalVelocity { get; set; }
    [ProtoMember(11)] public Vector3Int LocalAngularVelocity { get; set; }
    [ProtoMember(12)] public Vector2Int LocalGForce { get; set; }
    [ProtoMember(13)] public bool ParkingBlockState { get; set; }
    [ProtoMember(14)] public bool MonorailState { get; set; }
    [ProtoMember(15)] public bool RagdollState { get; set; }
    [ProtoMember(16)] public Vector3Int RagdollPosition { get; set; }
    [ProtoMember(17)] public Vector3Int RagdollRotation { get; set; }
}
