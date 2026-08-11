using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections.Generic;

public class NetworkTerrainSync : NetworkBehaviour
{
    private NetworkVariable<Vector3> netPos =
        new NetworkVariable<Vector3>();

    private NetworkVariable<Quaternion> netRot =
        new NetworkVariable<Quaternion>();

    private NetworkVariable<Vector3> netScale =
        new NetworkVariable<Vector3>(Vector3.one);

    // Estado global:
    // true = al menos un usuario está manipulando el terreno.
    private NetworkVariable<bool> terrenoEnManipulacion =
        new NetworkVariable<bool>(false);

    // Estado exclusivamente local.
    private bool isGrabbedLocally = false;

    // Permite que dos manos del mismo usuario tomen el terreno
    // sin liberar accidentalmente el bloqueo cuando suelta una.
    private int localGrabCount = 0;

    // El servidor mantiene qué clientes están manipulando.
    private readonly HashSet<ulong> clientesManipulando =
        new HashSet<ulong>();

    // Evento consumible por InteractionManager.
    public event Action<bool> OnTerrainManipulationStateChanged;

    // Para comprobaciones locales:
    // la persona que acaba de tomar el terreno queda bloqueada
    // inmediatamente, incluso antes de recibir la réplica de red.
    public bool IsTerrainBeingManipulated =>
        isGrabbedLocally || terrenoEnManipulacion.Value;

    // Para validaciones autoritativas del servidor.
    public bool IsTerrainLockedByNetwork =>
        terrenoEnManipulacion.Value;

    public override void OnNetworkSpawn()
    {
        terrenoEnManipulacion.OnValueChanged +=
            OnManipulationChanged;

        if (IsServer)
        {
            netPos.Value = transform.position;
            netRot.Value = transform.rotation;
            netScale.Value = transform.localScale;

            NetworkManager.OnClientDisconnectCallback +=
                OnClientDisconnected;
        }
        else
        {
            transform.position = netPos.Value;
            transform.rotation = netRot.Value;
            transform.localScale = netScale.Value;
        }
    }

    public override void OnNetworkDespawn()
    {
        terrenoEnManipulacion.OnValueChanged -=
            OnManipulationChanged;

        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientDisconnectCallback -=
                OnClientDisconnected;
        }
    }

    private void OnManipulationChanged(
        bool previousValue,
        bool newValue)
    {
        OnTerrainManipulationStateChanged?.Invoke(newValue);
    }

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

    [Rpc(
        SendTo.Server,
        InvokePermission = RpcInvokePermission.Everyone)]
    private void UpdateTransformServerRpc(
        Vector3 pos,
        Quaternion rot,
        Vector3 scale)
    {
        netPos.Value = pos;
        netRot.Value = rot;
        netScale.Value = scale;
    }

    // =========================================================
    // INICIO DE MANIPULACIÓN
    // =========================================================

    public void OnGrabLocally()
    {
        localGrabCount++;

        // Si usa dos manos, solo el primer agarre
        // solicita el bloqueo.
        if (localGrabCount > 1)
            return;

        isGrabbedLocally = true;

        if (IsSpawned)
            IniciarManipulacionServerRpc();
    }

    [Rpc(
        SendTo.Server,
        InvokePermission = RpcInvokePermission.Everyone)]
    private void IniciarManipulacionServerRpc(
        RpcParams rpcParams = default)
    {
        ulong clientId =
            rpcParams.Receive.SenderClientId;

        clientesManipulando.Add(clientId);

        terrenoEnManipulacion.Value =
            clientesManipulando.Count > 0;
    }

    // =========================================================
    // FIN DE MANIPULACIÓN
    // =========================================================

    public void OnReleaseLocally()
    {
        localGrabCount =
            Mathf.Max(0, localGrabCount - 1);

        // Todavía existe otra mano sujetando.
        if (localGrabCount > 0)
            return;

        if (!isGrabbedLocally)
            return;

        isGrabbedLocally = false;

        if (IsSpawned)
        {
            // La transformación final y la liberación
            // se realizan juntas en el servidor.
            FinalizarManipulacionServerRpc(
                transform.position,
                transform.rotation,
                transform.localScale);
        }
    }

    [Rpc(
        SendTo.Server,
        InvokePermission = RpcInvokePermission.Everyone)]
    private void FinalizarManipulacionServerRpc(
        Vector3 finalPos,
        Quaternion finalRot,
        Vector3 finalScale,
        RpcParams rpcParams = default)
    {
        // Primero se consolida la transformación final.
        netPos.Value = finalPos;
        netRot.Value = finalRot;
        netScale.Value = finalScale;

        ulong clientId =
            rpcParams.Receive.SenderClientId;

        clientesManipulando.Remove(clientId);

        // Solo se habilitan marcadores cuando ningún
        // usuario continúa manipulando.
        terrenoEnManipulacion.Value =
            clientesManipulando.Count > 0;
    }

    // Evita que el bloqueo quede activo si un usuario
    // se desconecta mientras sostenía el terreno.
    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer)
            return;

        if (clientesManipulando.Remove(clientId))
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
        if (IsServer)
        {
            netPos.Value = worldPos;
            netRot.Value = worldRot;
        }
    }
}