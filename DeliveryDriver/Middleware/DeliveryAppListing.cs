using HarmonyLib;
using Il2CppScheduleOne.UI.Phone.Delivery;
using MelonLoader;
using UnityEngine;

namespace DeliveryDriver.Middleware
{
    // Registers a GRQD entry in the vanilla Delivery app's shop list.
    //
    // FIRST DRAFT, not verified in-game. Clones an existing DeliveryShop (vanilla shops are
    // scene prefabs, we have none of our own) before DeliveryApp.Start() runs, so vanilla's
    // own shop-list UI building picks it up like any other vendor. This only makes GRQD show
    // up as a tile in the app — it still runs the cloned shop's vanilla buy/checkout flow
    // (MatchingShopInterfaceName still points at the source shop's ShopInterface) until we
    // patch CanOrder/SubmitOrder to redirect into GRQD's own route/schedule system. That
    // redirect is separate follow-up work (Route UX, DD2 step 5), not part of this ticket.
    public static class DeliveryAppListing
    {
        public const string ShopName = "Global Real Quick Delivery";
        public static readonly Color ShopColor = new Color(0f, 0.5f, 0.5f); // teal

        private static bool _installed;

        public static void Install(DeliveryApp app)
        {
            if (_installed)
            {
                MelonLogger.Msg("DeliveryAppListing: already installed this session, skipping.");
                return;
            }

            if (app.deliveryShops == null || app.deliveryShops.Count == 0)
            {
                MelonLogger.Warning("DeliveryAppListing: no template DeliveryShop found, GRQD not registered.");
                return;
            }

            var template = app.deliveryShops[0];
            var clone = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
            clone.name = "DeliveryShop_GRQD";

            var shop = clone.GetComponent<DeliveryShop>();
            if (shop == null)
            {
                MelonLogger.Warning("DeliveryAppListing: clone missing DeliveryShop component, aborting.");
                UnityEngine.Object.Destroy(clone);
                return;
            }

            shop.ShopColor = ShopColor;
            app.deliveryShops.Add(shop);
            _installed = true;

            MelonLogger.Msg($"DeliveryAppListing: registered '{ShopName}' (cloned from '{template.name}'). deliveryShops.Count is now {app.deliveryShops.Count}.");
        }
    }

    [HarmonyPatch(typeof(DeliveryApp), "Start")]
    internal static class DeliveryApp_Start_Prefix_Patch
    {
        [HarmonyPrefix]
        private static void Prefix(DeliveryApp __instance)
        {
            DeliveryAppListing.Install(__instance);
        }
    }

    // Diagnostic only: tells us whether Start() rebuilds/reassigns deliveryShops after our
    // prefix runs (which would explain the clone not showing up despite Install() succeeding).
    // Delete once the real mechanism is confirmed.
    [HarmonyPatch(typeof(DeliveryApp), "Start")]
    internal static class DeliveryApp_Start_Postfix_Diagnostic
    {
        [HarmonyPostfix]
        private static void Postfix(DeliveryApp __instance)
        {
            var shopsCount = __instance.deliveryShops?.Count.ToString() ?? "null";
            var elementsCount = __instance._shopElements?.Count.ToString() ?? "null";
            var panelsCount = __instance._shopPanels?.Count.ToString() ?? "null";
            MelonLogger.Msg($"[DeliveryAppListing diag] post-Start: deliveryShops={shopsCount}, _shopElements={elementsCount}, _shopPanels={panelsCount}");
        }
    }
}
