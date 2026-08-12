using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections.Generic;

public class NetworkTerrainSync : NetworkBehaviour
{
    // =========================================================
    // TRANSFORMACIÓN DEL TERRENO
    // =========================================================

    private NetworkVariable<Vector3> netPos =
        new NetworkVariable<Vector3>();

    private NetworkVariable<Quaternion> netRot =
        new NetworkVariable<Quaternion>();

    private NetworkVariable<Vector3> netScale =
        new NetworkVariable<Vector3>(
            Vector3.one);

    // =========================================================
    // BLOQUEO GLOBAL DE MANIPULACIÓN
    // =========================================================

    // true cuando uno o más usuarios
    // están manipulando el terreno.
    private NetworkVariable<bool>
        terrenoEnManipulacion =
            new NetworkVariable<bool>(
                false);

    // Estado del cliente local.
    private bool isGrabbedLocally =
        false;

    // Permite manipulación con dos manos.
    //
    // El bloqueo no se libera al soltar solo una
    // de las dos manos.
    private int localGrabCount =
        0;

    // El servidor conserva qué clientes tienen
    // actualmente el terreno seleccionado.
    private readonly HashSet<ulong>
        clientesManipulando =
            new HashSet<ulong>();

    // =========================================================
    // EVENTOS
    // =========================================================

    public event Action<bool>
        OnTerrainManipulationStateChanged;

    // =========================================================
    // PROPIEDADES PÚBLICAS
    // =========================================================

    // Uso local.
    //
    // Devuelve true inmediatamente si este usuario
    // está manipulando, o si el servidor informa
    // que cualquier otro usuario está manipulando.
    public bool IsTerrainBeingManipulated
    {
        get
        {
            return
                isGrabbedLocally ||
                terrenoEnManipulacion.Value;
        }
    }

    // Uso autoritativo en servidor.
    public bool IsTerrainLockedByNetwork
    {
        get
        {
            return
                terrenoEnManipulacion.Value;
        }
    }

    // =========================================================
    // NETWORK SPAWN
    // =========================================================

    public override void OnNetworkSpawn()
    {
        terrenoEnManipulacion.OnValueChanged +=
            OnManipulationChanged;

        if (IsServer)
        {
            netPos.Value =
                transform.position;

            netRot.Value =
                transform.rotation;

            netScale.Value =
                transform.localScale;

            if (NetworkManager != null)
            {
                NetworkManager
                    .OnClientDisconnectCallback +=
                    OnClientDisconnected;
            }
        }
        else
        {
            transform.position =
                netPos.Value;

            transform.rotation =
                netRot.Value;

            transform.localScale =
                netScale.Value;
        }
    }

    public override void OnNetworkDespawn()
    {
        terrenoEnManipulacion.OnValueChanged -=
            OnManipulationChanged;

        if (IsServer &&
            NetworkManager != null)
        {
            NetworkManager
                .OnClientDisconnectCallback -=
                OnClientDisconnected;
        }

        localGrabCount =
            0;

        isGrabbedLocally =
            false;
    }

    // =========================================================
    // CAMBIO DE ESTADO GLOBAL
    // =========================================================

    private void OnManipulationChanged(
        bool previousValue,
        bool newValue)
    {
        OnTerrainManipulationStateChanged?.Invoke(
            newValue);
    }

    // =========================================================
    // SINCRONIZACIÓN DE TRANSFORMACIÓN
    // =========================================================

    void Update()
    {
        if (!IsSpawned)
            return;

        if (isGrabbedLocally)
        {
            UpdateTransformServerRpc(
                transform.position,
                transform.rotation,
                transform.localScale);
        }
        else
        {
            transform.position =
                Vector3.Lerp(
                    transform.position,
                    netPos.Value,
                    Time.deltaTime * 10f);

            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    netRot.Value,
                    Time.deltaTime * 10f);

            transform.localScale =
                Vector3.Lerp(
                    transform.localScale,
                    netScale.Value,
                    Time.deltaTime * 10f);
        }
    }

    // =========================================================
    // ENVÍO DE TRANSFORMACIÓN AL SERVIDOR
    // =========================================================

    [Rpc(
        SendTo.Server,
        InvokePermission =
            RpcInvokePermission.Everyone)]
    private void UpdateTransformServerRpc(
        Vector3 pos,
        Quaternion rot,
        Vector3 scale)
    {
        netPos.Value =
            pos;

        netRot.Value =
            rot;

        netScale.Value =
            scale;
    }

    // =========================================================
    // INICIO DE MANIPULACIÓN LOCAL
    // =========================================================

    public void OnGrabLocally()
    {
        localGrabCount++;

        // Si existe un segundo agarre del mismo
        // usuario, el bloqueo ya se encuentra activo.
        if (localGrabCount > 1)
            return;

        isGrabbedLocally =
            true;

        // -----------------------------------------------------
        // BLOQUEO LOCAL INMEDIATO
        //
        // InteractionManager recibe el evento en este mismo
        // cliente antes de esperar al servidor.
        // Si existía un lazo en proceso, se cancela.
        // -----------------------------------------------------

        OnTerrainManipulationStateChanged?.Invoke(
            true);

        // -----------------------------------------------------
        // BLOQUEO GLOBAL
        // -----------------------------------------------------

        if (IsSpawned)
        {
            IniciarManipulacionServerRpc();
        }
    }

    // =========================================================
    // REGISTRAR MANIPULACIÓN EN SERVIDOR
    // =========================================================

    [Rpc(
        SendTo.Server,
        InvokePermission =
            RpcInvokePermission.Everyone)]
    private void IniciarManipulacionServerRpc(
        RpcParams rpcParams = default)
    {
        ulong clientId =
            rpcParams
                .Receive
                .SenderClientId;

        clientesManipulando.Add(
            clientId);

        terrenoEnManipulacion.Value =
            clientesManipulando.Count > 0;
    }

    // =========================================================
    // FIN DE MANIPULACIÓN LOCAL
    // =========================================================

    public void OnReleaseLocally()
    {
        localGrabCount =
            Mathf.Max(
                0,
                localGrabCount - 1);

        // Si todavía existe otra mano sujetando
        // el terreno, se conserva el bloqueo.
        if (localGrabCount > 0)
            return;

        if (!isGrabbedLocally)
            return;

        isGrabbedLocally =
            false;

        if (IsSpawned)
        {
            // La transformación final y la liberación
            // se realizan en una única operación
            // autoritativa.
            FinalizarManipulacionServerRpc(
                transform.position,
                transform.rotation,
                transform.localScale);
        }
    }

    // =========================================================
    // FINALIZAR MANIPULACIÓN EN SERVIDOR
    // =========================================================

    [Rpc(
        SendTo.Server,
        InvokePermission =
            RpcInvokePermission.Everyone)]
    private void FinalizarManipulacionServerRpc(
        Vector3 finalPos,
        Quaternion finalRot,
        Vector3 finalScale,
        RpcParams rpcParams = default)
    {
        // Primero registrar la transformación final.
        netPos.Value =
            finalPos;

        netRot.Value =
            finalRot;

        netScale.Value =
            finalScale;

        ulong clientId =
            rpcParams
                .Receive
                .SenderClientId;

        clientesManipulando.Remove(
            clientId);

        // Solo se desbloquea cuando no queda
        // ningún participante manipulando.
        terrenoEnManipulacion.Value =
            clientesManipulando.Count > 0;
    }

    // =========================================================
    // DESCONEXIÓN DE CLIENTE
    // =========================================================

    private void OnClientDisconnected(
        ulong clientId)
    {
        if (!IsServer)
            return;

        if (clientesManipulando.Remove(
            clientId))
        {
            terrenoEnManipulacion.Value =
                clientesManipulando.Count > 0;
        }
    }

    // =========================================================
    // SNAP A MODO MESA
    // =========================================================

    public void ForceSnapToTable(
        Vector3 worldPos,
        Quaternion worldRot)
    {
        if (!IsServer)
            return;

        netPos.Value =
            worldPos;

        netRot.Value =
            worldRot;
    }
}