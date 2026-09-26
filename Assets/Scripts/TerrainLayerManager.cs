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
    // ESTADO GLOBAL DE CAPA
    // =========================================================
    private NetworkVariable<bool> isSarActive = new NetworkVariable<bool>(false);

    // =========================================================
    // ESTADO LOCAL DE COMPARACIÓN
    // =========================================================
    private float localSarBlend = 0f;
    
    // AÑADIDO (PUNTO 5): Memoria del nivel de mezcla preferido por el usuario
    private float savedSarBlend = 1f;

    private Material terrainMaterial;
    private bool materialPreparado = false;
    private bool warningShaderMostrado = false;

    // =========================================================
    // EVENTOS LOCALES
    // =========================================================
    public event Action<bool> OnSarStateChangedLocal;
    public event Action<float> OnLocalBlendChanged;

    // =========================================================
    // PROPIEDADES PÚBLICAS
    // =========================================================
    public bool IsSarActive => isSarActive.Value;
    public float LocalSarBlend => localSarBlend;
    
    // AÑADIDO (PUNTO 5): Exponer la memoria para que el Slider la lea
    public float SavedSarBlend => savedSarBlend; 

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
        PrepararMaterial();
        AplicarEstadoGlobalLocal(isSarActive.Value);
    }

    public override void OnNetworkDespawn()
    {
        isSarActive.OnValueChanged -= OnLayerStateChanged;
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
    // CAMBIO GLOBAL
    // =========================================================
    public void ToggleLayer()
    {
        ToggleLayerServerRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ToggleLayerServerRpc()
    {
        isSarActive.Value = !isSarActive.Value;
    }

    private void OnLayerStateChanged(bool oldState, bool newState)
    {
        AplicarEstadoGlobalLocal(newState);
    }

    private void AplicarEstadoGlobalLocal(bool showSar)
    {
        PrepararMaterial();

        if (showSar)
        {
            // MODIFICADO (PUNTO 5): Vuelve al valor que el usuario guardó, no a 1f
            AplicarBlendLocal(savedSarBlend);
        }
        else
        {
            AplicarBlendLocal(0f);
        }

        OnSarStateChangedLocal?.Invoke(showSar);
    }

    // =========================================================
    // COMPARACIÓN LOCAL ÓPTICO / SAR
    // =========================================================
    public void SetLocalComparisonBlend(float sarBlend)
    {
        if (!IsSarActive)
        {
            AplicarBlendLocal(0f);
            return;
        }

        float clampedValue = Mathf.Clamp01(sarBlend);
        
        // AÑADIDO (PUNTO 5): Guardar la decisión del usuario en memoria
        savedSarBlend = clampedValue; 
        
        AplicarBlendLocal(clampedValue);
    }

    public void BeginLocalOpticalPreview()
    {
        if (!IsSarActive) return;
        AplicarBlendLocal(0f); // Vista temporal, NO sobrescribe savedSarBlend
    }

    public void EndLocalOpticalPreview()
    {
        ReturnToSarLocal();
    }

    public void ReturnToSarLocal()
    {
        if (!IsSarActive)
        {
            AplicarBlendLocal(0f);
            return;
        }
        
        // MODIFICADO (PUNTO 5): Retorna a la memoria guardada en vez de 1f
        AplicarBlendLocal(savedSarBlend); 
    }

    // =========================================================
    // APLICACIÓN VISUAL
    // =========================================================
    private void AplicarBlendLocal(float sarBlend)
    {
        localSarBlend = Mathf.Clamp01(sarBlend);
        PrepararMaterial();

        if (terrainMaterial == null) return;

        if (SupportsSmoothComparison)
        {
            terrainMaterial.SetFloat("_Blend", localSarBlend);
        }
        else
        {
            Texture2D texturaAAplicar = localSarBlend >= 0.5f ? texturaSAR : texturaOptica;
            terrainMaterial.mainTexture = texturaAAplicar;

            if (!warningShaderMostrado)
            {
                Debug.LogWarning("[TerrainLayerManager] El material del terreno no utiliza el shader de mezcla Óptico/SAR.");
                warningShaderMostrado = true;
            }
        }

        OnLocalBlendChanged?.Invoke(localSarBlend);
    }
}