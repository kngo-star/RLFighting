# RLFighting – Selbstverteidigung unter Grössenunterschied (Unity ML-Agents)

Reinforcement-Learning-Projekt: Eine kleinere, schwächere Person (die **Verteidigerin**) steckt
in einer Notlage fest (gepackt, gegen die Wand gedrückt, gewürgt, am Boden …). Ein PPO-Agent lernt,
welche **Entscheidungen und Techniken** am zuverlässigsten zur **Befreiung und Flucht** führen –
und wie sich das mit dem **Grössen- und Kraftverhältnis** zum Angreifer verändert.

> **Wichtig:** Die Simulation ist ein abstraktes Modell mit festgelegten Annahmen (siehe
> [`docs/MODELL.md`](docs/MODELL.md)). Ergebnisse sind Einsichten über Strategien *in diesem Modell*,
> keine Trainingsempfehlung für echte Selbstverteidigung.

## Forschungsfrage

*Welche Strategien entwickelt ein kleinerer, schwächerer Agent gegen einen deutlich grösseren
Angreifer, welche Technik-Sets (Kampfstile) funktionieren in welcher Notlage am besten,
und ab welchem Grössenverhältnis ändert sich die beste Strategie?*

## Aufbau

```
Assets/SelfDefenseRL/
  Scripts/
    Enums.cs              Szenarien, Stile, Grifftypen, Bewegungen
    TechniqueLibrary.cs   20 abstrakte Techniken mit Parametern + Stil-Zuordnung
    CombatSimulation.cs   Kampfmodell in reinem C# (Angreifer-KI, Griffe, Schaden, Flucht)
    DefenderAgent.cs      ML-Agents-Agent: Observations, Action Masking, Rewards, Statistik
    ArenaView.cs          Darstellung (Kapseln, Farben, Griff-Linie, Text)
    EpisodeCsvLogger.cs   CSV-Log jeder Episode für die Auswertung
  Editor/
    SceneBuilder.cs       Menü "SelfDefenseRL" → baut die Szenen automatisch
config/
  selfdefense_ppo.yaml          Basis-Training (alles zufällig gemischt)
  selfdefense_curriculum.yaml   Curriculum: Gegner wird schrittweise grösser
  styles/style_*.yaml           ein Training pro Kampfstil (fairer Vergleich)
scripts/make_style_configs.py   erzeugt die Stil-Configs neu
analysis/analyze_episodes.py    Heatmaps & Tabellen aus der CSV
tools/SimCheck.cs               Plausibilitätscheck des Modells ohne Unity
docs/MODELL.md                  alle Modellannahmen + Reward-Design
```

## RL-Design in Kürze

| | |
|---|---|
| **Algorithmus** | PPO (ML-Agents), optional Curriculum Learning |
| **Observations** | 39 Werte: Szenario, Stil, Masse/Kraft-Verhältnis, eigener Zustand, Grifftyp, Zustand des Angreifers, Positionen, Platz bis zur Wand, Zeit |
| **Actions** | 2 diskrete Branches: Bewegung (5) und Technik (20) |
| **Action Masking** | nur Techniken des gewählten Stils, die in der aktuellen Lage möglich sind |
| **Ziel** | Griff lösen → mindestens 3 m Abstand → Flucht (+1) |
| **Episode** | max. 30 s, Entscheidung alle 0.1 s |

Szenarien: `WristGrab`, `PinnedToWall`, `BearHugBehind`, `ChokeFront`, `GroundMount`, `CorneredStrikes`
Stile: `Untrained` (Baseline), `Boxing`, `MuayThai`, `BJJ`, `Judo`, `KravMaga`, `WingTsun`, `Mixed`

## Einrichtung (einmalig)

1. **Unity 6.6** (`6000.6.7f1`, steht in `ProjectSettings/ProjectVersion.txt`) über Unity Hub installieren. Alle im Team sollten dieselbe Version nutzen.
2. Repo klonen:
   ```bash
   git clone https://github.com/kngo-star/RLFighting.git
   ```
3. Unity Hub → **Add → Add project from repository** (oder nach dem Klonen **Add project from disk**).
   Fragt der Hub nach der Editor-Version, wähl deine installierte Unity-6-Version und bestätige das Upgrade.
   Unity erzeugt fehlende Projektdateien selbst und installiert das Package `com.unity.ml-agents`
   4.0.0 (steht in `Packages/manifest.json`). Falls der Package Manager meckert, die Version dort löschen und neu installieren:
   *Window → Package Manager → + → Add package by name → `com.unity.ml-agents`*.
4. **Python 3.10** einrichten (ML-Agents braucht genau diese Version):
   ```bash
   python -m venv venv
   # Windows: venv\Scripts\activate    macOS/Linux: source venv/bin/activate
   pip install --upgrade pip
   pip install mlagents==1.1.0
   mlagents-learn --help
   ```
   Mit Conda geht auch: `conda create -n mlagents python=3.10.12 && conda activate mlagents`.

## Szene bauen

In Unity in der Menüleiste: **SelfDefenseRL → Trainings-Szene erstellen (16 Arenen)**.
Die Szene wird unter `Assets/SelfDefenseRL/Scenes/Training.unity` gespeichert.
Für eine einzelne Arena zum Anschauen: **SelfDefenseRL → Demo-Szene erstellen (1 Arena)**.

Darstellung: blaue Kapsel = Verteidigerin, rote Kapsel = Angreifer (grösser je nach Masseverhältnis),
gelb = Angreifer benommen, grau = Angreifer am Boden, orange Linie = aktiver Griff, grün = Flucht gelungen.

Ohne Training kannst du die regelbasierte Baseline anschauen: Defender auswählen →
*Behavior Parameters → Behavior Type = Heuristic Only* → Play.

## Training

Im Projektordner (venv aktiv):

```bash
# 1) Alles gemischt
mlagents-learn config/selfdefense_ppo.yaml --run-id=mixed_v1
# → wenn "Start training by pressing the Play button" erscheint: in Unity Play drücken

# 2) Curriculum (Gegner wächst mit dem Können)
mlagents-learn config/selfdefense_curriculum.yaml --run-id=curriculum_v1

# 3) Stil-Vergleich: ein Lauf pro Stil
mlagents-learn config/styles/style_BJJ.yaml --run-id=style_BJJ
mlagents-learn config/styles/style_KravMaga.yaml --run-id=style_KravMaga
# ... usw.

# Fortschritt ansehen
tensorboard --logdir results
```

Unterbrochenes Training fortsetzen: gleicher Befehl mit `--resume`.
In TensorBoard gibt es eigene Kurven unter **SelfDefense/** (Erfolgsrate pro Szenario und Stil,
Zeit bis zur Flucht, Technik-Nutzung).

## Auswertung

1. Trainiertes Modell `results/<run-id>/SelfDefense.onnx` nach `Assets/` ziehen.
2. **SelfDefenseRL → Auswertungs-Szene erstellen** (16 Arenen mit CSV-Log).
3. Bei allen Defender-Objekten (Mehrfachauswahl): *Behavior Parameters → Model* = das `.onnx`,
   *Behavior Type = Inference Only*. Optional `Fixed Style` setzen.
4. Play, ein paar Minuten laufen lassen. Der Pfad der CSV steht in der Unity-Konsole.
5. ```bash
   pip install pandas matplotlib
   python analysis/analyze_episodes.py <pfad>/selfdefense_episodes.csv
   ```
   → Heatmap *Szenario × Stil*, Erfolg über das Grössenverhältnis, Top-Techniken pro Szenario.

## Modell ohne Unity testen

```bash
mcs -out:simcheck.exe tools/SimCheck.cs Assets/SelfDefenseRL/Scripts/Enums.cs \
    Assets/SelfDefenseRL/Scripts/TechniqueLibrary.cs Assets/SelfDefenseRL/Scripts/CombatSimulation.cs
mono simcheck.exe 1.5 flee
```

Zeigt die Erfolgsrate einfacher Baseline-Politiken. Nützlich nach jeder Änderung an den
Modellparametern (siehe `docs/MODELL.md`).

## Ideen für Erweiterungen

- Sensitivitätsanalyse: Technik-Parameter ±30 % und prüfen, ob die Rangfolge der Stile stabil bleibt
- Angreifer ebenfalls per RL trainieren (Self-Play / MA-POCA) statt Skript
- Vorphase mit Situationsbewusstsein, Abstand halten und lautem Widerstand
- Physikbasierte Ragdolls (ArticulationBody) statt abstrakter Techniken
