using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using DemonViglu.FirePlay.Core;
using DemonViglu.FirePlay.World;

namespace DemonViglu.FirePlay.Editor
{
    /// <summary>
    /// Builds the first art-directed campfire LookDev scene.
    /// This is intentionally an editor-only presentation scene: it does not replace DemoScene
    /// and it does not own gameplay state.
    /// </summary>
    public static class FirePlayLookDevBuilder
    {
        private const string ScenePath = "Assets/Scenes/LookDev_Campfire.unity";
        private const string WideScenePath = "Assets/Scenes/LookDev_WideValley.unity";
        private const string GrandScenePath = "Assets/Scenes/LookDev_GrandValley.unity";
        private const string SnowScenePath = "Assets/Scenes/LookDev_SnowGrandValley.unity";
        private const string RootPath = "Assets/FirePlay/LookDev";
        private const string MaterialPath = RootPath + "/Materials";
        private const string AnimationPath = RootPath + "/Animations";
        private const string PreviewPath = "LookDev_Campfire_Preview.png";
        private const string WidePreviewPath = "LookDev_WideValley_Preview.png";
        private const string GrandPreviewPath = "LookDev_GrandValley_Preview.png";
        private const string SnowPreviewPath = "LookDev_SnowGrandValley_Preview.png";
        private const string NatureRoot = "Assets/Resources/Art/Stylized Nature MegaKit[Standard]/FBX (Unity)/";
        private const string SurvivalRoot = "Assets/Resources/Art/kenney_survival-kit/Models/FBX format/";
        private const string UltimateNatureRoot = "Assets/Resources/Art/Ultimate Nature Pack - Jun 2019-20260728T054020Z-1-001/Ultimate Nature Pack - Jun 2019/";
        private const string SnowNatureRoot = UltimateNatureRoot + "FBX/";

        // Ultimate Nature source models arrive with a +90 degree X-axis orientation.
        // Apply the inverse in local model space, then preserve the authored world yaw.
        private static readonly Quaternion UltimateNatureRotationCorrection = Quaternion.Euler(-90f, 0f, 0f);

        private static readonly Dictionary<string, Material> Materials = new();

        [MenuItem("FirePlay/LookDev/Build Campfire Presentation Scene")]
        public static void BuildScene()
        {
            EnsureFolders();
            LoadOrCreateMaterials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ConfigureRenderSettings();
            CreatePresentationRoot();
            CreateLightingAndVolumes();
            CreateGroundComposition();
            CreateCampfire();
            CreatePath();
            CreatePond();
            CreateVegetation();
            CreateLanterns();
            CreateStars();
            CreateCamera();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FirePlayLookDevBuilder] Saved {ScenePath}");
        }

        [MenuItem("FirePlay/LookDev/Build Campfire + Render Preview")]
        public static void BuildAndRender()
        {
            BuildScene();
            RenderPreview();
        }

        [MenuItem("FirePlay/LookDev/Build Wide Valley (100x Area)")]
        public static void BuildWideValleyScene()
        {
            EnsureFolders();
            LoadOrCreateMaterials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ConfigureWideRenderSettings();
            CreateWidePresentationRoot();
            CreateLightingAndVolumes();
            CreateWideValleyFloor();
            CreateWideLake();
            CreateWideBridge();
            CreateWideLandforms();
            CreateWideSparseVegetation();

            // The original small composition becomes one deliberately dense point of interest.
            CreateGroundComposition();
            CreateCampfire();
            CreatePath();
            CreateVegetation();
            CreateLanterns();

            CreateWideSkyDetails();
            CreateWideCamera();

            EditorSceneManager.SaveScene(scene, WideScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FirePlayLookDevBuilder] Saved {WideScenePath}");
        }

        [MenuItem("FirePlay/LookDev/Build Wide Valley + Render Preview")]
        public static void BuildWideValleyAndRender()
        {
            BuildWideValleyScene();
            RenderPreviewTo(WidePreviewPath);
        }

        [MenuItem("FirePlay/LookDev/Build Grand Valley (400x Area)")]
        public static void BuildGrandValleyScene()
        {
            EnsureFolders();
            LoadOrCreateMaterials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ConfigureGrandRenderSettings();
            CreateGrandPresentationRoot();
            CreateLightingAndVolumes();
            CreateGrandValleyFloor();
            CreateWideLake();
            CreateWideBridge();
            CreateWideLandforms();
            CreateGrandTerrainLayers();
            CreateGrandHighlandLake();
            CreateGrandCanyonBridge();
            CreateWideSparseVegetation();
            CreateGrandSparseLandmarks();

            // The intimate campfire remains a small, warm destination inside the huge valley.
            CreateGroundComposition();
            CreateCampfire();
            CreatePath();
            CreateVegetation();
            CreateLanterns();

            CreateGrandSkyDetails();
            CreateGrandCamera();

            EditorSceneManager.SaveScene(scene, GrandScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FirePlayLookDevBuilder] Saved {GrandScenePath}");
        }

        [MenuItem("FirePlay/LookDev/Build Grand Valley + Render Preview")]
        public static void BuildGrandValleyAndRender()
        {
            BuildGrandValleyScene();
            RenderPreviewTo(GrandPreviewPath);
        }

        [MenuItem("FirePlay/LookDev/Build Snow Grand Valley")]
        public static void BuildSnowGrandValleyScene()
        {
            EnsureFolders();
            LoadOrCreateMaterials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ConfigureSnowRenderSettings();
            CreateSnowPresentationRoot();
            CreateSnowLightingAndVolume();
            CreateSnowGrandValleyFloor();
            CreateSnowTerrainLayers();
            CreateFrozenLakes();
            CreateWideBridge();
            CreateGrandCanyonBridge();
            CreateSnowNorthCanyon();
            CreateSnowLandmarks();
            CreateSnowTreeGroves();
            CreateSnowDecorations();
            CreateSnowfall();

            CreateCampfire(true);
            CreateSnowEnvironmentWarmthSession();
            CreateLanterns();
            CreateSnowCamera();

            EditorSceneManager.SaveScene(scene, SnowScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FirePlayLookDevBuilder] Saved {SnowScenePath}");
        }

        [MenuItem("FirePlay/LookDev/Build Snow Grand Valley + Render Preview")]
        public static void BuildSnowGrandValleyAndRender()
        {
            BuildSnowGrandValleyScene();
            RenderPreviewTo(SnowPreviewPath);
        }

        public static void RenderPreview()
        {
            RenderPreviewTo(PreviewPath);
        }

        private static void RenderPreviewTo(string previewPath)
        {
            var camera = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                throw new InvalidOperationException("LookDev camera was not created.");
            }

            var previousActive = RenderTexture.active;
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32)
            {
                name = "LookDevPreviewTarget",
                antiAliasing = 1
            };
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            var absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, previewPath);
            File.WriteAllBytes(absolutePath, image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = previousActive;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
            Debug.Log($"[FirePlayLookDevBuilder] Rendered {absolutePath}");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/FirePlay", "LookDev");
            EnsureFolder(RootPath, "Materials");
            EnsureFolder(RootPath, "Animations");
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void LoadOrCreateMaterials()
        {
            Materials.Clear();
            Materials["ground"] = CreateMaterial("Ground", new Color(0.105f, 0.16f, 0.14f), 0.05f);
            Materials["groundEdge"] = CreateMaterial("GroundEdge", new Color(0.055f, 0.085f, 0.105f), 0.1f);
            Materials["path"] = CreateMaterial("PathStone", new Color(0.26f, 0.29f, 0.27f), 0.3f);
            Materials["pathWarm"] = CreateMaterial("PathWarm", new Color(0.38f, 0.31f, 0.23f), 0.25f);
            Materials["rock"] = CreateMaterial("Rock", new Color(0.16f, 0.19f, 0.20f), 0.35f);
            Materials["rockWarm"] = CreateMaterial("RockWarm", new Color(0.29f, 0.23f, 0.18f), 0.3f);
            Materials["trunk"] = CreateMaterial("Trunk", new Color(0.20f, 0.13f, 0.10f), 0.25f);
            Materials["trunkLight"] = CreateMaterial("TrunkLight", new Color(0.34f, 0.22f, 0.14f), 0.25f);
            Materials["leafDeep"] = CreateMaterial("LeafDeep", new Color(0.08f, 0.20f, 0.17f), 0.08f);
            Materials["leaf"] = CreateMaterial("Leaf", new Color(0.14f, 0.34f, 0.25f), 0.08f);
            Materials["leafWarm"] = CreateMaterial("LeafWarm", new Color(0.32f, 0.38f, 0.18f), 0.08f);
            Materials["grass"] = CreateMaterial("Grass", new Color(0.19f, 0.40f, 0.25f), 0.05f);
            Materials["flowerBlue"] = CreateMaterial("FlowerBlue", new Color(0.30f, 0.55f, 0.95f), 0.12f, true);
            Materials["flowerGold"] = CreateMaterial("FlowerGold", new Color(1.0f, 0.55f, 0.16f), 0.12f, true);
            Materials["water"] = CreateTransparentMaterial("Water", new Color(0.05f, 0.19f, 0.27f, 0.72f), 0.65f);
            Materials["waterGlow"] = CreateEmissionMaterial("WaterGlow", new Color(0.04f, 0.26f, 0.36f), 0.65f);
            Materials["wood"] = CreateMaterial("Wood", new Color(0.28f, 0.16f, 0.09f), 0.2f);
            Materials["ember"] = CreateEmissionMaterial("Ember", new Color(1.0f, 0.12f, 0.025f), 4.5f);
            Materials["flameOuter"] = CreateEmissionMaterial("FlameOuter", new Color(1.0f, 0.12f, 0.015f), 3.0f);
            Materials["flameInner"] = CreateEmissionMaterial("FlameInner", new Color(1.0f, 0.65f, 0.08f), 6.0f);
            Materials["lantern"] = CreateEmissionMaterial("Lantern", new Color(1.0f, 0.42f, 0.08f), 4.0f);
            Materials["moon"] = CreateEmissionMaterial("Moon", new Color(0.42f, 0.62f, 1.0f), 1.5f);
            Materials["star"] = CreateEmissionMaterial("Star", new Color(0.40f, 0.60f, 1.0f), 3.0f);
            Materials["meadow"] = CreateMaterial("WideMeadow", new Color(0.105f, 0.205f, 0.165f), 0.08f);
            Materials["meadowLight"] = CreateMaterial("WideMeadowLight", new Color(0.16f, 0.285f, 0.20f), 0.08f);
            Materials["mountainNear"] = CreateMaterial("MountainNear", new Color(0.14f, 0.20f, 0.22f), 0.22f);
            Materials["mountainFar"] = CreateMaterial("MountainFar", new Color(0.095f, 0.125f, 0.19f), 0.18f);
            Materials["bridge"] = CreateMaterial("BridgeStone", new Color(0.41f, 0.34f, 0.245f), 0.28f);
            Materials["shore"] = CreateMaterial("LakeShore", new Color(0.21f, 0.27f, 0.26f), 0.25f);
            Materials["snow"] = CreateMaterial("SnowField", new Color(0.84f, 0.90f, 0.95f), 0.38f);
            Materials["snowLight"] = CreateMaterial("SnowLight", new Color(0.94f, 0.965f, 1.0f), 0.48f);
            Materials["snowShadow"] = CreateMaterial("SnowShadow", new Color(0.48f, 0.61f, 0.72f), 0.30f);
            Materials["snowRock"] = CreateMaterial("SnowRock", new Color(0.58f, 0.66f, 0.72f), 0.28f);
            Materials["lakeBed"] = CreateMaterial("SnowLakeBed", new Color(0.12f, 0.24f, 0.30f), 0.22f);
            Materials["ice"] = CreateIcePathMaterial();
            Materials["openWater"] = CreateDepthWaterMaterial();
            Materials["snowflake"] = CreateParticleMaterial("Snowflake", new Color(0.88f, 0.95f, 1.0f, 0.82f));
            Materials["thawMist"] = CreateParticleMaterial("ThawMist", new Color(0.78f, 0.90f, 0.86f, 0.32f));
            Materials["warmthSnow"] = CreateWarmthSnowMaterial();
        }

        private static Material CreateWarmthSnowMaterial()
        {
            var path = MaterialPath + "/WarmthSnow.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("DemonViglu/FirePlay/URP Warmth Snow");
            if (shader == null)
            {
                throw new InvalidOperationException("URP Warmth Snow shader is missing or not compiled.");
            }

            if (material == null)
            {
                material = new Material(shader) { name = "WarmthSnow" };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", new Color(0.91f, 0.95f, 0.98f));
            material.SetColor("_WarmColor", new Color(0.25f, 0.22f, 0.14f));
            material.SetColor("_EdgeColor", new Color(0.55f, 0.43f, 0.24f));
            material.SetFloat("_Smoothness", 0.38f);
            material.SetFloat("_WarmSmoothness", 0.62f);
            return material;
        }

        private static Material CreateMaterial(string name, Color color, float smoothness, bool emission = false)
        {
            var path = MaterialPath + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color);
            }

            return material;
        }

        private static Material CreateEmissionMaterial(string name, Color color, float intensity)
        {
            var material = CreateMaterial(name, color, 0.1f, true);
            material.SetColor("_EmissionColor", color * intensity);
            return material;
        }

        private static Material CreateTransparentMaterial(string name, Color color, float smoothness)
        {
            var material = CreateMaterial(name, color, smoothness);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private static Material CreateParticleMaterial(string name, Color color)
        {
            var path = MaterialPath + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private static Material CreateIcePathMaterial()
        {
            var path = MaterialPath + "/IcePathCrack.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("DemonViglu/FirePlay/URP Ice Path Crack");
            if (shader == null)
            {
                throw new InvalidOperationException("URP Ice Path Crack shader is missing or not compiled.");
            }

            if (material == null)
            {
                material = new Material(shader) { name = "IcePathCrack" };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", new Color(0.68f, 0.88f, 0.96f, 0.84f));
            material.SetColor("_DeepColor", new Color(0.18f, 0.48f, 0.68f, 0.88f));
            material.SetColor("_CrackColor", new Color(0.88f, 0.98f, 1.0f, 1.0f));
            material.SetFloat("_CrackScale", 1.35f);
            material.SetFloat("_CrackThreshold", 0.12f);
            material.SetFloat("_BreakThreshold", 0.8f);
            material.SetFloat("_EdgeSoftness", 0.08f);
            material.SetFloat("_Smoothness", 0.82f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateDepthWaterMaterial()
        {
            var path = MaterialPath + "/SnowDepthWater.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("DemonViglu/FirePlay/URP Depth Water");
            if (shader == null)
            {
                throw new InvalidOperationException("URP Depth Water shader is missing or not compiled.");
            }

            if (material == null)
            {
                material = new Material(shader) { name = "SnowDepthWater" };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.SetColor("_ShallowColor", new Color(0.20f, 0.62f, 0.72f, 0.32f));
            material.SetColor("_DeepColor", new Color(0.025f, 0.12f, 0.24f, 0.92f));
            material.SetColor("_FresnelColor", new Color(0.72f, 0.92f, 1.0f, 0.72f));
            material.SetFloat("_DepthDistance", 7.5f);
            material.SetFloat("_WaveScale", 0.14f);
            material.SetFloat("_WaveSpeed", 0.22f);
            material.SetFloat("_WaveStrength", 0.075f);
            material.SetFloat("_Smoothness", 0.88f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreatePresentationRoot()
        {
            var root = new GameObject("LOOKDEV_Campfire_Presentation");
            root.tag = "Untagged";
            return root;
        }

        private static GameObject CreateWidePresentationRoot()
        {
            var root = new GameObject("LOOKDEV_Wide_Valley_240x240m");
            root.tag = "Untagged";
            return root;
        }

        private static GameObject CreateGrandPresentationRoot()
        {
            var root = new GameObject("LOOKDEV_Grand_Valley_480x480m");
            root.tag = "Untagged";
            return root;
        }

        private static GameObject CreateSnowPresentationRoot()
        {
            var root = new GameObject("LOOKDEV_Snow_Grand_Valley_480x480m");
            root.tag = "Untagged";
            return root;
        }

        private static void ConfigureRenderSettings()
        {
            const string skyPath = MaterialPath + "/LookDev_NightSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural")) { name = "LookDev_NightSky" };
                AssetDatabase.CreateAsset(sky, skyPath);
            }

            sky.SetColor("_SkyTint", new Color(0.045f, 0.085f, 0.16f));
            sky.SetColor("_GroundColor", new Color(0.015f, 0.025f, 0.04f));
            sky.SetColor("_GroundColor", new Color(0.025f, 0.045f, 0.07f));
            sky.SetFloat("_AtmosphereThickness", 0.55f);
            sky.SetFloat("_Exposure", 0.55f);
            sky.SetFloat("_SunSize", 0.02f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 0.35f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.045f, 0.075f, 0.105f);
            RenderSettings.fogDensity = 0.018f;
            RenderSettings.reflectionIntensity = 0.4f;
        }

        private static void ConfigureWideRenderSettings()
        {
            ConfigureRenderSettings();
            RenderSettings.ambientIntensity = 0.48f;
            RenderSettings.fogColor = new Color(0.075f, 0.105f, 0.145f);
            RenderSettings.fogDensity = 0.0065f;
            RenderSettings.reflectionIntensity = 0.48f;
        }

        private static void ConfigureGrandRenderSettings()
        {
            ConfigureRenderSettings();
            RenderSettings.ambientIntensity = 0.52f;
            RenderSettings.fogColor = new Color(0.085f, 0.115f, 0.16f);
            RenderSettings.fogDensity = 0.0032f;
            RenderSettings.reflectionIntensity = 0.5f;
        }

        private static void ConfigureSnowRenderSettings()
        {
            const string skyPath = MaterialPath + "/LookDev_SnowSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural")) { name = "LookDev_SnowSky" };
                AssetDatabase.CreateAsset(sky, skyPath);
            }

            sky.SetColor("_SkyTint", new Color(0.48f, 0.68f, 0.86f));
            sky.SetColor("_GroundColor", new Color(0.58f, 0.67f, 0.75f));
            sky.SetFloat("_AtmosphereThickness", 0.78f);
            sky.SetFloat("_Exposure", 1.18f);
            sky.SetFloat("_SunSize", 0.035f);
            EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 0.72f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.64f, 0.75f, 0.84f);
            RenderSettings.fogDensity = 0.0022f;
            RenderSettings.reflectionIntensity = 0.68f;
        }

        private static void CreateLightingAndVolumes()
        {
            var moon = new GameObject("Moonlight");
            var moonLight = moon.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.color = new Color(0.31f, 0.46f, 0.78f);
            moonLight.intensity = 0.42f;
            moonLight.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(38f, -32f, 0f);

            var volumeObject = new GameObject("LookDev Volume");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            const string profilePath = MaterialPath + "/LookDev_NightProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "LookDev_NightProfile";
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            else
            {
                profile.components.Clear();
            }

            var color = profile.Add<ColorAdjustments>();
            color.postExposure.overrideState = true;
            color.postExposure.value = -0.18f;
            color.contrast.overrideState = true;
            color.contrast.value = 14f;
            color.saturation.overrideState = true;
            color.saturation.value = -6f;
            color.colorFilter.overrideState = true;
            color.colorFilter.value = new Color(0.78f, 0.86f, 1.0f);

            var bloom = profile.Add<Bloom>();
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.05f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.38f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.55f;

            var vignette = profile.Add<Vignette>();
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.18f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.55f;
            EditorUtility.SetDirty(profile);
            volume.profile = profile;
        }

        private static void CreateSnowLightingAndVolume()
        {
            var sun = new GameObject("Snow_Sun");
            var sunLight = sun.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = new Color(1.0f, 0.91f, 0.79f);
            sunLight.intensity = 1.22f;
            sunLight.shadows = LightShadows.Soft;
            sunLight.shadowStrength = 0.82f;
            sunLight.shadowNearPlane = 0.2f;
            sun.transform.rotation = Quaternion.Euler(38f, -48f, 0f);
            RenderSettings.sun = sunLight;

            var fill = new GameObject("Snow_Sky_Fill");
            var fillLight = fill.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.color = new Color(0.34f, 0.53f, 0.80f);
            fillLight.intensity = 0.22f;
            fillLight.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(52f, 132f, 0f);

            var volumeObject = new GameObject("Snow LookDev Volume");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 20f;
            const string profilePath = MaterialPath + "/LookDev_SnowProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "LookDev_SnowProfile";
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            else
            {
                profile.components.Clear();
            }

            var color = profile.Add<ColorAdjustments>();
            color.postExposure.overrideState = true;
            color.postExposure.value = 0.12f;
            color.contrast.overrideState = true;
            color.contrast.value = 18f;
            color.saturation.overrideState = true;
            color.saturation.value = -6f;
            color.colorFilter.overrideState = true;
            color.colorFilter.value = new Color(0.90f, 0.96f, 1.0f);

            var bloom = profile.Add<Bloom>();
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.25f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.11f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.36f;

            var vignette = profile.Add<Vignette>();
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.07f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.62f;
            EditorUtility.SetDirty(profile);
            volume.profile = profile;
        }

        private static void CreateSnowGrandValleyFloor()
        {
            var root = new GameObject("00_Snow_Grand_Ground");
            var mainLakeCenter = new Vector2(56f, 27f);
            var mainLakeOpening = new Vector2(78f, 112f);
            CreateRectangularSurfaceAroundHole("SnowValleyBase", root.transform, Vector2.zero, new Vector2(480f, 480f), mainLakeCenter, mainLakeOpening, -1.2f, 2.2f, Materials["snow"], true);
            CreateRectangularSurfaceAroundHole("BrightSnowBasin", root.transform, new Vector2(-12f, 3f), new Vector2(252f, 214f), mainLakeCenter, mainLakeOpening, 0.02f, 0.18f, Materials["snowLight"], false);
            CreateBox("BlueSnowField_West", root.transform, new Vector3(-135f, 0.01f, -118f), new Vector3(146f, 0.16f, 124f), Materials["snowShadow"], Quaternion.Euler(0f, 8f, 0f));
            CreateBox("WindSweptField_East", root.transform, new Vector3(160f, 0.01f, 122f), new Vector3(110f, 0.16f, 130f), Materials["snow"], Quaternion.Euler(0f, -7f, 0f));
            CreateBox("SnowShelf_West", root.transform, new Vector3(-92f, 1.3f, 30f), new Vector3(68f, 2.8f, 142f), Materials["snow"], Quaternion.Euler(0f, 9f, -1.4f), true);
            CreateBox("SnowShelf_East", root.transform, new Vector3(132f, 1.7f, -58f), new Vector3(70f, 3.6f, 126f), Materials["snow"], Quaternion.Euler(0f, -10f, 1.4f), true);
            // Keep a broad, traversable opening on the north axis. A single shelf here
            // visually sealed the canyon even though canyon objects existed behind it.
            CreateBox("SnowShelf_NorthWest", root.transform, new Vector3(-60f, 2.2f, 137f), new Vector3(58f, 4.6f, 65f), Materials["snowLight"], Quaternion.Euler(0f, 4f, -1.1f), true);
            CreateBox("SnowShelf_NorthEast", root.transform, new Vector3(92f, 2.2f, 137f), new Vector3(76f, 4.6f, 65f), Materials["snowLight"], Quaternion.Euler(0f, -4f, 1.1f), true);
        }

        private static void CreateSnowTerrainLayers()
        {
            var root = new GameObject("01_Snow_Terrain_Layers");

            CreateBox("SnowMesa_Lower", root.transform, new Vector3(-176f, 7f, 52f), new Vector3(100f, 15f, 176f), Materials["snowRock"], Quaternion.Euler(0f, 7f, -2f), true);
            CreateBox("SnowMesa_Middle", root.transform, new Vector3(-184f, 18f, 72f), new Vector3(79f, 24f, 132f), Materials["snow"], Quaternion.Euler(0f, -3f, 1.5f), true);
            CreateRectangularSurfaceAroundHole("SnowMesa_Top", root.transform, new Vector2(-177f, 91f), new Vector2(66f, 91f), new Vector2(-176f, 94f), new Vector2(58f, 42f), 33f, 13f, Materials["snowLight"], true);
            CreateRamp("SnowMesa_LongRamp", root.transform, new Vector3(-99f, 1.5f, -8f), new Vector3(-151f, 35f, 59f), 30f, 3f, Materials["snowLight"]);

            CreateBox("SnowEast_Lower", root.transform, new Vector3(184f, 8f, -32f), new Vector3(100f, 17f, 201f), Materials["snowRock"], Quaternion.Euler(0f, -8f, 2f), true);
            CreateBox("SnowEast_Upper", root.transform, new Vector3(198f, 24f, -48f), new Vector3(69f, 24f, 145f), Materials["snow"], Quaternion.Euler(0f, 4f, -1.5f), true);
            CreateRamp("SnowEast_LongRamp", root.transform, new Vector3(105f, 1.8f, -87f), new Vector3(169f, 27f, -71f), 38f, 3f, Materials["snow"]);

            // Split the former continuous north ridge into two mountain shoulders.
            // The 50m central gap is the actual canyon, rather than scenery layered
            // in front of an unbroken wall.
            CreateBox("SnowNorthWest_Step01", root.transform, new Vector3(-83f, 10f, 186f), new Vector3(78f, 20f, 45f), Materials["snowRock"], Quaternion.Euler(0f, 5f, -2f), true);
            CreateBox("SnowNorthWest_Step02", root.transform, new Vector3(-91f, 24f, 211f), new Vector3(66f, 20f, 37f), Materials["snowShadow"], Quaternion.Euler(0f, -5f, 3f), true);
            CreateBox("SnowNorthWest_Step03", root.transform, new Vector3(-99f, 40f, 232f), new Vector3(54f, 25f, 31f), Materials["snowRock"], Quaternion.Euler(0f, 7f, -4f), true);
            CreateBox("SnowNorthEast_Step01", root.transform, new Vector3(112f, 12f, 188f), new Vector3(86f, 24f, 48f), Materials["snowRock"], Quaternion.Euler(0f, -6f, 2f), true);
            CreateBox("SnowNorthEast_Step02", root.transform, new Vector3(120f, 28f, 214f), new Vector3(70f, 22f, 39f), Materials["snowShadow"], Quaternion.Euler(0f, 5f, -3f), true);
            CreateBox("SnowNorthEast_Step03", root.transform, new Vector3(130f, 46f, 236f), new Vector3(58f, 28f, 33f), Materials["snowRock"], Quaternion.Euler(0f, -7f, 4f), true);

            CreateBox("SnowCanyon_West", root.transform, new Vector3(-84f, 16f, -191f), new Vector3(124f, 33f, 67f), Materials["snowRock"], Quaternion.Euler(0f, -6f, 3f), true);
            CreateBox("SnowCanyon_East", root.transform, new Vector3(79f, 20f, -199f), new Vector3(118f, 41f, 70f), Materials["snowShadow"], Quaternion.Euler(0f, 8f, -3f), true);
            CreateBox("SnowCanyon_Back", root.transform, new Vector3(2f, 44f, -244f), new Vector3(245f, 58f, 35f), Materials["snowRock"], Quaternion.Euler(0f, 2f, -2f));

            var skyline = new[]
            {
                (new Vector3(-250f, 65f, 151f), new Vector3(90f, 130f, 102f), new Vector3(0f, 15f, -17f)),
                (new Vector3(-170f, 84f, 259f), new Vector3(126f, 168f, 73f), new Vector3(0f, -8f, 13f)),
                (new Vector3(-58f, 70f, 274f), new Vector3(72f, 140f, 69f), new Vector3(0f, 7f, -11f)),
                (new Vector3(111f, 76f, 277f), new Vector3(76f, 152f, 72f), new Vector3(0f, -6f, 10f)),
                (new Vector3(207f, 91f, 234f), new Vector3(112f, 182f, 80f), new Vector3(0f, -12f, 15f)),
                (new Vector3(270f, 73f, 57f), new Vector3(78f, 146f, 134f), new Vector3(0f, 11f, -14f)),
                (new Vector3(-275f, 77f, -88f), new Vector3(82f, 154f, 151f), new Vector3(0f, -9f, 12f))
            };
            for (var i = 0; i < skyline.Length; i++)
            {
                CreateBox("Snow_Skyline_Mass", root.transform, skyline[i].Item1, skyline[i].Item2, i % 2 == 0 ? Materials["snowRock"] : Materials["snowShadow"], Quaternion.Euler(skyline[i].Item3));
            }
        }

        private static void CreateFrozenLakes()
        {
            var root = new GameObject("02_Frozen_Lakes");

            // The water surface has no collider. Once ice triangles disappear the player
            // passes through it and lands in the physical bowl-shaped lake bed below.
            CreateLakeBasin("MainLake_PhysicalBasin", root.transform, new Vector3(56f, 0.12f, 27f), 76f, 110f, 11.5f, 72, Materials["lakeBed"]);
            CreateUnderwaterVolume("MainLake_UnderwaterVolume", root.transform, new Vector3(56f, 0.12f, 27f), 76f, 110f, 11.5f);
            CreatePlayerWaterVolume("MainLake_PlayerWaterVolume", root.transform, new Vector3(56f, 0.16f, 27f), 76f, 110f, 11.5f);
            var mainWater = CreateDisc("MainLake_ColdWater", root.transform, new Vector3(56f, 0.16f, 27f), 38f, 0.06f, Materials["openWater"], 72, 0.045f, new Vector3(0.92f, 1f, 1.42f));
            mainWater.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            CreateIcePathField(root.transform, "MainLake_PathDriven_IceSurface", new Vector3(56f, 0.38f, 27f), 70f, 100f, 1.35f, 512, "snow-grand-main-ice-field");

            CreateLakeBasin("HighlandLake_PhysicalBasin", root.transform, new Vector3(-176f, 40.06f, 94f), 56f, 40f, 9f, 56, Materials["lakeBed"]);
            CreateUnderwaterVolume("HighlandLake_UnderwaterVolume", root.transform, new Vector3(-176f, 40.06f, 94f), 56f, 40f, 9f);
            CreatePlayerWaterVolume("HighlandLake_PlayerWaterVolume", root.transform, new Vector3(-176f, 40.1f, 94f), 56f, 40f, 9f);
            var highWater = CreateDisc("HighlandLake_ColdWater", root.transform, new Vector3(-176f, 40.1f, 94f), 23f, 0.06f, Materials["openWater"], 56, 0.055f, new Vector3(1.15f, 1f, 0.72f));
            highWater.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            CreateIcePathField(root.transform, "HighlandLake_PathDriven_IceSurface", new Vector3(-176f, 40.34f, 94f), 54f, 36f, 1.1f, 384, "snow-grand-highland-ice-field");
        }

        private static void CreateIcePathField(Transform parent, string name, Vector3 position, float width, float depth, float collisionCellSize, int maskResolution, string stableId)
        {
            var field = new GameObject(name);
            field.transform.SetParent(parent);
            field.transform.position = position;
            field.AddComponent<MeshFilter>();
            var renderer = field.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Materials["ice"];
            field.AddComponent<MeshCollider>();

            var stableSceneId = field.AddComponent<StableSceneId>();
            var serializedId = new SerializedObject(stableSceneId);
            serializedId.FindProperty("_value").stringValue = stableId;
            serializedId.ApplyModifiedPropertiesWithoutUndo();

            var warmthNode = field.AddComponent<WarmthNode>();
            var serializedWarmth = new SerializedObject(warmthNode);
            serializedWarmth.FindProperty("_radius").floatValue = 14f;
            serializedWarmth.FindProperty("_transitionSpeed").floatValue = 1.25f;
            serializedWarmth.ApplyModifiedPropertiesWithoutUndo();

            var fieldType = Type.GetType("DemonViglu.FirePlay.World.IcePathCrackField, Assembly-CSharp");
            if (fieldType == null)
            {
                throw new InvalidOperationException("IcePathCrackField is not compiled yet. Wait for Unity script refresh and run the builder again.");
            }

            var fieldComponent = field.AddComponent(fieldType);
            var serializedField = new SerializedObject(fieldComponent);
            serializedField.FindProperty("_width").floatValue = width;
            serializedField.FindProperty("_depth").floatValue = depth;
            serializedField.FindProperty("_collisionCellSize").floatValue = collisionCellSize;
            serializedField.FindProperty("_maskResolution").intValue = maskResolution;
            serializedField.FindProperty("_iceMaterial").objectReferenceValue = Materials["ice"];
            serializedField.FindProperty("_warmthNode").objectReferenceValue = warmthNode;
            serializedField.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateSnowLandmarks()
        {
            var root = new GameObject("03_Snow_Landmarks");
            var placements = new[]
            {
                ("PineTree_Snow_3.fbx", new Vector3(-155f, 40.2f, 66f), 1.6f, 12f),
                ("PineTree_Snow_5.fbx", new Vector3(-198f, 40.2f, 111f), 1.9f, 84f),
                ("BirchTree_Dead_Snow_2.fbx", new Vector3(-169f, 40.2f, 125f), 1.5f, 143f),
                ("CommonTree_Snow_4.fbx", new Vector3(182f, 36.4f, -18f), 1.8f, 25f),
                ("PineTree_Snow_2.fbx", new Vector3(206f, 36.4f, -66f), 1.6f, 117f),
                ("BirchTree_Snow_3.fbx", new Vector3(-22f, 51.8f, 223f), 1.7f, 64f),
                ("CommonTree_Dead_Snow_4.fbx", new Vector3(-72f, 52f, 224f), 1.5f, 151f),
                ("PineTree_Snow_5.fbx", new Vector3(-120f, 33.1f, -172f), 1.8f, 202f),
                ("BirchTree_Dead_Snow_5.fbx", new Vector3(127f, 41.1f, -182f), 1.6f, 272f),
                ("CommonTree_Snow_2.fbx", new Vector3(18f, 0.3f, 78f), 1.45f, 45f),
                ("PineTree_Snow_4.fbx", new Vector3(-48f, 0.3f, 62f), 1.55f, 119f)
            };
            foreach (var placement in placements)
            {
                InstantiateModel(SnowNatureRoot + placement.Item1, root.transform, placement.Item2, Vector3.one * placement.Item3, Quaternion.Euler(0f, placement.Item4, 0f), "Snow_Landmark");
            }

            var rockPositions = new[]
            {
                new Vector3(22f, 0.35f, -5f), new Vector3(93f, 0.35f, 49f), new Vector3(-138f, 40.3f, 72f),
                new Vector3(151f, 36.5f, -32f), new Vector3(-112f, 33.2f, -168f)
            };
            for (var i = 0; i < rockPositions.Length; i++)
            {
                InstantiateModel(SnowNatureRoot + $"Rock_Snow_{i % 7 + 1}.fbx", root.transform, rockPositions[i], Vector3.one * (1.5f + i * 0.14f), Quaternion.Euler(0f, i * 49f, 0f), "Snow_Rock");
            }
        }

        private static void CreateSnowNorthCanyon()
        {
            var root = new GameObject("03_North_Canyon");

            // This canyon sits on the basin's north axis and continues through the
            // skyline opening, so it is readable from the lake and campfire areas.
            CreateBox("NorthCanyon_WestWall", root.transform, new Vector3(-36f, 24f, 214f), new Vector3(50f, 48f, 112f), Materials["snowRock"], Quaternion.Euler(0f, -6f, 4f), true);
            CreateBox("NorthCanyon_EastWall", root.transform, new Vector3(69f, 30f, 216f), new Vector3(54f, 60f, 116f), Materials["snowShadow"], Quaternion.Euler(0f, 8f, -5f), true);
            CreateBox("NorthCanyon_Floor", root.transform, new Vector3(16f, 2.8f, 218f), new Vector3(54f, 5.4f, 126f), Materials["snowLight"], Quaternion.Euler(0f, 0f, 0f), true);
            CreateRamp("NorthCanyon_Approach", root.transform, new Vector3(15f, 0.7f, 126f), new Vector3(16f, 5.4f, 168f), 42f, 2.2f, Materials["snow"]);
            CreateRamp("NorthCanyon_FarPass", root.transform, new Vector3(16f, 5.4f, 259f), new Vector3(22f, 12f, 286f), 45f, 2.4f, Materials["snowLight"]);

            for (var index = 0; index < 9; index++)
            {
                var t = index / 8f;
                var x = -12f + t * 58f;
                var arch = Mathf.Sin(t * Mathf.PI) * 3.2f;
                CreateBox("NorthCanyon_BridgeDeck", root.transform, new Vector3(x, 30f + arch, 224f), new Vector3(7.8f, 0.9f, 7f), Materials["bridge"], Quaternion.Euler(0f, 0f, Mathf.Cos(t * Mathf.PI) * -3f), true);
            }

            var canyonRocks = new[]
            {
                (new Vector3(-12f, 6f, 170f), 3.6f, 24f),
                (new Vector3(48f, 7f, 177f), 3.1f, 116f),
                (new Vector3(-9f, 6f, 255f), 2.8f, 71f),
                (new Vector3(44f, 7f, 264f), 3.4f, 153f)
            };
            for (var index = 0; index < canyonRocks.Length; index++)
            {
                var rock = canyonRocks[index];
                InstantiateModel(SnowNatureRoot + $"Rock_Snow_{index % 7 + 1}.fbx", root.transform, rock.Item1, Vector3.one * rock.Item2, Quaternion.Euler(0f, rock.Item3, 0f), "NorthCanyon_SnowRock");
            }

            var canyonTrees = new[]
            {
                ("PineTree_Snow_5.fbx", new Vector3(-48f, 48.5f, 181f), 2.6f, 72f),
                ("PineTree_Snow_3.fbx", new Vector3(-39f, 48.5f, 207f), 2.2f, 11f),
                ("BirchTree_Dead_Snow_4.fbx", new Vector3(-27f, 48.5f, 239f), 2.0f, 187f),
                ("CommonTree_Snow_4.fbx", new Vector3(57f, 60.5f, 181f), 2.4f, 31f),
                ("PineTree_Snow_2.fbx", new Vector3(72f, 60.5f, 211f), 2.8f, 118f),
                ("BirchTree_Snow_3.fbx", new Vector3(63f, 60.5f, 246f), 2.2f, 219f),
                ("PineTree_Snow_4.fbx", new Vector3(-15f, 6f, 275f), 2.5f, 46f),
                ("CommonTree_Dead_Snow_3.fbx", new Vector3(46f, 7f, 278f), 2.1f, 163f)
            };
            foreach (var tree in canyonTrees)
            {
                InstantiateModel(SnowNatureRoot + tree.Item1, root.transform, tree.Item2, Vector3.one * tree.Item3, Quaternion.Euler(0f, tree.Item4, 0f), "NorthCanyon_LandmarkTree");
            }
        }

        private static void CreateSnowTreeGroves()
        {
            var root = new GameObject("04_Sparse_Snow_Tree_Groves");

            // Deliberate clusters around the open spaces: these frame routes and
            // landmarks without turning the 480m valley into an even forest grid.
            var trees = new[]
            {
                // Main lake western and eastern shores.
                ("PineTree_Snow_3.fbx", new Vector3(7f, 0.35f, 16f), 1.9f, 18f),
                ("CommonTree_Snow_2.fbx", new Vector3(3f, 0.35f, 34f), 1.6f, 91f),
                ("BirchTree_Dead_Snow_2.fbx", new Vector3(12f, 0.35f, 54f), 1.7f, 146f),
                ("PineTree_Snow_5.fbx", new Vector3(99f, 0.35f, -14f), 2.1f, 71f),
                ("CommonTree_Dead_Snow_4.fbx", new Vector3(107f, 0.35f, 12f), 1.7f, 203f),
                ("PineTree_Snow_2.fbx", new Vector3(103f, 0.35f, 66f), 1.9f, 122f),

                // West mesa rim and highland lake overlook.
                ("PineTree_Snow_4.fbx", new Vector3(-142f, 40.3f, 43f), 2.2f, 33f),
                ("BirchTree_Snow_2.fbx", new Vector3(-153f, 40.3f, 54f), 1.8f, 104f),
                ("PineTree_Snow_5.fbx", new Vector3(-204f, 40.3f, 68f), 2.4f, 177f),
                ("CommonTree_Dead_Snow_2.fbx", new Vector3(-210f, 40.3f, 97f), 1.9f, 246f),
                ("BirchTree_Dead_Snow_5.fbx", new Vector3(-144f, 40.3f, 120f), 1.8f, 309f),

                // East plateau silhouette.
                ("CommonTree_Snow_5.fbx", new Vector3(164f, 36.5f, 11f), 2.3f, 52f),
                ("PineTree_Snow_3.fbx", new Vector3(177f, 36.5f, 28f), 2.0f, 136f),
                ("BirchTree_Dead_Snow_3.fbx", new Vector3(211f, 36.5f, 6f), 1.9f, 214f),
                ("PineTree_Snow_5.fbx", new Vector3(221f, 36.5f, -29f), 2.4f, 287f),

                // North approach: two sparse lines pull the eye into the pass.
                ("PineTree_Snow_2.fbx", new Vector3(-24f, 0.35f, 105f), 1.9f, 15f),
                ("BirchTree_Snow_4.fbx", new Vector3(-18f, 0.35f, 126f), 1.7f, 84f),
                ("PineTree_Snow_4.fbx", new Vector3(-21f, 4.9f, 148f), 2.2f, 155f),
                ("CommonTree_Snow_3.fbx", new Vector3(51f, 0.35f, 108f), 1.8f, 219f),
                ("BirchTree_Dead_Snow_4.fbx", new Vector3(55f, 0.35f, 132f), 1.7f, 278f),
                ("PineTree_Snow_5.fbx", new Vector3(53f, 4.9f, 153f), 2.3f, 337f),

                // A small southern counterweight; the broad snowfield remains empty.
                ("PineTree_Snow_3.fbx", new Vector3(-84f, 0.35f, -111f), 2.0f, 41f),
                ("CommonTree_Dead_Snow_3.fbx", new Vector3(-72f, 0.35f, -123f), 1.7f, 128f),
                ("BirchTree_Snow_2.fbx", new Vector3(105f, 0.35f, -126f), 1.8f, 231f),
                ("PineTree_Snow_4.fbx", new Vector3(118f, 0.35f, -118f), 2.1f, 302f)
            };

            foreach (var tree in trees)
            {
                InstantiateModel(SnowNatureRoot + tree.Item1, root.transform, tree.Item2, Vector3.one * tree.Item3, Quaternion.Euler(0f, tree.Item4, 0f), "SnowGrove_Tree");
            }

            var groveRocks = new[]
            {
                new Vector3(-3f, 0.35f, 49f), new Vector3(111f, 0.35f, 55f),
                new Vector3(-145f, 40.3f, 104f), new Vector3(170f, 36.5f, 20f),
                new Vector3(-26f, 0.35f, 119f), new Vector3(57f, 0.35f, 138f)
            };
            for (var index = 0; index < groveRocks.Length; index++)
            {
                InstantiateModel(SnowNatureRoot + $"Rock_Snow_{index % 7 + 1}.fbx", root.transform, groveRocks[index], Vector3.one * (1.4f + index % 3 * 0.3f), Quaternion.Euler(0f, index * 57f, 0f), "SnowGrove_Rock");
            }
        }

        private static void CreateSnowDecorations()
        {
            var root = new GameObject("04_Snow_Lived_In_Decorations");

            // Campfire rest point: a compact lived-in cluster, leaving the surrounding basin open.
            InstantiateModel(SurvivalRoot + "tent.fbx", root.transform, new Vector3(-8f, 0.36f, 6f), Vector3.one * 1.35f, Quaternion.Euler(0f, 28f, 0f), "SnowCamp_Tent");
            InstantiateModel(SurvivalRoot + "bedroll-packed.fbx", root.transform, new Vector3(-6.2f, 0.36f, 3.6f), Vector3.one * 1.15f, Quaternion.Euler(0f, 63f, 0f), "SnowCamp_Bedroll");
            InstantiateModel(SurvivalRoot + "box.fbx", root.transform, new Vector3(-10f, 0.36f, 2.9f), Vector3.one * 1.1f, Quaternion.Euler(0f, -12f, 0f), "SnowCamp_Crate");
            InstantiateModel(SurvivalRoot + "barrel.fbx", root.transform, new Vector3(-11.8f, 0.36f, 5.3f), Vector3.one * 1.05f, Quaternion.Euler(0f, 11f, 0f), "SnowCamp_Barrel");
            InstantiateModel(SurvivalRoot + "resource-wood.fbx", root.transform, new Vector3(-5.2f, 0.36f, 7.8f), Vector3.one * 1.2f, Quaternion.Euler(0f, 84f, 0f), "SnowCamp_Firewood");

            // Main lake shore gives the frozen lake a clear entry and story cue.
            InstantiateModel(SurvivalRoot + "signpost-single.fbx", root.transform, new Vector3(13f, 0.36f, -9f), Vector3.one * 1.45f, Quaternion.Euler(0f, 36f, 0f), "FrozenLake_Signpost");
            InstantiateModel(SurvivalRoot + "campfire-fishing-stand.fbx", root.transform, new Vector3(18f, 0.36f, 14f), Vector3.one * 1.2f, Quaternion.Euler(0f, -24f, 0f), "FrozenLake_FishingStand");
            InstantiateModel(SurvivalRoot + "bucket.fbx", root.transform, new Vector3(21.2f, 0.36f, 12.5f), Vector3.one, Quaternion.Euler(0f, 17f, 0f), "FrozenLake_Bucket");
            InstantiateModel(SurvivalRoot + "resource-planks.fbx", root.transform, new Vector3(21f, 0.36f, 16.8f), Vector3.one * 1.15f, Quaternion.Euler(0f, 78f, 0f), "FrozenLake_Planks");

            // Northern canyon gate is a second readable destination.
            InstantiateModel(SurvivalRoot + "signpost.fbx", root.transform, new Vector3(15f, 1f, 145f), Vector3.one * 1.8f, Quaternion.Euler(0f, 182f, 0f), "NorthCanyon_Signpost");
            InstantiateModel(SurvivalRoot + "tent-canvas.fbx", root.transform, new Vector3(-7f, 5.8f, 178f), Vector3.one * 1.5f, Quaternion.Euler(0f, 14f, 0f), "NorthCanyon_Shelter");
            InstantiateModel(SurvivalRoot + "chest.fbx", root.transform, new Vector3(-2f, 5.8f, 175f), Vector3.one * 1.2f, Quaternion.Euler(0f, -18f, 0f), "NorthCanyon_Chest");
            InstantiateModel(SurvivalRoot + "resource-stone-large.fbx", root.transform, new Vector3(42f, 5.8f, 181f), Vector3.one * 1.35f, Quaternion.Euler(0f, 91f, 0f), "NorthCanyon_StoneSupplies");
        }

        private static void CreateSnowfall()
        {
            var root = new GameObject("04_Snowfall_VFX");
            var particles = root.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.duration = 12f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 13f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.82f, 0.92f, 1f, 0.48f), new Color(1f, 1f, 1f, 0.92f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 2200;
            main.gravityModifier = 0.025f;

            var emission = particles.emission;
            emission.rateOverTime = 145f;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(360f, 1f, 360f);
            root.transform.position = new Vector3(0f, 110f, 0f);

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
            velocity.y = new ParticleSystem.MinMaxCurve(-1.0f, -2.1f);
            velocity.z = new ParticleSystem.MinMaxCurve(0.1f, 0.55f);

            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.42f;
            noise.frequency = 0.16f;
            noise.scrollSpeed = 0.18f;
            noise.damping = true;

            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = Materials["snowflake"];
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            particles.Play();
        }

        private static void CreateSnowCamera()
        {
            var cameraObject = new GameObject("Snow Grand Valley Camera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1400f;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.depthTextureMode |= DepthTextureMode.Depth;
            camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
            camera.transform.position = new Vector3(-268f, 154f, -312f);
            LookAt(camera.transform, new Vector3(14f, 17f, 27f));
            camera.backgroundColor = new Color(0.66f, 0.76f, 0.86f);
        }

        private static void CreateWideValleyFloor()
        {
            var root = new GameObject("00_Wide_Open_Ground");

            // 240 x 240 metres: roughly ten times the original LookDev width and 100x its area.
            CreateBox("ValleyBase_240m", root.transform, new Vector3(0f, -1.1f, 0f), new Vector3(240f, 2f, 240f), Materials["meadow"], Quaternion.identity, true);

            // Broad tonal fields break the ground plane without turning it into noisy detail.
            CreateBox("QuietMeadow_West", root.transform, new Vector3(-62f, 0.02f, 22f), new Vector3(98f, 0.18f, 126f), Materials["meadowLight"], Quaternion.Euler(0f, -7f, 0f));
            CreateBox("QuietMeadow_South", root.transform, new Vector3(18f, 0.01f, -72f), new Vector3(126f, 0.16f, 56f), Materials["ground"], Quaternion.Euler(0f, 4f, 0f));

            // A few long, low terraces give readable elevation while keeping most traversal flat.
            CreateBox("LowTerrace_NorthWest", root.transform, new Vector3(-70f, 1.15f, 76f), new Vector3(70f, 2.5f, 42f), Materials["meadow"], Quaternion.Euler(0f, 8f, -1.2f), true);
            CreateBox("LowTerrace_SouthEast", root.transform, new Vector3(77f, 0.8f, -67f), new Vector3(61f, 1.8f, 48f), Materials["meadow"], Quaternion.Euler(0f, -11f, 1.1f), true);
        }

        private static void CreateGrandValleyFloor()
        {
            var root = new GameObject("00_Grand_Open_Ground_480m");

            // Four times Wide Valley's area and 400x the original campfire slice.
            CreateBox("GrandValleyBase_480m", root.transform, new Vector3(0f, -1.2f, 0f), new Vector3(480f, 2.2f, 480f), Materials["meadow"], Quaternion.identity, true);

            // Huge quiet fields use broad value changes rather than repeated props.
            CreateBox("OpenBasin_Central", root.transform, new Vector3(0f, 0.02f, 3f), new Vector3(245f, 0.18f, 205f), Materials["meadowLight"], Quaternion.Euler(0f, -3f, 0f));
            CreateBox("OpenField_SouthWest", root.transform, new Vector3(-124f, 0.01f, -121f), new Vector3(150f, 0.16f, 122f), Materials["ground"], Quaternion.Euler(0f, 7f, 0f));
            CreateBox("OpenField_NorthEast", root.transform, new Vector3(142f, 0.01f, 130f), new Vector3(142f, 0.16f, 126f), Materials["meadow"], Quaternion.Euler(0f, -9f, 0f));

            // Gentle shelves create a readable foreground-to-distance cadence.
            CreateBox("BasinShelf_West", root.transform, new Vector3(-92f, 1.2f, 30f), new Vector3(68f, 2.6f, 142f), Materials["meadow"], Quaternion.Euler(0f, 9f, -1.4f), true);
            CreateBox("BasinShelf_East", root.transform, new Vector3(126f, 1.6f, -58f), new Vector3(82f, 3.4f, 126f), Materials["meadow"], Quaternion.Euler(0f, -12f, 1.7f), true);
            CreateBox("BasinShelf_North", root.transform, new Vector3(12f, 2.1f, 137f), new Vector3(132f, 4.4f, 65f), Materials["meadowLight"], Quaternion.Euler(0f, 4f, -1.1f), true);
        }

        private static void CreateWideLake()
        {
            var root = new GameObject("01_Great_Lake_And_Shore");
            var lake = CreateDisc(
                "GreatLake_70x105m",
                root.transform,
                new Vector3(56f, 0.18f, 27f),
                38f,
                0.08f,
                Materials["water"],
                72,
                0.045f,
                new Vector3(0.92f, 1f, 1.42f));
            lake.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Large, sparse shoreline masses. They frame the lake rather than outlining every metre.
            var shorePieces = new[]
            {
                (new Vector3(23f, 0.32f, 39f), new Vector3(18f, 0.55f, 56f), -8f),
                (new Vector3(88f, 0.3f, 22f), new Vector3(14f, 0.5f, 61f), 7f),
                (new Vector3(55f, 0.28f, 79f), new Vector3(50f, 0.42f, 9f), 2f),
                (new Vector3(61f, 0.29f, -25f), new Vector3(43f, 0.42f, 8f), -4f)
            };
            foreach (var piece in shorePieces)
            {
                CreateBox("Broad_Shore", root.transform, piece.Item1, piece.Item2, Materials["shore"], Quaternion.Euler(0f, piece.Item3, 0f));
            }

            // Only three water accents: enough scale cues without filling the lake with decoration.
            CreateDisc("LakeLightBand_A", root.transform, new Vector3(42f, 0.29f, 43f), 8f, 0.015f, Materials["waterGlow"], 40, 0f, new Vector3(1.8f, 1f, 0.12f));
            CreateDisc("LakeLightBand_B", root.transform, new Vector3(67f, 0.29f, 5f), 6f, 0.015f, Materials["waterGlow"], 40, 0f, new Vector3(1.7f, 1f, 0.10f));
            CreateDisc("LakeIsland", root.transform, new Vector3(69f, 0.34f, 55f), 7.5f, 0.35f, Materials["meadow"], 36, 0.08f);
        }

        private static void CreateWideBridge()
        {
            var root = new GameObject("02_Long_Stone_Bridge");
            const int segmentCount = 13;
            const float segmentLength = 4.8f;
            for (var i = 0; i < segmentCount; i++)
            {
                var t = i / (segmentCount - 1f);
                var x = 26f + i * segmentLength;
                var arch = Mathf.Sin(t * Mathf.PI) * 3.1f;
                CreateBox(
                    "BridgeDeck",
                    root.transform,
                    new Vector3(x, 1.25f + arch, 8f),
                    new Vector3(segmentLength + 0.12f, 0.8f, 5.6f),
                    Materials["bridge"],
                    Quaternion.Euler(0f, 0f, Mathf.Cos(t * Mathf.PI) * -2.3f),
                    true);

                if (i % 3 == 0)
                {
                    var supportHeight = 1.0f + arch;
                    CreateBox("BridgePier", root.transform, new Vector3(x, supportHeight * 0.5f, 8f), new Vector3(1.25f, supportHeight, 4.1f), Materials["rockWarm"], Quaternion.identity);
                }
            }

            // Low rails keep the bridge readable at distance, with gaps preserving the open silhouette.
            for (var i = 0; i < 7; i++)
            {
                var t = i / 6f;
                var x = 26f + t * segmentLength * (segmentCount - 1);
                var arch = Mathf.Sin(t * Mathf.PI) * 3.1f;
                CreateBox("BridgeRail_Left", root.transform, new Vector3(x, 2.1f + arch, 5.35f), new Vector3(7.2f, 0.45f, 0.35f), Materials["bridge"], Quaternion.identity);
                CreateBox("BridgeRail_Right", root.transform, new Vector3(x, 2.1f + arch, 10.65f), new Vector3(7.2f, 0.45f, 0.35f), Materials["bridge"], Quaternion.identity);
            }
        }

        private static void CreateWideLandforms()
        {
            var root = new GameObject("03_Hills_And_Distant_Mountains");

            // Near low hills: broad, traversable, deliberately simple forms.
            CreateBox("LowHill_West_A", root.transform, new Vector3(-82f, 4.2f, 18f), new Vector3(46f, 9f, 64f), Materials["meadow"], Quaternion.Euler(0f, 18f, -5f), true);
            CreateBox("LowHill_West_B", root.transform, new Vector3(-92f, 7.4f, 48f), new Vector3(34f, 9f, 46f), Materials["meadowLight"], Quaternion.Euler(0f, -9f, 9f), true);
            CreateBox("LowHill_North", root.transform, new Vector3(-10f, 5.6f, 101f), new Vector3(82f, 12f, 27f), Materials["mountainNear"], Quaternion.Euler(0f, 3f, -2f), true);
            CreateBox("LowHill_South", root.transform, new Vector3(-44f, 3.8f, -101f), new Vector3(76f, 8f, 27f), Materials["meadow"], Quaternion.Euler(0f, -8f, 3f), true);

            // Distant mountains use large rotated cubes as graphic silhouettes, Sky/Mountain-like in rhythm.
            var mountains = new[]
            {
                (new Vector3(-112f, 30f, 93f), new Vector3(54f, 58f, 44f), new Vector3(0f, 22f, -18f), false),
                (new Vector3(-65f, 23f, 119f), new Vector3(62f, 43f, 35f), new Vector3(0f, -11f, 14f), true),
                (new Vector3(5f, 28f, 128f), new Vector3(70f, 55f, 33f), new Vector3(0f, 9f, -12f), false),
                (new Vector3(86f, 33f, 112f), new Vector3(58f, 66f, 38f), new Vector3(0f, -16f, 16f), true),
                (new Vector3(124f, 24f, 45f), new Vector3(39f, 47f, 68f), new Vector3(0f, 14f, -11f), false),
                (new Vector3(-128f, 22f, -55f), new Vector3(36f, 44f, 76f), new Vector3(0f, -7f, 13f), true)
            };
            foreach (var mountain in mountains)
            {
                CreateBox(
                    "Distant_Mountain_Silhouette",
                    root.transform,
                    mountain.Item1,
                    mountain.Item2,
                    mountain.Item4 ? Materials["mountainFar"] : Materials["mountainNear"],
                    Quaternion.Euler(mountain.Item3));
            }
        }

        private static void CreateGrandTerrainLayers()
        {
            var root = new GameObject("06_Grand_Terrain_Layers");

            // West mesa: three clear elevation bands ending in a 36m high playable plateau.
            CreateBox("WestMesa_Lower", root.transform, new Vector3(-176f, 7f, 52f), new Vector3(100f, 15f, 176f), Materials["mountainNear"], Quaternion.Euler(0f, 7f, -2f), true);
            CreateBox("WestMesa_Middle", root.transform, new Vector3(-184f, 18f, 72f), new Vector3(79f, 24f, 132f), Materials["meadow"], Quaternion.Euler(0f, -3f, 1.5f), true);
            CreateBox("WestMesa_Top", root.transform, new Vector3(-177f, 33f, 91f), new Vector3(66f, 13f, 91f), Materials["meadowLight"], Quaternion.Euler(0f, 5f, -1f), true);
            CreateRamp("WestMesa_LongRamp", root.transform, new Vector3(-99f, 1.5f, -8f), new Vector3(-151f, 35f, 59f), 30f, 3f, Materials["meadowLight"]);

            // East escarpment rises more slowly and reads as a long traversable horizon.
            CreateBox("EastHighland_Lower", root.transform, new Vector3(184f, 8f, -32f), new Vector3(100f, 17f, 201f), Materials["mountainNear"], Quaternion.Euler(0f, -8f, 2f), true);
            CreateBox("EastHighland_Upper", root.transform, new Vector3(198f, 24f, -48f), new Vector3(69f, 24f, 145f), Materials["meadow"], Quaternion.Euler(0f, 4f, -1.5f), true);
            CreateRamp("EastHighland_LongRamp", root.transform, new Vector3(105f, 1.8f, -87f), new Vector3(169f, 27f, -71f), 38f, 3f, Materials["meadow"]);

            // Northern stair-ridge leads the eye upward in three large graphic steps.
            CreateBox("NorthRidge_Step01", root.transform, new Vector3(-42f, 10f, 186f), new Vector3(132f, 20f, 45f), Materials["mountainNear"], Quaternion.Euler(0f, 5f, -2f), true);
            CreateBox("NorthRidge_Step02", root.transform, new Vector3(-55f, 24f, 211f), new Vector3(112f, 20f, 37f), Materials["mountainFar"], Quaternion.Euler(0f, -5f, 3f), true);
            CreateBox("NorthRidge_Step03", root.transform, new Vector3(-70f, 40f, 232f), new Vector3(88f, 25f, 31f), Materials["mountainNear"], Quaternion.Euler(0f, 7f, -4f), true);
            CreateRamp("NorthRidge_Saddle", root.transform, new Vector3(13f, 3f, 151f), new Vector3(-29f, 35f, 219f), 34f, 3.5f, Materials["meadowLight"]);

            // Southern canyon is made from separated walls, preserving a broad, readable gap.
            CreateBox("SouthCanyon_WallWest", root.transform, new Vector3(-84f, 16f, -191f), new Vector3(124f, 33f, 67f), Materials["mountainNear"], Quaternion.Euler(0f, -6f, 3f), true);
            CreateBox("SouthCanyon_WallEast", root.transform, new Vector3(79f, 20f, -199f), new Vector3(118f, 41f, 70f), Materials["mountainFar"], Quaternion.Euler(0f, 8f, -3f), true);
            CreateBox("SouthCanyon_BackRidge", root.transform, new Vector3(2f, 44f, -244f), new Vector3(245f, 58f, 35f), Materials["mountainNear"], Quaternion.Euler(0f, 2f, -2f));

            // Outer skyline: fewer, larger masses with 65-110m peaks.
            var skyline = new[]
            {
                (new Vector3(-250f, 65f, 151f), new Vector3(90f, 130f, 102f), new Vector3(0f, 15f, -17f), false),
                (new Vector3(-170f, 84f, 259f), new Vector3(126f, 168f, 73f), new Vector3(0f, -8f, 13f), true),
                (new Vector3(34f, 70f, 274f), new Vector3(155f, 140f, 69f), new Vector3(0f, 7f, -11f), false),
                (new Vector3(207f, 91f, 234f), new Vector3(112f, 182f, 80f), new Vector3(0f, -12f, 15f), true),
                (new Vector3(270f, 73f, 57f), new Vector3(78f, 146f, 134f), new Vector3(0f, 11f, -14f), false),
                (new Vector3(-275f, 77f, -88f), new Vector3(82f, 154f, 151f), new Vector3(0f, -9f, 12f), true)
            };
            foreach (var mass in skyline)
            {
                CreateBox("Grand_Skyline_Mass", root.transform, mass.Item1, mass.Item2, mass.Item4 ? Materials["mountainFar"] : Materials["mountainNear"], Quaternion.Euler(mass.Item3));
            }
        }

        private static void CreateGrandHighlandLake()
        {
            var root = new GameObject("07_West_Highland_Lake");
            var lake = CreateDisc("HighlandLake", root.transform, new Vector3(-176f, 40.1f, 94f), 23f, 0.08f, Materials["water"], 56, 0.055f, new Vector3(1.15f, 1f, 0.72f));
            lake.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            CreateDisc("HighlandLake_Island", root.transform, new Vector3(-167f, 40.25f, 96f), 4.3f, 0.28f, Materials["meadowLight"], 28, 0.08f);
            CreateBox("HighlandLake_Shore", root.transform, new Vector3(-176f, 39.7f, 94f), new Vector3(60f, 0.45f, 43f), Materials["shore"], Quaternion.Euler(0f, 5f, 0f));
            lake.transform.position += Vector3.up * 0.2f;
        }

        private static void CreateGrandCanyonBridge()
        {
            var root = new GameObject("08_South_Canyon_Crossing");
            const int segmentCount = 11;
            for (var i = 0; i < segmentCount; i++)
            {
                var t = i / (segmentCount - 1f);
                var x = -54f + i * 10.8f;
                var arch = Mathf.Sin(t * Mathf.PI) * 5.2f;
                CreateBox("CanyonBridge_Deck", root.transform, new Vector3(x, 32f + arch, -180f), new Vector3(11f, 1.1f, 7.5f), Materials["bridge"], Quaternion.Euler(0f, 0f, Mathf.Cos(t * Mathf.PI) * -3f), true);
            }

            CreateRamp("CanyonBridge_WestApproach", root.transform, new Vector3(-111f, 18f, -178f), new Vector3(-59f, 32f, -180f), 19f, 2.2f, Materials["meadow"]);
            CreateRamp("CanyonBridge_EastApproach", root.transform, new Vector3(59f, 32f, -180f), new Vector3(124f, 21f, -179f), 19f, 2.2f, Materials["meadow"]);
        }

        private static void CreateWideSparseVegetation()
        {
            var root = new GameObject("04_Sparse_Vegetation_Zones");

            // Trees appear in small landmark clusters; the open fields remain genuinely open.
            var treeClusters = new[]
            {
                new Vector3(-55f, 0.3f, 65f), new Vector3(-62f, 0.3f, 72f), new Vector3(-48f, 0.3f, 75f),
                new Vector3(12f, 0.3f, 91f), new Vector3(22f, 0.3f, 88f),
                new Vector3(96f, 0.3f, 68f), new Vector3(103f, 0.3f, 58f), new Vector3(98f, 0.3f, 79f),
                new Vector3(-95f, 0.3f, -55f), new Vector3(-84f, 0.3f, -62f),
                new Vector3(78f, 0.3f, -83f), new Vector3(89f, 0.3f, -76f)
            };
            for (var i = 0; i < treeClusters.Length; i++)
            {
                var asset = i % 3 == 0 ? "Pine_3.fbx" : i % 2 == 0 ? "CommonTree_4.fbx" : "TwistedTree_3.fbx";
                var tree = InstantiateModel(NatureRoot + asset, root.transform, treeClusters[i], Vector3.one * (2.0f + (i % 3) * 0.28f), Quaternion.Euler(0f, i * 41f, 0f), "Sparse_Landmark_Tree");
                TintImportedMaterials(tree, i % 4 == 0 ? new Color(0.78f, 0.68f, 0.38f) : new Color(0.62f, 0.86f, 0.48f));
            }

            var shorelineRocks = new[]
            {
                new Vector3(25f, 0.35f, -2f), new Vector3(21f, 0.35f, 21f), new Vector3(29f, 0.35f, 64f),
                new Vector3(84f, 0.35f, -10f), new Vector3(91f, 0.35f, 43f), new Vector3(82f, 0.35f, 72f)
            };
            for (var i = 0; i < shorelineRocks.Length; i++)
            {
                InstantiateModel(NatureRoot + (i % 2 == 0 ? "Rock_Medium_3.fbx" : "Rock_Medium_1.fbx"), root.transform, shorelineRocks[i], Vector3.one * (1.8f + i % 3 * 0.35f), Quaternion.Euler(0f, i * 53f, 0f), "Sparse_Shore_Rock");
            }

            // Three tiny flower/grass pockets are visual rewards, not wallpaper.
            CreateWideMeadowPocket(root.transform, new Vector3(-34f, 0.35f, -26f), 0);
            CreateWideMeadowPocket(root.transform, new Vector3(7f, 0.35f, 52f), 1);
            CreateWideMeadowPocket(root.transform, new Vector3(91f, 0.35f, -48f), 2);
        }

        private static void CreateGrandSparseLandmarks()
        {
            var root = new GameObject("09_Grand_Sparse_Landmarks");

            // A handful of oversized tree groups communicate distance and plateau scale.
            var landmarks = new[]
            {
                new Vector3(-160f, 40.2f, 67f), new Vector3(-194f, 40.2f, 110f), new Vector3(-157f, 40.2f, 122f),
                new Vector3(181f, 36.4f, -18f), new Vector3(205f, 36.4f, -67f), new Vector3(174f, 36.4f, -91f),
                new Vector3(-23f, 51.8f, 224f), new Vector3(-70f, 52.0f, 225f),
                new Vector3(-119f, 33.1f, -172f), new Vector3(126f, 41.1f, -182f)
            };
            for (var i = 0; i < landmarks.Length; i++)
            {
                var asset = i % 3 == 0 ? "Pine_5.fbx" : i % 2 == 0 ? "CommonTree_5.fbx" : "TwistedTree_4.fbx";
                var tree = InstantiateModel(NatureRoot + asset, root.transform, landmarks[i], Vector3.one * (2.8f + (i % 3) * 0.35f), Quaternion.Euler(0f, i * 47f, 0f), "Grand_Landmark_Tree");
                TintImportedMaterials(tree, i % 4 == 0 ? new Color(0.80f, 0.66f, 0.38f) : new Color(0.62f, 0.84f, 0.47f));
            }

            var monoliths = new[]
            {
                (new Vector3(-130f, 9f, 151f), new Vector3(7f, 18f, 12f), -8f),
                (new Vector3(151f, 12f, 103f), new Vector3(9f, 24f, 13f), 12f),
                (new Vector3(-17f, 13f, -126f), new Vector3(8f, 26f, 10f), -4f)
            };
            foreach (var monolith in monoliths)
            {
                CreateBox("Quiet_Stone_Monolith", root.transform, monolith.Item1, monolith.Item2, Materials["rockWarm"], Quaternion.Euler(0f, monolith.Item3, -3f));
            }

            CreateWideMeadowPocket(root.transform, new Vector3(-137f, 0.35f, -38f), 3);
            CreateWideMeadowPocket(root.transform, new Vector3(126f, 0.35f, 64f), 4);
            CreateWideMeadowPocket(root.transform, new Vector3(-176f, 40.3f, 69f), 5);
        }

        private static void CreateWideMeadowPocket(Transform parent, Vector3 center, int variant)
        {
            for (var i = 0; i < 7; i++)
            {
                var angle = i * 2.39996f + variant;
                var radius = 1.5f + (i % 3) * 1.2f;
                var position = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                InstantiateModel(NatureRoot + (i % 2 == 0 ? "Grass_Wispy_Tall.fbx" : "Grass_Common_Tall.fbx"), parent, position, Vector3.one * 0.9f, Quaternion.Euler(0f, i * 47f, 0f), "Meadow_Pocket_Grass");
                if (i == 1 || i == 5)
                {
                    InstantiateModel(NatureRoot + (variant % 2 == 0 ? "Flower_3_Group.fbx" : "Flower_4_Group.fbx"), parent, position + new Vector3(0.6f, 0f, 0.4f), Vector3.one * 0.85f, Quaternion.identity, "Meadow_Pocket_Flowers");
                }
            }
        }

        private static void CreateWideSkyDetails()
        {
            var root = new GameObject("05_Wide_Sky_Details");
            var moon = CreateSphere("Moon", root.transform, new Vector3(-95f, 92f, 160f), Vector3.one * 10f, Materials["moon"]);
            moon.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            UnityEngine.Random.InitState(8421);
            for (var i = 0; i < 45; i++)
            {
                var position = new Vector3(UnityEngine.Random.Range(-155f, 155f), UnityEngine.Random.Range(72f, 155f), 175f + UnityEngine.Random.Range(-3f, 3f));
                var star = CreateSphere("Star", root.transform, position, Vector3.one * UnityEngine.Random.Range(0.18f, 0.48f), Materials["star"]);
                star.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static void CreateGrandSkyDetails()
        {
            var root = new GameObject("10_Grand_Sky_Details");
            var moon = CreateSphere("GrandMoon", root.transform, new Vector3(-245f, 205f, 355f), Vector3.one * 22f, Materials["moon"]);
            moon.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            UnityEngine.Random.InitState(9047);
            for (var i = 0; i < 55; i++)
            {
                var position = new Vector3(UnityEngine.Random.Range(-360f, 360f), UnityEngine.Random.Range(175f, 350f), 390f + UnityEngine.Random.Range(-6f, 6f));
                var star = CreateSphere("GrandStar", root.transform, position, Vector3.one * UnityEngine.Random.Range(0.35f, 0.95f), Materials["star"]);
                star.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static void CreateWideCamera()
        {
            var cameraObject = new GameObject("Wide Valley Camera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.fieldOfView = 48f;
            camera.nearClipPlane = 0.2f;
            camera.farClipPlane = 650f;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.transform.position = new Vector3(-108f, 66f, -126f);
            LookAt(camera.transform, new Vector3(19f, 4f, 25f));
            camera.backgroundColor = new Color(0.025f, 0.045f, 0.075f);
        }

        private static void CreateGrandCamera()
        {
            var cameraObject = new GameObject("Grand Valley Camera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1400f;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.transform.position = new Vector3(-278f, 176f, -326f);
            LookAt(camera.transform, new Vector3(12f, 18f, 24f));
            camera.backgroundColor = new Color(0.028f, 0.048f, 0.078f);
        }

        private static void CreateGroundComposition()
        {
            var root = new GameObject("Ground_Composition");
            CreateDisc("Island", root.transform, Vector3.zero, 12f, 0.1f, Materials["ground"], 64, 0.15f);
            CreateDisc("CentralClearing", root.transform, new Vector3(0f, 0.22f, 0.3f), 5.9f, 0.08f, Materials["ground"], 48, 0.03f);

            for (var i = 0; i < 28; i++)
            {
                var angle = i / 28f * Mathf.PI * 2f;
                var radius = 10.4f + Mathf.Sin(i * 2.13f) * 0.55f;
                var position = new Vector3(Mathf.Cos(angle) * radius, 0.28f, Mathf.Sin(angle) * radius);
                var rock = CreateRock("IslandEdgeRock", root.transform, position, 0.45f + (i % 3) * 0.12f, Materials[i % 4 == 0 ? "rockWarm" : "groundEdge"]);
                rock.transform.rotation = Quaternion.Euler(0f, i * 27f, i % 2 == 0 ? 7f : -5f);
            }

            for (var i = 0; i < 10; i++)
            {
                var angle = i / 10f * Mathf.PI * 2f + 0.2f;
                var position = new Vector3(Mathf.Cos(angle) * 7.0f, 0.34f, Mathf.Sin(angle) * 7.0f);
                CreateRock("ClearingBoundary", root.transform, position, 0.32f + (i % 2) * 0.1f, Materials["rock"]);
            }
        }

        private static GameObject CreateCampfire(bool includeGameplaySource = false)
        {
            var root = new GameObject("Hero_Campfire");
            root.transform.position = new Vector3(-0.5f, 0.38f, 0.8f);

            InstantiateModel(SurvivalRoot + "campfire-pit.fbx", root.transform, Vector3.zero, Vector3.one * 1.45f, Quaternion.identity, "Campfire_Pit_Art");

            for (var i = 0; i < 9; i++)
            {
                var angle = i / 9f * Mathf.PI * 2f;
                var rock = CreateRock("FirepitStone", root.transform, new Vector3(Mathf.Cos(angle) * 1.15f, 0.28f, Mathf.Sin(angle) * 1.15f), 0.34f, Materials["rockWarm"]);
                rock.transform.rotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 8f * Mathf.Sin(angle));
            }

            CreateCylinder("LogA", root.transform, new Vector3(0f, 0.63f, 0f), new Vector3(0.28f, 1.22f, 0.28f), Materials["wood"], Quaternion.Euler(0f, 0f, 90f));
            CreateCylinder("LogB", root.transform, new Vector3(0f, 0.73f, 0f), new Vector3(0.28f, 1.22f, 0.28f), Materials["trunkLight"], Quaternion.Euler(0f, 55f, 90f));
            for (var i = 0; i < 7; i++)
            {
                var angle = i * 0.9f;
                CreateSphere("Ember", root.transform, new Vector3(Mathf.Cos(angle) * 0.82f, 0.67f, Mathf.Sin(angle) * 0.82f), Vector3.one * 0.08f, Materials["ember"]);
            }

            CreateCone("OuterFlame", root.transform, new Vector3(0f, 1.45f, 0f), 0.65f, 1.65f, Materials["flameOuter"], 9);
            CreateCone("InnerFlame", root.transform, new Vector3(0f, 1.43f, -0.03f), 0.36f, 1.15f, Materials["flameInner"], 8);
            CreateCone("SideFlame", root.transform, new Vector3(0.34f, 1.15f, 0.08f), 0.26f, 0.82f, Materials["flameInner"], 7);

            var fireLightObject = new GameObject("CampfireLight");
            fireLightObject.transform.SetParent(root.transform);
            fireLightObject.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            var fireLight = fireLightObject.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1.0f, 0.24f, 0.055f);
            fireLight.intensity = 5.2f;
            fireLight.range = 8.5f;
            fireLight.shadows = LightShadows.Soft;

            var warmFillObject = new GameObject("CampfireWarmFill");
            warmFillObject.transform.SetParent(root.transform);
            warmFillObject.transform.localPosition = new Vector3(0f, 2.1f, 0f);
            var warmFill = warmFillObject.AddComponent<Light>();
            warmFill.type = LightType.Point;
            warmFill.color = new Color(1.0f, 0.52f, 0.16f);
            warmFill.intensity = 1.4f;
            warmFill.range = 5f;
            warmFill.shadows = LightShadows.None;

            if (includeGameplaySource)
            {
                var stableId = root.AddComponent<StableSceneId>();
                var serializedId = new SerializedObject(stableId);
                serializedId.FindProperty("_value").stringValue = "snow-lookdev-hero-campfire";
                serializedId.ApplyModifiedPropertiesWithoutUndo();

                var campfire = root.AddComponent<Campfire>();
                var serializedCampfire = new SerializedObject(campfire);
                serializedCampfire.FindProperty("_config").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Config/CampfireConfig_Prototype.asset");
                serializedCampfire.FindProperty("_level").intValue = 2;
                serializedCampfire.ApplyModifiedPropertiesWithoutUndo();
            }

            return root;
        }

        private static void CreateSnowEnvironmentWarmthSession()
        {
            var directorRoot = new GameObject("05_Environment_Warmth_Session");
            var directorType = Type.GetType("DemonViglu.FirePlay.Rendering.EnvironmentWarmthDirector, Assembly-CSharp");
            var snowReceiverType = Type.GetType("DemonViglu.FirePlay.Rendering.WarmthSnowReceiver, Assembly-CSharp");
            var growthReceiverType = Type.GetType("DemonViglu.FirePlay.Rendering.WarmthGrowthReceiver, Assembly-CSharp");
            var atmosphereReceiverType = Type.GetType("DemonViglu.FirePlay.Rendering.WarmthAtmosphereReceiver, Assembly-CSharp");
            var iceReceiverType = Type.GetType("DemonViglu.FirePlay.Rendering.WarmthIceReceiver, Assembly-CSharp");
            if (directorType == null || snowReceiverType == null || growthReceiverType == null || atmosphereReceiverType == null || iceReceiverType == null)
            {
                throw new InvalidOperationException("Environment warmth presentation scripts are not compiled yet. Wait for Unity script refresh and run the builder again.");
            }

            directorRoot.AddComponent(directorType);

            var groundRoot = new GameObject("Warmth_Snow_Surface_Receivers");
            groundRoot.transform.SetParent(directorRoot.transform);
            var groundRenderers = new List<Renderer>();
            var groundContainer = GameObject.Find("00_Snow_Grand_Ground");
            if (groundContainer != null)
            {
                foreach (var targetRenderer in groundContainer.GetComponentsInChildren<Renderer>(true))
                {
                    var coldColor = targetRenderer.sharedMaterial != null && targetRenderer.sharedMaterial.HasProperty("_BaseColor")
                        ? targetRenderer.sharedMaterial.GetColor("_BaseColor")
                        : new Color(0.91f, 0.95f, 0.98f);
                    targetRenderer.sharedMaterial = Materials["warmthSnow"];
                    var colorProperties = new MaterialPropertyBlock();
                    targetRenderer.GetPropertyBlock(colorProperties);
                    colorProperties.SetColor("_BaseColor", coldColor);
                    colorProperties.SetColor("_WarmColor", Color.Lerp(new Color(0.22f, 0.19f, 0.12f), coldColor, 0.08f));
                    targetRenderer.SetPropertyBlock(colorProperties);
                    groundRenderers.Add(targetRenderer);
                }
            }

            var snowReceiver = groundRoot.AddComponent(snowReceiverType);
            var serializedSnow = new SerializedObject(snowReceiver);
            SetObjectArray(serializedSnow.FindProperty("_targetRenderers"), groundRenderers.ToArray());
            serializedSnow.ApplyModifiedPropertiesWithoutUndo();

            var growthRoot = new GameObject("Authored_Thaw_Growth_Pockets");
            growthRoot.transform.SetParent(directorRoot.transform);
            var growthPlacements = new[]
            {
                (new Vector3(-4.3f, 0.38f, -0.8f), "Grass_Common_Short.fbx", 0.72f, 16f),
                (new Vector3(2.7f, 0.38f, -0.4f), "Grass_Wispy_Short.fbx", 0.66f, 84f),
                (new Vector3(-3.0f, 0.38f, 4.2f), "Fern_1.fbx", 0.62f, 141f),
                (new Vector3(3.8f, 0.38f, 3.5f), "Grass_Common_Tall.fbx", 0.64f, 212f),
                (new Vector3(-6.7f, 0.38f, 2.6f), "Flower_3_Group.fbx", 0.58f, 271f),
                (new Vector3(5.8f, 0.38f, 0.9f), "Flower_4_Group.fbx", 0.56f, 322f),
                (new Vector3(8.8f, 0.38f, 10.8f), "Grass_Wispy_Short.fbx", 0.68f, 28f),
                (new Vector3(13.2f, 0.38f, 12.8f), "Flower_3_Group.fbx", 0.55f, 109f),
                (new Vector3(18.0f, 0.38f, 8.8f), "Grass_Common_Short.fbx", 0.70f, 188f),
                (new Vector3(22.5f, 0.38f, 14.5f), "Flower_4_Group.fbx", 0.54f, 257f),
                (new Vector3(31.0f, 0.38f, -3.0f), "Grass_Common_Tall.fbx", 0.66f, 318f),
                (new Vector3(36.0f, 0.38f, 2.5f), "Flower_3_Group.fbx", 0.56f, 61f)
            };

            for (var growthIndex = 0; growthIndex < growthPlacements.Length; growthIndex++)
            {
                var placement = growthPlacements[growthIndex];
                var receiverName = placement.Item2.Contains("Flower") ? "ThawFlower_Receiver" : "ThawGrass_Receiver";
                var growth = InstantiateModel(
                    NatureRoot + placement.Item2,
                    growthRoot.transform,
                    placement.Item1,
                    Vector3.one * placement.Item3,
                    Quaternion.Euler(0f, placement.Item4, 0f),
                    receiverName + "_Visual");
                if (growth == null)
                {
                    continue;
                }

                var growthHost = CreateWarmthGrowthAnimator(growth, receiverName, growthIndex, out var growthAnimator);
                var growthReceiver = growthHost.AddComponent(growthReceiverType);
                var serializedGrowth = new SerializedObject(growthReceiver);
                serializedGrowth.FindProperty("_activationThreshold").floatValue = placement.Item2.Contains("Flower") ? 0.22f : 0.13f;
                serializedGrowth.FindProperty("_growthSpeed").floatValue = placement.Item2.Contains("Flower") ? 0.48f : 0.72f;
                serializedGrowth.FindProperty("_animator").objectReferenceValue = growthAnimator;
                serializedGrowth.FindProperty("_useAnimator").boolValue = growthAnimator != null;
                serializedGrowth.ApplyModifiedPropertiesWithoutUndo();
            }

            var atmosphereRoot = new GameObject("Warmth_Atmosphere_Receiver");
            atmosphereRoot.transform.SetParent(directorRoot.transform);
            const int atmosphereSourceCount = 4;
            var atmosphereTransforms = new Transform[atmosphereSourceCount];
            var warmthLights = new Light[atmosphereSourceCount];
            var thawMists = new ParticleSystem[atmosphereSourceCount];
            var warmthAudio = new AudioSource[atmosphereSourceCount];
            var campfireAudio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Sound/soundsforyou-campfire-crackling-fireplace-sound-119594.mp3");
            for (var index = 0; index < atmosphereSourceCount; index++)
            {
                var sourceRoot = new GameObject($"Warmth_Source_Atmosphere_{index + 1:00}");
                sourceRoot.transform.SetParent(atmosphereRoot.transform);
                atmosphereTransforms[index] = sourceRoot.transform;

                var warmthLight = sourceRoot.AddComponent<Light>();
                warmthLight.type = LightType.Point;
                warmthLight.color = new Color(1f, 0.53f, 0.18f);
                warmthLight.shadows = LightShadows.None;
                warmthLight.intensity = 0f;
                warmthLights[index] = warmthLight;

                var mist = sourceRoot.AddComponent<ParticleSystem>();
                var mistMain = mist.main;
                mistMain.loop = true;
                mistMain.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.8f);
                mistMain.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.42f);
                mistMain.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.42f);
                mistMain.startColor = new ParticleSystem.MinMaxGradient(new Color(0.72f, 0.86f, 0.82f, 0.05f), new Color(0.88f, 0.94f, 0.90f, 0.32f));
                mistMain.maxParticles = 32;
                var mistEmission = mist.emission;
                mistEmission.rateOverTime = 0f;
                var mistShape = mist.shape;
                mistShape.shapeType = ParticleSystemShapeType.Circle;
                mistShape.radius = 3.6f;
                var mistRenderer = sourceRoot.GetComponent<ParticleSystemRenderer>();
                mistRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                mistRenderer.sharedMaterial = Materials["thawMist"];
                thawMists[index] = mist;

                var audio = sourceRoot.AddComponent<AudioSource>();
                audio.clip = campfireAudio;
                audio.loop = true;
                audio.playOnAwake = false;
                audio.spatialBlend = 1f;
                audio.minDistance = 3f;
                audio.maxDistance = 28f;
                audio.volume = 0f;
                warmthAudio[index] = audio;
            }

            var atmosphereReceiver = atmosphereRoot.AddComponent(atmosphereReceiverType);
            var serializedAtmosphere = new SerializedObject(atmosphereReceiver);
            SetObjectArray(serializedAtmosphere.FindProperty("_sourceRoots"), atmosphereTransforms);
            SetObjectArray(serializedAtmosphere.FindProperty("_lights"), warmthLights);
            SetObjectArray(serializedAtmosphere.FindProperty("_thawMists"), thawMists);
            SetObjectArray(serializedAtmosphere.FindProperty("_audioSources"), warmthAudio);
            serializedAtmosphere.ApplyModifiedPropertiesWithoutUndo();

            var iceRoot = new GameObject("Warmth_Ice_Receiver");
            iceRoot.transform.SetParent(directorRoot.transform);
            var iceReceiver = iceRoot.AddComponent(iceReceiverType);
            var serializedIce = new SerializedObject(iceReceiver);
            SetObjectArray(serializedIce.FindProperty("_iceFields"), UnityEngine.Object.FindObjectsByType<IcePathCrackField>(FindObjectsInactive.Include));
            serializedIce.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(SerializedProperty property, UnityEngine.Object[] values)
        {
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private static GameObject CreateWarmthGrowthAnimator(GameObject growth, string receiverName, int index, out Animator animator)
        {
            animator = null;
            if (growth == null)
            {
                return null;
            }

            // Model-import roots are treated as visual assets. Keep the Animator and
            // Receiver on an ordinary scene object so adding components never depends
            // on the imported FBX root's component restrictions.
            var parent = growth.transform.parent;
            var host = new GameObject(receiverName);
            host.transform.SetParent(parent, false);
            host.transform.localPosition = growth.transform.localPosition;
            host.transform.localRotation = growth.transform.localRotation;
            host.transform.localScale = growth.transform.localScale;
            growth.transform.SetParent(host.transform, false);
            growth.transform.localPosition = Vector3.zero;
            growth.transform.localRotation = Quaternion.identity;
            growth.transform.localScale = Vector3.one;

            var clipPath = $"{AnimationPath}/WarmthGrowth_{index + 1:00}.anim";
            var controllerPath = $"{AnimationPath}/WarmthGrowth_{index + 1:00}.controller";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = $"WarmthGrowth_{index + 1:00}" };
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            clip.ClearCurves();
            clip.frameRate = 60f;
            clip.wrapMode = WrapMode.ClampForever;
            var authoredScale = host.transform.localScale;
            var authoredPosition = host.transform.localPosition;
            var scaleBindings = new[]
            {
                EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalScale.x"),
                EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalScale.y"),
                EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalScale.z")
            };
            var scaleValues = new[] { authoredScale.x, authoredScale.y, authoredScale.z };
            for (var axis = 0; axis < scaleBindings.Length; axis++)
            {
                AnimationUtility.SetEditorCurve(
                    clip,
                    scaleBindings[axis],
                    AnimationCurve.Linear(0f, scaleValues[axis] * 0.04f, 1f, scaleValues[axis]));
            }

            var positionBindings = new[]
            {
                EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalPosition.x"),
                EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalPosition.y"),
                EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalPosition.z")
            };
            var positionValues = new[] { authoredPosition.x, authoredPosition.y, authoredPosition.z };
            for (var axis = 0; axis < positionBindings.Length; axis++)
            {
                var start = positionValues[axis];
                if (axis == 1)
                {
                    start -= 0.12f;
                }

                AnimationUtility.SetEditorCurve(
                    clip,
                    positionBindings[axis],
                    AnimationCurve.Linear(0f, start, 1f, positionValues[axis]));
            }

            EditorUtility.SetDirty(clip);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            }

            var stateMachine = controller.layers[0].stateMachine;
            foreach (var childState in stateMachine.states)
            {
                stateMachine.RemoveState(childState.state);
            }

            var state = stateMachine.AddState("WarmthGrowth");
            state.motion = clip;
            stateMachine.defaultState = state;
            EditorUtility.SetDirty(controller);

            // UnityEngine.Object has overloaded null semantics; do not use C# `??` here.
            // An imported FBX root can return a managed Animator wrapper that is already
            // destroyed/invalid, which would otherwise fail on the next property access.
            animator = host.GetComponent<Animator>();
            if (animator == null)
            {
                animator = host.AddComponent<Animator>();
            }

            if (animator == null)
            {
                Debug.LogWarning($"[FirePlayLookDevBuilder] Could not attach Animator to {host.name}; using procedural warmth growth.");
                return host;
            }

            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            return host;
        }

        private static void CreatePath()
        {
            var root = new GameObject("Path_To_Campfire");
            for (var i = 0; i < 11; i++)
            {
                var t = i / 10f;
                var z = -10.2f + t * 10.3f;
                var x = Mathf.Sin(t * Mathf.PI * 0.9f) * 1.1f - 0.2f;
                var stone = InstantiateModel(
                    NatureRoot + (i % 3 == 0 ? "RockPath_Round_Wide.fbx" : i % 2 == 0 ? "RockPath_Round_Thin.fbx" : "RockPath_Round_Small_2.fbx"),
                    root.transform,
                    new Vector3(x, 0.40f + Mathf.Sin(i * 1.7f) * 0.025f, z),
                    Vector3.one * (0.52f + (i % 3) * 0.06f),
                    Quaternion.Euler(0f, -12f + Mathf.Sin(i) * 10f, 0f),
                    "PathStone_Art");
                TintImportedMaterials(stone, i % 4 == 0 ? new Color(0.88f, 0.72f, 0.48f) : new Color(0.72f, 0.76f, 0.70f));
            }
        }

        private static void CreatePond()
        {
            var root = new GameObject("Moonlit_Pond");
            CreateDisc("PondSurface", root.transform, new Vector3(5.3f, 0.34f, 3.55f), 3.2f, 0.06f, Materials["water"], 48, 0.12f, new Vector3(1.25f, 1f, 0.68f));

            var fishingStand = InstantiateModel(
                SurvivalRoot + "campfire-fishing-stand.fbx",
                root.transform,
                new Vector3(6.45f, 0.38f, 3.95f),
                Vector3.one * 1.05f,
                Quaternion.Euler(0f, -18f, 0f),
                "Fishing_Stand_Art");
            TintImportedMaterials(fishingStand, new Color(0.92f, 0.70f, 0.42f));

            for (var i = 0; i < 6; i++)
            {
                var angle = i / 6f * Mathf.PI * 2f;
                var pos = new Vector3(5.3f + Mathf.Cos(angle) * 2.55f, 0.39f, 3.55f + Mathf.Sin(angle) * 1.55f);
                CreateRock("PondEdge", root.transform, pos, 0.30f + i % 2 * 0.12f, Materials["rock"]);
            }

            for (var i = 0; i < 3; i++)
            {
                var ripple = CreateDisc("PondRipple", root.transform, new Vector3(4.5f + i * 0.7f, 0.42f, 3.45f + (i % 2) * 0.48f), 0.65f - i * 0.12f, 0.015f, Materials["waterGlow"], 32, 0f, new Vector3(1.3f, 1f, 0.25f));
                ripple.transform.localScale = new Vector3(1f, 1f, 1f);
            }
        }

        private static void CreateVegetation()
        {
            var root = new GameObject("Vegetation_Composition");

            var trees = new[]
            {
                new Vector3(-7.5f, 0.3f, 5.8f), new Vector3(-4.8f, 0.3f, 8.3f), new Vector3(1.8f, 0.3f, 9.0f),
                new Vector3(7.9f, 0.3f, 7.0f), new Vector3(9.0f, 0.3f, 2.0f), new Vector3(-8.8f, 0.3f, -1.2f),
                new Vector3(6.4f, 0.3f, 8.3f), new Vector3(-2.7f, 0.3f, 10.0f)
            };
            for (var i = 0; i < trees.Length; i++)
            {
                var asset = i % 4 == 0 ? "Pine_2.fbx" : i % 3 == 0 ? "CommonTree_3.fbx" : i % 2 == 0 ? "CommonTree_2.fbx" : "TwistedTree_2.fbx";
                var tree = InstantiateModel(NatureRoot + asset, root.transform, trees[i], Vector3.one * (1.35f + (i % 3) * 0.18f), Quaternion.Euler(0f, i * 37f, 0f), "Tree_Art");
                TintImportedMaterials(tree, i % 5 == 0 ? new Color(0.82f, 0.56f, 0.32f) : new Color(0.78f, 0.95f, 0.52f));
            }

            var shrubs = new[]
            {
                new Vector3(-5.7f, 0.35f, 2.7f), new Vector3(-3.4f, 0.35f, 5.2f), new Vector3(3.4f, 0.35f, 6.0f),
                new Vector3(7.0f, 0.35f, 0.1f), new Vector3(-7.0f, 0.35f, -5.0f), new Vector3(5.8f, 0.35f, 6.0f)
            };
            for (var i = 0; i < shrubs.Length; i++)
            {
                var shrub = InstantiateModel(NatureRoot + (i % 3 == 0 ? "Bush_Common_Flowers.fbx" : "Bush_Common.fbx"), root.transform, shrubs[i], Vector3.one * (0.9f + i % 3 * 0.12f), Quaternion.Euler(0f, i * 51f, 0f), "Shrub_Art");
                TintImportedMaterials(shrub, i % 2 == 0 ? new Color(0.52f, 0.85f, 0.32f) : new Color(0.30f, 0.62f, 0.30f));
            }

            for (var i = 0; i < 38; i++)
            {
                var angle = i * 2.39996f;
                var radius = 4.4f + (i % 7) * 0.38f;
                var pos = new Vector3(Mathf.Cos(angle) * radius - 0.2f, 0.37f, Mathf.Sin(angle) * radius + 0.5f);
                if (Vector3.Distance(pos, new Vector3(-0.5f, 0f, 0.8f)) < 2.0f || Vector3.Distance(pos, new Vector3(5.3f, 0f, 3.55f)) < 2.2f)
                {
                    continue;
                }

                var grassAsset = i % 4 == 0 ? "Grass_Wispy_Tall.fbx" : i % 3 == 0 ? "Grass_Common_Tall.fbx" : "Grass_Common_Short.fbx";
                var grass = InstantiateModel(NatureRoot + grassAsset, root.transform, pos, Vector3.one * (0.62f + (i % 3) * 0.08f), Quaternion.Euler(0f, i * 43f, 0f), "Grass_Art");
                TintImportedMaterials(grass, i % 5 == 0 ? new Color(0.95f, 0.68f, 0.22f) : new Color(0.62f, 0.95f, 0.32f));
                if (i % 5 == 0)
                {
                    var flower = InstantiateModel(NatureRoot + (i % 2 == 0 ? "Flower_3_Group.fbx" : "Flower_4_Group.fbx"), root.transform, pos + new Vector3(0.18f, 0f, 0.16f), Vector3.one * 0.7f, Quaternion.Euler(0f, i * 19f, 0f), "Flower_Art");
                    TintImportedMaterials(flower, i % 2 == 0 ? new Color(0.56f, 0.78f, 1.0f) : new Color(1.0f, 0.52f, 0.18f));
                }
                if (i % 7 == 0)
                {
                    InstantiateModel(NatureRoot + "Fern_1.fbx", root.transform, pos + new Vector3(-0.24f, 0f, 0.12f), Vector3.one * 0.7f, Quaternion.Euler(0f, i * 29f, 0f), "Fern_Art");
                }
            }

            var featureRocks = new[] { new Vector3(-6.5f, 0.35f, 4.0f), new Vector3(4.0f, 0.35f, 7.0f), new Vector3(7.4f, 0.35f, 4.4f), new Vector3(-5.7f, 0.35f, -3.5f) };
            for (var i = 0; i < featureRocks.Length; i++)
            {
                var rock = InstantiateModel(NatureRoot + (i % 2 == 0 ? "Rock_Medium_2.fbx" : "Rock_Medium_1.fbx"), root.transform, featureRocks[i], Vector3.one * (0.9f + i * 0.08f), Quaternion.Euler(0f, i * 33f, 0f), "FeatureRock_Art");
                TintImportedMaterials(rock, new Color(0.62f, 0.72f, 0.66f));
            }
        }

        private static GameObject InstantiateModel(string assetPath, Transform parent, Vector3 position, Vector3 scale, Quaternion rotation, string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (asset == null)
            {
                Debug.LogWarning($"[FirePlayLookDevBuilder] Missing art asset: {assetPath}");
                return null;
            }

            var instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(asset);
            }

            instance.name = name;
            instance.transform.SetParent(parent);
            instance.transform.position = position;
            instance.transform.localScale = scale;
            instance.transform.rotation = IsUltimateNatureAsset(assetPath)
                ? rotation * UltimateNatureRotationCorrection
                : rotation;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            return instance;
        }

        private static bool IsUltimateNatureAsset(string assetPath)
        {
            return assetPath.StartsWith(UltimateNatureRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static void TintImportedMaterials(GameObject root, Color tint)
        {
            if (root == null)
            {
                return;
            }

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);
                // Keep the imported kit's albedo textures visible. The tint is only a
                // restrained art-direction pass for warm/cool grouping, not a material replacement.
                var restrainedTint = Color.Lerp(Color.white, tint, 0.16f);
                propertyBlock.SetColor("_BaseColor", restrainedTint);
                propertyBlock.SetColor("_Color", restrainedTint);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private static void CreateTree(Transform parent, Vector3 position, float height, int variant)
        {
            var root = new GameObject("PineSilhouette");
            root.transform.SetParent(parent);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, variant * 37f, 0f);
            CreateCylinder("Trunk", root.transform, new Vector3(0f, height * 0.38f, 0f), new Vector3(0.32f, height * 0.75f, 0.32f), Materials["trunk"], Quaternion.identity);
            var material = variant == 2 ? Materials["leafWarm"] : variant == 1 ? Materials["leaf"] : Materials["leafDeep"];
            CreateCone("CanopyLow", root.transform, new Vector3(0f, height * 0.56f, 0f), height * 0.72f, height * 0.70f, material, 8);
            CreateCone("CanopyMid", root.transform, new Vector3(0f, height * 0.80f, 0f), height * 0.55f, height * 0.70f, material, 8);
            CreateCone("CanopyTop", root.transform, new Vector3(0f, height * 1.03f, 0f), height * 0.38f, height * 0.62f, material, 8);
        }

        private static void CreateShrub(Transform parent, Vector3 position, float scale, Material material)
        {
            var root = new GameObject("ShrubCluster");
            root.transform.SetParent(parent);
            root.transform.position = position;
            for (var i = 0; i < 4; i++)
            {
                var offset = new Vector3(Mathf.Cos(i * 1.7f) * scale * 0.45f, 0f, Mathf.Sin(i * 1.7f) * scale * 0.45f);
                CreateSphere("ShrubLeaf", root.transform, offset + Vector3.up * scale * (0.35f + (i % 2) * 0.13f), Vector3.one * scale * (0.55f + (i % 3) * 0.08f), material);
            }
        }

        private static void CreateGrassTuft(Transform parent, Vector3 position, float scale)
        {
            var root = new GameObject("GrassTuft");
            root.transform.SetParent(parent);
            root.transform.position = position;
            for (var i = 0; i < 3; i++)
            {
                var blade = CreateCone("GrassBlade", root.transform, Vector3.up * scale * 0.45f, scale * 0.12f, scale, Materials["grass"], 5);
                blade.transform.localRotation = Quaternion.Euler(0f, i * 120f, (i - 1) * 14f);
            }
        }

        private static void CreateFlower(Transform parent, Vector3 position, Material material)
        {
            var root = new GameObject("TinyFlower");
            root.transform.SetParent(parent);
            root.transform.position = position;
            CreateCylinder("Stem", root.transform, Vector3.up * 0.22f, new Vector3(0.025f, 0.22f, 0.025f), Materials["grass"], Quaternion.identity);
            CreateSphere("Petal", root.transform, Vector3.up * 0.48f, Vector3.one * 0.13f, material);
        }

        private static void CreateLanterns()
        {
            var root = new GameObject("Lantern_Markers");
            var positions = new[] { new Vector3(-3.0f, 0.35f, -1.0f), new Vector3(3.1f, 0.35f, 0.2f), new Vector3(6.8f, 0.35f, 2.2f) };
            for (var i = 0; i < positions.Length; i++)
            {
                var lantern = new GameObject("Lantern");
                lantern.transform.SetParent(root.transform);
                lantern.transform.position = positions[i];
                CreateCylinder("LanternPost", lantern.transform, Vector3.up * 0.7f, new Vector3(0.06f, 0.7f, 0.06f), Materials["wood"], Quaternion.identity);
                CreateCube("LanternCap", lantern.transform, Vector3.up * 1.55f, new Vector3(0.34f, 0.08f, 0.34f), Materials["wood"]);
                CreateSphere("LanternGlow", lantern.transform, Vector3.up * 1.32f, Vector3.one * 0.18f, Materials["lantern"]);

                var lightObject = new GameObject("LanternLight");
                lightObject.transform.SetParent(lantern.transform);
                lightObject.transform.localPosition = Vector3.up * 1.32f;
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.35f, 0.08f);
                light.intensity = 0.65f;
                light.range = 2.8f;
            }
        }

        private static void CreateStars()
        {
            var root = new GameObject("Night_Sky_Details");
            var moon = CreateSphere("Moon", root.transform, new Vector3(-8.0f, 9.5f, 11.5f), Vector3.one * 1.35f, Materials["moon"]);
            moon.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            UnityEngine.Random.InitState(8421);
            for (var i = 0; i < 30; i++)
            {
                var position = new Vector3(UnityEngine.Random.Range(-13f, 13f), UnityEngine.Random.Range(5.5f, 12f), 12.5f + UnityEngine.Random.Range(-0.4f, 0.5f));
                var star = CreateSphere("Star", root.transform, position, Vector3.one * UnityEngine.Random.Range(0.025f, 0.065f), Materials["star"]);
                star.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("LookDev Camera");
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.fieldOfView = 43f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 80f;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.transform.position = new Vector3(12.8f, 8.7f, -14.8f);
            LookAt(camera.transform, new Vector3(0.4f, 1.25f, 2.0f));
            camera.backgroundColor = new Color(0.02f, 0.035f, 0.065f);
        }

        private static GameObject CreateDisc(string name, Transform parent, Vector3 position, float radius, float thickness, Material material, int segments, float edgeNoise, Vector3 scale = default)
        {
            var meshObject = new GameObject(name);
            meshObject.transform.SetParent(parent);
            meshObject.transform.position = position;
            meshObject.transform.localScale = scale == default ? Vector3.one : scale;
            var mesh = new Mesh { name = name + "Mesh" };
            var vertices = new Vector3[segments * 2 + 2];
            var triangles = new int[segments * 12];
            vertices[0] = Vector3.up * thickness;
            vertices[1] = Vector3.zero;
            for (var i = 0; i < segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                var noise = 1f + Mathf.Sin(i * 2.71f) * edgeNoise;
                var x = Mathf.Cos(angle) * radius * noise;
                var z = Mathf.Sin(angle) * radius * noise;
                vertices[2 + i] = new Vector3(x, thickness, z);
                vertices[2 + segments + i] = new Vector3(x, 0f, z);
                var next = (i + 1) % segments;
                var top = 2 + i;
                var nextTop = 2 + next;
                var bottom = 2 + segments + i;
                var nextBottom = 2 + segments + next;
                var index = i * 12;
                triangles[index] = 0; triangles[index + 1] = nextTop; triangles[index + 2] = top;
                triangles[index + 3] = 1; triangles[index + 4] = bottom; triangles[index + 5] = nextBottom;
                triangles[index + 6] = top; triangles[index + 7] = nextTop; triangles[index + 8] = nextBottom;
                triangles[index + 9] = top; triangles[index + 10] = nextBottom; triangles[index + 11] = bottom;
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            var filter = meshObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = meshObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return meshObject;
        }

        private static void CreateRectangularSurfaceAroundHole(
            string name,
            Transform parent,
            Vector2 surfaceCenter,
            Vector2 surfaceSize,
            Vector2 holeCenter,
            Vector2 holeSize,
            float centerY,
            float thickness,
            Material material,
            bool keepCollider)
        {
            var minX = surfaceCenter.x - surfaceSize.x * 0.5f;
            var maxX = surfaceCenter.x + surfaceSize.x * 0.5f;
            var minZ = surfaceCenter.y - surfaceSize.y * 0.5f;
            var maxZ = surfaceCenter.y + surfaceSize.y * 0.5f;
            var holeMinX = Mathf.Clamp(holeCenter.x - holeSize.x * 0.5f, minX, maxX);
            var holeMaxX = Mathf.Clamp(holeCenter.x + holeSize.x * 0.5f, minX, maxX);
            var holeMinZ = Mathf.Clamp(holeCenter.y - holeSize.y * 0.5f, minZ, maxZ);
            var holeMaxZ = Mathf.Clamp(holeCenter.y + holeSize.y * 0.5f, minZ, maxZ);

            CreateSurfaceStrip(name + "_West", parent, minX, holeMinX, minZ, maxZ, centerY, thickness, material, keepCollider);
            CreateSurfaceStrip(name + "_East", parent, holeMaxX, maxX, minZ, maxZ, centerY, thickness, material, keepCollider);
            CreateSurfaceStrip(name + "_South", parent, holeMinX, holeMaxX, minZ, holeMinZ, centerY, thickness, material, keepCollider);
            CreateSurfaceStrip(name + "_North", parent, holeMinX, holeMaxX, holeMaxZ, maxZ, centerY, thickness, material, keepCollider);
            CreateEllipticalHoleCornerPatch(name + "_LakeEdge", parent, holeCenter, holeSize, centerY + thickness * 0.5f, material, keepCollider);
        }

        private static void CreateSurfaceStrip(string name, Transform parent, float minX, float maxX, float minZ, float maxZ, float centerY, float thickness, Material material, bool keepCollider)
        {
            var width = maxX - minX;
            var depth = maxZ - minZ;
            if (width <= 0.01f || depth <= 0.01f)
            {
                return;
            }

            CreateBox(
                name,
                parent,
                new Vector3((minX + maxX) * 0.5f, centerY, (minZ + maxZ) * 0.5f),
                new Vector3(width, thickness, depth),
                material,
                Quaternion.identity,
                keepCollider);
        }

        private static void CreateEllipticalHoleCornerPatch(string name, Transform parent, Vector2 center, Vector2 openingSize, float surfaceY, Material material, bool keepCollider)
        {
            const float targetCellSize = 1.5f;
            var columns = Mathf.Max(8, Mathf.CeilToInt(openingSize.x / targetCellSize));
            var rows = Mathf.Max(8, Mathf.CeilToInt(openingSize.y / targetCellSize));
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var uvs = new Vector2[vertices.Length];
            for (var row = 0; row <= rows; row++)
            {
                for (var column = 0; column <= columns; column++)
                {
                    var uv = new Vector2(column / (float)columns, row / (float)rows);
                    var index = row * (columns + 1) + column;
                    vertices[index] = new Vector3(
                        center.x + (uv.x - 0.5f) * openingSize.x,
                        surfaceY,
                        center.y + (uv.y - 0.5f) * openingSize.y);
                    uvs[index] = uv;
                }
            }

            var triangles = new List<int>(columns * rows * 6);
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var normalizedX = ((column + 0.5f) / columns - 0.5f) * 2f;
                    var normalizedZ = ((row + 0.5f) / rows - 0.5f) * 2f;
                    if (normalizedX * normalizedX + normalizedZ * normalizedZ <= 1f)
                    {
                        continue;
                    }

                    var stride = columns + 1;
                    var bottomLeft = row * stride + column;
                    var bottomRight = bottomLeft + 1;
                    var topLeft = bottomLeft + stride;
                    var topRight = topLeft + 1;
                    triangles.Add(bottomLeft);
                    triangles.Add(topLeft);
                    triangles.Add(bottomRight);
                    triangles.Add(bottomRight);
                    triangles.Add(topLeft);
                    triangles.Add(topRight);
                }
            }

            var patch = new GameObject(name);
            patch.transform.SetParent(parent);
            var mesh = new Mesh { name = name + "Mesh", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            patch.AddComponent<MeshFilter>().sharedMesh = mesh;
            patch.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (keepCollider)
            {
                patch.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }

        private static GameObject CreateLakeBasin(string name, Transform parent, Vector3 surfaceCenter, float width, float depth, float lakeDepth, int segments, Material material)
        {
            var basin = new GameObject(name);
            basin.transform.SetParent(parent);
            basin.transform.position = surfaceCenter;

            var vertices = new Vector3[segments * 3 + 1];
            var outerRadius = new Vector2(width * 0.5f, depth * 0.5f);
            var middleRadius = outerRadius * 0.62f;
            var bottomRadius = outerRadius * 0.34f;
            for (var index = 0; index < segments; index++)
            {
                var angle = index / (float)segments * Mathf.PI * 2f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertices[index] = new Vector3(direction.x * outerRadius.x, 0f, direction.y * outerRadius.y);
                vertices[segments + index] = new Vector3(direction.x * middleRadius.x, -lakeDepth * 0.68f, direction.y * middleRadius.y);
                vertices[segments * 2 + index] = new Vector3(direction.x * bottomRadius.x, -lakeDepth, direction.y * bottomRadius.y);
            }
            vertices[segments * 3] = new Vector3(0f, -lakeDepth, 0f);

            var triangles = new List<int>(segments * 15);
            for (var index = 0; index < segments; index++)
            {
                var next = (index + 1) % segments;
                AppendUpwardRingQuad(triangles, index, next, segments + index, segments + next);
                AppendUpwardRingQuad(triangles, segments + index, segments + next, segments * 2 + index, segments * 2 + next);
                triangles.Add(segments * 3);
                triangles.Add(segments * 2 + next);
                triangles.Add(segments * 2 + index);
            }

            var mesh = new Mesh { name = name + "Mesh", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            basin.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = basin.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            basin.AddComponent<MeshCollider>().sharedMesh = mesh;
            return basin;
        }

        private static void CreateUnderwaterVolume(string name, Transform parent, Vector3 surfaceCenter, float width, float depth, float lakeDepth)
        {
            var volumeObject = new GameObject(name);
            volumeObject.transform.SetParent(parent);
            volumeObject.transform.position = surfaceCenter - Vector3.up * lakeDepth * 0.5f;
            var bounds = volumeObject.AddComponent<BoxCollider>();
            bounds.isTrigger = true;
            // A box is cheaper than a mesh volume; keep it inside the elliptical shore
            // so standing on the corner banks never activates underwater grading.
            bounds.size = new Vector3(width * 0.62f, lakeDepth, depth * 0.62f);

            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = false;
            volume.priority = 35f;
            volume.blendDistance = 1.2f;
            volume.weight = 1f;

            const string profilePath = MaterialPath + "/LookDev_UnderwaterProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "LookDev_UnderwaterProfile";
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            else
            {
                profile.components.Clear();
            }

            var color = profile.Add<ColorAdjustments>();
            color.postExposure.overrideState = true;
            color.postExposure.value = -0.62f;
            color.contrast.overrideState = true;
            color.contrast.value = -8f;
            color.saturation.overrideState = true;
            color.saturation.value = -26f;
            color.colorFilter.overrideState = true;
            color.colorFilter.value = new Color(0.34f, 0.64f, 0.82f);

            var vignette = profile.Add<Vignette>();
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.18f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.72f;
            EditorUtility.SetDirty(profile);
            volume.profile = profile;
        }

        private static void CreatePlayerWaterVolume(string name, Transform parent, Vector3 surfaceCenter, float width, float depth, float lakeDepth)
        {
            const int segments = 24;
            var waterVolume = new GameObject(name);
            waterVolume.transform.SetParent(parent);
            waterVolume.transform.position = surfaceCenter;

            var vertices = new Vector3[segments * 2 + 2];
            var topCenter = segments * 2;
            var bottomCenter = topCenter + 1;
            var radius = new Vector2(width * 0.49f, depth * 0.49f);
            const float topOffset = 0.25f;
            var bottomOffset = -lakeDepth - 0.5f;
            for (var index = 0; index < segments; index++)
            {
                var angle = index / (float)segments * Mathf.PI * 2f;
                var x = Mathf.Cos(angle) * radius.x;
                var z = Mathf.Sin(angle) * radius.y;
                vertices[index] = new Vector3(x, topOffset, z);
                vertices[segments + index] = new Vector3(x, bottomOffset, z);
            }

            vertices[topCenter] = new Vector3(0f, topOffset, 0f);
            vertices[bottomCenter] = new Vector3(0f, bottomOffset, 0f);

            var triangles = new List<int>(segments * 12);
            for (var index = 0; index < segments; index++)
            {
                var next = (index + 1) % segments;
                triangles.Add(index);
                triangles.Add(next);
                triangles.Add(segments + next);
                triangles.Add(index);
                triangles.Add(segments + next);
                triangles.Add(segments + index);

                triangles.Add(topCenter);
                triangles.Add(next);
                triangles.Add(index);
                triangles.Add(bottomCenter);
                triangles.Add(segments + index);
                triangles.Add(segments + next);
            }

            var mesh = new Mesh { name = name + "Mesh" };
            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var waterCollider = waterVolume.AddComponent<MeshCollider>();
            waterCollider.sharedMesh = mesh;
            waterCollider.convex = true;
            waterCollider.isTrigger = true;

            var waterType = Type.GetType("DemonViglu.FirePlay.World.PlayerWaterVolume, Assembly-CSharp");
            if (waterType == null)
            {
                throw new InvalidOperationException("PlayerWaterVolume is not compiled yet. Wait for Unity script refresh and run the builder again.");
            }

            var waterComponent = waterVolume.AddComponent(waterType);
            var serializedWater = new SerializedObject(waterComponent);
            serializedWater.FindProperty("_surfaceY").floatValue = surfaceCenter.y;
            serializedWater.FindProperty("_entryDepth").floatValue = 0.18f;
            serializedWater.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AppendUpwardRingQuad(List<int> triangles, int outer, int outerNext, int inner, int innerNext)
        {
            triangles.Add(outer);
            triangles.Add(innerNext);
            triangles.Add(outerNext);
            triangles.Add(outer);
            triangles.Add(inner);
            triangles.Add(innerNext);
        }

        private static GameObject CreateCone(string name, Transform parent, Vector3 localPosition, float radius, float height, Material material, int segments)
        {
            var meshObject = new GameObject(name);
            meshObject.transform.SetParent(parent);
            meshObject.transform.localPosition = localPosition;
            var mesh = new Mesh { name = name + "Mesh" };
            var vertices = new Vector3[segments + 2];
            vertices[0] = Vector3.zero;
            vertices[1] = Vector3.up * height;
            for (var i = 0; i < segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                vertices[2 + i] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }
            var triangles = new int[segments * 6];
            for (var i = 0; i < segments; i++)
            {
                var next = (i + 1) % segments;
                var offset = i * 6;
                triangles[offset] = 0; triangles[offset + 1] = 2 + next; triangles[offset + 2] = 2 + i;
                triangles[offset + 3] = 1; triangles[offset + 4] = 2 + i; triangles[offset + 5] = 2 + next;
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            return meshObject;
        }

        private static GameObject CreateRock(string name, Transform parent, Vector3 position, float scale, Material material)
        {
            var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = name;
            rock.transform.SetParent(parent);
            rock.transform.position = position;
            rock.transform.localScale = new Vector3(scale * 1.25f, scale * 0.7f, scale);
            rock.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(rock.GetComponent<Collider>());
            return rock;
        }

        private static GameObject CreateSphere(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            sphere.transform.SetParent(parent);
            sphere.transform.localPosition = localPosition;
            sphere.transform.localScale = scale;
            sphere.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(sphere.GetComponent<Collider>());
            return sphere;
        }

        private static GameObject CreateCylinder(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material, Quaternion rotation)
        {
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent);
            cylinder.transform.localPosition = localPosition;
            cylinder.transform.localScale = scale;
            cylinder.transform.localRotation = rotation;
            cylinder.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(cylinder.GetComponent<Collider>());
            return cylinder;
        }

        private static GameObject CreateCube(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent);
            cube.transform.localPosition = localPosition;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
            return cube;
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 position, Vector3 scale, Material material, Quaternion rotation, bool keepCollider = false)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent);
            box.transform.position = position;
            box.transform.localScale = scale;
            box.transform.rotation = rotation;
            box.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider)
            {
                UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
            }

            return box;
        }

        private static GameObject CreateRamp(string name, Transform parent, Vector3 start, Vector3 end, float width, float thickness, Material material)
        {
            var midpoint = (start + end) * 0.5f;
            var direction = end - start;
            var length = direction.magnitude;
            var ramp = CreateBox(name, parent, midpoint, new Vector3(width, thickness, length), material, Quaternion.identity, true);
            ramp.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            return ramp;
        }

        private static void LookAt(Transform transform, Vector3 target)
        {
            transform.rotation = Quaternion.LookRotation(target - transform.position, Vector3.up);
        }
    }
}
