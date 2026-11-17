#!/usr/bin/env python3
"""Reads the BME280 on the Raspberry Pi and posts it to the SmartHome proxy.

Runs once per call; the systemd timer calls it every minute. Everything that differs per
Pi comes from the environment (see pi-sensor.env.example), above all ROOM: change it when
the Pi moves to another room, and the cloud moves the sensor with it.

    python3 pi_sensor.py                      read the sensor and send
    python3 pi_sensor.py --dry-run            read the sensor, print what would be sent
    python3 pi_sensor.py --values 21.3,58,1013  send these instead of reading (to test the path)
"""
import argparse
import json
import os
import sys
import urllib.error
import urllib.request


def env(name, default=None):
    value = os.environ.get(name, default)
    if value is None or value == "":
        sys.exit(f"pi-sensor: {name} is not set (see pi-sensor.env.example)")
    return value


def read_bme280():
    # Imported here so --values and --dry-run work on a machine without the I2C libraries.
    import bme280
    import smbus2

    bus_number = int(env("I2C_BUS", "1"))
    address = int(env("I2C_ADDRESS", "0x76"), 16)
    with smbus2.SMBus(bus_number) as bus:
        calibration = bme280.load_calibration_params(bus, address)
        data = bme280.sample(bus, address, calibration)
    return data.temperature, data.humidity, data.pressure


def parse_values(text):
    temperature, humidity, pressure = (float(v) for v in text.split(","))
    return temperature, humidity, pressure


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--values", help="temperature,humidity,pressure to send instead of reading the sensor")
    args = parser.parse_args()

    temperature, humidity, pressure = parse_values(args.values) if args.values else read_bme280()
    payload = {
        "device": env("DEVICE"),
        "room": env("ROOM"),
        "temperature": round(temperature, 2),
        "humidity": round(humidity, 2),
        "pressure": round(pressure, 2),
    }

    if args.dry_run:
        print(json.dumps(payload, ensure_ascii=False))
        return

    request = urllib.request.Request(
        env("PROXY_URL").rstrip("/") + "/local/states",
        data=json.dumps(payload).encode(),
        headers={"Content-Type": "application/json", "X-Api-Key": env("API_KEY")},
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=10) as response:
            print(f"pi-sensor: {payload} -> {response.status} {response.read().decode()}")
    except urllib.error.HTTPError as error:
        # 401 = wrong API_KEY, 400 = the proxy explains what's wrong with the reading.
        sys.exit(f"pi-sensor: proxy answered {error.code}: {error.read().decode()}")
    except urllib.error.URLError as error:
        sys.exit(f"pi-sensor: proxy not reachable at {env('PROXY_URL')}: {error.reason}")


if __name__ == "__main__":
    main()
