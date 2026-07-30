using HarmonyLib;
using MelonLoader;

[assembly: MelonInfo(typeof(DeliveryDriver.Plugin), "Global Real Quick Delivery", "0.0.1", "Legion", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace DeliveryDriver
{
    public class Plugin : MelonMod
    {
        private HarmonyLib.Harmony? _harmony;

        public override void OnInitializeMelon()
        {
            _harmony = new HarmonyLib.Harmony("DeliveryDriver");
            _harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
            LoggerInstance.Msg("Delivery Driver loaded.");
        }
    }
}
