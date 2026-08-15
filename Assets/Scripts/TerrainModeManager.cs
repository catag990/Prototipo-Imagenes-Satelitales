using UnityEngine;
using Unity.Netcode;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections.Generic;

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
    // true = modo mesa fija
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

    private Vector3 direccionInicial;

    private float rotacionInicialY;

    private Vector3 posicionMesaFija;

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
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

        // Conserva la lógica existente del anexo:
        // el agarre se produce desde el punto real
        // donde el usuario tomó el terreno.
        terrenoInteractable.useDynamicAttach =
            true;

        terrenoInteractable.matchAttachPosition =
            true;

        terrenoInteractable.matchAttachRotation =
            false;
    }

    // =========================================================
    // EVENTOS XR
    // =========================================================

    private void OnEnable()
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

    private void OnDisable()
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

        if (terrainSync != null)
        {
            terrainSync
                .OnLocalManipulationRejected +=
                OnLocalManipulationRejected;
        }

        AplicarModoLocal(
            isRotatoryMode.Value);
    }

    public override void OnNetworkDespawn()
    {
        isRotatoryMode.OnValueChanged -=
            OnModeChanged;

        if (terrainSync != null)
        {
            terrainSync
                .OnLocalManipulationRejected -=
                OnLocalManipulationRejected;
        }
    }

    // =========================================================
    // CAMBIO DE MODO
    // =========================================================

    public void ToggleMode()
    {
        // -----------------------------------------------------
        // PRIMERA BARRERA:
        // comprobación local inmediata.
        // -----------------------------------------------------

        if (terrainSync == null ||
            terrainSync.IsTerrainBeingManipulated)
        {
            Debug.Log(
                "[TerrainModeManager] " +
                "Cambio de modo bloqueado: " +
                "el terreno está siendo manipulado.");

            return;
        }

        ToggleModeServerRpc();
    }

    [Rpc(
        SendTo.Server,
        InvokePermission =
            RpcInvokePermission.Everyone)]
    private void ToggleModeServerRpc()
    {
        // -----------------------------------------------------
        // SEGUNDA BARRERA:
        // validación autoritativa en servidor.
        // -----------------------------------------------------

        if (terrainSync == null ||
            terrainSync.IsTerrainLockedByNetwork)
        {
            return;
        }

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
        AplicarModoLocal(
            newMode);
    }

    // =========================================================
    // APLICAR MODO
    // =========================================================

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
            // =================================================
            // MODO MESA FIJA
            // =================================================

            terrenoInteractable.trackPosition =
                false;

            terrenoInteractable.trackRotation =
                false;
        }
        else
        {
            // =================================================
            // MODO LIBRE
            // =================================================

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
        if (terrainSync == null)
        {
            ForzarSalidaInteractor(
                args.interactorObject);

            return;
        }

        // -----------------------------------------------------
        // SOLICITAR LOCK EXCLUSIVO
        // -----------------------------------------------------

        bool lockAceptado =
            terrainSync.OnGrabLocally();

        if (!lockAceptado)
        {
            // Otro usuario ya posee el terreno.
            ForzarSalidaInteractor(
                args.interactorObject);

            Debug.Log(
                "[TerrainModeManager] " +
                "Agarre rechazado: " +
                "otro usuario posee el lock del terreno.");

            return;
        }

        // En modo libre el XR Grab realiza
        // posición/rotación normalmente.
        if (!isRotatoryMode.Value)
            return;

        if (puntoAnclajeMesa == null)
            return;

        // =====================================================
        // ROTACIÓN MANUAL DE MESA FIJA
        // =====================================================

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

        Vector3 direccion =
            interactorActivo.position -
            posicionMesaFija;

        direccion.y = 0f;

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
    // SERVIDOR RECHAZÓ NUESTRA CARRERA DE AGARRE
    // =========================================================

    private void OnLocalManipulationRejected()
    {
        agarreRotacionActivo =
            false;

        interactorActivo =
            null;

        ForzarLiberacionCompleta();
    }

    // =========================================================
    // FINALIZAR UNA SELECCIÓN XR
    // =========================================================

    private void ForzarSalidaInteractor(
        IXRSelectInteractor interactor)
    {
        if (terrenoInteractable == null ||
            interactor == null)
        {
            return;
        }

        XRInteractionManager manager =
            terrenoInteractable
                .interactionManager;

        if (manager == null)
            return;

        // Trabajar sobre una copia evita modificar
        // directamente la colección de Unity.
        List<IXRSelectInteractor> seleccionadores =
            new List<IXRSelectInteractor>(
                terrenoInteractable
                    .interactorsSelecting);

        foreach (IXRSelectInteractor seleccionado
                 in seleccionadores)
        {
            if (seleccionado != interactor)
                continue;

            manager.SelectExit(
                seleccionado,
                terrenoInteractable);

            break;
        }
    }

    // =========================================================
    // LIBERAR TODAS LAS MANOS LOCALES
    // =========================================================

    private void ForzarLiberacionCompleta()
    {
        if (terrenoInteractable == null)
            return;

        XRInteractionManager manager =
            terrenoInteractable
                .interactionManager;

        if (manager == null)
            return;

        List<IXRSelectInteractor> seleccionadores =
            new List<IXRSelectInteractor>(
                terrenoInteractable
                    .interactorsSelecting);

        foreach (IXRSelectInteractor interactor
                 in seleccionadores)
        {
            manager.SelectExit(
                interactor,
                terrenoInteractable);
        }
    }

    // =========================================================
    // ROTACIÓN DE MESA FIJA
    // =========================================================

    private void LateUpdate()
    {
        if (!isRotatoryMode.Value ||
            puntoAnclajeMesa == null)
        {
            return;
        }

        // La posición siempre permanece fija.
        transform.position =
            puntoAnclajeMesa.position;

        // Solo quien posee realmente el lock
        // puede aplicar rotación.
        if (agarreRotacionActivo &&
            interactorActivo != null &&
            terrainSync != null &&
            terrainSync
                .IsLocalClientManipulationOwner)
        {
            Vector3 direccionActual =
                interactorActivo.position -
                posicionMesaFija;

            direccionActual.y = 0f;

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
            // Anular inclinación X/Z.
            Vector3 eulerTerreno =
                transform.eulerAngles;

            transform.rotation =
                Quaternion.Euler(
                    0f,
                    eulerTerreno.y,
                    0f);
        }

        // Sincronización visual de la mesa.
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
                            .localEulerAngles.y,
                        eulerMesa.z);
        }
    }
}
