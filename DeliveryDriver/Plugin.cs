using MelonLoader;

[assembly: MelonInfo(typeof(DeliveryDriver.Plugin), "Delivery Driver", "0.0.1", "Legion", null)]
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
