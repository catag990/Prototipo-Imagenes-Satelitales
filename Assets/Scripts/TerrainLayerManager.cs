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

    // false = Óptico
    // true  = SAR
    //
    // Este estado continúa siendo compartido por red.
    private NetworkVariable<bool> isSarActive =
        new NetworkVariable<bool>(false);

    // =========================================================
    // ESTADO LOCAL DE COMPARACIÓN
    // =========================================================

    // 0 = Óptico
    // 1 = SAR
    //
    // Este valor NO se sincroniza por red.
    // Cada usuario puede comparar las capas individualmente.
    private float localSarBlend = 0f;

    private Material terrainMaterial;

    private bool materialPreparado = false;
    private bool warningShaderMostrado = false;

    // =========================================================
    // EVENTOS LOCALES
    // =========================================================

    // Informa a la UI local si la capa global
    // actualmente activa es SAR.
    public event Action<bool> OnSarStateChangedLocal;

    // Informa a controles locales, como el slider,
    // del porcentaje actual de mezcla.
    public event Action<float> OnLocalBlendChanged;

    // =========================================================
    // PROPIEDADES PÚBLICAS
    // =========================================================

    public bool IsSarActive
    {
        get
        {
            return isSarActive.Value;
        }
    }

    public float LocalSarBlend
    {
        get
        {
            return localSarBlend;
        }
    }

    // Indica si el material permite comparación progresiva.
    public bool SupportsSmoothComparison
    {
        get
        {
            if (!materialPreparado ||
                terrainMaterial == null)
            {
                return false;
            }

            return
                terrainMaterial.HasProperty("_OpticalTex") &&
                terrainMaterial.HasProperty("_SARTex") &&
                terrainMaterial.HasProperty("_Blend");
        }
    }

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        PrepararMaterial();
    }

    // =========================================================
    // NETWORK SPAWN
    // =========================================================

    public override void OnNetworkSpawn()
    {
        isSarActive.OnValueChanged +=
            OnLayerStateChanged;

        PrepararMaterial();

        // Late-Joining:
        // aplicar inmediatamente la capa vigente.
        AplicarEstadoGlobalLocal(
            isSarActive.Value);
    }

    public override void OnNetworkDespawn()
    {
        isSarActive.OnValueChanged -=
            OnLayerStateChanged;
    }

    // =========================================================
    // PREPARACIÓN DEL MATERIAL
    // =========================================================

    private void PrepararMaterial()
    {
        if (terrainRenderer == null)
        {
            return;
        }

        if (terrainMaterial == null)
        {
            terrainMaterial =
                terrainRenderer.material;
        }

        materialPreparado =
            terrainMaterial != null;

        if (!materialPreparado)
        {
            return;
        }

        // Si está asignado el shader de mezcla,
        // se cargan ambas texturas una sola vez.
        if (SupportsSmoothComparison)
        {
            terrainMaterial.SetTexture(
                "_OpticalTex",
                texturaOptica);

            terrainMaterial.SetTexture(
                "_SARTex",
                texturaSAR);
        }
    }

    // =========================================================
    // CAMBIO GLOBAL DESDE UI
    // =========================================================

    public void ToggleLayer()
    {
        ToggleLayerServerRpc();
    }

    [Rpc(
        SendTo.Server,
        InvokePermission = RpcInvokePermission.Everyone)]
    private void ToggleLayerServerRpc()
    {
        isSarActive.Value =
            !isSarActive.Value;
    }

    // =========================================================
    // RECEPCIÓN DEL CAMBIO GLOBAL
    // =========================================================

    private void OnLayerStateChanged(
        bool oldState,
        bool newState)
    {
        AplicarEstadoGlobalLocal(
            newState);
    }

    private void AplicarEstadoGlobalLocal(
        bool showSar)
    {
        PrepararMaterial();

        // Al cambiar globalmente de capa,
        // cualquier comparación temporal se reinicia.
        if (showSar)
        {
            AplicarBlendLocal(1f);
        }
        else
        {
            AplicarBlendLocal(0f);
        }

        // Notificar exclusivamente a interfaces
        // de este cliente.
        OnSarStateChangedLocal?.Invoke(
            showSar);
    }

    // =========================================================
    // COMPARACIÓN LOCAL ÓPTICO / SAR
    // =========================================================

    public void SetLocalComparisonBlend(
        float sarBlend)
    {
        // La comparación solo tiene sentido cuando
        // la capa compartida vigente es SAR.
        if (!IsSarActive)
        {
            AplicarBlendLocal(0f);
            return;
        }

        AplicarBlendLocal(
            Mathf.Clamp01(sarBlend));
    }

    // Vista Óptica temporal.
    //
    // No modifica isSarActive.
    // No envía RPC.
    // No altera a otros usuarios.
    public void BeginLocalOpticalPreview()
    {
        if (!IsSarActive)
        {
            return;
        }

        AplicarBlendLocal(0f);
    }

    // Al soltar el control comparativo,
    // regresar completamente a SAR.
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

        AplicarBlendLocal(1f);
    }

    // =========================================================
    // APLICACIÓN VISUAL
    // =========================================================

    private void AplicarBlendLocal(
        float sarBlend)
    {
        localSarBlend =
            Mathf.Clamp01(sarBlend);

        PrepararMaterial();

        if (terrainMaterial == null)
        {
            return;
        }

        if (SupportsSmoothComparison)
        {
            // Mezcla progresiva:
            //
            // 0 = Óptico
            // 1 = SAR
            terrainMaterial.SetFloat(
                "_Blend",
                localSarBlend);
        }
        else
        {
            // Fallback para el material antiguo.
            //
            // Permite que el botón "mantener para comparar"
            // siga funcionando, aunque NO habrá transición
            // progresiva del slider.
            Texture2D texturaAAplicar =
                localSarBlend >= 0.5f
                    ? texturaSAR
                    : texturaOptica;

            terrainMaterial.mainTexture =
                texturaAAplicar;

            if (!warningShaderMostrado)
            {
                Debug.LogWarning(
                    "[TerrainLayerManager] " +
                    "El material del terreno no utiliza " +
                    "el shader de mezcla Óptico/SAR. " +
                    "La comparación funcionará como cambio " +
                    "directo, pero el slider no tendrá " +
                    "transición progresiva.");

                warningShaderMostrado = true;
            }
        }

        OnLocalBlendChanged?.Invoke(
            localSarBlend);
    }
}