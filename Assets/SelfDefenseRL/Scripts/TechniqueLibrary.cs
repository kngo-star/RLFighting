using System;
using System.Collections.Generic;

namespace SelfDefenseRL
{
    /// <summary>In welcher Lage eine Technik ausführbar ist.</summary>
    [Flags]
    public enum Context
    {
        None = 0,
        StandingFree = 1 << 0,
        Wrist = 1 << 1,
        FrontPin = 1 << 2,
        BearHug = 1 << 3,
        Choke = 1 << 4,
        GroundMount = 1 << 5,
        GroundFree = 1 << 6,

        AllStandingHeld = Wrist | FrontPin | BearHug | Choke,
        AllStanding = StandingFree | AllStandingHeld,
        All = AllStanding | GroundMount | GroundFree
    }

    /// <summary>
    /// Abstrakte Technik. ALLE Zahlen sind Modellannahmen und sollten im Bericht
    /// begründet und per Sensitivitätsanalyse geprüft werden (siehe docs/MODELL.md).
    /// </summary>
    public class Technique
    {
        public string Name;
        public string Description;    // kurze Erklärung (Deutsch)
        public int Styles;            // Bitmaske über Style (wird aus StyleTable berechnet)
        public Context Contexts;      // wo die Technik möglich ist
        public float MaxRange = 1.2f; // nur relevant im Kontext StandingFree (Meter)
        public float MinRange = 0f;

        public float Duration = 0.4f; // Sekunden, in denen die Verteidigerin "busy" ist
        public float Accuracy = 1f;   // Trefferwahrscheinlichkeit (Basis)

        public float Damage;          // Schaden am Angreifer (0..1 Skala)
        public float MassScaling;     // 0 = unabhängig von Masse, 1 = proportional zu (1/massRatio)
        public float StunFactor;      // Benommenheit: Sekunden = StunFactor × Schaden × 6

        public float GripBreak;       // Basiswahrscheinlichkeit, den Griff zu lösen
        public float StrengthScaling; // 0 = Hebel/Technik, 1 = reine Kraft gegen Kraft

        public float Balance;         // Gleichgewichtsverlust beim Angreifer
        public float Push;            // erzeugter Abstand (Meter), falls frei
        public float Exposure;        // zusätzliches Risiko, getroffen zu werden
        public float StaminaCost = 0.05f;

        public bool IsGuard;          // Deckung: reduziert erlittenen Schaden
        public bool IsStandUp;        // Aufstehen aus Bodenlage
        public bool RemovesMount;     // kann die Mount-Position auflösen (Bridge & Roll)
        public bool TurnsOffWall;     // dreht die Position: danach steht der Angreifer an der Wand (Underhook)
    }

    public static class TechniqueLibrary
    {
        /// <summary>Techniken, die jeder Mensch ohne Training hat (gehören zu jedem Stil).</summary>
        public static readonly string[] Universal = { "None", "Guard", "PushOff", "StandUp" };

        /// <summary>
        /// Welcher Kampfstil welche Techniken trainiert. Das ist die zentrale Tabelle für den Stil-Vergleich:
        /// Jeder Stil bekommt die Universal-Techniken plus die hier genannten.
        /// </summary>
        public static readonly Dictionary<Style, string[]> StyleTable = new Dictionary<Style, string[]>
        {
            { Style.Untrained,  new[] { "HammerFist" } },
            { Style.Boxing,     new[] { "JabCross", "Frame" } },
            { Style.Kickboxing, new[] { "JabCross", "FrontKick", "RoundKickThigh", "KneeGroin", "KickKnee" } },
            { Style.MuayThai,   new[] { "JabCross", "Elbow", "KneeGroin", "RoundKickThigh", "FrontKick", "Frame", "Underhook" } },
            { Style.Karate,     new[] { "ReversePunch", "PalmStrike", "HammerFist", "Elbow", "FrontKick", "SideKick", "KickKnee" } },
            { Style.Taekwondo,  new[] { "ReversePunch", "FrontKick", "SideKick", "RoundKickThigh", "KickKnee" } },
            { Style.BJJ,        new[] { "Frame", "Underhook", "GripReleaseThumb", "ChokeDefense", "DropWeight", "HipThrow", "BridgeRoll", "HipEscape" } },
            { Style.JuJutsu,    new[] { "WristLock", "GripReleaseThumb", "ChokeDefense", "DropWeight", "HipThrow", "PalmStrike", "Elbow", "KneeGroin", "KickKnee", "BridgeRoll" } },
            { Style.Judo,       new[] { "HipThrow", "Underhook", "GripReleaseThumb", "ChokeDefense", "DropWeight", "Frame", "BridgeRoll" } },
            { Style.Wrestling,  new[] { "Underhook", "DropWeight", "Frame", "GripReleaseThumb", "HipThrow", "BridgeRoll", "HipEscape" } },
            { Style.Aikido,     new[] { "WristLock", "GripReleaseThumb", "HipThrow" } },
            { Style.KravMaga,   new[] { "PalmStrike", "JabCross", "HammerFist", "Elbow", "KneeGroin", "FaceStrike", "KickKnee", "FrontKick", "GripReleaseThumb", "ChokeDefense", "DropWeight" } },
            { Style.WingTsun,   new[] { "PalmStrike", "JabCross", "Elbow", "FaceStrike", "KickKnee", "GripReleaseThumb" } },
            { Style.MMA,        new[] { "JabCross", "Elbow", "RoundKickThigh", "FrontKick", "KickKnee", "Frame", "Underhook", "DropWeight", "HipThrow", "BridgeRoll", "HipEscape", "GripReleaseThumb", "ChokeDefense" } },
            // Style.Mixed = alle Techniken (wird automatisch gesetzt)
        };

        // Muss NACH StyleTable stehen (statische Felder werden in Reihenfolge initialisiert)
        public static readonly List<Technique> All = Build();
        public static int Count => All.Count;

        static List<Technique> Build()
        {
            var t = new List<Technique>();

            // 0 – Nichts tun (immer erlaubt, damit die Maske nie leer ist)
            t.Add(new Technique { Name = "None", Description = "nichts tun", Contexts = Context.All, Duration = 0f, StaminaCost = 0f });

            // ---- Schläge ----
            t.Add(new Technique
            {
                Name = "PalmStrike", Description = "Handballenstoss ins Gesicht",
                Contexts = Context.StandingFree | Context.Wrist | Context.FrontPin | Context.Choke,
                Duration = 0.35f, Accuracy = 0.7f, Damage = 0.12f, MassScaling = 0.8f, StunFactor = 0.5f,
                Push = 0.2f, Exposure = 0.2f, StaminaCost = 0.04f
            });
            t.Add(new Technique
            {
                Name = "JabCross", Description = "Gerade Schläge (Jab, Cross)",
                Contexts = Context.StandingFree | Context.FrontPin | Context.Choke,
                Duration = 0.25f, Accuracy = 0.7f, Damage = 0.10f, MassScaling = 0.9f, StunFactor = 0.4f,
                Exposure = 0.15f, StaminaCost = 0.04f
            });
            t.Add(new Technique
            {
                Name = "ReversePunch", Description = "Karate-Fauststoss (Gyaku-Zuki) mit Hüfteinsatz",
                Contexts = Context.StandingFree | Context.Wrist | Context.FrontPin | Context.Choke,
                Duration = 0.3f, Accuracy = 0.7f, Damage = 0.12f, MassScaling = 0.85f, StunFactor = 0.45f,
                Exposure = 0.15f, StaminaCost = 0.04f
            });
            t.Add(new Technique
            {
                Name = "HammerFist", Description = "Hammerfaust, auch von unten",
                Contexts = Context.AllStanding | Context.GroundMount,
                Duration = 0.3f, Accuracy = 0.7f, Damage = 0.08f, MassScaling = 0.7f, StunFactor = 0.3f,
                Exposure = 0.15f, StaminaCost = 0.04f
            });
            t.Add(new Technique
            {
                Name = "Elbow", Description = "Ellbogenschlag (auch nach hinten)",
                Contexts = Context.Wrist | Context.FrontPin | Context.Choke | Context.BearHug,
                Duration = 0.3f, Accuracy = 0.75f, Damage = 0.15f, MassScaling = 0.7f, StunFactor = 0.6f,
                GripBreak = 0.05f, StrengthScaling = 0.5f, Exposure = 0.1f, StaminaCost = 0.05f
            });

            // ---- Ziele mit hoher Wirkung (wenig abhängig von der eigenen Masse) ----
            t.Add(new Technique
            {
                Name = "KneeGroin", Description = "Kniestoss (Leiste/Körper)",
                Contexts = Context.Wrist | Context.FrontPin | Context.Choke,
                Duration = 0.4f, Accuracy = 0.55f, Damage = 0.30f, MassScaling = 0.15f, StunFactor = 1.5f,
                GripBreak = 0.10f, StrengthScaling = 0.3f, Exposure = 0.15f, StaminaCost = 0.05f
            });
            t.Add(new Technique
            {
                Name = "FaceStrike", Description = "Angriff auf Augen/Nase",
                Contexts = Context.StandingFree | Context.Wrist | Context.FrontPin | Context.Choke | Context.GroundMount,
                Duration = 0.3f, Accuracy = 0.45f, Damage = 0.25f, MassScaling = 0f, StunFactor = 1.2f,
                GripBreak = 0.15f, StrengthScaling = 0.2f, Exposure = 0.25f, StaminaCost = 0.04f
            });

            // ---- Tritte ----
            t.Add(new Technique
            {
                Name = "KickKnee", Description = "Tritt gegen Knie/Schienbein, Fussstampfer",
                Contexts = Context.StandingFree | Context.Wrist | Context.FrontPin | Context.BearHug | Context.Choke,
                MinRange = 0.4f, MaxRange = 1.4f,
                Duration = 0.4f, Accuracy = 0.6f, Damage = 0.15f, MassScaling = 0.4f, StunFactor = 0.6f,
                Balance = 0.2f, Exposure = 0.2f, StaminaCost = 0.05f
            });
            t.Add(new Technique
            {
                Name = "FrontKick", Description = "Frontaltritt / Teep (stösst weg)",
                Contexts = Context.StandingFree | Context.Wrist, MinRange = 0.7f, MaxRange = 1.5f,
                Duration = 0.4f, Accuracy = 0.65f, Damage = 0.10f, MassScaling = 0.6f, StunFactor = 0.4f,
                Push = 0.9f, Exposure = 0.25f, StaminaCost = 0.06f
            });
            t.Add(new Technique
            {
                Name = "SideKick", Description = "Seitwärtstritt (Yop Chagi / Yoko-Geri)",
                Contexts = Context.StandingFree, MinRange = 0.9f, MaxRange = 1.8f,
                Duration = 0.5f, Accuracy = 0.55f, Damage = 0.14f, MassScaling = 0.6f, StunFactor = 0.5f,
                Push = 1.1f, Balance = 0.2f, Exposure = 0.35f, StaminaCost = 0.08f
            });
            t.Add(new Technique
            {
                Name = "RoundKickThigh", Description = "Halbkreistritt auf den Oberschenkel (Low Kick)",
                Contexts = Context.StandingFree, MinRange = 0.9f, MaxRange = 1.8f,
                Duration = 0.5f, Accuracy = 0.6f, Damage = 0.15f, MassScaling = 0.7f, StunFactor = 0.4f,
                Balance = 0.15f, Exposure = 0.35f, StaminaCost = 0.07f
            });

            // ---- Befreiungen und Hebel ----
            t.Add(new Technique
            {
                Name = "GripReleaseThumb", Description = "Griff am Daumen lösen",
                Contexts = Context.Wrist,
                Duration = 0.4f, GripBreak = 0.55f, StrengthScaling = 0.3f, Exposure = 0.1f, StaminaCost = 0.04f
            });
            t.Add(new Technique
            {
                Name = "WristLock", Description = "Handgelenkhebel mit Wurf (Kote-Gaeshi)",
                Contexts = Context.Wrist | Context.FrontPin | Context.Choke,
                Duration = 0.5f, Accuracy = 0.6f, GripBreak = 0.5f, StrengthScaling = 0.2f,
                Balance = 0.35f, MassScaling = 0.3f, Exposure = 0.15f, StaminaCost = 0.06f
            });
            t.Add(new Technique
            {
                Name = "ChokeDefense", Description = "Würgegriff aufreissen und wegdrehen",
                Contexts = Context.Choke,
                Duration = 0.45f, GripBreak = 0.45f, StrengthScaling = 0.5f, Exposure = 0.1f, StaminaCost = 0.06f
            });
            t.Add(new Technique
            {
                Name = "DropWeight", Description = "Gewicht senken, Hüfte raus",
                Contexts = Context.BearHug,
                Duration = 0.5f, GripBreak = 0.35f, StrengthScaling = 0.35f, Balance = 0.1f, StaminaCost = 0.06f
            });

            // ---- Clinch, Rahmen und Distanz ----
            t.Add(new Technique
            {
                Name = "Underhook", Description = "Unterhaken und von der Wand wegdrehen",
                Contexts = Context.FrontPin | Context.Choke,
                Duration = 0.6f, Accuracy = 0.55f, GripBreak = 0.15f, StrengthScaling = 0.5f,
                Exposure = 0.1f, StaminaCost = 0.08f, TurnsOffWall = true
            });
            t.Add(new Technique
            {
                Name = "Frame", Description = "Unterarm-Rahmen gegen Hals/Schulter",
                Contexts = Context.StandingFree | Context.FrontPin | Context.Choke | Context.GroundMount,
                Duration = 0.4f, GripBreak = 0.2f, StrengthScaling = 0.6f, Push = 0.6f, MassScaling = 0.5f,
                Exposure = 0.05f, StaminaCost = 0.04f
            });
            t.Add(new Technique
            {
                Name = "PushOff", Description = "Wegstossen (ohne Training)",
                Contexts = Context.StandingFree | Context.FrontPin | Context.Wrist,
                Duration = 0.4f, GripBreak = 0.25f, StrengthScaling = 0.8f, Push = 1.2f, MassScaling = 0.8f,
                Exposure = 0.15f, StaminaCost = 0.06f
            });

            // ---- Würfe ----
            t.Add(new Technique
            {
                Name = "HipThrow", Description = "Hüftwurf (O-Goshi)",
                Contexts = Context.FrontPin | Context.Wrist | Context.StandingFree, MaxRange = 0.9f,
                Duration = 0.7f, Accuracy = 0.5f, Balance = 1.2f, MassScaling = 0.5f,
                Exposure = 0.4f, StaminaCost = 0.12f
            });

            // ---- Boden ----
            t.Add(new Technique
            {
                Name = "BridgeRoll", Description = "Brücke und Rolle aus dem Mount (Upa)",
                Contexts = Context.GroundMount,
                Duration = 0.8f, Accuracy = 0.6f, Balance = 1.3f, MassScaling = 0.45f,
                Exposure = 0.2f, StaminaCost = 0.12f, RemovesMount = true
            });
            t.Add(new Technique
            {
                Name = "HipEscape", Description = "Hüftflucht (Shrimp)",
                Contexts = Context.GroundMount | Context.GroundFree,
                Duration = 0.7f, GripBreak = 0.35f, StrengthScaling = 0.4f, Push = 0.5f, MassScaling = 0.3f,
                StaminaCost = 0.1f
            });
            t.Add(new Technique
            {
                Name = "StandUp", Description = "Technisch aufstehen", Contexts = Context.GroundFree,
                Duration = 0.8f, Exposure = 0.3f, StaminaCost = 0.08f, IsStandUp = true
            });

            // ---- Deckung ----
            t.Add(new Technique
            {
                Name = "Guard", Description = "Deckung, Kopf schützen", Contexts = Context.All,
                Duration = 0.5f, StaminaCost = 0.01f, IsGuard = true
            });

            AssignStyles(t);
            return t;
        }

        static void AssignStyles(List<Technique> list)
        {
            var byName = new Dictionary<string, Technique>();
            foreach (var tech in list) byName[tech.Name] = tech;

            for (int s = 0; s < EnumCounts.Styles; s++)
                foreach (var name in Universal) byName[name].Styles |= 1 << s;

            foreach (var kv in StyleTable)
                foreach (var name in kv.Value)
                {
                    if (!byName.TryGetValue(name, out var tech))
                        throw new ArgumentException($"StyleTable: unbekannte Technik '{name}' bei {kv.Key}");
                    tech.Styles |= 1 << (int)kv.Key;
                }

            foreach (var tech in list) tech.Styles |= 1 << (int)Style.Mixed;
        }

        public static bool StyleAllows(Technique t, Style s) => (t.Styles & (1 << (int)s)) != 0;

        public static int IndexOf(string name)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Name == name) return i;
            return -1;
        }
    }
}
