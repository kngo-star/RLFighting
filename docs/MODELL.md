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

## 3. Techniken und Kampfstile (`TechniqueLibrary.cs`)

Jeder Stil = eine Teilmenge der Techniken (Action Masking). Die Zuordnung steht zentral in `TechniqueLibrary.StyleTable`
und lässt sich dort leicht ändern. Diese Tabellen werden aus dem Code erzeugt.

| Technik | Beschreibung | MassScaling | StrengthScaling | Wirkung |
|---|---|---|---|---|
| PalmStrike | Handballenstoss ins Gesicht | 0.8 | – | Schaden 0.12, Abstand 0.2 m |
| JabCross | Gerade Schläge (Jab, Cross) | 0.9 | – | Schaden 0.10 |
| ReversePunch | Karate-Fauststoss (Gyaku-Zuki) mit Hüfteinsatz | 0.85 | – | Schaden 0.12 |
| HammerFist | Hammerfaust, auch von unten | 0.7 | – | Schaden 0.08 |
| Elbow | Ellbogenschlag (auch nach hinten) | 0.7 | 0.5 | Schaden 0.15, Griff lösen 5% |
| KneeGroin | Kniestoss in Leiste/Körper | 0.15 | 0.3 | Schaden 0.30, Griff lösen 10% |
| FaceStrike | Schlag auf Nase/Gesicht | 0.0 | 0.2 | Schaden 0.25, Griff lösen 15% |
| KickKnee | Tritt gegen Knie/Schienbein, Fussstampfer | 0.4 | – | Schaden 0.15, Gleichgewicht 0.2 |
| FrontKick | Frontaltritt / Teep (stösst weg) | 0.6 | – | Schaden 0.10, Abstand 0.9 m |
| SideKick | Seitwärtstritt (Yop Chagi / Yoko-Geri) | 0.6 | – | Schaden 0.14, Gleichgewicht 0.2, Abstand 1.1 m |
| RoundKickThigh | Halbkreistritt auf den Oberschenkel (Low Kick) | 0.7 | – | Schaden 0.15, Gleichgewicht 0.15 |
| GripReleaseThumb | Griff am Daumen lösen | – | 0.3 | Griff lösen 55% |
| WristLock | Handgelenkhebel mit Wurf (Kote-Gaeshi) | 0.3 | 0.2 | Griff lösen 50%, Gleichgewicht 0.35 |
| ChokeDefense | Würgegriff aufreissen und wegdrehen | – | 0.5 | Griff lösen 45% |
| DropWeight | Gewicht senken, Hüfte raus | 0.0 | 0.35 | Griff lösen 35%, Gleichgewicht 0.1 |
| Underhook | Unterhaken und von der Wand wegdrehen | – | 0.5 | Griff lösen 15%, dreht von der Wand weg |
| Frame | Unterarm-Rahmen gegen Hals/Schulter | 0.5 | 0.6 | Griff lösen 20%, Abstand 0.6 m |
| PushOff | Wegstossen (ohne Training) | 0.8 | 0.8 | Griff lösen 25%, Abstand 1.2 m |
| HipThrow | Hüftwurf (O-Goshi) | 0.5 | – | Gleichgewicht 1.2 |
| BridgeRoll | Brücke und Rolle aus dem Mount (Upa) | 0.45 | – | Gleichgewicht 1.3, löst Mount |
| HipEscape | Hüftflucht (Shrimp) | 0.3 | 0.4 | Griff lösen 35%, Abstand 0.5 m |
| StandUp | Technisch aufstehen | – | – | aufstehen |
| Bite | Beissen in Arm/Hand, die festhält | 0.0 | 0.15 | Schaden 0.06, Griff lösen 30% |
| EyePoke | Finger in die Augen | 0.0 | 0.1 | Schaden 0.20, Griff lösen 20% |
| GroinKick | Tritt in die Hoden | 0.1 | 0.3 | Schaden 0.30, Griff lösen 15% |
| Headbutt | Kopfstoss (nach vorne oder hinten) | 0.5 | 0.5 | Schaden 0.15, Griff lösen 10% |
| Guard | Deckung, Kopf schützen | – | – | Schaden ×0.4 |

Jede Technik hat ausserdem Dauer, Trefferquote, Ausdauerkosten und **Exposure**
(wie viel leichter man während der Ausführung getroffen wird).

| Stil | Techniken (zusätzlich zu None, Guard, PushOff, StandUp) |
|---|---|
| Untrained | HammerFist |
| Boxing | JabCross, Frame |
| Kickboxing | JabCross, KneeGroin, KickKnee, FrontKick, RoundKickThigh |
| MuayThai | JabCross, Elbow, KneeGroin, FrontKick, RoundKickThigh, Underhook, Frame |
| Karate | PalmStrike, ReversePunch, HammerFist, Elbow, KickKnee, FrontKick, SideKick |
| Taekwondo | ReversePunch, KickKnee, FrontKick, SideKick, RoundKickThigh |
| BJJ | GripReleaseThumb, ChokeDefense, DropWeight, Underhook, Frame, HipThrow, BridgeRoll, HipEscape |
| JuJutsu | PalmStrike, Elbow, KneeGroin, KickKnee, GripReleaseThumb, WristLock, ChokeDefense, DropWeight, HipThrow, BridgeRoll, EyePoke, GroinKick |
| Judo | GripReleaseThumb, ChokeDefense, DropWeight, Underhook, Frame, HipThrow, BridgeRoll |
| Wrestling | GripReleaseThumb, DropWeight, Underhook, Frame, HipThrow, BridgeRoll, HipEscape |
| Aikido | GripReleaseThumb, WristLock, HipThrow |
| KravMaga | PalmStrike, JabCross, HammerFist, Elbow, KneeGroin, FaceStrike, KickKnee, FrontKick, GripReleaseThumb, ChokeDefense, DropWeight, EyePoke, GroinKick, Headbutt |
| WingTsun | PalmStrike, JabCross, Elbow, FaceStrike, KickKnee, GripReleaseThumb, EyePoke, GroinKick |
| MMA | JabCross, Elbow, KickKnee, FrontKick, RoundKickThigh, GripReleaseThumb, ChokeDefense, DropWeight, Underhook, Frame, HipThrow, BridgeRoll, HipEscape |
| Mixed | alle |

**Strassen-Tricks** (Bite, EyePoke, GroinKick, Headbutt): Krav Maga, Wing Tsun und Ju-Jutsu haben einen Teil davon im
Lehrplan. Mit `dirty_tricks: 1` (Config) bzw. dem Knopf *Strassen-Tricks für alle* im StatsHUD bekommt **jeder** Stil
alle vier zusätzlich. So lässt sich vergleichen: Welcher Stil ist am besten *mit* und *ohne* schmutzige Tricks?

Bewusste Entscheidungen, die im Bericht diskutiert werden sollten:
- **MMA** ohne Leisten- und Augenangriffe (Sport-Regelwerk). Selbstverteidigungs-Systeme (Krav Maga, Wing Tsun, Ju-Jutsu) haben sie.
- **Aikido** nur mit Hebeln und Würfen, **Boxen** nur mit Schlägen und Rahmen.
- **Mixed** = alle Techniken. Das ist die Obergrenze: so gut kann ein Agent mit *allem* werden.
- „Untrained“ ist die Baseline: Deckung, Wegstossen, Hammerfaust, Aufstehen.

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
- **Underhook** (Unterhaken) kann im Clinch/Würgen die Position drehen: danach steht der Angreifer an der Wand und hält nur noch den Arm.
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

## 8. Plausibilitätscheck und Sensitivität (ohne RL)

`tools/SimCheck.cs`, Politik *flee* (zufällige erlaubte Technik solange festgehalten, sonst sofort weglaufen),
1000 Episoden pro Zelle, alle Grössen 1.0–1.8. Rangliste der Stile (Erfolgsrate über alle Szenarien):

| Annahme | Rangliste |
|---|---|
| Standard (`mass_scaling_floor = 0`) | WingTsun 88 %, KravMaga 86 %, JuJutsu 79 %, Mixed 65 %, MuayThai 63 %, Judo 55 %, MMA 49 %, Kickboxing 49 %, Karate 46 %, BJJ 45 %, Wrestling 45 %, Taekwondo 28 %, Boxing 20 %, Untrained 17 %, Aikido 15 % |
| Keine Technik kraftunabhängig (`floor = 0.6`) | WingTsun 83 %, KravMaga 81 %, JuJutsu 74 %, MuayThai 61 %, Mixed 59 %, Judo 51 %, Kickboxing 47 %, MMA 46 %, Karate 45 %, Wrestling 43 %, BJJ 42 %, Taekwondo 26 %, Boxing 20 %, Untrained 17 %, Aikido 14 % |

Mit und ohne Strassen-Tricks (Masse 1.0–1.8, Zufallspolitik *flee*):

| Modus | Rangliste | entscheidende Techniken |
|---|---|---|
| ohne (`dirty_tricks: 0`) | WingTsun 92 %, KravMaga 89 %, JuJutsu 83 %, Mixed 74 %, MuayThai 63 %, Judo 54 %, MMA 49 %, Karate 48 %, Kickboxing 48 %, Wrestling 45 %, BJJ 44 %, Taekwondo 28 %, Boxing 20 %, Untrained 16 %, Aikido 15 % | |
| mit (`dirty_tricks: 1`) | WingTsun 90 %, KravMaga 89 %, Kickboxing 89 %, Taekwondo 84 %, JuJutsu 81 %, MuayThai 81 %, Karate 80 %, Untrained 76 %, Boxing 74 %, Mixed 74 %, Aikido 71 %, Judo 71 %, Wrestling 68 %, MMA 67 %, BJJ 67 % | EyePoke 24 %, GroinKick 16 %, Bite 11 %, Headbutt 10 % |

→ Im Modell gleichen Strassen-Tricks die Unterschiede zwischen den Stilen stark aus: auch „Untrained“ springt von 16 % auf 76 %.
Das ist eine direkte Folge der Annahme, dass diese Tricks fast unabhängig von der Kraft wirken (`MassScaling ≈ 0`, hoher `StunFactor`).

Beobachtungen:
- Die Rangfolge bleibt bei der Sensitivitätsanalyse stabil, die Werte sinken nur leicht. Treiber ist eher der
  **StunFactor** (Knie, Augen → lange Benommenheit → Griff lockert sich) als das MassScaling. Das ist die nächste
  Annahme, die variiert werden sollte.
- **Achtung, Zufallspolitik:** Stile mit wenigen, aber starken Techniken (Wing Tsun) sind hier bevorzugt; Stile mit vielen
  Techniken (MMA, Mixed) werden durch schlechte Zufallswahl verwässert. Erst der **trainierte RL-Agent** wählt gezielt,
  deshalb ist erst *seine* Rangliste die eigentliche Antwort.
- Stile ohne Bodentechniken (Boxen, Taekwondo, Karate, Kickboxen) haben im Szenario *Am Boden* fast 0 %.
- Befehle: `mono simcheck.exe 1.5 flee` (Tabelle), `mono simcheck.exe 1.5 flee baseline.csv 0.6` (CSV mit Floor 0.6).
