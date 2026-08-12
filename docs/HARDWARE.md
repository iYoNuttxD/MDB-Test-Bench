# Hardware topology

```text
MDB Test Bench (.NET / Avalonia)
        | USB / Serial
        v
USB <-> RS232
        |
        v
Wafer MDB-RS232 (reported revision 2022061K5)
        | MDB
        v
Zilog Z8 / proprietary cashless MDB device
```

The Zilog Z8 is responsible for MDB communication. The ESP32 and BLE path are outside MDB Test Bench v0.1 communication scope.

The Settings page discovers whatever port names the operating system reports. Typical names may resemble `COM3`, `/dev/cu.usbserial-*`, `/dev/cu.usbmodem-*`, `/dev/ttyUSB*`, or `/dev/ttyACM*`, but none is hardcoded or assumed.

Defaults are 9600 baud, 8 data bits, no parity, one stop bit, adapter-managed polling, and a two-second timeout. They are test starting points, not confirmed properties of revision `2022061K5`.

Before physical testing, verify voltage levels, cable pinout, ground, isolation, serial adapter driver, device permissions, and safe connection procedures with the hardware owner. Follow [TESTING_WITH_HARDWARE.md](TESTING_WITH_HARDWARE.md) and preserve traffic evidence.
