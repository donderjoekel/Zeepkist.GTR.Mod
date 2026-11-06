// Headless capture state tests. These substitutes do not validate Unity physics or Harmony patch scheduling.
using UnityEngine;
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type, string method) { }
    }
}
namespace UnityEngine
{
    public class Transform
    {
        public string name = "trigger";
        public Vector3 position;
        public Quaternion rotation = new(0, 0, 0, 1);
        public Vector3 lossyScale = new(1, 1, 1);
        public Transform parent;
        public Vector3 TransformPoint(Vector3 p) => position + rotation * new Vector3(p.x * lossyScale.x, p.y * lossyScale.y, p.z * lossyScale.z);
    }
    public class GameObject
    {
        public Transform transform = new();
        public bool activeInHierarchy = true;
        public object component;
        public T GetComponent<T>() where T : class => component as T;
    }
    public class Rigidbody { public Vector3 position; public Quaternion rotation = new(0, 0, 0, 1); public Transform transform = new(); }
    public class Collider;
    public class SphereCollider : Collider
    {
        public string name = "TopSphereMan";
        public bool enabled = true;
        public GameObject gameObject = new();
        public Transform transform = new();
        public Rigidbody attachedRigidbody = new();
        public Vector3 center;
        public float radius = .27f;
        public ReadyToResetPointer pointer;
        public T GetComponent<T>() where T : class => pointer as T;
    }
    public static class Time { public static float fixedDeltaTime = .011f; }
}
public class ReadyToResetPointer { public ReadyToReset theScript; }
public class TimeTicker { public float what_ticker; public float GetTicker() => what_ticker; }
public class CaptureCharacter
{
    public bool dead;
    public CaptureSphereCollection ragdollTransform = new();
    public bool IsDead() => dead;
}
public class CaptureSphereCollection
{
    public SphereCollider[] spheres = [];
    public T[] GetComponentsInChildren<T>(bool includeInactive) where T : class => spheres.Cast<T>().ToArray();
}
public class ReadyToReset : CaptureSphereCollection
{
    public TimeTicker ticker = new();
    public CaptureCharacter character = new();
    public List<GameObject> allTriggers = [];
    public bool actuallyFinished;
    public void HeyYouHitATrigger() { }
}
public class CaptureProperties { public string UID = "checkpoint"; public Transform transform; }
public class BlockTriggerFinishOrCheckpoint { public CaptureProperties properties; }
namespace ZeepSDK.Level
{
    public static class LevelApi { public static Level CurrentLevel; public static LevelHash CurrentHashV2; }
    public class Level { public string UID; }
    public class LevelHash { public string ZeepHash; public string Hash; }
}
namespace ZeepSDK.External.Cysharp.Threading.Tasks
{
    public enum PlayerLoopTiming { LastFixedUpdate }
    public interface IPlayerLoopItem { bool MoveNext(); }
    public static class PlayerLoopHelper
    {
        public static List<IPlayerLoopItem> Items = [];
        public static void AddAction(PlayerLoopTiming timing, IPlayerLoopItem item) => Items.Add(item);
        public static void Tick() { foreach (var item in Items.ToArray()) if (!item.MoveNext()) Items.Remove(item); }
    }
}
namespace TNRD.Zeepkist.GTR.Ghosting.Recording
{
    public partial class GhostRecorder
    {
        private bool _sealed;
        private ReadyToReset _readyToReset;
        internal int CapturedFrames;
        internal GhostRecorder(ReadyToReset player) { _readyToReset = player; }
        private void CaptureFrame(float time) { CapturedFrames++; }
        internal void StartCapture() => StartEvidence();
        internal void StopCapture() { StopEvidence(); _sealed = true; }
        internal RunEvidence Evidence => _evidence;
    }
}
