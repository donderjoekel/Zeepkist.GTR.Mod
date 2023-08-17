using System;
using System.Linq;
using ZeepSDK.Racing;
using Object = UnityEngine.Object;

namespace TNRD.Zeepkist.GTR.Mod.Stats;

internal abstract class RacingTrackerBase : TrackerBase, ITickable, IDisposable
{
    protected SetupCar SetupCar { get; private set; }

    protected virtual bool MustBeAlive => true;

    public RacingTrackerBase()
    {
        RacingApi.PlayerSpawned += OnPlayerSpawned;
    }

    private void OnPlayerSpawned()
    {
        GameMaster gameMaster = Object.FindObjectOfType<GameMaster>();
        SetupCar = gameMaster.carSetups.First();
    }

    public void Tick()
    {
        if (SetupCar == null)
            return;

        if (MustBeAlive && SetupCar.characterDamage.IsDead())
            return;

        OnTick();
    }

    protected abstract void OnTick();

    public virtual void Dispose()
    {
        RacingApi.PlayerSpawned -= OnPlayerSpawned;
    }
}
