"""Smoke-test host-side OTLP export without Docker (Python 3, standard library only).
Run from any directory after: dotnet build MVC_App.sln
This verifies OTLP/HTTP receipt, not Collector processing or Elasticsearch/Kibana ingestion.
"""

import http.server
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import threading
import time
import urllib.request


ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / "MVC_App"
DLL = APP / "bin" / "Debug" / "net10.0" / "MVC_App.dll"
SIGNALS = {"/v1/traces", "/v1/metrics", "/v1/logs"}
received = {}
lock = threading.Lock()


class Receiver(http.server.BaseHTTPRequestHandler):
    def do_POST(self):
        size = int(self.headers.get("Content-Length", "0"))
        body = self.rfile.read(size)
        if self.path not in SIGNALS or not body:
            self.send_error(400)
            return
        if "application/x-protobuf" not in self.headers.get("Content-Type", ""):
            self.send_error(415)
            return
        with lock:
            received.setdefault(self.path, []).append(body)
        # An empty protobuf message is a successful OTLP Export*ServiceResponse.
        self.send_response(200)
        self.send_header("Content-Type", "application/x-protobuf")
        self.send_header("Content-Length", "0")
        self.end_headers()

    def log_message(self, *_):
        pass


def free_port():
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        return sock.getsockname()[1]


def exercise(receiver_port, mode):
    with lock:
        received.clear()
    port = free_port()
    env = os.environ.copy()
    # Isolate this test from host-specific OTEL settings.
    for key in list(env):
        if key.upper().startswith(("OTEL_", "TELEMETRY__", "ASPNETCORE_", "DOTNET_ENVIRONMENT")):
            del env[key]
    env.update({
        "DOTNET_ENVIRONMENT": "Production" if mode == "unconfigured" else "Development",
        "ASPNETCORE_URLS": f"http://127.0.0.1:{port}",
        "OTEL_SERVICE_NAME": "MVC_App.SmokeTest",
        "OTEL_METRIC_EXPORT_INTERVAL": "1000",
        "OTEL_BSP_SCHEDULE_DELAY": "500",
        "OTEL_BLRP_SCHEDULE_DELAY": "500",
    })
    if mode != "unconfigured":
        env["OTEL_EXPORTER_OTLP_ENDPOINT"] = f"http://127.0.0.1:{receiver_port}"
    env["Telemetry__Enabled"] = "false" if mode == "disabled" else "true"
    # Ignore OS HTTP proxy settings for loopback test requests.
    client = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    with tempfile.TemporaryFile(mode="w+b") as output:
        process = subprocess.Popen(["dotnet", str(DLL)], cwd=APP, env=env,
                                   stdout=output, stderr=subprocess.STDOUT)
        try:
            deadline = time.monotonic() + 30
            while True:
                if process.poll() is not None:
                    raise RuntimeError(f"App exited during {mode} startup ({process.returncode})")
                try:
                    with client.open(f"http://127.0.0.1:{port}/", timeout=2) as response:
                        assert response.status == 200
                    break
                except OSError:
                    if time.monotonic() > deadline:
                        raise RuntimeError(f"App readiness timeout in {mode} mode") from None
                    time.sleep(0.25)
            for path in ("/", "/Privacy", "/Error", "/css/site.css", "/Department/Create"):
                with client.open(f"http://127.0.0.1:{port}{path}", timeout=5) as response:
                    assert response.status == 200, path
            if mode == "enabled":
                deadline = time.monotonic() + 20
                while time.monotonic() < deadline:
                    with lock:
                        if SIGNALS.issubset(received):
                            break
                    time.sleep(0.25)
                with lock:
                    assert SIGNALS.issubset(received), f"Missing signals: {SIGNALS - received.keys()}"
                    for signal in sorted(SIGNALS):
                        assert any(b"MVC_App.SmokeTest" in body for body in received[signal]), signal
                        print(f"PASS {signal}: protobuf payload received with service identity")
            else:
                time.sleep(3)
                with lock:
                    assert not received, f"Unexpected export while {mode}"
                print(f"PASS {mode}: pages respond and no telemetry reaches the test receiver")
            print(f"PASS {mode}: five non-destructive HTTP route checks")
        finally:
            process.terminate()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()


def main():
    if not DLL.exists():
        raise SystemExit("Build the default Debug configuration first: dotnet build MVC_App.sln")
    server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Receiver)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    try:
        for mode in ("enabled", "disabled", "unconfigured"):
            exercise(server.server_port, mode)
    finally:
        server.shutdown()
        server.server_close()
        thread.join(timeout=5)
    print("Host-side telemetry smoke tests passed. No database writes or migrations performed.")


if __name__ == "__main__":
    main()
