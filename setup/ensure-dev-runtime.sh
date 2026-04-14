#!/usr/bin/env bash
set -u

MODE="full"
for arg in "$@"; do
    case "$arg" in
        --api-only)
            MODE="api"
            ;;
        --adb-only)
            MODE="adb"
            ;;
        --adb-maintain)
            MODE="adb-maintain"
            ;;
        --monitor)
            MODE="monitor"
            ;;
    esac
done

ROOT="/workspace"
LOCAL_API_HEALTH_URL="http://127.0.0.1:8080/health"
HOST_API_HEALTH_URL="http://host.docker.internal:8080/health"
LOG_DIR="/tmp/starterapp"
API_LOG="$LOG_DIR/api-startup.log"
ADB_LOG="$LOG_DIR/adb-proxy.log"
ADB_REVERSE_LOG="$LOG_DIR/adb-reverse.log"
MONITOR_LOG="$LOG_DIR/runtime-monitor.log"
MONITOR_INTERVAL_SECONDS="${RUNTIME_MONITOR_INTERVAL_SECONDS:-5}"

mkdir -p "$LOG_DIR"

start_adb_proxy() {
    if pgrep -f "python3 $ROOT/adb-proxy.py" >/dev/null 2>&1; then
        return 0
    fi

    nohup python3 "$ROOT/adb-proxy.py" >"$ADB_LOG" 2>&1 &
}

api_is_healthy() {
    curl -fsS --max-time 2 "$LOCAL_API_HEALTH_URL" >/dev/null 2>&1 \
        || curl -fsS --max-time 2 "$HOST_API_HEALTH_URL" >/dev/null 2>&1
}

start_api_if_needed() {
    if api_is_healthy; then
        return 0
    fi

    if ! pgrep -f "StarterApp.Api/StarterApp.Api.csproj" >/dev/null 2>&1; then
        echo "[$(date -Iseconds)] API unhealthy, starting backend" >>"$MONITOR_LOG"
        nohup env ASPNETCORE_URLS="http://0.0.0.0:8080" dotnet run --project "$ROOT/StarterApp.Api/StarterApp.Api.csproj" >"$API_LOG" 2>&1 &
    fi

    for _ in $(seq 1 30); do
        if api_is_healthy; then
            return 0
        fi
        sleep 2
    done

    return 1
}

adb_device_is_ready() {
    adb devices 2>/dev/null | awk 'NR > 1 && $2 == "device" { found = 1 } END { exit(found ? 0 : 1) }'
}

adb_reverse_is_active() {
    adb reverse --list 2>/dev/null | grep -q 'tcp:8080 tcp:8080'
}

maintain_adb_reverse() {
    local attempts="${1:-150}"

    if ! command -v adb >/dev/null 2>&1; then
        return 0
    fi

    for _ in $(seq 1 "$attempts"); do
        if adb_device_is_ready; then
            adb reverse tcp:8080 tcp:8080 >/dev/null 2>&1 || true
            if adb_reverse_is_active; then
                return 0
            fi
        fi
        sleep 2
    done

    return 1
}

ensure_adb_reverse() {
    if ! command -v adb >/dev/null 2>&1; then
        return 0
    fi

    if adb_reverse_is_active; then
        return 0
    fi

    if maintain_adb_reverse 15; then
        return 0
    fi

    if ! pgrep -f 'ensure-dev-runtime.sh --adb-maintain' >/dev/null 2>&1; then
        nohup bash "$ROOT/setup/ensure-dev-runtime.sh" --adb-maintain >"$ADB_REVERSE_LOG" 2>&1 &
    fi

    return 0
}

start_monitor_if_needed() {
    if pgrep -f 'ensure-dev-runtime.sh --monitor' >/dev/null 2>&1; then
        return 0
    fi

    nohup bash "$ROOT/setup/ensure-dev-runtime.sh" --monitor >>"$MONITOR_LOG" 2>&1 &
}

monitor_runtime() {
    echo "[$(date -Iseconds)] runtime monitor active with ${MONITOR_INTERVAL_SECONDS}s interval"
    while true; do
        start_adb_proxy
        start_api_if_needed >/dev/null 2>&1 || true
        ensure_adb_reverse >/dev/null 2>&1 || true
        sleep "$MONITOR_INTERVAL_SECONDS"
    done
}

case "$MODE" in
    api)
        start_api_if_needed
        start_monitor_if_needed
        ;;
    adb)
        start_adb_proxy
        ensure_adb_reverse
        start_monitor_if_needed
        ;;
    adb-maintain)
        start_adb_proxy
        maintain_adb_reverse
        ;;
    monitor)
        monitor_runtime
        ;;
    *)
        start_adb_proxy
        start_api_if_needed
        ensure_adb_reverse
        start_monitor_if_needed
        ;;
esac
