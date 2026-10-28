using UnityEngine;

namespace SelfDefenseRL
{
    /// <summary>
    /// Kamera für die Demo-Szene: folgt den beiden Figuren aus der Nähe (seitlich, damit man Schläge,
    /// Tritte und Kopfbewegungen gut sieht) und wackelt kurz bei harten Treffern.
    /// Im StatsHUD lässt sie sich ein- und ausschalten.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        public ArenaView arena;
        public float distance = 4.2f;
        public float height = 2.0f;
        public float smoothing = 4f;
        public bool follow = true;
        [Tooltip("Stärke des Kamera-Wackelns bei harten Treffern")]
        public float shakeAmount = 0.12f;

        float shake;
        Vector3 smoothedMid, smoothedSide;
        bool init;

        void OnEnable() { ArenaView.StrongHit += OnHit; }
        void OnDisable() { ArenaView.StrongHit -= OnHit; }

        void OnHit(ArenaView view, float strength)
        {
            if (view == arena) shake = Mathf.Max(shake, Mathf.Clamp01(strength));
        }

        void LateUpdate()
        {
            if (!follow || arena == null || arena.defender == null || arena.attacker == null) return;
            Vector3 a = arena.defender.transform.position, b = arena.attacker.transform.position;
            Vector3 mid = (a + b) * 0.5f + Vector3.up * 1.0f;
            Vector3 along = b - a; along.y = 0f;
            if (along.sqrMagnitude < 0.01f) along = arena.defender.transform.forward;
            // seitlich auf die Kampflinie schauen, Seite nur langsam wechseln
            Vector3 side = Vector3.Cross(Vector3.up, along.normalized);
            if (init && Vector3.Dot(side, smoothedSide) < 0f) side = -side;

            float k = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            if (!init) { smoothedMid = mid; smoothedSide = side; init = true; }
            smoothedMid = Vector3.Lerp(smoothedMid, mid, k);
            smoothedSide = Vector3.Slerp(smoothedSide, side, k * 0.5f);

            Vector3 pos = smoothedMid + smoothedSide * distance + Vector3.up * (height - 1.0f);
            if (shake > 0f)
            {
                pos += Random.insideUnitSphere * shake * shakeAmount;
                shake = Mathf.Max(0f, shake - Time.unscaledDeltaTime * 4f);
            }
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(smoothedMid - pos);
        }
    }
}
