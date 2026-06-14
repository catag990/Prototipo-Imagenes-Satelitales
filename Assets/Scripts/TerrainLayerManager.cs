using UnityEngine;
using Unity.Netcode;

public class TerrainLayerManager : NetworkBehaviour
{
    [Header("Referencias Visuales")]
    [Tooltip("El MeshRenderer de tu terreno tridimensional")]
    public MeshRenderer terrainRenderer;
    
    [Header("Texturas (Capas)")]
    public Texture2D texturaOptica;
    public Texture2D texturaSAR;

    // NetworkVariable: Guarda el estado global. Falso = Óptico, Verdadero = SAR.
    // Al usar NetworkVariable, los Late-Joiners leen esto automáticamente al entrar.
    private NetworkVariable<bool> isSarActive = new NetworkVariable<bool>(false);

    public override void OnNetworkSpawn()
    {
        // 1. Nos suscribimos para escuchar cuando alguien cambie la capa
        isSarActive.OnValueChanged += OnLayerStateChanged;
        
        // 2. Late-Joiners: Aplicamos la textura correcta en el instante en que nos conectamos
        AplicarTexturaLocal(isSarActive.Value);
    }

    public override void OnNetworkDespawn()
    {
        isSarActive.OnValueChanged -= OnLayerStateChanged;
    }

    // --- INTERACCIÓN DESDE LA UI ---
    
    // Este es el método que pondrás en el OnClick() de tu botón en el Canvas
    public void ToggleLayer()
    {
        ToggleLayerServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ToggleLayerServerRpc()
    {
        // El servidor invierte el estado (de Óptico a SAR, o viceversa)
        isSarActive.Value = !isSarActive.Value;
    }

    // --- ACTUALIZACIÓN VISUAL ---

    private void OnLayerStateChanged(bool oldState, bool newState)
    {
        AplicarTexturaLocal(newState);
    }

    private void AplicarTexturaLocal(bool showSar)
    {
        if (terrainRenderer != null)
        {
            // Seleccionamos la textura correspondiente
            Texture2D texturaAAplicar = showSar ? texturaSAR : texturaOptica;
            
            // Reemplazamos la textura principal del material.
            // Esto es sumamente barato en procesamiento de GPU (ideal para VR).
            terrainRenderer.material.mainTexture = texturaAAplicar;
            
            // Nota: Si usas Universal Render Pipeline (URP), descomenta la siguiente línea 
            // y comenta la anterior si mainTexture no te funciona:
            // terrainRenderer.material.SetTexture("_BaseMap", texturaAAplicar);
        }
    }
}