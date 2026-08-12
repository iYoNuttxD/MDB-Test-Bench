# Hardware topology

```text
MDB Test Bench (.NET / Avalonia)
        | Serial
        v
USB <-> RS232
        |
        v
Wafer MDB-RS232 (reported revision 2022061K5)
        | MDB
        v
Zilog Z8 / proprietary cashless MDB device
        |
        v
ESP32 / BLE
```

The Zilog Z8 is responsible for MDB communication. The ESP32 provides BLE communication with external devices and is outside the MDB Test Bench v0.1 communication scope.

No electrical assumptions, serial framing, command bytes, or timing values for the reported Wafer revision are encoded as facts in this foundation. Before physical tests, verify voltage levels, cabling, isolation, grounds, serial parameters, adapter documentation, and safe connection procedures with the hardware owner.
