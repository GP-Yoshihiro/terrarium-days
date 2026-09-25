#!/usr/bin/env python3
"""Token-lean summary of Unity test results and build logs.

Prints totals per results file, then only the failing tests (name, first message line,
first project stack frame) and unique compiler/Xcode errors. Use it instead of reading
the raw XML/log files, which run to hundreds of KB.

usage: scripts/test-summary.py [Logs/EditMode-results.xml ...] [--log Logs/xcodebuild.log ...]
       (no args: every Logs/*-results.xml plus compiler errors from Logs/*.log)
"""
import glob
import os
import re
import sys

root = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
args = sys.argv[1:]
logs = []
if "--log" in args:
    i = args.index("--log")
    logs, args = args[i + 1:], args[:i]
results = args or sorted(glob.glob(os.path.join(root, "Logs", "*-results.xml")))
if not sys.argv[1:]:
    logs = sorted(glob.glob(os.path.join(root, "Logs", "*.log")))

for path in results:
    xml = open(path, encoding="utf-8", errors="replace").read()
    run = re.search(r"<test-run[^>]*>", xml)
    counts = dict(re.findall(r'(total|passed|failed|skipped)="(\d+)"', run.group(0))) if run else {}
    print(f"{os.path.basename(path)}: " + " ".join(f"{k}={counts.get(k, '?')}" for k in ("total", "passed", "failed", "skipped")))
    for case in re.finditer(r'<test-case[^>]*fullname="([^"]+)"[^>]*result="Failed"(.*?)</test-case>', xml, re.S):
        name, body = case.group(1), case.group(2)
        msg = re.search(r"<message><!\[CDATA\[(.*?)\]\]>", body, re.S)
        stack = re.search(r"(Assets/[^\s:]+:\d+|Assets/[^\s)]+\.cs:line \d+)", body)
        first = msg.group(1).strip().splitlines()[0][:200] if msg else ""
        print(f"  FAIL {name}\n       {first}" + (f"\n       at {stack.group(1)}" if stack else ""))

seen = set()
for path in logs:
    for line in open(path, encoding="utf-8", errors="replace"):
        m = re.search(r"(error CS\d+: .*|^\S+\.(?:cs|mm|m|h|cpp):\d+:\d+: error: .*|^error: .*)", line)
        if m and m.group(1) not in seen:
            seen.add(m.group(1))
            print(f"{os.path.basename(path)}: {m.group(1)[:220]}")
