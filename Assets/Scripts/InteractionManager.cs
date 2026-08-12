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

    // =========================================================
    // EVENTOS DE UI
    // =========================================================

    public System.Action<GeoMarkerData> OnMarkerAddedLocal;
    public System.Action<GeoMarkerData> OnMarkerUpdatedLocal;
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

        // Intentar resolver referencias automáticamente
        // si no fueron asignadas desde el Inspector.
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
    // CONTROL GLOBAL DE MARCACIÓN
    // =========================================================

    private bool EstaBloqueadaLaMarcacion()
    {
        // Fallar cerrado:
        // si no existe NetworkTerrainSync no se permite marcar.
        if (terrainSync == null)
        {
            if (!errorTerrainSyncReportado)
            {
                Debug.LogError(
                    "[InteractionManager] terrainSync no está asignado. " +
                    "La creación de POIs y lazos permanece bloqueada.");
                
                errorTerrainSyncReportado = true;
            }

            return true;
        }

        // Bloqueo local inmediato.
        //
        // Si ESTE cliente tiene agarrado el terreno,
        // no se espera ninguna actualización de red:
        // queda impedido de marcar inmediatamente.
        if (terrenoInteractable != null &&
            terrenoInteractable.isSelected)
        {
            return true;
        }

        // Incluye:
        // 1. manipulación de este mismo cliente;
        // 2. manipulación de cualquier otro cliente.
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

        // Si alguien comenzó a mover el terreno mientras
        // este cliente estaba dibujando, se cancela el trazo.
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
                "[InteractionManager] No existe referencia " +
                "a NetworkTerrainSync.");
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
    // SELECCIÓN DE HERRAMIENTA
    // =========================================================

    public void SetToolPOI()
    {
        currentTool = ToolMode.POI;
    }

    public void SetToolLasso()
    {
        currentTool = ToolMode.Lasso;
    }

    // =========================================================
    // ENTRADA DEL CONTROLADOR
    // =========================================================

    public void ProcesarEntrada(
        RaycastHit hit,
        bool estaPresionado)
    {
        // -----------------------------------------------------
        // PRIMERA BARRERA:
        // bloqueo inmediato antes de procesar cualquier input.
        // -----------------------------------------------------

        if (EstaBloqueadaLaMarcacion())
        {
            CancelarInteraccionDeMarcado();
            return;
        }

        // -----------------------------------------------------
        // INPUT PRESIONADO
        // -----------------------------------------------------

        if (estaPresionado)
        {
            // =========================
            // LAZO
            // =========================

            if (currentTool == ToolMode.Lasso &&
                hit.collider != null)
            {
                if (!isLassoDrawing)
                {
                    lassoTool.IniciarLazo(
                        hit.point);

                    isLassoDrawing = true;
                }
                else
                {
                    lassoTool.ActualizarLazo(
                        hit.point);
                }
            }

            // =========================
            // POI
            // =========================

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

                yaPuseUnPOI = true;
            }
        }

        // -----------------------------------------------------
        // INPUT LIBERADO
        // -----------------------------------------------------

        else
        {
            yaPuseUnPOI = false;

            if (isLassoDrawing)
            {
                // Segunda comprobación.
                //
                // Evita finalizar el lazo si el terreno comenzó
                // a ser manipulado justo antes de soltar
                // el gatillo.
                if (EstaBloqueadaLaMarcacion())
                {
                    CancelarInteraccionDeMarcado();
                    return;
                }

                Vector3[] puntosMundo =
                    lassoTool.TerminarLazo();

                isLassoDrawing = false;

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
    // REGISTRO DE POI
    // =========================================================

    [Rpc(
        SendTo.Server,
        InvokePermission =
            RpcInvokePermission.Everyone)]
    private void RegistrarPOIServerRpc(
        Vector3 posLocal,
        Vector3 normLocal)
    {
        // -----------------------------------------------------
        // SEGUNDA BARRERA:
        // validación autoritativa en servidor.
        // -----------------------------------------------------

        if (terrainSync == null)
        {
            Debug.LogWarning(
                "[InteractionManager] POI rechazado: " +
                "terrainSync no está disponible.");

            return;
        }

        if (terrainSync.IsTerrainLockedByNetwork)
        {
            Debug.Log(
                "[InteractionManager] POI rechazado: " +
                "el terreno está siendo manipulado.");

            return;
        }

        GeoMarkerData nuevoPOI =
            new GeoMarkerData
            {
                markerID =
                    (ulong)historialMarcadores.Count,

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
    // REGISTRO DE LAZO
    // =========================================================

    [Rpc(
        SendTo.Server,
        InvokePermission =
            RpcInvokePermission.Everyone)]
    private void RegistrarLazoServerRpc(
        Vector3[] puntosLocales)
    {
        // -----------------------------------------------------
        // SEGUNDA BARRERA:
        // validación autoritativa en servidor.
        // -----------------------------------------------------

        if (terrainSync == null)
        {
            Debug.LogWarning(
                "[InteractionManager] Lazo rechazado: " +
                "terrainSync no está disponible.");

            return;
        }

        if (terrainSync.IsTerrainLockedByNetwork)
        {
            Debug.Log(
                "[InteractionManager] Lazo rechazado: " +
                "el terreno está siendo manipulado.");

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
                    (ulong)historialMarcadores.Count,

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
    // RECONSTRUCCIÓN POI
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
    // RECONSTRUCCIÓN LAZO
    // =========================================================

    private void ReconstruirLazo(
        GeoMarkerData data,
        Vector3[] puntosLocales)
    {
        if (contenedorTerreno == null)
            return;

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
                indice >=
                    historialPuntosLazo.Count)
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
        bool encontrado =
            false;

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

                encontrado =
                    true;

                break;
            }
        }

        if (!encontrado)
            return;

        UpdateMarkerClientRpc(
            markerID,
            isVisible,
            newColor,
            newTag);
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
    // UI
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
    // RESET GENERAL
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
            foreach (Transform child
                     in contenedorTerreno)
            {
                if (child.name.StartsWith(
                    "Lazo_Network_"))
                {
                    Destroy(
                        child.gameObject);
                }
            }
        }

        localVisuals.Clear();

        OnEnvironmentReset?.Invoke();
    }
}
