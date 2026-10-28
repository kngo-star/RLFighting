"""Erzeugt pro Kampfstil eine eigene Trainings-Config (fairer Stil-Vergleich).

Jeder Stil wird als eigener Agent trainiert, mit identischen Hyperparametern,
Szenarien und Grössenverhältnissen. Nur der erlaubte Technik-Satz unterscheidet sich.

    python scripts/make_style_configs.py
    mlagents-learn config/styles/style_BJJ.yaml --run-id=style_BJJ
"""
from pathlib import Path

STYLES = ["Untrained", "Boxing", "Kickboxing", "MuayThai", "Karate", "Taekwondo", "BJJ", "JuJutsu",
          "Judo", "Wrestling", "Aikido", "KravMaga", "WingTsun", "MMA", "Mixed"]

ROOT = Path(__file__).resolve().parent.parent
base = (ROOT / "config" / "selfdefense_ppo.yaml").read_text(encoding="utf-8")
out_dir = ROOT / "config" / "styles"
out_dir.mkdir(parents=True, exist_ok=True)

for idx, name in enumerate(STYLES):
    text = base.replace("max_steps: 3000000", "max_steps: 2000000")
    lines = []
    body = text.splitlines()
    while body and body[0].startswith("#"):
        body.pop(0)
    for line in body:
        if line.strip().startswith("style:"):
            line = f"  style: {idx}          # {name}"
        lines.append(line)
    header = f"# Automatisch erzeugt von scripts/make_style_configs.py – Stil: {name}\n"
    header += f"# Start:  mlagents-learn config/styles/style_{name}.yaml --run-id=style_{name}\n"
    (out_dir / f"style_{name}.yaml").write_text(header + "\n".join(lines) + "\n", encoding="utf-8")
    print("geschrieben:", out_dir / f"style_{name}.yaml")
