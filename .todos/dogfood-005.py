"""Dogfooding-Skript fuer Einheit 005."""
import json
import subprocess
import os
import time
import threading

ROOT = r"C:\Daten\Entwicklung\Ralf\AiNetLinter"
EXE = os.path.join(ROOT, "src", "AiNetLinter", "bin", "Debug", "net10.0", "AiNetLinter.exe")

def frame(req_id, method, params):
    return json.dumps({"jsonrpc": "2.0", "id": req_id, "method": method, "params": params})

def call(req_id, name, args):
    return frame(req_id, "tools/call", {"name": name, "arguments": args})

def main():
    proc = subprocess.Popen(
        [EXE, "--mcp-server", "--path", ROOT],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        encoding="utf-8",
        bufsize=0,
    )

    def send(msg):
        proc.stdin.write(msg + "\n")
        proc.stdin.flush()
        time.sleep(0.5)

    stderr_lines = []
    def drain_stderr():
        for line in proc.stderr:
            stderr_lines.append(line.rstrip())
    t = threading.Thread(target=drain_stderr, daemon=True)
    t.start()

    print("=== initialize ===")
    send(frame(1, "initialize", {
        "protocolVersion": "2024-11-05",
        "capabilities": {},
        "clientInfo": {"name": "dogfood-005", "version": "1.0.0"},
    }))

    send(json.dumps({"jsonrpc": "2.0", "method": "notifications/initialized"}))

    print("=== tools/list ===")
    send(frame(2, "tools/list", {}))

    print("=== find_references(Greeter.Greet, maxResults=5) (Dummy, Symbol existiert nicht im Repo) ===")
    send(call(3, "find_references", {"symbolIdentifier": "DiffImpactAnalyzer.FindCallSitesAsync", "maxResults": 5}))

    print("=== get_impact(symbolIdentifier=DiffImpactAnalyzer.FindCallSitesAsync, maxResults=3) ===")
    send(call(4, "get_impact", {"symbolIdentifier": "DiffImpactAnalyzer.FindCallSitesAsync", "maxResults": 3}))

    print("=== get_impact(gitRef=HEAD, maxResults=10) (uncommittete diff) ===")
    send(call(5, "get_impact", {"gitRef": "HEAD", "maxResults": 10}))

    # Read all responses.
    deadline = time.time() + 60
    responses = {}
    for line in proc.stdout:
        if time.time() > deadline:
            print("  (timeout reading)")
            break
        line = line.rstrip()
        if not line:
            continue
        try:
            obj = json.loads(line)
        except json.JSONDecodeError:
            print(f"  (non-JSON) {line}")
            continue
        rid = obj.get("id")
        if rid is not None:
            responses[rid] = obj
            if len(responses) == 5:
                break

    if 1 in responses:
        r = responses[1].get("result", {})
        si = r.get("serverInfo", {})
        print(f"  serverInfo.name: {si.get('name')}")
        print(f"  serverInfo.version: {si.get('version')}")

    if 2 in responses:
        r = responses[2].get("result", {})
        tools = r.get("tools", [])
        for tool in tools:
            if tool.get("name") in ("find_references", "get_impact"):
                desc = tool.get("description", "")
                print(f"  --- {tool.get('name')} description ---")
                for ln in desc.splitlines():
                    print(f"  {ln}")

    for rid in (3, 4, 5):
        if rid in responses:
            r = responses[rid].get("result", {})
            content = r.get("content", [])
            text = content[0].get("text", "") if content else "(leer)"
            print(f"  --- response id={rid} ---")
            for ln in text.splitlines():
                print(f"  {ln}")

    try:
        proc.stdin.close()
    except Exception:
        pass
    try:
        proc.wait(timeout=5)
    except subprocess.TimeoutExpired:
        proc.kill()

    if stderr_lines:
        print("\n=== stderr ===")
        for ln in stderr_lines:
            print(f"  {ln}")

if __name__ == "__main__":
    main()
