# Pi sensor

`bme280.py` runs on the Raspberry Pi as `bme280.service`. Every two minutes it reads the BME280
(I2C address 0x77), appends the reading to `bme280_data.jsonl`, and sends it to the SmartHome
proxy (`POST /local/states`), which forwards it to the cloud without Home Assistant. In the app
the readings show up as `sensor.<DEVICE>_temperature`, `_humidity` and `_pressure`.

## Install / update on the Pi

```bash
scp bme280.py hannes@raspberrypi.local:~/Documents/bme280.py
ssh hannes@raspberrypi.local sudo systemctl restart bme280.service
```

First time only: `bme280.service` goes to `/etc/systemd/system/`, the adafruit libraries
(`adafruit-circuitpython-bme280`) must be installed, and `/etc/pi-sensor.env` is created from
`pi-sensor.env.example` with the API key from the proxy's `secrets.env`.

## Check

```bash
ssh hannes@raspberrypi.local journalctl -u bme280.service -n 5
```

`Sent to proxy: 202 {"forwarded":3}` means it arrived; `Proxy answered 401` means the API key
differs from the proxy's.

## Moving the Pi

Change `ROOM` in `/etc/pi-sensor.env`. The next reading moves the sensor to that room in the
app. If the room doesn't exist there yet, the app offers to create it.
