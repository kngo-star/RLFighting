using UnityEngine;

namespace SelfDefenseRL
{
    /// <summary>Körperhaltungen, die eine Figur einnehmen kann.</summary>
    public enum Pose
    {
        Idle, Guard, Punch, Elbow, Knee, Kick, Push, Release, DropWeight, Throw,
        Grab, Choke, Kneel, Stunned, LyingBack, Bridge, HipEscape, StandUp
    }

    /// <summary>
    /// Einfache menschliche Figur aus Grundkörpern (Kopf, Rumpf, Arme, Beine mit Gelenken).
    /// Wird vom SceneBuilder im Editor erzeugt und zur Laufzeit von <see cref="ArenaView"/> posiert.
    /// Der Ursprung (Pivot) liegt zwischen den Füssen auf dem Boden, die Figur schaut entlang +Z.
    /// </summary>
    public class HumanoidFigure : MonoBehaviour
    {
        // Gelenke
        public Transform pelvis, spine, head, shoulderL, elbowL, shoulderR, elbowR, hipL, kneeL, hipR, kneeR;
        public float poseSpeed = 14f;
        [Tooltip("Material des Körpers (wird zur Laufzeit eingefärbt, Kopf/Hände bleiben hautfarben)")]
        public Material bodyMaterial;

        const float PelvisHeight = 0.95f;

        Pose currentPose = Pose.Idle;
        bool poseInitialized;
        Transform[] joints;
        Quaternion[] target;
        Quaternion[] current;

        public Pose CurrentPose => currentPose;
        public Vector3 ChestPosition => spine != null ? spine.position + spine.up * 0.35f : transform.position + Vector3.up * 1.3f;

        // ------------------------------------------------------------------ Aufbau
        /// <summary>Erzeugt den Körper als Kinder dieses Objekts (im Editor oder zur Laufzeit).</summary>
        public void Build(Material bodyMaterial, Material headMaterial)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
            this.bodyMaterial = bodyMaterial;

            pelvis = Joint("Pelvis", transform, new Vector3(0f, PelvisHeight, 0f));
            Part(PrimitiveType.Cube, pelvis, new Vector3(0f, 0f, 0f), new Vector3(0.32f, 0.16f, 0.2f), bodyMaterial);

            spine = Joint("Spine", pelvis, new Vector3(0f, 0.08f, 0f));
            Part(PrimitiveType.Capsule, spine, new Vector3(0f, 0.27f, 0f), new Vector3(0.36f, 0.27f, 0.22f), bodyMaterial);

            head = Joint("Head", spine, new Vector3(0f, 0.55f, 0f));
            Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.12f, 0f), new Vector3(0.22f, 0.25f, 0.23f), headMaterial);
            // "Nase" zeigt die Blickrichtung an
            Part(PrimitiveType.Cube, head, new Vector3(0f, 0.12f, 0.12f), new Vector3(0.05f, 0.05f, 0.05f), headMaterial);

            shoulderL = Joint("ShoulderL", spine, new Vector3(-0.22f, 0.47f, 0f));
            Part(PrimitiveType.Capsule, shoulderL, new Vector3(0f, -0.15f, 0f), new Vector3(0.1f, 0.15f, 0.1f), bodyMaterial);
            elbowL = Joint("ElbowL", shoulderL, new Vector3(0f, -0.3f, 0f));
            Part(PrimitiveType.Capsule, elbowL, new Vector3(0f, -0.14f, 0f), new Vector3(0.09f, 0.14f, 0.09f), headMaterial);

            shoulderR = Joint("ShoulderR", spine, new Vector3(0.22f, 0.47f, 0f));
            Part(PrimitiveType.Capsule, shoulderR, new Vector3(0f, -0.15f, 0f), new Vector3(0.1f, 0.15f, 0.1f), bodyMaterial);
            elbowR = Joint("ElbowR", shoulderR, new Vector3(0f, -0.3f, 0f));
            Part(PrimitiveType.Capsule, elbowR, new Vector3(0f, -0.14f, 0f), new Vector3(0.09f, 0.14f, 0.09f), headMaterial);

            hipL = Joint("HipL", pelvis, new Vector3(-0.1f, -0.06f, 0f));
            Part(PrimitiveType.Capsule, hipL, new Vector3(0f, -0.21f, 0f), new Vector3(0.14f, 0.21f, 0.14f), bodyMaterial);
            kneeL = Joint("KneeL", hipL, new Vector3(0f, -0.43f, 0f));
            Part(PrimitiveType.Capsule, kneeL, new Vector3(0f, -0.21f, 0f), new Vector3(0.11f, 0.21f, 0.11f), bodyMaterial);
            Part(PrimitiveType.Cube, kneeL, new Vector3(0f, -0.43f, 0.05f), new Vector3(0.1f, 0.06f, 0.22f), headMaterial);

            hipR = Joint("HipR", pelvis, new Vector3(0.1f, -0.06f, 0f));
            Part(PrimitiveType.Capsule, hipR, new Vector3(0f, -0.21f, 0f), new Vector3(0.14f, 0.21f, 0.14f), bodyMaterial);
            kneeR = Joint("KneeR", hipR, new Vector3(0f, -0.43f, 0f));
            Part(PrimitiveType.Capsule, kneeR, new Vector3(0f, -0.21f, 0f), new Vector3(0.11f, 0.21f, 0.11f), bodyMaterial);
            Part(PrimitiveType.Cube, kneeR, new Vector3(0f, -0.43f, 0.05f), new Vector3(0.1f, 0.06f, 0.22f), headMaterial);

            CacheJoints();
            SetPose(Pose.Idle, true);
        }

        static Transform Joint(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        static void Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = type.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        void CacheJoints()
        {
            joints = new[] { spine, head, shoulderL, elbowL, shoulderR, elbowR, hipL, kneeL, hipR, kneeR, pelvis };
            target = new Quaternion[joints.Length];
            current = new Quaternion[joints.Length];
            for (int i = 0; i < joints.Length; i++) current[i] = joints[i] != null ? joints[i].localRotation : Quaternion.identity;
            reactAngle = new Vector3[joints.Length];
            reactVel = new Vector3[joints.Length];
        }

        // ------------------------------------------------------------------ Posen
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
                    return new[] { new Vector3(5, -20, 0), z, V(-115, 0, 10), V(-110), V(-90), V(-5), V(-20), V(20), V(15), V(5), z };
                case Pose.Elbow:
                    return new[] { new Vector3(5, -30, 0), z, V(-115, 0, 10), V(-110), V(-85, 0, 30), V(-140), V(-10), V(10), V(10), V(5), z };
                case Pose.Knee:
                    return new[] { V(15), z, V(-70), V(-30), V(-70), V(-30), V(5), V(5), V(-100), V(100), z };
                case Pose.Kick:
                    return new[] { V(-10), z, V(-60, 0, 20), V(-90), V(-40), V(-60), V(5), V(10), V(-75), V(10), z };
                case Pose.Push:
                    return new[] { V(15), z, V(-85), V(-10), V(-85), V(-10), V(-25), V(20), V(20), V(5), z };
                case Pose.Release:
                    return new[] { V(25), V(10), V(-45), V(-70), V(-45, 0, -15), V(-80), V(-20), V(30), V(-10), V(25), z };
                case Pose.DropWeight:
                    return new[] { V(30), V(10), V(-30, 0, 25), V(-60), V(-30, 0, -25), V(-60), V(-60), V(85), V(-60), V(85), z };
                case Pose.Throw:
                    return new[] { new Vector3(40, 60, 0), z, V(-80), V(-40), V(-90), V(-20), V(-30), V(40), V(-40), V(30), z };
                case Pose.Grab:
                    return new[] { V(10), z, V(-85, 0, 10), V(-20), V(-85, 0, -10), V(-20), V(-10), V(10), V(15), V(5), z };
                case Pose.Choke:
                    return new[] { V(10), z, V(-100, 0, 15), V(-25), V(-100, 0, -15), V(-25), V(-10), V(10), V(15), V(5), z };
                case Pose.Kneel:
                    return new[] { V(25), V(20), V(-70), V(-40), V(-70), V(-40), V(-20), V(110), V(-20), V(110), z };
                case Pose.Stunned:
                    return new[] { V(20), V(30), V(-10, 0, 10), V(-30), V(-10, 0, -10), V(-30), V(-10), V(20), V(5), V(15), z };
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
                target[i] = Quaternion.Euler(a[i]);
                if (instant) current[i] = target[i];
            }
            if (instant) UpdatePelvisHeight(1f);
        }

        // ================================================================== Treffer-Reaktion (Feder-Physik)
        // Jeder Treffer gibt den Gelenken einen Drehimpuls. Eine gedämpfte Feder zieht sie zurück:
        // der Kopf schnappt nach hinten und federt zurück, der Rumpf knickt ein, die Knie geben nach.
        [Header("Treffer-Physik")]
        public float springStiffness = 140f;
        public float springDamping = 11f;
        public float hitScale = 1f;

        Vector3[] reactAngle, reactVel;       // Grad, Grad/s pro Gelenk
        Vector3 pelvisShift, pelvisShiftVel;  // Meter, im Figur-Raum

        const int J_SPINE = 0, J_HEAD = 1, J_SHL = 2, J_ELL = 3, J_SHR = 4, J_ELR = 5, J_HIPL = 6, J_KNL = 7, J_HIPR = 8, J_KNR = 9, J_PELVIS = 10;

        /// <summary>Treffer anwenden. worldDir = Richtung der Kraft (vom Schlagenden zum Getroffenen).</summary>
        public void ApplyHit(HitZone zone, float strength, Vector3 worldDir)
        {
            if (joints == null) CacheJoints();
            strength *= hitScale;
            Vector3 d = transform.InverseTransformDirection(worldDir.normalized);
            d.y = 0f;
            float back = -d.z;     // > 0: Kraft kommt von vorne und drückt nach hinten
            float side = d.x;      // > 0: Kraft drückt nach rechts

            switch (zone)
            {
                case HitZone.Head:
                    // Kopf schnappt in Kraftrichtung, Rumpf folgt etwas, Schritt nach hinten
                    reactVel[J_HEAD] += new Vector3(-back * 900f, side * 300f, -side * 700f) * strength;
                    reactVel[J_SPINE] += new Vector3(-back * 300f, 0f, -side * 250f) * strength;
                    pelvisShiftVel += new Vector3(side, 0f, -back) * 1.2f * strength;
                    break;
                case HitZone.Body:
                    // Rumpf klappt zusammen (Kniestoss, Fauststoss in den Bauch)
                    reactVel[J_SPINE] += new Vector3(350f + back * 150f, 0f, -side * 200f) * strength;
                    reactVel[J_HEAD] += new Vector3(250f, 0f, 0f) * strength;
                    reactVel[J_SHL] += new Vector3(250f, 0f, 0f) * strength;
                    reactVel[J_SHR] += new Vector3(250f, 0f, 0f) * strength;
                    pelvisShiftVel += new Vector3(side, -0.5f, -back) * 1.0f * strength;
                    break;
                case HitZone.Legs:
                    // getroffenes Bein knickt ein, Becken sackt ab
                    int hip = side >= 0f ? J_HIPL : J_HIPR, knee = side >= 0f ? J_KNL : J_KNR;
                    reactVel[hip] += new Vector3(-250f, 0f, 0f) * strength;
                    reactVel[knee] += new Vector3(600f, 0f, 0f) * strength;
                    reactVel[J_SPINE] += new Vector3(0f, 0f, -side * 300f) * strength;
                    pelvisShiftVel += new Vector3(side * 0.6f, -1.2f, -back * 0.4f) * strength;
                    break;
            }
            flash[(int)zone] = 1f;
        }

        void UpdateSprings(float dt)
        {
            for (int i = 0; i < reactAngle.Length; i++)
            {
                reactVel[i] += (-springStiffness * reactAngle[i] - springDamping * reactVel[i]) * dt;
                reactAngle[i] += reactVel[i] * dt;
                reactAngle[i] = Vector3.ClampMagnitude(reactAngle[i], 70f);
            }
            pelvisShiftVel += (-springStiffness * 0.6f * pelvisShift - springDamping * pelvisShiftVel) * dt;
            pelvisShift += pelvisShiftVel * dt;
            pelvisShift = Vector3.ClampMagnitude(pelvisShift, 0.35f);
        }

        // ================================================================== Schritte (Beinarbeit)
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

        // ================================================================== Schadensanzeige
        [Header("Schadensanzeige")]
        public Color bruiseColor = new Color(0.45f, 0.02f, 0.08f);
        public Color flashColor = new Color(1f, 0.9f, 0.3f);
        Color baseColor = Color.white, skinColor = new Color(0.93f, 0.76f, 0.62f);
        readonly float[] zoneDamage = new float[3];
        readonly float[] flash = new float[3];
        Renderer[][] zoneRenderers;
        bool[][] isSkin;
        MaterialPropertyBlock mpb;

        /// <summary>Grundfarbe des Körpers (z. B. blau/rot).</summary>
        public void SetBaseColor(Color c) => baseColor = c;

        /// <summary>Schaden pro Zone (0..1): Kopf, Rumpf, Beine. Verfärbt die Körperteile.</summary>
        public void SetDamage(float head, float body, float legs)
        {
            zoneDamage[0] = head; zoneDamage[1] = body; zoneDamage[2] = legs;
        }

        void CollectZones()
        {
            var all = GetComponentsInChildren<Renderer>();
            var headSet = head != null ? head.GetComponentsInChildren<Renderer>() : new Renderer[0];
            var legList = new System.Collections.Generic.List<Renderer>();
            if (hipL != null) legList.AddRange(hipL.GetComponentsInChildren<Renderer>());
            if (hipR != null) legList.AddRange(hipR.GetComponentsInChildren<Renderer>());
            var bodyList = new System.Collections.Generic.List<Renderer>();
            foreach (var r in all)
                if (System.Array.IndexOf(headSet, r) < 0 && !legList.Contains(r)) bodyList.Add(r);
            zoneRenderers = new[] { headSet, bodyList.ToArray(), legList.ToArray() };
            isSkin = new bool[3][];
            for (int z = 0; z < 3; z++)
            {
                isSkin[z] = new bool[zoneRenderers[z].Length];
                for (int i = 0; i < zoneRenderers[z].Length; i++)
                {
                    var m = zoneRenderers[z][i].sharedMaterial;
                    isSkin[z][i] = bodyMaterial != null && m != bodyMaterial;
                    if (isSkin[z][i] && m != null && m.HasProperty("_BaseColor")) skinColor = m.GetColor("_BaseColor");
                    else if (isSkin[z][i] && m != null && m.HasProperty("_Color")) skinColor = m.GetColor("_Color");
                }
            }
            mpb = new MaterialPropertyBlock();
        }

        void UpdateColors(float dt)
        {
            if (zoneRenderers == null) CollectZones();
            for (int z = 0; z < 3; z++)
            {
                flash[z] = Mathf.Max(0f, flash[z] - dt * 8f);
                float dmg = Mathf.Clamp01(zoneDamage[z]) * 0.85f;
                for (int i = 0; i < zoneRenderers[z].Length; i++)
                {
                    var r = zoneRenderers[z][i];
                    if (r == null) continue;
                    Color c = Color.Lerp(isSkin[z][i] ? skinColor : baseColor, bruiseColor, dmg);
                    c = Color.Lerp(c, flashColor, flash[z]);
                    r.GetPropertyBlock(mpb);
                    mpb.SetColor("_BaseColor", c);
                    mpb.SetColor("_Color", c);
                    r.SetPropertyBlock(mpb);
                }
            }
        }

        // ================================================================== Frame-Update
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
            }
            basePelvisY = Mathf.Lerp(basePelvisY, h, k);
            float bob = IsStandingPose ? -Mathf.Abs(Mathf.Sin(stepPhase)) * 0.04f * stepAmount : 0f;
            pelvis.localPosition = new Vector3(pelvisShift.x, basePelvisY + Mathf.Min(0f, pelvisShift.y) + bob, pelvisShift.z);
        }

        float basePelvisY = PelvisHeight;

        void Awake()
        {
            if (pelvis != null) CacheJoints();
            if (joints != null) SetPose(Pose.Idle, true);
        }
    }
}
