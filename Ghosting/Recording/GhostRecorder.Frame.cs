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
    }
}
