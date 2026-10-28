using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SelfDefenseRL
{
    /// <summary>
    /// Live-Auswertung im Game-Fenster: sammelt alle beendeten Episoden aller Arenen und zeigt
    ///  - Übersicht: Erfolgsrate pro Stil, pro Szenario und pro Grössenverhältnis
    ///  - Stil × Szenario: Erfolgsraten-Tabelle
    ///  - Techniken: wie oft benutzt, wie oft sie einen Griff gelöst / den Angreifer umgeworfen haben,
    ///    und wie oft sie die entscheidende Technik vor der Flucht waren
    /// Taste/Knopf "Speichern" schreibt die Tabellen als Text-Datei (Pfad steht in der Konsole).
    /// </summary>
    public class StatsHUD : MonoBehaviour
    {
        public bool visible = true;
        public int fontSize = 13;

        class Counter { public int n, wins; public float Rate => n == 0 ? 0f : (float)wins / n; }

        readonly Counter total = new Counter();
        readonly Counter[] byStyle = NewCounters(EnumCounts.Styles);
        readonly Counter[] byScenario = NewCounters(EnumCounts.Scenarios);
        readonly Counter[,] byStyleScenario = new Counter[EnumCounts.Styles, EnumCounts.Scenarios];
        readonly Counter[] byMass = NewCounters(5);
        readonly long[] uses = new long[TechniqueLibrary.Count];
        readonly long[] hits = new long[TechniqueLibrary.Count];
        readonly long[] releases = new long[TechniqueLibrary.Count];
        readonly long[] knockdowns = new long[TechniqueLibrary.Count];
        readonly long[] decisive = new long[TechniqueLibrary.Count];
        readonly long[] wallTurns = new long[TechniqueLibrary.Count];
        float sumEscapeTime, sumPinned;

        int tab;
        Vector2 scroll;
        GUIStyle mono;
        static readonly string[] Tabs = { "Übersicht", "Stil × Szenario", "Techniken" };

        static Counter[] NewCounters(int n) { var a = new Counter[n]; for (int i = 0; i < n; i++) a[i] = new Counter(); return a; }

        void Awake()
        {
            for (int s = 0; s < EnumCounts.Styles; s++)
                for (int c = 0; c < EnumCounts.Scenarios; c++)
                    byStyleScenario[s, c] = new Counter();
        }

        void OnEnable() { DefenderAgent.EpisodeFinished += OnEpisode; }
        void OnDisable() { DefenderAgent.EpisodeFinished -= OnEpisode; }

        static int MassBin(float m) => Mathf.Clamp(Mathf.FloorToInt((m - 1.0f) / 0.2f + 0.5f), 0, 4);
        static string MassLabel(int b) => $"{1.0f + 0.2f * b:0.0}x";

        void OnEpisode(CombatSimulation s)
        {
            int w = s.Success ? 1 : 0;
            Add(total, w);
            Add(byStyle[(int)s.Style], w);
            Add(byScenario[(int)s.Scenario], w);
            Add(byStyleScenario[(int)s.Style, (int)s.Scenario], w);
            Add(byMass[MassBin(s.MassRatio)], w);
            for (int i = 1; i < TechniqueLibrary.Count; i++)
            {
                uses[i] += s.TechniqueUses[i];
                hits[i] += s.TechniqueHits[i];
                releases[i] += s.TechniqueReleases[i];
                knockdowns[i] += s.TechniqueKnockdowns[i];
                wallTurns[i] += s.TechniqueWallTurns[i];
            }
            if (s.Success) { decisive[s.DecisiveTechnique]++; sumEscapeTime += s.Time; }
            sumPinned += s.TimePinnedToWall;
        }

        static void Add(Counter c, int win) { c.n++; c.wins += win; }

        // ------------------------------------------------------------------ Anzeige
        void OnGUI()
        {
            if (mono == null)
            {
                mono = new GUIStyle(GUI.skin.label) { fontSize = fontSize, richText = true, wordWrap = false };
                var font = Font.CreateDynamicFontFromOSFont(new[] { "Menlo", "Consolas", "Courier New", "DejaVu Sans Mono" }, fontSize);
                if (font != null) mono.font = font; // Monospace, damit die Spalten sauber stehen
                mono.normal.textColor = Color.white;
            }

            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.H) { visible = !visible; e.Use(); }
            if (!visible)
            {
                if (GUI.Button(new Rect(10, 10, 130, 24), "Statistik (H)")) visible = true;
                return;
            }

            float w = Mathf.Min(640f, Screen.width - 20f), h = Mathf.Min(560f, Screen.height - 20f);
            GUI.Box(new Rect(10, 10, w, h), GUIContent.none);
            GUILayout.BeginArea(new Rect(18, 16, w - 16, h - 12));
            GUILayout.BeginHorizontal();
            tab = GUILayout.Toolbar(tab, Tabs);
            if (GUILayout.Button("Speichern", GUILayout.Width(80))) Save();
            if (GUILayout.Button("Reset", GUILayout.Width(55))) ResetStats();
            if (GUILayout.Button("–", GUILayout.Width(24))) visible = false;
            GUILayout.EndHorizontal();

            // Steuerung: Zeitlupe, Kamera, Strassen-Tricks
            GUILayout.BeginHorizontal();
            GUILayout.Label("Tempo:", GUILayout.Width(50));
            foreach (var ts in new[] { 1f, 0.5f, 0.25f, 0.1f })
                if (GUILayout.Toggle(Mathf.Approximately(Time.timeScale, ts), ts + "x", "Button", GUILayout.Width(44))) Time.timeScale = ts;
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (cam != null) cam.follow = GUILayout.Toggle(cam.follow, "Kamera folgt", "Button", GUILayout.Width(100));
            bool dirty = DefenderAgent.DirtyTricksOverride == 1;
            bool newDirty = GUILayout.Toggle(dirty, "Strassen-Tricks für alle", "Button", GUILayout.Width(170));
            if (newDirty != dirty) { DefenderAgent.DirtyTricksOverride = newDirty ? 1 : 0; ResetStats(); }
            GUILayout.EndHorizontal();
            GUILayout.Label("Körperteile: <color=#33c74d>grün = unverletzt</color>  <color=#f9d126>gelb = angeschlagen</color>  <color=#e61414>rot = schwer verletzt</color>   Bänder: blau/rot = Team, violett = benommen, grau = am Boden", mono);

            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label(tab == 0 ? Overview() : tab == 1 ? Matrix() : Techniques(), mono);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        string Overview()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<b>Episoden: {total.n}   Erfolg (Flucht): {total.Rate:P0}</b>");
            if (total.wins > 0) sb.AppendLine($"Ø Zeit bis zur Flucht: {sumEscapeTime / total.wins:0.0} s");
            if (total.n > 0) sb.AppendLine($"Ø Zeit an der Wand fixiert: {sumPinned / total.n:0.0} s");
            sb.AppendLine();
            sb.AppendLine("<b>Rangliste der Kampfstile (Erfolgsrate)</b>");
            int rank = 1;
            foreach (int i in Enumerable.Range(0, EnumCounts.Styles).OrderByDescending(i => byStyle[i].Rate))
                if (byStyle[i].n > 0) sb.AppendLine($"{rank++,2}. {((Style)i),-11} {Bar(byStyle[i].Rate)} {byStyle[i].Rate,4:P0}  (n={byStyle[i].n})");
            sb.AppendLine();
            sb.AppendLine("<b>Pro Szenario</b>");
            for (int i = 0; i < EnumCounts.Scenarios; i++)
                if (byScenario[i].n > 0) sb.AppendLine($"  {((Scenario)i),-16} {Bar(byScenario[i].Rate)} {byScenario[i].Rate,4:P0}  (n={byScenario[i].n})");
            sb.AppendLine();
            sb.AppendLine("<b>Pro Grössenverhältnis (Angreifer / Verteidigerin)</b>");
            for (int i = 0; i < byMass.Length; i++)
                if (byMass[i].n > 0) sb.AppendLine($"  {MassLabel(i),-5} {Bar(byMass[i].Rate)} {byMass[i].Rate,4:P0}  (n={byMass[i].n})");
            return sb.ToString();
        }

        string Matrix()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<b>Erfolgsrate: Zeilen = Stil, Spalten = Szenario</b>");
            sb.Append($"{"",-11}");
            string[] shortNames = { "Hand", "Wand", "Hinten", "Würgen", "Boden", "Ecke" };
            foreach (var n in shortNames) sb.Append($"{n,8}");
            sb.AppendLine();
            for (int s = 0; s < EnumCounts.Styles; s++)
            {
                if (byStyle[s].n == 0) continue;
                sb.Append($"{((Style)s),-11}");
                for (int c = 0; c < EnumCounts.Scenarios; c++)
                {
                    var k = byStyleScenario[s, c];
                    sb.Append(k.n == 0 ? $"{"–",8}" : $"{k.Rate,8:P0}");
                }
                sb.AppendLine();
            }
            sb.AppendLine();
            sb.AppendLine("Hand = Handgelenk gepackt, Wand = an die Wand gedrückt, Hinten = Umklammerung von hinten,");
            sb.AppendLine("Würgen = Würgegriff vorne, Boden = Angreifer sitzt oben, Ecke = in der Ecke mit Schlägen");
            return sb.ToString();
        }

        string Techniques()
        {
            var sb = new StringBuilder();
            long decTotal = decisive.Sum();
            sb.AppendLine("<b>Technik          benutzt  Treffer  löst Griff  wirft um  dreht  entscheidend</b>");
            foreach (int i in Enumerable.Range(1, TechniqueLibrary.Count - 1).OrderByDescending(i => decisive[i]).ThenByDescending(i => uses[i]))
            {
                if (uses[i] == 0) continue;
                float hitRate = (float)hits[i] / uses[i];
                float relRate = (float)releases[i] / uses[i];
                float decRate = decTotal == 0 ? 0f : (float)decisive[i] / decTotal;
                sb.AppendLine($"{TechniqueLibrary.All[i].Name,-16}{uses[i],8}{hitRate,9:P0}{relRate,12:P0}{knockdowns[i],10}{wallTurns[i],7}{decRate,13:P0}");
            }
            if (decTotal > 0 && decisive[0] > 0)
                sb.AppendLine($"{"(nur Bewegung)",-16}{"",8}{"",9}{"",12}{"",10}{"",7}{(float)decisive[0] / decTotal,13:P0}");
            sb.AppendLine();
            sb.AppendLine("löst Griff = Anteil der Einsätze, die einen Griff gelöst haben");
            sb.AppendLine("dreht = wie oft sie die Position an der Wand umgedreht hat (Underhook)");
            sb.AppendLine("entscheidend = Anteil der erfolgreichen Fluchten, bei denen diese Technik");
            sb.AppendLine("               die letzte wirksame Aktion war (Griff gelöst, umgeworfen, benommen)");
            return sb.ToString();
        }

        static string Bar(float rate)
        {
            int n = Mathf.RoundToInt(rate * 20f);
            return "<color=#5fd35f>" + new string('█', n) + "</color><color=#555555>" + new string('█', 20 - n) + "</color>";
        }

        void ResetStats()
        {
            total.n = total.wins = 0;
            foreach (var c in byStyle) c.n = c.wins = 0;
            foreach (var c in byScenario) c.n = c.wins = 0;
            foreach (var c in byMass) c.n = c.wins = 0;
            foreach (var c in byStyleScenario) c.n = c.wins = 0;
            System.Array.Clear(uses, 0, uses.Length); System.Array.Clear(hits, 0, hits.Length);
            System.Array.Clear(releases, 0, releases.Length); System.Array.Clear(knockdowns, 0, knockdowns.Length);
            System.Array.Clear(decisive, 0, decisive.Length); System.Array.Clear(wallTurns, 0, wallTurns.Length);
            sumEscapeTime = sumPinned = 0f;
        }

        static string Strip(string text) => System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", "").Replace('█', '#');

        void Save()
        {
            string path = Path.Combine(Application.persistentDataPath, $"selfdefense_summary_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(path, Strip(Overview()) + "\n\n" + Strip(Matrix()) + "\n\n" + Strip(Techniques()));
            Debug.Log($"[SelfDefenseRL] Zusammenfassung gespeichert: {path}");
        }
    }
}
