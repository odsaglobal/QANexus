#!/usr/bin/env python3
"""E2E harness: run every scenario in a project twice.

Pass 1 ("learn")  - may use AI to ground steps that have no recording yet.
Pass 2 ("replay") - must be fully deterministic: no MCP subprocess, no LLM calls.

Usage: run-all.py <projectId> <environmentId>
"""
import json
import subprocess
import sys
import time
import urllib.request

API = "http://localhost:5125/api/v1"
KEY = "qk_flipkart_seed_20260904_x7f2"
LOG = "/tmp/atip-api.log"


def call(method, path, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(API + path, data=data, method=method)
    req.add_header("X-Api-Key", KEY)
    req.add_header("Content-Type", "application/json")
    with urllib.request.urlopen(req) as r:
        raw = r.read()
    return json.loads(raw) if raw else None


def unwrap(d):
    return d["items"] if isinstance(d, dict) and "items" in d else d


def log_lines():
    with open(LOG, "r", errors="ignore") as f:
        return len(f.readlines())


def log_since(start):
    with open(LOG, "r", errors="ignore") as f:
        return f.readlines()[start:]


def run_once(project, scenario, env):
    """Runs one scenario, returns (elapsed, status, steps, mcp_started, llm_calls)."""
    mark = log_lines()
    started = time.time()
    session = call("POST", f"/projects/{project}/scenarios/{scenario}/run",
                   {"environmentId": env})
    sid = session["id"]

    while True:
        time.sleep(3)
        s = call("GET", f"/projects/{project}/explorer/sessions/{sid}")
        if s["status"] in ("Completed", "Failed", "Cancelled"):
            break
        if time.time() - started > 600:
            break

    elapsed = time.time() - started
    detail = call("GET", f"/projects/{project}/explorer/sessions/{sid}")
    steps = unwrap(call("GET", f"/projects/{project}/explorer/sessions/{sid}/steps"))

    tail = log_since(mark)
    mcp = sum(1 for ln in tail if "MCP server started" in ln)
    llm = sum(1 for ln in tail if "LLM" in ln or "chat/completions" in ln)
    return elapsed, detail["status"], steps, mcp, llm


def summarise(label, elapsed, status, steps, mcp, llm):
    verdicts = [st.get("status") for st in steps]
    passed = sum(1 for v in verdicts if v == "Passed")
    healed = sum(1 for v in verdicts if v == "Healed")
    failed = sum(1 for v in verdicts if v == "Failed")
    print(f"    {label:<8} {elapsed:5.0f}s  {status:<10} "
          f"pass={passed} healed={healed} fail={failed}  mcp={mcp}")
    for st in steps:
        if st.get("status") == "Failed":
            print(f"        FAILED step {st.get('stepOrder')}: {st.get('action')}")
            print(f"          -> {st.get('detail')}")
    return passed, healed, failed


def main():
    project, env = sys.argv[1], sys.argv[2]
    scenarios = unwrap(call("GET", f"/projects/{project}/scenarios"))

    # De-duplicate scenarios that share a title (TestRail sync creates pairs).
    seen, unique = set(), []
    for s in scenarios:
        if s["title"] in seen:
            continue
        seen.add(s["title"])
        unique.append(s)

    print(f"{len(unique)} unique scenario(s) to verify\n")
    report = []

    for s in unique:
        print(f"» {s['title']}  ({len(s['steps'])} steps)")
        e1, st1, sp1, m1, l1 = run_once(project, s["id"], env)
        summarise("learn", e1, st1, sp1, m1, l1)
        e2, st2, sp2, m2, l2 = run_once(project, s["id"], env)
        p2, h2, f2 = summarise("replay", e2, st2, sp2, m2, l2)

        report.append({
            "title": s["title"], "learn_s": e1, "replay_s": e2,
            "replay_status": st2, "replay_pass": p2, "replay_heal": h2,
            "replay_fail": f2, "replay_mcp": m2,
        })
        print()

    print("=" * 78)
    print(f"{'SCENARIO':<44}{'LEARN':>7}{'REPLAY':>8}{'P/H/F':>10}{'MCP':>5}")
    print("=" * 78)
    for r in report:
        phf = f"{r['replay_pass']}/{r['replay_heal']}/{r['replay_fail']}"
        print(f"{r['title'][:43]:<44}{r['learn_s']:6.0f}s{r['replay_s']:7.0f}s"
              f"{phf:>10}{r['replay_mcp']:>5}")

    clean = sum(1 for r in report if r["replay_fail"] == 0 and r["replay_heal"] == 0)
    print("=" * 78)
    print(f"{clean}/{len(report)} scenario(s) replayed cleanly with zero AI.")


if __name__ == "__main__":
    main()
