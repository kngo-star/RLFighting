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

    [Flags]
    public enum StyleMask
    {
        None = 0,
        Untrained = 1 << Style.Untrained,
        Boxing = 1 << Style.Boxing,
        MuayThai = 1 << Style.MuayThai,
        BJJ = 1 << Style.BJJ,
        Judo = 1 << Style.Judo,
        KravMaga = 1 << Style.KravMaga,
        WingTsun = 1 << Style.WingTsun,
        Mixed = 1 << Style.Mixed,
        All = Untrained | Boxing | MuayThai | BJJ | Judo | KravMaga | WingTsun | Mixed
    }

    /// <summary>
    /// Abstrakte Technik. ALLE Zahlen sind Modellannahmen und sollten im Bericht
    /// begründet und per Sensitivitätsanalyse geprüft werden (siehe docs/MODELL.md).
    /// </summary>
    public class Technique
    {
        public string Name;
        public StyleMask Styles;      // Mixed wird automatisch ergänzt
        public Context Contexts;      // wo die Technik möglich ist
        public float MaxRange = 1.2f; // nur relevant im Kontext StandingFree (Meter)
        public float MinRange = 0f;

        public float Duration = 0.4f; // Sekunden, in denen die Verteidigerin "busy" ist
        public float Accuracy = 1f;   // Trefferwahrscheinlichkeit (Basis)

        public float Damage;          // Schaden am Angreifer (0..1 Skala)
        public float MassScaling;     // 0 = unabhängig von Masse, 1 = proportional zu (1/massRatio)
        public float StunFactor;      // Sekunden Benommenheit pro Schadenseinheit*10

        public float GripBreak;       // Basiswahrscheinlichkeit, den Griff zu lösen
        public float StrengthScaling; // 0 = Hebel/Technik, 1 = reine Kraft gegen Kraft

        public float Balance;         // Gleichgewichtsverlust beim Angreifer
        public float Push;            // erzeugter Abstand (Meter), falls frei
        public float Exposure;        // zusätzliches Risiko, getroffen zu werden
        public float StaminaCost = 0.05f;

        public bool IsGuard;          // Deckung: reduziert erlittenen Schaden
        public bool IsStandUp;        // Aufstehen aus Bodenlage
        public bool RemovesMount;     // kann die Mount-Position auflösen (Bridge & Roll)
    }

    public static class TechniqueLibrary
    {
        public static readonly List<Technique> All = Build();
        public static int Count => All.Count;

        static List<Technique> Build()
        {
            var t = new List<Technique>();

            // 0 – Nichts tun (immer erlaubt, damit die Maske nie leer ist)
            t.Add(new Technique { Name = "None", Styles = StyleMask.All, Contexts = Context.All, Duration = 0f, StaminaCost = 0f });

            // ---- Schläge mit grober Motorik ----
            t.Add(new Technique
            {
                Name = "PalmStrike", Styles = StyleMask.KravMaga | StyleMask.WingTsun,
                Contexts = Context.StandingFree | Context.Wrist | Context.FrontPin | Context.Choke,
                Duration = 0.35f, Accuracy = 0.7f, Damage = 0.12f, MassScaling = 0.8f, StunFactor = 0.5f,
                Push = 0.2f, Exposure = 0.2f, StaminaCost = 0.04f
            });
            t.Add(new Technique
            {
                Name = "JabCross", Styles = StyleMask.Boxing | StyleMask.MuayThai | StyleMask.KravMaga,
                Contexts = Context.StandingFree | Context.FrontPin | Context.Choke,
                Duration = 0.25f, Accuracy = 0.7f, Damage = 0.10f, MassScaling = 0.9f, StunFactor = 0.4f,
                Exposure = 0.15f, StaminaCost = 0.04f
            });
            t.Add(new Technique
            {
                Name = "HammerFist", Styles = StyleMask.Untrained | StyleMask.KravMaga,
                Contexts = Context.AllStanding | Context.GroundMount,
                Duration = 0.3f, Accuracy = 0.7f, Damage = 0.08f, MassScaling = 0.7f, StunFactor = 0.3f,
                Exposure = 0.15f, StaminaCost = 0.04f
            });
            t.Add(new Technique
            {
                Name = "Elbow", Styles = StyleMask.KravMaga | StyleMask.MuayThai | StyleMask.WingTsun,
                Contexts = Context.Wrist | Context.FrontPin | Context.Choke | Context.BearHug,
                Duration = 0.3f, Accuracy = 0.75f, Damage = 0.15f, MassScaling = 0.7f, StunFactor = 0.6f,
                GripBreak = 0.05f, StrengthScaling = 0.5f, Exposure = 0.1f, StaminaCost = 0.05f
            });

            // ---- Ziele mit hoher Wirkung (wenig abhängig von der eigenen Masse) ----
            t.Add(new Technique
            {
                Name = "KneeGroin", Styles = StyleMask.KravMaga | StyleMask.MuayThai,
                Contexts = Context.Wrist | Context.FrontPin | Context.Choke,
                Duration = 0.4f, Accuracy = 0.55f, Damage = 0.30f, MassScaling = 0.15f, StunFactor = 1.5f,
                GripBreak = 0.10f, StrengthScaling = 0.3f, Exposure = 0.15f, StaminaCost = 0.05f
            });
            t.Add(new Technique
            {
                Name = "FaceStrike", Styles = StyleMask.KravMaga | StyleMask.WingTsun,
                Contexts = Context.StandingFree | Context.Wrist | Context.FrontPin | Context.Choke | Context.GroundMount,
                Duration = 0.3f, Accuracy = 0.45f, Damage = 0.25f, MassScaling = 0f, StunFactor = 1.2f,
                GripBreak = 0.15f, StrengthScaling = 0.2f, Exposure = 0.25f, StaminaCost = 0.04f
            });
            t.Add(new Technique
            {
                Name = "KickKnee", Styles = StyleMask.KravMaga | StyleMask.WingTsun | StyleMask.MuayThai,
                Contexts = Context.StandingFree | Context.Wrist | Context.FrontPin | Context.BearHug | Context.Choke,
                MinRange = 0.4f, MaxRange = 1.4f,
                Duration = 0.4f, Accuracy = 0.6f, Damage = 0.15f, MassScaling = 0.4f, StunFactor = 0.6f,
                Balance = 0.2f, Exposure = 0.2f, StaminaCost = 0.05f
            });
            t.Add(new Technique
            {
                Name = "RoundKickThigh", Styles = StyleMask.MuayThai,
                Contexts = Context.StandingFree, MinRange = 0.9f, MaxRange = 1.8f,
                Duration = 0.5f, Accuracy = 0.6f, Damage = 0.15f, MassScaling = 0.7f, StunFactor = 0.4f,
                Balance = 0.15f, Exposure = 0.35f, StaminaCost = 0.07f
            });

            // ---- Befreiungen (Hebel statt Kraft) ----
            t.Add(new Technique
            {
                Name = "GripReleaseThumb", Styles = StyleMask.KravMaga | StyleMask.BJJ | StyleMask.Judo | StyleMask.WingTsun,
                Contexts = Context.Wrist,
                Duration = 0.4f, GripBreak = 0.55f, StrengthScaling = 0.3f, Exposure = 0.1f, StaminaCost = 0.04f
            });
            t.Add(new Technique
            {
                Name = "ChokeDefense", Styles = StyleMask.KravMaga | StyleMask.BJJ | StyleMask.Judo,
                Contexts = Context.Choke,
                Duration = 0.45f, GripBreak = 0.45f, StrengthScaling = 0.5f, Exposure = 0.1f, StaminaCost = 0.06f
            });
            t.Add(new Technique
            {
                Name = "DropWeight", Styles = StyleMask.BJJ | StyleMask.Judo | StyleMask.KravMaga,
                Contexts = Context.BearHug,
                Duration = 0.5f, GripBreak = 0.35f, StrengthScaling = 0.35f, Balance = 0.1f, StaminaCost = 0.06f
            });

            // ---- Rahmen und Distanz ----
            t.Add(new Technique
            {
                Name = "Frame", Styles = StyleMask.BJJ | StyleMask.MuayThai | StyleMask.Judo,
                Contexts = Context.StandingFree | Context.FrontPin | Context.Choke | Context.GroundMount,
                Duration = 0.4f, GripBreak = 0.2f, StrengthScaling = 0.6f, Push = 0.6f, MassScaling = 0.5f,
                Exposure = 0.05f, StaminaCost = 0.04f
            });
            t.Add(new Technique
            {
                Name = "PushOff", Styles = StyleMask.All,
                Contexts = Context.StandingFree | Context.FrontPin | Context.Wrist,
                Duration = 0.4f, GripBreak = 0.25f, StrengthScaling = 0.8f, Push = 1.2f, MassScaling = 0.8f,
                Exposure = 0.15f, StaminaCost = 0.06f
            });

            // ---- Würfe und Gleichgewicht ----
            t.Add(new Technique
            {
                Name = "HipThrow", Styles = StyleMask.Judo | StyleMask.BJJ,
                Contexts = Context.FrontPin | Context.Wrist | Context.StandingFree, MaxRange = 0.9f,
                Duration = 0.7f, Accuracy = 0.5f, Balance = 1.2f, MassScaling = 0.5f,
                Exposure = 0.4f, StaminaCost = 0.12f
            });

            // ---- Boden ----
            t.Add(new Technique
            {
                Name = "BridgeRoll", Styles = StyleMask.BJJ | StyleMask.Judo,
                Contexts = Context.GroundMount,
                Duration = 0.8f, Accuracy = 0.6f, Balance = 1.3f, MassScaling = 0.45f,
                Exposure = 0.2f, StaminaCost = 0.12f, RemovesMount = true
            });
            t.Add(new Technique
            {
                Name = "HipEscape", Styles = StyleMask.BJJ,
                Contexts = Context.GroundMount | Context.GroundFree,
                Duration = 0.7f, GripBreak = 0.35f, StrengthScaling = 0.4f, Push = 0.5f, MassScaling = 0.3f,
                StaminaCost = 0.1f
            });
            t.Add(new Technique
            {
                Name = "StandUp", Styles = StyleMask.All, Contexts = Context.GroundFree,
                Duration = 0.8f, Exposure = 0.3f, StaminaCost = 0.08f, IsStandUp = true
            });

            // ---- Deckung ----
            t.Add(new Technique
            {
                Name = "Guard", Styles = StyleMask.All, Contexts = Context.All,
                Duration = 0.5f, StaminaCost = 0.01f, IsGuard = true
            });

            foreach (var tech in t) tech.Styles |= StyleMask.Mixed;
            return t;
        }

        public static bool StyleAllows(Technique t, Style s) => (t.Styles & (StyleMask)(1 << (int)s)) != 0;

        public static int IndexOf(string name)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Name == name) return i;
            return -1;
        }
    }
}
