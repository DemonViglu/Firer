using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DemonViglu.FirePlay.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DemonViglu.FirePlay.Editor
{
    /// <summary>
    /// Builds the first real character animation hookup from the imported FBX.
    /// The controller uses the meaningful locomotion clips plus the authored
    /// rest, roasting and sit actions from the downloaded character FBX.
    /// </summary>
    public static class FirePlayCharacterAnimationBuilder
    {
        private const string FemaleFbxPath =
            "Assets/FirePlay/Art/Character/Generated/SnowTraveler_Female_Rigged.fbx";
        private const string FemaleVisualPrefabPath =
            "Assets/FirePlay/Art/Character/Generated/Prefabs/SnowTraveler_Female.prefab";
        private const string ControllerPath =
            "Assets/FirePlay/Art/Character/Generated/Controllers/SnowTraveler_Female_Locomotion.controller";
        private const string PlayerPrefabPath =
            "Assets/FirePlay/Runtime/Prefab/Player.prefab";
        private const string PlayerCorePrefabPath =
            "Assets/FirePlay/Runtime/Prefab/PlayerCoreOnly.prefab";

        private static readonly string[] MotionNames =
        {
            "Idle",
            "Walk",
            "Run",
            "JumpStart",
            "JumpLoop",
            "Fall",
            "Land",
            "Fishing",
            "Sit",
            "Marshmallow",
            "Stargaze"
        };

        private static readonly string[] ActivityBoolNames =
        {
            "IsResting",
            "IsMarshmallowRoasting",
            "IsGuitarPlaying",
            "IsFishing"
        };

        private static readonly string[] ActivityTriggerNames =
        {
            "MarshmallowMaterialize",
            "MarshmallowTurn",
            "MarshmallowEat",
            "MarshmallowCancel",
            "RitualOffer",
            "GuitarBegin",
            "GuitarPlay",
            "FishingCast",
            "FishingReel",
            "EmoteWave",
            "EmoteThanks",
            "EmoteWarmth",
            "EmoteSit"
        };

        [MenuItem("FirePlay/Character/Build Female Animation Setup")]
        public static void BuildFemaleAnimationSetup()
        {
            var clips = LoadMotionClips();
            ConfigureClipLooping(clips);
            var controller = BuildController(clips);
            InstallFemaleVisualAndAnimator(controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "FirePlay Character",
                "Female Animator Controller 已生成，并已挂到 Player 与 PlayerCoreOnly。\n\n" +
                "当前接入：Idle / Walk / Run / Jump / Fall / Land / Fishing。\n" +
                "现已接入：坐下 / 烤棉花 / 观星。",
                "好的");
        }

        private static Dictionary<string, AnimationClip> LoadMotionClips()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FemaleFbxPath) == null)
            {
                throw new InvalidOperationException(
                    $"找不到 Female FBX：{FemaleFbxPath}。请确认文件位于项目 Assets 内并完成导入。 ");
            }

            var clips = AssetDatabase.LoadAllAssetsAtPath(FemaleFbxPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.Contains("__preview__", StringComparison.OrdinalIgnoreCase))
                .Select(clip => (Key: GetMotionKey(clip.name), Clip: clip))
                .Where(item => item.Key != null)
                .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Clip, StringComparer.OrdinalIgnoreCase);

            var missing = MotionNames.Where(name => !clips.ContainsKey(name)).ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException(
                    "Female FBX 没有找到这些有效动作：" + string.Join(", ", missing) +
                    "。请先在 Project 窗口选中 FBX，确认 Animation Import 已启用。 ");
            }

            return clips;
        }

        private static string GetMotionKey(string clipName)
        {
            if (string.IsNullOrWhiteSpace(clipName))
                return null;

            var tail = clipName.Split('|').Last();
            foreach (var motionName in MotionNames)
            {
                if (string.Equals(tail, motionName, StringComparison.OrdinalIgnoreCase) ||
                    tail.EndsWith("_" + motionName, StringComparison.OrdinalIgnoreCase) ||
                    tail.EndsWith("-" + motionName, StringComparison.OrdinalIgnoreCase))
                    return motionName;
            }

            return null;
        }

        private static AnimatorController BuildController(IReadOnlyDictionary<string, AnimationClip> clips)
        {
            EnsureAssetFolder("Assets/FirePlay/Art/Character/Generated");
            EnsureAssetFolder("Assets/FirePlay/Art/Character/Generated/Controllers");

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AddParameters(controller);

            var stateMachine = controller.layers[0].stateMachine;
            var idle = AddState(stateMachine, "Idle", clips["Idle"], new Vector3(280f, 80f));
            var walk = AddState(stateMachine, "Walk", clips["Walk"], new Vector3(520f, 80f));
            var run = AddState(stateMachine, "Run", clips["Run"], new Vector3(760f, 80f));
            var jumpStart = AddState(stateMachine, "JumpStart", clips["JumpStart"], new Vector3(520f, 300f));
            var jumpLoop = AddState(stateMachine, "JumpLoop", clips["JumpLoop"], new Vector3(760f, 300f));
            var fall = AddState(stateMachine, "Fall", clips["Fall"], new Vector3(1000f, 300f));
            var land = AddState(stateMachine, "Land", clips["Land"], new Vector3(1240f, 300f));
            var fishing = AddState(stateMachine, "Fishing", clips["Fishing"], new Vector3(1000f, 80f));
            var sit = AddState(stateMachine, "Sit", clips["Sit"], new Vector3(280f, 500f));
            var marshmallow = AddState(stateMachine, "Marshmallow", clips["Marshmallow"], new Vector3(520f, 500f));
            var stargaze = AddState(stateMachine, "Stargaze", clips["Stargaze"], new Vector3(760f, 500f));
            stateMachine.defaultState = idle;

            AddFloatTransition(idle, walk, AnimatorConditionMode.Greater, 0.1f, "MoveSpeed");
            AddFloatTransition(walk, idle, AnimatorConditionMode.Less, 0.1f, "MoveSpeed");
            AddBoolTransition(walk, run, "IsSprinting", true);
            AddBoolTransition(run, walk, "IsSprinting", false);

            var jumpTransition = stateMachine.AddAnyStateTransition(jumpStart);
            jumpTransition.hasExitTime = false;
            jumpTransition.duration = 0.08f;
            jumpTransition.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
            jumpTransition.AddCondition(AnimatorConditionMode.Greater, 0.05f, "VerticalVelocity");

            var fallTransition = stateMachine.AddAnyStateTransition(fall);
            fallTransition.hasExitTime = false;
            fallTransition.duration = 0.08f;
            fallTransition.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
            fallTransition.AddCondition(AnimatorConditionMode.Less, 0.05f, "VerticalVelocity");

            AddExitTimeTransition(jumpStart, jumpLoop, 0.8f, 0.08f);
            AddFloatTransition(jumpLoop, fall, AnimatorConditionMode.Less, 0.05f, "VerticalVelocity");
            AddBoolTransition(fall, land, "IsGrounded", true);
            AddExitTimeTransition(land, idle, 0.8f, 0.08f);

            var fishingTransition = stateMachine.AddAnyStateTransition(fishing);
            fishingTransition.hasExitTime = false;
            fishingTransition.duration = 0.12f;
            fishingTransition.AddCondition(AnimatorConditionMode.If, 0f, "IsFishing");
            AddBoolTransition(fishing, idle, "IsFishing", false);

            var sitTransition = stateMachine.AddAnyStateTransition(sit);
            sitTransition.hasExitTime = false;
            sitTransition.duration = 0.12f;
            sitTransition.canTransitionToSelf = false;
            sitTransition.AddCondition(AnimatorConditionMode.If, 0f, "EmoteSit");
            AddExitTimeTransition(sit, idle, 0.92f, 0.12f);

            var roastingTransition = stateMachine.AddAnyStateTransition(marshmallow);
            roastingTransition.hasExitTime = false;
            roastingTransition.duration = 0.16f;
            roastingTransition.canTransitionToSelf = false;
            roastingTransition.AddCondition(AnimatorConditionMode.If, 0f, "IsMarshmallowRoasting");
            AddBoolTransition(marshmallow, idle, "IsMarshmallowRoasting", false);

            var stargazeTransition = stateMachine.AddAnyStateTransition(stargaze);
            stargazeTransition.hasExitTime = false;
            stargazeTransition.duration = 0.2f;
            stargazeTransition.canTransitionToSelf = false;
            stargazeTransition.AddCondition(AnimatorConditionMode.If, 0f, "IsResting");
            AddBoolTransition(stargaze, idle, "IsResting", false);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void ConfigureClipLooping(IReadOnlyDictionary<string, AnimationClip> clips)
        {
            foreach (var pair in clips)
            {
                var settings = AnimationUtility.GetAnimationClipSettings(pair.Value);
                settings.loopTime = pair.Key is "Idle" or "Walk" or "Run" or "JumpLoop" or "Fishing" or "Marshmallow" or "Stargaze";
                settings.loopBlend = settings.loopTime;
                AnimationUtility.SetAnimationClipSettings(pair.Value, settings);
            }
        }

        private static AnimatorState AddState(
            AnimatorStateMachine stateMachine,
            string name,
            AnimationClip clip,
            Vector3 position)
        {
            var state = stateMachine.AddState(name, position);
            state.motion = clip;
            state.iKOnFeet = false;
            state.writeDefaultValues = true;
            return state;
        }

        private static void AddParameters(AnimatorController controller)
        {
            AddParameter(controller, "MoveSpeed", AnimatorControllerParameterType.Float);
            AddParameter(controller, "IsSprinting", AnimatorControllerParameterType.Bool);
            AddParameter(controller, "IsGrounded", AnimatorControllerParameterType.Bool, true);
            AddParameter(controller, "IsInWater", AnimatorControllerParameterType.Bool);
            AddParameter(controller, "VerticalVelocity", AnimatorControllerParameterType.Float);

            foreach (var name in ActivityBoolNames)
                AddParameter(controller, name, AnimatorControllerParameterType.Bool);

            foreach (var name in ActivityTriggerNames)
                AddParameter(controller, name, AnimatorControllerParameterType.Trigger);
        }

        private static void AddParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type,
            bool defaultBool = false)
        {
            var parameter = new AnimatorControllerParameter
            {
                name = name,
                type = type,
                defaultBool = defaultBool
            };
            controller.AddParameter(parameter);
        }

        private static void AddFloatTransition(
            AnimatorState from,
            AnimatorState to,
            AnimatorConditionMode mode,
            float threshold,
            string parameter)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.12f;
            transition.AddCondition(mode, threshold, parameter);
        }

        private static void AddBoolTransition(
            AnimatorState from,
            AnimatorState to,
            string parameter,
            bool value)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.12f;
            transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
        }

        private static void AddExitTimeTransition(
            AnimatorState from,
            AnimatorState to,
            float exitTime,
            float duration)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.duration = duration;
        }

        private static void InstallFemaleVisualAndAnimator(AnimatorController controller)
        {
            var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FemaleVisualPrefabPath);
            if (visualPrefab == null)
                throw new InvalidOperationException($"找不到 Female 角色 Prefab：{FemaleVisualPrefabPath}。 ");

            var avatar = AssetDatabase.LoadAllAssetsAtPath(FemaleFbxPath)
                .OfType<Avatar>()
                .FirstOrDefault(candidate => candidate.isValid);
            if (avatar == null)
                throw new InvalidOperationException(
                    "Female FBX 没有生成有效 Avatar。请在 FBX 的 Rig 面板点击 Apply，并确认 Animation Type 为 Humanoid。 ");

            InstallVisualOnPlayerPrefab(PlayerPrefabPath, visualPrefab, avatar, controller);
            InstallVisualOnPlayerPrefab(PlayerCorePrefabPath, visualPrefab, avatar, controller);
        }

        private static void InstallVisualOnPlayerPrefab(
            string prefabPath,
            GameObject visualPrefab,
            Avatar avatar,
            RuntimeAnimatorController controller)
        {
            var playerRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var oldVisual = playerRoot.transform.Find("SnowTravelerVisual");
                if (oldVisual != null)
                    UnityEngine.Object.DestroyImmediate(oldVisual.gameObject);

                var visual = PrefabUtility.InstantiatePrefab(visualPrefab, playerRoot.transform) as GameObject;
                if (visual == null)
                    throw new InvalidOperationException($"无法把 Female Visual Prefab 实例化到 {prefabPath}。 ");

                visual.name = "SnowTravelerVisual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                var animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                AssignAnimator(playerRoot.GetComponent<PlayerAnimationController>(), animator);
                AssignAnimator(playerRoot.GetComponent<PlayerLocomotionAnimationBridge>(), animator);
                AssignObjectReference(playerRoot.GetComponent<PlayerMovement>(), "_visualTransform", visual.transform);

                PrefabUtility.SaveAsPrefabAsset(playerRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerRoot);
            }
        }

        private static void AssignAnimator(UnityEngine.Object component, Animator animator)
        {
            AssignObjectReference(component, "_animator", animator);
        }

        private static void AssignObjectReference(
            UnityEngine.Object component,
            string propertyName,
            UnityEngine.Object value)
        {
            if (component == null)
                return;

            var serializedObject = new SerializedObject(component);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
                return;

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            var parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            var folderName = Path.GetFileName(folderPath);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folderName))
                throw new InvalidOperationException($"无法创建资源目录：{folderPath}");

            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
