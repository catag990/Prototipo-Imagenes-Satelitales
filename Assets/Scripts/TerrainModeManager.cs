using UnityEngine;
using Unity.Netcode;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// ¡LA MAGIA!: Obliga a este script a corregir la rotación DESPUÉS de que el sistema VR mueva las manos
[DefaultExecutionOrder(50)] 
public class TerrainModeManager : NetworkBehaviour
{
    [Header("Referencias Visuales")]
    public GameObject mesaVisual;
    public Transform puntoAnclajeMesa; 
    
    [Header("Referencias del Terreno")]
    public XRGrabInteractable terrenoInteractable;
    public NetworkTerrainSync terrainSync;

    private Rigidbody rb;
    private NetworkVariable<bool> isRotatoryMode = new NetworkVariable<bool>(false);

    void Awake()
    {
        rb = terrenoInteractable.GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        isRotatoryMode.OnValueChanged += OnModeChanged;
        AplicarModoLocal(isRotatoryMode.Value); 
    }

    public override void OnNetworkDespawn()
    {
        isRotatoryMode.OnValueChanged -= OnModeChanged;
    }

    public void ToggleMode()
    {
        ToggleModeServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ToggleModeServerRpc()
    {
        isRotatoryMode.Value = !isRotatoryMode.Value;
        
        if (isRotatoryMode.Value && puntoAnclajeMesa != null)
        {
            terrainSync.ForceSnapToTable(puntoAnclajeMesa.position, puntoAnclajeMesa.rotation);
        }
    }

    private void OnModeChanged(bool oldMode, bool newMode)
    {
        AplicarModoLocal(newMode);
    }

    private void AplicarModoLocal(bool rotatory)
    {
        if (mesaVisual != null) mesaVisual.SetActive(rotatory);

        // OBLIGATORIO: Mantenerlo Kinematic para que la malla cóncava de las montañas no crashee Unity
        rb.isKinematic = true;
        terrenoInteractable.movementType = XRBaseInteractable.MovementType.Kinematic;

        if (rotatory)
        {
            // MODO MESA: Apagamos el seguimiento de posición para que no se separe de la base
            terrenoInteractable.trackPosition = false;
        }
        else
        {
            // MODO LIBRE: Rastreo total liberado
            terrenoInteractable.trackPosition = true;
        }
    }

    // El Candado Matemático
    // El Candado Matemático y Sincronización Visual
    void LateUpdate()
    {
        // Solo actuamos si estamos en Modo Giratorio
        if (isRotatoryMode.Value && puntoAnclajeMesa != null)
        {
            // 1. Clavamos la posición estrictamente a la mesa
            transform.position = puntoAnclajeMesa.position;

            // 2. Destruimos cualquier inclinación (X) o balanceo (Z) del terreno
            Vector3 eulerTerreno = transform.localEulerAngles;
            transform.localEulerAngles = new Vector3(0f, eulerTerreno.y, 0f);

            // 3. NUEVO: Hacemos que la Mesa Visual imite el giro del terreno
            if (mesaVisual != null)
            {
                Vector3 eulerMesa = mesaVisual.transform.localEulerAngles;
                mesaVisual.transform.localEulerAngles = new Vector3(eulerMesa.x, eulerTerreno.y, eulerMesa.z);
            }
        }
    }
}