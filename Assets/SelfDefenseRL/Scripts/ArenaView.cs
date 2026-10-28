using System.Collections.Generic;
using UnityEngine;

namespace SelfDefenseRL
{
    /// <summary>
    /// Zeigt den Zustand der Simulation mit zwei menschlichen Figuren an (nur Darstellung, keine Logik).
    /// Blau = Verteidigerin, Rot = Angreifer (grösser und breiter je nach Masseverhältnis).
    /// </summary>
    public class ArenaView : MonoBehaviour
    {
        public DefenderAgent agent;
        public HumanoidFigure defender;
        public HumanoidFigure attacker;
        public TextMesh label;
        public LineRenderer gripLine;
        public float smoothing = 12f;
        [Tooltip("So lange (Sekunden Simulationszeit) bleibt eine Schlag-/Technik-Pose sichtbar")]
        public float poseHold = 0.3f;
        [Tooltip("Grösse der Verteidigerin relativ zur Standardfigur (1.75 m)")]
        public float defenderScale = 0.92f;

        static readonly Color AttackerColor = new Color(0.8f, 0.18f, 0.18f);
        static readonly Color StunColor = new Color(1f, 0.8f, 0.2f);
        static readonly Color DownColor = new Color(0.45f, 0.45f, 0.45f);
        static readonly Color DefenderColor = new Color(0.2f, 0.45f, 0.95f);
        static readonly Color DefenderHurtColor = new Color(0.55f, 0.1f, 0.55f);
        static readonly Color SuccessColor = new Color(0.2f, 0.85f, 0.3f);

        MaterialPropertyBlock block;
        readonly List<Renderer> attackerBody = new List<Renderer>();
        readonly List<Renderer> defenderBody = new List<Renderer>();
        float defenderYaw, attackerYaw;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            CollectBody(attacker, attackerBody);
            CollectBody(defender, defenderBody);
        }

        static void CollectBody(HumanoidFigure fig, List<Renderer> list)
        {
            list.Clear();
            if (fig == null) return;
            foreach (var r in fig.GetComponentsInChildren<Renderer>())
                if (fig.bodyMaterial == null || r.sharedMaterial == fig.bodyMaterial) list.Add(r);
        }

        void LateUpdate()
        {
            if (agent == null || agent.Sim == null || defender == null || attacker == null) return;
            var s = agent.Sim;
            float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);

            var dPos = new Vector3(s.DX, 0f, s.DZ);
            var aPos = new Vector3(s.AX, 0f, s.AZ);

            // --- Blickrichtungen
            Vector3 toAtt = aPos - dPos;
            if (toAtt.sqrMagnitude > 0.0004f)
            {
                float yaw = Mathf.Atan2(toAtt.x, toAtt.z) * Mathf.Rad2Deg;
                if (!s.DefGrounded)
                    defenderYaw = s.Grip == GripType.BearHug ? yaw + 180f : yaw; // Umklammerung von hinten
                if (s.Grip != GripType.Mount && s.AttDown <= 0f)
                    attackerYaw = yaw + 180f;
            }
            if (s.Grip == GripType.Mount) attackerYaw = defenderYaw + 180f;

            // --- Verteidigerin
            Vector3 dForward = Quaternion.Euler(0f, defenderYaw, 0f) * Vector3.forward;
            defender.transform.localPosition = Vector3.Lerp(defender.transform.localPosition, dPos, k);
            defender.transform.localRotation = Quaternion.Slerp(defender.transform.localRotation, Quaternion.Euler(0f, defenderYaw, 0f), k);
            defender.SetPose(DefenderPose(s));

            // --- Grössen: Verteidigerin kleiner, Angreifer grösser und deutlich breiter
            defender.transform.localScale = new Vector3(defenderScale, defenderScale, defenderScale);
            float height = Mathf.Pow(s.MassRatio, 0.25f);
            float width = Mathf.Pow(s.MassRatio, 0.6f);
            attacker.transform.localScale = new Vector3(width, height, width);
            if (s.Grip == GripType.Mount) aPos = dPos - dForward * 0.25f; // kniet über dem Oberkörper
            attacker.transform.localPosition = Vector3.Lerp(attacker.transform.localPosition, aPos, k);
            attacker.transform.localRotation = Quaternion.Slerp(attacker.transform.localRotation, Quaternion.Euler(0f, attackerYaw, 0f), k);
            attacker.SetPose(AttackerPose(s));

            // --- Farben
            bool down = s.AttDown > 0f;
            SetColor(attackerBody, down ? DownColor : (s.AttStun > 0f ? StunColor : AttackerColor));
            SetColor(defenderBody, s.Done && s.Success ? SuccessColor : Color.Lerp(DefenderHurtColor, DefenderColor, s.DefHealth));

            // --- Griff-Linie
            if (gripLine)
            {
                gripLine.enabled = s.Grip != GripType.None;
                if (gripLine.enabled)
                {
                    gripLine.SetPosition(0, defender.ChestPosition);
                    gripLine.SetPosition(1, attacker.ChestPosition);
                }
            }

            // --- Text
            if (label)
            {
                label.text = agent.StatusText();
                label.transform.position = defender.transform.position + Vector3.up * 2.1f;
                var cam = Camera.main;
                if (cam) label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position);
            }
        }

        // ------------------------------------------------------------------ Posen
        Pose DefenderPose(CombatSimulation s)
        {
            int tech = s.DefTechnique;
            if (tech == 0 && s.Time - s.LastDefTechniqueTime < poseHold) tech = s.LastDefTechnique;

            if (s.DefGrounded)
            {
                if (tech > 0)
                {
                    string n = TechniqueLibrary.All[tech].Name;
                    if (n == "BridgeRoll") return Pose.Bridge;
                    if (n == "HipEscape") return Pose.HipEscape;
                    if (n == "StandUp") return Pose.StandUp;
                }
                return Pose.LyingBack;
            }

            if (tech > 0) return TechniquePose(TechniqueLibrary.All[tech].Name);
            if (s.DefGuard > 0f) return Pose.Guard;
            return s.Grip == GripType.None ? Pose.Guard : Pose.Idle;
        }

        Pose AttackerPose(CombatSimulation s)
        {
            if (s.AttDown > 0f) return Pose.LyingBack;
            if (s.AttStun > 0f) return Pose.Stunned;
            switch (s.Grip)
            {
                case GripType.Mount: return Pose.Kneel;
                case GripType.Choke: return Pose.Choke;
                case GripType.Wrist:
                case GripType.FrontPin:
                case GripType.BearHug:
                    return s.Time - s.LastAttackerStrikeTime < poseHold ? Pose.Punch : Pose.Grab;
            }
            return s.Time - s.LastAttackerStrikeTime < poseHold ? Pose.Punch : Pose.Guard;
        }

        static Pose TechniquePose(string name)
        {
            switch (name)
            {
                case "PalmStrike":
                case "JabCross":
                case "ReversePunch":
                case "FaceStrike":
                case "HammerFist": return Pose.Punch;
                case "Elbow": return Pose.Elbow;
                case "KneeGroin": return Pose.Knee;
                case "KickKnee":
                case "RoundKickThigh":
                case "FrontKick":
                case "SideKick": return Pose.Kick;
                case "Frame":
                case "PushOff": return Pose.Push;
                case "GripReleaseThumb":
                case "ChokeDefense":
                case "WristLock": return Pose.Release;
                case "Underhook": return Pose.Throw;
                case "DropWeight": return Pose.DropWeight;
                case "HipThrow": return Pose.Throw;
                case "Guard": return Pose.Guard;
                default: return Pose.Idle;
            }
        }

        void SetColor(List<Renderer> renderers, Color c)
        {
            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", c); // URP
                block.SetColor("_Color", c);     // Built-in
                r.SetPropertyBlock(block);
            }
        }
    }
}
