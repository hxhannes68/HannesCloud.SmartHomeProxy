# Pi sensor

Sends temperature, humidity and air pressure of a BME280 on a Raspberry Pi to the SmartHome
proxy every minute (`POST /local/states`). The proxy forwards them to the cloud, without Home
Assistant. In the app they show up as `sensor.<DEVICE>_temperature`, `_humidity` and `_pressure`.

## Install on the Pi

```bash
sudo raspi-config nonint do_i2c 0
sudo mkdir -p /opt/pi-sensor
sudo cp pi_sensor.py /opt/pi-sensor/
sudo python3 -m venv /opt/pi-sensor/venv
sudo /opt/pi-sensor/venv/bin/pip install smbus2 RPi.bme280
sudo cp pi-sensor.env.example /etc/pi-sensor.env
sudo nano /etc/pi-sensor.env
sudo cp pi-sensor.service pi-sensor.timer /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now pi-sensor.timer
```

## Check

```bash
sudo systemctl start pi-sensor.service && journalctl -u pi-sensor.service -n 5
```

## Moving the Pi

Change `ROOM` in `/etc/pi-sensor.env`. The next reading (within a minute) moves the sensor to
that room in the app. If the room doesn't exist there yet, the app offers to create it.
