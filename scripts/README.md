# EdaSimulator — Scripts

Python utility scripts for data generation and project setup.

| Script | Purpose |
|--------|---------|
| `GenerateComponentLibrary.py` | Generates `MasterComponentDatabase.json` with 180+ components |
| `GenerateDeepResearchData.py` | Generates `EdaDeepResearchDatabase.pkl.gz` (physics knowledge base) |
| `GenerateEdaDeepResearch_v2.py` | Version 2 of the research database generator |
| `GenerateEdaDeepResearch_v3.py` | Version 3 — current, includes SI/RF/thermal data |
| `DownloadVendorLibraries.py` | Downloads third-party SPICE model libraries |
| `test_cupy.py` | Quick GPU/CuPy availability test |

## Usage

```powershell
cd d:\electronicssimulatorproject

# Regenerate component library
python scripts\GenerateComponentLibrary.py

# Regenerate research database (takes ~5 min, requires GPU recommended)
python scripts\GenerateEdaDeepResearch_v3.py

# Download vendor SPICE models
python scripts\DownloadVendorLibraries.py
```

> Requires Python 3.10+ and the `.venv` environment activated.
