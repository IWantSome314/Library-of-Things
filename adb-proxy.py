#!/usr/bin/env python3
import os
import socket
import threading

LISTEN_HOST = os.getenv("ADB_PROXY_LISTEN_HOST", "127.0.0.1")
LISTEN_PORT = int(os.getenv("ADB_PROXY_LISTEN_PORT", "5037"))
TARGET_HOST = os.getenv("ADB_PROXY_TARGET_HOST", "host.docker.internal")
TARGET_PORT = int(os.getenv("ADB_PROXY_TARGET_PORT", "5037"))
TARGET_HOSTS = [
    host.strip()
    for host in os.getenv("ADB_PROXY_TARGET_HOSTS", TARGET_HOST).split(",")
    if host.strip()
]


def pipe(src: socket.socket, dst: socket.socket):
    try:
        while True:
            data = src.recv(65536)
            if not data:
                break
            dst.sendall(data)
    except Exception:
        pass
    finally:
        try:
            dst.shutdown(socket.SHUT_WR)
        except Exception:
            pass


def handle_client(client: socket.socket):
    upstream = None
    errors = []
    try:
        for host in TARGET_HOSTS:
            try:
                upstream = socket.create_connection((host, TARGET_PORT), timeout=10)
                break
            except Exception as error:
                errors.append(f"{host}:{TARGET_PORT} -> {error}")

        if upstream is None:
            raise ConnectionError("; ".join(errors))

        t1 = threading.Thread(target=pipe, args=(client, upstream), daemon=True)
        t2 = threading.Thread(target=pipe, args=(upstream, client), daemon=True)
        t1.start()
        t2.start()
        t1.join()
        t2.join()
    except Exception as error:
        print(f"proxy connection failed: {error}", flush=True)
        if "Connection refused" in str(error):
            print(
                "hint: host adb is reachable but not accepting remote connections; run 'adb kill-server' then 'adb -a start-server' on the host",
                flush=True,
            )
    finally:
        try:
            client.close()
        except Exception:
            pass
        if upstream is not None:
            try:
                upstream.close()
            except Exception:
                pass


def main():
    server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    try:
        server.bind((LISTEN_HOST, LISTEN_PORT))
    except OSError as error:
        print(f"failed to bind {LISTEN_HOST}:{LISTEN_PORT}: {error}", flush=True)
        print("Tip: stop the process using this port or set ADB_PROXY_LISTEN_PORT.", flush=True)
        raise
    server.listen(32)
    print(
        f"adb proxy listening on {LISTEN_HOST}:{LISTEN_PORT} -> {','.join(TARGET_HOSTS)}:{TARGET_PORT}",
        flush=True,
    )
    while True:
        client, _ = server.accept()
        threading.Thread(target=handle_client, args=(client,), daemon=True).start()


if __name__ == "__main__":
    main()