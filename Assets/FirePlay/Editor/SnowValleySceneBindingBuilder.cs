using System;
using System.Linq;
using DemonViglu.FirePlay.Activity;
using DemonViglu.FirePlay.CameraSystem;
using DemonViglu.FirePlay.Core;
using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.Network;
using DemonViglu.FirePlay.Player;
using DemonViglu.FirePlay.UI;
using DemonViglu.FirePlay.World;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DemonViglu.FirePlay.Editor
{
    /// <summary>
    /// Writes the reviewed, explicit SnowValley scene composition.
    /// This is an editor-time scene authoring command; no runtime dependency is
    /// created by AddComponent or by a hidden bootstrap.
    /// </summary>
    public static class SnowValleySceneBindingBuilder
    {
        private const string ScenePath = "Assets/Scenes/SnowValley_Playable.unity";
        private const string NetworkPrefabPath =
            "Assets/FirePlay/Runtime/Prefab/PlayerNetworkGameplay.prefab";
        private const string NetworkWorldStatePrefabPath =
            "Assets/FirePlay/Runtime/Prefab/NetworkWorldState.prefab";
        private const string NetworkPrefabsListPath = "Assets/DefaultNetworkPrefabs.asset";
        private const string NetworkCharacterVisualPrefabPath =
            "Assets/FirePlay/Art/Character/Generated/Prefabs/SnowTraveler_Female.prefab";
        private const string NetworkCharacterFbxPath =
            "Assets/FirePlay/Art/Character/Generated/SnowTraveler_Female_Rigged.fbx";
        private const string NetworkCharacterControllerPath =
            "Assets/FirePlay/Art/Character/Generated/Controllers/SnowTraveler_Female_Locomotion.controller";
        private const string FlameSourcePrefabPath =
            "Assets/FirePlay/Runtime/Prefab/FlameSource.prefab";
        private const string ActivityCatalogPath =
            "Assets/FirePlay/Content/Activities/ActivityCatalog.asset";
        private const string ActivityVisualModulePath =
            "Assets/FirePlay/Runtime/Prefab/PlayerActivityVisualModule.prefab";
        private const string MarshmallowDefinitionPath =
            "Assets/FirePlay/Content/Activities/MarshmallowActivityDefinition.asset";
        private const string FishingDefinitionPath =
            "Assets/FirePlay/Content/Activities/FishingActivityDefinition.asset";
        private const string StargazingDefinitionPath =
            "Assets/FirePlay/Content/Activities/StargazingActivityDefinition.asset";

        [MenuItem("FirePlay/Scene Integration/SnowValley/Configure Scene Bindings")]
        public static void ConfigureFromMenu()
        {
            Configure();
        }

        /// <summary>
        /// Batchmode entry point used by the repository task so the scene is
        /// configured without requiring a manual Unity menu click.
        /// </summary>
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先退出 Play Mode，再配置 SnowValley 场景绑定。");

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid() || scene.path != ScenePath)
                throw new InvalidOperationException($"无法打开正式接入场景：{ScenePath}");

            var player = FindSinglePlayer(scene);
            var services = FindOrCreateRoot(scene, "Gameplay_SceneServices");
            var bindings = GetOrAdd<PlayerSceneServiceBindings>(services);

            ConfigureNetworkPlayerVisual();
            ConfigureGameplayWorldContent(scene);

            var targets = player.GetComponent<PlayerCameraTargetSet>();
            if (targets == null)
                throw new InvalidOperationException("Player_Core 缺少 PlayerCameraTargetSet。");
            BindActivityFollowTargetToFrame(targets);

            var activityRegistry = ConfigureActivityRegistry(scene);
            ConfigureActivityAnchors(scene);
            ConfigureOutputCamera(scene, player);
            var cameraRig = ConfigureActivityCameraRig(scene, targets);
            ConfigurePlayerGameplayModules(player, activityRegistry, cameraRig);
            ConfigureSceneBindings(bindings, activityRegistry, cameraRig, player);
            ConfigureNetworkBootstrap(scene, bindings, player);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = services;

            Debug.Log(
                "[SnowValleySceneBindingBuilder] SnowValley 场景绑定完成：" +
                "Activity Registry、Activity Camera Rig、Network Bootstrap、Spawn Point " +
                "Player Camera Targets、网络角色表现和余火循环内容点已显式写入。",
                services);
        }

        private static void ConfigureNetworkPlayerVisual()
        {
            var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkCharacterVisualPrefabPath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(NetworkCharacterControllerPath);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(NetworkCharacterFbxPath)
                .OfType<Avatar>()
                .FirstOrDefault(candidate => candidate.isValid);
            if (visualPrefab == null || controller == null || avatar == null)
            {
                throw new InvalidOperationException(
                    "网络角色表现资源不完整；需要 SnowTraveler Female Prefab、有效 Avatar 和 Locomotion Controller。");
            }

            var playerRoot = PrefabUtility.LoadPrefabContents(NetworkPrefabPath);
            try
            {
                // Locomotion owns the facing root. Animator/social presentation only
                // writes local offsets on its child visual, so an emote can never
                // overwrite the player's persistent yaw.
                var oldVisual = playerRoot.transform.Find("SnowTravelerVisual");
                if (oldVisual != null)
                    UnityEngine.Object.DestroyImmediate(oldVisual.gameObject);

                var facingRoot = playerRoot.transform.Find("CharacterFacingRoot");
                if (facingRoot == null)
                {
                    var facingObject = new GameObject("CharacterFacingRoot");
                    facingObject.transform.SetParent(playerRoot.transform, false);
                    facingRoot = facingObject.transform;
                }

                facingRoot.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                facingRoot.localScale = Vector3.one;

                var previousNestedVisual = facingRoot.Find("SnowTravelerVisual");
                if (previousNestedVisual != null)
                    UnityEngine.Object.DestroyImmediate(previousNestedVisual.gameObject);

                var visual = PrefabUtility.InstantiatePrefab(visualPrefab, facingRoot) as GameObject;
                if (visual == null)
                    throw new InvalidOperationException("无法把 SnowTraveler 角色表现写入 PlayerNetworkGameplay。");

                visual.name = "SnowTravelerVisual";
                visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                visual.transform.localScale = Vector3.one;

                var animator = visual.GetComponentInChildren<Animator>(true);
                if (animator == null)
                    animator = visual.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                var body = playerRoot.transform.Find("Body");
                if (body != null)
                {
                    foreach (var renderer in body.GetComponentsInChildren<Renderer>(true))
                        renderer.enabled = false;
                }

                var movement = playerRoot.GetComponent<PlayerMovement>();
                var input = playerRoot.GetComponent<FirePlayPlayerInput>();
                var locomotion = playerRoot.GetComponent<PlayerLocomotionAnimationBridge>();
                if (locomotion == null)
                    locomotion = playerRoot.AddComponent<PlayerLocomotionAnimationBridge>();

                AssignObjectReference(movement, "_visualTransform", facingRoot);
                AssignObjectReference(locomotion, "_movement", movement);
                AssignObjectReference(locomotion, "_input", input);
                AssignObjectReference(locomotion, "_animator", animator);
                var animation = playerRoot.GetComponentInChildren<PlayerAnimationController>(true);
                AssignObjectReference(animation, "_animator", animator);
                AssignObjectReference(animation, "_placeholderVisual", visual.transform);
                AssignBool(animation, "_useSocialCueFallback", true);

                PrefabUtility.SaveAsPrefabAsset(playerRoot, NetworkPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void ConfigureGameplayWorldContent(Scene scene)
        {
            var root = FindOrCreateRoot(scene, "Gameplay_WorldContent");
            var flameSourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlameSourcePrefabPath);
            if (flameSourcePrefab == null)
                throw new InvalidOperationException("找不到 FlameSource Prefab。");

            ConfigureWorldPrefab(
                root.transform,
                flameSourcePrefab,
                "FlameSource_CampRoute",
                "snow.flame-source.camp-route",
                new Vector3(3f, 0.52f, -1.5f));
            ConfigureWorldPrefab(
                root.transform,
                flameSourcePrefab,
                "FlameSource_ValleyRoute",
                "snow.flame-source.valley-route",
                new Vector3(10f, 0.52f, 6f));
            ConfigureWorldPrefab(
                root.transform,
                flameSourcePrefab,
                "FlameSource_LakeRoute",
                "snow.flame-source.lake-route",
                new Vector3(16f, 0.48f, 11.5f));
            RequireAuthoredSnowGroveWorldTree(scene);

            EditorUtility.SetDirty(root);
        }

        private static void RequireAuthoredSnowGroveWorldTree(Scene scene)
        {
            var worldTrees = UnityEngine.Object
                .FindObjectsByType<WorldTreeContribution>(FindObjectsInactive.Include)
                .Where(candidate => candidate.gameObject.scene == scene)
                .ToArray();
            if (worldTrees.Length != 1)
            {
                throw new InvalidOperationException(
                    $"SnowValley 必须且只能有一棵已显式装配的 SnowGrove 世界树，当前数量={worldTrees.Length}。" +
                    "请恢复正式场景中的 SnowGrove_WorldTree 显式节点；不会再回退实例化旧 Tree.prefab。");
            }

            var identity = worldTrees[0].GetComponent<StableSceneId>();
            if (identity == null || identity.Value != "snow.world-tree.main")
            {
                throw new InvalidOperationException(
                    "SnowValley 世界树必须显式配置 StableSceneId=snow.world-tree.main。");
            }

            if (worldTrees[0].GetComponent<RestorableNode>() != null)
            {
                throw new InvalidOperationException(
                    "SnowValley 世界树仍挂有已弃用的 RestorableNode；贡献树不得接回颜色复苏实验链。");
            }

            var tree = worldTrees[0];
            var progressVisuals = tree.GetComponent<WorldTreeProgressVisuals>();
            var networkAdapter = tree.GetComponent<FirePlayNetworkWorldTree>();
            if (tree.GetComponent<Collider>() == null
                || tree.GetComponent<NetworkObject>() == null
                || progressVisuals == null
                || networkAdapter == null)
            {
                throw new InvalidOperationException(
                    "SnowValley 世界树必须显式装配 Collider、NetworkObject、" +
                    "WorldTreeProgressVisuals 与 FirePlayNetworkWorldTree。");
            }

            var networkAdapterObject = new SerializedObject(networkAdapter);
            if (networkAdapterObject.FindProperty("_tree")?.objectReferenceValue != tree
                || networkAdapterObject.FindProperty("_visuals")?.objectReferenceValue != progressVisuals)
            {
                throw new InvalidOperationException(
                    "SnowValley 世界树网络适配器必须显式绑定同对象的 WorldTreeContribution " +
                    "与 WorldTreeProgressVisuals；不会在运行时按名称修复。");
            }
        }

        private static GameObject ConfigureWorldPrefab(
            Transform parent,
            GameObject prefab,
            string objectName,
            string stableId,
            Vector3 worldPosition)
        {
            var existing = parent.Find(objectName);
            var instance = existing != null ? existing.gameObject : null;
            if (instance == null)
            {
                instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
                if (instance == null)
                    throw new InvalidOperationException($"无法实例化世界内容：{objectName}");
                Undo.RegisterCreatedObjectUndo(instance, $"Add {objectName}");
            }

            instance.name = objectName;
            instance.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
            instance.transform.localScale = Vector3.one;

            var identity = instance.GetComponent<StableSceneId>();
            if (identity == null)
                throw new InvalidOperationException($"世界内容 {objectName} 缺少 StableSceneId。");
            var serialized = new SerializedObject(identity);
            serialized.FindProperty("_value").stringValue = stableId;
            serialized.FindProperty("_allowRuntimeAssignment").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(instance);
            return instance;
        }

        private static void AssignObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            if (target == null)
                throw new InvalidOperationException($"无法配置 {propertyName}：目标组件为空。");

            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException($"{target.GetType().Name} 缺少序列化字段 {propertyName}。");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void AssignBool(UnityEngine.Object target, string propertyName, bool value)
        {
            if (target == null)
                throw new InvalidOperationException($"无法配置 {propertyName}：目标组件为空。");

            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException($"{target.GetType().Name} 缺少序列化字段 {propertyName}。");
            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static LocalPlayerContext FindSinglePlayer(Scene scene)
        {
            var players = UnityEngine.Object
                .FindObjectsByType<LocalPlayerContext>(FindObjectsInactive.Include)
                .Where(candidate => candidate.gameObject.scene == scene)
                .ToArray();
            if (players.Length != 1)
                throw new InvalidOperationException(
                    $"SnowValley 必须且只能有一个 LocalPlayerContext，当前数量={players.Length}。");
            return players[0];
        }

        private static GameObject FindOrCreateRoot(Scene scene, string name)
        {
            var root = scene.GetRootGameObjects().FirstOrDefault(candidate => candidate.name == name);
            if (root != null)
                return root;

            root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, $"Create {name}");
            return root;
        }

        private static T GetOrAdd<T>(GameObject owner) where T : Component
        {
            var component = owner.GetComponent<T>();
            if (component != null)
                return component;

            component = Undo.AddComponent<T>(owner);
            EditorUtility.SetDirty(owner);
            return component;
        }

        private static void BindActivityFollowTargetToFrame(PlayerCameraTargetSet targets)
        {
            var serialized = new SerializedObject(targets);
            var activityFollow = serialized.FindProperty("_activityFollowTarget");
            if (activityFollow == null)
                throw new InvalidOperationException("PlayerCameraTargetSet 缺少 _activityFollowTarget 字段。");

            activityFollow.objectReferenceValue = targets.FrameTarget;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(targets);
        }

        private static ActivityLogicRegistryBehaviour ConfigureActivityRegistry(Scene scene)
        {
            var root = FindOrCreateRoot(scene, "Gameplay_ActivityServices");
            var registry = GetOrAdd<ActivityLogicRegistryBehaviour>(root);
            var marshmallow = GetOrAdd<MarshmallowActivityLogicFactory>(root);
            var fishing = GetOrAdd<FishingActivityLogicFactory>(root);
            var emote = GetOrAdd<EmoteActivityLogicFactory>(root);
            var guitar = GetOrAdd<GuitarActivityLogicFactory>(root);
            var stargazing = GetOrAdd<StargazingActivityLogicFactory>(root);

            var serialized = new SerializedObject(registry);
            var factories = serialized.FindProperty("_factories");
            factories.arraySize = 5;
            factories.GetArrayElementAtIndex(0).objectReferenceValue = marshmallow;
            factories.GetArrayElementAtIndex(1).objectReferenceValue = fishing;
            factories.GetArrayElementAtIndex(2).objectReferenceValue = emote;
            factories.GetArrayElementAtIndex(3).objectReferenceValue = guitar;
            factories.GetArrayElementAtIndex(4).objectReferenceValue = stargazing;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(registry);
            return registry;
        }

        private static void ConfigureActivityAnchors(Scene scene)
        {
            var root = FindOrCreateRoot(scene, "Gameplay_ActivityAnchors");
            var marshmallow = LoadDefinition(MarshmallowDefinitionPath);
            var fishing = LoadDefinition(FishingDefinitionPath);
            var stargazing = LoadDefinition(StargazingDefinitionPath);

            ConfigureAnchor(
                root.transform,
                "CampfireActivityAnchor",
                "snow.activity.campfire.main",
                "篝火旁",
                "snow.camp",
                new Vector3(-0.5f, 0.38f, 0.8f),
                new[] { marshmallow },
                restRadius: 2.75f,
                autoStartStargazing: false);
            ConfigureAnchor(
                root.transform,
                "FishingActivityAnchor",
                "snow.activity.fishing.lake",
                "冰湖边",
                "snow.frozen-lake",
                new Vector3(18f, 0.36f, 14f),
                new[] { fishing },
                restRadius: 0f,
                autoStartStargazing: false);
            ConfigureAnchor(
                root.transform,
                "TentRestAnchor",
                "snow.activity.rest.tent",
                "帐篷旁",
                "snow.camp",
                new Vector3(-8f, 0.36f, 6f),
                new[] { stargazing },
                restRadius: 3.25f,
                autoStartStargazing: true);
        }

        private static ActivityDefinitionAsset LoadDefinition(string path)
        {
            var definition = AssetDatabase.LoadAssetAtPath<ActivityDefinitionAsset>(path);
            if (definition == null)
                throw new InvalidOperationException($"找不到 Activity Definition：{path}");
            return definition;
        }

        private static void ConfigureAnchor(
            Transform parent,
            string objectName,
            string stableId,
            string displayName,
            string regionId,
            Vector3 worldPosition,
            ActivityDefinitionAsset[] activities,
            float restRadius,
            bool autoStartStargazing)
        {
            var owner = EnsureChild(parent, objectName);
            owner.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);

            var identity = GetOrAdd<StableSceneId>(owner);
            var identitySerialized = new SerializedObject(identity);
            identitySerialized.FindProperty("_value").stringValue = stableId;
            identitySerialized.FindProperty("_allowRuntimeAssignment").boolValue = false;
            identitySerialized.ApplyModifiedPropertiesWithoutUndo();

            var anchor = GetOrAdd<ActivityAnchorNode>(owner);
            var anchorSerialized = new SerializedObject(anchor);
            anchorSerialized.FindProperty("_anchorId").stringValue = stableId;
            anchorSerialized.FindProperty("_displayName").stringValue = displayName;
            anchorSerialized.FindProperty("_regionId").stringValue = regionId;
            var activityList = anchorSerialized.FindProperty("_activities");
            activityList.arraySize = activities.Length;
            for (var index = 0; index < activities.Length; index++)
                activityList.GetArrayElementAtIndex(index).objectReferenceValue = activities[index];
            anchorSerialized.ApplyModifiedPropertiesWithoutUndo();

            if (restRadius > 0f)
            {
                var rest = GetOrAdd<RestSpot>(owner);
                var restSerialized = new SerializedObject(rest);
                restSerialized.FindProperty("_interactionRadius").floatValue = restRadius;
                restSerialized.ApplyModifiedPropertiesWithoutUndo();

                if (autoStartStargazing)
                {
                    var trigger = GetOrAdd<StargazingActivityTrigger>(owner);
                    var triggerSerialized = new SerializedObject(trigger);
                    triggerSerialized.FindProperty("_restSpot").objectReferenceValue = rest;
                    triggerSerialized.FindProperty("_anchor").objectReferenceValue = anchor;
                    triggerSerialized.ApplyModifiedPropertiesWithoutUndo();

                    var lookTarget = EnsureChild(owner.transform, "SkyLookTarget").transform;
                    lookTarget.localPosition = new Vector3(0f, 6f, 2.5f);
                    var cameraTargets = GetOrAdd<ActivityCameraAnchorTargets>(owner);
                    var cameraTargetsSerialized = new SerializedObject(cameraTargets);
                    cameraTargetsSerialized.FindProperty("_lookTarget").objectReferenceValue = lookTarget;
                    cameraTargetsSerialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EditorUtility.SetDirty(owner);
        }

        private static void ConfigurePlayerGameplayModules(
            LocalPlayerContext player,
            ActivityLogicRegistryBehaviour activityRegistry,
            ActivityCameraRigExecutor cameraRig)
        {
            var stateRoot = EnsureChild(player.transform, "PlayerStateModule");
            var mode = GetOrAdd<PlayerModeController>(stateRoot);

            var interactionRoot = EnsureChild(player.transform, "InteractionModule");
            var interaction = GetOrAdd<PlayerInteraction>(interactionRoot);
            var router = GetOrAdd<InteractionRouter>(interactionRoot);
            var commands = GetOrAdd<WorldCommandExecutor>(interactionRoot);
            var interactionModule = GetOrAdd<InteractionModule>(interactionRoot);

            var interactionSerialized = new SerializedObject(interaction);
            interactionSerialized.FindProperty("_flameController").objectReferenceValue =
                player.GetComponentInChildren<PlayerFlameController>(true);
            interactionSerialized.FindProperty("_modeController").objectReferenceValue = mode;
            interactionSerialized.ApplyModifiedPropertiesWithoutUndo();

            var interactionModuleSerialized = new SerializedObject(interactionModule);
            interactionModuleSerialized.FindProperty("_scanner").objectReferenceValue = interaction;
            interactionModuleSerialized.FindProperty("_router").objectReferenceValue = router;
            interactionModuleSerialized.ApplyModifiedPropertiesWithoutUndo();

            var activityRoot = EnsureChild(player.transform, "ActivityModule");
            var animation = GetOrAdd<PlayerAnimationController>(activityRoot);
            var presentation = GetOrAdd<PlayerActivityPresentationHost>(activityRoot);
            var activityHost = GetOrAdd<PlayerActivityHost>(activityRoot);
            var expressions = GetOrAdd<PlayerExpressionController>(activityRoot);
            var activityModule = GetOrAdd<ActivityModule>(activityRoot);

            var catalog = AssetDatabase.LoadAssetAtPath<ActivityCatalogAsset>(ActivityCatalogPath);
            if (catalog == null)
                throw new InvalidOperationException($"找不到 Activity Catalog：{ActivityCatalogPath}");

            var presentationSerialized = new SerializedObject(presentation);
            presentationSerialized.FindProperty("_cameraExecutorBehaviour").objectReferenceValue = cameraRig;
            presentationSerialized.FindProperty("_movement").objectReferenceValue = player.Movement;
            presentationSerialized.FindProperty("_look").objectReferenceValue = player.Look;
            presentationSerialized.FindProperty("_animation").objectReferenceValue = animation;
            presentationSerialized.ApplyModifiedPropertiesWithoutUndo();

            var activityHostSerialized = new SerializedObject(activityHost);
            activityHostSerialized.FindProperty("_playerId").stringValue = player.PlayerId;
            activityHostSerialized.FindProperty("_isLocalPlayer").boolValue = true;
            activityHostSerialized.FindProperty("_catalogAsset").objectReferenceValue = catalog;
            activityHostSerialized.FindProperty("_logicFactoryBehaviour").objectReferenceValue = activityRegistry;
            activityHostSerialized.FindProperty("_presentationBehaviour").objectReferenceValue = presentation;
            activityHostSerialized.FindProperty("_flameBehaviour").objectReferenceValue =
                player.GetComponentInChildren<FlameResourceController>(true);
            activityHostSerialized.FindProperty("_playerStateBehaviour").objectReferenceValue = mode;
            activityHostSerialized.FindProperty("_nearestAnchorDistance").floatValue = 3f;
            activityHostSerialized.ApplyModifiedPropertiesWithoutUndo();

            var activityModuleSerialized = new SerializedObject(activityModule);
            activityModuleSerialized.FindProperty("_activityHost").objectReferenceValue = activityHost;
            activityModuleSerialized.FindProperty("_presentationHost").objectReferenceValue = presentation;
            activityModuleSerialized.ApplyModifiedPropertiesWithoutUndo();

            // The visual prefab root is intentionally free to be renamed. Use
            // its typed boundary instead of a child name; the old name check
            // instantiated another full module on every Builder run.
            if (activityRoot.GetComponentInChildren<MarshmallowVisuals>(true) == null)
            {
                var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ActivityVisualModulePath);
                if (visualPrefab == null)
                    throw new InvalidOperationException($"找不到 Activity Visual Module：{ActivityVisualModulePath}");

                var visualInstance = PrefabUtility.InstantiatePrefab(
                    visualPrefab,
                    activityRoot.transform) as GameObject;
                if (visualInstance == null)
                    throw new InvalidOperationException("无法把 Activity Visual Module 实例化到 ActivityModule。");
                Undo.RegisterCreatedObjectUndo(visualInstance, "Add Activity Visual Module");
            }

            var restRoot = EnsureChild(player.transform, "RestModule");
            var rest = GetOrAdd<RestInteraction>(restRoot);
            var restSerialized = new SerializedObject(rest);
            restSerialized.FindProperty("_movement").objectReferenceValue = player.Movement;
            restSerialized.FindProperty("_look").objectReferenceValue = player.Look;
            restSerialized.FindProperty("_modeController").objectReferenceValue = mode;
            restSerialized.FindProperty("_resourceController").objectReferenceValue =
                player.GetComponentInChildren<FlameResourceController>(true);
            restSerialized.FindProperty("_animation").objectReferenceValue = animation;
            restSerialized.ApplyModifiedPropertiesWithoutUndo();

            var placement = player.GetComponentInChildren<CampfirePlacement>(true);
            if (placement != null)
            {
                var placementSerialized = new SerializedObject(placement);
                placementSerialized.FindProperty("_modeController").objectReferenceValue = mode;
                placementSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var contraction = player.GetComponentInChildren<FlameContractionController>(true);
            if (contraction != null)
            {
                var contractionSerialized = new SerializedObject(contraction);
                contractionSerialized.FindProperty("_modeController").objectReferenceValue = mode;
                contractionSerialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var playerSerialized = new SerializedObject(player);
            playerSerialized.FindProperty("_expressions").objectReferenceValue = expressions;
            playerSerialized.FindProperty("_commandExecutor").objectReferenceValue = commands;
            playerSerialized.FindProperty("_interactionRouter").objectReferenceValue = router;
            playerSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(stateRoot);
            EditorUtility.SetDirty(interactionRoot);
            EditorUtility.SetDirty(activityRoot);
            EditorUtility.SetDirty(restRoot);
            EditorUtility.SetDirty(player);
        }

        private static GameObject EnsureChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
                return existing.gameObject;

            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(child, $"Create {name}");
            return child;
        }

        private static ActivityCameraRigExecutor ConfigureActivityCameraRig(
            Scene scene,
            PlayerCameraTargetSet playerTargets)
        {
            var root = FindOrCreateRoot(scene, "Gameplay_ActivityCameraRig");
            var executor = GetOrAdd<ActivityCameraRigExecutor>(root);

            var explore = EnsureCamera(root.transform, "ExploreCamera", 10);
            // One semantic activity camera is enough. Activity-specific framing
            // comes from the request profile plus the selected Anchor targets;
            // separate virtual cameras would duplicate hierarchy without adding
            // a new ownership boundary.
            var activity = EnsureCamera(root.transform, "ActivityCamera", 0);
            var activityGroup = EnsureTargetGroup(root.transform, "ActivityTargetGroup");

            // Exploration must remain locked to the Player target. Any
            // positional/rotational lag makes the Player visibly drift away
            // from screen centre and then snap back while walking or looking.
            BindCameraPipeline(
                explore,
                playerTargets.FollowTarget,
                playerTargets.LookAtTarget,
                positionDamping: 0f,
                rotationDamping: Vector2.zero);
            ConfigureTerrainCollision(explore);
            BindCameraPipeline(
                activity,
                null,
                null,
                positionDamping: 0.15f,
                rotationDamping: new Vector2(0.2f, 0.2f));
            ConfigureTerrainCollision(activity);

            var serialized = new SerializedObject(executor);
            serialized.FindProperty("_exploreCamera").objectReferenceValue = explore;
            serialized.FindProperty("_fallbackPlayerFrameTarget").objectReferenceValue = playerTargets.FrameTarget;

            var profiles = serialized.FindProperty("_profiles");
            profiles.arraySize = 3;
            ConfigureProfile(profiles.GetArrayElementAtIndex(0), "activity.ritual", activity, activityGroup, 20);
            ConfigureProfile(profiles.GetArrayElementAtIndex(1), "custom.fishing", activity, activityGroup, 20);
            ConfigureProfile(profiles.GetArrayElementAtIndex(2), "activity.stargazing", activity, activityGroup, 20);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(executor);
            return executor;
        }

        private static void ConfigureOutputCamera(Scene scene, LocalPlayerContext player)
        {
            var output = FindOrCreateRoot(scene, "Gameplay_CameraOutput");
            output.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var camera = GetOrAdd<Camera>(output);
            camera.enabled = true;
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1400f;

            GetOrAdd<AudioListener>(output);
            var cameraData = GetOrAdd<UniversalAdditionalCameraData>(output);
            cameraData.renderPostProcessing = true;
            GetOrAdd<CinemachineBrain>(output);

            var playerCamera = player.GetComponentInChildren<Camera>(true);
            if (playerCamera == null)
                throw new InvalidOperationException("Player_Core 缺少可用的内置 Camera。");

            var playerContext = new SerializedObject(player);
            var localCamera = playerContext.FindProperty("_localCamera");
            if (localCamera == null)
                throw new InvalidOperationException("Player_Core 的 LocalPlayerContext 缺少 _localCamera 字段。");
            localCamera.objectReferenceValue = camera;
            playerContext.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(player);

            // PlayerCoreOnly 只提供语义目标。让场景输出相机独占渲染，避免
            // CameraPivot 的父级旋转与 Cinemachine 跟随产生双重变换。
            playerCamera.gameObject.SetActive(false);
            EditorUtility.SetDirty(playerCamera.gameObject);
            EditorUtility.SetDirty(output);
        }

        private static CinemachineCamera EnsureCamera(Transform parent, string name, int priority)
        {
            var existing = parent.Find(name);
            var owner = existing != null ? existing.gameObject : new GameObject(name);
            if (existing == null)
            {
                owner.transform.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(owner, $"Create {name}");
            }

            var camera = GetOrAdd<CinemachineCamera>(owner);
            var follow = GetOrAdd<CinemachineHardLockToTarget>(owner);
            var composer = GetOrAdd<CinemachineRotationComposer>(owner);
            follow.Damping = priority > 0 ? 0f : 0.15f;
            composer.Damping = priority > 0 ? Vector2.zero : new Vector2(0.2f, 0.2f);
            camera.Priority = priority;
            camera.Lens.FieldOfView = 50f;
            EditorUtility.SetDirty(owner);
            return camera;
        }

        private static CinemachineTargetGroup EnsureTargetGroup(Transform parent, string name)
        {
            var existing = parent.Find(name);
            var owner = existing != null ? existing.gameObject : new GameObject(name);
            if (existing == null)
            {
                owner.transform.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(owner, $"Create {name}");
            }
            var group = GetOrAdd<CinemachineTargetGroup>(owner);
            group.PositionMode = CinemachineTargetGroup.PositionModes.GroupCenter;
            group.RotationMode = CinemachineTargetGroup.RotationModes.Manual;
            EditorUtility.SetDirty(owner);
            return group;
        }

        private static void BindCameraPipeline(
            CinemachineCamera camera,
            Transform followTarget,
            Transform lookAtTarget,
            float positionDamping,
            Vector2 rotationDamping)
        {
            if (followTarget != null)
                camera.Follow = followTarget;
            if (lookAtTarget != null)
                camera.LookAt = lookAtTarget;

            var follow = camera.GetComponent<CinemachineHardLockToTarget>();
            if (follow != null)
                follow.Damping = Mathf.Max(0f, positionDamping);

            var composer = camera.GetComponent<CinemachineRotationComposer>();
            if (composer != null)
                composer.Damping = new Vector2(
                    Mathf.Max(0f, rotationDamping.x),
                    Mathf.Max(0f, rotationDamping.y));
        }

        private static void ConfigureTerrainCollision(CinemachineCamera camera)
        {
            // The Player-owned follow target already shortens its camera arm for
            // walls and terrain while explicitly ignoring the Player root.  Keep
            // terrain resolution here as a final output-side guard for both the
            // exploration and activity cameras.  SnowValley currently authors
            // walkable snow/ice on Default, so this mask covers physical layers.
            var decollider = GetOrAdd<CinemachineDecollider>(camera.gameObject);
            decollider.CameraRadius = 0.28f;
            decollider.TerrainResolution = new CinemachineDecollider.TerrainSettings
            {
                Enabled = true,
                TerrainLayers = ~0,
                MaximumRaycast = 12f,
                // Collision correction must remain immediate to prevent clipping,
                // but a slower release avoids the camera snapping back as soon as
                // the terrain/ice obstruction clears.
                Damping = 0.4f
            };
            // General Decollision remains disabled: exploration obstruction is
            // owned by PlayerCameraFollowTarget, while an activity camera may
            // frame several participants and must not guess which colliders are
            // part of its subject. TerrainResolution still prevents underground
            // output without introducing a layer/name-based Player heuristic.
            decollider.Decollision = new CinemachineDecollider.DecollisionSettings
            {
                Enabled = false,
                ObstacleLayers = 0,
                Damping = 0.2f,
                SmoothingTime = 0f
            };
            EditorUtility.SetDirty(decollider);
        }

        private static void ConfigureProfile(
            SerializedProperty profile,
            string profileId,
            CinemachineCamera camera,
            CinemachineTargetGroup targetGroup,
            int priority)
        {
            profile.FindPropertyRelative("_profileId").stringValue = profileId;
            profile.FindPropertyRelative("_camera").objectReferenceValue = camera;
            profile.FindPropertyRelative("_targetGroup").objectReferenceValue = targetGroup;
            profile.FindPropertyRelative("_followAnchor").objectReferenceValue = null;
            profile.FindPropertyRelative("_lookTarget").objectReferenceValue = null;
            profile.FindPropertyRelative("_priority").intValue = priority;
            profile.FindPropertyRelative("_playerWeight").floatValue = 1f;
            profile.FindPropertyRelative("_playerRadius").floatValue = 0.7f;
            profile.FindPropertyRelative("_lookTargetWeight").floatValue = 1f;
            profile.FindPropertyRelative("_lookTargetRadius").floatValue = 0.3f;
        }

        private static void ConfigureSceneBindings(
            PlayerSceneServiceBindings bindings,
            ActivityLogicRegistryBehaviour registry,
            ActivityCameraRigExecutor cameraRig,
            LocalPlayerContext player)
        {
            var serialized = new SerializedObject(bindings);
            serialized.FindProperty("_activityLogicFactory").objectReferenceValue = registry;
            serialized.FindProperty("_activityCameraExecutor").objectReferenceValue = cameraRig;

            var spawn = EnsureSpawnPoint(bindings.transform, player.transform);
            serialized.FindProperty("_networkPlayerSpawnPoint").objectReferenceValue = spawn;
            serialized.FindProperty("_networkPlayerSpawnSpacing").floatValue = 1.5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bindings);
        }

        private static Transform EnsureSpawnPoint(Transform parent, Transform player)
        {
            var existing = parent.Find("NetworkPlayerSpawnPoint");
            var owner = existing != null ? existing.gameObject : new GameObject("NetworkPlayerSpawnPoint");
            if (existing == null)
            {
                owner.transform.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(owner, "Create NetworkPlayerSpawnPoint");
            }
            owner.transform.SetPositionAndRotation(player.position, player.rotation);
            EditorUtility.SetDirty(owner);
            return owner.transform;
        }

        private static void ConfigureNetworkBootstrap(
            Scene scene,
            PlayerSceneServiceBindings bindings,
            LocalPlayerContext standalonePlayer)
        {
            var root = FindOrCreateRoot(scene, "Gameplay_NetworkBootstrap");
            var networkManager = GetOrAdd<NetworkManager>(root);
            var transport = GetOrAdd<UnityTransport>(root);
            var bootstrap = GetOrAdd<FirePlayNetworkBootstrap>(root);
            var handoff = GetOrAdd<StandalonePlayerNetworkHandoff>(root);

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPrefabPath);
            var worldStatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkWorldStatePrefabPath);
            var prefabsList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsListPath);
            if (playerPrefab == null || worldStatePrefab == null || prefabsList == null
                || !worldStatePrefab.TryGetComponent<NetworkObject>(out var worldStateNetworkObject))
            {
                throw new InvalidOperationException(
                    "找不到 PlayerNetworkGameplay、NetworkWorldState 或 DefaultNetworkPrefabs.asset。");
            }

            var config = networkManager.NetworkConfig;
            config.NetworkTransport = transport;
            config.PlayerPrefab = playerPrefab;
            config.ConnectionApproval = true;
            config.TickRate = 30;
            config.ClientConnectionBufferTimeout = 10;
            config.EnableSceneManagement = true;
            config.ForceSamePrefabs = true;
            config.Prefabs.NetworkPrefabsLists.Clear();
            config.Prefabs.NetworkPrefabsLists.Add(prefabsList);
            transport.SetConnectionData("127.0.0.1", 7777, "0.0.0.0");

            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("_networkManager").objectReferenceValue = networkManager;
            serialized.FindProperty("_transport").objectReferenceValue = transport;
            serialized.FindProperty("_worldStatePrefab").objectReferenceValue = worldStateNetworkObject;
            serialized.FindProperty("_autoStart").enumValueIndex = 0; // Manual: keep single-player startup unchanged.
            serialized.FindProperty("_serverAddress").stringValue = "127.0.0.1";
            serialized.FindProperty("_listenAddress").stringValue = "0.0.0.0";
            serialized.FindProperty("_port").intValue = 7777;
            serialized.FindProperty("_allowCommandLineOverrides").boolValue = true;
            serialized.FindProperty("_maximumPlayers").intValue = 4;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var handoffSerialized = new SerializedObject(handoff);
            handoffSerialized.FindProperty("_standalonePlayer").objectReferenceValue = standalonePlayer;
            handoffSerialized.FindProperty("_sceneBindings").objectReferenceValue = bindings;
            handoffSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(networkManager);
            EditorUtility.SetDirty(transport);
            EditorUtility.SetDirty(bootstrap);
            EditorUtility.SetDirty(handoff);
            Debug.Log(
                "[SnowValleySceneBindingBuilder] NetworkManager 已配置为 Manual + 127.0.0.1:7777；" +
                "NetworkWorldState Prefab 已显式接入；可通过 NetworkConnectionForms 或命令行启动。",
                root);
        }
    }
}
