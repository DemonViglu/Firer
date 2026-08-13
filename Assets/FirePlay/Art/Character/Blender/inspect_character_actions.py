"""Report whether each character Action contains usable keyframe content."""

from __future__ import annotations

from pathlib import Path

import bpy


PROJECT_ROOT = Path(__file__).resolve().parents[5]
BLEND_PATH = PROJECT_ROOT / "Assets" / "FirePlay" / "Art" / "Character" / "Generated" / "SnowTraveler_Duo_Rigged.blend"
FBX_PATHS = {
    "Male": PROJECT_ROOT / "Assets" / "FirePlay" / "Art" / "Character" / "Generated" / "SnowTraveler_Male_Rigged.fbx",
    "Female": PROJECT_ROOT / "Assets" / "FirePlay" / "Art" / "Character" / "Generated" / "SnowTraveler_Female_Rigged.fbx",
}


def action_fcurves(action):
    if hasattr(action, "fcurves"):
        return list(action.fcurves)
    curves = []
    for layer in action.layers:
        for strip in layer.strips:
            for channelbag in strip.channelbags:
                curves.extend(channelbag.fcurves)
    return curves


def action_stats(action):
    curves = action_fcurves(action)
    keyframes = [point for curve in curves for point in curve.keyframe_points]
    varying_curves = sum(
        1
        for curve in curves
        if curve.keyframe_points
        and max(point.co[1] for point in curve.keyframe_points)
        - min(point.co[1] for point in curve.keyframe_points)
        > 0.0001
    )
    return len(curves), len(keyframes), varying_curves


def action_range(action):
    points = [point for curve in action_fcurves(action) for point in curve.keyframe_points]
    if not points:
        return 1.0, 1.0
    return min(point.co[0] for point in points), max(point.co[0] for point in points)


def pose_signature(rig):
    values = []
    for bone in rig.pose.bones:
        values.extend(bone.location)
        values.extend(bone.rotation_euler)
        values.extend(bone.rotation_quaternion)
        values.extend(bone.scale)
    return values


def pose_delta(first, second):
    return max((abs(a - b) for a, b in zip(first, second)), default=0.0)


def source_report():
    bpy.ops.wm.open_mainfile(filepath=str(BLEND_PATH))
    print("SOURCE_ACTION_REPORT")
    for role in ("Male", "Female"):
        rig = bpy.data.objects[f"SnowTraveler_{role}_Rig"]
        for action_name in sorted(name for name in bpy.data.actions.keys() if name.startswith(f"SnowTraveler_{role}_")):
            action = bpy.data.actions[action_name]
            curves, keys, varying = action_stats(action)
            for track in rig.animation_data.nla_tracks:
                track.mute = True
            rig.animation_data.action = action
            if action.slots:
                rig.animation_data.action_slot = action.slots[0]
            start, end = action_range(action)
            samples = [start, (start + end) * 0.5, end]
            signatures = []
            for frame in samples:
                bpy.context.scene.frame_set(round(frame))
                bpy.context.view_layer.update()
                signatures.append(pose_signature(rig))
            motion = max(pose_delta(signatures[0], signature) for signature in signatures[1:])
            print(f"SOURCE {role} {action.name} curves={curves} keys={keys} varying_curves={varying} pose_delta={motion:.6f}")


def imported_report(role, path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    result = bpy.ops.import_scene.fbx(filepath=str(path))
    armatures = [obj for obj in bpy.data.objects if obj.type == "ARMATURE"]
    print(f"FBX_IMPORT {role} result={result} armatures={len(armatures)} actions={len(bpy.data.actions)}")
    if not armatures:
        return
    rig = armatures[0]
    for action in sorted(bpy.data.actions, key=lambda item: item.name):
        curves, keys, varying = action_stats(action)
        if rig.animation_data is None:
            rig.animation_data_create()
        rig.animation_data.action = action
        if action.slots:
            rig.animation_data.action_slot = action.slots[0]
        start, end = action_range(action)
        samples = [start, (start + end) * 0.5, end]
        signatures = []
        for frame in samples:
            bpy.context.scene.frame_set(round(frame))
            bpy.context.view_layer.update()
            signatures.append(pose_signature(rig))
        motion = max(pose_delta(signatures[0], signature) for signature in signatures[1:])
        print(f"FBX {role} {action.name} curves={curves} keys={keys} varying_curves={varying} pose_delta={motion:.6f}")


source_report()
for role, path in FBX_PATHS.items():
    imported_report(role, path)
