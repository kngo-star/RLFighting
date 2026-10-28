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

## 6. Plausibilitätscheck (ohne RL)

`tools/SimCheck.cs`, Politik *flee* (zufällige erlaubte Technik solange festgehalten, sonst sofort weglaufen),
2000 Episoden pro Zelle, `massRatio = 1.5`:

| Szenario | Untrained | Boxing | MuayThai | BJJ | Judo | KravMaga | WingTsun | Mixed |
|---|---|---|---|---|---|---|---|---|
| WristGrab | 34 % | 36 % | 86 % | 59 % | 66 % | 93 % | 92 % | 85 % |
| PinnedToWall | 1 % | 2 % | 58 % | 32 % | 44 % | 83 % | 83 % | 62 % |
| BearHugBehind | 6 % | 0 % | 64 % | 60 % | 78 % | 82 % | 88 % | 68 % |
| ChokeFront | 10 % | 6 % | 75 % | 37 % | 50 % | 86 % | 89 % | 76 % |
| GroundMount | 0 % | 0 % | 31 % | 50 % | 72 % | 71 % | 76 % | 53 % |
| CorneredStrikes | 6 % | 6 % | 53 % | 21 % | 27 % | 75 % | 76 % | 56 % |

Beobachtungen:
- Der Grössenunterschied wirkt stark (bei 1.0 sind alle Werte deutlich höher, bei 1.8 tiefer).
- Stile mit Zielen hoher Wirkung (Krav, WingTsun) liegen vorne – **das folgt direkt aus der Annahme
  `MassScaling ≈ 0` für KneeGroin/FaceStrike**. Genau das muss im Bericht kritisch diskutiert werden.
- `Mixed` liegt bei zufälliger Wahl unter Krav, weil schlechte Techniken die guten verwässern.
  Ein RL-Agent sollte das lernen und mindestens so gut werden wie der beste Einzelstil.
- Der RL-Agent muss deutlich über dieser Baseline liegen, sonst lernt er nichts Sinnvolles.
