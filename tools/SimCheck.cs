// Schneller Plausibilitäts-Check des Kampfmodells OHNE Unity (läuft mit Mono/.NET).
//   mcs -out:simcheck.exe tools/SimCheck.cs Assets/SelfDefenseRL/Scripts/Enums.cs \
//       Assets/SelfDefenseRL/Scripts/TechniqueLibrary.cs Assets/SelfDefenseRL/Scripts/CombatSimulation.cs
//   mono simcheck.exe [massRatio] [random|flee]
// Gibt die Erfolgsrate einer einfachen Politik pro Szenario x Stil aus:
//   random = zufällige erlaubte Technik
//   flee   = zufällige Technik nur solange festgehalten/am Boden, sonst sofort wegrennen
// Ziel: weder 0 % noch 100 % überall – sonst gibt es für RL nichts zu lernen.
using System;
using SelfDefenseRL;

public static class SimCheck
{
    public static void Main(string[] args)
    {
        float mass = args.Length > 0 ? float.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture) : 1.5f;
        float strength = (float)Math.Pow(mass, 1.3) * 1.1f;
        int episodes = 2000;
        bool flee = args.Length > 1 && args[1] == "flee";
        var rng = new Random(1);
        var sim = new CombatSimulation(42);
        const float dt = 0.02f;

        Console.WriteLine($"Politik={(flee ? "flee" : "random")}, massRatio={mass:0.00}, strengthRatio={strength:0.00}, {episodes} Episoden je Zelle\n");
        Console.Write("{0,-16}", "Szenario");
        for (int st = 0; st < EnumCounts.Styles; st++) Console.Write("{0,10}", ((Style)st).ToString());
        Console.WriteLine();

        for (int sc = 0; sc < EnumCounts.Scenarios; sc++)
        {
            Console.Write("{0,-16}", ((Scenario)sc).ToString());
            for (int st = 0; st < EnumCounts.Styles; st++)
            {
                int wins = 0;
                for (int ep = 0; ep < episodes; ep++)
                {
                    sim.Reset((Scenario)sc, (Style)st, mass, strength);
                    while (!sim.Done)
                    {
                        // zufällige erlaubte Technik (70 %), sonst nichts; Bewegung: weg, bei Wand seitlich
                        int choice = 0, seen = 0;
                        for (int i = 1; i < TechniqueLibrary.Count; i++)
                            if (sim.IsTechniqueAvailable(i)) { seen++; if (rng.Next(seen) == 0) choice = i; }
                        if (rng.NextDouble() > 0.7) choice = 0;
                        if (flee && sim.Grip == GripType.None && !sim.DefGrounded) choice = 0;
                        if (flee && sim.DefGrounded && sim.Grip == GripType.None) choice = TechniqueLibrary.IndexOf("StandUp");
                        var move = MoveAction.AwayFromAttacker;
                        if (sim.SpaceBehind() < 0.5f) move = rng.NextDouble() < 0.5 ? MoveAction.StrafeLeft : MoveAction.StrafeRight;
                        sim.ApplyDecision(move, choice, dt);
                        for (int k = 0; k < 5 && !sim.Done; k++) sim.Tick(dt);
                    }
                    if (sim.Success) wins++;
                }
                Console.Write("{0,9:0}%", 100.0 * wins / episodes);
            }
            Console.WriteLine();
        }
    }
}
