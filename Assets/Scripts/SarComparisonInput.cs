using UnityEngine;
using UnityEngine.InputSystem;

public class SarComparisonInput : MonoBehaviour
{
    [Header("Referencias")]
    public TerrainLayerManager layerManager;
    public SarGuidanceUI guidanceUI;

    [Header("Input")]
    [Tooltip("Botón que se mantiene presionado para visualizar temporalmente Óptico.")]
    public InputActionProperty compareOpticalAction;

    private bool holdActive = false;
    private bool actionEnabledByThisScript = false;

    // =========================================================
    // ENABLE
    // =========================================================
    private void OnEnable()
    {
        if (compareOpticalAction.action == null) return;

        compareOpticalAction.action.started += OnCompareStarted;
        compareOpticalAction.action.canceled += OnCompareCanceled;

        if (!compareOpticalAction.action.enabled)
        {
            compareOpticalAction.action.Enable();
            actionEnabledByThisScript = true;
        }
    }

    private void OnDisable()
    {
        if (compareOpticalAction.action == null) return;

        compareOpticalAction.action.started -= OnCompareStarted;
        compareOpticalAction.action.canceled -= OnCompareCanceled;

        if (actionEnabledByThisScript)
        {
            compareOpticalAction.action.Disable();
            actionEnabledByThisScript = false;
        }

        if (holdActive && layerManager != null)
        {
            layerManager.EndSharedOpticalPreview();
        }

        holdActive = false;
    }

    // =========================================================
    // PRESIONAR
    // =========================================================
    private void OnCompareStarted(InputAction.CallbackContext context)
    {
        if (layerManager == null || !layerManager.IsSarActive) return;

        if (guidanceUI != null && guidanceUI.IsMicroExplanationActive) return;

        holdActive = true;
        
        // MODIFICADO: La vista óptica temporal ahora se transmite a todos
        layerManager.BeginSharedOpticalPreview();
    }

    // =========================================================
    // SOLTAR
    // =========================================================
    private void OnCompareCanceled(InputAction.CallbackContext context)
    {
        if (!holdActive) return;

        holdActive = false;

        if (layerManager != null)
        {
            // MODIFICADO: Devuelve a todos a la capa SAR guardada
            layerManager.EndSharedOpticalPreview();
        }
    }
}