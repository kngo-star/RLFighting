using System;

namespace SelfDefenseRL
{
    /// <summary>
    /// Reines C#-Kampfmodell ohne Unity-Abhängigkeit (testbar, schnell, deterministisch per Seed).
    /// Unity-Komponenten (Environment, Agent) spiegeln nur den Zustand auf GameObjects.
    ///
    /// Koordinaten: Ebene x/z, Arena = Quadrat [-ArenaHalfSize, +ArenaHalfSize].
    /// Alle Zahlen sind Modellannahmen – siehe docs/MODELL.md.
    /// </summary>
    public class CombatSimulation
    {
        // ------------------------------------------------------------------ Parameter
        public class Settings
        {
            public float ArenaHalfSize = 4f;
            public float EscapeDistance = 3f;   // Abstand, ab dem die Flucht gelungen ist
            public float MaxEpisodeTime = 30f;  // Sekunden
            public float DefenderSpeed = 3.5f;  // m/s
            public float AttackerSpeed = 3.0f;  // m/s (grösser = schwerfälliger beim Antritt)
            public float ReleaseRecovery = 0.6f; // s, die der Angreifer nach einem gelösten Griff braucht
            public float GrabReach = 1.0f;
            public float AttackerStrikeDamage = 0.07f;
            public float AttackerBaseHitChance = 0.55f;
            public float ChokeDamagePerSecond = 0.035f;
            public float TakedownChance = 0.12f;
            // Sensitivitätsanalyse: Untergrenzen für MassScaling/StrengthScaling aller Techniken.
            // 0 = Werte wie in TechniqueLibrary. 0.6 = "keine Technik ist unabhängig von Kraft".
            public float MassScalingFloor = 0f;
            public float StrengthScalingFloor = 0f;
            public float WallDriveSpeed = 0.6f; // m/s, mit der der Angreifer im Clinch zur Wand drückt
        }

        public Settings S = new Settings();

        // ------------------------------------------------------------------ Episode-Setup
        public Scenario Scenario { get; private set; }
        public Style Style { get; private set; }
        public float MassRatio { get; private set; }     // Angreifer / Verteidigerin (>= 1)
        public float StrengthRatio { get; private set; } // Angreifer / Verteidigerin (>= 1)
        public float Time { get; private set; }

        // ------------------------------------------------------------------ Verteidigerin
        public float DX, DZ;
        public float DefHealth;      // 1 = unverletzt, 0 = verloren
        public float DefStamina;     // 0..1
        public bool DefGrounded;
        public float DefBusy;        // Restzeit der laufenden Technik
        public int DefTechnique;     // laufende Technik (0 = keine)
        public float DefGuard;       // Restzeit Deckung

        // ------------------------------------------------------------------ Angreifer
        public float AX, AZ;
        public GripType Grip;
        public float GripStrength;   // 0..1
        public float AttStun;        // Sekunden
        public float AttBalance;     // 0..1 (1 = stabil)
        public float AttDown;        // Sekunden am Boden
        public float AttDamage;      // 0..1
        public bool AttIncapacitated;
        float attDecisionTimer;

        // Für die Darstellung: wann zuletzt etwas passiert ist
        public float LastAttackerStrikeTime = -10f;
        public int LastDefTechnique;
        public float LastDefTechniqueTime = -10f;

        // ------------------------------------------------------------------ Ergebnis
        public bool Done { get; private set; }
        public bool Success { get; private set; }
        public string EndReason { get; private set; }
        public int[] TechniqueUses = new int[TechniqueLibrary.Count];
        public int[] TechniqueHits = new int[TechniqueLibrary.Count];
        public int[] TechniqueReleases = new int[TechniqueLibrary.Count];   // wie oft diese Technik einen Griff gelöst hat
        public int[] TechniqueKnockdowns = new int[TechniqueLibrary.Count]; // wie oft sie den Angreifer zu Boden gebracht hat
        public int[] TechniqueWallTurns = new int[TechniqueLibrary.Count];  // wie oft sie die Position an der Wand gedreht hat

        // Auswertung pro Episode
        public int DecisiveTechnique;      // letzte wirksame Technik vor der Flucht (0 = nur Bewegung)
        public float DamageTakenTotal;
        public int TimesGrabbed;
        public int Takedowns;
        public float TimePinnedToWall;     // Sekunden mit Rücken an der Wand, während festgehalten
        public float FirstReleaseTime = -1f;

        /// <summary>Treffer für die Darstellung (Kopf fliegt zurück usw.). Wird von ArenaView abgeholt.</summary>
        public struct HitEvent
        {
            public bool OnDefender;   // true = Verteidigerin wurde getroffen, false = Angreifer
            public HitZone Zone;
            public float Strength;    // ~0..1
            public float DirX, DirZ;  // Richtung der Kraft (Welt, x/z)
            public bool Knockdown;
        }
        public readonly System.Collections.Generic.List<HitEvent> Hits = new System.Collections.Generic.List<HitEvent>();

        /// <summary>Schaden pro Körperzone (Kopf, Rumpf, Beine) für die Anzeige, 0..1.</summary>
        public readonly float[] DefZoneDamage = new float[3];
        public readonly float[] AttZoneDamage = new float[3];

        int resolvingTechnique;            // Technik, die gerade ausgewertet wird
        int lastEffectiveTechnique;
        float gripDirX, gripDirZ = 1f;     // Richtung Verteidigerin → Angreifer beim Greifen

        // Körpermasse → Platzbedarf (Meter)
        public float DefRadius => 0.28f;
        public float AttRadius => 0.3f * (float)Math.Pow(MassRatio, 0.6);
        const float LyingMargin = 1.0f;

        /// <summary>Ereignisse seit dem letzten Abholen (für Rewards).</summary>
        public struct Events
        {
            public float DamageTaken;
            public int GripsReleased;
            public int Knockdowns;
            public bool Incapacitated;
            public int PositionWorsened;
            public int PositionImproved;
            public bool Success;
            public bool Failed;
        }
        Events ev;

        readonly Random rng;

        public CombatSimulation(int seed = 0)
        {
            rng = seed == 0 ? new Random() : new Random(seed);
        }

        float R() => (float)rng.NextDouble();
        float R(float a, float b) => a + (b - a) * R();

        public Events ConsumeEvents()
        {
            var e = ev;
            ev = new Events();
            return e;
        }

        // ================================================================== Reset
        public void Reset(Scenario scenario, Style style, float massRatio, float strengthRatio)
        {
            Scenario = scenario;
            Style = style;
            MassRatio = Math.Max(1f, massRatio);
            StrengthRatio = Math.Max(1f, strengthRatio);
            Time = 0f;
            Done = false;
            Success = false;
            EndReason = "";
            ev = new Events();
            Array.Clear(TechniqueUses, 0, TechniqueUses.Length);
            Array.Clear(TechniqueHits, 0, TechniqueHits.Length);
            Array.Clear(TechniqueReleases, 0, TechniqueReleases.Length);
            Array.Clear(TechniqueKnockdowns, 0, TechniqueKnockdowns.Length);
            Array.Clear(TechniqueWallTurns, 0, TechniqueWallTurns.Length);
            DecisiveTechnique = 0; DamageTakenTotal = 0f; TimesGrabbed = 0; Takedowns = 0;
            TimePinnedToWall = 0f; FirstReleaseTime = -1f;
            Hits.Clear(); Array.Clear(DefZoneDamage, 0, 3); Array.Clear(AttZoneDamage, 0, 3);
            resolvingTechnique = 0; lastEffectiveTechnique = 0;

            DefHealth = 1f; DefStamina = 1f; DefGrounded = false;
            DefBusy = 0f; DefTechnique = 0; DefGuard = 0f;

            Grip = GripType.None; GripStrength = 0f;
            AttStun = 0f; AttBalance = 1f; AttDown = 0f; AttDamage = 0f; AttIncapacitated = false;
            attDecisionTimer = R(0.3f, 0.7f);
            LastAttackerStrikeTime = -10f; LastDefTechnique = 0; LastDefTechniqueTime = -10f;

            float h = S.ArenaHalfSize;
            float jx = R(-1f, 1f), jz = R(-1f, 1f);

            switch (scenario)
            {
                case Scenario.WristGrab:
                    DX = jx; DZ = jz; AX = DX; AZ = DZ + 0.8f;
                    SetGrip(GripType.Wrist, R(0.75f, 0.95f));
                    break;
                case Scenario.PinnedToWall:
                    DX = jx; DZ = -h + 0.3f; AX = DX; AZ = DZ + 0.6f;
                    SetGrip(GripType.FrontPin, R(0.8f, 1f));
                    break;
                case Scenario.BearHugBehind:
                    DX = jx; DZ = jz; AX = DX; AZ = DZ - 0.4f;
                    SetGrip(GripType.BearHug, R(0.8f, 1f));
                    break;
                case Scenario.ChokeFront:
                    DX = jx; DZ = jz; AX = DX; AZ = DZ + 0.6f;
                    SetGrip(GripType.Choke, R(0.75f, 0.95f));
                    break;
                case Scenario.GroundMount:
                    DX = jx; DZ = jz; AX = DX; AZ = DZ;
                    DefGrounded = true;
                    SetGrip(GripType.Mount, R(0.8f, 1f));
                    break;
                case Scenario.CorneredStrikes:
                default:
                    DX = -h + 0.4f; DZ = -h + 0.4f;
                    AX = DX + 1.0f + R(0f, 0.3f); AZ = DZ + 1.0f + R(0f, 0.3f);
                    break;
            }
        }

        void SetGrip(GripType g, float strength)
        {
            if (Grip == GripType.None && g != GripType.None)
            {
                float vx = AX - DX, vz = AZ - DZ;
                float len = (float)Math.Sqrt(vx * vx + vz * vz);
                if (len > 1e-3f) { gripDirX = vx / len; gripDirZ = vz / len; }
                else { gripDirX = 0f; gripDirZ = 1f; }
            }
            Grip = g;
            GripStrength = g == GripType.None ? 0f : Clamp01(strength);
        }

        // ================================================================== Abfragen
        public float Distance => (float)Math.Sqrt((AX - DX) * (AX - DX) + (AZ - DZ) * (AZ - DZ));

        public Context CurrentContext()
        {
            if (DefGrounded) return Grip == GripType.Mount ? Context.GroundMount : Context.GroundFree;
            switch (Grip)
            {
                case GripType.Wrist: return Context.Wrist;
                case GripType.FrontPin: return Context.FrontPin;
                case GripType.BearHug: return Context.BearHug;
                case GripType.Choke: return Context.Choke;
                case GripType.Mount: return Context.GroundMount;
                default: return Context.StandingFree;
            }
        }

        public bool CanMove => !Done && Grip == GripType.None && !DefGrounded && DefBusy <= 0f;

        public bool IsTechniqueAvailable(int index)
        {
            if (index == 0) return true;
            if (Done || DefBusy > 0f) return false;
            var t = TechniqueLibrary.All[index];
            if (!TechniqueLibrary.StyleAllows(t, Style)) return false;
            var ctx = CurrentContext();
            if ((t.Contexts & ctx) == 0) return false;
            if (ctx == Context.StandingFree)
            {
                float d = Distance;
                bool needsRange = t.Damage > 0f || t.Balance > 0f || t.Push > 0f;
                if (needsRange && (d > t.MaxRange || d < t.MinRange)) return false;
            }
            if (DefStamina < t.StaminaCost * 0.5f) return false;
            return true;
        }

        /// <summary>Wie viel Platz hinter der Verteidigerin (weg vom Angreifer) bis zur Wand ist.</summary>
        public float SpaceBehind()
        {
            float vx = DX - AX, vz = DZ - AZ;
            float len = (float)Math.Sqrt(vx * vx + vz * vz);
            if (len < 1e-4f) return S.ArenaHalfSize;
            vx /= len; vz /= len;
            float h = S.ArenaHalfSize, best = 2f * h;
            if (vx > 1e-4f) best = Math.Min(best, (h - DX) / vx);
            if (vx < -1e-4f) best = Math.Min(best, (-h - DX) / vx);
            if (vz > 1e-4f) best = Math.Min(best, (h - DZ) / vz);
            if (vz < -1e-4f) best = Math.Min(best, (-h - DZ) / vz);
            return Math.Max(0f, best);
        }

        // ================================================================== Entscheidung der Verteidigerin
        public void ApplyDecision(MoveAction move, int technique, float dt)
        {
            if (Done) return;

            if (technique > 0 && IsTechniqueAvailable(technique))
            {
                var t = TechniqueLibrary.All[technique];
                DefTechnique = technique;
                DefBusy = Math.Max(0.05f, t.Duration);
                DefStamina = Clamp01(DefStamina - t.StaminaCost);
                TechniqueUses[technique]++;
                if (t.IsGuard) DefGuard = t.Duration + 0.3f;
            }

            pendingMove = CanMove ? move : MoveAction.Stay;
        }

        MoveAction pendingMove = MoveAction.Stay;

        // ================================================================== Simulationsschritt
        public void Tick(float dt)
        {
            if (Done) return;
            Time += dt;

            // --- Verteidigerin: Bewegung
            if (CanMove && pendingMove != MoveAction.Stay) MoveDefender(pendingMove, dt);

            // --- Verteidigerin: laufende Technik abschliessen
            if (DefBusy > 0f)
            {
                DefBusy -= dt;
                if (DefBusy <= 0f)
                {
                    DefBusy = 0f;
                    LastDefTechnique = DefTechnique;
                    LastDefTechniqueTime = Time;
                    ResolveTechnique(DefTechnique);
                    DefTechnique = 0;
                }
            }
            if (DefGuard > 0f) DefGuard -= dt;
            DefStamina = Clamp01(DefStamina + 0.05f * dt);

            // --- Angreifer
            UpdateAttacker(dt);

            // --- Ende prüfen
            if (DefHealth <= 0f) { End(false, "Verteidigerin ausgeschaltet"); return; }
            if (Grip == GripType.None && !DefGrounded && Distance >= S.EscapeDistance)
            {
                End(true, "Flucht gelungen");
                return;
            }
            if (Time >= S.MaxEpisodeTime) End(false, "Zeit abgelaufen");
        }

        void End(bool success, string reason)
        {
            if (success) DecisiveTechnique = lastEffectiveTechnique;
            Done = true;
            Success = success;
            EndReason = reason;
            if (success) ev.Success = true; else ev.Failed = true;
        }

        void MoveDefender(MoveAction m, float dt)
        {
            float vx = DX - AX, vz = DZ - AZ;
            float len = (float)Math.Sqrt(vx * vx + vz * vz);
            if (len < 1e-4f) { vx = 0f; vz = 1f; } else { vx /= len; vz /= len; }

            float mx = 0f, mz = 0f;
            switch (m)
            {
                case MoveAction.AwayFromAttacker: mx = vx; mz = vz; break;
                case MoveAction.TowardAttacker: mx = -vx; mz = -vz; break;
                case MoveAction.StrafeLeft: mx = -vz; mz = vx; break;
                case MoveAction.StrafeRight: mx = vz; mz = -vx; break;
            }
            float speed = S.DefenderSpeed * (0.7f + 0.3f * DefStamina) * (0.6f + 0.4f * DefHealth);
            DX += mx * speed * dt;
            DZ += mz * speed * dt;
            ClampToArena(ref DX, ref DZ, DefRadius);
        }

        // ================================================================== Technik auflösen
        void ResolveTechnique(int index)
        {
            var t = TechniqueLibrary.All[index];
            var ctx = CurrentContext();
            if ((t.Contexts & ctx) == 0) return; // Lage hat sich geändert → verpufft

            float eff = 0.5f + 0.5f * DefStamina;
            float power = (float)Math.Pow(1.0 / MassRatio, Math.Max(t.MassScaling, S.MassScalingFloor));
            bool stunned = AttStun > 0f || AttDown > 0f;

            if (t.IsStandUp) { DefGrounded = false; return; }
            if (t.IsGuard) return;
            resolvingTechnique = index;

            bool isAttack = t.Damage > 0f || t.Balance > 0f;
            bool hit = true;
            if (isAttack)
            {
                float acc = t.Accuracy * eff * (stunned ? 1.3f : 1f);
                hit = R() < Math.Min(0.95f, acc);
                if (hit)
                {
                    TechniqueHits[index]++;
                    float groundPenalty = DefGrounded ? 0.6f : 1f;

                    if (t.Damage > 0f)
                    {
                        float dmg = t.Damage * power * eff * groundPenalty * R(0.7f, 1.2f);
                        AttDamage = Clamp01(AttDamage + dmg);
                        AttZoneDamage[(int)t.Target] = Clamp01(AttZoneDamage[(int)t.Target] + dmg * 2f);
                        AddHit(false, t.Target, dmg * 5f, AX - DX, AZ - DZ, false);
                        float stun = t.StunFactor * dmg * 6f;
                        AttStun = Math.Max(AttStun, stun);
                        if (stun > 0.5f) lastEffectiveTechnique = index;
                        GripStrength = Clamp01(GripStrength - dmg * 1.5f);
                        if (Grip != GripType.None && stun > 1.2f && R() < 0.6f) ReleaseGrip();
                    }
                    if (t.Balance > 0f)
                    {
                        AttBalance -= t.Balance * power * eff * R(0.7f, 1.2f);
                        if (AttBalance <= 0f) { KnockDownAttacker(); AddHit(false, HitZone.Body, 0.8f, AX - DX, AZ - DZ, true); }
                    }
                    if (!AttIncapacitated && AttDamage >= 0.9f)
                    {
                        AttIncapacitated = true;
                        ev.Incapacitated = true;
                        if (Grip != GripType.None) ReleaseGrip();
                        AttDown = Math.Max(AttDown, 8f);
                    }
                }
            }
            else
            {
                TechniqueHits[index]++;
            }

            // Griff lösen (bei Schlägen nur nach Treffer)
            if (hit && t.GripBreak > 0f && Grip != GripType.None)
            {
                float chance = t.GripBreak
                               * (float)Math.Pow(1.0 / StrengthRatio, Math.Max(t.StrengthScaling, S.StrengthScalingFloor))
                               * (1.5f - GripStrength) * eff * (stunned ? 1.8f : 1f);
                if (R() < Math.Min(0.95f, chance)) ReleaseGrip();
            }

            // Unterhaken: Position drehen, danach steht der Angreifer an der Wand
            if (t.TurnsOffWall && (Grip == GripType.FrontPin || Grip == GripType.Choke))
            {
                float chance = t.Accuracy * (float)Math.Pow(1.0 / StrengthRatio, 0.5) * eff * (1.3f - 0.5f * GripStrength);
                if (R() < Math.Min(0.9f, chance)) TurnOffWall(index);
            }

            // Mount auflösen
            if (hit && t.RemovesMount && Grip == GripType.Mount && AttDown > 0f) ReleaseGrip();

            // Abstand schaffen
            if (t.Push > 0f && Grip == GripType.None)
                PushAttacker(t.Push * power * eff);
            resolvingTechnique = 0;
        }

        void ReleaseGrip()
        {
            if (Grip == GripType.None) return;
            bool wasMount = Grip == GripType.Mount;
            Grip = GripType.None;
            GripStrength = 0f;
            ev.GripsReleased++;
            if (resolvingTechnique > 0)
            {
                TechniqueReleases[resolvingTechnique]++;
                lastEffectiveTechnique = resolvingTechnique;
            }
            if (FirstReleaseTime < 0f) FirstReleaseTime = Time;
            AttStun = Math.Max(AttStun, S.ReleaseRecovery); // kurz aus dem Gleichgewicht
            // Angreifer kommt nicht direkt auf der Verteidigerin zu stehen
            if (Distance < 0.6f || wasMount) PushAttacker(0.7f);
        }

        void TurnOffWall(int index)
        {
            float tx = DX, tz = DZ;
            DX = AX; DZ = AZ;
            AX = tx; AZ = tz;
            gripDirX = -gripDirX; gripDirZ = -gripDirZ;
            ClampToArena(ref DX, ref DZ, DefRadius);
            ClampToArena(ref AX, ref AZ, AttRadius);
            SetGrip(GripType.Wrist, GripStrength * 0.7f); // Kontrolle nur noch am Arm
            TechniqueWallTurns[index]++;
            lastEffectiveTechnique = index;
            ev.PositionImproved++;
        }

        void KnockDownAttacker()
        {
            AttDown = Math.Max(AttDown, 1.5f + AttDamage * 2f);
            ev.Knockdowns++;
            if (resolvingTechnique > 0)
            {
                TechniqueKnockdowns[resolvingTechnique]++;
                lastEffectiveTechnique = resolvingTechnique;
            }
            // liegender Angreifer braucht Platz
            ClampToArena(ref AX, ref AZ, LyingMargin * (float)Math.Pow(MassRatio, 0.25));
            if (Grip != GripType.None) ReleaseGrip();
        }

        void PushAttacker(float amount)
        {
            float vx = AX - DX, vz = AZ - DZ;
            float len = (float)Math.Sqrt(vx * vx + vz * vz);
            if (len < 1e-4f) { vx = R(-1f, 1f); vz = R(-1f, 1f); len = (float)Math.Sqrt(vx * vx + vz * vz) + 1e-4f; }
            AX += vx / len * amount;
            AZ += vz / len * amount;
            ClampToArena(ref AX, ref AZ, AttRadius);
        }

        // ================================================================== Angreifer (Skript)
        void UpdateAttacker(float dt)
        {
            AttBalance = Math.Min(1f, AttBalance + 0.25f * dt);

            if (AttDown > 0f)
            {
                AttDown -= dt;
                if (AttDown <= 0f) { AttDown = 0f; AttBalance = 0.7f; }
                return;
            }
            if (AttStun > 0f)
            {
                AttStun -= dt;
                GripStrength = Clamp01(GripStrength - 0.4f * dt);
                if (Grip != GripType.None && GripStrength <= 0f) ReleaseGrip();
                return;
            }

            float maxGrip = 1f - 0.6f * AttDamage;
            if (Grip != GripType.None) GripStrength = Math.Min(maxGrip, GripStrength + 0.05f * dt);

            // Würgen = kontinuierlicher Schaden
            if (Grip == GripType.Choke)
            {
                float c = S.ChokeDamagePerSecond * (float)Math.Sqrt(StrengthRatio) * GripStrength * dt;
                Hurt(c);
                DefZoneDamage[0] = Clamp01(DefZoneDamage[0] + c * 1.5f); // Hals/Kopf
            }

            // Griff → Angreifer klebt an der Verteidigerin
            if (Grip != GripType.None) FollowDefender(dt);
            else if (Distance > 0.8f)
            {
                float speed = S.AttackerSpeed * (1f - 0.5f * AttDamage);
                float vx = DX - AX, vz = DZ - AZ, len = Distance;
                AX += vx / len * speed * dt;
                AZ += vz / len * speed * dt;
                ClampToArena(ref AX, ref AZ, AttRadius);
            }

            attDecisionTimer -= dt;
            if (attDecisionTimer > 0f) return;
            attDecisionTimer = R(0.4f, 0.8f);
            AttackerDecide();
        }

        void AttackerDecide()
        {
            float r = R();
            switch (Grip)
            {
                case GripType.Mount:
                    if (r < 0.6f) AttackerStrike(1.3f, 0.15f);
                    else GripStrength = Math.Min(1f, GripStrength + 0.1f);
                    break;
                case GripType.Choke:
                    GripStrength = Math.Min(1f, GripStrength + 0.08f);
                    if (r < 0.1f) Takedown();
                    break;
                case GripType.BearHug:
                    if (r < S.TakedownChance * 1.3f) Takedown();
                    else GripStrength = Math.Min(1f, GripStrength + 0.08f);
                    break;
                case GripType.Wrist:
                    if (r < 0.25f) SetGrip(GripType.FrontPin, GripStrength);
                    else if (r < 0.65f) AttackerStrike(1f, 0f);
                    break;
                case GripType.FrontPin:
                    if (r < S.TakedownChance) Takedown();
                    else if (r < 0.6f) AttackerStrike(1f, 0.05f);
                    break;
                default: // frei
                    if (Distance > S.GrabReach) break;
                    if (DefGrounded)
                    {
                        if (r < 0.4f) { SetGrip(GripType.Mount, 0.8f); ev.PositionWorsened++; }
                        else AttackerStrike(1.2f, 0.1f);
                        break;
                    }
                    if (r < 0.5f) TryGrab();
                    else AttackerStrike(1f, 0f);
                    break;
            }
        }

        void TryGrab()
        {
            float chance = 0.55f * AttBalance + (DefBusy > 0f ? 0.15f : 0f);
            if (R() >= chance) return;

            bool nearWall = DistanceToWall(DX, DZ) < 0.8f;
            float r = R();
            GripType g;
            if (nearWall) g = r < 0.7f ? GripType.FrontPin : GripType.Choke;
            else if (r < 0.45f) g = GripType.Wrist;
            else if (r < 0.75f) g = GripType.FrontPin;
            else if (r < 0.9f) g = GripType.Choke;
            else g = GripType.BearHug;

            SetGrip(g, R(0.7f, 0.9f) * (1f - 0.6f * AttDamage));
            TimesGrabbed++;
            ev.PositionWorsened++;
        }

        void Takedown()
        {
            DefGrounded = true;
            Takedowns++;
            ClampToArena(ref DX, ref DZ, LyingMargin); // liegend braucht man Platz (Kopf nicht in der Wand)
            SetGrip(GripType.Mount, Math.Max(GripStrength, 0.7f));
            DefBusy = 0f; DefTechnique = 0;
            Hurt(0.05f);
            AddHit(true, HitZone.Body, 0.7f, DX - AX, DZ - AZ, true);
            ev.PositionWorsened++;
        }

        void AttackerStrike(float damageMult, float hitBonus)
        {
            float exposure = DefBusy > 0f ? TechniqueLibrary.All[DefTechnique].Exposure : 0f;
            float guard = DefGuard > 0f ? 0.25f : 0f;
            LastAttackerStrikeTime = Time;
            float hitChance = S.AttackerBaseHitChance + hitBonus + exposure - guard;
            if (R() >= hitChance) return;
            float dmg = S.AttackerStrikeDamage * damageMult * (float)Math.Sqrt(StrengthRatio)
                        * (1f - 0.5f * AttDamage) * (DefGuard > 0f ? 0.4f : 1f) * R(0.7f, 1.3f);
            Hurt(dmg);
            var zone = DefGuard > 0f ? HitZone.Body : (R() < 0.65f ? HitZone.Head : HitZone.Body); // Deckung schützt den Kopf
            DefZoneDamage[(int)zone] = Clamp01(DefZoneDamage[(int)zone] + dmg * 2.5f);
            AddHit(true, zone, dmg * 6f, DX - AX, DZ - AZ, false);
        }

        void Hurt(float amount)
        {
            DefHealth -= amount;
            DamageTakenTotal += amount;
            ev.DamageTaken += amount;
        }

        /// <summary>
        /// Angreifer hält fest: er bleibt auf der Seite, von der er gegriffen hat, und drückt die
        /// Verteidigerin bei Clinch/Würgen rückwärts gegen die Wand. Wände sind fest.
        /// </summary>
        void FollowDefender(float dt)
        {
            float off;
            switch (Grip)
            {
                case GripType.Wrist: off = 0.8f; break;
                case GripType.BearHug: off = 0.35f; break;
                case GripType.Mount: off = 0f; break;
                default: off = 0.25f + AttRadius; break; // FrontPin, Choke: Brust an Brust
            }

            // Zur Wand drücken (nur stehend, Clinch oder Würgen)
            if (!DefGrounded && (Grip == GripType.FrontPin || Grip == GripType.Choke))
            {
                float drive = S.WallDriveSpeed * (float)Math.Sqrt(StrengthRatio) * GripStrength * dt;
                DX -= gripDirX * drive;
                DZ -= gripDirZ * drive;
            }

            float margin = DefGrounded ? LyingMargin : DefRadius;
            ClampToArena(ref DX, ref DZ, margin);
            AX = DX + gripDirX * off;
            AZ = DZ + gripDirZ * off;

            // Angreifer passt nicht mehr hin (Wand hinter ihm) → beide verschieben
            float ax = AX, az = AZ;
            ClampToArena(ref AX, ref AZ, AttRadius);
            if (Math.Abs(ax - AX) > 1e-4f || Math.Abs(az - AZ) > 1e-4f)
            {
                DX = AX - gripDirX * off;
                DZ = AZ - gripDirZ * off;
                ClampToArena(ref DX, ref DZ, margin);
            }

            if (!DefGrounded && SpaceBehind() < DefRadius + 0.15f) TimePinnedToWall += dt;
        }

        // ================================================================== Hilfen
        void AddHit(bool onDefender, HitZone zone, float strength, float dx, float dz, bool knockdown)
        {
            float len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-4f) { dx = 0f; dz = 1f; len = 1f; }
            if (Hits.Count < 64)
                Hits.Add(new HitEvent { OnDefender = onDefender, Zone = zone, Strength = Math.Min(1.2f, strength),
                                        DirX = dx / len, DirZ = dz / len, Knockdown = knockdown });
        }

        float DistanceToWall(float x, float z)
        {
            float h = S.ArenaHalfSize;
            return Math.Min(Math.Min(h - x, x + h), Math.Min(h - z, z + h));
        }

        void ClampToArena(ref float x, ref float z, float margin)
        {
            float h = S.ArenaHalfSize - margin;
            x = Math.Max(-h, Math.Min(h, x));
            z = Math.Max(-h, Math.Min(h, z));
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
