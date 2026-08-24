using System.IO;
using DemonViglu.FirePlay.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DemonViglu.FirePlay.Editor
{
    /// <summary>
    /// Applies the snow-valley UI art direction to existing SUIFW assets.
    /// It only changes serialized presentation components; all form scripts,
    /// button callbacks and activity state ownership stay untouched.
    /// </summary>
    public static class FirePlayUiLookDevBuilder
    {
        private const string UiArtDirectory = "Assets/FirePlay/LookDev/UI";
        private const string RoundedSpritePath = UiArtDirectory + "/SnowUi_RoundedPanel.png";
        private const string CircleSpritePath = UiArtDirectory + "/SnowUi_SoftCircle.png";
        private const string CanvasPath = "Assets/Resources/SUIFW/Canvas.prefab";

        private static readonly string[] ActivityFormPaths =
        {
            "Assets/Resources/SUIFW/UIPrefabs/ActivitySelectionForms.prefab",
            "Assets/Resources/SUIFW/UIPrefabs/MarshmallowActivityForms.prefab",
            "Assets/Resources/SUIFW/UIPrefabs/FishingActivityForms.prefab",
            "Assets/Resources/SUIFW/UIPrefabs/GuitarActivityForms.prefab",
            "Assets/Resources/SUIFW/UIPrefabs/EmoteActivityForms.prefab"
        };

        // Keep the interface almost colourless. Snow light and a dark text outline
        // provide readability; the UI should feel like air, not a dark control deck.
        private static readonly Color SnowGlass = new(1f, 1f, 1f, 0.14f);
        private static readonly Color SnowGlassLight = new(1f, 1f, 1f, 0.26f);
        private static readonly Color PrimaryWhite = new(1f, 1f, 1f, 0.34f);
        private static readonly Color PrimaryWhitePressed = new(0.72f, 0.82f, 0.88f, 0.58f);
        private static readonly Color IceAccent = new(0.93f, 0.98f, 1f, 0.88f);
        private static readonly Color FrostText = new(0.92f, 0.97f, 1f, 0.98f);
        private static readonly Color SubtleText = new(0.73f, 0.84f, 0.90f, 0.92f);

        [MenuItem("FirePlay/UI/Apply Snow Valley UI LookDev")]
        public static void ApplySnowValleyUiLookDev()
        {
            // The accepted direction is transparent grey/white UGUI without
            // decorative panel textures. Keep the parameters for compatibility
            // with the existing authoring routine, but do not generate assets.
            Sprite roundedSprite = null;
            Sprite circleSprite = null;
            StylePrefab(CanvasPath, roundedSprite, circleSprite, isHud: true);
            foreach (var formPath in ActivityFormPaths)
            {
                StylePrefab(formPath, roundedSprite, circleSprite, isHud: false);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FirePlayUiLookDevBuilder] Applied the Snow Valley UI look to HUD and activity forms.");
        }

        private static void StylePrefab(string path, Sprite roundedSprite, Sprite circleSprite, bool isHud)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                Debug.LogWarning($"[FirePlayUiLookDevBuilder] Missing UI prefab: {path}");
                return;
            }

            try
            {
                StyleCanvas(root, isHud);
                StyleHudDataColors(root, isHud);
                StyleImages(root, roundedSprite, circleSprite, isHud);
                StyleButtons(root, roundedSprite);
                StyleText(root, isHud);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void StyleCanvas(GameObject root, bool isHud)
        {
            var scaler = root.GetComponentInChildren<CanvasScaler>(true);
            if (scaler == null)
            {
                return;
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = isHud ? 0.58f : 0.5f;
        }

        private static void StyleHudDataColors(GameObject root, bool isHud)
        {
            if (isHud)
            {
                var hud = root.GetComponentInChildren<FirePlayHudForm>(true);
                if (hud != null)
                {
                    var serializedHud = new SerializedObject(hud);
                    serializedHud.FindProperty("_lowFuelColor").colorValue = new Color(0.58f, 0.66f, 0.70f, 0.88f);
                    serializedHud.FindProperty("_fullFuelColor").colorValue = new Color(1f, 1f, 1f, 0.96f);
                    serializedHud.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            var selection = root.GetComponentInChildren<ActivitySelectionForms>(true);
            if (selection != null)
            {
                var serializedSelection = new SerializedObject(selection);
                serializedSelection.FindProperty("_anchorButtonColor").colorValue = PrimaryWhite;
                serializedSelection.FindProperty("_anywhereButtonColor").colorValue = new Color(1f, 1f, 1f, 0.22f);
                serializedSelection.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void StyleImages(GameObject root, Sprite roundedSprite, Sprite circleSprite, bool isHud)
        {
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                var name = image.name;
                if (IsButtonImage(image))
                {
                    continue;
                }

                var usesCircularControl = name.Contains("FuelRoot")
                    || name.Contains("FuelFill")
                    || name.Contains("JoystickArea")
                    || name == "Handle";
                image.sprite = null;
                image.type = name.Contains("FuelFill") ? Image.Type.Filled : usesCircularControl ? Image.Type.Simple : Image.Type.Sliced;
                if (name == "Handle")
                {
                    image.raycastTarget = false;
                }

                if (name.Contains("FuelFill"))
                {
                    image.color = new Color(1f, 1f, 1f, 0.96f);
                }
                else if (name.Contains("FlameImage"))
                {
                    image.color = new Color(1f, 1f, 1f, 0.96f);
                }
                else if (name.Contains("Needle") || name.Contains("FishMarker"))
                {
                    image.color = new Color(1f, 1f, 1f, 0.96f);
                }
                else if (name.Contains("PerfectZone") || name.Contains("CatchZone") || name.Contains("ProgressFill"))
                {
                    image.color = IceAccent;
                }
                else if (name.Contains("InteractionPrompt") || name.Contains("FuelRoot"))
                {
                    image.color = SnowGlass;
                }
                else if (name.Contains("Timing") || name.Contains("Fight"))
                {
                    image.color = new Color(1f, 1f, 1f, 0.18f);
                }
                else
                {
                    image.color = isHud ? new Color(1f, 1f, 1f, 0.10f) : SnowGlass;
                }
            }
        }

        private static void StyleButtons(GameObject root, Sprite roundedSprite)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
                if (image == null)
                {
                    continue;
                }

                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = button.name.Contains("Close")
                    ? new Color(1f, 1f, 1f, 0.20f)
                    : PrimaryWhite;
                ApplyCompactButtonShape(button);

                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
                colors.pressedColor = PrimaryWhitePressed;
                colors.selectedColor = new Color(0.92f, 0.98f, 1f, 1f);
                colors.disabledColor = new Color(0.75f, 0.78f, 0.80f, 0.38f);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.12f;
                button.colors = colors;

                var outline = GetOrAdd<Outline>(image.gameObject);
                outline.effectColor = new Color(0.08f, 0.14f, 0.18f, 0.40f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
        }

        private static void StyleText(GameObject root, bool isHud)
        {
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                var name = text.name;
                var isTitle = name.Contains("Title");
                var isStatus = name.Contains("Status");
                var isFuel = name.Contains("Fuel");
                text.color = isTitle || isFuel ? FrostText : isStatus ? SubtleText : FrostText;
                text.fontStyle = isTitle ? FontStyle.Bold : FontStyle.Normal;
                text.supportRichText = true;

                if (isTitle)
                {
                    text.fontSize = Mathf.Max(text.fontSize, isHud ? 30 : 38);
                }
                else if (isStatus)
                {
                    text.fontSize = Mathf.Max(text.fontSize, isHud ? 23 : 25);
                }
                else if (!isHud)
                {
                    text.fontSize = Mathf.Max(text.fontSize, 26);
                }

                var outline = GetOrAdd<Outline>(text.gameObject);
                outline.effectColor = new Color(0.06f, 0.10f, 0.13f, 0.76f);
                outline.effectDistance = new Vector2(1f, -1f);
            }
        }

        private static bool IsButtonImage(Image image)
        {
            return image.GetComponent<Button>() != null || image.GetComponentInParent<Button>()?.targetGraphic == image;
        }

        private static void ApplyCompactButtonShape(Button button)
        {
            var rect = button.transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            if (button.name.Contains("ActivityButton"))
            {
                rect.sizeDelta = new Vector2(148f, 148f);
                return;
            }

            if (button.name.Contains("Close"))
            {
                rect.sizeDelta = new Vector2(96f, 96f);
                return;
            }

            // Activity forms previously relied on thin, long action strips. Keep
            // their anchors intact but give them a generous compact touch target.
            if (button.name.Contains("Primary")
                || button.name.Contains("Materialize")
                || button.name.Contains("Turn")
                || button.name.Contains("Eat")
                || button.name.Contains("Reel"))
            {
                rect.sizeDelta = new Vector2(190f, 108f);
            }
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            return gameObject.GetComponent<T>() ?? gameObject.AddComponent<T>();
        }

        private static Sprite EnsureRoundedPanelSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(RoundedSpritePath) ?? UiArtDirectory);
            const int size = 128;
            const float cornerRadius = 28f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "SnowUi_RoundedPanel"
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distanceX = Mathf.Max(Mathf.Abs(x - (size - 1) * 0.5f) - (size * 0.5f - cornerRadius), 0f);
                    var distanceY = Mathf.Max(Mathf.Abs(y - (size - 1) * 0.5f) - (size * 0.5f - cornerRadius), 0f);
                    var edgeDistance = Mathf.Sqrt(distanceX * distanceX + distanceY * distanceY) - cornerRadius;
                    var alpha = Mathf.Clamp01(0.5f - edgeDistance);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(RoundedSpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(RoundedSpritePath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(RoundedSpritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius);
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
        }

        private static Sprite EnsureSoftCircleSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(CircleSpritePath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(CircleSpritePath) ?? UiArtDirectory);
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "SnowUi_SoftCircle"
            };
            var pixels = new Color32[size * size];
            var radius = (size - 2) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f));
                    var alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(CircleSpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(CircleSpritePath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(CircleSpritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(CircleSpritePath);
        }
    }
}
