using BepInEx.Logging;
using UnityEngine;
using ZeepSDK.Utilities;

namespace TNRD.Zeepkist.GTR.Mod.Components;

public abstract class MonoBehaviourWithLogging : MonoBehaviour
{
    protected static ManualLogSource Logger { get; private set; }
    
    protected virtual void Awake()
    {
        Logger = LoggerFactory.GetLogger(GetType());
    }
}
