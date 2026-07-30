using System.Text;
using Il2CppScheduleOne.UI.Phone.Delivery;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LegionCore.Delivery
{
    // Fixes the invisible-tile bug: _shopElements (not deliveryShops) is the real render
    // list Start() iterates, private, editor-populated only, zero runtime writers in vanilla.
    internal static class DeliveryShopTileFactory
    {
        private static bool _hierarchyLogged;

        public static bool TryCreateTile(DeliveryApp app, string shopName, Color tileColor, string shopInterfaceName, out DeliveryShop? shop, out Button? button)
        {
            shop = null;
            button = null;

            if (app.deliveryShops == null || app._shopElements == null || app._shopElements.Count == 0)
                return false;

            var template = app._shopElements[0];

            var newButton = UnityEngine.Object.Instantiate(template.Button, template.Button.transform.parent);
            newButton.name = shopName + "_Button";
            newButton.onClick.RemoveAllListeners();

            // TEMP diagnostic: the cloned button still shows the template's own name/
            // description text (nothing overwrites it yet - ShopColor on DeliveryShop is
            // never actually read by vanilla code, it's not a real recolor hook). Dump the
            // clone's child hierarchy + any Text values once so we know exactly which
            // component to rename instead of guessing. Remove once the real label-set call
            // replaces this.
            if (!_hierarchyLogged)
            {
                _hierarchyLogged = true;
                var sb = new StringBuilder("LegionCore: cloned button hierarchy for '" + shopName + "':\n");
                DumpHierarchy(newButton.transform, 0, sb);
                MelonLogger.Msg(sb.ToString());
            }

            var newShop = UnityEngine.Object.Instantiate(template.Shop, template.Shop.transform.parent);
            newShop.name = shopName + "_Shop";
            newShop.ShopColor = tileColor;
            newShop.MatchingShopInterfaceName = shopInterfaceName;
            newShop.AvailableByDefault = true;

            // foreach (Transform child in transform) throws under Il2CppInterop - Current
            // doesn't cast cleanly to UnityEngine.Transform. Index-based GetChild is the safe
            // pattern. Iterate backwards since Destroy doesn't shrink childCount until next frame.
            for (int i = newShop.ListingContainer.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(newShop.ListingContainer.GetChild(i).gameObject);

            app.deliveryShops.Add(newShop);
            app._shopElements.Add(new DeliveryApp.DeliveryShopElement { Shop = newShop, Button = newButton });

            newButton.gameObject.SetActive(newShop.AvailableByDefault);
            // Il2Cpp interop represents UnityAction/Il2CppSystem.Action<T> as non-delegate
            // wrapper classes - a lambda/method group needs an explicit cast to convert, and
            // System.Delegate.Combine doesn't apply to them at all.
            newButton.onClick.AddListener((UnityAction)(() => app.OpenShop(newShop)));
            newShop.OnSelect = (Il2CppSystem.Action<DeliveryShop>)app.CloseShop;
            newShop.Initialize();

            shop = newShop;
            button = newButton;
            return true;
        }

        private static void DumpHierarchy(Transform t, int depth, StringBuilder sb)
        {
            var text = t.GetComponent<Text>();
            sb.Append(' ', depth * 2).Append("- ").Append(t.name);
            if (text != null) sb.Append(" [Text=\"").Append(text.text).Append("\"]");
            sb.Append('\n');

            // Index-based GetChild, not foreach - same Il2CppInterop enumerator-cast issue as
            // the ListingContainer cleanup above.
            for (int i = 0; i < t.childCount; i++)
                DumpHierarchy(t.GetChild(i), depth + 1, sb);
        }
    }
}
