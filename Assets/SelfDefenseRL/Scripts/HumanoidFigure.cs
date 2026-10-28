using System.Collections.Generic;
using UnityEngine;

namespace SelfDefenseRL
{
    /// <summary>Körperhaltungen, die eine Figur einnehmen kann.</summary>
    public enum Pose
    {
        Idle, Guard, Punch, Elbow, Knee, Kick, Push, Release, DropWeight, Throw,
        Grab, Choke, Kneel, Stunned, LyingBack, Bridge, HipEscape, StandUp,
        Poke, Bite, Headbutt, GroinKick
    }

    /// <summary>
    /// Menschliche Figur aus Grundkörpern mit sichtbaren Gelenken (Nacken, Schultern, Ellbogen, Handgelenke,
    /// Hüften, Knie, Knöchel). Wird vom SceneBuilder erzeugt und von <see cref="ArenaView"/> gesteuert.
    ///  - Posen pro Technik (weich überblendet)
    ///  - Treffer-Physik: gedämpfte Federn pro Gelenk + Becken (Kopf schnappt zurück, Rumpf klappt ein …)
    ///  - Beinarbeit: Schritte aus der Bewegung
    ///  - Verletzungsanzeige pro Körperteil: grün = unverletzt, gelb = angeschlagen, rot = schwer verletzt
    /// Pivot zwischen den Füssen auf dem Boden, Blickrichtung +Z.
    /// </summary>
    public class HumanoidFigure : MonoBehaviour
    {
        // Gelenke (Reihenfolge der Pose-Tabellen: spine, head, shL, elL, shR, elR, hipL, knL, hipR, knR, pelvis)
        public Transform pelvis, spine, head, shoulderL, elbowL, shoulderR, elbowR, hipL, kneeL, hipR, kneeR;
        public Transform neck, wristL, wristR, ankleL, ankleR;
        public float poseSpeed = 12f;
        [Tooltip("Teamfarbe (Stirnband, Armbänder, Bodenring)")]
        public Material bodyMaterial;
        public Material jointMaterial;

        const float PelvisHeight = 0.95f;

        Pose currentPose = Pose.Idle;
        bool poseInitialized;
        Transform[] joints;
        Quaternion[] target, current;

        public Pose CurrentPose => currentPose;
        public Vector3 ChestPosition => spine != null ? spine.position + spine.up * 0.35f : transform.position + Vector3.up * 1.3f;
        public Vector3 HeadPosition => head != null ? head.position + head.up * 0.12f : transform.position + Vector3.up * 1.7f;

        // Körperteil-Renderer (Index = BodyPart) und Team-Renderer
        [SerializeField] List<Renderer> partHead = new List<Renderer>(), partTorso = new List<Renderer>(), partGroin = new List<Renderer>(),
                                        partArmL = new List<Renderer>(), partArmR = new List<Renderer>(),
                                        partLegL = new List<Renderer>(), partLegR = new List<Renderer>(), teamParts = new List<Renderer>();

        // ================================================================== Aufbau
        /// <summary>Erzeugt den Körper als Kinder dieses Objekts (im Editor oder zur Laufzeit).</summary>
        public void Build(Material teamMaterial, Material skinMaterial, Material jointMat = null)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
            bodyMaterial = teamMaterial;
            jointMaterial = jointMat != null ? jointMat : skinMaterial;
            foreach (var l in AllPartLists()) l.Clear();
            teamParts.Clear();

            // Becken / Leiste
            pelvis = Joint("Pelvis", transform, new Vector3(0f, PelvisHeight, 0f));
            Part(PrimitiveType.Cube, pelvis, V3(0f, -0.02f, 0f), V3(0.33f, 0.18f, 0.21f), skinMaterial, partGroin);

            // Rumpf
            spine = Joint("Spine", pelvis, V3(0f, 0.08f, 0f));
            Part(PrimitiveType.Capsule, spine, V3(0f, 0.15f, 0f), V3(0.3f, 0.16f, 0.2f), skinMaterial, partTorso);   // Bauch
            Part(PrimitiveType.Capsule, spine, V3(0f, 0.36f, 0f), V3(0.4f, 0.18f, 0.24f), skinMaterial, partTorso);  // Brust

            // Nacken + Kopf
            neck = Joint("Neck", spine, V3(0f, 0.52f, 0f));
            Ball(neck, 0.1f);
            Part(PrimitiveType.Cylinder, neck, V3(0f, 0.04f, 0f), V3(0.09f, 0.05f, 0.09f), skinMaterial, partTorso);
            head = Joint("Head", neck, V3(0f, 0.08f, 0f));
            Part(PrimitiveType.Sphere, head, V3(0f, 0.12f, 0f), V3(0.22f, 0.26f, 0.24f), skinMaterial, partHead);
            Part(PrimitiveType.Cube, head, V3(0f, 0.12f, 0.12f), V3(0.05f, 0.05f, 0.05f), skinMaterial, partHead);  // Nase
            Part(PrimitiveType.Sphere, head, V3(-0.05f, 0.16f, 0.105f), V3(0.035f, 0.035f, 0.02f), jointMaterial, null); // Augen
            Part(PrimitiveType.Sphere, head, V3(0.05f, 0.16f, 0.105f), V3(0.035f, 0.035f, 0.02f), jointMaterial, null);
            Part(PrimitiveType.Cylinder, head, V3(0f, 0.2f, 0f), V3(0.235f, 0.025f, 0.25f), teamMaterial, teamParts); // Stirnband

            // Arme
            BuildArm(-1f, skinMaterial, teamMaterial, out shoulderL, out elbowL, out wristL, partArmL);
            BuildArm(1f, skinMaterial, teamMaterial, out shoulderR, out elbowR, out wristR, partArmR);

            // Beine
            BuildLeg(-1f, skinMaterial, out hipL, out kneeL, out ankleL, partLegL);
            BuildLeg(1f, skinMaterial, out hipR, out kneeR, out ankleR, partLegR);

            // Teamring am Boden
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "TeamRing";
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = V3(0f, 0.01f, 0f);
            ring.transform.localScale = V3(0.75f, 0.005f, 0.75f);
            DestroyImmediate(ring.GetComponent<Collider>());
            ring.GetComponent<Renderer>().sharedMaterial = teamMaterial;
            teamParts.Add(ring.GetComponent<Renderer>());

            CacheJoints();
            SetPose(Pose.Idle, true);
        }

        void BuildArm(float side, Material skin, Material team, out Transform sh, out Transform el, out Transform wr, List<Renderer> list)
        {
            string s = side < 0 ? "L" : "R";
            sh = Joint("Shoulder" + s, spine, V3(0.23f * side, 0.47f, 0f));
            Ball(sh, 0.12f);
            Part(PrimitiveType.Capsule, sh, V3(0f, -0.15f, 0f), V3(0.1f, 0.15f, 0.1f), skin, list);
            el = Joint("Elbow" + s, sh, V3(0f, -0.3f, 0f));
            Ball(el, 0.095f);
            Part(PrimitiveType.Capsule, el, V3(0f, -0.13f, 0f), V3(0.085f, 0.13f, 0.085f), skin, list);
            wr = Joint("Wrist" + s, el, V3(0f, -0.27f, 0f));
            Part(PrimitiveType.Cylinder, wr, V3(0f, 0.02f, 0f), V3(0.095f, 0.025f, 0.095f), team, teamParts); // Armband
            Part(PrimitiveType.Cube, wr, V3(0f, -0.06f, 0.01f), V3(0.07f, 0.11f, 0.04f), skin, list);          // Hand
        }

        void BuildLeg(float side, Material skin, out Transform hip, out Transform kn, out Transform an, List<Renderer> list)
        {
            string s = side < 0 ? "L" : "R";
            hip = Joint("Hip" + s, pelvis, V3(0.1f * side, -0.08f, 0f));
            Ball(hip, 0.13f);
            Part(PrimitiveType.Capsule, hip, V3(0f, -0.21f, 0f), V3(0.14f, 0.21f, 0.14f), skin, list);
            kn = Joint("Knee" + s, hip, V3(0f, -0.42f, 0f));
            Ball(kn, 0.115f);
            Part(PrimitiveType.Capsule, kn, V3(0f, -0.2f, 0f), V3(0.11f, 0.2f, 0.11f), skin, list);
            an = Joint("Ankle" + s, kn, V3(0f, -0.4f, 0f));
            Ball(an, 0.08f);
            Part(PrimitiveType.Cube, an, V3(0f, -0.04f, 0.06f), V3(0.1f, 0.06f, 0.24f), skin, list);  // Fuss
        }

        IEnumerable<List<Renderer>> AllPartLists()
        {
            yield return partHead; yield return partTorso; yield return partGroin;
            yield return partArmL; yield return partArmR; yield return partLegL; yield return partLegR;
        }

        static Vector3 V3(float x, float y, float z) => new Vector3(x, y, z);

        static Transform Joint(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        void Ball(Transform parent, float size) => Part(PrimitiveType.Sphere, parent, Vector3.zero, new Vector3(size, size, size), jointMaterial, null);

        static void Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat, List<Renderer> list)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = type.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
            var r = go.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            if (list != null) list.Add(r);
        }

        void CacheJoints()
        {
            joints = new[] { spine, head, shoulderL, elbowL, shoulderR, elbowR, hipL, kneeL, hipR, kneeR, pelvis, neck, wristL, wristR, ankleL, ankleR };
            target = new Quaternion[joints.Length];
            current = new Quaternion[joints.Length];
            reactAngle = new Vector3[joints.Length];
            reactVel = new Vector3[joints.Length];
            for (int i = 0; i < joints.Length; i++)
            {
                target[i] = Quaternion.identity;
                current[i] = joints[i] != null ? joints[i].localRotation : Quaternion.identity;
            }
        }

        // ================================================================== Posen
        // Reihenfolge: spine, head, shoulderL, elbowL, shoulderR, elbowR, hipL, kneeL, hipR, kneeR, pelvis
        // Schulter/Hüfte x = -90 → Arm/Bein zeigt nach vorne. Ellbogen x < 0 beugt nach vorne, Knie x > 0 nach hinten.
        static Vector3[] Angles(Pose p)
        {
            Vector3 z = Vector3.zero;
            switch (p)
            {
                case Pose.Guard:
                    return new[] { V(8), z, V(-115, 0, 10), V(-110), V(-115, 0, -10), V(-110), V(-15), V(20), V(10), V(10), z };
                case Pose.Punch:
                    return new[] { new Vector3(5, -25, 0), z, V(-115, 0, 10), V(-110), V(-92), V(-3), V(-20), V(20), V(15), V(5), z };
                case Pose.Poke:
                    return new[] { new Vector3(10, -20, 0), V(5), V(-115, 0, 10), V(-110), V(-105), V(0), V(-20), V(20), V(15), V(5), z };
                case Pose.Elbow:
                    return new[] { new Vector3(5, -35, 0), z, V(-115, 0, 10), V(-110), V(-85, 0, 30), V(-140), V(-10), V(10), V(10), V(5), z };
                case Pose.Knee:
                    return new[] { V(15), z, V(-70), V(-30), V(-70), V(-30), V(5), V(5), V(-100), V(100), z };
                case Pose.Kick:
                    return new[] { V(-12), z, V(-60, 0, 20), V(-90), V(-40), V(-60), V(5), V(10), V(-80), V(5), z };
                case Pose.GroinKick:
                    return new[] { V(-5), V(10), V(-60, 0, 20), V(-90), V(-50), V(-60), V(5), V(10), V(-60), V(10), z };
                case Pose.Push:
                    return new[] { V(15), z, V(-85), V(-10), V(-85), V(-10), V(-25), V(20), V(20), V(5), z };
                case Pose.Release:
                    return new[] { V(25), V(10), V(-45), V(-70), V(-45, 0, -15), V(-80), V(-20), V(30), V(-10), V(25), z };
                case Pose.DropWeight:
                    return new[] { V(30), V(10), V(-30, 0, 25), V(-60), V(-30, 0, -25), V(-60), V(-60), V(85), V(-60), V(85), z };
                case Pose.Throw:
                    return new[] { new Vector3(40, 60, 0), z, V(-80), V(-40), V(-90), V(-20), V(-30), V(40), V(-40), V(30), z };
                case Pose.Bite:
                    return new[] { V(35), V(35), V(-60), V(-60), V(-60), V(-60), V(-20), V(25), V(-10), V(15), z };
                case Pose.Headbutt:
                    return new[] { V(30), V(30), V(-50, 0, 20), V(-70), V(-50, 0, -20), V(-70), V(-20), V(20), V(10), V(10), z };
                case Pose.Grab:
                    return new[] { V(10), z, V(-85, 0, 10), V(-20), V(-85, 0, -10), V(-20), V(-10), V(10), V(15), V(5), z };
                case Pose.Choke:
                    return new[] { V(10), z, V(-100, 0, 15), V(-25), V(-100, 0, -15), V(-25), V(-10), V(10), V(15), V(5), z };
                case Pose.Kneel:
                    return new[] { V(25), V(20), V(-70), V(-40), V(-70), V(-40), V(-20), V(110), V(-20), V(110), z };
                case Pose.Stunned:
                    return new[] { V(25), V(35), V(-5, 0, 15), V(-20), V(-5, 0, -15), V(-20), V(-15), V(30), V(10), V(25), z };
                case Pose.LyingBack:
                    return new[] { z, V(-15), V(-70, 0, 15), V(-100), V(-70, 0, -15), V(-100), V(-35), V(70), V(-5), V(10), V(-90) };
                case Pose.Bridge:
                    return new[] { V(10), V(-25), V(-90, 0, 10), V(-30), V(-90, 0, -10), V(-30), V(-50), V(90), V(-50), V(90), V(-70) };
                case Pose.HipEscape:
                    return new[] { new Vector3(0, 0, 25), V(-15), V(-80), V(-20), V(-80), V(-20), V(-30), V(40), V(-80), V(110), new Vector3(-90, 0, 30) };
                case Pose.StandUp:
                    return new[] { V(30), V(10), V(-40), V(-20), V(0, 0, 20), V(-10), V(-90), V(90), V(-10), V(100), V(-20) };
                default: // Idle
                    return new[] { V(3), z, V(0, 0, 8), V(-20), V(0, 0, -8), V(-20), V(0), V(3), V(0), V(3), z };
            }
        }

        static Vector3 V(float x, float y = 0f, float zz = 0f) => new Vector3(x, y, zz);

        public void SetPose(Pose pose, bool instant = false)
        {
            if (joints == null || joints[0] == null) CacheJoints();
            if (!instant && pose == currentPose && poseInitialized) return;
            poseInitialized = true;
            currentPose = pose;
            var a = Angles(pose);
            for (int i = 0; i < joints.Length; i++)
            {
                Vector3 e = i < a.Length ? a[i] : Vector3.zero;
                // Knöchel gleichen die Kniebeugung aus, damit die Füsse flach bleiben
                if (i == 14) e = new Vector3(-(a[6].x + a[7].x) * 0.5f, 0f, 0f);
                if (i == 15) e = new Vector3(-(a[8].x + a[9].x) * 0.5f, 0f, 0f);
                target[i] = Quaternion.Euler(e);
                if (instant) current[i] = target[i];
                if (instant && joints[i] != null) joints[i].localRotation = target[i];
            }
            if (instant) UpdatePelvisHeight(1f);
        }

        // ================================================================== Treffer-Physik (Feder-Dämpfer)
        [Header("Treffer-Physik")]
        public float springStiffness = 110f;
        public float springDamping = 8f;
        [Tooltip("Stärke aller Treffer-Reaktionen")]
        public float hitScale = 1.4f;

        Vector3[] reactAngle, reactVel;
        Vector3 pelvisShift, pelvisShiftVel;

        const int J_SPINE = 0, J_HEAD = 1, J_SHL = 2, J_ELL = 3, J_SHR = 4, J_ELR = 5, J_HIPL = 6, J_KNL = 7, J_HIPR = 8, J_KNR = 9,
                  J_PELVIS = 10, J_NECK = 11;

        /// <summary>Treffer anwenden. worldDir = Richtung der Kraft (vom Schlagenden zum Getroffenen).</summary>
        public void ApplyHit(BodyPart part, float strength, Vector3 worldDir)
        {
            if (joints == null) CacheJoints();
            strength = Mathf.Clamp(strength, 0.15f, 1.3f) * hitScale;
            Vector3 d = transform.InverseTransformDirection(worldDir.normalized);
            float back = -d.z;   // > 0: Kraft kommt von vorne
            float side = d.x;    // > 0: Kraft drückt nach rechts
            bool lying = !IsStandingPose;

            switch (part)
            {
                case BodyPart.Head:
                    // Kopf schnappt in Kraftrichtung, Nacken und Rumpf folgen, Schritt zurück
                    reactVel[J_HEAD] += new Vector3(-back * 1100f, side * 400f, -side * 900f) * strength;
                    reactVel[J_NECK] += new Vector3(-back * 600f, 0f, -side * 500f) * strength;
                    reactVel[J_SPINE] += new Vector3(-back * 350f, side * 150f, -side * 300f) * strength;
                    reactVel[J_SHL] += new Vector3(-150f, 0f, -200f) * strength;   // Arme fliegen leicht weg
                    reactVel[J_SHR] += new Vector3(-150f, 0f, 200f) * strength;
                    if (!lying) pelvisShiftVel += new Vector3(side, 0f, -back) * 1.6f * strength;
                    break;
                case BodyPart.Torso:
                    reactVel[J_SPINE] += new Vector3(450f + back * 150f, 0f, -side * 250f) * strength;
                    reactVel[J_HEAD] += new Vector3(300f, 0f, 0f) * strength;
                    reactVel[J_SHL] += new Vector3(300f, 0f, 0f) * strength;
                    reactVel[J_SHR] += new Vector3(300f, 0f, 0f) * strength;
                    if (!lying) pelvisShiftVel += new Vector3(side, -0.5f, -back) * 1.3f * strength;
                    break;
                case BodyPart.Groin:
                    // klappt komplett zusammen, Knie geben nach
                    reactVel[J_SPINE] += new Vector3(800f, 0f, 0f) * strength;
                    reactVel[J_HEAD] += new Vector3(400f, 0f, 0f) * strength;
                    reactVel[J_HIPL] += new Vector3(-300f, 0f, 150f) * strength;
                    reactVel[J_HIPR] += new Vector3(-300f, 0f, -150f) * strength;
                    reactVel[J_KNL] += new Vector3(500f, 0f, 0f) * strength;
                    reactVel[J_KNR] += new Vector3(500f, 0f, 0f) * strength;
                    reactVel[J_ELL] += new Vector3(-400f, 0f, 0f) * strength;
                    reactVel[J_ELR] += new Vector3(-400f, 0f, 0f) * strength;
                    if (!lying) pelvisShiftVel += new Vector3(0f, -2.2f, -back * 0.8f) * strength;
                    break;
                case BodyPart.ArmL:
                case BodyPart.ArmR:
                    int sh = part == BodyPart.ArmL ? J_SHL : J_SHR, el = part == BodyPart.ArmL ? J_ELL : J_ELR;
                    reactVel[sh] += new Vector3(back * 500f, 0f, (part == BodyPart.ArmL ? -1f : 1f) * 400f) * strength;
                    reactVel[el] += new Vector3(-300f, 0f, 0f) * strength;
                    reactVel[J_SPINE] += new Vector3(-back * 150f, (part == BodyPart.ArmL ? 1f : -1f) * 250f, 0f) * strength;
                    if (!lying) pelvisShiftVel += new Vector3(side, 0f, -back) * 0.7f * strength;
                    break;
                case BodyPart.LegL:
                case BodyPart.LegR:
                    int hip = part == BodyPart.LegL ? J_HIPL : J_HIPR, knee = part == BodyPart.LegL ? J_KNL : J_KNR;
                    reactVel[hip] += new Vector3(-300f, 0f, (part == BodyPart.LegL ? -1f : 1f) * 250f) * strength;
                    reactVel[knee] += new Vector3(800f, 0f, 0f) * strength;
                    reactVel[J_SPINE] += new Vector3(100f, 0f, (part == BodyPart.LegL ? 1f : -1f) * 350f) * strength;
                    if (!lying) pelvisShiftVel += new Vector3((part == BodyPart.LegL ? -0.8f : 0.8f), -1.6f, -back * 0.4f) * strength;
                    break;
            }
            flash[(int)part] = 1f;
        }

        void UpdateSprings(float dt)
        {
            for (int i = 0; i < reactAngle.Length; i++)
            {
                reactVel[i] += (-springStiffness * reactAngle[i] - springDamping * reactVel[i]) * dt;
                reactAngle[i] += reactVel[i] * dt;
                reactAngle[i] = Vector3.ClampMagnitude(reactAngle[i], 80f);
            }
            pelvisShiftVel += (-springStiffness * 0.5f * pelvisShift - springDamping * pelvisShiftVel) * dt;
            pelvisShift += pelvisShiftVel * dt;
            pelvisShift = Vector3.ClampMagnitude(pelvisShift, 0.45f);
        }

        // ================================================================== Beinarbeit
        float stepPhase, stepAmount;
        Vector3 lastRootPos;
        bool hasLastRoot;

        bool IsStandingPose => currentPose != Pose.LyingBack && currentPose != Pose.Bridge && currentPose != Pose.HipEscape
                               && currentPose != Pose.Kneel && currentPose != Pose.StandUp;

        void UpdateLocomotion(float dt)
        {
            Vector3 pos = transform.position;
            Vector3 vel = hasLastRoot && dt > 0f ? (pos - lastRootPos) / dt : Vector3.zero;
            lastRootPos = pos; hasLastRoot = true;
            Vector3 local = transform.InverseTransformDirection(vel);
            float speed = new Vector2(local.x, local.z).magnitude;
            float wanted = IsStandingPose ? Mathf.Clamp01(speed / 2.5f) : 0f;
            stepAmount = Mathf.Lerp(stepAmount, wanted, 1f - Mathf.Exp(-10f * dt));
            stepPhase += dt * (6f + 6f * stepAmount) * (local.z >= -0.2f ? 1f : -1f);
        }

        Vector3 StepOffset(int joint)
        {
            if (stepAmount < 0.01f) return Vector3.zero;
            float sL = Mathf.Sin(stepPhase), sR = Mathf.Sin(stepPhase + Mathf.PI);
            float a = 35f * stepAmount;
            switch (joint)
            {
                case J_HIPL: return new Vector3(-sL * a, 0f, 0f);
                case J_HIPR: return new Vector3(-sR * a, 0f, 0f);
                case J_KNL: return new Vector3(Mathf.Max(0f, sL) * a * 1.4f, 0f, 0f);
                case J_KNR: return new Vector3(Mathf.Max(0f, sR) * a * 1.4f, 0f, 0f);
                case J_SHL: return new Vector3(sR * a * 0.3f, 0f, 0f);
                case J_SHR: return new Vector3(sL * a * 0.3f, 0f, 0f);
                default: return Vector3.zero;
            }
        }

        // ================================================================== Verletzungsanzeige
        [Header("Verletzungsanzeige (grün → gelb → rot)")]
        public Color healthyColor = new Color(0.2f, 0.78f, 0.3f);
        public Color hurtColor = new Color(0.98f, 0.82f, 0.15f);
        public Color badlyHurtColor = new Color(0.9f, 0.08f, 0.08f);
        public Color flashColor = Color.white;

        readonly float[] partDamage = new float[CombatSimulation.PartCount];
        readonly float[] flash = new float[CombatSimulation.PartCount];
        Color teamColor = Color.blue;
        MaterialPropertyBlock mpb;

        public void SetTeamColor(Color c) => teamColor = c;

        /// <summary>Verletzung pro Körperteil (Index = BodyPart), 0 = unverletzt, 1 = schwer verletzt.</summary>
        public void SetDamage(float[] damage)
        {
            for (int i = 0; i < partDamage.Length && i < damage.Length; i++) partDamage[i] = damage[i];
        }

        public static Color HealthColor(float damage, Color ok, Color mid, Color bad)
        {
            damage = Mathf.Clamp01(damage);
            return damage < 0.5f ? Color.Lerp(ok, mid, damage * 2f) : Color.Lerp(mid, bad, (damage - 0.5f) * 2f);
        }

        void UpdateColors(float dt)
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            int p = 0;
            foreach (var list in AllPartLists())
            {
                flash[p] = Mathf.Max(0f, flash[p] - dt * 7f);
                Color c = Color.Lerp(HealthColor(partDamage[p], healthyColor, hurtColor, badlyHurtColor), flashColor, flash[p]);
                foreach (var r in list) Paint(r, c);
                p++;
            }
            foreach (var r in teamParts) Paint(r, teamColor);
        }

        void Paint(Renderer r, Color c)
        {
            if (r == null) return;
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c); // URP
            mpb.SetColor("_Color", c);     // Built-in
            r.SetPropertyBlock(mpb);
        }

        // ================================================================== Frame-Update
        float basePelvisY = PelvisHeight;

        void Update()
        {
            if (joints == null || target == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            float k = 1f - Mathf.Exp(-poseSpeed * dt);

            UpdateSprings(dt);
            UpdateLocomotion(dt);

            for (int i = 0; i < joints.Length; i++)
            {
                if (joints[i] == null) continue;
                current[i] = Quaternion.Slerp(current[i], target[i], k);
                joints[i].localRotation = current[i] * Quaternion.Euler(reactAngle[i] + StepOffset(i));
            }
            UpdatePelvisHeight(k);
            UpdateColors(dt);
        }

        void UpdatePelvisHeight(float k)
        {
            if (pelvis == null) return;
            float h = PelvisHeight;
            switch (currentPose)
            {
                case Pose.LyingBack:
                case Pose.HipEscape: h = 0.15f; break;
                case Pose.Bridge: h = 0.35f; break;
                case Pose.Kneel: h = 0.55f; break;
                case Pose.StandUp: h = 0.55f; break;
                case Pose.DropWeight: h = 0.7f; break;
                case Pose.Stunned: h = 0.88f; break;
            }
            basePelvisY = Mathf.Lerp(basePelvisY, h, k);
            float bob = IsStandingPose ? -Mathf.Abs(Mathf.Sin(stepPhase)) * 0.04f * stepAmount : 0f;
            pelvis.localPosition = new Vector3(pelvisShift.x, basePelvisY + Mathf.Min(0f, pelvisShift.y) + bob, pelvisShift.z);
        }

        void Awake()
        {
            // Szene wurde mit einer älteren Version gebaut → Körper neu aufbauen (mehr Gelenke, Körperteile)
            if (Application.isPlaying && pelvis != null && neck == null && bodyMaterial != null)
            {
                var joint = new Material(bodyMaterial); joint.color = new Color(0.15f, 0.15f, 0.18f);
                Build(bodyMaterial, new Material(bodyMaterial), joint);
            }
            if (pelvis != null) CacheJoints();
            if (joints != null) SetPose(Pose.Idle, true);
        }
    }
}
