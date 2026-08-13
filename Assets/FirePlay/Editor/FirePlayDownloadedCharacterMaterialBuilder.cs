using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DemonViglu.FirePlay.Editor
{
    /// <summary>
    /// Keeps the downloaded character's visual appearance stable across GLB,
    /// FBX, Blender and Unity.  The source uses one material for both clothing
    /// and face details, so the Unity baseline deliberately favours soft cloth
    /// over literal glTF metal/roughness values.
    /// </summary>
    public static class FirePlayDownloadedCharacterMaterialBuilder
    {
        private const string GeneratedRoot = "Assets/FirePlay/Art/Character/Generated";
        private const string PrefabRoot = GeneratedRoot + "/Prefabs";

        [MenuItem("FirePlay/Character/Build Downloaded Duo Materials")]
        public static void BuildDownloadedDuoMaterials()
        {
            EnsureFolder(PrefabRoot);
            foreach (var role in new[] { "Male", "Female" })
            {
                var baseColor = ConfigureTexture(role, "BaseColor", TextureImporterType.Default, true);
                var normal = ConfigureTexture(role, "Normal", TextureImporterType.NormalMap, false);
                var material = CreateMaterial(role, baseColor, normal);
                RemapFbxMaterial(role, material);
                BuildExplicitPrefab(role, material);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FirePlay: Downloaded male/female materials, FBX remaps and explicit prefabs are ready.");
        }

        private static Texture2D ConfigureTexture(string role, string suffix, TextureImporterType type, bool srgb)
        {
            var path = $"{GeneratedRoot}/SnowTraveler_{role}_{suffix}.png";
            return ConfigureTexturePath(path, type, srgb);
        }

        private static Texture2D ConfigureTexturePath(string path, TextureImporterType type, bool srgb)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = type;
                importer.sRGBTexture = srgb;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static string EnsureUnityMaskMap(string role)
        {
            var sourcePath = $"{GeneratedRoot}/SnowTraveler_{role}_MetallicRoughness.png";
            var targetPath = $"{GeneratedRoot}/SnowTraveler_{role}_MaskMap.png";
            var absoluteSource = ToAbsolutePath(sourcePath);
            var absoluteTarget = ToAbsolutePath(targetPath);
            if (!File.Exists(absoluteSource))
            {
                throw new FileNotFoundException($"Missing glTF metallic/roughness texture: {sourcePath}");
            }

            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!ImageConversion.LoadImage(source, File.ReadAllBytes(absoluteSource), false))
            {
                UnityEngine.Object.DestroyImmediate(source);
                throw new InvalidOperationException($"Could not read {sourcePath}.");
            }

            var input = source.GetPixels32();
            var output = new Color32[input.Length];
            for (var i = 0; i < input.Length; i++)
            {
                // Source glTF: R unused, G roughness, B metallic.
                // URP Lit mask: R metallic, G occlusion, B detail mask, A smoothness.
                output[i] = new Color32(input[i].b, 255, 255, (byte)(255 - input[i].g));
            }

            var mask = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
            mask.SetPixels32(output);
            mask.Apply(false, false);
            File.WriteAllBytes(absoluteTarget, mask.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(mask);
            AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
            return targetPath;
        }

        private static Material CreateMaterial(string role, Texture2D baseColor, Texture2D normal)
        {
            var path = $"{GeneratedRoot}/SnowTraveler_{role}_URP.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                {
                    name = $"SnowTraveler_{role}_URP"
                };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            material.SetTexture("_BaseMap", baseColor);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.SetTexture("_MetallicGlossMap", null);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.18f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_Cull", (float)CullMode.Back);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_ReceiveShadows", 1f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_NORMALMAP");
            material.DisableKeyword("_METALLICSPECGLOSSMAP");
            material.renderQueue = (int)RenderQueue.Geometry;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void RemapFbxMaterial(string role, Material material)
        {
            var modelPath = $"{GeneratedRoot}/SnowTraveler_{role}_Rigged.fbx";
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Could not access ModelImporter for {modelPath}.");
            }

            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            var sourceMaterials = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().ToArray();
            foreach (var sourceMaterial in sourceMaterials)
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), sourceMaterial.name), material);
            }

            importer.SaveAndReimport();
            Debug.Log($"FirePlay: Remapped {role} FBX materials to {material.name}.");
        }

        private static void BuildExplicitPrefab(string role, Material material)
        {
            var modelPath = $"{GeneratedRoot}/SnowTraveler_{role}_Rigged.fbx";
            var prefabPath = $"{PrefabRoot}/SnowTraveler_{role}.prefab";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                throw new InvalidOperationException($"Missing imported character model: {modelPath}");
            }

            var instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException($"Could not instantiate {modelPath}");
            }

            try
            {
                instance.name = $"SnowTraveler_{role}";
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    if (materials.Length == 0)
                    {
                        renderer.sharedMaterial = material;
                        continue;
                    }

                    for (var i = 0; i < materials.Length; i++)
                    {
                        materials[i] = material;
                    }

                    renderer.sharedMaterials = materials;
                }

                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                {
                    AssetDatabase.DeleteAsset(prefabPath);
                }

                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }

        private static string ToAbsolutePath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new InvalidOperationException("Could not resolve Unity project root.");
            }

            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
