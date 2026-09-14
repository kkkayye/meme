#!/usr/bin/env python3
"""Tripo pipeline for Rune Arena, driven by the official `tripo` CLI (V3 API).

text -> model -> rig-check -> rig (v1.0 biped, mixamo bone names, FBX) -> retarget presets (FBX, in place)
-> files copied into Assets/Art/Tripo/<id>/ for Unity's CharacterPrefabBuilder.

Usage (project root):
  tools/.venv/bin/python tools/tripo_pipeline.py --dry-run          # print the plan, no API calls
  tools/.venv/bin/python tools/tripo_pipeline.py                    # everything (key from TRIPO_API_KEY or tools/tripo.key)
  tools/.venv/bin/python tools/tripo_pipeline.py --only blaze tower # subset
  tools/.venv/bin/python tools/tripo_pipeline.py --skip-anim        # models + rig only

State lives in tools/tripo_manifest.json: finished steps are reused, so re-running never pays twice.
"""
import argparse
import json
import os
import shutil
import subprocess
import sys
import time

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART_DIR = os.path.join(ROOT, "Assets", "Art", "Tripo")
OUT_DIR = os.path.join(ROOT, "tools", "tripo_out")
MANIFEST = os.path.join(ROOT, "tools", "tripo_manifest.json")
TRIPO = shutil.which("tripo") or os.path.expanduser("~/.hermes/node/bin/tripo")

GEN_MODEL = "tripo-v3.1"
RIG_MODEL = "rig-v1.0"           # biped rig with the 90+ preset library (cast_a_spell, chop, ...)
RIG_SPEC = "mixamo"              # Mixamo bone names: Unity Humanoid-friendly, Mixamo clips can be added later
STYLE = ("stylized low-poly game character, clean readable silhouette, T-pose with arms straight out, "
         "facing forward, full body, single layer of clothing, no cape, no loose accessories, simple armor pieces, "
         "solid colors, game-ready asset")
COMMON_ANIMS = ["idle", "run", "hurt", "fall", "dive"]

CHARACTERS = [
    {"id": "blaze", "rig": True, "faces": 16000, "attack": "shoot", "cast": "cast_a_spell",
     "prompt": "young fire mage, orange and red robes with gold trim, short spiky hair, holding a short staff with a small flame, " + STYLE},
    {"id": "vanguard", "rig": True, "faces": 16000, "attack": "slash", "cast": "chop",
     "prompt": "heavy armored knight, steel blue plate armor, large round shield on the left arm, short sword in the right hand, sturdy build, " + STYLE},
    {"id": "shade", "rig": True, "faces": 16000, "attack": "slash", "cast": "chop",
     "prompt": "agile hooded assassin, dark purple leather outfit, face mask, twin daggers, slim build, " + STYLE},
    {"id": "minion_melee", "rig": True, "faces": 6000, "attack": "slash", "cast": None, "anims": ["idle", "run", "hurt", "fall"],
     "prompt": "small round goblin foot soldier, grey tunic, wooden club, big head, short legs, " + STYLE},
    {"id": "minion_ranged", "rig": True, "faces": 6000, "attack": "shoot", "cast": None, "anims": ["idle", "run", "hurt", "fall"],
     "prompt": "small goblin archer, grey tunic with a yellow scarf, short bow in hand, big head, short legs, " + STYLE},
    {"id": "tower", "rig": False, "faces": 12000,
     "prompt": "stylized stone watchtower, round tower with a crystal turret on top, low poly game asset, clean silhouette, solid colors"},
]

EXIT_MEANING = {2: "usage/params", 3: "auth", 4: "insufficient credits (run `tripo topup`)", 5: "content policy",
                6: "task failed (credits refunded)", 7: "network", 8: "not found", 9: "rate limit"}


class TripoError(RuntimeError):
    def __init__(self, code, message):
        super().__init__(message)
        self.code = code


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
    for key in ("attack", "cast"):
        if c.get(key) and c[key] not in anims:
            anims.append(c[key])
    return anims


def chunks(items, size):
    for i in range(0, len(items), size):
        yield items[i:i + size]


def preset(name):
    return "preset:biped:" + name if RIG_MODEL == "rig-v1.0" else "preset:" + name


def plan(chars):
    print(f"Plan (tripo CLI at {TRIPO}, model {GEN_MODEL}, rig {RIG_MODEL}/{RIG_SPEC}):")
    total = 0
    for c in chars:
        if c["rig"]:
            anims = animations_for(c)
            cost = 20 + 25 + 10 * len(anims)
            print(f"  {c['id']:14s} model({c['faces']} faces) -> rig-check -> rig -> retarget {len(anims)} anims in {len(list(chunks(anims, 5)))} task(s): {', '.join(anims)}   ~{cost} credits")
        else:
            cost = 20 + 10
            print(f"  {c['id']:14s} model({c['faces']} faces) -> convert FBX (static)   ~{cost} credits")
        total += cost
    print(f"  estimated total: ~{total} credits (list prices: model 20, rig 25, animation 10 each, complex convert 10)")
    print(f"Output: {ART_DIR}/<id>/  (rig.fbx + anim_N.fbx | model.fbx); CLI artifacts and preview.png in {OUT_DIR}/<id>/")


def run(args, label):
    """Runs a tripo CLI command, returns its final JSON line. Raises TripoError with the CLI exit code."""
    cmd = [TRIPO] + args + ["--json", "--yes", "--quiet", "--no-open"]
    print(f"    $ {' '.join(a if ' ' not in a else repr(a) for a in cmd)}", flush=True)
    proc = subprocess.run(cmd, capture_output=True, text=True)
    stdout = proc.stdout.strip()
    if proc.returncode != 0:
        detail = stdout or proc.stderr.strip()[-600:]
        raise TripoError(proc.returncode, f"{label}: exit {proc.returncode} ({EXIT_MEANING.get(proc.returncode, '?')}): {detail}")
    line = stdout.splitlines()[-1] if stdout else "{}"
    try:
        return json.loads(line)
    except json.JSONDecodeError as e:
        raise TripoError(1, f"{label}: could not parse CLI output: {line[:300]}") from e


def step(manifest, cid, name, factory):
    """Reuses a finished step from the manifest, else runs it and records the result."""
    entry = manifest.setdefault(cid, {})
    if name in entry and entry[name].get("status") == "success":
        print(f"    {name}: cached [{entry[name].get('task_id')}]")
        return entry[name]
    result = factory()
    entry[name] = result
    entry.pop("error", None)
    save_manifest(manifest)
    return result


def copy_model(result, dest_dir, base_name):
    """Copies the CLI's downloaded model file into Assets/Art/Tripo/<id>/<base_name>.<ext>."""
    src = result.get("model_file")
    if not src:
        files = result.get("files") or []
        src = next((f for f in files if str(f).lower().endswith((".fbx", ".glb"))), None)
    if not src or not os.path.exists(src):
        raise TripoError(1, f"no model file in CLI result: {json.dumps(result)[:300]}")
    os.makedirs(dest_dir, exist_ok=True)
    dest = os.path.join(dest_dir, base_name + os.path.splitext(src)[1].lower())
    shutil.copy2(src, dest)
    return dest


def process_character(manifest, c, skip_anim):
    cid = c["id"]
    art = os.path.join(ART_DIR, cid)
    out = os.path.join(OUT_DIR, cid)
    os.makedirs(out, exist_ok=True)
    print(f"== {cid}")
    model = step(manifest, cid, "model", lambda: run(
        ["generate", "text-to-model", c["prompt"], "--model", GEN_MODEL, "-p", f"face_limit={c['faces']}",
         "--name", cid, "-o", os.path.join(out, "model")], "model"))
    print(f"    model task {model.get('task_id')} preview={model.get('preview')}")
    if not c["rig"]:
        conv = step(manifest, cid, "convert", lambda: run(
            ["model", "convert", model["task_id"], "--format", "FBX", "--texture-format", "PNG", "--texture-size", "2048",
             "--pivot-to-center-bottom", "-o", os.path.join(out, "convert")], "convert"))
        print(f"    saved {copy_model(conv, art, cid + '_model')}")
        return
    check = step(manifest, cid, "rigcheck", lambda: run(["anim", "check", model["task_id"], "-o", os.path.join(out, "check")], "rig-check"))
    output = check.get("output") or check
    riggable = output.get("riggable", check.get("riggable", True))
    if riggable is False:
        raise TripoError(6, f"{cid}: model is not riggable (rig_type={output.get('rig_type')}); regenerate with a clearer T-pose prompt")
    rig = step(manifest, cid, "rig", lambda: run(
        ["anim", "rig", model["task_id"], "--rig-type", "biped", "--spec", RIG_SPEC, "--out-format", "fbx", "-p", f"model={RIG_MODEL}",
         "-o", os.path.join(out, "rig")], "rig"))
    print(f"    saved {copy_model(rig, art, cid + '_rig')}")
    if skip_anim:
        return
    for index, group in enumerate(chunks(animations_for(c), 5)):
        name = f"anim_{index}"
        result = step(manifest, cid, name, lambda group=group, name=name: run(
            ["anim", "retarget", rig["task_id"], "--animation"] + [preset(a) for a in group]
            + ["--out-format", "fbx", "--animate-in-place", "-o", os.path.join(out, name)], name))
        manifest[cid][name]["clips"] = group
        save_manifest(manifest)
        print(f"    saved {copy_model(result, art, cid + '_' + name)} clips={group}")


def balance():
    try:
        return run(["balance"], "balance")
    except TripoError as e:
        print(f"    balance check failed: {e}")
        return {}


def main():
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
    if not os.path.exists(TRIPO):
        print("tripo CLI not found; install with: npm install -g tripo-cli", file=sys.stderr)
        sys.exit(2)
    manifest = load_manifest()
    print(f"Balance before: {balance()}")
    started = time.time()
    failures = 0
    for c in chars:
        try:
            process_character(manifest, c, args.skip_anim)
        except TripoError as e:
            failures += 1
            print(f"!! {c['id']} failed: {e}", file=sys.stderr)
            manifest.setdefault(c["id"], {})["error"] = str(e)
            save_manifest(manifest)
            if e.code == 4:
                print("!! out of credits — stopping. Top up at https://developers.tripo3d.ai (tripo topup) and re-run.", file=sys.stderr)
                break
    print(f"Balance after: {balance()}  elapsed {time.time() - started:.0f}s  failures {failures}")
    print("Next: Unity menu RuneArena > Build Character Prefabs (or -executeMethod RuneArena.Editor.CharacterPrefabBuilder.BuildAll).")
    sys.exit(1 if failures else 0)


if __name__ == "__main__":
    main()
