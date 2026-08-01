"""Dogfooding-Skript fuer Einheit 004."""
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
        bufsize=0,  # unbuffered
    )

    # Send each frame separately with a small delay to allow server to process.
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
        "clientInfo": {"name": "dogfood-004", "version": "1.0.0"},
    }))

    send(json.dumps({"jsonrpc": "2.0", "method": "notifications/initialized"}))

    print("=== find_symbol(FindSymbol, maxResults=5) ===")
    send(call(2, "find_symbol", {"namePattern": "FindSymbol", "maxResults": 5}))

    print("=== find_symbol(Kritiker, default) ===")
    send(call(3, "find_symbol", {"namePattern": "Kritiker"}))

    # Read all responses.
    deadline = time.time() + 30
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
            if len(responses) == 3:
                break

    # Render.
    if 1 in responses:
        r = responses[1].get("result", {})
        si = r.get("serverInfo", {})
        print(f"  serverInfo.name: {si.get('name')}")
        print(f"  serverInfo.version: {si.get('version')}")
        instr = r.get("instructions", "")
        print(f"  instructions: {instr[:200]}...")

    if 2 in responses:
        r = responses[2].get("result", {})
        content = r.get("content", [])
        text = content[0].get("text", "") if content else "(leer)"
        print("  --- response ---")
        for ln in text.splitlines():
            print(f"  {ln}")

    if 3 in responses:
        r = responses[3].get("result", {})
        content = r.get("content", [])
        text = content[0].get("text", "") if content else "(leer)"
        print("  --- response ---")
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
