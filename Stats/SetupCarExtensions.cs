using System.Collections.Generic;
using System.Linq;

namespace TNRD.Zeepkist.GTR.Mod.Stats;

internal static class SetupCarExtensions
{
    private static readonly List<DamageWheel> pooled = new();

    private static IEnumerable<DamageWheel> GetDamageWheels(this SetupCar setupCar)
    {
        pooled.Clear();
        pooled.Add(setupCar.damageLF);
        pooled.Add(setupCar.damageRF);
        pooled.Add(setupCar.damageLR);
        pooled.Add(setupCar.damageRR);
        return pooled;
    }

    public static bool AreAllWheelsDead(this SetupCar setupCar)
    {
        return setupCar.GetDamageWheels().All(x => x.isdead);
    }

    public static bool AreAllWheelsAlive(this SetupCar setupCar)
    {
        return setupCar.GetDamageWheels().All(x => !x.isdead);
    }

    public static bool IsAnyWheelAlive(this SetupCar setupCar)
    {
        return setupCar.GetDamageWheels().Any(x => !x.isdead);
    }

    public static bool AreAllWheelsInAir(this SetupCar setupCar)
    {
        foreach (DamageWheel damageWheel in setupCar.GetDamageWheels())
        {
            if (damageWheel.isdead)
                continue;

            if (damageWheel.theActualWheel.IsGrounded())
                return false;
        }

        return true;
    }

    public static bool AreAllWheelsGrounded(this SetupCar setupCar)
    {
        foreach (DamageWheel damageWheel in setupCar.GetDamageWheels())
        {
            if (damageWheel.isdead)
                continue;

            if (!damageWheel.theActualWheel.IsGrounded())
                return false;
        }

        return true;
    }

    public static bool IsAnyWheelGrounded(this SetupCar setupCar)
    {
        foreach (DamageWheel damageWheel in setupCar.GetDamageWheels())
        {
            if (damageWheel.isdead)
                continue;

            if (damageWheel.theActualWheel.IsGrounded())
                return true;
        }

        return false;
    }
}
