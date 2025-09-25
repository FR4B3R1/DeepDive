using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class Minimap : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;
    public Rigidbody2D playerRb;     // opzionale
    public RectTransform arrow;
    public Image glow;
    public Image pulseRing;

    [Header("Config")]
    public bool minimapKeepsNorthUp = true; // deve combaciare con la camera
    public float arrowHeadingOffsetDeg = 0f;

    [Header("Colors")]
    public Color markerColor = new Color(1f, 0.95f, 0.2f, 1f);
    public Color glowColor = new Color(1f, 1f, 1f, 0.55f);
    public Color pulseColor = new Color(1f, 0.95f, 0.2f, 0.8f);

    [Header("Breathing")]
    public bool breathingPulse = true;
    public float pulseMinScale = 0.85f;
    public float pulseMaxScale = 1.25f;
    public float pulseSpeed = 1.6f;
    public bool pulseScalesWithSpeed = true;
    public float refSpeed = 8f;
    public float maxPulseSpeedMultiplier = 2.0f;

    [Header("Ping")]
    public bool periodicPing = true;
    public float pingInterval = 1.8f;
    public float pingDuration = 0.9f;
    public float pingStartScale = 0.6f;
    public float pingEndScale = 2.0f;
    public float pingStartAlpha = 0.65f;

    private Coroutine pingRoutine;

    void Awake()
    {
        if (arrow) arrow.GetComponent<Image>().color = markerColor;
        if (glow) glow.color = glowColor;
        if (pulseRing) pulseRing.color = pulseColor;
    }

    void OnEnable()
    {
        if (periodicPing && pulseRing && pingRoutine == null)
            pingRoutine = StartCoroutine(PingLoop());
    }

    void OnDisable()
    {
        if (pingRoutine != null) { StopCoroutine(pingRoutine); pingRoutine = null; }
    }

    void Update()
    {
        if (!player) return;

        // Rotazione freccia: se la mappa � Nord-su, la freccia indica l�heading del player.
        if (arrow)
        {
            if (minimapKeepsNorthUp)
                arrow.localRotation = Quaternion.Euler(0, 0, player.eulerAngles.z + arrowHeadingOffsetDeg);
            else
                arrow.localRotation = Quaternion.identity;
        }

        // Respiro sul ring
        if (breathingPulse && pulseRing)
        {
            float speedMul = 1f;
            if (pulseScalesWithSpeed && playerRb)
            {
                float v = playerRb.linearVelocity.magnitude;
                speedMul = Mathf.Lerp(1f, maxPulseSpeedMultiplier, Mathf.Clamp01(v / refSpeed));
            }

            float t = (Mathf.Sin(Time.time * Mathf.PI * 2f * pulseSpeed * speedMul) + 1f) * 0.5f;
            float s = Mathf.Lerp(pulseMinScale, pulseMaxScale, t);
            pulseRing.rectTransform.localScale = new Vector3(s, s, 1f);

            var c = pulseRing.color; c.a = Mathf.Lerp(0.35f, pulseColor.a, t); pulseRing.color = c;
        }

        // Glow lieve
        if (glow)
        {
            float t = (Mathf.Sin(Time.time * 1.2f) + 1f) * 0.5f;
            var c = glow.color; c.a = Mathf.Lerp(0.35f, glowColor.a, t * 0.3f + 0.7f); glow.color = c;
        }
    }

    private IEnumerator PingLoop()
    {
        var ring = pulseRing;
        while (true)
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, pingInterval));
            if (!ring) continue;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, pingDuration);
                float s = Mathf.Lerp(pingStartScale, pingEndScale, 1f - Mathf.Pow(1f - t, 3f)); // easeOutCubic
                float a = Mathf.Lerp(pingStartAlpha, 0f, t);

                ring.rectTransform.localScale = new Vector3(s, s, 1f);
                var c = ring.color; c.a = a; ring.color = c;
                yield return null;
            }

            // ripristino
            var c2 = ring.color; c2.a = pulseColor.a; ring.color = c2;
            ring.rectTransform.localScale = Vector3.one;
        }
    }
}