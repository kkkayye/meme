#!/usr/bin/env python3
"""Tripo 3D pipeline for Rune Arena: text -> model -> rig -> retarget animations -> FBX into Assets/Art/Tripo/<id>/.

Usage (run from the project root with the venv):
  TRIPO_API_KEY=tsk_... tools/.venv/bin/python tools/tripo_pipeline.py            # everything
  tools/.venv/bin/python tools/tripo_pipeline.py --dry-run                        # print the plan, no API calls
  tools/.venv/bin/python tools/tripo_pipeline.py --only blaze vanguard            # subset
  tools/.venv/bin/python tools/tripo_pipeline.py --skip-anim                      # models + rig only

Progress is cached in tools/tripo_manifest.json so re-runs resume instead of paying again.
"""
import argparse
import asyncio
import json
import os
import shutil
import sys
import time

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART_DIR = os.path.join(ROOT, "Assets", "Art", "Tripo")
PREVIEW_DIR = os.path.join(ROOT, "tools", "tripo_out")
MANIFEST = os.path.join(ROOT, "tools", "tripo_manifest.json")

MODEL_VERSION = "v3.0-20250812"
RIG_VERSION = "v1.0-20240301"   # biped rig with the 101-animation library (cast_a_spell, slash, chop, ...)
STYLE = ("stylized low-poly game character, clean readable silhouette, T-pose with arms out, "
         "facing forward, full body, single layer of clothing, no cape, no loose accessories, "
         "simple armor pieces, solid colors, game-ready asset")
COMMON_ANIMS = ["idle", "run", "hurt", "fall", "dive"]

CHARACTERS = [
    {"id": "blaze", "rig": True, "faces": 16000,
     "prompt": "young fire mage, orange and red robes with gold trim, short spiky hair, holding a short staff with a small flame, " + STYLE,
     "attack": "shoot", "cast": "cast_a_spell"},
    {"id": "vanguard", "rig": True, "faces": 16000,
     "prompt": "heavy armored knight, steel blue plate armor, large round shield on the left arm, short sword in the right hand, sturdy build, " + STYLE,
     "attack": "slash", "cast": "chop"},
    {"id": "shade", "rig": True, "faces": 16000,
     "prompt": "agile hooded assassin, dark purple leather outfit, face mask, twin daggers, slim build, " + STYLE,
     "attack": "slash", "cast": "chop"},
    {"id": "minion_melee", "rig": True, "faces": 6000, "anims": ["idle", "run", "hurt", "fall"],
     "prompt": "small round goblin foot soldier, grey tunic, wooden club, big head, short legs, " + STYLE,
     "attack": "slash", "cast": None},
    {"id": "minion_ranged", "rig": True, "faces": 6000, "anims": ["idle", "run", "hurt", "fall"],
     "prompt": "small goblin archer, grey tunic with a yellow scarf, short bow in hand, big head, short legs, " + STYLE,
     "attack": "shoot", "cast": None},
    {"id": "tower", "rig": False, "faces": 12000,
     "prompt": "stylized stone watchtower, round tower with a crystal turret on top, low poly game asset, clean silhouette, solid colors"},
]


def load_manifest():
    if os.path.exists(MANIFEST):
        with open(MANIFEST) as f:
            return json.load(f)
    return {}


def save_manifest(m):
    with open(MANIFEST, "w") as f:
        json.dump(m, f, indent=2)


def animations_for(c):
    anims = list(c.get("anims", COMMON_ANIMS))
    if c.get("attack"): anims.append(c["attack"])
    if c.get("cast") and c["cast"] not in anims: anims.append(c["cast"])
    return anims


def chunks(items, size):
    for i in range(0, len(items), size):
        yield items[i:i + size]


def plan(chars):
    print("Plan:")
    for c in chars:
        if c["rig"]:
            anims = animations_for(c)
            print(f"  {c['id']:14s} model({c['faces']} faces) -> rig({RIG_VERSION}) -> retarget {len(anims)} anims in {len(list(chunks(anims,5)))} task(s): {', '.join(anims)}")
        else:
            print(f"  {c['id']:14s} model({c['faces']} faces) -> convert FBX (static)")
    print(f"Output: {ART_DIR}/<id>/  (rig.fbx, anim_N.fbx | model.fbx), previews in {PREVIEW_DIR}")


async def wait(client, task_id, label):
    from tripo3d import TaskStatus
    print(f"    waiting {label} [{task_id}]", flush=True)
    task = await client.wait_for_task(task_id, polling_interval=3.0, verbose=False)
    if task.status != TaskStatus.SUCCESS:
        raise RuntimeError(f"{label} failed: status={task.status} code={task.error_code} msg={task.error_msg}")
    return task


async def download(client, task, out_dir, base_name):
    os.makedirs(out_dir, exist_ok=True)
    tmp = os.path.join(PREVIEW_DIR, "tmp_" + task.task_id)
    os.makedirs(tmp, exist_ok=True)
    files = await client.download_task_models(task, tmp)
    result = {}
    for key in ("model", "pbr_model", "base_model"):
        path = files.get(key)
        if not path or not os.path.exists(path):
            continue
        ext = os.path.splitext(path)[1].lower()
        dest = os.path.join(out_dir, f"{base_name}{ext}")
        shutil.move(path, dest)
        result[key] = dest
        break
    shutil.rmtree(tmp, ignore_errors=True)
    return result


async def ensure_task(client, manifest, cid, step, create, label):
    """Create (or reuse from the manifest) a task and wait for it. Returns the finished Task."""
    entry = manifest.setdefault(cid, {})
    task_id = entry.get(step)
    if task_id:
        try:
            task = await client.get_task(task_id)
            if task.status.value in ("success",):
                print(f"    {label}: cached [{task_id}]")
                return task
            if task.status.value in ("queued", "running"):
                return await wait(client, task_id, label)
        except Exception as e:  # noqa: BLE001 - a stale id just means we recreate the task
            print(f"    {label}: cached id unusable ({e}); recreating")
    task_id = await create()
    entry[step] = task_id
    save_manifest(manifest)
    return await wait(client, task_id, label)


async def process_character(client, manifest, c, skip_anim):
    cid = c["id"]
    out_dir = os.path.join(ART_DIR, cid)
    print(f"== {cid}")
    model = await ensure_task(client, manifest, cid, "model",
        lambda: client.text_to_model(prompt=c["prompt"], model_version=MODEL_VERSION, face_limit=c["faces"], texture=True, pbr=True), "model")
    if model.output.rendered_image:
        os.makedirs(PREVIEW_DIR, exist_ok=True)
        await client.download_rendered_image(model, PREVIEW_DIR)
    if not c["rig"]:
        conv = await ensure_task(client, manifest, cid, "convert",
            lambda: client.create_task({"type": "convert_model", "format": "FBX", "original_model_task_id": model.task_id,
                                        "texture_format": "PNG", "texture_size": 2048, "pivot_to_center_bottom": True}), "convert")
        files = await download(client, conv, out_dir, f"{cid}_model")
        print(f"    saved {files}")
        return
    rig = await ensure_task(client, manifest, cid, "rig",
        lambda: client.create_task({"type": "animate_rig", "original_model_task_id": model.task_id, "out_format": "fbx",
                                    "model_version": RIG_VERSION, "rig_type": "biped", "spec": "tripo"}), "rig")
    files = await download(client, rig, out_dir, f"{cid}_rig")
    print(f"    saved {files}")
    if skip_anim:
        return
    for index, group in enumerate(chunks(animations_for(c), 5)):
        names = ["preset:" + a for a in group]
        step = f"anim_{index}"
        retarget = await ensure_task(client, manifest, cid, step,
            lambda names=names: client.create_task({"type": "animate_retarget", "original_model_task_id": rig.task_id, "out_format": "fbx",
                                                    "animations": names, "animate_in_place": True, "export_with_geometry": True,
                                                    "bake_animation": True}), step)
        files = await download(client, retarget, out_dir, f"{cid}_{step}")
        manifest[cid][step + "_clips"] = group
        save_manifest(manifest)
        print(f"    saved {files} clips={group}")


async def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--only", nargs="*", default=None)
    parser.add_argument("--skip-anim", action="store_true")
    args = parser.parse_args()
    chars = [c for c in CHARACTERS if not args.only or c["id"] in args.only]
    plan(chars)
    if args.dry_run:
        return
    key_file = os.path.join(ROOT, "tools", "tripo.key")
    if not os.environ.get("TRIPO_API_KEY") and os.path.exists(key_file):
        with open(key_file) as f:
            os.environ["TRIPO_API_KEY"] = f.read().strip()
    if not os.environ.get("TRIPO_API_KEY"):
        print("TRIPO_API_KEY is not set (export it, or put the key in tools/tripo.key)", file=sys.stderr)
        sys.exit(2)
    from tripo3d import TripoClient
    manifest = load_manifest()
    async with TripoClient() as client:
        balance = await client.get_balance()
        print(f"Balance before: {balance.balance} (frozen {balance.frozen})")
        started = time.time()
        for c in chars:
            try:
                await process_character(client, manifest, c, args.skip_anim)
            except Exception as e:  # noqa: BLE001 - keep going with the other characters, report at the end
                print(f"!! {c['id']} failed: {e}", file=sys.stderr)
                manifest.setdefault(c["id"], {})["error"] = str(e)
                save_manifest(manifest)
        balance = await client.get_balance()
        print(f"Balance after: {balance.balance}  elapsed {time.time() - started:.0f}s")
        print("Next: in Unity run menu RuneArena > Build Character Prefabs (or the batch command in README).")


if __name__ == "__main__":
    asyncio.run(main())
