# EdaSimulator — Component Library

Contains the master component database used by the component picker and auto-completion systems.

## Files

| File | Description |
|------|-------------|
| `MasterComponentDatabase.json` | 180+ component definitions (passives, semiconductors, ICs, MCUs) |

## Component Coverage

- **E12 Passives** — Resistors, Capacitors, Inductors (full E12 value series)
- **Discrete Semiconductors** — Diodes (1N4148, Zener), BJTs (2N2222, BC547), MOSFETs (2N7002, IRF540N)
- **Op-Amps** — LM741, LM358, TL071, LM324
- **Logic ICs** — 74HC series (AND, OR, NOT, NAND, NOR, XOR, D-FlipFlop)
- **Power ICs** — LM7805, LM317, LM337, LM2596
- **Microcontrollers** — Arduino Uno R3, ESP32-WROOM-32, Raspberry Pi 4B
- **Communication** — HC-05 Bluetooth, ESP8266 WiFi, SIM800L GSM
- **Sensors** — DHT22, HC-SR04, MPU-6050, DS18B20

## Usage by the Application

`ComponentLibraryService.cs` loads this file at startup. It is copied to the build output
directory automatically via the `.csproj` `CopyToOutputDirectory` setting.

## Regenerating

```powershell
python scripts\GenerateComponentLibrary.py
# → overwrites resources\components\MasterComponentDatabase.json
# → also copies to src\Engines\EdaSimulator.Engines\Library\
```
