using BepInEx;
using BepInEx.Logging;

namespace TNRD.Zeepkist.GTR.Mod
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
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
