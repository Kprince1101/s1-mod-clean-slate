using MelonLoader;

[assembly: MelonInfo(typeof(DeliveryDriver.Plugin), "Global Real Quick Delivery", "0.0.1", "Legion", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace DeliveryDriver
{
    public class Plugin : MelonMod
    {
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Delivery Driver loaded.");
        }
    }
}
