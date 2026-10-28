# Modell und Annahmen

Dieses Dokument sammelt **alle Annahmen**, die das Ergebnis beeinflussen. Für den Projektbericht:
jede Zahl begründen (Quelle oder Plausibilität) und die wichtigsten per Sensitivitätsanalyse variieren.

## 1. Abstraktionsebene

Die Simulation ist **hierarchisch**: Der RL-Agent entscheidet *was* getan wird (Bewegung + Technik),
das Kampfmodell (`CombatSimulation.cs`) bestimmt mit Wahrscheinlichkeiten, *was dabei passiert*.
Es gibt keine Ragdoll-Physik. Vorteile: schnelles Training, nachvollziehbare Ergebnisse.
Nachteil: Das Ergebnis hängt direkt an den Parametern unten.

Arena: 8 × 8 m mit Wänden. Zeitschritt 0.02 s, Entscheidung alle 0.1 s, Episode max. 30 s.

## 2. Grössen- und Kraftverhältnis

- `massRatio` = Masse Angreifer / Masse Verteidigerin (Training: zufällig 1.0–1.8)
- `strengthRatio` = Kraft Angreifer / Kraft Verteidigerin, Standard: `massRatio^1.3 × 1.1`
  (Annahme: Kraftunterschied wächst schneller als der Gewichtsunterschied, v. a. Oberkörperkraft)

Wirkung einer Technik:

| Grösse | Formel |
|---|---|
| Schaden | `Damage × (1/massRatio)^MassScaling × Ausdauerfaktor × Zufall(0.7–1.2)` |
| Griff lösen | `GripBreak × (1/strengthRatio)^StrengthScaling × (1.5 − Griffstärke) × Ausdauerfaktor` (×1.8 wenn Angreifer benommen) |
| Gleichgewicht | `Balance × (1/massRatio)^MassScaling` → Angreifer fällt, wenn sein Gleichgewicht < 0 |
| Benommenheit | `StunFactor × Schaden × 6` Sekunden |

**Kernannahme:** `MassScaling` und `StrengthScaling` nahe 0 bedeuten *„funktioniert unabhängig von der
eigenen Kraft“* (z. B. Ziele mit hoher Verletzlichkeit, Hebel). Werte nahe 1 bedeuten *„Kraft gegen Kraft“*.
Diese beiden Zahlen pro Technik bestimmen weitgehend, welche Stile bei grossem Gegner gewinnen.
→ **Unbedingt im Bericht diskutieren und variieren.**

## 3. Techniken (`TechniqueLibrary.cs`)

| Technik | Stile | MassScaling | StrengthScaling | Wirkung |
|---|---|---|---|---|
| PalmStrike | Krav, WingTsun | 0.8 | – | Schaden 0.12 |
| JabCross | Boxing, MuayThai, Krav | 0.9 | – | Schaden 0.10 |
| HammerFist | Untrained, Krav | 0.7 | – | Schaden 0.08, auch von unten |
| Elbow | Krav, MuayThai, WingTsun | 0.7 | 0.5 | Schaden 0.15 |
| KneeGroin | Krav, MuayThai | 0.15 | 0.3 | Schaden 0.30, starke Benommenheit |
| FaceStrike | Krav, WingTsun | 0.0 | 0.2 | Schaden 0.25, geringe Trefferquote |
| KickKnee | Krav, WingTsun, MuayThai | 0.4 | – | Schaden + Gleichgewicht |
| RoundKickThigh | MuayThai | 0.7 | – | nur auf Distanz |
| GripReleaseThumb | Krav, BJJ, Judo, WingTsun | – | 0.3 | Griff lösen 55 % Basis |
| ChokeDefense | Krav, BJJ, Judo | – | 0.5 | Würgegriff lösen |
| DropWeight | BJJ, Judo, Krav | – | 0.35 | Umklammerung lösen |
| Frame | BJJ, MuayThai, Judo | 0.5 | 0.6 | Abstand + Griff lösen |
| PushOff | alle | 0.8 | 0.8 | Abstand, reine Kraft |
| HipThrow | Judo, BJJ | 0.5 | – | Gleichgewicht 1.2 → Wurf |
| BridgeRoll | BJJ, Judo | 0.45 | – | löst Mount bei Erfolg |
| HipEscape | BJJ | 0.3 | 0.4 | Abstand am Boden |
| StandUp | alle | – | – | vom Boden aufstehen |
| Guard | alle | – | – | Schaden ×0.4, Trefferchance −25 % |

Jede Technik hat ausserdem Dauer, Trefferquote, Ausdauerkosten und **Exposure**
(wie viel leichter man während der Ausführung getroffen wird). `Mixed` darf alles.

## 4. Angreifer (Skript, `CombatSimulation.UpdateAttacker`)

Entscheidet alle 0.4–0.8 s:
- **Mount:** schlägt (60 %) oder festigt den Griff
- **Würgen:** verursacht laufend Schaden, festigt den Griff, selten Takedown
- **Umklammerung / Clinch:** Takedown-Chance 12–16 % → Mount
- **Handgelenk:** zieht in den Clinch (25 %) oder schlägt (40 %)
- **frei:** verfolgt mit 3.0 m/s (Verteidigerin 3.5 m/s), packt (50 %) oder schlägt

Nach einem gelösten Griff braucht der Angreifer 0.6 s, um sich zu fangen.
Benommenheit und Schaden machen ihn langsamer und lockern den Griff.

## 5. Reward-Design (`DefenderAgent.cs`, im Inspector änderbar)

| Ereignis | Reward |
|---|---|
| Flucht gelungen (frei, stehend, ≥ 3 m Abstand) | +1.0 |
| Griff gelöst | +0.2 |
| Angreifer zu Boden | +0.1 |
| Angreifer kampfunfähig | +0.3 (einmal) |
| erlittener Schaden | −0.5 pro Schadenseinheit |
| Position verschlechtert (gepackt, Takedown, Mount) | −0.3 |
| verloren (Gesundheit 0 oder Zeit abgelaufen) | −1.0 |
| pro Entscheidung | −0.001 |

Die Zwischenbelohnungen (Griff gelöst, Knockdown) sind Reward Shaping. Für den Bericht lohnt sich
ein Vergleich: Training **mit** und **ohne** Shaping.

## 6. Wände und Körpergrösse

- Wände sind fest. Jede Figur hat einen Platzbedarf: Verteidigerin 0.28 m, Angreifer `0.3 × massRatio^0.6` m.
  Liegende Figuren brauchen 1 m Abstand zur Wand (Kopf und Füsse bleiben in der Arena).
- Im **Clinch** und beim **Würgen** drückt der Angreifer die Verteidigerin mit `0.6 m/s × √strengthRatio × Griffstärke`
  rückwärts, bis sie mit dem Rücken an der Wand steht. Gemessen wird `time_pinned_wall` (Sekunden an der Wand fixiert).
- Der Angreifer bleibt auf der Seite, von der er gegriffen hat. Passt er nicht mehr zwischen Verteidigerin und Wand,
  werden beide verschoben.
- Darstellung: Verteidigerin 92 % der Standardgrösse, Angreifer Höhe `massRatio^0.25`, Breite `massRatio^0.6`.

## 7. Kennzahlen für die Auswertung

| Kennzahl | Bedeutung |
|---|---|
| Erfolgsrate | Anteil Episoden mit gelungener Flucht (Hauptkennzahl) |
| löst-Griff-Rate | Griffe gelöst / Einsätze einer Technik → Wirksamkeit |
| wirft-um-Rate | Angreifer zu Boden / Einsätze |
| entscheidende Technik | letzte wirksame Aktion (Griff gelöst, umgeworfen, ≥ 0.5 s benommen) vor der Flucht → Wichtigkeit |
| Erfolg mit / ohne | Erfolgsrate der Episoden, in denen die Technik (nicht) benutzt wurde (Korrelation, keine Ursache!) |
| time_pinned_wall, times_grabbed, takedowns, damage_taken | wie die Episode verlaufen ist |

## 8. Plausibilitätscheck (ohne RL)

`tools/SimCheck.cs`, Politik *flee* (zufällige erlaubte Technik solange festgehalten, sonst sofort weglaufen),
2000 Episoden pro Zelle, `massRatio = 1.5`:

| Szenario | Untrained | Boxing | MuayThai | BJJ | Judo | KravMaga | WingTsun | Mixed |
|---|---|---|---|---|---|---|---|---|
| WristGrab | 28 % | 28 % | 85 % | 48 % | 53 % | 93 % | 93 % | 80 % |
| PinnedToWall | 1 % | 3 % | 63 % | 15 % | 16 % | 86 % | 87 % | 61 % |
| BearHugBehind | 6 % | 0 % | 65 % | 58 % | 76 % | 84 % | 90 % | 67 % |
| ChokeFront | 10 % | 6 % | 72 % | 24 % | 31 % | 88 % | 92 % | 73 % |
| GroundMount | 0 % | 0 % | 31 % | 47 % | 73 % | 75 % | 81 % | 54 % |
| CorneredStrikes | 7 % | 10 % | 64 % | 13 % | 13 % | 82 % | 84 % | 63 % |

Beobachtungen:
- Der Grössenunterschied wirkt stark, und seit die Wand fest ist, verlieren Grappling-Stile (BJJ, Judo) im Stand deutlich.
- Krav Maga und Wing Tsun liegen vorne – **das folgt direkt aus der Annahme `MassScaling ≈ 0` für KneeGroin/FaceStrike**.
  Genau das muss im Bericht kritisch diskutiert und per Sensitivitätsanalyse geprüft werden.
- `Mixed` liegt bei zufälliger Wahl unter Krav, weil schlechte Techniken die guten verwässern. Ein RL-Agent sollte
  mindestens so gut werden wie der beste Einzelstil.
- Der RL-Agent muss deutlich über dieser Baseline liegen, sonst lernt er nichts Sinnvolles.
- Baseline-CSV für die Auswertung: `mono simcheck.exe 1.5 flee baseline.csv` → `python analysis/analyze_episodes.py baseline.csv`
