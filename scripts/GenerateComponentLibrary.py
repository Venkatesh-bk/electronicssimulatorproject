import json
import os

def generate_library():
    print("Generating Master Component Database...")
    
    components = []
    
    # 1. IoT Devices & Microcontrollers
    components.extend([
        {
            "Id": "MCU-ESP32-WROOM",
            "Name": "ESP32-WROOM-32",
            "Manufacturer": "Espressif Systems",
            "Category": "IoT / Wireless",
            "Description": "Powerful Wi-Fi & Bluetooth MCU module with dual-core processor.",
            "Pins": 38,
            "SpiceModel": "* Behavioral model for ESP32 digital I/O\n.SUBCKT ESP32_WROOM ... \n.ENDS",
            "IsCustomIoT": True
        },
        {
            "Id": "MCU-ARD-UNO-R3",
            "Name": "Arduino Uno R3",
            "Manufacturer": "Arduino",
            "Category": "Development Board",
            "Description": "ATmega328P based microcontroller board.",
            "Pins": 32,
            "SpiceModel": "* Mixed-signal model for Arduino UNO R3 \n.SUBCKT ARD_UNO_R3 ... \n.ENDS",
            "IsCustomIoT": True
        },
        {
            "Id": "SBC-RPI-4B",
            "Name": "Raspberry Pi 4 Model B",
            "Manufacturer": "Raspberry Pi Foundation",
            "Category": "Single Board Computer",
            "Description": "Broadcom BCM2711, Quad core Cortex-A72 (ARM v8) 64-bit SoC @ 1.5GHz.",
            "Pins": 40,
            "SpiceModel": "* High-speed behavioral model for RPi4 GPIO \n.SUBCKT RPI_4B ... \n.ENDS",
            "IsCustomIoT": True
        },
        {
            "Id": "MCU-STM32F103C8T6",
            "Name": "STM32 Blue Pill",
            "Manufacturer": "STMicroelectronics",
            "Category": "Microcontroller",
            "Description": "ARM Cortex-M3 32-bit MCU.",
            "Pins": 40,
            "SpiceModel": "* STM32 Behavioral Model \n.SUBCKT STM32_BLUEPILL ... \n.ENDS",
            "IsCustomIoT": True
        }
    ])

    # 2. Standard ICs
    components.extend([
        {
            "Id": "IC-NE555",
            "Name": "NE555",
            "Manufacturer": "Texas Instruments",
            "Category": "Integrated Circuit",
            "Description": "Precision Timer",
            "Pins": 8,
            "SpiceModel": ".SUBCKT NE555 GND TRIG OUT RESET CONT THRES DISCH VCC ... .ENDS",
            "IsCustomIoT": False
        },
        {
            "Id": "IC-LM358",
            "Name": "LM358",
            "Manufacturer": "ON Semiconductor",
            "Category": "Operational Amplifier",
            "Description": "Low-Power, Dual-Operational Amplifier",
            "Pins": 8,
            "SpiceModel": ".SUBCKT LM358 1 2 3 4 5 ... .ENDS",
            "IsCustomIoT": False
        }
    ])

    # 3. Discrete Semiconductors
    discrete_semis = [
        ("DIO-1N4148", "1N4148", "Diode", "High-speed switching diode", ".MODEL 1N4148 D (IS=4.35E-9 N=1.906 RS=0.6458 BV=110 IBV=0.0001)"),
        ("DIO-1N4007", "1N4007", "Diode", "General-purpose rectifier diode", ".MODEL 1N4007 D (IS=7.02767n RS=0.0341512 N=1.80803 EG=1.05743)"),
        ("BJT-2N2222", "2N2222", "NPN Transistor", "General purpose NPN amplifier", ".MODEL 2N2222 NPN (IS=1E-14 VAF=100 BF=200 IKF=0.3 XTB=1.5 BR=3)"),
        ("BJT-2N3906", "2N3906", "PNP Transistor", "General purpose PNP amplifier", ".MODEL 2N3906 PNP (IS=1E-14 VAF=100 BF=200 IKF=0.3 XTB=1.5 BR=3)"),
        ("MOS-IRF540N", "IRF540N", "N-Channel MOSFET", "Power MOSFET", ".MODEL IRF540N VDMOS (Rg=3 Vto=4 Rd=0.044 Rs=0.012 Rb=0.016 Kp=20)"),
        ("MOS-BSS138", "BSS138", "N-Channel MOSFET", "Logic Level MOSFET", ".MODEL BSS138 VDMOS (Rg=10 Vto=1.2 Rd=0.5 Rs=0.1 Kp=0.5)")
    ]

    for d_id, name, cat, desc, model in discrete_semis:
        components.append({
            "Id": d_id,
            "Name": name,
            "Manufacturer": "Generic",
            "Category": cat,
            "Description": desc,
            "Pins": 2 if "Diode" in cat else 3,
            "SpiceModel": model,
            "IsCustomIoT": False
        })

    # 4. Generate thousands of standard passive components (E12 series)
    print("Generating E12 series passives (Resistors & Capacitors)...")
    multipliers = [1, 10, 100, 1000, 10000, 100000, 1000000] # 1 Ohm to 1M Ohm
    e12_base = [1.0, 1.2, 1.5, 1.8, 2.2, 2.7, 3.3, 3.9, 4.7, 5.6, 6.8, 8.2]
    
    # Resistors
    for mult in multipliers:
        for base in e12_base:
            val = base * mult
            val_str = f"{val}R" if mult < 1000 else f"{val/1000}k" if mult < 1000000 else f"{val/1000000}M"
            components.append({
                "Id": f"RES-{val_str}",
                "Name": f"Resistor {val_str}",
                "Manufacturer": "Generic",
                "Category": "Resistor",
                "Description": f"{val_str} 5% 0.25W Axial Resistor",
                "Pins": 2,
                "SpiceModel": f"R_val {val}",
                "IsCustomIoT": False
            })

    # Capacitors (Picofarads to Microfarads)
    cap_mults = [1e-12, 1e-11, 1e-10, 1e-9, 1e-8, 1e-7, 1e-6]
    for mult in cap_mults:
        for base in e12_base:
            val = base * mult
            val_str = f"{val*1e12:.0f}pF" if mult < 1e-9 else f"{val*1e9:.1f}nF" if mult < 1e-6 else f"{val*1e6:.1f}uF"
            components.append({
                "Id": f"CAP-{val_str}",
                "Name": f"Capacitor {val_str}",
                "Manufacturer": "Generic",
                "Category": "Capacitor",
                "Description": f"{val_str} Ceramic/Electrolytic Capacitor",
                "Pins": 2,
                "SpiceModel": f"C_val {val}",
                "IsCustomIoT": False
            })

    database = {
        "Metadata": {
            "Version": "1.0",
            "TotalComponents": len(components),
            "GeneratedAt": "2026-05-19T00:00:00Z"
        },
        "Components": components
    }

    output_path = os.path.join("src", "Engines", "EdaSimulator.Engines", "Library", "MasterComponentDatabase.json")
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    
    with open(output_path, "w") as f:
        json.dump(database, f, indent=2)
        
    print(f"Successfully generated database with {len(components)} components at {output_path}")

if __name__ == "__main__":
    generate_library()
