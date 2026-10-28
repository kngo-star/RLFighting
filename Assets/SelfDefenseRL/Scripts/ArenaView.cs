using UnityEngine;

namespace SelfDefenseRL
{
    /// <summary>
    /// Zeigt den Zustand der Simulation auf den GameObjects an (nur Darstellung, keine Logik).
    /// Kapsel blau = Verteidigerin, Kapsel rot = Angreifer (Grösse skaliert mit dem Masseverhältnis).
    /// </summary>
    public class ArenaView : MonoBehaviour
    {
        public DefenderAgent agent;
        public Transform defender;
        public Transform attacker;
        public TextMesh label;
        public LineRenderer gripLine;
        public float smoothing = 15f;

        Renderer attackerRenderer;
        Renderer defenderRenderer;
        static readonly Color AttackerColor = new Color(0.85f, 0.2f, 0.2f);
        static readonly Color StunColor = new Color(1f, 0.85f, 0.2f);
        static readonly Color DownColor = new Color(0.45f, 0.45f, 0.45f);
        static readonly Color DefenderColor = new Color(0.2f, 0.45f, 0.95f);
        static readonly Color DefenderHurtColor = new Color(0.6f, 0.1f, 0.6f);
        static readonly Color SuccessColor = new Color(0.2f, 0.85f, 0.3f);

        MaterialPropertyBlock block;

        void Awake()
        {
            if (attacker) attackerRenderer = attacker.GetComponentInChildren<Renderer>();
            if (defender) defenderRenderer = defender.GetComponentInChildren<Renderer>();
            block = new MaterialPropertyBlock();
        }

        void LateUpdate()
        {
            if (agent == null || agent.Sim == null) return;
            var s = agent.Sim;
            float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);

            // --- Verteidigerin
            var dTarget = new Vector3(s.DX, s.DefGrounded ? 0.3f : 1f, s.DZ);
            defender.localPosition = Vector3.Lerp(defender.localPosition, dTarget, k);
            var dRot = s.DefGrounded ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            defender.localRotation = Quaternion.Slerp(defender.localRotation, dRot, k);

            // --- Angreifer (Volumen ~ Masse → Skalierung mit Kubikwurzel)
            float scale = Mathf.Pow(s.MassRatio, 1f / 3f);
            attacker.localScale = new Vector3(scale, scale, scale);
            bool down = s.AttDown > 0f;
            var aTarget = new Vector3(s.AX, down ? 0.35f * scale : 1f * scale, s.AZ);
            if (s.Grip == GripType.Mount) aTarget.y = 0.9f * scale;
            attacker.localPosition = Vector3.Lerp(attacker.localPosition, aTarget, k);
            var aRot = down ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
            attacker.localRotation = Quaternion.Slerp(attacker.localRotation, aRot, k);

            // --- Farben
            SetColor(attackerRenderer, down ? DownColor : (s.AttStun > 0f ? StunColor : AttackerColor));
            Color dc = s.Done && s.Success ? SuccessColor : Color.Lerp(DefenderHurtColor, DefenderColor, s.DefHealth);
            SetColor(defenderRenderer, dc);

            // --- Griff-Linie
            if (gripLine)
            {
                gripLine.enabled = s.Grip != GripType.None;
                if (gripLine.enabled)
                {
                    gripLine.SetPosition(0, defender.position);
                    gripLine.SetPosition(1, attacker.position);
                }
            }

            // --- Text
            if (label)
            {
                label.text = agent.StatusText();
                label.transform.position = defender.position + Vector3.up * 1.6f;
                var cam = Camera.main;
                if (cam) label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position);
            }
        }

        void SetColor(Renderer r, Color c)
        {
            if (r == null) return;
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", c); // URP
            block.SetColor("_Color", c);     // Built-in
            r.SetPropertyBlock(block);
        }
    }
}
