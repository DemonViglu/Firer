"""Author the three missing expressive actions in the saved duo source.

This keeps the downloaded mesh, materials, armature hierarchy and existing
locomotion clips intact.  It only keys the already-present Sit, Marshmallow
and Stargaze Actions for both characters, then saves the existing .blend.

Run from Blender's Scripting workspace, or from the project root:

    E:\\Blender\\blender.exe --background --python \\
        Assets/FirePlay/Art/Character/Blender/author_activity_actions.py

Then export_edited_duo_from_blend.py and rebuild Unity's character setup.
"""

from __future__ import annotations

from pathlib import Path

import bpy


PROJECT_ROOT = Path(__file__).resolve().parents[5]
BLEND_PATH = PROJECT_ROOT / "Assets" / "FirePlay" / "Art" / "Character" / "Generated" / "SnowTraveler_Duo_Rigged.blend"


def set_action(rig: bpy.types.Object, action: bpy.types.Action) -> None:
    if rig.animation_data is None:
        rig.animation_data_create()
    rig.animation_data.action = action
    if action.slots:
        rig.animation_data.action_slot = action.slots[0]
    for track in rig.animation_data.nla_tracks:
        track.mute = True
        for strip in track.strips:
            strip.mute = True


def clear_existing_keys(action: bpy.types.Action) -> None:
    """Discard the old static placeholder keys before authoring a new clip."""
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    while curve.keyframe_points:
                        curve.keyframe_points.remove(curve.keyframe_points[0])


def sync_nla_range(rig: bpy.types.Object, action: bpy.types.Action, start: float, end: float) -> None:
    """Keep NLA-only FBX export aligned with the newly authored Action range."""
    if rig.animation_data is None:
        return
    for track in rig.animation_data.nla_tracks:
        for strip in track.strips:
            if strip.action != action:
                continue
            strip.action_frame_start = start
            strip.action_frame_end = end
            strip.frame_start = start
            strip.frame_end = end


def key_pose(
    bones: bpy.types.PoseBones,
    frame: float,
    *,
    hip_drop: float = 0.0,
    hip_tilt: float = 0.0,
    spine_x: float = 0.0,
    chest_x: float = 0.0,
    neck_x: float = 0.0,
    head_x: float = 0.0,
    arm_l: float = 0.0,
    arm_r: float = 0.0,
    forearm_l: float = 0.0,
    forearm_r: float = 0.0,
    thigh_l: float = 0.0,
    thigh_r: float = 0.0,
    shin_l: float = 0.0,
    shin_r: float = 0.0,
) -> None:
    """Key a deliberately small pose set on top of the existing humanoid rig."""
    rotations = {
        "spine": (spine_x, 0.0, 0.0),
        "chest": (chest_x, 0.0, 0.0),
        "neck": (neck_x, 0.0, 0.0),
        "head": (head_x, 0.0, 0.0),
        "upper_arm.L": (arm_l, 0.0, 0.0),
        "upper_arm.R": (arm_r, 0.0, 0.0),
        "forearm.L": (forearm_l, 0.0, 0.0),
        "forearm.R": (forearm_r, 0.0, 0.0),
        "thigh.L": (thigh_l, 0.0, 0.0),
        "thigh.R": (thigh_r, 0.0, 0.0),
        "shin.L": (shin_l, 0.0, 0.0),
        "shin.R": (shin_r, 0.0, 0.0),
    }
    for name, value in rotations.items():
        bone = bones.get(name)
        if bone is None:
            raise RuntimeError(f"Missing required bone: {name}")
        bone.rotation_mode = "XYZ"
        bone.rotation_euler = value
        bone.keyframe_insert(data_path="rotation_euler", frame=frame)

    hips = bones.get("hips")
    if hips is None:
        raise RuntimeError("Missing required bone: hips")
    hips.rotation_mode = "XYZ"
    hips.location = (0.0, hip_drop, 0.0)
    hips.rotation_euler = (0.0, 0.0, hip_tilt)
    hips.keyframe_insert(data_path="location", frame=frame)
    hips.keyframe_insert(data_path="rotation_euler", frame=frame)


def smooth_action(rig: bpy.types.Object, action: bpy.types.Action, start: float, end: float, looping: bool) -> None:
    action.frame_start = start
    action.frame_end = end
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    for key in curve.keyframe_points:
                        key.interpolation = "BEZIER"
    # A custom property is harmless in Blender and documents the intended
    # Unity import setting next to the authored Action.
    action["fireplay_looping"] = looping
    sync_nla_range(rig, action, start, end)


def author_sit(rig: bpy.types.Object, action: bpy.types.Action) -> None:
    set_action(rig, action)
    clear_existing_keys(action)
    bones = rig.pose.bones
    key_pose(bones, 1.0)
    key_pose(
        bones,
        13.0,
        hip_drop=-0.18,
        spine_x=0.10,
        chest_x=0.12,
        arm_l=-0.34,
        arm_r=0.34,
        forearm_l=-0.22,
        forearm_r=0.22,
        thigh_l=-1.18,
        thigh_r=-1.18,
        shin_l=1.30,
        shin_r=1.30,
    )
    key_pose(
        bones,
        24.0,
        hip_drop=-0.20,
        spine_x=0.12,
        chest_x=0.14,
        arm_l=-0.38,
        arm_r=0.38,
        forearm_l=-0.25,
        forearm_r=0.25,
        thigh_l=-1.24,
        thigh_r=-1.24,
        shin_l=1.35,
        shin_r=1.35,
    )
    smooth_action(rig, action, 1.0, 24.0, looping=False)


def author_marshmallow(rig: bpy.types.Object, action: bpy.types.Action) -> None:
    set_action(rig, action)
    clear_existing_keys(action)
    bones = rig.pose.bones
    # The hands sit together in front of the character, as if holding the
    # existing visual skewer; a restrained sway keeps the loop from freezing.
    poses = [
        (1.0, -0.045, 0.16, -0.52, 0.52, -0.48, 0.48, -0.20),
        (20.0, -0.030, 0.20, -0.58, 0.46, -0.54, 0.42, 0.18),
        (40.0, -0.045, 0.16, -0.52, 0.52, -0.48, 0.48, -0.20),
    ]
    for frame, hip_drop, chest, arm_l, arm_r, forearm_l, forearm_r, tilt in poses:
        key_pose(
            bones,
            frame,
            hip_drop=hip_drop,
            hip_tilt=tilt * 0.03,
            spine_x=0.06,
            chest_x=chest,
            neck_x=-0.05,
            head_x=-0.03,
            arm_l=arm_l,
            arm_r=arm_r,
            forearm_l=forearm_l,
            forearm_r=forearm_r,
            thigh_l=0.06,
            thigh_r=-0.06,
        )
    smooth_action(rig, action, 1.0, 40.0, looping=True)


def author_stargaze(rig: bpy.types.Object, action: bpy.types.Action) -> None:
    set_action(rig, action)
    clear_existing_keys(action)
    bones = rig.pose.bones
    # A seated, upward-looking pose with a two-beat breathing sway.  It is
    # intentionally gentle because it can play for an extended Rest session.
    poses = [
        (1.0, -0.20, 0.15, -0.08, 0.18, -0.42, 0.42, -0.32),
        (30.0, -0.175, 0.18, -0.10, 0.21, -0.40, 0.40, -0.34),
        (60.0, -0.20, 0.15, -0.08, 0.18, -0.42, 0.42, -0.32),
    ]
    for frame, hip_drop, chest, neck, head, arm_l, arm_r, tilt in poses:
        key_pose(
            bones,
            frame,
            hip_drop=hip_drop,
            hip_tilt=tilt * 0.035,
            spine_x=0.10,
            chest_x=chest,
            neck_x=neck,
            head_x=head,
            arm_l=arm_l,
            arm_r=arm_r,
            forearm_l=-0.22,
            forearm_r=0.22,
            thigh_l=-1.24,
            thigh_r=-1.24,
            shin_l=1.35,
            shin_r=1.35,
        )
    smooth_action(rig, action, 1.0, 60.0, looping=True)


def main() -> None:
    loaded_path = Path(bpy.data.filepath).resolve() if bpy.data.filepath else None
    if loaded_path != BLEND_PATH.resolve():
        bpy.ops.wm.open_mainfile(filepath=str(BLEND_PATH))

    authors = {
        "Sit": author_sit,
        "Marshmallow": author_marshmallow,
        "Stargaze": author_stargaze,
    }
    for role in ("Female", "Male"):
        rig = bpy.data.objects.get(f"SnowTraveler_{role}_Rig")
        if rig is None or rig.type != "ARMATURE":
            raise RuntimeError(f"Missing rig for {role}")
        for name, author in authors.items():
            action = bpy.data.actions.get(f"SnowTraveler_{role}_{name}")
            if action is None:
                raise RuntimeError(f"Missing action for {role}: {name}")
            author(rig, action)

    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    print("ACTIVITY_ACTIONS_AUTHORED roles=Female,Male actions=Sit,Marshmallow,Stargaze")
    print("NEXT export_edited_duo_from_blend.py then rebuild Unity materials/controller")


if __name__ == "__main__":
    main()
