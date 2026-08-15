using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MarkerRowUI : MonoBehaviour
{
    public TextMeshProUGUI nombreTxt;

    public Button btnOcultar;
    public Button btnCambiarTag;

    [Header("Eliminación")]
    public Button btnEliminar;

    private GeoMarkerData dataRef;
    private InteractionManager manager;

    private bool eliminacionSolicitada =
        false;

    // =========================================================
    // SETUP
    // =========================================================

    public void Setup(
        GeoMarkerData data,
        InteractionManager mgr)
    {
        dataRef =
            data;

        manager =
            mgr;

        if (btnOcultar != null)
        {
            btnOcultar.onClick.AddListener(
                OnOcultar);
        }

        if (btnCambiarTag != null)
        {
            btnCambiarTag.onClick.AddListener(
                OnCambiarTag);
        }

        if (btnEliminar != null)
        {
            btnEliminar.onClick.AddListener(
                OnEliminar);
        }

        ActualizarTextos();
    }

    // =========================================================
    // ACTUALIZACIÓN DESDE RED
    // =========================================================

    public void ActualizarDesdeRed(
        GeoMarkerData newData)
    {
        dataRef.isVisible =
            newData.isVisible;

        dataRef.color =
            newData.color;

        dataRef.tag =
            newData.tag;

        ActualizarTextos();
    }

    // =========================================================
    // VISIBILIDAD
    // =========================================================

    private void OnOcultar()
    {
        if (manager == null ||
            eliminacionSolicitada)
        {
            return;
        }

        dataRef.isVisible =
            !dataRef.isVisible;

        manager.SolicitarCambioMarcador(
            dataRef.markerID,
            dataRef.isVisible,
            dataRef.color,
            dataRef.tag);
    }

    // =========================================================
    // TAG
    // =========================================================

    private void OnCambiarTag()
    {
        if (manager == null ||
            eliminacionSolicitada)
        {
            return;
        }

        int nextTag =
            ((int)dataRef.tag + 1) % 4;

        dataRef.tag =
            (MarkerTag)nextTag;

        switch (dataRef.tag)
        {
            case MarkerTag.Generico:
                dataRef.color =
                    Color.white;
                break;

            case MarkerTag.Riesgo:
                dataRef.color =
                    Color.red;
                break;

            case MarkerTag.Agua:
                dataRef.color =
                    Color.blue;
                break;

            case MarkerTag.Alerta:
                dataRef.color =
                    Color.yellow;
                break;
        }

        manager.SolicitarCambioMarcador(
            dataRef.markerID,
            dataRef.isVisible,
            dataRef.color,
            dataRef.tag);
    }

    // =========================================================
    // ELIMINACIÓN
    // =========================================================

    private void OnEliminar()
    {
        if (manager == null ||
            eliminacionSolicitada)
        {
            return;
        }

        eliminacionSolicitada =
            true;

        // Evitar que el usuario envíe la solicitud
        // varias veces antes de recibir el RPC.
        if (btnEliminar != null)
        {
            btnEliminar.interactable =
                false;
        }

        if (btnOcultar != null)
        {
            btnOcultar.interactable =
                false;
        }

        if (btnCambiarTag != null)
        {
            btnCambiarTag.interactable =
                false;
        }

        manager.SolicitarEliminarMarcador(
            dataRef.markerID);
    }

    // =========================================================
    // TEXTO
    // =========================================================

    private void ActualizarTextos()
    {
        if (nombreTxt != null)
        {
            nombreTxt.text =
                $"{dataRef.type} [{dataRef.tag}]";

            nombreTxt.color =
                dataRef.color;
        }

        if (btnOcultar != null)
        {
            TextMeshProUGUI textoBoton =
                btnOcultar
                    .GetComponentInChildren<
                        TextMeshProUGUI>();

            if (textoBoton != null)
            {
                textoBoton.text =
                    dataRef.isVisible
                        ? "Ocultar"
                        : "Mostrar";
            }
        }
    }

    // =========================================================
    // ACCESO A DATOS
    // =========================================================

    public GeoMarkerData GetData()
    {
        return dataRef;
    }
}