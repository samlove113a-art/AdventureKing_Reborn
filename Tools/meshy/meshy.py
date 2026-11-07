"""Minimal Meshy API client for the Adventure King art pipeline.

Reads the key from the MESHY_API_KEY environment variable (never printed, never written to disk).
Every task is logged to ArtSource/Meshy/manifest.jsonl (prompt, task id, credits, licence) so the
provenance of each shipped asset can be audited. Raw downloads go to ArtSource/Meshy/raw/ (git-ignored;
Meshy deletes outputs after 3 days, so keep this folder backed up).

Usage (run with Blender's bundled Python or any Python 3.10+):
  python meshy.py balance
  python meshy.py library [search]
  python meshy.py run <kind> <name> <request.json>     kind: text-to-image | image-to-3d | text-to-3d | rigging | animations | retexture | remesh
  python meshy.py get <kind> <task_id>
"""
import json
import os
import sys
import time
import urllib.error
import urllib.request

API = "https://api.meshy.ai"
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "ArtSource", "Meshy")
RAW = os.path.join(OUT, "raw")
MANIFEST = os.path.join(OUT, "manifest.jsonl")
ENDPOINT = {
    "text-to-image": "/openapi/v1/text-to-image",
    "image-to-3d": "/openapi/v1/image-to-3d",
    "text-to-3d": "/openapi/v2/text-to-3d",
    "rigging": "/openapi/v1/rigging",
    "animations": "/openapi/v1/animations",
    "retexture": "/openapi/v1/retexture",
    "remesh": "/openapi/v1/remesh",
}
LICENSE = "Meshy paid plan: private licence, full commercial use (docs.meshy.ai/en/webapp/pricing)"


def _key():
    k = os.environ.get("MESHY_API_KEY")
    if not k:
        sys.exit("MESHY_API_KEY is not set")
    return k


def call(method, path, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(API + path, data=data, method=method)
    req.add_header("Authorization", "Bearer " + _key())
    if data:
        req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req, timeout=120) as r:
            return json.loads(r.read().decode())
    except urllib.error.HTTPError as e:
        raise RuntimeError(f"HTTP {e.code} {path}: {e.read().decode()[:500]}") from None


def download(url, dest):
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    for attempt in range(4):
        try:
            with urllib.request.urlopen(url, timeout=300) as r, open(dest, "wb") as f:
                f.write(r.read())
            return dest
        except Exception:
            if attempt == 3:
                raise
            time.sleep(5)


def _collect_urls(task):
    urls = {}
    for k, v in (task.get("model_urls") or {}).items():
        if v and k in ("glb", "fbx"):
            urls[k] = v
    for i, u in enumerate(task.get("image_urls") or []):
        urls[f"image_{i}.png"] = u
    res = task.get("result") or {}
    for k, v in res.items():
        if isinstance(v, str) and v.startswith("http"):
            urls[k] = v
    for k, v in (res.get("basic_animations") or {}).items():
        if isinstance(v, str) and v.startswith("http") and v.endswith(("glb", "fbx")) or (isinstance(v, str) and "glb" in k):
            urls["basic_" + k] = v
    if task.get("thumbnail_url"):
        urls["thumbnail.png"] = task["thumbnail_url"]
    return urls


def _ext(key, url):
    if "." in key[-5:]:
        return ""
    base = url.split("?")[0]
    return os.path.splitext(base)[1] or ".bin"


def wait(kind, task_id, quiet=False):
    path = f"{ENDPOINT[kind]}/{task_id}"
    while True:
        t = call("GET", path)
        st = t.get("status")
        if not quiet:
            print(f"  {kind} {task_id} {st} {t.get('progress', '')}", flush=True)
        if st in ("SUCCEEDED", "FAILED", "CANCELED"):
            return t
        time.sleep(8)


def run(kind, name, body):
    created = call("POST", ENDPOINT[kind], body)
    task_id = created["result"]
    print(f"created {kind} {name} -> {task_id}", flush=True)
    t = wait(kind, task_id)
    files = {}
    if t.get("status") == "SUCCEEDED":
        for key, url in _collect_urls(t).items():
            dest = os.path.join(RAW, name, key + _ext(key, url))
            download(url, dest)
            files[key] = os.path.relpath(dest, ROOT).replace("\\", "/")
    rec = {
        "time": time.strftime("%Y-%m-%dT%H:%M:%S"), "name": name, "kind": kind, "task_id": task_id,
        "status": t.get("status"), "credits": t.get("consumed_credits"), "error": t.get("task_error"),
        "request": body, "files": files, "license": LICENSE,
    }
    # never log large data URIs
    if isinstance(rec["request"].get("image_url"), str) and rec["request"]["image_url"].startswith("data:"):
        rec["request"] = dict(rec["request"], image_url="<data-uri>")
    if isinstance(rec["request"].get("model_url"), str) and rec["request"]["model_url"].startswith("data:"):
        rec["request"] = dict(rec["request"], model_url="<data-uri>")
    os.makedirs(OUT, exist_ok=True)
    with open(MANIFEST, "a", encoding="utf-8") as f:
        f.write(json.dumps(rec, ensure_ascii=False) + "\n")
    print(json.dumps({k: rec[k] for k in ("name", "status", "credits", "error", "files")}, ensure_ascii=False, indent=1))
    return t


def data_uri(path):
    import base64
    mime = "image/png" if path.lower().endswith(".png") else "image/jpeg"
    if path.lower().endswith(".glb"):
        mime = "application/octet-stream"
    with open(path, "rb") as f:
        return f"data:{mime};base64," + base64.b64encode(f.read()).decode()


def _resolve(body):
    """Allow "@file:relative/path" values to be sent as data URIs."""
    for k, v in list(body.items()):
        if isinstance(v, str) and v.startswith("@file:"):
            body[k] = data_uri(os.path.join(ROOT, v[6:]))
    return body


if __name__ == "__main__":
    a = sys.argv[1:]
    if not a or a[0] == "balance":
        print(call("GET", "/openapi/v1/balance"))
    elif a[0] == "library":
        q = "?search=" + urllib.request.quote(a[1]) if len(a) > 1 else ""
        lib = call("GET", "/openapi/v1/animations/library" + q)
        items = lib if isinstance(lib, list) else lib.get("result", lib.get("data", lib))
        for it in items:
            print(it.get("action_id"), it.get("name"), it.get("category"), it.get("sub_category"), sep="\t")
    elif a[0] == "run":
        kind, name, req = a[1], a[2], a[3]
        with open(req, encoding="utf-8-sig") as f:
            body = _resolve(json.load(f))
        run(kind, name, body)
    elif a[0] == "get":
        print(json.dumps(call("GET", f"{ENDPOINT[a[1]]}/{a[2]}"), indent=1)[:4000])
    else:
        sys.exit(__doc__)
