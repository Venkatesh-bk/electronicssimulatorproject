# EdaSimulator — Research Databases

Large pre-computed knowledge bases used to power the physics simulation engine and AI-assisted component suggestions.

> ⚠️ These files are in `.gitignore` — they are too large for standard git (total ~274 MB).
> Regenerate them using the scripts in `scripts/`.

## Files

| File | Size | Description |
|------|------|-------------|
| `EdaDeepResearchDatabase.pkl.gz` | ~119 MB | v1 — Initial EDA physics and component dataset |
| `EdaDeepResearchDatabase_v2.pkl.gz` | ~3 MB | v2 — Refined subset |
| `EdaDeepResearchDatabase_v3.pkl.gz` | ~152 MB | v3 — Current, full SI/RF/thermal/analog coverage |
| `EdaPhysicsKnowledgeBase.zip` | ~14 KB | Compact physics formulae reference (zipped markdown) |

## Regenerating

```powershell
# Latest version (recommended)
python scripts\GenerateEdaDeepResearch_v3.py

# Older versions if needed for comparison
python scripts\GenerateEdaDeepResearch_v2.py
python scripts\GenerateDeepResearchData.py
```

> **GPU Recommended:** Generation uses CuPy for GPU-accelerated processing.
> Falls back to NumPy (CPU) automatically if no CUDA GPU is present.
