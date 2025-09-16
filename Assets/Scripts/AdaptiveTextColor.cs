using UnityEngine;
using TMPro;

[ExecuteAlways]
public class AdaptiveTextColorTwoLayers : MonoBehaviour
{
    [Header("Riferimenti")]
    public TMP_Text targetText;
    [Tooltip("Se lasci vuoto, lo script userà la Canvas del TMP_Text.")]
    public RectTransform labelRect;
    [Tooltip("Camera sorgente da cui copiare la vista (se vuota, usa Camera.main).")]
    public Camera sourceCamera;

    [Header("Layer di interesse")]
    [Tooltip("Layer del CIELO (sfondo chiaro).")]
    public LayerMask skyMask;
    [Tooltip("Layer dell'ACQUA (sfondo scuro).")]
    public LayerMask waterMask;

    [Header("Colori testo")]
    [Tooltip("Per sfondo scuro (acqua).")]
    public Color lightTextOnDark = new Color(0.43f, 0.78f, 1f, 1f); // azzurro chiaro
    [Tooltip("Per sfondo chiaro (cielo).")]
    public Color darkTextOnLight = new Color(0.04f, 0.10f, 0.27f, 1f); // blu scuro

    [Header("Campionamento")]
    [Range(8, 128)] public int sampleRTWidth = 32;
    [Range(8, 128)] public int sampleRTHeight = 18;
    [Range(0.05f, 1f)] public float sampleInterval = 0.2f;
    [Tooltip("Soglia di alpha per considerare 'visibile' il layer Water.")]
    [Range(0.01f, 0.5f)] public float waterAlphaThreshold = 0.05f;

    [Tooltip("Offset in pixel schermo per dove campionare rispetto al pivot della label.")]
    public Vector2 screenOffsetPixels = Vector2.zero;

    private Camera _sampleCam;          // camera dedicata al campionamento
    private RenderTexture _rt;
    private Texture2D _pixel;
    private float _timer;

    private Canvas _rootCanvas;

    void Reset()
    {
        targetText = GetComponent<TMP_Text>();
        if (targetText) labelRect = targetText.rectTransform;
    }

    void Awake()
    {
        if (!targetText) targetText = GetComponent<TMP_Text>();
        if (!labelRect && targetText) labelRect = targetText.rectTransform;
        _rootCanvas = targetText ? targetText.canvas : GetComponentInParent<Canvas>();
    }

    void OnEnable()
    {
        EnsureResources();
    }

    void OnDisable()
    {
        CleanupResources();
    }

    void EnsureResources()
    {
        if (_rt == null)
        {
            _rt = new RenderTexture(sampleRTWidth, sampleRTHeight, 0, RenderTextureFormat.ARGB32);
            _rt.filterMode = FilterMode.Point;
        }
        if (_pixel == null)
        {
            _pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false, false);
        }
        if (_sampleCam == null)
        {
            var go = new GameObject("[AdaptiveTextColor SampleCam]");
            go.hideFlags = Application.isPlaying ? HideFlags.HideAndDontSave : HideFlags.DontSave;
            _sampleCam = go.AddComponent<Camera>();
            _sampleCam.enabled = false;
            _sampleCam.clearFlags = CameraClearFlags.SolidColor;
            _sampleCam.backgroundColor = new Color(0, 0, 0, 0); // trasparente, così l'alpha dice se Water è presente
        }
    }

    void CleanupResources()
    {
        if (_rt) { _rt.Release(); DestroyImmediate(_rt); _rt = null; }
        if (_pixel) { DestroyImmediate(_pixel); _pixel = null; }
        if (_sampleCam) { DestroyImmediate(_sampleCam.gameObject); _sampleCam = null; }
    }

    void LateUpdate()
    {
        _timer += Application.isPlaying ? Time.unscaledDeltaTime : 0.1f;
        if (_timer < sampleInterval) return;
        _timer = 0f;

        if (!targetText || !labelRect) return;
        EnsureResources();

        // Scegli la camera sorgente da imitare (vista)
        Camera src = sourceCamera ? sourceCamera : Camera.main;
        if (!src) return;

        // Copia i parametri di vista (posizione, size, proiezione)
        _sampleCam.CopyFrom(src);
        _sampleCam.enabled = false; // assicurati che non renderizzi sullo schermo
        _sampleCam.clearFlags = CameraClearFlags.SolidColor;
        _sampleCam.backgroundColor = new Color(0, 0, 0, 0);
        _sampleCam.targetTexture = _rt;

        // 1) Renderizza SOLO il layer Water su RT trasparente
        _sampleCam.cullingMask = waterMask;
        _sampleCam.Render();

        // Calcola la posizione schermo della label
        Vector2 screenPos = GetLabelScreenPosition(src) + screenOffsetPixels;

        // Converte in coordinate RT (u,v -> px,py)
        int px = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(screenPos.x / Screen.width) * (_rt.width - 1)), 0, _rt.width - 1);
        int py = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(screenPos.y / Screen.height) * (_rt.height - 1)), 0, _rt.height - 1);

        // Leggi il pixel
        var prevActive = RenderTexture.active;
        RenderTexture.active = _rt;
        _pixel.ReadPixels(new Rect(px, py, 1, 1), 0, 0, false);
        _pixel.Apply(false, false);
        Color waterPixel = _pixel.GetPixel(0, 0);
        RenderTexture.active = prevActive;

        // Se il layer Water è visibile (alpha sopra soglia) -> usa testo chiaro, altrimenti scuro
        bool waterVisible = waterPixel.a >= waterAlphaThreshold;
        targetText.color = waterVisible ? lightTextOnDark : darkTextOnLight;
    }

    Vector2 GetLabelScreenPosition(Camera srcCam)
    {
        // Determina la camera da usare per la conversione world->screen della UI
        Camera uiCam = null;
        if (_rootCanvas != null)
        {
            if (_rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                // In overlay la camera va passata null
                return RectTransformUtility.WorldToScreenPoint(null, labelRect.position);
            }
            else
            {
                uiCam = _rootCanvas.worldCamera ? _rootCanvas.worldCamera : srcCam;
                return RectTransformUtility.WorldToScreenPoint(uiCam, labelRect.position);
            }
        }
        // Fallback
        return RectTransformUtility.WorldToScreenPoint(srcCam, labelRect.position);
    }

    // In editor, se cambi i parametri, ri-crea le risorse
    void OnValidate()
    {
        if (isActiveAndEnabled)
        {
            if (_rt && (_rt.width != sampleRTWidth || _rt.height != sampleRTHeight))
            {
                CleanupResources();
                EnsureResources();
            }
        }
    }
}
