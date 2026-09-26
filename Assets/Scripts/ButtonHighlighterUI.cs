using UnityEngine;
using UnityEngine.UI;

public class ButtonHighlighterUI : MonoBehaviour
{
    [Header("Referencias a Managers")]
    public InteractionManager interactionManager;
    public TerrainLayerManager layerManager;
    public TerrainModeManager modeManager;

    [Header("Imágenes de los Botones")]
    [Tooltip("Arrastra aquí el objeto del botón (tiene el componente Image)")]
    public Image btnPOI;
    public Image btnLazo;
    public Image btnCapa;
    public Image btnModo;

    [Header("Configuración Visual")]
    public Color colorInactivo = Color.white;
    public Color colorActivo = new Color(0.6f, 0.85f, 1f, 1f); // Azul claro destacado

    private void Start()
    {
        // 1. Escuchar a la Herramienta (POI / Lazo)
        if (interactionManager != null)
        {
            interactionManager.OnToolChanged += ActualizarBotonesHerramienta;
            ActualizarBotonesHerramienta(interactionManager.currentTool); // Estado inicial
        }

        // 2. Escuchar a la Capa (Óptico / SAR)
        if (layerManager != null)
        {
            layerManager.OnSarStateChangedLocal += ActualizarBotonCapa;
            ActualizarBotonCapa(layerManager.IsSarActive); // Estado inicial
        }

        // 3. Escuchar al Modo (Libre / Fija)
        if (modeManager != null)
        {
            modeManager.OnModeChangedLocal += ActualizarBotonModo;
            // El estado inicial de modo se empuja automáticamente desde el OnNetworkSpawn de TerrainModeManager.
        }
    }

    private void OnDestroy()
    {
        if (interactionManager != null) interactionManager.OnToolChanged -= ActualizarBotonesHerramienta;
        if (layerManager != null) layerManager.OnSarStateChangedLocal -= ActualizarBotonCapa;
        if (modeManager != null) modeManager.OnModeChangedLocal -= ActualizarBotonModo;
    }

    private void ActualizarBotonesHerramienta(ToolMode herramientaActiva)
    {
        if (btnPOI != null) btnPOI.color = (herramientaActiva == ToolMode.POI) ? colorActivo : colorInactivo;
        if (btnLazo != null) btnLazo.color = (herramientaActiva == ToolMode.Lasso) ? colorActivo : colorInactivo;
    }

    private void ActualizarBotonCapa(bool isSarActive)
    {
        // Se ilumina si la capa secundaria (SAR) está activa
        if (btnCapa != null) btnCapa.color = isSarActive ? colorActivo : colorInactivo;
    }

    private void ActualizarBotonModo(bool isMesaFija)
    {
        // Se ilumina si el Modo Alternativo (Mesa Fija) está activo
        if (btnModo != null) btnModo.color = isMesaFija ? colorActivo : colorInactivo;
    }
}