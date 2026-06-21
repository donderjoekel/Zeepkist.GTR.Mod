using System;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public sealed class BulkGhostModeState
{
    public bool IsActive { get; private set; }
    public event Action Changed;

    public void SetActive(bool active)
    {
        if (IsActive == active)
            return;

        IsActive = active;
        Changed?.Invoke();
    }
}
