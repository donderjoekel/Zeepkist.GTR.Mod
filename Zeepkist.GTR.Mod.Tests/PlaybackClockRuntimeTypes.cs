// Native event/player adapters. Clock, playback controls, and photo-mode event handling use production source.
namespace ZeepSDK.Racing
{
    public static class RacingApi
    {
        public static event Action RoundStarted;
        public static event Action RoundEnded;
        public static event Action PlayerSpawned;
        public static event Action QuickReset;
        public static event Action Quit;
        public static void StartRound() => RoundStarted?.Invoke();
        public static void EndRound() => RoundEnded?.Invoke();
        public static void Spawn() => PlayerSpawned?.Invoke();
        public static void Reset() => QuickReset?.Invoke();
        public static void Exit() => Quit?.Invoke();
    }
}
namespace ZeepSDK.Multiplayer
{
    public static class MultiplayerApi
    {
        public static bool IsPlayingOnline;
        public static event Action DisconnectedFromGame;
        public static void Disconnect() => DisconnectedFromGame?.Invoke();
    }
}
namespace ZeepSDK.PhotoMode
{
    public static class PhotoModeFlyingCamera { public static FlyingCameraScript Current; }
    public static class PhotoModeApi
    {
        public static event Action PhotoModeEntered;
        public static event Action PhotoModeExited;
        public static void Enter() => PhotoModeEntered?.Invoke();
        public static void Exit() => PhotoModeExited?.Invoke();
    }
}
namespace HarmonyLib
{
    public static class AccessTools
    {
        public static System.Reflection.MethodInfo Method(Type type, string name) => null;
    }
}
namespace TNRD.Zeepkist.GTR.PlayerLoop
{
    public sealed class PlayerLoopService
    {
        public void SubscribeLateUpdate(Action action) { }
    }
}
namespace TNRD.Zeepkist.GTR.Ghosting.Playback
{
    public partial class GhostPlayer
    {
        public event EventHandler<GhostAddedEventArgs> GhostAdded;
        public void StartManualPlayback() { }
        public void StopManualPlayback() { }
        public void PauseGhosts() { }
        public void ResumeGhosts() { }
        internal void RestartRoundPlayback() { }
        public void SeekAllGhosts(float time) { }
        public bool TryStepFrame(float time, int direction, float epsilon, out float result)
        { result = time; return false; }
    }
}
