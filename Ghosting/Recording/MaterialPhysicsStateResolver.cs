namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

internal static class MaterialPhysicsStateResolver
{
    public static bool ShouldIncludeWheel(bool enabled, bool grounded)
    {
        return enabled && grounded;
    }

    public static MaterialPhysicsState GetEffectiveState(
        bool soapOverride,
        int? logicOutput)
    {
        return soapOverride
            ? MaterialPhysicsState.Soap
            : FromLogicOutput(logicOutput);
    }

    public static MaterialPhysicsState FromLogicOutput(int? logicOutput)
    {
        return logicOutput switch
        {
            0 => MaterialPhysicsState.Tarmac,
            1 => MaterialPhysicsState.Grass,
            2 => MaterialPhysicsState.Sand,
            3 => MaterialPhysicsState.Mud,
            4 => MaterialPhysicsState.Soap,
            5 => MaterialPhysicsState.Wood,
            6 => MaterialPhysicsState.Ice1,
            7 => MaterialPhysicsState.Ice2,
            8 => MaterialPhysicsState.Ice3,
            _ => MaterialPhysicsState.None
        };
    }

    public static MaterialPhysicsState Combine(
        MaterialPhysicsState currentState,
        MaterialPhysicsState nextState)
    {
        return currentState | nextState;
    }
}
