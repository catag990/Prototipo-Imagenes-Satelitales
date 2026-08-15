using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public enum ToolMode
{
    POI,
    Lasso
}

public class InteractionManager : NetworkBehaviour
{
    [Header("Herramientas")]
    public ToolMode currentTool = ToolMode.POI;
    public POIPlacementSystem poiSystem;
    public LassoTool lassoTool;

    [Header("Control de Terreno")]
    public NetworkTerrainSync terrainSync;
    public XRGrabInteractable terrenoInteractable;

    [Header("Prefabs y Entorno")]
    public GameObject flagPrefab;
    public Transform contenedorTerreno;

    private bool yaPuseUnPOI = false;
    private bool isLassoDrawing = false;
    private bool errorTerrainSyncReportado = false;

    private NetworkList<GeoMarkerData> historialMarcadores;
    private NetworkList<Vector3> historialPuntosLazo;

    private Dictionary<ulong, GameObject> localVisuals =
        new Dictionary<ulong, GameObject>();

    // ID monotónico.
    // No depende de historialMarcadores.Count porque
    // la eliminación individual haría posible repetir IDs.
    private ulong nextMarkerID = 0;

    // =========================================================
    // EVENTOS DE UI
    // =========================================================

    public System.Action<GeoMarkerData> OnMarkerAddedLocal;
    public System.Action<GeoMarkerData> OnMarkerUpdatedLocal;

    // Nuevo evento de eliminación individual.
    public System.Action<ulong> OnMarkerDeletedLocal;

    public System.Action OnEnvironmentReset;

    // =========================================================
    // INICIALIZACIÓN
    // =========================================================

    void Awake()
    {
        historialMarcadores =
            new NetworkList<GeoMarkerData>();

        historialPuntosLazo =
            new NetworkList<Vector3>();

        // Intentar recuperar referencias si no fueron
        // asignadas directamente desde el Inspector.
        if (contenedorTerreno != null)
        {
            if (terrainSync == null)
            {
                terrainSync =
                    contenedorTerreno
                        .GetComponent<NetworkTerrainSync>();

                if (terrainSync == null)
                {
                    terrainSync =
                        contenedorTerreno
                            .GetComponentInParent<
                                NetworkTerrainSync>();
                }
            }

            if (terrenoInteractable == null)
            {
                terrenoInteractable =
                    contenedorTerreno
                        .GetComponent<XRGrabInteractable>();

                if (terrenoInteractable == null)
                {
                    terrenoInteractable =
                        contenedorTerreno
                            .GetComponentInParent<
                                XRGrabInteractable>();
                }
            }
        }
    }

    // =========================================================
    // BLOQUEO GLOBAL DE MARCACIÓN
    // =========================================================

    private bool EstaBloqueadaLaMarcacion()
    {
        // Fallar cerrado:
        // si falta NetworkTerrainSync no se permite crear
        // POIs o lazos.
        if (terrainSync == null)
        {
            if (!errorTerrainSyncReportado)
            {
                Debug.LogError(
                    "[InteractionManager] terrainSync no está asignado. " +
                    "La creación de POIs y lazos queda bloqueada.");

                errorTerrainSyncReportado = true;
            }

            return true;
        }

        // Bloqueo local inmediato.
        if (terrenoInteractable != null &&
            terrenoInteractable.isSelected)
        {
            return true;
        }

        // Bloqueo global proveniente de NetworkTerrainSync.
        if (terrainSync.IsTerrainBeingManipulated)
        {
            return true;
        }

        return false;
    }

    private void OnTerrainManipulationChanged(
        bool estaManipulando)
    {
        if (!estaManipulando)
            return;

        CancelarInteraccionDeMarcado();
    }

    private void CancelarInteraccionDeMarcado()
    {
        yaPuseUnPOI = false;

        if (isLassoDrawing)
        {
            if (lassoTool != null)
            {
                lassoTool.CancelarLazo();
            }

            isLassoDrawing = false;
        }
    }

    // =========================================================
    // NETWORK SPAWN
    // =========================================================

    public override void OnNetworkSpawn()
    {
        if (terrainSync != null)
        {
            terrainSync.OnTerrainManipulationStateChanged +=
                OnTerrainManipulationChanged;
        }
        else
        {
            Debug.LogError(
                "[InteractionManager] " +
                "No existe referencia a NetworkTerrainSync.");
        }

        // El servidor calcula el próximo ID disponible.
        //
        // Esto también protege el sistema si el objeto
        // vuelve a realizar NetworkSpawn manteniendo historial.
        if (IsServer)
        {
            nextMarkerID = 0;

            foreach (GeoMarkerData marker
                     in historialMarcadores)
            {
                if (marker.markerID >= nextMarkerID)
                {
                    nextMarkerID =
                        marker.markerID + 1;
                }
            }
        }

        // Reconstrucción para Late-Joining.
        foreach (GeoMarkerData marker
                 in historialMarcadores)
        {
            if (marker.type == MarkerType.POI)
            {
                ReconstruirPOI(marker);
            }
            else if (marker.type == MarkerType.Lasso)
            {
                ReconstruirLazoDesdeHistorial(marker);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (terrainSync != null)
        {
            terrainSync.OnTerrainManipulationStateChanged -=
                OnTerrainManipulationChanged;
        }
    }

    // =========================================================
    // SELECCIÓN DE HERRAMIENTAS
    // =========================================================

    public void SetToolPOI()
    {
        currentTool =
            ToolMode.POI;
    }

    public void SetToolLasso()
    {
        currentTool =
            ToolMode.Lasso;
    }

    // =========================================================
    // PROCESAMIENTO DE INPUT
    // =========================================================

    public void ProcesarEntrada(
        RaycastHit hit,
        bool estaPresionado)
    {
        // Ningún usuario puede marcar mientras
        // cualquier usuario manipula el terreno.
        if (EstaBloqueadaLaMarcacion())
        {
            CancelarInteraccionDeMarcado();
            return;
        }

        if (estaPresionado)
        {
            // =================================================
            // LAZO
            // =================================================

            if (currentTool == ToolMode.Lasso &&
                hit.collider != null)
            {
                if (!isLassoDrawing)
                {
                    lassoTool.IniciarLazo(
                        hit.point);

                    isLassoDrawing =
                        true;
                }
                else
                {
                    lassoTool.ActualizarLazo(
                        hit.point);
                }
            }

            // =================================================
            // POI
            // =================================================

            else if (
                currentTool == ToolMode.POI &&
                !yaPuseUnPOI &&
                contenedorTerreno != null &&
                hit.collider != null)
            {
                Vector3 posicionLocal =
                    contenedorTerreno
                        .InverseTransformPoint(
                            hit.point);

                Vector3 normalLocal =
                    contenedorTerreno
                        .InverseTransformDirection(
                            hit.normal);

                RegistrarPOIServerRpc(
                    posicionLocal,
                    normalLocal);

                yaPuseUnPOI =
                    true;
            }
        }
        else
        {
            yaPuseUnPOI =
                false;

            if (isLassoDrawing)
            {
                // Revalidar antes de finalizar.
                if (EstaBloqueadaLaMarcacion())
                {
                    CancelarInteraccionDeMarcado();
                    return;
                }

                Vector3[] puntosMundo =
                    lassoTool.TerminarLazo();

                isLassoDrawing =
                    false;

                if (puntosMundo.Length > 1 &&
                    contenedorTerreno != null)
                {
                    Vector3[] puntosLocales =
                        new Vector3[
                            puntosMundo.Length];

                    for (int i = 0;
                         i < puntosMundo.Length;
                         i++)
                    {
                        puntosLocales[i] =
                            contenedorTerreno
                                .InverseTransformPoint(
                                    puntosMundo[i]);
                    }

                    RegistrarLazoServerRpc(
                        puntosLocales);
                }
            }
        }
    }

    // =========================================================
    // CREACIÓN DE POI
    // =========================================================

    [Rpc(
        SendTo.Server,
        InvokePermission =
            RpcInvokePermission.Everyone)]
    private void RegistrarPOIServerRpc(
        Vector3 posLocal,
        Vector3 normLocal)
    {
        // Validación autoritativa.
        if (terrainSync == null)
        {
            Debug.LogWarning(
                "[InteractionManager] " +
                "POI rechazado: terrainSync no disponible.");

            return;
        }

        if (terrainSync.IsTerrainLockedByNetwork)
        {
            Debug.Log(
                "[InteractionManager] " +
                "POI rechazado: terreno en manipulación.");

            return;
        }

        GeoMarkerData nuevoPOI =
            new GeoMarkerData
            {
                markerID =
                    nextMarkerID++,

                type =
                    MarkerType.POI,

                position =
                    posLocal,

                normal =
                    normLocal,

                isVisible =
                    true,

                color =
                    Color.white,

                tag =
                    MarkerTag.Generico
            };

        historialMarcadores.Add(
            nuevoPOI);

        DibujarPOIRpc(
            nuevoPOI);
    }

    // =========================================================
    // CREACIÓN DE LAZO
    // =========================================================

    [Rpc(
        SendTo.Server,
        InvokePermission =
            RpcInvokePermission.Everyone)]
    private void RegistrarLazoServerRpc(
        Vector3[] puntosLocales)
    {
        // Validación autoritativa.
        if (terrainSync == null)
        {
            Debug.LogWarning(
                "[InteractionManager] " +
                "Lazo rechazado: terrainSync no disponible.");

            return;
        }

        if (terrainSync.IsTerrainLockedByNetwork)
        {
            Debug.Log(
                "[InteractionManager] " +
                "Lazo rechazado: terreno en manipulación.");

            return;
        }

        if (puntosLocales == null ||
            puntosLocales.Length <= 1)
        {
            return;
        }

        int startIdx =
            historialPuntosLazo.Count;

        foreach (Vector3 punto
                 in puntosLocales)
        {
            historialPuntosLazo.Add(
                punto);
        }

        GeoMarkerData nuevoLazo =
            new GeoMarkerData
            {
                markerID =
                    nextMarkerID++,

                type =
                    MarkerType.Lasso,

                lassoStartIndex =
                    startIdx,

                lassoPointCount =
                    puntosLocales.Length,

                isVisible =
                    true,

                color =
                    Color.white,

                tag =
                    MarkerTag.Generico
            };

        historialMarcadores.Add(
            nuevoLazo);

        DibujarLazoRpc(
            nuevoLazo,
            puntosLocales);
    }

    // =========================================================
    // RPC DE REPRESENTACIÓN
    // =========================================================

    [Rpc(SendTo.Everyone)]
    private void DibujarPOIRpc(
        GeoMarkerData data)
    {
        ReconstruirPOI(data);
    }

    [Rpc(SendTo.Everyone)]
    private void DibujarLazoRpc(
        GeoMarkerData data,
        Vector3[] puntosLocales)
    {
        ReconstruirLazo(
            data,
            puntosLocales);
    }

    // =========================================================
    // RECONSTRUCCIÓN DE POI
    // =========================================================

    private void ReconstruirPOI(
        GeoMarkerData data)
    {
        if (contenedorTerreno == null)
            return;

        Vector3 posicionMundo =
            contenedorTerreno
                .TransformPoint(
                    data.position);

        Vector3 normalMundo =
            contenedorTerreno
                .TransformDirection(
                    data.normal);

        GameObject nuevaBandera =
            Instantiate(
                flagPrefab,
                posicionMundo,
                Quaternion.FromToRotation(
                    Vector3.up,
                    normalMundo));

        nuevaBandera.transform.SetParent(
            contenedorTerreno,
            true);

        if (poiSystem != null)
        {
            poiSystem.RegisterPOI(
                nuevaBandera);
        }

        localVisuals[data.markerID] =
            nuevaBandera;

        AplicarEstiloVisual(
            nuevaBandera,
            data);

        OnMarkerAddedLocal?.Invoke(
            data);
    }

    // =========================================================
    // RECONSTRUCCIÓN DE LAZO
    // =========================================================

    private void ReconstruirLazo(
        GeoMarkerData data,
        Vector3[] puntosLocales)
    {
        if (contenedorTerreno == null ||
            lassoTool == null)
        {
            return;
        }

        GameObject lineaObj =
            new GameObject(
                $"Lazo_Network_{data.markerID}");

        lineaObj.transform.SetParent(
            contenedorTerreno,
            false);

        LineRenderer lr =
            lineaObj.AddComponent<
                LineRenderer>();

        lr.material =
            lassoTool.materialLinea;

        lr.startWidth =
            lassoTool.anchoLinea;

        lr.endWidth =
            lassoTool.anchoLinea;

        lr.useWorldSpace =
            false;

        lr.positionCount =
            puntosLocales.Length;

        lr.SetPositions(
            puntosLocales);

        localVisuals[data.markerID] =
            lineaObj;

        AplicarEstiloVisual(
            lineaObj,
            data);

        OnMarkerAddedLocal?.Invoke(
            data);
    }

    private void ReconstruirLazoDesdeHistorial(
        GeoMarkerData data)
    {
        if (data.lassoPointCount <= 0)
            return;

        Vector3[] puntosExtraidos =
            new Vector3[
                data.lassoPointCount];

        for (int i = 0;
             i < data.lassoPointCount;
             i++)
        {
            int indice =
                data.lassoStartIndex + i;

            if (indice < 0 ||
                indice >= historialPuntosLazo.Count)
            {
                Debug.LogWarning(
                    "[InteractionManager] " +
                    "Índice de lazo fuera de rango.");

                return;
            }

            puntosExtraidos[i] =
                historialPuntosLazo[
                    indice];
        }

        ReconstruirLazo(
            data,
            puntosExtraidos);
    }

    // =========================================================
    // ACTUALIZACIÓN DE MARCADORES
    // =========================================================

    public void SolicitarCambioMarcador(
        ulong markerID,
        bool isVisible,
        Color newColor,
        MarkerTag newTag)
    {
        UpdateMarkerServerRpc(
            markerID,
            isVisible,
            newColor,
            newTag);
    }

    [Rpc(
        SendTo.Server,
        InvokePermission =
            RpcInvokePermission.Everyone)]
    private void UpdateMarkerServerRpc(
        ulong markerID,
        bool isVisible,
        Color newColor,
        MarkerTag newTag)
    {
        for (int i = 0;
             i < historialMarcadores.Count;
             i++)
        {
            if (historialMarcadores[i]
                    .markerID == markerID)
            {
                GeoMarkerData data =
                    historialMarcadores[i];

                data.isVisible =
                    isVisible;

                data.color =
                    newColor;

                data.tag =
                    newTag;

                historialMarcadores[i] =
                    data;

                UpdateMarkerClientRpc(
                    markerID,
                    isVisible,
                    newColor,
                    newTag);

                return;
            }
        }
    }

    [Rpc(SendTo.Everyone)]
    private void UpdateMarkerClientRpc(
        ulong markerID,
        bool isVisible,
        Color newColor,
        MarkerTag newTag)
    {
        if (localVisuals.TryGetValue(
            markerID,
            out GameObject obj))
        {
            GeoMarkerData temp =
                new GeoMarkerData
                {
                    markerID =
                        markerID,

                    isVisible =
                        isVisible,

                    color =
                        newColor,

                    tag =
                        newTag
                };

            AplicarEstiloVisual(
                obj,
                temp);

            OnMarkerUpdatedLocal?.Invoke(
                temp);
        }
    }

    // =========================================================
    // ELIMINACIÓN INDIVIDUAL
    // =========================================================

    public void SolicitarEliminarMarcador(
        ulong markerID)
    {
        EliminarMarcadorServerRpc(
            markerID);
    }

    [Rpc(
        SendTo.Server,
        InvokePermission =
            RpcInvokePermission.Everyone)]
    private void EliminarMarcadorServerRpc(
        ulong markerID)
    {
        int markerIndex =
            -1;

        GeoMarkerData markerAEliminar =
            default;

        // Buscar marcador por ID.
        for (int i = 0;
             i < historialMarcadores.Count;
             i++)
        {
            if (historialMarcadores[i]
                    .markerID == markerID)
            {
                markerIndex =
                    i;

                markerAEliminar =
                    historialMarcadores[i];

                break;
            }
        }

        // Puede ocurrir si dos solicitudes intentan
        // eliminar el mismo marcador.
        if (markerIndex < 0)
        {
            return;
        }

        // Si es un lazo, también deben eliminarse
        // sus puntos del Flat Buffer.
        if (markerAEliminar.type ==
            MarkerType.Lasso)
        {
            EliminarPuntosDelLazo(
                markerAEliminar);
        }

        // Eliminar metadata del marcador.
        historialMarcadores.RemoveAt(
            markerIndex);

        // Ordenar a todos los clientes que eliminen
        // la representación local.
        EliminarMarcadorClientRpc(
            markerID,
            markerAEliminar.type);
    }

    // =========================================================
    // COMPACTACIÓN DEL FLAT BUFFER DE LAZOS
    // =========================================================

    private void EliminarPuntosDelLazo(
        GeoMarkerData markerAEliminar)
    {
        int inicio =
            markerAEliminar.lassoStartIndex;

        int cantidad =
            markerAEliminar.lassoPointCount;

        if (cantidad <= 0)
        {
            return;
        }

        if (inicio < 0 ||
            inicio + cantidad >
                historialPuntosLazo.Count)
        {
            Debug.LogError(
                "[InteractionManager] " +
                $"No fue posible eliminar los puntos del lazo " +
                $"{markerAEliminar.markerID}: rango inválido.");

            return;
        }

        // Eliminar desde el último elemento hacia el primero
        // para mantener índices válidos durante la eliminación.
        for (int i =
             inicio + cantidad - 1;
             i >= inicio;
             i--)
        {
            historialPuntosLazo.RemoveAt(i);
        }

        // Los lazos ubicados después del bloque eliminado
        // deben desplazar su índice inicial.
        for (int i = 0;
             i < historialMarcadores.Count;
             i++)
        {
            GeoMarkerData data =
                historialMarcadores[i];

            // Ignorar el lazo que estamos eliminando.
            if (data.markerID ==
                markerAEliminar.markerID)
            {
                continue;
            }

            if (data.type == MarkerType.Lasso &&
                data.lassoStartIndex > inicio)
            {
                data.lassoStartIndex -=
                    cantidad;

                historialMarcadores[i] =
                    data;
            }
        }
    }

    // =========================================================
    // ELIMINACIÓN VISUAL EN CLIENTES
    // =========================================================

    [Rpc(SendTo.Everyone)]
    private void EliminarMarcadorClientRpc(
        ulong markerID,
        MarkerType markerType)
    {
        if (localVisuals.TryGetValue(
            markerID,
            out GameObject obj))
        {
            // Si corresponde a un POI, retirar también
            // su referencia de POIPlacementSystem.
            if (markerType == MarkerType.POI &&
                poiSystem != null)
            {
                poiSystem.UnregisterPOI(
                    obj);
            }

            if (obj != null)
            {
                Destroy(obj);
            }

            localVisuals.Remove(
                markerID);
        }

        // La fila se elimina aunque el objeto visual
        // ya no estuviera disponible.
        OnMarkerDeletedLocal?.Invoke(
            markerID);
    }

    // =========================================================
    // ESTILO VISUAL
    // =========================================================

    private void AplicarEstiloVisual(
        GameObject obj,
        GeoMarkerData data)
    {
        if (obj == null)
            return;

        obj.SetActive(
            data.isVisible);

        LineRenderer lr =
            obj.GetComponent<
                LineRenderer>();

        if (lr != null)
        {
            lr.startColor =
                data.color;

            lr.endColor =
                data.color;

            if (lr.material != null)
            {
                lr.material.color =
                    data.color;
            }
        }
        else
        {
            MeshRenderer renderer =
                obj.GetComponentInChildren<
                    MeshRenderer>();

            if (renderer != null &&
                renderer.material != null)
            {
                renderer.material.SetColor(
                    "_BaseColor",
                    data.color);
            }
        }
    }

    // =========================================================
    // REFRESCO UI
    // =========================================================

    public void RefrescarUIExistente()
    {
        foreach (GeoMarkerData marker
                 in historialMarcadores)
        {
            OnMarkerAddedLocal?.Invoke(
                marker);
        }
    }

    // =========================================================
    // RESET GLOBAL
    // =========================================================

    public void ResetEnvironment()
    {
        SolicitarResetRpc();
    }

    [Rpc(
        SendTo.Server,
        InvokePermission =
            RpcInvokePermission.Everyone)]
    private void SolicitarResetRpc()
    {
        historialMarcadores.Clear();
        historialPuntosLazo.Clear();

        ResetRpc();
    }

    [Rpc(SendTo.Everyone)]
    private void ResetRpc()
    {
        CancelarInteraccionDeMarcado();

        if (poiSystem != null)
        {
            poiSystem.ClearAllPOIs();
        }

        if (contenedorTerreno != null)
        {
            // Copiar primero los objetos a destruir para
            // evitar problemas al modificar la jerarquía
            // mientras se itera sobre ella.
            List<GameObject> lazosAEliminar =
                new List<GameObject>();

            foreach (Transform child
                     in contenedorTerreno)
            {
                if (child.name.StartsWith(
                    "Lazo_Network_"))
                {
                    lazosAEliminar.Add(
                        child.gameObject);
                }
            }

            foreach (GameObject lazo
                     in lazosAEliminar)
            {
                if (lazo != null)
                {
                    Destroy(lazo);
                }
            }
        }

        localVisuals.Clear();

        OnEnvironmentReset?.Invoke();
    }
}
