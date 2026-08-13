"""Create one Blender source file per character from the saved duo source.

Blender stores Actions globally in a .blend file.  Selecting one collection is
therefore not enough to make the FBX exporter see only that character's
animations when ``All Actions`` is enabled.  This script creates two clean
single-character source files, each containing only its own collection and
Actions.

Run after saving SnowTraveler_Duo_Rigged.blend:
    E:\\Blender\\blender.exe --background --python \\
        Assets/FirePlay/Art/Character/Blender/prepare_single_character_blends.py

The duo source is reopened after each output and is never saved over.
"""

from __future__ import annotations

from pathlib import Path

import bpy


PROJECT_ROOT = Path(__file__).resolve().parents[5]
OUTPUT_DIR = PROJECT_ROOT / "Assets" / "FirePlay" / "Art" / "Character" / "Generated"
BLEND_PATH = OUTPUT_DIR / "SnowTraveler_Duo_Rigged.blend"


def remove_other_character(role: str) -> None:
    """Leave only one role collection and that role's action datablocks."""
    other_role = "Female" if role == "Male" else "Male"
    other_collection = bpy.data.collections.get(f"SnowTraveler_{other_role}")
    if other_collection is not None:
        for obj in list(other_collection.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.collections.remove(other_collection)

    prefix = f"SnowTraveler_{role}_"
    for action in list(bpy.data.actions):
        if not action.name.startswith(prefix):
            bpy.data.actions.remove(action)


def build_single_character_blend(role: str) -> Path:
    """Reopen the duo source, prune it, and save one isolated source file."""
    bpy.ops.wm.open_mainfile(filepath=str(BLEND_PATH))
    collection_name = f"SnowTraveler_{role}"
    if bpy.data.collections.get(collection_name) is None:
        raise RuntimeError(f"Missing collection {collection_name}")

    remove_other_character(role)
    for obj in bpy.data.collections[collection_name].objects:
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.hide_render = False
    remaining_actions = sorted(
        action.name
        for action in bpy.data.actions
        if action.name.startswith(f"SnowTraveler_{role}_")
    )
    if not remaining_actions:
        raise RuntimeError(f"No actions found for {role}")

    output = OUTPUT_DIR / f"SnowTraveler_{role}_Single.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(output), check_existing=False)
    print(
        f"SINGLE_CHARACTER_BLEND role={role} path={output} "
        f"collection={collection_name} actions={len(remaining_actions)}"
    )
    for action_name in remaining_actions:
        print(f"  ACTION {action_name}")
    return output


def main() -> None:
    if not BLEND_PATH.exists():
        raise RuntimeError(f"Missing saved Blender source: {BLEND_PATH}")

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    build_single_character_blend("Male")
    build_single_character_blend("Female")
    bpy.ops.wm.open_mainfile(filepath=str(BLEND_PATH))
    print(f"SINGLE_CHARACTER_BLEND_DONE source_restored={BLEND_PATH}")


if __name__ == "__main__":
    main()
