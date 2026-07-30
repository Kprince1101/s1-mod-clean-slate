using Il2CppScheduleOne.UI.Phone.Delivery;
using UnityEngine;
using UnityEngine.UI;

namespace LegionCore.Delivery
{
    // Fixes the invisible-tile bug: _shopElements (not deliveryShops) is the real render
    // list Start() iterates, private, editor-populated only, zero runtime writers in vanilla.
    internal static class DeliveryShopTileFactory
    {
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

            var newShop = UnityEngine.Object.Instantiate(template.Shop, template.Shop.transform.parent);
            newShop.name = shopName + "_Shop";
            newShop.ShopColor = tileColor;
            newShop.MatchingShopInterfaceName = shopInterfaceName;
            newShop.AvailableByDefault = true;

            foreach (Transform child in newShop.ListingContainer)
                UnityEngine.Object.Destroy(child.gameObject);

            app.deliveryShops.Add(newShop);
            app._shopElements.Add(new DeliveryApp.DeliveryShopElement { Shop = newShop, Button = newButton });

            newButton.gameObject.SetActive(newShop.AvailableByDefault);
            newButton.onClick.AddListener(() => app.OpenShop(newShop));
            newShop.OnSelect = (System.Action<DeliveryShop>)System.Delegate.Combine(newShop.OnSelect, new System.Action<DeliveryShop>(app.CloseShop));
            newShop.Initialize();

            shop = newShop;
            button = newButton;
            return true;
        }
    }
}
