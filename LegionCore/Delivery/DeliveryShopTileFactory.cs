using Il2CppScheduleOne.UI.Phone.Delivery;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LegionCore.Delivery
{
    // Fixes the invisible-tile bug: _shopElements (not deliveryShops) is the real render
    // list Start() iterates, private, editor-populated only, zero runtime writers in vanilla.
    internal static class DeliveryShopTileFactory
    {
        // Confirmed via a real in-game hierarchy dump (now removed - this is the answer it
        // gave): the cloned button is
        //   <button root> (Image = colored bar background, Button)
        //     - Icon
        //       - Image        <- portrait/icon graphic
        //     - Title          <- Text, shop name
        //     - Description    <- Text, shop subtitle
        //     - Arrow
        //     - SelectedFrame
        public static bool TryCreateTile(DeliveryApp app, string shopName, Color tileColor, string shopInterfaceName,
            string description, Sprite? icon, System.Action? onClick, out DeliveryShop? shop, out Button? button)
        {
            shop = null;
            button = null;

            if (app.deliveryShops == null || app._shopElements == null || app._shopElements.Count == 0)
                return false;

            var template = app._shopElements[0];

            var newButton = UnityEngine.Object.Instantiate(template.Button, template.Button.transform.parent);
            newButton.name = shopName + "_Button";
            newButton.onClick.RemoveAllListeners();

            // ShopColor on DeliveryShop itself is never read by vanilla code (not a real
            // recolor hook) - the button's own background Image is the actual color source.
            var background = newButton.GetComponent<Image>();
            if (background != null) background.color = tileColor;

            var titleText = newButton.transform.Find("Title")?.GetComponent<Text>();
            if (titleText != null) titleText.text = shopName;

            var descriptionText = newButton.transform.Find("Description")?.GetComponent<Text>();
            if (descriptionText != null) descriptionText.text = description;

            var iconImage = newButton.transform.Find("Icon/Image")?.GetComponent<Image>();
            if (iconImage != null && icon != null) iconImage.sprite = icon;

            if (onClick != null)
            {
                // Custom-handled tile (GRQD's own management panel, wired up directly instead
                // of the vanilla DeliveryShop listing/ordering screen). No DeliveryShop clone,
                // no _shopElements entry - DeliveryApp.Start() has already finished its one-time
                // wiring loop over _shopElements by the time this runs (it's called from a
                // Harmony POSTFIX on Start(), see DeliveryApi.cs), so appending to that list
                // now wouldn't retroactively wire anything from that loop anyway. We just need
                // the button itself active and clickable, which we do here directly.
                newButton.gameObject.SetActive(true);
                newButton.onClick.AddListener((UnityAction)onClick);
                button = newButton;
                return true;
            }

            var newShop = UnityEngine.Object.Instantiate(template.Shop, template.Shop.transform.parent);
            newShop.name = shopName + "_Shop";
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

            // DeliveryShop.Initialize() looks up MatchingShop by shopInterfaceName and, if
            // nothing matches (always true for us with shopInterfaceName left blank), logs an
            // error and returns BEFORE it ever wires BackButton.onClick - confirmed by reading
            // the decompiled source, and matches exactly what was reported ("back button
            // doesn't work"). Wire it ourselves so navigation works regardless of whether a
            // real ShopInterface exists yet.
            WireBackButton(app, newShop);
            newShop.Initialize();

            shop = newShop;
            button = newButton;
            return true;
        }

        private static void WireBackButton(DeliveryApp app, DeliveryShop newShop)
        {
            if (newShop.BackButton == null) return;
            newShop.BackButton.onClick.AddListener((UnityAction)(() => app.CloseShop(newShop)));
        }
    }
}
