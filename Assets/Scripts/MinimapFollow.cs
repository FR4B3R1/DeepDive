using UnityEngine;

public class MinimapFollow : MonoBehaviour
{
    public Transform target;          // il player
    public float orthoSize = 20f;     // zoom minimappa
    public bool keepNorthUp = true;   // true = mappa fissa Nord-su; false = ruota con il player
    public Vector3 offset = new Vector3(0, 0, -10f);
    public float followLerp = 15f;    // smoothing follow
    public float rotateLerp = 12f;    // smoothing rotazione

    private Camera _cam;

    void Awake() => _cam = GetComponent<Camera>();

    void LateUpdate()
    {
        if (!target) return;

        // Segui posizione (smussato)
        Vector3 desired = new Vector3(target.position.x, target.position.y, 0f) + offset;
        transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followLerp * Time.deltaTime));

        // Rotazione
        Quaternion desiredRot = keepNorthUp ? Quaternion.identity : Quaternion.Euler(0, 0, target.eulerAngles.z);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, 1f - Mathf.Exp(-rotateLerp * Time.deltaTime));

        // Zoom
        if (_cam) _cam.orthographicSize = orthoSize;
    }
}