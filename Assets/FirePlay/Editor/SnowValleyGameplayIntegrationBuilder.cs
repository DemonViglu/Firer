using System;
using System.Linq;
using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.Player;
using DemonViglu.FirePlay.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemonViglu.FirePlay.Editor
{
    /// <summary>
    /// Applies reviewed gameplay modules to SnowValley one milestone at a time.
    /// It never rebuilds the environment and never copies DemoScene wholesale.
    /// </summary>
    public static class SnowValleyGameplayIntegrationBuilder
    {
        private const string ScenePath = "Assets/Scenes/SnowValley_Playable.unity";
        private const string FlameModulePrefabPath =
            "Assets/FirePlay/Runtime/Prefab/PlayerFlameModule.prefab";
        private const string PlayerFlamePrefabPath =
            "Assets/FirePlay/Runtime/Prefab/Flame.prefab";

        [MenuItem("FirePlay/Scene Integration/SnowValley/Step 2 - Add Flame + HUD")]
        public static void AddFlameAndHud()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "SnowValley Integration",
                    "请先退出 Play Mode，再执行场景接入。",
                    "好的");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
            {
                EditorUtility.DisplayDialog(
                    "SnowValley Integration",
                    $"请先打开并保存 {ScenePath}，本工具不会自动切换或覆盖其他场景。",
                    "好的");
                return;
            }

            try
            {
                var player = FindSinglePlayer(scene);
                EnsureFlameModule(player);
                EnsureSceneFlameFactory(scene);
                EnsureUiBootstrap(scene);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Selection.activeGameObject = player.gameObject;

                Debug.Log(
                    "[SnowValleyGameplayIntegration] Step 2 complete: " +
                    "Player Flame module, scene Flame factory and HUD bootstrap are explicit in Hierarchy.",
                    player);
                EditorUtility.DisplayDialog(
                    "SnowValley Integration",
                    "Step 2 已写入：FlameModule、场景火苗工厂和 HUD。\n\n" +
                    "本步骤没有接入 Interaction、Activity、Rest 或 Network。",
                    "好的");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "SnowValley Integration",
                    exception.Message,
                    "关闭");
            }
        }

        private static LocalPlayerContext FindSinglePlayer(Scene scene)
        {
            var players = UnityEngine.Object
                .FindObjectsByType<LocalPlayerContext>(
                    FindObjectsInactive.Include)
                .Where(candidate => candidate.gameObject.scene == scene)
                .ToArray();
            if (players.Length != 1)
            {
                throw new InvalidOperationException(
                    $"SnowValley 必须且只能有一个 LocalPlayerContext，当前数量={players.Length}。");
            }

            return players[0];
        }

        private static void EnsureFlameModule(LocalPlayerContext player)
        {
            if (player.GetComponentInChildren<FlameModule>(true) != null)
                return;

            var prefab = LoadPrefab(FlameModulePrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"找不到 Flame Module Prefab：{FlameModulePrefabPath}");
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab, player.transform) as GameObject;
            if (instance == null)
                throw new InvalidOperationException("无法把 FlameModule 实例化到 Player_Core。");

            instance.name = "FlameModule";
            instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
        }

        private static void EnsureSceneFlameFactory(Scene scene)
        {
            var bindings = FindInScene<PlayerSceneServiceBindings>(scene);
            if (bindings == null)
            {
                var root = new GameObject("Gameplay_SceneServices");
                SceneManager.MoveGameObjectToScene(root, scene);
                bindings = root.AddComponent<PlayerSceneServiceBindings>();
            }

            var flamePrefab = LoadPrefab(PlayerFlamePrefabPath);
            var flame = flamePrefab != null ? flamePrefab.GetComponent<FlameBrush>() : null;
            if (flame == null)
            {
                throw new InvalidOperationException(
                    $"找不到玩家火苗 Prefab 或 FlameBrush：{PlayerFlamePrefabPath}");
            }

            SetObjectReference(bindings, "_playerFlamePrefab", flame);
        }

        private static GameObject LoadPrefab(string assetPath)
        {
            // The prefab may have been created or changed while the Unity editor was open.
            // Force a synchronous import so the integration command never observes a stale
            // AssetDatabase entry on its first run.
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        }

        private static void EnsureUiBootstrap(Scene scene)
        {
            var bootstrap = FindInScene<FirePlayUiBootstrap>(scene);
            if (bootstrap == null)
            {
                var root = new GameObject("Gameplay_UI");
                SceneManager.MoveGameObjectToScene(root, scene);
                bootstrap = root.AddComponent<FirePlayUiBootstrap>();
            }

            var serialized = new SerializedObject(bootstrap);
            var showNetwork = serialized.FindProperty("_showNetworkConnectionOnStart");
            if (showNetwork != null)
                showNetwork.boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bootstrap);
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            return UnityEngine.Object
                .FindObjectsByType<T>(
                    FindObjectsInactive.Include)
                .FirstOrDefault(candidate => candidate.gameObject.scene == scene);
        }

        private static void SetObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{target.GetType().Name} 缺少序列化字段 {propertyName}。");
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
