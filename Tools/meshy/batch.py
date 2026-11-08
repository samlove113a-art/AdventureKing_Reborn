"""Run several Meshy jobs in parallel (same logging/provenance as meshy.py).

  python batch.py <jobs.json> [max_parallel]

jobs.json: [{"kind": "text-to-image", "name": "...", "request": "ArtSource/Meshy/requests/x.json"}, ...]
A job whose name already has a SUCCEEDED entry in the manifest is skipped (re-runs never re-spend credits).
"""
import json
import os
import sys
from concurrent.futures import ThreadPoolExecutor

sys.path.insert(0, os.path.dirname(__file__))
import meshy  # noqa: E402


def done_names():
    names = set()
    if os.path.exists(meshy.MANIFEST):
        with open(meshy.MANIFEST, encoding="utf-8") as f:
            for line in f:
                try:
                    rec = json.loads(line)
                except ValueError:
                    continue
                if rec.get("status") == "SUCCEEDED":
                    names.add(rec["name"])
    return names


def one(job):
    with open(os.path.join(meshy.ROOT, job["request"]), encoding="utf-8-sig") as f:
        body = meshy._resolve(json.load(f))
    try:
        t = meshy.run(job["kind"], job["name"], body)
        return job["name"], t.get("status")
    except Exception as e:  # keep the other jobs going; the error is reported
        return job["name"], "ERROR " + str(e)[:300]


if __name__ == "__main__":
    with open(sys.argv[1], encoding="utf-8-sig") as f:
        jobs = json.load(f)
    par = int(sys.argv[2]) if len(sys.argv) > 2 else 6
    skip = done_names()
    todo = [j for j in jobs if j["name"] not in skip]
    print(f"{len(jobs)} jobs, {len(jobs) - len(todo)} already done, running {len(todo)}", flush=True)
    with ThreadPoolExecutor(par) as ex:
        for name, status in ex.map(one, todo):
            print("RESULT", name, status, flush=True)
