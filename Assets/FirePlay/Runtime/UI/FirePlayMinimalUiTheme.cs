using UnityEngine;
using UnityEngine.UI;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>
    /// Shared runtime presentation theme for activity forms. It uses plain
    /// UGUI colours instead of background textures, preserving each activity's
    /// independent layout and interaction code.
    /// </summary>
    public static class FirePlayMinimalUiTheme
    {
        private static readonly Color Button = new(0.92f, 0.95f, 0.97f, 0.22f);

        public static Color AnchorActivityButton => new(0.94f, 0.96f, 0.98f, 0.24f);
        public static Color AnywhereActivityButton => new(0.78f, 0.83f, 0.88f, 0.17f);

        public static void Apply(GameObject root)
        {
            if (root == null) return;

            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                var selectable = image.GetComponent<Selectable>();
                var owner = selectable == null ? image.GetComponentInParent<Selectable>() : selectable;
                var isTargetGraphic = owner != null
                    && (owner.targetGraphic == image || selectable == owner);
                // A null Sprite renders as a simple UGUI quad and avoids a
                // decorative image dependency or broken imported texture.
                image.sprite = null;
                if (image.type == Image.Type.Sliced || image.type == Image.Type.Tiled)
                    image.type = Image.Type.Simple;

                if (isTargetGraphic)
                {
                    if (owner.targetGraphic == null)
                        owner.targetGraphic = image;
                    image.color = Button;
                    image.raycastTarget = true;
                    ConfigureSelectable(owner);
                }
                else
                {
                    image.raycastTarget = false;
                }
            }

            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                text.raycastTarget = false;
            }
        }

        private static void ConfigureSelectable(Selectable selectable)
        {
            var colors = selectable.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.66f, 0.72f, 0.78f, 0.82f);
            colors.selectedColor = new Color(0.90f, 0.94f, 0.98f, 1f);
            colors.disabledColor = new Color(0.62f, 0.65f, 0.68f, 0.35f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.10f;
            selectable.colors = colors;
        }
    }
}
