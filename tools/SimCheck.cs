// Schneller Plausibilitäts-Check des Kampfmodells OHNE Unity (läuft mit Mono/.NET).
//   mcs -out:simcheck.exe tools/SimCheck.cs Assets/SelfDefenseRL/Scripts/Enums.cs \
//       Assets/SelfDefenseRL/Scripts/TechniqueLibrary.cs Assets/SelfDefenseRL/Scripts/CombatSimulation.cs
//   mono simcheck.exe [massRatio] [random|flee] [baseline.csv]
//   Mit CSV-Pfad: schreibt Baseline-Episoden (alle Grössen 1.0–1.8) im selben Format wie Unity → analysis/analyze_episodes.py
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
        if (args.Length > 2) { WriteCsv(args[2], args.Length > 1 && args[1] == "flee"); return; }
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

    static int Policy(CombatSimulation sim, Random rng, bool flee, out MoveAction move)
    {
        int choice = 0, seen = 0;
        for (int i = 1; i < TechniqueLibrary.Count; i++)
            if (sim.IsTechniqueAvailable(i)) { seen++; if (rng.Next(seen) == 0) choice = i; }
        if (rng.NextDouble() > 0.7) choice = 0;
        if (flee && sim.Grip == GripType.None && !sim.DefGrounded) choice = 0;
        if (flee && sim.DefGrounded && sim.Grip == GripType.None) choice = TechniqueLibrary.IndexOf("StandUp");
        move = MoveAction.AwayFromAttacker;
        if (sim.SpaceBehind() < 0.5f) move = rng.NextDouble() < 0.5 ? MoveAction.StrafeLeft : MoveAction.StrafeRight;
        return choice;
    }

    static void WriteCsv(string path, bool flee)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var rng = new Random(7); var sim = new CombatSimulation(11);
        var sb = new System.Text.StringBuilder();
        sb.Append("scenario,style,mass_ratio,strength_ratio,success,end_reason,time,health_left,attacker_damage,damage_taken,times_grabbed,takedowns,time_pinned_wall,first_release_time,decisive_technique");
        foreach (var pre in new[] { "uses_", "hits_", "releases_", "knockdowns_" })
            for (int i = 1; i < TechniqueLibrary.Count; i++) sb.Append(',').Append(pre).Append(TechniqueLibrary.All[i].Name);
        sb.Append('\n');
        float[] masses = { 1.0f, 1.2f, 1.4f, 1.6f, 1.8f };
        foreach (var m in masses)
            for (int sc = 0; sc < EnumCounts.Scenarios; sc++)
                for (int st = 0; st < EnumCounts.Styles; st++)
                    for (int ep = 0; ep < 200; ep++)
                    {
                        float str = (float)Math.Pow(m, 1.3) * 1.1f;
                        sim.Reset((Scenario)sc, (Style)st, m, str);
                        while (!sim.Done)
                        {
                            int c = Policy(sim, rng, flee, out var mv);
                            sim.ApplyDecision(mv, c, 0.02f);
                            for (int k = 0; k < 5 && !sim.Done; k++) sim.Tick(0.02f);
                        }
                        sb.Append(sim.Scenario).Append(',').Append(sim.Style).Append(',')
                          .Append(m.ToString("0.###", inv)).Append(',').Append(str.ToString("0.###", inv)).Append(',')
                          .Append(sim.Success ? 1 : 0).Append(',').Append(sim.EndReason).Append(',')
                          .Append(sim.Time.ToString("0.##", inv)).Append(',').Append(Math.Max(0f, sim.DefHealth).ToString("0.###", inv)).Append(',')
                          .Append(sim.AttDamage.ToString("0.###", inv)).Append(',').Append(sim.DamageTakenTotal.ToString("0.###", inv)).Append(',')
                          .Append(sim.TimesGrabbed).Append(',').Append(sim.Takedowns).Append(',')
                          .Append(sim.TimePinnedToWall.ToString("0.##", inv)).Append(',').Append(sim.FirstReleaseTime.ToString("0.##", inv)).Append(',')
                          .Append(sim.Success ? TechniqueLibrary.All[sim.DecisiveTechnique].Name : "");
                        foreach (var arr in new[] { sim.TechniqueUses, sim.TechniqueHits, sim.TechniqueReleases, sim.TechniqueKnockdowns })
                            for (int i = 1; i < TechniqueLibrary.Count; i++) sb.Append(',').Append(arr[i]);
                        sb.Append('\n');
                    }
        System.IO.File.WriteAllText(path, sb.ToString());
        Console.WriteLine("geschrieben: " + path);
    }
}
