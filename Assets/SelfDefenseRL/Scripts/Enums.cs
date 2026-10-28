namespace SelfDefenseRL
{
    /// <summary>Notlage, mit der eine Episode startet.</summary>
    public enum Scenario
    {
        WristGrab = 0,      // Am Handgelenk / an der Kleidung gepackt
        PinnedToWall = 1,   // Gegen die Wand gedrückt (vorne, Clinch)
        BearHugBehind = 2,  // Umklammerung von hinten
        ChokeFront = 3,     // Würgegriff von vorne
        GroundMount = 4,    // Am Boden, Angreifer sitzt oben
        CorneredStrikes = 5 // In einer Ecke, Angreifer schlägt
    }

    /// <summary>Kampfstil = erlaubte Teilmenge der Techniken (Action Masking).</summary>
    public enum Style
    {
        Untrained = 0,  // Baseline: nur natürliche Reaktionen
        Boxing = 1,
        Kickboxing = 2,
        MuayThai = 3,
        Karate = 4,
        Taekwondo = 5,
        BJJ = 6,        // Brazilian Jiu-Jitsu
        JuJutsu = 7,    // japanisches Ju-Jutsu (Selbstverteidigung: Hebel, Würfe, Schläge)
        Judo = 8,
        Wrestling = 9,  // Ringen
        Aikido = 10,
        KravMaga = 11,
        WingTsun = 12,
        MMA = 13,       // Mixed Martial Arts (Sport-Regelwerk: keine Leiste, keine Augen)
        Mixed = 14      // Alle Techniken (Obergrenze)
    }

    /// <summary>Art des Griffs, den der Angreifer gerade hat.</summary>
    public enum GripType
    {
        None = 0,
        Wrist = 1,
        FrontPin = 2,
        BearHug = 3,
        Choke = 4,
        Mount = 5
    }

    /// <summary>Bewegungs-Aktion (Branch 0).</summary>
    public enum MoveAction
    {
        Stay = 0,
        AwayFromAttacker = 1,
        TowardAttacker = 2,
        StrafeLeft = 3,
        StrafeRight = 4
    }

    public static class EnumCounts
    {
        public const int Scenarios = 6;
        public const int Styles = 15;
        public const int GripTypes = 6;
        public const int MoveActions = 5;
    }
}
