using BepInEx.Logging;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Mod.Components;

public abstract class MonoBehaviourWithLogging : MonoBehaviour
{
    protected static ManualLogSource Logger { get; private set; }
    
    protected virtual void Awake()
    {
        Logger = Plugin.CreateLogger(GetType().Name);
    }
}
