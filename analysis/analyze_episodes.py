"""Auswertung der Episoden-CSV aus Unity (EpisodeCsvLogger).

    pip install pandas matplotlib
    python analysis/analyze_episodes.py pfad/zu/selfdefense_episodes.csv

Erzeugt im Ordner analysis/output/:
  - success_scenario_style.csv / .png   Erfolgsrate pro Szenario x Stil (Heatmap)
  - success_by_mass.png                 Erfolgsrate je Stil über das Grössenverhältnis
  - technique_usage.csv                 welche Techniken der Agent pro Szenario bevorzugt
"""
import sys
from pathlib import Path

import matplotlib.pyplot as plt
import pandas as pd

STYLE_ORDER = ["Untrained", "Boxing", "MuayThai", "BJJ", "Judo", "KravMaga", "WingTsun", "Mixed"]
SCENARIO_ORDER = ["WristGrab", "PinnedToWall", "BearHugBehind", "ChokeFront", "GroundMount", "CorneredStrikes"]


def main(csv_path: str) -> None:
    df = pd.read_csv(csv_path)
    out = Path(__file__).resolve().parent / "output"
    out.mkdir(exist_ok=True)
    print(f"{len(df)} Episoden geladen")

    # 1) Heatmap Szenario x Stil
    table = (df.pivot_table(index="scenario", columns="style", values="success", aggfunc="mean")
               .reindex(index=[s for s in SCENARIO_ORDER if s in df.scenario.unique()],
                        columns=[s for s in STYLE_ORDER if s in df["style"].unique()]))
    table.to_csv(out / "success_scenario_style.csv")
    print("\nErfolgsrate (Szenario x Stil):\n", (table * 100).round(0))

    fig, ax = plt.subplots(figsize=(1.1 * len(table.columns) + 2, 0.6 * len(table.index) + 1.5))
    im = ax.imshow(table.values, vmin=0, vmax=1, cmap="viridis", aspect="auto")
    ax.set_xticks(range(len(table.columns)), table.columns, rotation=35, ha="right")
    ax.set_yticks(range(len(table.index)), table.index)
    for i in range(table.shape[0]):
        for j in range(table.shape[1]):
            v = table.values[i, j]
            if pd.notna(v):
                ax.text(j, i, f"{v * 100:.0f}%", ha="center", va="center",
                        color="white" if v < 0.6 else "black", fontsize=9)
    fig.colorbar(im, ax=ax, label="Erfolgsrate (Flucht gelungen)")
    ax.set_title("Erfolgsrate pro Szenario und Kampfstil")
    fig.tight_layout()
    fig.savefig(out / "success_scenario_style.png", dpi=150)

    # 2) Erfolg über Grössenverhältnis
    df["mass_bin"] = pd.cut(df["mass_ratio"], bins=[1.0, 1.2, 1.4, 1.6, 1.8, 2.5], include_lowest=True)
    by_mass = df.pivot_table(index="mass_bin", columns="style", values="success", aggfunc="mean", observed=False)
    fig, ax = plt.subplots(figsize=(8, 4.5))
    for style in [s for s in STYLE_ORDER if s in by_mass.columns]:
        ax.plot([str(b) for b in by_mass.index], by_mass[style], marker="o", label=style)
    ax.set_xlabel("Masseverhältnis Angreifer / Verteidigerin")
    ax.set_ylabel("Erfolgsrate")
    ax.set_ylim(0, 1)
    ax.legend(ncol=2, fontsize=8)
    ax.set_title("Wie schnell sinkt der Erfolg, wenn der Gegner grösser wird?")
    fig.tight_layout()
    fig.savefig(out / "success_by_mass.png", dpi=150)

    # 3) Technik-Nutzung pro Szenario (Durchschnitt pro Episode)
    uses = [c for c in df.columns if c.startswith("uses_")]
    usage = df.groupby("scenario")[uses].mean().rename(columns=lambda c: c[5:])
    usage.to_csv(out / "technique_usage.csv")
    print("\nTop-3 Techniken pro Szenario:")
    for scenario, row in usage.iterrows():
        top = row.sort_values(ascending=False).head(3)
        print(f"  {scenario:16s} " + ", ".join(f"{k} ({v:.1f}x)" for k, v in top.items()))

    print(f"\nDateien in {out}")


if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.exit("Aufruf: python analysis/analyze_episodes.py <selfdefense_episodes.csv>")
    main(sys.argv[1])
