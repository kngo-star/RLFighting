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
        Untrained = 0, // Baseline: nur natürliche Reaktionen
        Boxing = 1,
        MuayThai = 2,
        BJJ = 3,
        Judo = 4,
        KravMaga = 5,
        WingTsun = 6,
        Mixed = 7      // Alle Techniken
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
        public const int Styles = 8;
        public const int GripTypes = 6;
        public const int MoveActions = 5;
    }
}
