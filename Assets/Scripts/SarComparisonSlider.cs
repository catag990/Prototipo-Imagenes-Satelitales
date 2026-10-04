using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SarComparisonSlider :
    MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IBeginDragHandler,
    IEndDragHandler
{
    [Header("Referencias")]
    public TerrainLayerManager layerManager;
    public SarGuidanceUI guidanceUI;
    public Slider comparisonSlider;

    private bool usuarioInteractuando = false;

    // =========================================================
    // START & DESTROY
    // =========================================================
    private void Start()
    {
        if (comparisonSlider == null) comparisonSlider = GetComponent<Slider>();

        if (comparisonSlider == null)
        {
            Debug.LogError("[SarComparisonSlider] No existe componente Slider.");
            return;
        }

        comparisonSlider.minValue = 0f;
        comparisonSlider.maxValue = 1f;
        comparisonSlider.wholeNumbers = false;

        comparisonSlider.onValueChanged.AddListener(OnSliderValueChanged);

        if (layerManager != null)
        {
            layerManager.OnSarStateChangedLocal += OnSarStateChanged;
            layerManager.OnLocalBlendChanged += OnLocalBlendChanged;
            ActualizarEstado(layerManager.IsSarActive);
        }
    }

    private void OnDestroy()
    {
        if (comparisonSlider != null)
        {
            comparisonSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
        }

        if (layerManager != null)
        {
            layerManager.OnSarStateChangedLocal -= OnSarStateChanged;
            layerManager.OnLocalBlendChanged -= OnLocalBlendChanged;
        }
    }

    // =========================================================
    // CAMBIO DEL SLIDER
    // =========================================================
    private void OnSliderValueChanged(float value)
    {
        if (!usuarioInteractuando) return;
        if (layerManager == null || !layerManager.IsSarActive) return;
        if (guidanceUI != null && guidanceUI.IsMicroExplanationActive) return;

        // MODIFICADO: Ahora envía el valor deslizado al servidor para todos
        layerManager.SetSharedComparisonBlend(value);
    }

    // =========================================================
    // SINCRONIZACIÓN VISUAL
    // =========================================================
    private void OnLocalBlendChanged(float blend)
    {
        if (comparisonSlider == null) return;
        comparisonSlider.SetValueWithoutNotify(blend);
    }

    private void OnSarStateChanged(bool sarActive)
    {
        ActualizarEstado(sarActive);
    }

    private void ActualizarEstado(bool sarActive)
    {
        if (comparisonSlider == null) return;

        comparisonSlider.interactable = sarActive && layerManager != null && layerManager.SupportsSmoothComparison;

        // MODIFICADO: Lee el valor actual de la red para posicionarse
        float targetValue = (sarActive && layerManager != null) ? layerManager.CurrentSarBlend : 0f;
        comparisonSlider.SetValueWithoutNotify(targetValue);
    }

    // =========================================================
    // POINTER / DRAG
    // =========================================================
    public void OnPointerDown(PointerEventData eventData)
    {
        usuarioInteractuando = true;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        usuarioInteractuando = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        FinalizarComparacion();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        FinalizarComparacion();
    }

    private void FinalizarComparacion()
    {
        if (!usuarioInteractuando) return;
        usuarioInteractuando = false;
        // Al soltar el slider, simplemente se queda en su valor actual de red.
    }
}