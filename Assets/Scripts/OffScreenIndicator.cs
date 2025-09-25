using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mostra una freccia ai bordi dello schermo quando un target (es. nave) esce dalla vista della main camera.
/// Funziona con Canvas in Screen Space - Overlay (più semplice). Vedi note per Screen Space - Camera/World.
/// </summary>
public class OffscreenIndicator : MonoBehaviour
{
    [Header("Riferimenti")]
    public Camera mainCamera;
    public Transform target;
    public RectTransform indicatorUI; // istanza del prefab (freccia) già sotto il Canvas

    [Header("Parametri")]
    [Tooltip("Padding dai bordi dello schermo in pixel.")]
    public float edgePadding = 40f;
    [Tooltip("Nascondi l'indicatore quando il target è dentro il viewport.")]
    public bool hideWhenOnScreen = true;
    [Tooltip("Ruota la freccia per puntare verso il target.")]
    public bool rotateArrow = true;
    [Tooltip("Se true, la freccia si avvicina al bordo quando il target è lontano dal centro.")]
    public bool clampToBorder = true;

    [Header("Look & Feel")]
    public Color arrowColor = Color.white;
    [Tooltip("Riduci l'alpha quando il target è vicino al bordo/centro?")]
    public bool fadeWithDistance = false;
    [Range(0.1f, 1f)] public float minAlpha = 0.35f;

    private Image arrowImage;
    private Vector2 screenCenter;

    void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (indicatorUI != null) arrowImage = indicatorUI.GetComponent<Image>();
    }

    void Update()
    {
        if (mainCamera == null || target == null || indicatorUI == null) return;

        screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        // 1) Target in coordinate viewport
        Vector3 vp = mainCamera.WorldToViewportPoint(target.position);
        bool behind = vp.z < 0f;

        // Se dietro la camera, riflettiamo le coordinate (trucchetto per gestire la direzione)
        if (behind)
        {
            vp.x = 1f - vp.x;
            vp.y = 1f - vp.y;
            vp.z = 0f; // trattiamolo come davanti dopo il flip
        }

        bool onScreen = (vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f);

        // 2) Mostra/nascondi
        if (hideWhenOnScreen && onScreen)
        {
            if (indicatorUI.gameObject.activeSelf)
                indicatorUI.gameObject.SetActive(false);
            return;
        }
        else
        {
            if (!indicatorUI.gameObject.activeSelf)
                indicatorUI.gameObject.SetActive(true);
        }

        // 3) Clamping ai bordi con padding
        Vector2 vpClamped = new Vector2(
            Mathf.Clamp(vp.x, 0f + 0.0001f, 1f - 0.0001f),
            Mathf.Clamp(vp.y, 0f + 0.0001f, 1f - 0.0001f)
        );

        Vector2 screenPos = mainCamera.ViewportToScreenPoint(vpClamped);

        if (clampToBorder)
        {
            float minX = edgePadding;
            float maxX = Screen.width - edgePadding;
            float minY = edgePadding;
            float maxY = Screen.height - edgePadding;

            screenPos.x = Mathf.Clamp(screenPos.x, minX, maxX);
            screenPos.y = Mathf.Clamp(screenPos.y, minY, maxY);
        }

        // 4) Posizionamento UI (Canvas Overlay: .position accetta coord. schermo)
        indicatorUI.position = screenPos;

        // 5) Rotazione freccia verso il target
        if (rotateArrow)
        {
            // Direzione dallo schermo verso il target (in spazio schermo)
            Vector2 targetScreen = mainCamera.WorldToScreenPoint(target.position);
            if (behind)
            {
                // Se dietro, invertiamo la direzione: punta comunque verso i bordi "giusti"
                targetScreen = screenCenter - (targetScreen - screenCenter);
            }

            Vector2 dir = (targetScreen - screenCenter).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f; // freccia punta su = -90
            indicatorUI.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        // 6) Colore/alpha
        if (arrowImage != null)
        {
            var c = arrowColor;

            if (fadeWithDistance)
            {
                // più lontano dal centro, più opaco (puoi adattare la curva)
                float maxDist = screenCenter.magnitude;
                float d = Vector2.Distance(screenPos, screenCenter);
                float t = Mathf.Clamp01(d / maxDist);
                c.a = Mathf.Lerp(minAlpha, 1f, t);
            }

            arrowImage.color = c;
        }
    }
}
