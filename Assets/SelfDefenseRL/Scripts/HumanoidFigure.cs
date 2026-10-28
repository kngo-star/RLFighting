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
                if (instant && joints[i] != null) joints[i].localRotation = target[i];
            }
            if (instant) UpdatePelvisHeight(1f);
        }

        void Update()
        {
            if (joints == null || target == null) return;
            float k = 1f - Mathf.Exp(-poseSpeed * Time.deltaTime);
            for (int i = 0; i < joints.Length; i++)
                if (joints[i] != null) joints[i].localRotation = Quaternion.Slerp(joints[i].localRotation, target[i], k);
            UpdatePelvisHeight(k);
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
            var p = pelvis.localPosition;
            p.y = Mathf.Lerp(p.y, h, k);
            pelvis.localPosition = p;
        }

        void Awake()
        {
            if (pelvis != null) CacheJoints();
            if (joints != null) SetPose(Pose.Idle, true);
        }
    }
}
