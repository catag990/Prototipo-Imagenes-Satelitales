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

    // --- LISTAS UNIFICADAS (SOLO PARA PERSISTENCIA / LATE-JOIN) ---
    private NetworkList<GeoMarkerData> historialMarcadores;
    private NetworkList<Vector3> historialPuntosLazo;

    void Awake()
    {
        historialMarcadores = new NetworkList<GeoMarkerData>();
        historialPuntosLazo = new NetworkList<Vector3>();
    }

    public override void OnNetworkSpawn()
    {
        // LATE-JOIN: Cuando alguien entra, las listas ya se descargaron completas.
        // Reconstruimos todo sin riesgo de IndexOutOfRange.
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

    public void SetToolPOI() => currentTool = ToolMode.POI;
    public void SetToolLasso() => currentTool = ToolMode.Lasso;

    public void ProcesarEntrada(RaycastHit hit, bool estaPresionado)
    {
        if (estaPresionado)
        {
            if (currentTool == ToolMode.Lasso)
            {
                if (hit.collider != null)
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
            }
            else if (currentTool == ToolMode.POI)
            {
                if (!yaPuseUnPOI && contenedorTerreno != null && hit.collider != null)
                {
                    Vector3 posicionLocal = contenedorTerreno.InverseTransformPoint(hit.point);
                    Vector3 normalLocal = contenedorTerreno.InverseTransformDirection(hit.normal);
                    
                    RegistrarPOIServerRpc(posicionLocal, normalLocal);
                    yaPuseUnPOI = true;
                }
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
                    for (int i = 0; i < puntosMundo.Length; i++)
                    {
                        puntosLocales[i] = contenedorTerreno.InverseTransformPoint(puntosMundo[i]);
                    }
                    RegistrarLazoServerRpc(puntosLocales);
                }
            }
        }
    }

    // --- RED: GUARDAR EN SERVIDOR Y AVISAR A TODOS ---

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RegistrarPOIServerRpc(Vector3 posLocal, Vector3 normLocal)
    {
        GeoMarkerData nuevoPOI = new GeoMarkerData 
        { 
            markerID = (ulong)historialMarcadores.Count,
            type = MarkerType.POI, 
            position = posLocal, 
            normal = normLocal,
            isVisible = true,
            color = Color.red
        };
        
        historialMarcadores.Add(nuevoPOI); // Se guarda para los que entren en el futuro
        DibujarPOIRpc(nuevoPOI);           // Se dibuja AHORA para los que ya están
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RegistrarLazoServerRpc(Vector3[] puntosLocales)
    {
        int startIdx = historialPuntosLazo.Count;
        foreach (Vector3 p in puntosLocales)
        {
            historialPuntosLazo.Add(p); // Se guardan los puntos para el futuro
        }

        GeoMarkerData nuevoLazo = new GeoMarkerData 
        { 
            markerID = (ulong)historialMarcadores.Count,
            type = MarkerType.Lasso, 
            lassoStartIndex = startIdx, 
            lassoPointCount = puntosLocales.Length,
            isVisible = true,
            color = Color.yellow
        };
        
        historialMarcadores.Add(nuevoLazo); // Se guarda el metadato para el futuro
        DibujarLazoRpc(nuevoLazo, puntosLocales); // Se dibuja AHORA pasando el arreglo completo instantáneamente
    }

    // --- RED: DIBUJADO EN TIEMPO REAL (CLIENTES) ---

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

    // --- RECONSTRUCCIÓN LOCAL (VISUAL) ---

    private void ReconstruirPOI(GeoMarkerData data)
    {
        if (contenedorTerreno == null) return;

        Vector3 posicionMundo = contenedorTerreno.TransformPoint(data.position);
        Vector3 normalMundo = contenedorTerreno.TransformDirection(data.normal);
        Quaternion rotacion = Quaternion.FromToRotation(Vector3.up, normalMundo);
        
        GameObject nuevaBandera = Instantiate(flagPrefab, posicionMundo, rotacion);
        nuevaBandera.transform.SetParent(contenedorTerreno, true);
        
        MeshRenderer renderer = nuevaBandera.GetComponentInChildren<MeshRenderer>();
        if (renderer != null) renderer.material.SetColor("_BaseColor", data.color); 

        poiSystem.RegisterPOI(nuevaBandera);
    }

    private void ReconstruirLazo(GeoMarkerData data, Vector3[] puntosLocales)
    {
        if (contenedorTerreno == null) return;

        GameObject lineaObj = new GameObject($"Lazo_Network_{data.markerID}");
        lineaObj.transform.SetParent(contenedorTerreno, false);
        
        LineRenderer lr = lineaObj.AddComponent<LineRenderer>();
        lr.material = lassoTool.materialLinea;
        lr.startWidth = lassoTool.anchoLinea;
        lr.endWidth = lassoTool.anchoLinea;
        lr.useWorldSpace = false;
        
        lr.positionCount = puntosLocales.Length;
        lr.SetPositions(puntosLocales); // SetPositions es hiper-optimizado en Unity
    }

    private void ReconstruirLazoDesdeHistorial(GeoMarkerData data)
    {
        // Función exclusiva para extraer los puntos del Flat Buffer cuando un usuario entra tarde
        Vector3[] puntosExtraidos = new Vector3[data.lassoPointCount];
        for (int i = 0; i < data.lassoPointCount; i++)
        {
            puntosExtraidos[i] = historialPuntosLazo[data.lassoStartIndex + i];
        }
        ReconstruirLazo(data, puntosExtraidos);
    }

    // --- RESETEO ---
    public void ResetEnvironment() => SolicitarResetRpc();

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SolicitarResetRpc()
    {
        historialMarcadores.Clear();
        historialPuntosLazo.Clear();
        ResetRpc();
    }

    [Rpc(SendTo.Everyone)]
    private void ResetRpc()
    {
        poiSystem.ClearAllPOIs();
        foreach (Transform child in contenedorTerreno)
        {
            if (child.name.StartsWith("Lazo_Network_")) Destroy(child.gameObject);
        }
    }
}