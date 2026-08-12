using UnityEngine;
using System.Collections;

public class SarGuidanceUI : MonoBehaviour
{
    [Header("Referencias")]
    public TerrainLayerManager layerManager;

    [Header("Paneles SAR")]
    public GameObject microExplanationPanel;
    public GameObject sarLegendPanel;

    [Header("Duración")]
    [Range(5f, 8f)]
    public float microExplanationDuration = 7f;

    [Header("Persistencia")]
    [Tooltip(
        "Si está activo, la explicación se muestra " +
        "solo una vez en esta instalación. " +
        "Si está desactivado, vuelve a mostrarse " +
        "en la primera activación SAR de cada ejecución.")]
    public bool recordarEntreSesiones = false;

    private const string TutorialPlayerPrefsKey =
        "SAR_MICRO_EXPLANATION_SEEN";

    private bool tutorialVisto = false;
    private Coroutine tutorialCoroutine;

    public bool IsMicroExplanationActive
    {
        get;
        private set;
    }

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        if (recordarEntreSesiones)
        {
            tutorialVisto =
                PlayerPrefs.GetInt(
                    TutorialPlayerPrefsKey,
                    0) == 1;
        }

        OcultarTodo();
    }

    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        if (layerManager == null)
        {
            Debug.LogError(
                "[SarGuidanceUI] " +
                "TerrainLayerManager no está asignado.");

            return;
        }

        layerManager.OnSarStateChangedLocal +=
            OnSarStateChanged;

        // Necesario para Late-Joining o cuando
        // TerrainLayerManager ya se inicializó antes.
        OnSarStateChanged(
            layerManager.IsSarActive);
    }

    private void OnDestroy()
    {
        if (layerManager != null)
        {
            layerManager.OnSarStateChangedLocal -=
                OnSarStateChanged;
        }

        DetenerTutorial();
    }

    // =========================================================
    // CAMBIO DE CAPA
    // =========================================================

    private void OnSarStateChanged(
        bool sarActive)
    {
        if (!sarActive)
        {
            DetenerTutorial();
            OcultarTodo();
            return;
        }

        // SAR está activa.
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
    // MICROEXPLICACIÓN
    // =========================================================

    private void MostrarMicroExplanation()
    {
        DetenerTutorial();

        IsMicroExplanationActive =
            true;

        if (microExplanationPanel != null)
        {
            microExplanationPanel.SetActive(
                true);
        }

        if (sarLegendPanel != null)
        {
            sarLegendPanel.SetActive(
                false);
        }

        tutorialCoroutine =
            StartCoroutine(
                MicroExplanationRoutine());
    }

    private IEnumerator MicroExplanationRoutine()
    {
        yield return new WaitForSeconds(
            microExplanationDuration);

        tutorialCoroutine =
            null;

        // Si alguien cambió nuevamente a Óptico
        // antes de finalizar, no se registra
        // como tutorial completado.
        if (layerManager == null ||
            !layerManager.IsSarActive)
        {
            IsMicroExplanationActive =
                false;

            yield break;
        }

        tutorialVisto =
            true;

        if (recordarEntreSesiones)
        {
            PlayerPrefs.SetInt(
                TutorialPlayerPrefsKey,
                1);

            PlayerPrefs.Save();
        }

        IsMicroExplanationActive =
            false;

        if (microExplanationPanel != null)
        {
            microExplanationPanel.SetActive(
                false);
        }

        MostrarLeyenda();
    }

    // =========================================================
    // LEYENDA
    // =========================================================

    private void MostrarLeyenda()
    {
        IsMicroExplanationActive =
            false;

        if (microExplanationPanel != null)
        {
            microExplanationPanel.SetActive(
                false);
        }

        if (sarLegendPanel != null)
        {
            sarLegendPanel.SetActive(
                true);
        }
    }

    // =========================================================
    // UTILIDADES
    // =========================================================

    private void OcultarTodo()
    {
        IsMicroExplanationActive =
            false;

        if (microExplanationPanel != null)
        {
            microExplanationPanel.SetActive(
                false);
        }

        if (sarLegendPanel != null)
        {
            sarLegendPanel.SetActive(
                false);
        }
    }

    private void DetenerTutorial()
    {
        if (tutorialCoroutine != null)
        {
            StopCoroutine(
                tutorialCoroutine);

            tutorialCoroutine =
                null;
        }

        IsMicroExplanationActive =
            false;
    }

    // Útil durante pruebas de usabilidad.
    public void ResetTutorialLocal()
    {
        DetenerTutorial();

        tutorialVisto =
            false;

        PlayerPrefs.DeleteKey(
            TutorialPlayerPrefsKey);

        if (layerManager != null &&
            layerManager.IsSarActive)
        {
            MostrarMicroExplanation();
        }
    }
}