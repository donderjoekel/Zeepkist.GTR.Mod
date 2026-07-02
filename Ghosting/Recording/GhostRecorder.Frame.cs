namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

public partial class GhostRecorder
{
    private class Frame
    {
        public float Time { get; set; }
        public float Speed { get; set; }
        public UnityEngine.Vector3 Position { get; set; }
        public UnityEngine.Vector3 Rotation { get; set; }
        public float Steering { get; set; }
        public bool ArmsUp { get; set; }
        public bool Braking { get; set; }
        public bool Horn { get; set; }
        public byte SoapboxState { get; set; }
        public WheelState WheelState { get; set; }
        public GroundedWheelState GroundedWheelState { get; set; }
        public SlippingWheelState SlippingWheelState { get; set; }
        public SurfaceState SurfaceState { get; set; }
        public UnityEngine.Vector3 LocalVelocity { get; set; }
        public UnityEngine.Vector3 LocalAngularVelocity { get; set; }
        public UnityEngine.Vector2 LocalGForce { get; set; }
        public bool ParkingBlockState { get; set; }
        public bool MonorailState { get; set; }
    }
}
