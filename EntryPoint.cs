using BepInEx;
using BepInEx.Logging;

namespace TNRD.Zeepkist.GTR.Mod
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency("ZeepSDK", "1.11.1")]
    public class EntryPoint : BaseUnityPlugin
    {
        static EntryPoint()
        {
            CosturaUtility.Initialize();
        }

        private static EntryPoint instance;

        public static ManualLogSource CreateLogger(string sourceName)
        {
            return BepInEx.Logging.Logger.CreateLogSource(instance.Info.Metadata.Name + "." + sourceName);
        }

        private void Awake()
        {
            instance = this;
            Plugin plugin = gameObject.AddComponent<Plugin>();
            plugin.Config = Config;
            plugin.Logger = Logger;
            plugin.Info = Info;
        }
    }
}
