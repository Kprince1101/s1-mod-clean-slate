using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LegionCore.Ui
{
    // Small procedural-UI helpers shared by any mod building a custom phone App<T> (GRQD's
    // Delivery app now, Clean Slate's Store app later). No AssetBundle/prefab dependency -
    // everything is built from plain UnityEngine.UI components at runtime, which is a
    // well-established safe pattern under Il2CppInterop (unlike foreach-over-Transform,
    // AddComponent<T>() on built-in Unity component types works fine).
    public static class UiFactory
    {
        // A fresh screen-space-overlay canvas, suitable as an App<T>'s appContainer root.
        public static Canvas CreateRootCanvas(string name, Transform? parent = null)
        {
            // GameObject(string, params Type[]) needs Il2CppSystem.Type, not System.Type -
            // typeof() gives the latter, so plain construction + AddComponent<RectTransform>()
            // (which Unity upgrades the default Transform into, same as any uGUI element) is
            // the safe path under Il2CppInterop.
            var go = new GameObject(name);
            go.AddComponent<RectTransform>();
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform CreatePanel(Transform parent, Color color, string name = "Panel")
        {
            var go = new GameObject(name);
            go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            return (RectTransform)go.transform;
        }

        public static Text CreateText(Transform parent, string content, int fontSize = 24, string name = "Text")
        {
            var go = new GameObject(name);
            go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.text = content;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return text;
        }

        // Takes a real System.Action, not UnityAction - under Il2CppInterop, UnityAction (like
        // Il2CppSystem.Action<T> - see DeliveryShopTileFactory's OnSelect cast) isn't a true
        // delegate type, so a bare lambda can't implicitly convert to it at the call site. One
        // explicit cast here beats needing (UnityAction)(() => ...) at every call site.
        public static Button CreateButton(Transform parent, string label, System.Action onClick, string name = "Button")
        {
            var go = new GameObject(name);
            go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.15f);
            var button = go.AddComponent<Button>();
            button.onClick.AddListener((UnityAction)onClick);

            var label_ = CreateText(go.transform, label, 20, "Label");
            label_.alignment = TextAnchor.MiddleCenter;
            var labelRect = (RectTransform)label_.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            return button;
        }

        // A flat-color square sprite - used for a mod's app icon when no real art exists yet.
        public static Sprite CreateSolidSprite(Color color, int size = 64)
        {
            var tex = new Texture2D(size, size);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
