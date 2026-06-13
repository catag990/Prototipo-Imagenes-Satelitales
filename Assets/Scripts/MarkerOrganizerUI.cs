using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

public class MarkerOrganizerUI : MonoBehaviour
{
    public InteractionManager manager;
    public GameObject rowPrefab; 
    public Transform contentPanel; 

    [Header("Botones de Filtro")]
    public Button btnTodos;
    public Button btnPOIs;
    public Button btnLazos;

    private List<MarkerRowUI> filasInstanciadas = new List<MarkerRowUI>();

    void Start()
    {
        manager.OnMarkerAddedLocal += CrearFila;
        manager.OnEnvironmentReset += LimpiarLista; // Escucha el botón reset
        manager.OnMarkerUpdatedLocal += ActualizarFilaUI; // Escucha ediciones

        if (btnTodos != null) btnTodos.onClick.AddListener(() => FiltrarLista(null));
        if (btnPOIs != null) btnPOIs.onClick.AddListener(() => FiltrarLista(MarkerType.POI));
        if (btnLazos != null) btnLazos.onClick.AddListener(() => FiltrarLista(MarkerType.Lasso));

        manager.RefrescarUIExistente();
    }

    private void CrearFila(GeoMarkerData data)
    {
        GameObject nuevaFila = Instantiate(rowPrefab, contentPanel, false);
        nuevaFila.transform.localScale = Vector3.one;
        nuevaFila.transform.localPosition = new Vector3(nuevaFila.transform.localPosition.x, nuevaFila.transform.localPosition.y, 0f);

        MarkerRowUI rowScript = nuevaFila.GetComponent<MarkerRowUI>();
        rowScript.Setup(data, manager);
        
        filasInstanciadas.Add(rowScript);
    }

    private void LimpiarLista()
    {
        foreach (MarkerRowUI fila in filasInstanciadas)
        {
            if (fila != null) Destroy(fila.gameObject);
        }
        filasInstanciadas.Clear();
    }

    private void ActualizarFilaUI(GeoMarkerData data)
    {
        foreach (MarkerRowUI fila in filasInstanciadas)
        {
            if (fila != null && fila.GetData().markerID == data.markerID)
            {
                fila.ActualizarDesdeRed(data);
                break;
            }
        }
    }

    private void FiltrarLista(MarkerType? tipoPermitido)
    {
        foreach (MarkerRowUI fila in filasInstanciadas)
        {
            if (fila == null) continue; 
            if (tipoPermitido == null) fila.gameObject.SetActive(true);
            else fila.gameObject.SetActive(fila.GetData().type == tipoPermitido);
        }
    }
}