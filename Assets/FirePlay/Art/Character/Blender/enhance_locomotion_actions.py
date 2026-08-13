"""Give the existing downloaded duo a clearer, loop-safe locomotion cycle.

This script edits only the Walk and Run Actions in the saved duo source.  It
does not rebuild meshes, weights, materials, or the armature.  The result is a
more readable light-footed gait: alternating arms/legs, bent knees, a small
hips bob and side-to-side weight shift.  Frame one and the final frame match,
so Unity can loop the clips without a visible snap.

Run from Blender's Scripting workspace, or from the project root:

    E:\\Blender\\blender.exe --background --python \\
        Assets/FirePlay/Art/Character/Blender/enhance_locomotion_actions.py

After it completes, run export_edited_duo_from_blend.py, then in Unity run:

    FirePlay/Character/Build Downloaded Duo Materials
    FirePlay/Character/Build Female Animation Setup
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


def key_rotation(bone: bpy.types.PoseBone, frame: float, x: float, z: float = 0.0) -> None:
    bone.rotation_mode = "XYZ"
    bone.rotation_euler = (x, 0.0, z)
    bone.keyframe_insert(data_path="rotation_euler", frame=frame)


def key_hips(bone: bpy.types.PoseBone, frame: float, lift: float, sway: float) -> None:
    bone.rotation_mode = "XYZ"
    bone.location = (0.0, lift, 0.0)
    bone.rotation_euler = (0.0, 0.0, sway)
    bone.keyframe_insert(data_path="location", frame=frame)
    bone.keyframe_insert(data_path="rotation_euler", frame=frame)


def enhance_cycle(rig: bpy.types.Object, action_name: str, poses: list[tuple[float, float, float, float, float]]) -> None:
    action = bpy.data.actions.get(action_name)
    if action is None:
        raise RuntimeError(f"Missing action {action_name}")

    set_action(rig, action)
    bones = rig.pose.bones
    required = ("hips", "chest", "upper_arm.L", "forearm.L", "upper_arm.R", "forearm.R", "thigh.L", "shin.L", "thigh.R", "shin.R")
    missing = [name for name in required if bones.get(name) is None]
    if missing:
        raise RuntimeError(f"{rig.name} is missing bones: {', '.join(missing)}")

    for frame, arm, thigh, shin, lift in poses:
        # Positive arm/thigh means the left side is leading.  The chest twists
        # slightly against the pelvis so the silhouette reads at game distance.
        key_hips(bones["hips"], frame, lift, thigh * 0.055)
        key_rotation(bones["chest"], frame, 0.0, -thigh * 0.10)
        key_rotation(bones["upper_arm.L"], frame, -arm, thigh * 0.05)
        key_rotation(bones["upper_arm.R"], frame, arm, -thigh * 0.05)
        key_rotation(bones["forearm.L"], frame, -arm * 0.26)
        key_rotation(bones["forearm.R"], frame, arm * 0.26)
        key_rotation(bones["thigh.L"], frame, thigh)
        key_rotation(bones["thigh.R"], frame, -thigh)
        # The back leg bends most while passing under the body.  This is the
        # missing shape that made the old walk feel like two rigid pendulums.
        key_rotation(bones["shin.L"], frame, shin if thigh < 0.0 else -shin * 0.28)
        key_rotation(bones["shin.R"], frame, shin if thigh > 0.0 else -shin * 0.28)

    for curve in [curve for layer in action.layers for strip in layer.strips for bag in strip.channelbags for curve in bag.fcurves]:
        for key in curve.keyframe_points:
            key.interpolation = "BEZIER"


def main() -> None:
    loaded_path = Path(bpy.data.filepath).resolve() if bpy.data.filepath else None
    if loaded_path != BLEND_PATH.resolve():
        bpy.ops.wm.open_mainfile(filepath=str(BLEND_PATH))

    # (frame, arm swing, thigh swing, knee bend, vertical hips offset)
    # First and last poses are intentionally identical for reliable looping.
    walk = [
        (1.0, 0.52, 0.58, 0.30, -0.018),
        (8.0, 0.10, 0.08, 0.50, 0.040),
        (16.0, -0.52, -0.58, 0.30, -0.018),
        (24.0, -0.10, -0.08, 0.50, 0.040),
        (31.0, 0.52, 0.58, 0.30, -0.018),
    ]
    run = [
        (1.0, 0.78, 0.90, 0.48, -0.032),
        (5.0, 0.18, 0.18, 0.74, 0.070),
        (10.0, -0.78, -0.90, 0.48, -0.032),
        (14.0, -0.18, -0.18, 0.74, 0.070),
        (19.0, 0.78, 0.90, 0.48, -0.032),
    ]

    for role in ("Female", "Male"):
        rig = bpy.data.objects.get(f"SnowTraveler_{role}_Rig")
        if rig is None or rig.type != "ARMATURE":
            raise RuntimeError(f"Missing rig for {role}")
        enhance_cycle(rig, f"SnowTraveler_{role}_Walk", walk)
        enhance_cycle(rig, f"SnowTraveler_{role}_Run", run)

        # B-Bone/Cube is a display style from the first rig pass, not a mesh or a
        # runtime requirement.  Octahedral makes the animation rig legible in
        # Blender while keeping the exact same deform bones and skin weights.
        rig.data.display_type = "OCTAHEDRAL"
        for bone in rig.data.bones:
            bone.display_type = "OCTAHEDRAL"

    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    print("LOCOMOTION_ENHANCED roles=Female,Male actions=Walk,Run")
    print("NEXT export_edited_duo_from_blend.py then rebuild Unity materials/controller")


if __name__ == "__main__":
    main()
