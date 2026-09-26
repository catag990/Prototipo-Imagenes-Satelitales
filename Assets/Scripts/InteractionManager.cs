using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public enum ToolMode { POI, Lasso }

public class InteractionManager : NetworkBehaviour
{
    // =========================================================
    // HERRAMIENTAS
    // =========================================================
    [Header("Herramientas")]
    public ToolMode currentTool = ToolMode.POI;
    public POIPlacementSystem poiSystem;
    public LassoTool lassoTool;

    // =========================================================
    // CONTROL DEL TERRENO
    // =========================================================
    [Header("Control de Terreno")]
    public NetworkTerrainSync terrainSync;
    public XRGrabInteractable terrenoInteractable;

    // =========================================================
    // ENTORNO
    // =========================================================
    [Header("Prefabs y Entorno")]
    public GameObject flagPrefab;
    public Transform contenedorTerreno;

    // =========================================================
    // ESTADO LOCAL Y SELECCIÓN
    // =========================================================
    private bool yaPuseUnPOI = false;
    private bool isLassoDrawing = false;
    private bool errorTerrainSyncReportado = false;

    [Header("Estado de Selección")]
    [Tooltip("El ID del marcador actualmente seleccionado con el gatillo izquierdo")]
    public ulong marcadorSeleccionadoID = ulong.MaxValue;

    // =========================================================
    // ESTADO DE RED
    // =========================================================
    private NetworkList<GeoMarkerData> historialMarcadores;
    private NetworkList<Vector3> historialPuntosLazo;

    private ulong nextMarkerID = 0;
    private int nextMarkerNumber = 1;

    // =========================================================
    // REPRESENTACIÓN LOCAL
    // =========================================================
    private Dictionary<ulong, GameObject> localVisuals = new Dictionary<ulong, GameObject>();

    // =========================================================
    // EVENTOS DE UI
    // =========================================================
    public System.Action<GeoMarkerData> OnMarkerAddedLocal;
    public System.Action<GeoMarkerData> OnMarkerUpdatedLocal;
    public System.Action<ulong> OnMarkerDeletedLocal;
    public System.Action OnEnvironmentReset;
    public System.Action<ToolMode> OnToolChanged;

    // =========================================================
    // AWAKE
    // =========================================================
    private void Awake()
    {
        historialMarcadores = new NetworkList<GeoMarkerData>();
        historialPuntosLazo = new NetworkList<Vector3>();
        ResolverReferenciasTerreno();
    }

    private void ResolverReferenciasTerreno()
    {
        if (contenedorTerreno == null) return;

        if (terrainSync == null)
        {
            terrainSync = contenedorTerreno.GetComponent<NetworkTerrainSync>();
            if (terrainSync == null)
            {
                terrainSync = contenedorTerreno.GetComponentInParent<NetworkTerrainSync>();
            }
        }

        if (terrenoInteractable == null)
        {
            terrenoInteractable = contenedorTerreno.GetComponent<XRGrabInteractable>();
            if (terrenoInteractable == null)
            {
                terrenoInteractable = contenedorTerreno.GetComponentInParent<XRGrabInteractable>();
            }
        }
    }

    // =========================================================
    // CONTROL GLOBAL DE MARCACIÓN
    // =========================================================
    private bool EstaBloqueadaLaMarcacion()
    {
        if (terrainSync == null)
        {
            if (!errorTerrainSyncReportado)
            {
                Debug.LogError("[InteractionManager] terrainSync no está asignado. La creación de POIs y lazos permanece bloqueada.");
                errorTerrainSyncReportado = true;
            }
            return true;
        }

        if (terrenoInteractable != null && terrenoInteractable.isSelected) return true;
        if (terrainSync.IsTerrainBeingManipulated) return true;

        return false;
    }

    private void OnTerrainManipulationChanged(bool estaManipulando)
    {
        if (!estaManipulando) return;
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

    public void CancelarMarcacionPorUI()
    {
        CancelarInteraccionDeMarcado();
    }

    // =========================================================
    // NETWORK SPAWN
    // =========================================================
    public override void OnNetworkSpawn()
    {
        if (terrainSync != null)
        {
            terrainSync.OnTerrainManipulationStateChanged += OnTerrainManipulationChanged;
        }

        if (IsServer)
        {
            bool existeMarcador = false;
            ulong maxMarkerID = 0;

            foreach (GeoMarkerData marker in historialMarcadores)
            {
                if (!existeMarcador || marker.markerID > maxMarkerID)
                {
                    maxMarkerID = marker.markerID;
                    existeMarcador = true;
                }
            }

            nextMarkerID = existeMarcador ? maxMarkerID + 1 : 0;
        }

        foreach (GeoMarkerData marker in historialMarcadores)
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
            terrainSync.OnTerrainManipulationStateChanged -= OnTerrainManipulationChanged;
        }
    }

    // =========================================================
    // SELECCIÓN DE HERRAMIENTA
    // =========================================================
    public void SetToolPOI()
    {
        currentTool = ToolMode.POI;
        OnToolChanged?.Invoke(currentTool);
    }

    public void SetToolLasso()
    {
        currentTool = ToolMode.Lasso;
        OnToolChanged?.Invoke(currentTool);
    }

    // =========================================================
    // SELECCIÓN 3D Y CAMBIO DE TAG (PUNTO 2)
    // =========================================================
    public void SeleccionarMarcador(ulong markerID)
    {
        marcadorSeleccionadoID = markerID;
        Debug.Log($"[InteractionManager] Objeto 3D seleccionado con ID: {markerID}");
    }

    public void CambiarTagMarcadorSeleccionado()
    {
        if (marcadorSeleccionadoID == ulong.MaxValue)
        {
            Debug.LogWarning("[InteractionManager] No hay ningún marcador seleccionado para cambiar el Tag.");
            return;
        }

        CiclarTagServerRpc(marcadorSeleccionadoID);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void CiclarTagServerRpc(ulong markerID)
    {
        bool encontrado = false;
        GeoMarkerData data = default;
        int index = -1;

        for (int i = 0; i < historialMarcadores.Count; i++)
        {
            if (historialMarcadores[i].markerID == markerID)
            {
                data = historialMarcadores[i];
                index = i;
                encontrado = true;
                break;
            }
        }

        if (!encontrado) return;

        int nextTag = ((int)data.tag + 1) % 4;
        MarkerTag nuevoTag = (MarkerTag)nextTag;

        Color nuevoColor = Color.white;
        switch (nuevoTag)
        {
            case MarkerTag.Generico: nuevoColor = Color.white; break;
            case MarkerTag.Riesgo: nuevoColor = Color.red; break;
            case MarkerTag.Agua: nuevoColor = Color.blue; break;
            case MarkerTag.Alerta: nuevoColor = Color.yellow; break;
        }

        data.tag = nuevoTag;
        data.color = nuevoColor;
        historialMarcadores[index] = data;

        UpdateMarkerClientRpc(markerID, data.isVisible, nuevoColor, nuevoTag);
    }

    // =========================================================
    // INPUT DE DIBUJO/MARCADO
    // =========================================================
    public void ProcesarEntrada(RaycastHit hit, bool estaPresionado)
    {
        if (EstaBloqueadaLaMarcacion())
        {
            CancelarInteraccionDeMarcado();
            return;
        }

        if (estaPresionado)
        {
            if (currentTool == ToolMode.Lasso && hit.collider != null)
            {
                if (!isLassoDrawing)
                {
                    lassoTool.IniciarLazo(hit.point);
                    isLassoDrawing = true;
                }
                else
                {
                    lassoTool.ActualizarLazo(hit.point);
                }
            }
            else if (currentTool == ToolMode.POI && !yaPuseUnPOI && contenedorTerreno != null && hit.collider != null)
            {
                Vector3 posicionLocal = contenedorTerreno.InverseTransformPoint(hit.point);
                Vector3 normalLocal = contenedorTerreno.InverseTransformDirection(hit.normal);
                RegistrarPOIServerRpc(posicionLocal, normalLocal);
                yaPuseUnPOI = true;
            }
        }
        else
        {
            yaPuseUnPOI = false;
            if (isLassoDrawing)
            {
                if (EstaBloqueadaLaMarcacion())
                {
                    CancelarInteraccionDeMarcado();
                    return;
                }

                Vector3[] puntosMundo = lassoTool.TerminarLazo();
                isLassoDrawing = false;

                if (puntosMundo.Length > 1 && contenedorTerreno != null)
                {
                    Vector3[] puntosLocales = new Vector3[puntosMundo.Length];
                    for (int i = 0; i < puntosMundo.Length; i++)
                    {
                        puntosLocales[i] = contenedorTerreno.InverseTransformPoint(puntosMundo[i]);
                    }
                    RegistrarLazoServerRpc(puntosLocales);
                }
            }
        }
    }

    // =========================================================
    // REGISTRAR POI Y LAZO
    // =========================================================
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RegistrarPOIServerRpc(Vector3 posLocal, Vector3 normLocal)
    {
        if (terrainSync == null || terrainSync.IsTerrainLockedByNetwork) return;

        GeoMarkerData nuevoPOI = new GeoMarkerData
        {
            markerID = nextMarkerID++,
            markerNumber = nextMarkerNumber++,
            type = MarkerType.POI,
            position = posLocal,
            normal = normLocal,
            isVisible = true,
            color = Color.white,
            tag = MarkerTag.Generico
        };

        historialMarcadores.Add(nuevoPOI);
        DibujarPOIRpc(nuevoPOI);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RegistrarLazoServerRpc(Vector3[] puntosLocales)
    {
        if (terrainSync == null || terrainSync.IsTerrainLockedByNetwork) return;
        if (puntosLocales == null || puntosLocales.Length <= 1) return;

        int startIdx = historialPuntosLazo.Count;

        foreach (Vector3 punto in puntosLocales)
        {
            historialPuntosLazo.Add(punto);
        }

        GeoMarkerData nuevoLazo = new GeoMarkerData
        {
            markerID = nextMarkerID++,
            markerNumber = nextMarkerNumber++,
            type = MarkerType.Lasso,
            lassoStartIndex = startIdx,
            lassoPointCount = puntosLocales.Length,
            isVisible = true,
            color = Color.white,
            tag = MarkerTag.Generico
        };

        historialMarcadores.Add(nuevoLazo);
        DibujarLazoRpc(nuevoLazo, puntosLocales);
    }

    // =========================================================
    // PROPAGACIÓN VISUAL Y COLISIONADORES (PUNTO 2)
    // =========================================================
    [Rpc(SendTo.Everyone)]
    private void DibujarPOIRpc(GeoMarkerData data)
    {
        ReconstruirPOI(data);
    }

    [Rpc(SendTo.Everyone)]
    private void DibujarLazoRpc(GeoMarkerData data, Vector3[] puntosLocales)
    {
        ReconstruirLazo(data, puntosLocales);
    }

    private void ReconstruirPOI(GeoMarkerData data)
    {
        if (contenedorTerreno == null || flagPrefab == null) return;

        Vector3 posicionMundo = contenedorTerreno.TransformPoint(data.position);
        Quaternion rotacion = Quaternion.FromToRotation(Vector3.up, contenedorTerreno.TransformDirection(data.normal));

        GameObject nuevaBandera = Instantiate(flagPrefab, posicionMundo, rotacion);
        nuevaBandera.transform.SetParent(contenedorTerreno, true);

        MarkerIdentity mi = nuevaBandera.AddComponent<MarkerIdentity>();
        mi.markerID = data.markerID;

        MarkerNumber3D text3D = nuevaBandera.GetComponent<MarkerNumber3D>();
        if (text3D != null)
        {
            text3D.SetNumber(data.markerNumber, data.color);
        }

        if (poiSystem != null)
        {
            poiSystem.RegisterPOI(nuevaBandera);
        }

        localVisuals[data.markerID] = nuevaBandera;
        AplicarEstiloVisual(nuevaBandera, data);
        OnMarkerAddedLocal?.Invoke(data);
    }

    private void ReconstruirLazo(GeoMarkerData data, Vector3[] puntosLocales)
    {
        if (contenedorTerreno == null || lassoTool == null || puntosLocales == null) return;

        GameObject lineaObj = new GameObject($"Lazo_Network_{data.markerID}");
        lineaObj.transform.SetParent(contenedorTerreno, false);

        LineRenderer lr = lineaObj.AddComponent<LineRenderer>();
        lr.material = lassoTool.materialLinea;
        lr.startWidth = lassoTool.anchoLinea;
        lr.endWidth = lassoTool.anchoLinea;
        lr.useWorldSpace = false;
        lr.positionCount = puntosLocales.Length;
        lr.SetPositions(puntosLocales);

        MarkerIdentity mi = lineaObj.AddComponent<MarkerIdentity>();
        mi.markerID = data.markerID;
        
        MeshCollider mc = lineaObj.AddComponent<MeshCollider>();
        Mesh colliderMesh = new Mesh();
        lr.BakeMesh(colliderMesh, true);
        mc.sharedMesh = colliderMesh;

        localVisuals[data.markerID] = lineaObj;
        AplicarEstiloVisual(lineaObj, data);
        OnMarkerAddedLocal?.Invoke(data);
    }

    private void ReconstruirLazoDesdeHistorial(GeoMarkerData data)
    {
        if (data.lassoStartIndex < 0 || data.lassoPointCount <= 0 || data.lassoStartIndex + data.lassoPointCount > historialPuntosLazo.Count) return;

        Vector3[] puntosExtraidos = new Vector3[data.lassoPointCount];
        for (int i = 0; i < data.lassoPointCount; i++)
        {
            puntosExtraidos[i] = historialPuntosLazo[data.lassoStartIndex + i];
        }

        ReconstruirLazo(data, puntosExtraidos);
    }

    // =========================================================
    // MODIFICACIÓN Y ELIMINACIÓN
    // =========================================================
    public void SolicitarCambioMarcador(ulong markerID, bool isVisible, Color newColor, MarkerTag newTag)
    {
        UpdateMarkerServerRpc(markerID, isVisible, newColor, newTag);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void UpdateMarkerServerRpc(ulong markerID, bool isVisible, Color newColor, MarkerTag newTag)
    {
        bool encontrado = false;
        for (int i = 0; i < historialMarcadores.Count; i++)
        {
            if (historialMarcadores[i].markerID != markerID) continue;

            GeoMarkerData data = historialMarcadores[i];
            data.isVisible = isVisible;
            data.color = newColor;
            data.tag = newTag;
            historialMarcadores[i] = data;
            encontrado = true;
            break;
        }

        if (!encontrado) return;
        UpdateMarkerClientRpc(markerID, isVisible, newColor, newTag);
    }

    [Rpc(SendTo.Everyone)]
    private void UpdateMarkerClientRpc(ulong markerID, bool isVisible, Color newColor, MarkerTag newTag)
    {
        if (localVisuals.TryGetValue(markerID, out GameObject obj))
        {
            GeoMarkerData temp = new GeoMarkerData
            {
                markerID = markerID,
                isVisible = isVisible,
                color = newColor,
                tag = newTag
            };

            for (int i = 0; i < historialMarcadores.Count; i++)
            {
                if (historialMarcadores[i].markerID == markerID)
                {
                    temp.markerNumber = historialMarcadores[i].markerNumber;
                    break;
                }
            }

            MarkerNumber3D text3D = obj.GetComponent<MarkerNumber3D>();
            if (text3D != null)
            {
                text3D.SetNumber(temp.markerNumber, newColor);
            }

            AplicarEstiloVisual(obj, temp);
            OnMarkerUpdatedLocal?.Invoke(temp);
        }
    }

    public void SolicitarEliminarMarcador(ulong markerID)
    {
        EliminarMarcadorServerRpc(markerID);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void EliminarMarcadorServerRpc(ulong markerID)
    {
        int markerIndex = -1;
        GeoMarkerData markerAEliminar = default;

        for (int i = 0; i < historialMarcadores.Count; i++)
        {
            if (historialMarcadores[i].markerID == markerID)
            {
                markerIndex = i;
                markerAEliminar = historialMarcadores[i];
                break;
            }
        }

        if (markerIndex < 0) return;

        if (markerAEliminar.type == MarkerType.Lasso)
        {
            int startIndex = markerAEliminar.lassoStartIndex;
            int pointCount = markerAEliminar.lassoPointCount;
            
            for (int i = pointCount - 1; i >= 0; i--)
            {
                historialPuntosLazo.RemoveAt(startIndex + i);
            }

            for (int i = 0; i < historialMarcadores.Count; i++)
            {
                GeoMarkerData data = historialMarcadores[i];
                if (data.markerID == markerID) continue;
                if (data.type != MarkerType.Lasso) continue;

                if (data.lassoStartIndex > startIndex)
                {
                    data.lassoStartIndex -= pointCount;
                    historialMarcadores[i] = data;
                }
            }
        }

        MarkerType tipoEliminado = markerAEliminar.type;
        historialMarcadores.RemoveAt(markerIndex);
        EliminarMarcadorClientRpc(markerID, tipoEliminado);
    }

    [Rpc(SendTo.Everyone)]
    private void EliminarMarcadorClientRpc(ulong markerID, MarkerType markerType)
    {
        if (localVisuals.TryGetValue(markerID, out GameObject obj))
        {
            if (markerType == MarkerType.POI && poiSystem != null)
            {
                poiSystem.UnregisterPOI(obj);
            }
            if (obj != null) Destroy(obj);
            localVisuals.Remove(markerID);
            
            if (marcadorSeleccionadoID == markerID) marcadorSeleccionadoID = ulong.MaxValue;
        }
        OnMarkerDeletedLocal?.Invoke(markerID);
    }

    private void AplicarEstiloVisual(GameObject obj, GeoMarkerData data)
    {
        if (obj == null) return;
        obj.SetActive(data.isVisible);

        LineRenderer lr = obj.GetComponent<LineRenderer>();
        if (lr != null)
        {
            lr.startColor = data.color;
            lr.endColor = data.color;
            if (lr.material != null)
            {
                lr.material.color = data.color;
            }
        }
        else
        {
            MeshRenderer renderer = obj.GetComponentInChildren<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material.SetColor("_BaseColor", data.color);
            }
        }
    }

    public void RefrescarUIExistente()
    {
        foreach (GeoMarkerData marker in historialMarcadores)
        {
            OnMarkerAddedLocal?.Invoke(marker);
        }
    }

    public void ResetEnvironment()
    {
        SolicitarResetRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SolicitarResetRpc()
    {
        historialMarcadores.Clear();
        historialPuntosLazo.Clear();
        nextMarkerNumber = 1;
        ResetRpc();
    }

    [Rpc(SendTo.Everyone)]
    private void ResetRpc()
    {
        CancelarInteraccionDeMarcado();
        if (poiSystem != null) poiSystem.ClearAllPOIs();

        if (contenedorTerreno != null)
        {
            foreach (Transform child in contenedorTerreno)
            {
                if (child.name.StartsWith("Lazo_Network_"))
                {
                    Destroy(child.gameObject);
                }
            }
        }
        localVisuals.Clear();
        marcadorSeleccionadoID = ulong.MaxValue;
        OnEnvironmentReset?.Invoke();
    }
}