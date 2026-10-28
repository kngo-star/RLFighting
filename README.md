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
    TechniqueLibrary.cs   29 abstrakte Techniken (inkl. Strassen-Tricks), Stil-Tabelle (15 Kampfstile)
    CameraFollow.cs       Demo-Kamera: folgt seitlich, wackelt bei Treffern
    CombatSimulation.cs   Kampfmodell in reinem C# (Angreifer-KI, Griffe, Schaden, Flucht)
    DefenderAgent.cs      ML-Agents-Agent: Observations, Action Masking, Rewards, Statistik
    ArenaView.cs          Darstellung (Figuren, Posen, Farben, Griff-Linie, Text)
    StatsHUD.cs           Live-Statistik im Game-Fenster (Taste H)
    HumanoidFigure.cs     menschliche Figur: Gelenke, Posen, Treffer-Physik, Schritte, Schadensfarben
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
| **Observations** | 47 Werte: Szenario, Stil, Masse/Kraft-Verhältnis, eigener Zustand, Grifftyp, Zustand des Angreifers, Positionen, Platz bis zur Wand, Zeit |
| **Actions** | 2 diskrete Branches: Bewegung (5) und Technik (29) |
| **Action Masking** | nur Techniken des gewählten Stils, die in der aktuellen Lage möglich sind |
| **Ziel** | Griff lösen → mindestens 3 m Abstand → Flucht (+1) |
| **Episode** | max. 30 s, Entscheidung alle 0.1 s |

Szenarien: `WristGrab`, `PinnedToWall`, `BearHugBehind`, `ChokeFront`, `GroundMount`, `CorneredStrikes`
Strassen-Tricks (Beissen, Augen stechen, Tritt in die Hoden, Kopfstoss) per `dirty_tricks: 1` für alle Stile.

Stile (15): `Untrained` (Baseline), `Boxing`, `Kickboxing`, `MuayThai`, `Karate`, `Taekwondo`, `BJJ`, `JuJutsu`, `Judo`,
`Wrestling`, `Aikido`, `KravMaga`, `WingTsun`, `MMA`, `Mixed` (alle Techniken). Welche Technik zu welchem Stil gehört:
`TechniqueLibrary.StyleTable` bzw. Tabelle in `docs/MODELL.md`.

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

Darstellung: blaue Figur = Verteidigerin, rote Figur = Angreifer (grösser und breiter je nach Masseverhältnis).
Die Figuren nehmen zu jeder Technik eine passende Haltung ein (Schlag, Knie, Abstossen, Würgen, am Boden …);
gelb = Angreifer benommen, grau = Angreifer am Boden, orange Linie = aktiver Griff, grün = Flucht gelungen.

Ohne Training kannst du die regelbasierte Baseline anschauen: Defender auswählen →
*Behavior Parameters → Behavior Type = Heuristic Only* → Play.

## Training

Im Projektordner (venv aktiv), dann jeweils in Unity **Play** drücken, sobald im Terminal
„Start training by pressing the Play button“ erscheint:

```bash
# EMPFOHLEN: ein Agent für alle 15 Stile (Stil steht in der Observation, gleiches Netz → fairer Vergleich)
mlagents-learn config/selfdefense_ppo.yaml --run-id=alle_stile_v1

# Alternative: Curriculum (Gegner wächst mit dem Können)
mlagents-learn config/selfdefense_curriculum.yaml --run-id=curriculum_v1

# Optional: ein eigener Agent pro Stil (15 Läufe, dauert lange)
mlagents-learn config/styles/style_MMA.yaml --run-id=style_MMA

# Sensitivitätsanalyse: in der Config mass_scaling_floor: 0.6 setzen und neu trainieren/auswerten

# Fortschritt ansehen
tensorboard --logdir results
```

Unterbrochenes Training fortsetzen: gleicher Befehl mit `--resume`. Schneller trainieren: Trainings-Szene als
Build exportieren und mit `--env=<pfad> --num-envs=4 --no-graphics` starten.

## Wie lernt der Agent? (Daten, Training, wird er immer besser?)

- **Man braucht keine Daten.** Reinforcement Learning erzeugt seine Daten selbst: Die Verteidigerin spielt Tausende
  Episoden, probiert Bewegungen und Techniken aus und bekommt Belohnungen (Flucht +1, Griff gelöst +0.2, Treffer −…).
  `mlagents-learn` sammelt diese Erfahrungen und verbessert damit das neuronale Netz (PPO).
- **Ablauf:** Training starten → in Unity Play → laufen lassen (z. B. 6 Mio. Schritte, je nach Rechner 1–4 h mit 16 Arenen)
  → in TensorBoard die Kurven `Environment/Cumulative Reward` und `SelfDefense/Success` beobachten.
- **Wird sie immer besser?** Ja, aber nur bis zu einer Grenze. Am Anfang wählt sie zufällig, dann steigt die Erfolgsrate
  und flacht irgendwann ab (Plateau). Ab dort hat sie die beste Strategie gefunden, *die ihr Körper und ihr Stil erlauben*:
  Sie wird klüger (Timing, Abstand, Technikwahl), aber nicht stärker. Gegen einen sehr viel Grösseren bleibt die
  Erfolgsrate deshalb tiefer, genau das wollen wir messen.
- Der Angreifer ist ein festes Skript und lernt nicht mit. Erweiterung: Angreifer ebenfalls mit RL trainieren (Self-Play),
  dann verbessern sich beide gegenseitig.
- Ergebnis jedes Trainings ist eine `.onnx`-Datei in `results/<run-id>/`. Die zieht man in Unity auf den Defender
  (*Behavior Parameters → Model*) und sieht die gelernte Strategie.

## Darstellung und Physik

Die Figuren sind prozedural animiert (keine Motion-Capture-Animationen wie in EA UFC):
- **Treffer-Physik:** Jeder Treffer gibt den Gelenken einen Impuls, eine gedämpfte Feder zieht sie zurück. Ein Schlag
  ins Gesicht lässt den Kopf zurückschnappen, ein Kniestoss klappt den Rumpf ein, ein Low Kick knickt das Bein ein.
  Einstellbar am Figur-Objekt (*Treffer-Physik*: Steifigkeit, Dämpfung, Stärke).
- **Verletzungsanzeige pro Körperteil** (Kopf, Rumpf, Leiste, linker/rechter Arm, linkes/rechtes Bein):
  **grün = unverletzt, gelb = angeschlagen, rot = schwer verletzt**; beim Treffer blitzt das Teil weiss auf.
  Die Teamfarbe steckt in Stirnband, Armbändern und Bodenring (blau = Verteidigerin, rot = Angreifer,
  violett = benommen, grau = am Boden, türkis = geflohen).
- **Gelenke sichtbar:** Nacken, Schultern, Ellbogen, Handgelenke, Hüften, Knie und Knöchel sind als dunkle Kugeln zu sehen.
- **Demo-Szene:** Kamera folgt seitlich aus der Nähe und wackelt bei harten Treffern. Im StatsHUD gibt es **Zeitlupe**
  (1x, 0.5x, 0.25x, 0.1x), *Kamera folgt* und *Strassen-Tricks für alle*.
- **Beinarbeit:** Beim Bewegen machen die Figuren Schritte. Wohin und wie weit sie gehen (Abstand halten, ausweichen,
  in Tritt-Distanz gehen), entscheidet der RL-Agent.
- Die Kampflogik bleibt das Modell in `CombatSimulation.cs`. Volle Ragdoll-Physik (ArticulationBody), bei der der Agent
  jedes Gelenk selbst steuert, wäre ein eigenes, viel grösseres Projekt.

## Auswertung: Wie findet man heraus, welche Technik am besten ist?

Es gibt drei Stellen, an denen man Ergebnisse ablesen kann:

**1. Live in Unity (StatsHUD):** In jeder Szene oben links im Game-Fenster. Taste **H** blendet ein/aus.
- *Übersicht*: Erfolgsrate pro Stil, pro Szenario und pro Grössenverhältnis
- *Stil × Szenario*: Tabelle, welcher Stil in welcher Lage funktioniert
- *Techniken*: wie oft benutzt, Trefferquote, wie oft sie einen Griff löst, wie oft sie umwirft, und wie oft sie die
  **entscheidende** Technik vor der Flucht war. *Speichern* schreibt alles als Textdatei.

**2. Während des Trainings (TensorBoard):** `tensorboard --logdir results` → Bereich **SelfDefense/**
(Erfolg pro Szenario und Stil, Zeit bis zur Flucht, Zeit an der Wand, Technik-Nutzung, entscheidende Techniken).
Steigt die Erfolgskurve, lernt der Agent.

**3. Systematische Auswertung (für den Bericht):**
1. Trainiertes Modell `results/<run-id>/SelfDefense.onnx` nach `Assets/` ziehen.
2. **SelfDefenseRL → Auswertungs-Szene erstellen** (16 Arenen, geht automatisch alle Kombinationen
   Szenario × Stil × Grösse 1.0–1.8 durch und schreibt jede Episode in eine CSV).
3. Alle Defender auswählen: *Behavior Parameters → Model* = das `.onnx`, *Behavior Type = Inference Only*.
4. Play, ca. 10 Minuten laufen lassen. Der Pfad der CSV steht in der Unity-Konsole.
5. ```bash
   pip install pandas matplotlib
   python analysis/analyze_episodes.py <pfad>/selfdefense_episodes.csv
   ```
   → `analysis/output/`: Heatmaps (Stil × Szenario, entscheidende Techniken, Wirksamkeit pro Technik),
   Kurve über das Grössenverhältnis, `technik_tabelle.csv` und ein automatischer **`bericht.md`**.

**Welcher Kampfstil ist am effektivsten?** → Rangliste im StatsHUD (Tab *Übersicht*) bzw. Abschnitt 1 und 2 im `bericht.md`.
Die Antwort hat zwei Teile: *im Durchschnitt* (Rangliste) und *gegen sehr viel Grössere* (wer verliert am wenigsten von 1.0x zu 1.8x).

Eine Technik ist „am besten“, wenn sie oft **entscheidend** ist *und* eine hohe **löst-Griff-Rate** hat.
Immer mit der Baseline (ohne RL) vergleichen und die Modellannahmen in `docs/MODELL.md` mitdiskutieren.

## Modell ohne Unity testen

```bash
mcs -out:simcheck.exe tools/SimCheck.cs Assets/SelfDefenseRL/Scripts/Enums.cs \
    Assets/SelfDefenseRL/Scripts/TechniqueLibrary.cs Assets/SelfDefenseRL/Scripts/CombatSimulation.cs
mono simcheck.exe 1.5 flee
mono simcheck.exe 1.5 flee baseline.csv   # Baseline-CSV für analysis/analyze_episodes.py
```

Zeigt die Erfolgsrate einfacher Baseline-Politiken. Nützlich nach jeder Änderung an den
Modellparametern (siehe `docs/MODELL.md`).

## Ideen für Erweiterungen

- Sensitivitätsanalyse: Technik-Parameter ±30 % und prüfen, ob die Rangfolge der Stile stabil bleibt
- Angreifer ebenfalls per RL trainieren (Self-Play / MA-POCA) statt Skript
- Vorphase mit Situationsbewusstsein, Abstand halten und lautem Widerstand
- Physikbasierte Ragdolls (ArticulationBody) statt abstrakter Techniken

## Hinweis: ML-Agents ist eingebettet

`Packages/com.unity.ml-agents` ist eine Kopie von ML-Agents 4.0.0 (Release 23) mit einer Ein-Zeilen-Korrektur
(`GetInstanceID()` → `GetHashCode()` in `Match3ActuatorComponent.cs`). Ab Unity 6.5 ist `GetInstanceID()` ein
Kompilierfehler, ohne die Korrektur kompiliert das Projekt nicht. Python-Seite bleibt `mlagents==1.1.0` (gleiche Kommunikations-API 1.5.0).
