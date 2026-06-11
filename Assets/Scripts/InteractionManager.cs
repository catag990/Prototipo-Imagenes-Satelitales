using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode; 

public enum ToolMode { POI, Paint }

// Estructura contenedora para los vectores del POI (Requerida para NetworkList)
public struct POIData : INetworkSerializable, System.IEquatable<POIData>
{
    public Vector3 localPos;
    public Vector3 localNormal;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref localPos);
        serializer.SerializeValue(ref localNormal);
    }

    public bool Equals(POIData other) => localPos == other.localPos && localNormal == other.localNormal;
}

public class InteractionManager : NetworkBehaviour
{
    public ToolMode currentTool = ToolMode.POI;
    public POIPlacementSystem poiSystem;
    public TerrainPainter painter;
    
    [Header("Prefabs y Entorno")]
    public GameObject flagPrefab;
    public Transform contenedorTerreno; 
    
    private bool yaPuseUnPOI = false;

    // --- LISTAS DE ESTADO PERSISTENTE ---
    private NetworkList<POIData> historialPOIs;
    private NetworkList<Vector2> historialPintura;

    void Awake()
    {
        historialPOIs = new NetworkList<POIData>();
        historialPintura = new NetworkList<Vector2>();
    }

    public override void OnNetworkSpawn()
    {
        historialPOIs.OnListChanged += OnPOIListChanged;
        historialPintura.OnListChanged += OnPinturaListChanged;

        // RECUPERACIÓN HISTÓRICA (El Late-Joiner dibuja lo que ya estaba)
        if (!IsServer)
        {
            foreach (POIData poi in historialPOIs)
            {
                ReconstruirPOIEnMundo(poi.localPos, poi.localNormal);
            }
            foreach (Vector2 uv in historialPintura)
            {
                painter.PaintAt(uv);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        historialPOIs.OnListChanged -= OnPOIListChanged;
        historialPintura.OnListChanged -= OnPinturaListChanged;
    }

    public void SetToolPOI() => currentTool = ToolMode.POI;
    public void SetToolPaint() => currentTool = ToolMode.Paint;

    public void ProcesarEntrada(RaycastHit hit, bool estaPresionado)
    {
        if (estaPresionado)
        {
            if (currentTool == ToolMode.Paint)
            {
                RegistrarPinturaRpc(hit.textureCoord);
            }
            else if (currentTool == ToolMode.POI)
            {
                if (!yaPuseUnPOI && contenedorTerreno != null)
                {
                    Vector3 posicionLocal = contenedorTerreno.InverseTransformPoint(hit.point);
                    Vector3 normalLocal = contenedorTerreno.InverseTransformDirection(hit.normal);
                    
                    RegistrarPOIRpc(posicionLocal, normalLocal);
                    yaPuseUnPOI = true;
                }
            }
        }
        else
        {
            yaPuseUnPOI = false; 
        }
    }

    // --- MENSAJERÍA RPC (SINTAXIS MODERNIZADA) ---

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RegistrarPinturaRpc(Vector2 uvCoords)
    {
        historialPintura.Add(uvCoords);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RegistrarPOIRpc(Vector3 posLocal, Vector3 normLocal)
    {
        POIData nuevoPOI = new POIData { localPos = posLocal, localNormal = normLocal };
        historialPOIs.Add(nuevoPOI);
    }

    // --- ESCUCHADORES DE RED ---

    private void OnPOIListChanged(NetworkListEvent<POIData> changeEvent)
    {
        if (changeEvent.Type == NetworkListEvent<POIData>.EventType.Add)
        {
            ReconstruirPOIEnMundo(changeEvent.Value.localPos, changeEvent.Value.localNormal);
        }
    }

    private void OnPinturaListChanged(NetworkListEvent<Vector2> changeEvent)
    {
        if (changeEvent.Type == NetworkListEvent<Vector2>.EventType.Add)
        {
            painter.PaintAt(changeEvent.Value);
        }
    }

    // --- DIBUJADO LOCAL ---

    private void ReconstruirPOIEnMundo(Vector3 posLocal, Vector3 normLocal)
    {
        if (contenedorTerreno == null) return;

        Vector3 posicionMundo = contenedorTerreno.TransformPoint(posLocal);
        Vector3 normalMundo = contenedorTerreno.TransformDirection(normLocal);

        Quaternion rotacionPerpendicular = Quaternion.FromToRotation(Vector3.up, normalMundo);
        GameObject nuevaBandera = Instantiate(flagPrefab, posicionMundo, rotacionPerpendicular);

        nuevaBandera.transform.SetParent(contenedorTerreno, true);

        MeshRenderer renderer = nuevaBandera.GetComponentInChildren<MeshRenderer>();
        if (renderer != null) renderer.material.SetColor("_BaseColor", Color.red); 

        poiSystem.RegisterPOI(nuevaBandera);
    }

    // --- RESETEO GLOBAL ---
    public void ResetEnvironment()
    {
        SolicitarResetRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SolicitarResetRpc()
    {
        historialPOIs.Clear();
        historialPintura.Clear();
        ResetRpc();
    }

    [Rpc(SendTo.Everyone)]
    private void ResetRpc()
    {
        poiSystem.ClearAllPOIs();
        painter.ResetTexture();
        Debug.Log("Entorno reseteado colaborativamente.");
    }
}