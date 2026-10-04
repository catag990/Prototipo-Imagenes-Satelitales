using UnityEngine;
using Unity.Netcode;
using System;

public class TerrainLayerManager : NetworkBehaviour
{
    [Header("Referencias Visuales")]
    [Tooltip("MeshRenderer del terreno tridimensional")]
    public MeshRenderer terrainRenderer;

    [Header("Texturas (Capas)")]
    public Texture2D texturaOptica;
    public Texture2D texturaSAR;

    // =========================================================
    // ESTADO GLOBAL DE CAPA Y MEZCLA
    // =========================================================
    private NetworkVariable<bool> isSarActive = new NetworkVariable<bool>(false);
    
    // AÑADIDO: La mezcla ahora es una variable de red compartida
    private NetworkVariable<float> netSarBlend = new NetworkVariable<float>(0f);

    private Material terrainMaterial;
    private bool materialPreparado = false;
    private bool warningShaderMostrado = false;

    // Memoria en el servidor para el botón de "vistazo rápido"
    private float previousBlendValue = 1f;

    // =========================================================
    // EVENTOS LOCALES (Para actualizar la UI)
    // =========================================================
    public event Action<bool> OnSarStateChangedLocal;
    public event Action<float> OnLocalBlendChanged;

    // =========================================================
    // PROPIEDADES PÚBLICAS
    // =========================================================
    public bool IsSarActive => isSarActive.Value;
    public float CurrentSarBlend => netSarBlend.Value;

    public bool SupportsSmoothComparison
    {
        get
        {
            if (!materialPreparado || terrainMaterial == null) return false;
            return terrainMaterial.HasProperty("_OpticalTex") &&
                   terrainMaterial.HasProperty("_SARTex") &&
                   terrainMaterial.HasProperty("_Blend");
        }
    }

    // =========================================================
    // AWAKE & NETWORK SPAWN
    // =========================================================
    private void Awake()
    {
        PrepararMaterial();
    }

    public override void OnNetworkSpawn()
    {
        isSarActive.OnValueChanged += OnLayerStateChanged;
        netSarBlend.OnValueChanged += OnBlendStateChanged;

        PrepararMaterial();
        AplicarEstadoGlobalLocal(isSarActive.Value);
        AplicarBlendVisual(netSarBlend.Value);
    }

    public override void OnNetworkDespawn()
    {
        isSarActive.OnValueChanged -= OnLayerStateChanged;
        netSarBlend.OnValueChanged -= OnBlendStateChanged;
    }

    // =========================================================
    // PREPARACIÓN DEL MATERIAL
    // =========================================================
    private void PrepararMaterial()
    {
        if (terrainRenderer == null) return;
        if (terrainMaterial == null) terrainMaterial = terrainRenderer.material;

        materialPreparado = terrainMaterial != null;
        if (!materialPreparado) return;

        if (SupportsSmoothComparison)
        {
            terrainMaterial.SetTexture("_OpticalTex", texturaOptica);
            terrainMaterial.SetTexture("_SARTex", texturaSAR);
        }
    }

    // =========================================================
    // CAMBIO GLOBAL DE CAPA (Botón Activar/Desactivar)
    // =========================================================
    public void ToggleLayer()
    {
        ToggleLayerServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ToggleLayerServerRpc()
    {
        bool newState = !isSarActive.Value;
        isSarActive.Value = newState;
        
        // Si encendemos SAR, vamos al 100% SAR. Si apagamos, al 0%.
        netSarBlend.Value = newState ? 1f : 0f;
    }

    private void OnLayerStateChanged(bool oldState, bool newState)
    {
        AplicarEstadoGlobalLocal(newState);
    }

    private void AplicarEstadoGlobalLocal(bool showSar)
    {
        PrepararMaterial();
        OnSarStateChangedLocal?.Invoke(showSar);
    }

    // =========================================================
    // CAMBIO GLOBAL DE MEZCLA (Slider y Vistazo Rápido)
    // =========================================================
    public void SetSharedComparisonBlend(float sarBlend)
    {
        if (!IsSarActive) return;
        SetBlendServerRpc(Mathf.Clamp01(sarBlend), true);
    }

    public void BeginSharedOpticalPreview()
    {
        if (!IsSarActive) return;
        SetBlendServerRpc(0f, false);
    }

    public void EndSharedOpticalPreview()
    {
        if (!IsSarActive) return;
        RestoreBlendServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SetBlendServerRpc(float newBlend, bool saveAsPrevious)
    {
        if (saveAsPrevious)
        {
            previousBlendValue = newBlend;
        }
        netSarBlend.Value = newBlend;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RestoreBlendServerRpc()
    {
        netSarBlend.Value = previousBlendValue;
    }

    private void OnBlendStateChanged(float oldBlend, float newBlend)
    {
        AplicarBlendVisual(newBlend);
        
        // Actualiza el Slider UI de todos los usuarios
        OnLocalBlendChanged?.Invoke(newBlend); 
    }

    // =========================================================
    // APLICACIÓN VISUAL
    // =========================================================
    private void AplicarBlendVisual(float sarBlend)
    {
        PrepararMaterial();

        if (terrainMaterial == null) return;

        if (SupportsSmoothComparison)
        {
            terrainMaterial.SetFloat("_Blend", sarBlend);
        }
        else
        {
            Texture2D texturaAAplicar = sarBlend >= 0.5f ? texturaSAR : texturaOptica;
            terrainMaterial.mainTexture = texturaAAplicar;

            if (!warningShaderMostrado)
            {
                Debug.LogWarning("[TerrainLayerManager] El material del terreno no utiliza el shader de mezcla Óptico/SAR.");
                warningShaderMostrado = true;
            }
        }
    }
}