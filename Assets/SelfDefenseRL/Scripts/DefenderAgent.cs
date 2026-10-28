using System.Text;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace SelfDefenseRL
{
    /// <summary>
    /// RL-Agent "Verteidigerin". Entscheidet auf hoher Ebene (Bewegung + Technik),
    /// die eigentliche Kampflogik steckt in <see cref="CombatSimulation"/>.
    ///
    /// Actions (diskret):
    ///   Branch 0: Bewegung (5)          – siehe MoveAction
    ///   Branch 1: Technik (TechniqueLibrary.Count) – per Action Masking auf Stil + Lage begrenzt
    /// Observations: 39 Floats (siehe CollectObservations)
    /// </summary>
    public class DefenderAgent : Agent
    {
        public const int ObservationSize = 39;

        [Header("Episode-Setup (-1 = aus Trainings-Config bzw. zufällig)")]
        [Tooltip("-1 = zufällig / Config, sonst Index aus Scenario")]
        public int fixedScenario = -1;
        [Tooltip("-1 = zufällig / Config, sonst Index aus Style")]
        public int fixedStyle = -1;
        [Tooltip("-1 = zufällig 1.0–1.8 / Config")]
        public float fixedMassRatio = -1f;
        [Tooltip("-1 = aus Masse abgeleitet / Config")]
        public float fixedStrengthRatio = -1f;

        [Header("Simulation")]
        [Tooltip("Muss zum DecisionRequester.DecisionPeriod passen")]
        public int ticksPerDecision = 5;
        public int seed = 0;

        [Header("Auswertung")]
        public bool logEpisodesToCsv = false;
        [Tooltip("Geht systematisch alle Kombinationen Szenario × Stil × Grösse durch (für faire Auswertung)")]
        public bool evaluationSweep = false;

        /// <summary>Wird nach jeder Episode aufgerufen (für Live-Statistik im StatsHUD).</summary>
        public static event System.Action<CombatSimulation> EpisodeFinished;

        public static readonly float[] SweepMassRatios = { 1.0f, 1.2f, 1.4f, 1.6f, 1.8f };
        static int sweepCounter;

        [Header("Rewards")]
        public float rewardEscape = 1.0f;
        public float rewardGripReleased = 0.2f;
        public float rewardKnockdown = 0.1f;
        public float rewardIncapacitated = 0.3f;
        public float penaltyDamagePerUnit = 0.5f;
        public float penaltyPositionWorsened = 0.3f;
        public float penaltyFail = 1.0f;
        public float penaltyPerDecision = 0.001f;

        public CombatSimulation Sim { get; private set; }
        public float LastDecisionTime { get; private set; }

        public override void Initialize()
        {
            Sim = new CombatSimulation(seed);
        }

        // ================================================================== Episode
        public override void OnEpisodeBegin()
        {
            var p = Academy.Instance.EnvironmentParameters;

            if (evaluationSweep)
            {
                int cell = sweepCounter++;
                int sc = cell % EnumCounts.Scenarios;
                int stIdx = fixedStyle >= 0 ? fixedStyle : (cell / EnumCounts.Scenarios) % EnumCounts.Styles;
                int stCount = fixedStyle >= 0 ? 1 : EnumCounts.Styles;
                float m = SweepMassRatios[(cell / (EnumCounts.Scenarios * stCount)) % SweepMassRatios.Length];
                Sim.Reset((Scenario)sc, (Style)stIdx, m, DefaultStrengthRatio(m));
                Sim.ConsumeEvents();
                return;
            }

            int scenario = fixedScenario >= 0 ? fixedScenario : (int)p.GetWithDefault("scenario", -1f);
            if (scenario < 0 || scenario >= EnumCounts.Scenarios) scenario = Random.Range(0, EnumCounts.Scenarios);

            int style = fixedStyle >= 0 ? fixedStyle : (int)p.GetWithDefault("style", -1f);
            if (style < 0 || style >= EnumCounts.Styles) style = Random.Range(0, EnumCounts.Styles);

            float mass = fixedMassRatio > 0f ? fixedMassRatio : p.GetWithDefault("mass_ratio", -1f);
            if (mass <= 0f)
            {
                float maxMass = p.GetWithDefault("mass_ratio_max", 1.8f);
                mass = Random.Range(1.0f, maxMass);
            }

            float strength = fixedStrengthRatio > 0f ? fixedStrengthRatio : p.GetWithDefault("strength_ratio", -1f);
            if (strength <= 0f) strength = DefaultStrengthRatio(mass);

            Sim.Reset((Scenario)scenario, (Style)style, mass, strength);
            Sim.ConsumeEvents();
        }

        /// <summary>
        /// Annahme: Kraftunterschied wächst schneller als der Masseunterschied
        /// (Oberkörperkraft Männer/Frauen deutlich höher als das Gewichtsverhältnis).
        /// Im Bericht begründen und per Sensitivitätsanalyse variieren!
        /// </summary>
        public static float DefaultStrengthRatio(float massRatio) => Mathf.Pow(massRatio, 1.3f) * 1.1f;

        // ================================================================== Observations
        public override void CollectObservations(VectorSensor sensor)
        {
            var s = Sim;
            float h = s.S.ArenaHalfSize;

            sensor.AddOneHotObservation((int)s.Scenario, EnumCounts.Scenarios);          // 6
            sensor.AddOneHotObservation((int)s.Style, EnumCounts.Styles);                // 8

            sensor.AddObservation(s.MassRatio - 1f);                                     // 1
            sensor.AddObservation((s.StrengthRatio - 1f) / 1.5f);                         // 1

            sensor.AddObservation(s.DefHealth);                                          // 1
            sensor.AddObservation(s.DefStamina);                                         // 1
            sensor.AddObservation(s.DefGrounded);                                        // 1
            sensor.AddObservation(Mathf.Clamp01(s.DefBusy));                              // 1
            sensor.AddObservation(s.DefGuard > 0f);                                      // 1

            sensor.AddOneHotObservation((int)s.Grip, EnumCounts.GripTypes);              // 6

            sensor.AddObservation(s.GripStrength);                                       // 1
            sensor.AddObservation(Mathf.Clamp01(s.AttStun / 3f));                         // 1
            sensor.AddObservation(Mathf.Clamp01(s.AttBalance));                           // 1
            sensor.AddObservation(Mathf.Clamp01(s.AttDown / 3f));                         // 1
            sensor.AddObservation(s.AttDamage);                                          // 1

            sensor.AddObservation((s.AX - s.DX) / (2f * h));                              // 1
            sensor.AddObservation((s.AZ - s.DZ) / (2f * h));                              // 1
            sensor.AddObservation(Mathf.Clamp01(s.Distance / s.S.EscapeDistance));        // 1

            sensor.AddObservation(s.DX / h);                                             // 1
            sensor.AddObservation(s.DZ / h);                                             // 1
            sensor.AddObservation(Mathf.Clamp01(s.SpaceBehind() / (2f * h)));             // 1

            sensor.AddObservation(Mathf.Clamp01(s.Time / s.S.MaxEpisodeTime));            // 1
            // Summe: 39
        }

        // ================================================================== Action Masking
        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
        {
            if (!Sim.CanMove)
                for (int m = 1; m < EnumCounts.MoveActions; m++)
                    actionMask.SetActionEnabled(0, m, false);

            for (int i = 1; i < TechniqueLibrary.Count; i++)
                if (!Sim.IsTechniqueAvailable(i))
                    actionMask.SetActionEnabled(1, i, false);
        }

        // ================================================================== Actions + Rewards
        public override void OnActionReceived(ActionBuffers actions)
        {
            var move = (MoveAction)actions.DiscreteActions[0];
            int tech = actions.DiscreteActions[1];

            float dt = Time.fixedDeltaTime;
            Sim.ApplyDecision(move, tech, dt);
            for (int i = 0; i < ticksPerDecision && !Sim.Done; i++) Sim.Tick(dt);
            LastDecisionTime = Time.time;

            var e = Sim.ConsumeEvents();
            float r = -penaltyPerDecision;
            r -= penaltyDamagePerUnit * e.DamageTaken;
            r += rewardGripReleased * e.GripsReleased;
            r += rewardKnockdown * e.Knockdowns;
            if (e.Incapacitated) r += rewardIncapacitated;
            r -= penaltyPositionWorsened * e.PositionWorsened;
            if (e.Success) r += rewardEscape;
            if (e.Failed) r -= penaltyFail;
            AddReward(r);

            if (Sim.Done)
            {
                RecordStats();
                EndEpisode();
            }
        }

        // ================================================================== Baseline-Heuristik
        /// <summary>
        /// Regelbasierte Baseline (Behavior Type = Heuristic Only): wählt eine zufällige erlaubte Technik,
        /// bewegt sich weg, wenn frei. Dient als Vergleich zum gelernten Agenten.
        /// </summary>
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var d = actionsOut.DiscreteActions;
            d[0] = Sim.CanMove ? (int)MoveAction.AwayFromAttacker : 0;
            if (Sim.CanMove && Sim.SpaceBehind() < 0.5f)
                d[0] = Random.value < 0.5f ? (int)MoveAction.StrafeLeft : (int)MoveAction.StrafeRight;

            int choice = 0, seen = 0;
            for (int i = 1; i < TechniqueLibrary.Count; i++)
            {
                if (!Sim.IsTechniqueAvailable(i)) continue;
                seen++;
                if (Random.Range(0, seen) == 0) choice = i; // Reservoir-Sampling
            }
            d[1] = Random.value < 0.7f ? choice : 0;
        }

        // ================================================================== Statistik
        void RecordStats()
        {
            var stats = Academy.Instance.StatsRecorder;
            float success = Sim.Success ? 1f : 0f;
            string sc = Sim.Scenario.ToString();
            string st = Sim.Style.ToString();

            stats.Add("SelfDefense/Success", success);
            stats.Add($"SelfDefense/Success_{sc}", success);
            stats.Add($"SelfDefense/SuccessStyle_{st}", success);
            stats.Add("SelfDefense/EpisodeTime", Sim.Time);
            stats.Add("SelfDefense/HealthLeft", Mathf.Max(0f, Sim.DefHealth));
            if (Sim.Success) stats.Add("SelfDefense/TimeToEscape", Sim.Time);

            for (int i = 1; i < TechniqueLibrary.Count; i++)
                stats.Add($"SelfDefense/Uses_{TechniqueLibrary.All[i].Name}", Sim.TechniqueUses[i]);

            if (Sim.Success) stats.Add($"SelfDefense/Decisive_{TechniqueLibrary.All[Sim.DecisiveTechnique].Name}", 1f, StatAggregationMethod.Sum);
            stats.Add("SelfDefense/TimePinnedToWall", Sim.TimePinnedToWall);

            if (logEpisodesToCsv) EpisodeCsvLogger.Log(Sim);
            EpisodeFinished?.Invoke(Sim);
        }

        /// <summary>Kurzbeschreibung für die Anzeige über der Figur.</summary>
        public string StatusText()
        {
            var sb = new StringBuilder();
            sb.Append(Sim.Style).Append(" | ").Append(Sim.Scenario).Append('\n');
            if (Sim.DefTechnique > 0) sb.Append(TechniqueLibrary.All[Sim.DefTechnique].Name);
            else sb.Append(Sim.Grip == GripType.None ? (Sim.DefGrounded ? "am Boden" : "frei") : Sim.Grip.ToString());
            sb.Append($"  HP {Sim.DefHealth:0.00}");
            return sb.ToString();
        }
    }
}
