"""Auswertung der Episoden-CSV aus Unity (EpisodeCsvLogger).

    pip install pandas matplotlib
    python analysis/analyze_episodes.py pfad/zu/selfdefense_episodes.csv

Erzeugt im Ordner analysis/output/:
  1_erfolg_szenario_stil.png     Heatmap: Erfolgsrate pro Szenario x Stil
  2_erfolg_nach_groesse.png      Erfolgsrate je Stil über das Grössenverhältnis
  3_entscheidende_techniken.png  Welche Technik war vor der Flucht entscheidend? (pro Szenario)
  4_technik_wirksamkeit.png      Pro Technik: wie oft löst sie einen Griff / wirft um (pro Einsatz)
  technik_tabelle.csv            alle Technik-Kennzahlen pro Szenario
  bericht.md                     automatische Zusammenfassung mit den wichtigsten Aussagen

Wie liest man das?
  - Erfolgsrate     = Anteil der Episoden, in denen die Flucht gelungen ist. Hauptkennzahl.
  - löst-Griff-Rate = Griffe gelöst / Einsätze. Misst, wie WIRKSAM eine Technik ist, wenn sie benutzt wird.
  - entscheidend    = in wie viel % der erfolgreichen Fluchten war diese Technik die letzte wirksame Aktion.
                      Misst, wie WICHTIG die Technik für den Erfolg war.
  - Nutzung         = was der Agent gelernt hat zu tun (seine Strategie).
  Eine Technik ist "am besten", wenn sie oft entscheidend ist UND eine hohe löst-Griff-Rate hat.
"""
import sys
from pathlib import Path

import matplotlib.pyplot as plt
import pandas as pd

STYLE_ORDER = ["Untrained", "Boxing", "Kickboxing", "MuayThai", "Karate", "Taekwondo", "BJJ", "JuJutsu",
               "Judo", "Wrestling", "Aikido", "KravMaga", "WingTsun", "MMA", "Mixed"]
SCENARIO_ORDER = ["WristGrab", "PinnedToWall", "BearHugBehind", "ChokeFront", "GroundMount", "CorneredStrikes"]
SCENARIO_DE = {
    "WristGrab": "Handgelenk gepackt", "PinnedToWall": "An die Wand gedrückt",
    "BearHugBehind": "Umklammerung von hinten", "ChokeFront": "Würgegriff vorne",
    "GroundMount": "Am Boden (Mount)", "CorneredStrikes": "In der Ecke, Schläge",
}


def ordered(values, order):
    return [v for v in order if v in set(values)]


def heatmap(ax, table, title, fmt="{:.0f}%", scale=100, vmax=1):
    im = ax.imshow(table.values, vmin=0, vmax=vmax, cmap="viridis", aspect="auto")
    ax.set_xticks(range(len(table.columns)), table.columns, rotation=35, ha="right")
    ax.set_yticks(range(len(table.index)), table.index)
    for i in range(table.shape[0]):
        for j in range(table.shape[1]):
            v = table.values[i, j]
            if pd.notna(v):
                ax.text(j, i, fmt.format(v * scale), ha="center", va="center",
                        color="white" if v < 0.6 * vmax else "black", fontsize=8)
    ax.set_title(title)
    return im


def main(csv_path: str) -> None:
    df = pd.read_csv(csv_path)
    out = Path(__file__).resolve().parent / "output"
    out.mkdir(exist_ok=True)
    techs = [c[5:] for c in df.columns if c.startswith("uses_")]
    print(f"{len(df)} Episoden geladen")
    report = ["# Auswertung Selbstverteidigungs-Simulation", "", f"Episoden: **{len(df)}**, "
              f"Erfolgsrate gesamt: **{df.success.mean():.0%}**", ""]

    # ---- 1) Szenario x Stil
    scen = ordered(df.scenario, SCENARIO_ORDER)
    styles = ordered(df["style"], STYLE_ORDER)
    table = df.pivot_table(index="scenario", columns="style", values="success", aggfunc="mean").reindex(index=scen, columns=styles)
    table.to_csv(out / "erfolg_szenario_stil.csv")
    fig, ax = plt.subplots(figsize=(1.1 * len(styles) + 3, 0.6 * len(scen) + 2))
    im = heatmap(ax, table.rename(index=SCENARIO_DE), "Erfolgsrate (Flucht gelungen) pro Szenario und Stil")
    fig.colorbar(im, ax=ax)
    fig.tight_layout(); fig.savefig(out / "1_erfolg_szenario_stil.png", dpi=150); plt.close(fig)

    report += ["## 1. Welcher Stil funktioniert in welcher Lage?", "", "| Szenario | bester Stil | Erfolg | schlechtester Stil | Erfolg |", "|---|---|---|---|---|"]
    for s in scen:
        row = table.loc[s].dropna()
        if len(row):
            report.append(f"| {SCENARIO_DE[s]} | **{row.idxmax()}** | {row.max():.0%} | {row.idxmin()} | {row.min():.0%} |")
    style_rank = df.groupby("style").success.mean().sort_values(ascending=False)
    report += ["", "Rangfolge über alle Szenarien: " + ", ".join(f"{k} ({v:.0%})" for k, v in style_rank.items()), ""]

    # ---- 2) Grössenverhältnis
    df["groesse"] = (df.mass_ratio / 0.2).round() * 0.2
    by_mass = df.pivot_table(index="groesse", columns="style", values="success", aggfunc="mean")
    fig, ax = plt.subplots(figsize=(8, 4.5))
    for st in ordered(by_mass.columns, STYLE_ORDER):
        ax.plot(by_mass.index, by_mass[st], marker="o", label=st)
    ax.set_xlabel("Masseverhältnis Angreifer / Verteidigerin"); ax.set_ylabel("Erfolgsrate"); ax.set_ylim(0, 1)
    ax.legend(ncol=2, fontsize=8); ax.set_title("Wie stark sinkt der Erfolg, wenn der Gegner grösser wird?")
    fig.tight_layout(); fig.savefig(out / "2_erfolg_nach_groesse.png", dpi=150); plt.close(fig)

    if by_mass.shape[0] >= 2:
        lo, hi = by_mass.index.min(), by_mass.index.max()
        drop = (by_mass.loc[lo] - by_mass.loc[hi]).dropna().sort_values()
        report += ["## 2. Welcher Stil verliert am wenigsten, wenn der Gegner grösser wird?", "",
                   f"Verlust an Erfolgsrate von {lo:.1f}x bis {hi:.1f}x Masse:", ""]
        report += [f"- {k}: {by_mass.loc[lo, k]:.0%} → {by_mass.loc[hi, k]:.0%} (−{v * 100:.0f} Punkte)" for k, v in drop.items()]
        report += ["", "Ein kleiner Verlust heisst: der Stil funktioniert auch gegen deutlich Grössere.", ""]

    # ---- 3) Entscheidende Techniken
    wins = df[df.success == 1]
    if "decisive_technique" in df.columns and len(wins):
        dec = pd.crosstab(wins.scenario, wins.decisive_technique.fillna("None"), normalize="index").reindex(index=scen)
        dec = dec.rename(columns={"None": "(nur Bewegung)"})
        dec = dec[dec.sum().sort_values(ascending=False).index[:12]]
        fig, ax = plt.subplots(figsize=(1.0 * dec.shape[1] + 3, 0.6 * len(scen) + 2.5))
        im = heatmap(ax, dec.rename(index=SCENARIO_DE), "Entscheidende Technik vor der Flucht (Anteil der Erfolge)", vmax=max(0.01, dec.values.max()))
        fig.colorbar(im, ax=ax); fig.tight_layout(); fig.savefig(out / "3_entscheidende_techniken.png", dpi=150); plt.close(fig)
        report += ["## 3. Welche Technik hat die Flucht ermöglicht?", ""]
        for s in scen:
            if s in dec.index:
                top = dec.loc[s].sort_values(ascending=False).head(3)
                report.append(f"- **{SCENARIO_DE[s]}**: " + ", ".join(f"{k} ({v:.0%})" for k, v in top.items() if v > 0))
        report.append("")

    # ---- 4) Technik-Wirksamkeit
    rows = []
    for s in scen:
        sub = df[df.scenario == s]
        for t in techs:
            used = sub[f"uses_{t}"].sum()
            if used == 0:
                continue
            rows.append({
                "szenario": s, "technik": t, "einsaetze": int(used),
                "treffer_rate": sub[f"hits_{t}"].sum() / used,
                "loest_griff_rate": sub[f"releases_{t}"].sum() / used if f"releases_{t}" in sub else float("nan"),
                "wirft_um_rate": sub[f"knockdowns_{t}"].sum() / used if f"knockdowns_{t}" in sub else float("nan"),
                "erfolg_wenn_benutzt": sub[sub[f"uses_{t}"] > 0].success.mean(),
                "erfolg_wenn_nicht": sub[sub[f"uses_{t}"] == 0].success.mean(),
            })
    tt = pd.DataFrame(rows)
    if len(tt):
        tt["lift"] = tt.erfolg_wenn_benutzt - tt.erfolg_wenn_nicht
        tt.round(3).to_csv(out / "technik_tabelle.csv", index=False)
        eff = tt.pivot_table(index="szenario", columns="technik", values="loest_griff_rate").reindex(index=scen)
        eff = eff.loc[:, eff.notna().any()]
        if eff.shape[1]:
            fig, ax = plt.subplots(figsize=(0.8 * eff.shape[1] + 3, 0.6 * len(scen) + 2.5))
            im = heatmap(ax, eff.rename(index=SCENARIO_DE), "Griff gelöst pro Einsatz (Wirksamkeit)", vmax=max(0.01, eff.max().max()))
            fig.colorbar(im, ax=ax); fig.tight_layout(); fig.savefig(out / "4_technik_wirksamkeit.png", dpi=150); plt.close(fig)
        report += ["## 4. Wirksamste Techniken pro Lage (mind. 30 Einsätze)", "",
                   "| Szenario | Technik | löst Griff | wirft um | Erfolg mit / ohne |", "|---|---|---|---|---|"]
        for s in scen:
            sub = tt[(tt.szenario == s) & (tt.einsaetze >= 30)].sort_values("loest_griff_rate", ascending=False).head(3)
            for _, r in sub.iterrows():
                report.append(f"| {SCENARIO_DE[s]} | {r.technik} | {r.loest_griff_rate:.0%} | {r.wirft_um_rate:.0%} | "
                              f"{r.erfolg_wenn_benutzt:.0%} / {r.erfolg_wenn_nicht:.0%} |")
        report.append("")

    # ---- Wand
    if "time_pinned_wall" in df.columns:
        pinned = df.groupby("style").time_pinned_wall.mean().reindex(styles)
        report += ["## 5. Zeit an der Wand fixiert (Ø Sekunden pro Episode)", ""]
        report += [f"- {k}: {v:.1f} s" for k, v in pinned.items() if pd.notna(v)]
        report.append("")

    report += ["## Vorsicht bei der Interpretation", "",
               "- Die Ergebnisse gelten für **dieses Modell**. Die Wirkung jeder Technik ist eine Annahme "
               "(docs/MODELL.md). Vor allem `MassScaling` und `StrengthScaling` bestimmen, welche Stile gegen Grössere gewinnen.",
               "- Prüfen: bleibt die Rangfolge gleich, wenn diese Parameter ±30 % verändert werden (Sensitivitätsanalyse)?",
               "- „Erfolg wenn benutzt“ ist eine Korrelation, keine Ursache: lange Episoden enthalten mehr Techniken.",
               "- Nur Unterschiede deutlich über dem Zufall interpretieren (bei n=200 pro Zelle ca. ±7 Prozentpunkte)."]
    (out / "bericht.md").write_text("\n".join(report), encoding="utf-8")
    print("\n".join(report))
    print(f"\nDateien in {out}")


if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.exit("Aufruf: python analysis/analyze_episodes.py <selfdefense_episodes.csv>")
    main(sys.argv[1])
