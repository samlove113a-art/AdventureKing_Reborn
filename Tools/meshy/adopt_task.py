"""Finish an already-created Meshy task (wait, download, log to the manifest) without paying again.

  python adopt_task.py <kind> <name> <task_id> <request.json>
"""
import json
import os
import sys
import time

sys.path.insert(0, os.path.dirname(__file__))
import meshy  # noqa: E402

kind, name, task_id, req = sys.argv[1:5]
t = meshy.wait(kind, task_id, quiet=True)
files = {}
if t.get("status") == "SUCCEEDED":
    for key, url in meshy._collect_urls(t).items():
        dest = os.path.join(meshy.RAW, name, key + meshy._ext(key, url))
        meshy.download(url, dest)
        files[key] = os.path.relpath(dest, meshy.ROOT).replace("\\", "/")
with open(os.path.join(meshy.ROOT, req), encoding="utf-8-sig") as f:
    body = json.load(f)
rec = {"time": time.strftime("%Y-%m-%dT%H:%M:%S"), "name": name, "kind": kind, "task_id": task_id, "status": t.get("status"),
       "credits": t.get("consumed_credits"), "error": t.get("task_error"), "request": body, "files": files, "license": meshy.LICENSE}
with open(meshy.MANIFEST, "a", encoding="utf-8") as f:
    f.write(json.dumps(rec, ensure_ascii=False) + "\n")
print(name, rec["status"], rec["credits"], list(files))
