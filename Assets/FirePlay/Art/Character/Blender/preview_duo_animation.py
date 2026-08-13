"""Prepare the duo source for direct animation preview in Blender.

The source file keeps NLA strips muted so the 11 actions do not overlap and
blend into an unusable pose.  This script assigns one normal Action to each
rig, mutes the NLA strips, sets the scene range, and saves the source.  After
opening the .blend in Blender, select a rig and press Play.

Select one character's mesh or armature before running the script to preview
that character only.  If nothing is selected, PREVIEW_ROLE is used.

Change PREVIEW_ACTION or PREVIEW_ROLE to preview another action/character,
then run this script again:
    Idle, Walk, Run, JumpStart, JumpLoop, Fall, Land, Sit, Fishing,
    Marshmallow, Stargaze

Run from Blender's Scripting workspace or with:
    E:\\Blender\\blender.exe --background --python \\
        Assets/FirePlay/Art/Character/Blender/preview_duo_animation.py
"""

from __future__ import annotations

from pathlib import Path

import bpy


PROJECT_ROOT = Path(__file__).resolve().parents[5]
BLEND_PATH = (
    PROJECT_ROOT
    / "Assets"
    / "FirePlay"
    / "Art"
    / "Character"
    / "Generated"
    / "SnowTraveler_Duo_Rigged.blend"
)
PREVIEW_ACTION = "Walk"
PREVIEW_ROLE = "Female"


def role_from_selection() -> str | None:
    selected = set(bpy.context.selected_objects)
    roles = []
    for role in ("Male", "Female"):
        collection = bpy.data.collections.get(f"SnowTraveler_{role}")
        if collection is not None and any(obj in selected for obj in collection.objects):
            roles.append(role)
    if len(roles) > 1:
        raise RuntimeError("Select only one character to preview")
    return roles[0] if roles else None


def set_preview_action(role: str, enabled: bool) -> tuple[float, float] | None:
    rig = bpy.data.objects.get(f"SnowTraveler_{role}_Rig")
    if rig is None or rig.type != "ARMATURE":
        raise RuntimeError(f"Missing SnowTraveler_{role}_Rig")

    if rig.animation_data is None:
        rig.animation_data_create()
    rig.animation_data.action = None
    for track in rig.animation_data.nla_tracks:
        track.mute = True
        for strip in track.strips:
            strip.mute = True
    if not enabled:
        return None

    action = bpy.data.actions.get(f"SnowTraveler_{role}_{PREVIEW_ACTION}")
    if action is None:
        raise RuntimeError(f"Missing action SnowTraveler_{role}_{PREVIEW_ACTION}")

    rig.animation_data.action = action
    if action.slots:
        rig.animation_data.action_slot = action.slots[0]
    return action.frame_start, action.frame_end


def main() -> None:
    loaded_path = Path(bpy.data.filepath).resolve() if bpy.data.filepath else None
    if loaded_path != BLEND_PATH.resolve():
        bpy.ops.wm.open_mainfile(filepath=str(BLEND_PATH))

    role = role_from_selection() or PREVIEW_ROLE
    if role not in {"Male", "Female"}:
        raise RuntimeError("PREVIEW_ROLE must be Male or Female")
    preview_range = set_preview_action(role, True)
    set_preview_action("Female" if role == "Male" else "Male", False)
    assert preview_range is not None
    bpy.context.scene.frame_start = int(preview_range[0])
    bpy.context.scene.frame_end = int(preview_range[1])
    bpy.context.scene.frame_set(bpy.context.scene.frame_start)

    bpy.ops.object.select_all(action="DESELECT")
    preview_rig = bpy.data.objects[f"SnowTraveler_{role}_Rig"]
    preview_rig.hide_set(False)
    preview_rig.select_set(True)
    bpy.context.view_layer.objects.active = preview_rig
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    print(
        f"DUO_PREVIEW_READY role={role} action={PREVIEW_ACTION} "
        f"frames={bpy.context.scene.frame_start}-{bpy.context.scene.frame_end}"
    )


if __name__ == "__main__":
    main()
