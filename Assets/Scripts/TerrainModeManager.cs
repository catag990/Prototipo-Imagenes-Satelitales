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

    private NetworkVariable<bool> isRotatoryMode =
        new NetworkVariable<bool>(false);

    // =========================================================
    // ROTACIÓN DESDE EL PUNTO DE AGARRE
    // =========================================================

    private Transform interactorActivo;

    private bool agarreRotacionActivo = false;

    // Dirección inicial desde el centro de la mesa hacia
    // el mando, proyectada sobre el plano horizontal.
    private Vector3 direccionInicial;

    // Rotación Y que tenía el terreno al comenzar el agarre.
    private float rotacionInicialY;

    // Posición fija de la mesa durante el agarre.
    private Vector3 posicionMesaFija;

    void Awake()
    {
        rb = terrenoInteractable.GetComponent<Rigidbody>();

        // Dynamic Attach evita que el agarre se trate
        // siempre como si se hubiera realizado desde el centro.
        terrenoInteractable.useDynamicAttach = true;
        terrenoInteractable.matchAttachPosition = true;
        terrenoInteractable.matchAttachRotation = false;
    }

    void OnEnable()
    {
        terrenoInteractable.selectEntered.AddListener(
            OnGrabStarted);

        terrenoInteractable.selectExited.AddListener(
            OnGrabEnded);
    }

    void OnDisable()
    {
        terrenoInteractable.selectEntered.RemoveListener(
            OnGrabStarted);

        terrenoInteractable.selectExited.RemoveListener(
            OnGrabEnded);
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

    // =========================================================
    // CAMBIO ENTRE MODO LIBRE Y MODO MESA
    // =========================================================

    public void ToggleMode()
    {
        ToggleModeServerRpc();
    }

    [Rpc(
        SendTo.Server,
        InvokePermission = RpcInvokePermission.Everyone)]
    private void ToggleModeServerRpc()
    {
        isRotatoryMode.Value =
            !isRotatoryMode.Value;

        if (isRotatoryMode.Value &&
            puntoAnclajeMesa != null)
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
        AplicarModoLocal(newMode);
    }

    private void AplicarModoLocal(bool rotatory)
    {
        if (mesaVisual != null)
            mesaVisual.SetActive(rotatory);

        rb.isKinematic = true;

        terrenoInteractable.movementType =
            XRBaseInteractable.MovementType.Kinematic;

        if (rotatory)
        {
            // MODO MESA
            //
            // XRGrabInteractable no controlará ni posición
            // ni rotación directamente.
            // TerrainModeManager realizará la rotación
            // horizontal desde el lugar donde fue tomada.

            terrenoInteractable.trackPosition = false;
            terrenoInteractable.trackRotation = false;
        }
        else
        {
            // MODO LIBRE
            //
            // El XRGrabInteractable recupera el control
            // normal de posición y rotación.

            terrenoInteractable.trackPosition = true;
            terrenoInteractable.trackRotation = true;

            agarreRotacionActivo = false;
            interactorActivo = null;
        }
    }

    // =========================================================
    // INICIO DEL AGARRE
    // =========================================================

    private void OnGrabStarted(
        SelectEnterEventArgs args)
    {
        if (terrainSync != null)
            terrainSync.OnGrabLocally();
        // En modo libre no modificamos el comportamiento
        // normal del XR Grab Interactable.
        if (!isRotatoryMode.Value)
            return;

        if (puntoAnclajeMesa == null)
            return;

        // Obtener el Transform que representa el punto
        // efectivo del mando/interactor.
        Transform attachInteractor =
            args.interactorObject.GetAttachTransform(
                terrenoInteractable);

        if (attachInteractor == null)
            return;

        interactorActivo = attachInteractor;

        posicionMesaFija =
            puntoAnclajeMesa.position;

        rotacionInicialY =
            transform.eulerAngles.y;

        // Vector desde el centro de la mesa hasta
        // el lugar actual del mando.
        Vector3 direccion =
            interactorActivo.position -
            posicionMesaFija;

        // Ignorar movimiento vertical.
        direccion.y = 0f;

        if (direccion.sqrMagnitude <
            0.0001f)
        {
            agarreRotacionActivo = false;
            return;
        }

        direccionInicial =
            direccion.normalized;

        agarreRotacionActivo = true;
    }

    // =========================================================
    // FIN DEL AGARRE
    // =========================================================

    private void OnGrabEnded(
        SelectExitEventArgs args)
    {
        if (terrainSync != null)
            terrainSync.OnReleaseLocally();
            
        if (!isRotatoryMode.Value)
            return;

        agarreRotacionActivo = false;
        interactorActivo = null;
    }

    // =========================================================
    // ROTACIÓN DE LA MESA
    // =========================================================

    void LateUpdate()
    {
        if (!isRotatoryMode.Value ||
            puntoAnclajeMesa == null)
        {
            return;
        }

        // La mesa permanece en su posición fija.
        transform.position =
            puntoAnclajeMesa.position;

        if (agarreRotacionActivo &&
            interactorActivo != null)
        {
            Vector3 direccionActual =
                interactorActivo.position -
                posicionMesaFija;

            direccionActual.y = 0f;

            if (direccionActual.sqrMagnitude >
                0.0001f)
            {
                direccionActual.Normalize();

                // Ángulo horizontal recorrido por la mano
                // desde el lugar donde comenzó el agarre.
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
            // Aunque no exista agarre, impedir inclinación
            // accidental en X o Z.
            Vector3 eulerTerreno =
                transform.eulerAngles;

            transform.rotation =
                Quaternion.Euler(
                    0f,
                    eulerTerreno.y,
                    0f);
        }

        // La representación visual de la mesa conserva
        // el mismo giro horizontal del terreno.
        if (mesaVisual != null)
        {
            Vector3 eulerMesa =
                mesaVisual.transform.localEulerAngles;

            mesaVisual.transform.localEulerAngles =
                new Vector3(
                    eulerMesa.x,
                    transform.localEulerAngles.y,
                    eulerMesa.z);
        }
    }
}