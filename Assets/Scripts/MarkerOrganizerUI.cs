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

    private List<MarkerRowUI> filasInstanciadas =
        new List<MarkerRowUI>();

    // =========================================================
    // START
    // =========================================================

    void Start()
    {
        if (manager == null)
        {
            Debug.LogError(
                "[MarkerOrganizerUI] " +
                "InteractionManager no está asignado.");

            return;
        }

        manager.OnMarkerAddedLocal +=
            CrearFila;

        manager.OnMarkerUpdatedLocal +=
            ActualizarFilaUI;

        manager.OnMarkerDeletedLocal +=
            EliminarFilaUI;

        manager.OnEnvironmentReset +=
            LimpiarLista;

        if (btnTodos != null)
        {
            btnTodos.onClick.AddListener(
                () => FiltrarLista(null));
        }

        if (btnPOIs != null)
        {
            btnPOIs.onClick.AddListener(
                () => FiltrarLista(
                    MarkerType.POI));
        }

        if (btnLazos != null)
        {
            btnLazos.onClick.AddListener(
                () => FiltrarLista(
                    MarkerType.Lasso));
        }

        manager.RefrescarUIExistente();
    }

    // =========================================================
    // LIMPIEZA DE EVENTOS
    // =========================================================

    private void OnDestroy()
    {
        if (manager == null)
            return;

        manager.OnMarkerAddedLocal -=
            CrearFila;

        manager.OnMarkerUpdatedLocal -=
            ActualizarFilaUI;

        manager.OnMarkerDeletedLocal -=
            EliminarFilaUI;

        manager.OnEnvironmentReset -=
            LimpiarLista;
    }

    // =========================================================
    // CREAR FILA
    // =========================================================

    private void CrearFila(
        GeoMarkerData data)
    {
        // Evitar filas duplicadas.
        foreach (MarkerRowUI fila
                 in filasInstanciadas)
        {
            if (fila != null &&
                fila.GetData().markerID ==
                    data.markerID)
            {
                return;
            }
        }

        if (rowPrefab == null ||
            contentPanel == null)
        {
            Debug.LogError(
                "[MarkerOrganizerUI] " +
                "rowPrefab o contentPanel no está asignado.");

            return;
        }

        GameObject nuevaFila =
            Instantiate(
                rowPrefab,
                contentPanel,
                false);

        nuevaFila.transform.localScale =
            Vector3.one;

        nuevaFila.transform.localPosition =
            new Vector3(
                nuevaFila.transform.localPosition.x,
                nuevaFila.transform.localPosition.y,
                0f);

        MarkerRowUI rowScript =
            nuevaFila.GetComponent<
                MarkerRowUI>();

        if (rowScript == null)
        {
            Debug.LogError(
                "[MarkerOrganizerUI] " +
                "El prefab no contiene MarkerRowUI.");

            Destroy(nuevaFila);

            return;
        }

        rowScript.Setup(
            data,
            manager);

        filasInstanciadas.Add(
            rowScript);
    }

    // =========================================================
    // ELIMINAR FILA INDIVIDUAL
    // =========================================================

    private void EliminarFilaUI(
        ulong markerID)
    {
        for (int i =
             filasInstanciadas.Count - 1;
             i >= 0;
             i--)
        {
            MarkerRowUI fila =
                filasInstanciadas[i];

            if (fila == null)
            {
                filasInstanciadas.RemoveAt(
                    i);

                continue;
            }

            if (fila.GetData().markerID ==
                markerID)
            {
                filasInstanciadas.RemoveAt(
                    i);

                Destroy(
                    fila.gameObject);

                return;
            }
        }
    }

    // =========================================================
    // RESET COMPLETO
    // =========================================================

    private void LimpiarLista()
    {
        foreach (MarkerRowUI fila
                 in filasInstanciadas)
        {
            if (fila != null)
            {
                Destroy(
                    fila.gameObject);
            }
        }

        filasInstanciadas.Clear();
    }

    // =========================================================
    // ACTUALIZAR FILA
    // =========================================================

    private void ActualizarFilaUI(
        GeoMarkerData data)
    {
        foreach (MarkerRowUI fila
                 in filasInstanciadas)
        {
            if (fila != null &&
                fila.GetData().markerID ==
                    data.markerID)
            {
                fila.ActualizarDesdeRed(
                    data);

                return;
            }
        }
    }

    // =========================================================
    // FILTRADO
    // =========================================================

    private void FiltrarLista(
        MarkerType? tipoPermitido)
    {
        foreach (MarkerRowUI fila
                 in filasInstanciadas)
        {
            if (fila == null)
                continue;

            if (tipoPermitido == null)
            {
                fila.gameObject.SetActive(
                    true);
            }
            else
            {
                fila.gameObject.SetActive(
                    fila.GetData().type ==
                    tipoPermitido);
            }
        }
    }
}
