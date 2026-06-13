using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MarkerRowUI : MonoBehaviour
{
    public TextMeshProUGUI nombreTxt;
    public Button btnOcultar;
    public Button btnCambiarTag;
    
    private GeoMarkerData dataRef;
    private InteractionManager manager;

    public void Setup(GeoMarkerData data, InteractionManager mgr)
    {
        dataRef = data;
        manager = mgr;
        btnOcultar.onClick.AddListener(OnOcultar);
        btnCambiarTag.onClick.AddListener(OnCambiarTag);
        ActualizarTextos();
    }

    // Nuevo método para recibir datos de la red y actualizarse a sí misma
    public void ActualizarDesdeRed(GeoMarkerData newData)
    {
        dataRef.isVisible = newData.isVisible;
        dataRef.color = newData.color;
        dataRef.tag = newData.tag;
        ActualizarTextos();
    }

    private void OnOcultar()
    {
        dataRef.isVisible = !dataRef.isVisible;
        manager.SolicitarCambioMarcador(dataRef.markerID, dataRef.isVisible, dataRef.color, dataRef.tag);
    }

    private void OnCambiarTag()
    {
        int nextTag = ((int)dataRef.tag + 1) % 4;
        dataRef.tag = (MarkerTag)nextTag;
        switch(dataRef.tag)
        {
            case MarkerTag.Generico: dataRef.color = Color.white; break;
            case MarkerTag.Riesgo:   dataRef.color = Color.red; break;
            case MarkerTag.Agua:     dataRef.color = Color.blue; break;
            case MarkerTag.Calor:    dataRef.color = Color.yellow; break;
        }
        manager.SolicitarCambioMarcador(dataRef.markerID, dataRef.isVisible, dataRef.color, dataRef.tag);
    }

    private void ActualizarTextos()
    {
        nombreTxt.text = $"{dataRef.type} [{dataRef.tag}]";
        nombreTxt.color = dataRef.color;
        btnOcultar.GetComponentInChildren<TextMeshProUGUI>().text = dataRef.isVisible ? "Ocultar" : "Mostrar";
    }

    public GeoMarkerData GetData() => dataRef;
}