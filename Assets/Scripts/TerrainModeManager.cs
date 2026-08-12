using UnityEngine;
using Unity.Netcode;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

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

    // false = modo libre
    // true  = modo mesa fija
    private NetworkVariable<bool>
        isRotatoryMode =
            new NetworkVariable<bool>(
                false);

    // =========================================================
    // ROTACIÓN DESDE EL PUNTO DE AGARRE
    // =========================================================

    private Transform interactorActivo;

    private bool agarreRotacionActivo =
        false;

    // Dirección inicial entre el centro de la mesa
    // y el controlador que realizó el agarre.
    private Vector3 direccionInicial;

    // Rotación que tenía el terreno al comenzar.
    private float rotacionInicialY;

    // Posición central fija de la mesa.
    private Vector3 posicionMesaFija;

    // =========================================================
    // AWAKE
    // =========================================================

    void Awake()
    {
        if (terrenoInteractable == null)
        {
            Debug.LogError(
                "[TerrainModeManager] " +
                "terrenoInteractable no está asignado.");

            return;
        }

        rb =
            terrenoInteractable
                .GetComponent<Rigidbody>();

        if (terrainSync == null)
        {
            terrainSync =
                terrenoInteractable
                    .GetComponent<
                        NetworkTerrainSync>();

            if (terrainSync == null)
            {
                terrainSync =
                    terrenoInteractable
                        .GetComponentInParent<
                            NetworkTerrainSync>();
            }
        }

        if (terrainSync == null)
        {
            Debug.LogError(
                "[TerrainModeManager] " +
                "NetworkTerrainSync no está asignado.");
        }

        // Mantener el punto donde realmente
        // se tomó el terreno.
        terrenoInteractable.useDynamicAttach =
            true;

        terrenoInteractable.matchAttachPosition =
            true;

        terrenoInteractable.matchAttachRotation =
            false;
    }

    // =========================================================
    // EVENTOS DE AGARRE
    // =========================================================

    void OnEnable()
    {
        if (terrenoInteractable == null)
            return;

        terrenoInteractable
            .selectEntered
            .AddListener(
                OnGrabStarted);

        terrenoInteractable
            .selectExited
            .AddListener(
                OnGrabEnded);
    }

    void OnDisable()
    {
        if (terrenoInteractable == null)
            return;

        terrenoInteractable
            .selectEntered
            .RemoveListener(
                OnGrabStarted);

        terrenoInteractable
            .selectExited
            .RemoveListener(
                OnGrabEnded);
    }

    // =========================================================
    // NETWORK SPAWN
    // =========================================================

    public override void OnNetworkSpawn()
    {
        isRotatoryMode.OnValueChanged +=
            OnModeChanged;

        AplicarModoLocal(
            isRotatoryMode.Value);
    }

    public override void OnNetworkDespawn()
    {
        isRotatoryMode.OnValueChanged -=
            OnModeChanged;
    }

    // =========================================================
    // CAMBIO ENTRE MODO LIBRE Y MODO MESA
    // =========================================================

    public void ToggleMode()
    {
        ToggleModeServerRpc();
    }

    [Rpc(
        SendTo.Server,
        InvokePermission =
            RpcInvokePermission.Everyone)]
    private void ToggleModeServerRpc()
    {
        isRotatoryMode.Value =
            !isRotatoryMode.Value;

        if (isRotatoryMode.Value &&
            puntoAnclajeMesa != null &&
            terrainSync != null)
        {
            terrainSync.ForceSnapToTable(
                puntoAnclajeMesa.position,
                puntoAnclajeMesa.rotation);
        }
    }

    private void OnModeChanged(
        bool oldMode,
        bool newMode)
    {
        AplicarModoLocal(
            newMode);
    }

    private void AplicarModoLocal(
        bool rotatory)
    {
        if (mesaVisual != null)
        {
            mesaVisual.SetActive(
                rotatory);
        }

        if (rb != null)
        {
            rb.isKinematic =
                true;
        }

        if (terrenoInteractable == null)
            return;

        terrenoInteractable.movementType =
            XRBaseInteractable
                .MovementType
                .Kinematic;

        if (rotatory)
        {
            // =============================================
            // MODO MESA FIJA
            // =============================================
            //
            // TerrainModeManager controla manualmente
            // la rotación horizontal.
            //
            // El XR Grab Interactable no mueve ni rota
            // directamente el terreno.

            terrenoInteractable.trackPosition =
                false;

            terrenoInteractable.trackRotation =
                false;
        }
        else
        {
            // =============================================
            // MODO LIBRE
            // =============================================

            terrenoInteractable.trackPosition =
                true;

            terrenoInteractable.trackRotation =
                true;

            agarreRotacionActivo =
                false;

            interactorActivo =
                null;
        }
    }

    // =========================================================
    // INICIO DEL AGARRE
    // =========================================================

    private void OnGrabStarted(
        SelectEnterEventArgs args)
    {
        // -----------------------------------------------------
        // IMPORTANTE:
        // Este llamado ocurre SIEMPRE.
        //
        // Tanto en modo libre como en modo mesa,
        // cualquier agarre bloquea POIs y lazos
        // para TODOS los clientes.
        // -----------------------------------------------------

        if (terrainSync != null)
        {
            terrainSync.OnGrabLocally();
        }
        else
        {
            Debug.LogError(
                "[TerrainModeManager] " +
                "No se pudo activar el bloqueo global " +
                "porque terrainSync es null.");
        }

        // En modo libre no necesitamos
        // la lógica manual de rotación.
        if (!isRotatoryMode.Value)
            return;

        if (puntoAnclajeMesa == null)
            return;

        // Obtener el punto efectivo donde el
        // controlador tomó el terreno.
        Transform attachInteractor =
            args.interactorObject
                .GetAttachTransform(
                    terrenoInteractable);

        if (attachInteractor == null)
            return;

        interactorActivo =
            attachInteractor;

        posicionMesaFija =
            puntoAnclajeMesa.position;

        rotacionInicialY =
            transform.eulerAngles.y;

        // Vector horizontal desde el centro de la mesa
        // al punto de agarre.
        Vector3 direccion =
            interactorActivo.position -
            posicionMesaFija;

        direccion.y =
            0f;

        if (direccion.sqrMagnitude <
            0.0001f)
        {
            agarreRotacionActivo =
                false;

            return;
        }

        direccionInicial =
            direccion.normalized;

        agarreRotacionActivo =
            true;
    }

    // =========================================================
    // FIN DEL AGARRE
    // =========================================================

    private void OnGrabEnded(
        SelectExitEventArgs args)
    {
        // -----------------------------------------------------
        // IMPORTANTE:
        // Se libera independientemente de modo libre/fijo.
        // -----------------------------------------------------

        if (terrainSync != null)
        {
            terrainSync.OnReleaseLocally();
        }

        if (!isRotatoryMode.Value)
            return;

        agarreRotacionActivo =
            false;

        interactorActivo =
            null;
    }

    // =========================================================
    // ROTACIÓN EN MODO MESA
    // =========================================================

    void LateUpdate()
    {
        if (!isRotatoryMode.Value ||
            puntoAnclajeMesa == null)
        {
            return;
        }

        // La mesa conserva su centro fijo.
        transform.position =
            puntoAnclajeMesa.position;

        if (agarreRotacionActivo &&
            interactorActivo != null)
        {
            Vector3 direccionActual =
                interactorActivo.position -
                posicionMesaFija;

            direccionActual.y =
                0f;

            if (direccionActual.sqrMagnitude >
                0.0001f)
            {
                direccionActual.Normalize();

                float deltaY =
                    Vector3.SignedAngle(
                        direccionInicial,
                        direccionActual,
                        Vector3.up);

                float nuevaRotacionY =
                    rotacionInicialY +
                    deltaY;

                transform.rotation =
                    Quaternion.Euler(
                        0f,
                        nuevaRotacionY,
                        0f);
            }
        }
        else
        {
            // Bloquear inclinación accidental.
            Vector3 eulerTerreno =
                transform.eulerAngles;

            transform.rotation =
                Quaternion.Euler(
                    0f,
                    eulerTerreno.y,
                    0f);
        }

        // Mantener sincronizada visualmente
        // la base de la mesa.
        if (mesaVisual != null)
        {
            Vector3 eulerMesa =
                mesaVisual
                    .transform
                    .localEulerAngles;

            mesaVisual
                .transform
                .localEulerAngles =
                    new Vector3(
                        eulerMesa.x,
                        transform
                            .localEulerAngles
                            .y,
                        eulerMesa.z);
        }
    }
}