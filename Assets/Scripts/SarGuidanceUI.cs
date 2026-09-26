using UnityEngine;

public class SarGuidanceUI : MonoBehaviour
{
    [Header("Referencias")]
    public TerrainLayerManager layerManager;
    [Tooltip("El colisionador de este Canvas que bloquea el láser")]
    public BoxCollider canvasCollider; 

    [Header("Paneles SAR")]
    public GameObject microExplanationPanel;
    public GameObject sarLegendPanel;

    [Header("Persistencia")]
    [Tooltip("Si está activo, la explicación se muestra solo una vez en la vida.")]
    public bool recordarEntreSesiones = false;

    private const string TutorialPlayerPrefsKey = "SAR_MICRO_EXPLANATION_SEEN";
    private bool tutorialVisto = false;

    public bool IsMicroExplanationActive { get; private set; }

    // =========================================================
    // AWAKE & START
    // =========================================================
    private void Awake()
    {
        // Autocompletar el colisionador si se nos olvida asignarlo
        if (canvasCollider == null) canvasCollider = GetComponent<BoxCollider>();

        if (recordarEntreSesiones)
        {
            tutorialVisto = PlayerPrefs.GetInt(TutorialPlayerPrefsKey, 0) == 1;
        }

        OcultarTodo();
    }

    private void Start()
    {
        if (layerManager == null)
        {
            Debug.LogError("[SarGuidanceUI] TerrainLayerManager no está asignado.");
            return;
        }

        layerManager.OnSarStateChangedLocal += OnSarStateChanged;
        OnSarStateChanged(layerManager.IsSarActive);
    }

    private void OnDestroy()
    {
        if (layerManager != null)
        {
            layerManager.OnSarStateChangedLocal -= OnSarStateChanged;
        }
    }

    // =========================================================
    // CAMBIO DE CAPA
    // =========================================================
    private void OnSarStateChanged(bool sarActive)
    {
        if (!sarActive)
        {
            OcultarTodo();
            return;
        }

        if (!tutorialVisto)
        {
            MostrarMicroExplanation();
        }
        else
        {
            MostrarLeyenda();
        }
    }

    // =========================================================
    // MICROEXPLICACIÓN Y BOTÓN CONTINUAR (PUNTO 4)
    // =========================================================
    private void MostrarMicroExplanation()
    {
        IsMicroExplanationActive = true;

        if (canvasCollider != null) canvasCollider.enabled = true; // Activa el muro físico
        if (microExplanationPanel != null) microExplanationPanel.SetActive(true);
        if (sarLegendPanel != null) sarLegendPanel.SetActive(false);
    }

    // MÉTODO QUE LLAMARÁ EL BOTÓN DE LA INTERFAZ
    public void ContinuarTutorial()
    {
        if (layerManager == null || !layerManager.IsSarActive)
        {
            IsMicroExplanationActive = false;
            return;
        }

        tutorialVisto = true;

        if (recordarEntreSesiones)
        {
            PlayerPrefs.SetInt(TutorialPlayerPrefsKey, 1);
            PlayerPrefs.Save();
        }

        MostrarLeyenda();
    }

    // =========================================================
    // LEYENDA Y OCULTAMIENTO
    // =========================================================
    private void MostrarLeyenda()
    {
        IsMicroExplanationActive = false;

        if (canvasCollider != null) canvasCollider.enabled = true; // Activa el muro físico
        if (microExplanationPanel != null) microExplanationPanel.SetActive(false);
        if (sarLegendPanel != null) sarLegendPanel.SetActive(true);
    }

    private void OcultarTodo()
    {
        IsMicroExplanationActive = false;

        if (canvasCollider != null) canvasCollider.enabled = false; // APAGA EL MURO FÍSICO
        if (microExplanationPanel != null) microExplanationPanel.SetActive(false);
        if (sarLegendPanel != null) sarLegendPanel.SetActive(false);
    }

    // =========================================================
    // UTILIDAD
    // =========================================================
    public void ResetTutorialLocal()
    {
        tutorialVisto = false;
        PlayerPrefs.DeleteKey(TutorialPlayerPrefsKey);

        if (layerManager != null && layerManager.IsSarActive)
        {
            MostrarMicroExplanation();
        }
    }
}