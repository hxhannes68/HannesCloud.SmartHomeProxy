#!/usr/bin/env python3
"""BME280 logger, v1.5

Writes one JSON object per line to bme280_data.jsonl and, since v1.5, sends each reading to
the SmartHome proxy (POST /local/states), which forwards it to the cloud. The file stays the
record: a failed send is logged and dropped, never retried and never allowed to stop logging.

Sending is configured in /etc/pi-sensor.env (see pi-sensor.env.example), read again before
every send, so changing ROOM there moves the sensor in the app without a restart. Without that
file, or with PROXY_URL empty, the logger only writes the file as before.

Robustness compared to v1.2: a failed sensor read skips one cycle and re-opens the
I2C bus instead of killing the loop, and a systemd watchdog restarts the service if it ever hangs.
"""
import json
import os
import socket
import time
import urllib.error
import urllib.request
from datetime import datetime

import adafruit_bme280.advanced as adafruit_bme280
import board
import busio

# --------------------------------------
# CONSTANTS
# --------------------------------------
MEASUREMENT_INTERVAL = 2 * 60    # seconds
REOPEN_AFTER_FAILURES = 3
EXIT_AFTER_FAILURES = 10
I2C_ADDRESS = 0x77

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DATA_FILE = os.path.join(BASE_DIR, "bme280_data.jsonl")   # one JSON object per line, append-only
SEND_CONFIG = "/etc/pi-sensor.env"
SEND_TIMEOUT = 5  # seconds; well inside the watchdog's 120


# --------------------------------------
# SYSTEMD WATCHDOG
# --------------------------------------
def sd_notify(message):
    """Tell systemd we're alive. No-op when not started by systemd."""
    path = os.environ.get("NOTIFY_SOCKET")
    if not path:
        return
    if path.startswith("@"):
        path = "\0" + path[1:]
    try:
        with socket.socket(socket.AF_UNIX, socket.SOCK_DGRAM) as sock:
            sock.connect(path)
            sock.sendall(message.encode())
    except OSError:
        pass


# --------------------------------------
# SENSOR
# --------------------------------------
class SensorReader:
    def __init__(self):
        self.i2c = None
        self.sensor = None
        self.open()

    def open(self):
        self.i2c = busio.I2C(board.SCL, board.SDA)
        self.sensor = adafruit_bme280.Adafruit_BME280_I2C(self.i2c, address=I2C_ADDRESS)

    def close(self):
        try:
            if self.i2c is not None:
                self.i2c.deinit()
        except Exception:
            pass
        self.i2c = None
        self.sensor = None

    def reopen(self):
        self.close()
        time.sleep(1)
        self.open()

    def read(self):
        temperature = self.sensor.temperature
        humidity = self.sensor.humidity
        pressure = self.sensor.pressure
        # datasheet ranges; anything else is a bus glitch, not a measurement
        if not (-40 <= temperature <= 85 and 0 <= humidity <= 100 and 300 <= pressure <= 1100):
            raise ValueError(f"implausible reading: {temperature} C, {humidity} %, {pressure} hPa")
        return {
            "timestamp": datetime.now().astimezone().isoformat(timespec="seconds"),
            "temperature": round(temperature, 2),
            "humidity": round(humidity, 2),
            "pressure": round(pressure, 2),
        }


# --------------------------------------
# LOGGING
# --------------------------------------
def append_line(path, entry):
    with open(path, "a") as f:
        f.write(json.dumps(entry) + "\n")


def wait_until(deadline):
    while True:
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            return
        sd_notify("WATCHDOG=1")
        time.sleep(min(remaining, 10))


# --------------------------------------
# SENDING
# --------------------------------------
def read_config(path):
    """KEY=value lines; missing file = empty config (sending off)."""
    config = {}
    try:
        with open(path) as f:
            for line in f:
                line = line.strip()
                if line and not line.startswith("#") and "=" in line:
                    key, value = line.split("=", 1)
                    config[key.strip()] = value.strip()
    except OSError:
        pass
    return config


def send(entry):
    config = read_config(SEND_CONFIG)
    url = config.get("PROXY_URL", "")
    if not url:
        return

    payload = {
        "device": config.get("DEVICE", "pi1"),
        "room": config.get("ROOM", ""),
        "temperature": entry["temperature"],
        "humidity": entry["humidity"],
        "pressure": entry["pressure"],
    }
    request = urllib.request.Request(
        url.rstrip("/") + "/local/states",
        data=json.dumps(payload).encode(),
        headers={"Content-Type": "application/json", "X-Api-Key": config.get("API_KEY", "")},
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=SEND_TIMEOUT) as response:
            print(f"Sent to proxy: {response.status} {response.read().decode()}", flush=True)
    except urllib.error.HTTPError as e:
        # 401 = API_KEY doesn't match the proxy's, 400 = the proxy says what's wrong.
        print(f"Proxy answered {e.code}: {e.read().decode()}", flush=True)
    except Exception as e:
        print(f"Sending to proxy failed: {e}", flush=True)


# --------------------------------------
# MAIN LOOP
# --------------------------------------
def main():
    sensor = SensorReader()
    failures = 0
    next_run = time.monotonic()
    sd_notify("READY=1")

    while True:
        sd_notify("WATCHDOG=1")
        try:
            entry = sensor.read()
        except Exception as e:
            failures += 1
            print(f"Sensor read failed ({failures}/{EXIT_AFTER_FAILURES}): {e}", flush=True)
            if failures >= EXIT_AFTER_FAILURES:
                raise SystemExit(1)  # systemd restarts us
            if failures % REOPEN_AFTER_FAILURES == 0:
                try:
                    sensor.reopen()
                except Exception as reopen_error:
                    print(f"Re-opening the sensor failed: {reopen_error}", flush=True)
        else:
            failures = 0
            print(f"New measurement: {entry}", flush=True)
            append_line(DATA_FILE, entry)
            send(entry)

        next_run += MEASUREMENT_INTERVAL
        if next_run < time.monotonic():   # fell behind: don't fire a burst of reads
            next_run = time.monotonic()
        wait_until(next_run)


if __name__ == "__main__":
    main()
