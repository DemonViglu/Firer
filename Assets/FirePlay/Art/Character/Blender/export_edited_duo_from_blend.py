"""Export manually edited characters from the saved duo .blend.

Unlike build_downloaded_character_duo.py, this script never imports the source
GLBs and never clears or rebuilds the scene.  It treats the saved .blend as the
art source of truth and only writes the Unity delivery files beside it.

Run after saving SnowTraveler_Duo_Rigged.blend:
    E:\\Blender\\blender.exe --background --python \
        Assets/FirePlay/Art/Character/Blender/export_edited_duo_from_blend.py

For manual Blender FBX export, run prepare_single_character_blends.py first
and open SnowTraveler_Male_Single.blend or SnowTraveler_Female_Single.blend.
Those files contain only one character's Actions, so ``All Actions`` is safe.

When this script is run from Blender's Scripting workspace while the duo
source is open, selecting one character's mesh or armature exports only that
character.  With no character selected it exports both, preserving the batch
workflow used from PowerShell.
"""

from __future__ import annotations

import math
from pathlib import Path

import bpy


PROJECT_ROOT = Path(__file__).resolve().parents[5]
OUTPUT_DIR = PROJECT_ROOT / "Assets" / "FirePlay" / "Art" / "Character" / "Generated"
BLEND_PATH = OUTPUT_DIR / "SnowTraveler_Duo_Rigged.blend"


def classify_image(node):
    """Classify an image by its current shader connection, not its old name."""
    targets = []
    for output in node.outputs:
        targets.extend(link.to_socket.name.lower() for link in output.links)
        targets.extend(link.to_node.name.lower() for link in output.links)
    joined = " ".join(targets)
    if "normal" in joined:
        return "Normal"
    if "separate" in joined or "roughness" in joined or "metallic" in joined:
        return "MetallicRoughness"
    if "base color" in joined or "albedo" in joined:
        return "BaseColor"
    return None


def extract_current_images(role, collection):
    """Write the current packed/unpacked image nodes beside the FBX."""
    extracted = {}
    for obj in collection.objects:
        if obj.type != "MESH":
            continue
        for material in obj.data.materials:
            if not material or not material.use_nodes:
                continue
            for node in material.node_tree.nodes:
                if node.type != "TEX_IMAGE" or node.image is None:
                    continue
                kind = classify_image(node)
                if kind is None:
                    print(f"WARNING_UNCLASSIFIED_IMAGE {role} {node.image.name}")
                    continue
                image = node.image
                key = (kind, image.as_pointer())
                target = OUTPUT_DIR / f"SnowTraveler_{role}_{kind}.png"
                if key not in extracted:
                    image.filepath = str(target)
                    image.file_format = "PNG"
                    image.save()
                    extracted[key] = target
                    print(f"EXTRACTED_TEXTURE {role} {kind} {target}")
                else:
                    image.filepath = str(extracted[key])
    return extracted


def prepare_role_nla_for_export(rig, role):
    """Temporarily enable only this role's NLA strips.

    The saved duo blend keeps strips muted so both characters open in a clean
    preview pose.  ``All Actions`` is intentionally disabled here; Blender's
    NLA exporter receives only the current role's unmuted strips instead.
    """
    if not rig.animation_data:
        return []

    prefix = f"SnowTraveler_{role}_"
    original = []
    for track in rig.animation_data.nla_tracks:
        original.append((track, track.mute))
        role_strip_found = False
        for strip in track.strips:
            original.append((strip, strip.mute))
            is_role_strip = (
                strip.action is not None
                and strip.action.name.startswith(prefix)
            )
            strip.mute = not is_role_strip
            role_strip_found = role_strip_found or is_role_strip
        track.mute = not role_strip_found
    return original


def restore_nla_mutes(original):
    for item, mute in original:
        item.mute = mute


def make_objects_exportable(collection):
    """Temporarily reveal objects; hidden armatures are skipped by FBX export."""
    original = []
    for obj in collection.objects:
        original.append((obj, obj.hide_get(), obj.hide_viewport, obj.hide_render))
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.hide_render = False
    return original


def restore_object_visibility(original):
    for obj, hide_get, hide_viewport, hide_render in original:
        if obj is None:
            continue
        obj.hide_set(hide_get)
        obj.hide_viewport = hide_viewport
        obj.hide_render = hide_render


def snapshot_selection():
    return (
        [(obj, obj.select_get()) for obj in bpy.context.scene.objects],
        bpy.context.view_layer.objects.active,
    )


def restore_selection(original):
    selected, active = original
    for obj, selected_state in selected:
        if obj is not None:
            obj.select_set(selected_state)
    bpy.context.view_layer.objects.active = active


def selected_roles():
    """Return one selected role, or both roles when nothing is selected."""
    roles = []
    selected = set(bpy.context.selected_objects)
    for role in ("Male", "Female"):
        collection = bpy.data.collections.get(f"SnowTraveler_{role}")
        if collection is not None and any(obj in selected for obj in collection.objects):
            roles.append(role)
    if len(roles) > 1:
        raise RuntimeError(
            "Select only one character (Male or Female) before exporting; "
            "selecting both would create a mixed-character export."
        )
    return roles or ["Male", "Female"]


def export_role(role):
    collection = bpy.data.collections.get(f"SnowTraveler_{role}")
    rig = bpy.data.objects.get(f"SnowTraveler_{role}_Rig")
    if collection is None or rig is None or rig.type != "ARMATURE":
        raise RuntimeError(f"Saved blend is missing SnowTraveler_{role} collection or rig")

    extract_current_images(role, collection)
    bpy.ops.object.mode_set(mode="OBJECT") if bpy.context.object and bpy.context.object.mode != "OBJECT" else None
    bpy.ops.object.select_all(action="DESELECT")
    original_visibility = make_objects_exportable(collection)
    exportable = []
    for obj in collection.objects:
        if obj.type in {"ARMATURE", "MESH"}:
            obj.select_set(True)
            exportable.append(obj)
    if not exportable:
        raise RuntimeError(f"No mesh/armature objects in SnowTraveler_{role}")

    bpy.context.view_layer.objects.active = rig
    action_count = sum(
        1
        for action in bpy.data.actions
        if action.name.startswith(f"SnowTraveler_{role}_")
    )
    if action_count == 0:
        raise RuntimeError(f"No actions found for SnowTraveler_{role}")

    original_action = rig.animation_data.action if rig.animation_data else None
    if rig.animation_data:
        rig.animation_data.action = None
    original_nla_mutes = prepare_role_nla_for_export(rig, role)
    path = OUTPUT_DIR / f"SnowTraveler_{role}_Rigged.fbx"
    try:
        bpy.ops.export_scene.fbx(
            filepath=str(path),
            use_selection=True,
            object_types={"ARMATURE", "MESH"},
            apply_unit_scale=True,
            add_leaf_bones=False,
            use_armature_deform_only=True,
            bake_anim=True,
            bake_anim_use_all_actions=False,
            bake_anim_use_nla_strips=True,
            bake_anim_use_all_bones=True,
            primary_bone_axis="Y",
            secondary_bone_axis="X",
            path_mode="RELATIVE",
        )
    finally:
        restore_nla_mutes(original_nla_mutes)
        restore_object_visibility(original_visibility)
        if rig.animation_data:
            rig.animation_data.action = original_action
    print(f"EXPORTED_EDITED {role} {path}")


def main():
    if not BLEND_PATH.exists():
        raise RuntimeError(f"Missing saved Blender source: {BLEND_PATH}")
    loaded_path = Path(bpy.data.filepath).resolve() if bpy.data.filepath else None
    if loaded_path != BLEND_PATH.resolve():
        bpy.ops.wm.open_mainfile(filepath=str(BLEND_PATH))
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    original_selection = snapshot_selection()
    roles = selected_roles()
    try:
        for role in roles:
            export_role(role)
    finally:
        restore_selection(original_selection)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    print(f"EDITED_DUO_EXPORT_DONE blend={BLEND_PATH} roles={','.join(roles)}")
    print("EDITED_DUO_EXPORT_NEXT Unity: FirePlay/Character/Build Downloaded Duo Materials")


if __name__ == "__main__":
    main()
