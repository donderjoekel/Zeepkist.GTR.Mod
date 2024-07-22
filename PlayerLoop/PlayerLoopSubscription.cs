using System;

namespace TNRD.Zeepkist.GTR.PlayerLoop;

public class PlayerLoopSubscription
{
    public Guid Id { get; }

    public PlayerLoopSubscription()
    {
        Id = Guid.NewGuid();
    }
}
