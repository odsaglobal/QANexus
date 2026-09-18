#!/usr/bin/env python3
"""Drive a scenario RUN through the API and report per-step results.

Usage: run-scenario.py <projectId> <scenarioId> <environmentId> [label]
"""
import json
import sys
import time
import urllib.request

API = "http://localhost:5125/api/v1"
KEY = "qk_flipkart_seed_20260904_x7f2"


def call(method, path, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(API + path, data=data, method=method)
    req.add_header("X-Api-Key", KEY)
    req.add_header("Content-Type", "application/json")
    with urllib.request.urlopen(req) as r:
        raw = r.read()
    return json.loads(raw) if raw else None


def main():
    project, scenario, env = sys.argv[1], sys.argv[2], sys.argv[3]
    label = sys.argv[4] if len(sys.argv) > 4 else "run"

    started = time.time()
    session = call("POST", f"/projects/{project}/scenarios/{scenario}/run", {
        "environmentId": env,
    })
    sid = session["id"]
    print(f"[{label}] session {sid}")

    last = None
    while True:
        time.sleep(4)
        s = call("GET", f"/projects/{project}/explorer/sessions/{sid}")
        st = s["status"]
        if st != last:
            print(f"[{label}] status={st} ({time.time() - started:.0f}s)")
            last = st
        if st in ("Completed", "Failed", "Cancelled"):
            break
        if time.time() - started > 900:
            print(f"[{label}] TIMEOUT")
            break

    elapsed = time.time() - started
    detail = call("GET", f"/projects/{project}/explorer/sessions/{sid}")
    print(f"[{label}] finished status={detail['status']} in {elapsed:.0f}s")
    if detail.get("errorMessage"):
        print(f"[{label}] error: {detail['errorMessage']}")

    steps = call("GET", f"/projects/{project}/explorer/sessions/{sid}/steps")
    items = steps["items"] if isinstance(steps, dict) and "items" in steps else steps
    for st in items:
        print(f"   [{st.get('status')}] {st.get('title') or st.get('action')}")
        if st.get("details"):
            print(f"        {st['details']}")

    print(f"\n[{label}] ELAPSED={elapsed:.0f}s STATUS={detail['status']}")


if __name__ == "__main__":
    main()
