using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode; 

public enum ToolMode { POI, Lasso } 

public class InteractionManager : NetworkBehaviour
{
    public ToolMode currentTool = ToolMode.POI;
    public POIPlacementSystem poiSystem;
    public LassoTool lassoTool; 
    
    [Header("Prefabs y Entorno")]
    public GameObject flagPrefab;
    public Transform contenedorTerreno; 
    
    private bool yaPuseUnPOI = false;
    private bool isLassoDrawing = false; 

    private NetworkList<GeoMarkerData> historialMarcadores;
    private NetworkList<Vector3> historialPuntosLazo;

    private Dictionary<ulong, GameObject> localVisuals = new Dictionary<ulong, GameObject>();
    
    // --- EVENTOS DE UI (NUEVO) ---
    public System.Action<GeoMarkerData> OnMarkerAddedLocal;
    public System.Action<GeoMarkerData> OnMarkerUpdatedLocal; // Avisa si se editó
    public System.Action OnEnvironmentReset; // Avisa si se borró todo

    void Awake()
    {
        historialMarcadores = new NetworkList<GeoMarkerData>();
        historialPuntosLazo = new NetworkList<Vector3>();
    }

    public override void OnNetworkSpawn()
    {
        foreach (GeoMarkerData marker in historialMarcadores)
        {
            if (marker.type == MarkerType.POI) ReconstruirPOI(marker);
            else if (marker.type == MarkerType.Lasso) ReconstruirLazoDesdeHistorial(marker);
        }
    }

    public void SetToolPOI() => currentTool = ToolMode.POI;
    public void SetToolLasso() => currentTool = ToolMode.Lasso;

    public void ProcesarEntrada(RaycastHit hit, bool estaPresionado)
    {
        if (estaPresionado)
        {
            if (currentTool == ToolMode.Lasso && hit.collider != null)
            {
                if (!isLassoDrawing) { lassoTool.IniciarLazo(hit.point); isLassoDrawing = true; }
                else { lassoTool.ActualizarLazo(hit.point); }
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
                Vector3[] puntosMundo = lassoTool.TerminarLazo();
                isLassoDrawing = false;

                if (puntosMundo.Length > 1 && contenedorTerreno != null)
                {
                    Vector3[] puntosLocales = new Vector3[puntosMundo.Length];
                    for (int i = 0; i < puntosMundo.Length; i++) puntosLocales[i] = contenedorTerreno.InverseTransformPoint(puntosMundo[i]);
                    RegistrarLazoServerRpc(puntosLocales);
                }
            }
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RegistrarPOIServerRpc(Vector3 posLocal, Vector3 normLocal)
    {
        GeoMarkerData nuevoPOI = new GeoMarkerData { markerID = (ulong)historialMarcadores.Count, type = MarkerType.POI, position = posLocal, normal = normLocal, isVisible = true, color = Color.white, tag = MarkerTag.Generico };
        historialMarcadores.Add(nuevoPOI); 
        DibujarPOIRpc(nuevoPOI);           
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RegistrarLazoServerRpc(Vector3[] puntosLocales)
    {
        int startIdx = historialPuntosLazo.Count;
        foreach (Vector3 p in puntosLocales) historialPuntosLazo.Add(p); 
        
        GeoMarkerData nuevoLazo = new GeoMarkerData { markerID = (ulong)historialMarcadores.Count, type = MarkerType.Lasso, lassoStartIndex = startIdx, lassoPointCount = puntosLocales.Length, isVisible = true, color = Color.white, tag = MarkerTag.Generico };
        historialMarcadores.Add(nuevoLazo); 
        DibujarLazoRpc(nuevoLazo, puntosLocales); 
    }

    [Rpc(SendTo.Everyone)]
    private void DibujarPOIRpc(GeoMarkerData data) => ReconstruirPOI(data);

    [Rpc(SendTo.Everyone)]
    private void DibujarLazoRpc(GeoMarkerData data, Vector3[] puntosLocales) => ReconstruirLazo(data, puntosLocales);

    private void ReconstruirPOI(GeoMarkerData data)
    {
        if (contenedorTerreno == null) return;
        Vector3 posicionMundo = contenedorTerreno.TransformPoint(data.position);
        GameObject nuevaBandera = Instantiate(flagPrefab, posicionMundo, Quaternion.FromToRotation(Vector3.up, contenedorTerreno.TransformDirection(data.normal)));
        nuevaBandera.transform.SetParent(contenedorTerreno, true);
        poiSystem.RegisterPOI(nuevaBandera);
        
        localVisuals[data.markerID] = nuevaBandera;
        AplicarEstiloVisual(nuevaBandera, data);
        OnMarkerAddedLocal?.Invoke(data);
    }

    private void ReconstruirLazo(GeoMarkerData data, Vector3[] puntosLocales)
    {
        if (contenedorTerreno == null) return;
        GameObject lineaObj = new GameObject($"Lazo_Network_{data.markerID}");
        lineaObj.transform.SetParent(contenedorTerreno, false);
        LineRenderer lr = lineaObj.AddComponent<LineRenderer>();
        lr.material = lassoTool.materialLinea;
        lr.startWidth = lassoTool.anchoLinea; lr.endWidth = lassoTool.anchoLinea;
        lr.useWorldSpace = false; lr.positionCount = puntosLocales.Length;
        lr.SetPositions(puntosLocales);
        
        localVisuals[data.markerID] = lineaObj;
        AplicarEstiloVisual(lineaObj, data);
        OnMarkerAddedLocal?.Invoke(data);
    }

    private void ReconstruirLazoDesdeHistorial(GeoMarkerData data)
    {
        Vector3[] puntosExtraidos = new Vector3[data.lassoPointCount];
        for (int i = 0; i < data.lassoPointCount; i++) puntosExtraidos[i] = historialPuntosLazo[data.lassoStartIndex + i];
        ReconstruirLazo(data, puntosExtraidos);
    }
    
    public void SolicitarCambioMarcador(ulong markerID, bool isVisible, Color newColor, MarkerTag newTag) => UpdateMarkerServerRpc(markerID, isVisible, newColor, newTag);

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void UpdateMarkerServerRpc(ulong markerID, bool isVisible, Color newColor, MarkerTag newTag)
    {
        for (int i = 0; i < historialMarcadores.Count; i++)
        {
            if (historialMarcadores[i].markerID == markerID)
            {
                GeoMarkerData data = historialMarcadores[i];
                data.isVisible = isVisible; data.color = newColor; data.tag = newTag;
                historialMarcadores[i] = data; break;
            }
        }
        UpdateMarkerClientRpc(markerID, isVisible, newColor, newTag);
    }

    [Rpc(SendTo.Everyone)]
    private void UpdateMarkerClientRpc(ulong markerID, bool isVisible, Color newColor, MarkerTag newTag)
    {
        if (localVisuals.TryGetValue(markerID, out GameObject obj))
        {
            GeoMarkerData temp = new GeoMarkerData { markerID = markerID, isVisible = isVisible, color = newColor, tag = newTag };
            AplicarEstiloVisual(obj, temp);
            OnMarkerUpdatedLocal?.Invoke(temp); // Dispara el evento a la UI
        }
    }

    private void AplicarEstiloVisual(GameObject obj, GeoMarkerData data)
    {
        if (obj == null) return;
        obj.SetActive(data.isVisible);
        LineRenderer lr = obj.GetComponent<LineRenderer>();
        if (lr != null) { lr.startColor = data.color; lr.endColor = data.color; lr.material.color = data.color; }
        else { MeshRenderer renderer = obj.GetComponentInChildren<MeshRenderer>(); if (renderer != null) renderer.material.SetColor("_BaseColor", data.color); }
    }

    public void RefrescarUIExistente()
    {
        foreach (GeoMarkerData marker in historialMarcadores) OnMarkerAddedLocal?.Invoke(marker);
    }

    public void ResetEnvironment() => SolicitarResetRpc();

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SolicitarResetRpc() { historialMarcadores.Clear(); historialPuntosLazo.Clear(); ResetRpc(); }

    [Rpc(SendTo.Everyone)]
    private void ResetRpc()
    {
        poiSystem.ClearAllPOIs();
        foreach (Transform child in contenedorTerreno) { if (child.name.StartsWith("Lazo_Network_")) Destroy(child.gameObject); }
        localVisuals.Clear();
        OnEnvironmentReset?.Invoke(); // Le grita a la UI que destruya sus filas
    }
}