using System.Collections.Generic;
using HarmonyLib;
using Il2CppScheduleOne.UI.Phone.Delivery;
using MelonLoader;
using UnityEngine;

namespace LegionCore.Delivery
{
    internal sealed class DeliveryApi : IDeliveryApi
    {
        private static readonly List<(string Name, Color Color, string ShopInterfaceName, string Description, Sprite? Icon, System.Action? OnClick)> PendingTiles = new();
        private static readonly HashSet<DeliveryShop> ManagedShops = new();

        public bool IsReady => Readiness.Check();

        public void RegisterShopTile(string shopName, Color tileColor, string shopInterfaceName,
            string description = "", Sprite? icon = null, System.Action? onClick = null) =>
            PendingTiles.Add((shopName, tileColor, shopInterfaceName, description, icon, onClick));

        internal static bool IsManaged(DeliveryShop shop) => ManagedShops.Contains(shop);

        internal static void InstallPendingTiles(DeliveryApp app)
        {
            foreach (var (name, color, shopInterfaceName, description, icon, onClick) in PendingTiles)
            {
                if (DeliveryShopTileFactory.TryCreateTile(app, name, color, shopInterfaceName, description, icon, onClick, out var shop, out _))
                {
                    if (shop != null) ManagedShops.Add(shop);
                    MelonLogger.Msg($"LegionCore: registered delivery shop tile '{name}'.");
                }
                else
                {
                    MelonLogger.Warning($"LegionCore: failed to register delivery shop tile '{name}' - no template tile found.");
                }
            }

            PendingTiles.Clear();
        }
    }

    [HarmonyPatch(typeof(DeliveryApp), "Start")]
    internal static class DeliveryApp_Start_Postfix
    {
        [HarmonyPostfix]
        private static void Postfix(DeliveryApp __instance) => DeliveryApi.InstallPendingTiles(__instance);
    }

    // Managed shops have no real ShopInterface behind them yet (that's separate order-flow
    // work) - block ordering instead of letting WillCartFitInVehicle NPE on a null MatchingShop.
    [HarmonyPatch(typeof(DeliveryShop), nameof(DeliveryShop.CanOrder))]
    internal static class DeliveryShop_CanOrder_Prefix
    {
        [HarmonyPrefix]
        private static bool Prefix(DeliveryShop __instance, out string reason, ref bool __result)
        {
            reason = string.Empty;
            if (!DeliveryApi.IsManaged(__instance)) return true;

            reason = "Not available yet";
            __result = false;
            return false;
        }
    }
}
