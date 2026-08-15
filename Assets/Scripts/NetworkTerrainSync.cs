using UnityEngine;
using Unity.Netcode;
using System;

public class NetworkTerrainSync : NetworkBehaviour
{
    // =========================================================
    // CONSTANTES
    // =========================================================

    // ulong.MaxValue representa que nadie posee
    // actualmente el lock del terreno.
    private const ulong NoManipulationOwner = ulong.MaxValue;

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
    // LOCK EXCLUSIVO DE MANIPULACIÓN
    // =========================================================

    // Solo puede existir UN ClientId propietario.
    private NetworkVariable<ulong> manipulationOwnerClientId =
        new NetworkVariable<ulong>(
            NoManipulationOwner);

    // Estado local.
    private bool isGrabbedLocally = false;

    // Permite que las dos manos DEL MISMO CLIENTE
    // utilicen el terreno sin liberar el lock
    // cuando solo una de ellas lo suelta.
    private int localGrabCount = 0;

    // Evita saturar al servidor con solicitudes
    // mientras se espera la concesión del lock.
    private float nextLockRequestTime = 0f;

    private const float LockRequestRetryInterval = 0.10f;

    // =========================================================
    // EVENTOS
    // =========================================================

    // Consumido por InteractionManager para
    // bloquear POIs y lazos.
    public event Action<bool>
        OnTerrainManipulationStateChanged;

    // Consumido por TerrainModeManager.
    //
    // Se dispara si este cliente intentó tomar
    // el terreno, pero otro cliente obtuvo
    // primero el lock.
    public event Action
        OnLocalManipulationRejected;

    // =========================================================
    // PROPIEDADES PÚBLICAS
    // =========================================================

    // Hay un propietario de red.
    public bool IsTerrainLockedByNetwork
    {
        get
        {
            return
                manipulationOwnerClientId.Value !=
                NoManipulationOwner;
        }
    }

    // Bloqueo para lógica local de POIs/lazos.
    //
    // Considera tanto una solicitud local todavía
    // pendiente como un propietario confirmado.
    public bool IsTerrainBeingManipulated
    {
        get
        {
            return
                isGrabbedLocally ||
                IsTerrainLockedByNetwork;
        }
    }

    // Este cliente posee el lock confirmado.
    public bool IsLocalClientManipulationOwner
    {
        get
        {
            if (!IsSpawned ||
                NetworkManager == null)
            {
                return false;
            }

            return
                manipulationOwnerClientId.Value ==
                NetworkManager.LocalClientId;
        }
    }

    // Otro cliente posee actualmente el terreno.
    public bool IsLockedByOtherClient
    {
        get
        {
            if (!IsSpawned ||
                NetworkManager == null)
            {
                return false;
            }

            return
                manipulationOwnerClientId.Value !=
                    NoManipulationOwner &&
                manipulationOwnerClientId.Value !=
                    NetworkManager.LocalClientId;
        }
    }

    public ulong ManipulationOwnerClientId
    {
        get
        {
            return manipulationOwnerClientId.Value;
        }
    }

    // =========================================================
    // NETWORK SPAWN
    // =========================================================

    public override void OnNetworkSpawn()
    {
        manipulationOwnerClientId.OnValueChanged +=
            OnManipulationOwnerChanged;

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
            AplicarTransformacionDeRedExacta();
        }
    }

    public override void OnNetworkDespawn()
    {
        manipulationOwnerClientId.OnValueChanged -=
            OnManipulationOwnerChanged;

        if (IsServer &&
            NetworkManager != null)
        {
            NetworkManager
                .OnClientDisconnectCallback -=
                OnClientDisconnected;
        }

        localGrabCount = 0;
        isGrabbedLocally = false;
    }

    // =========================================================
    // CAMBIO DE PROPIETARIO DEL LOCK
    // =========================================================

    private void OnManipulationOwnerChanged(
        ulong previousOwner,
        ulong newOwner)
    {
        bool terrenoManipulado =
            newOwner != NoManipulationOwner;

        // -----------------------------------------------------
        // CASO DE CARRERA
        //
        // Este cliente intentó agarrar el terreno,
        // pero el servidor concedió el lock
        // a otro cliente.
        // -----------------------------------------------------

        if (isGrabbedLocally &&
            NetworkManager != null &&
            newOwner != NoManipulationOwner &&
            newOwner != NetworkManager.LocalClientId)
        {
            RechazarManipulacionLocal();
        }

        // Si todavía tenemos el terreno físicamente
        // agarrado y el lock volvió a quedar libre,
        // puede reintentarse la solicitud.
        if (isGrabbedLocally &&
            newOwner == NoManipulationOwner)
        {
            nextLockRequestTime = 0f;
        }

        OnTerrainManipulationStateChanged?.Invoke(
            terrenoManipulado);
    }

    // =========================================================
    // SINCRONIZACIÓN DE TRANSFORMACIÓN
    // =========================================================

    private void Update()
    {
        if (!IsSpawned)
            return;

        // -----------------------------------------------------
        // CLIENTE QUE ESTÁ INTENTANDO MANIPULAR
        // -----------------------------------------------------

        if (isGrabbedLocally)
        {
            // Solo el propietario confirmado puede
            // modificar la transformación en red.
            if (IsLocalClientManipulationOwner)
            {
                UpdateTransformServerRpc(
                    transform.position,
                    transform.rotation,
                    transform.localScale);
            }
            else
            {
                // Si aparentemente sigue libre,
                // reintentar periódicamente.
                if (!IsLockedByOtherClient &&
                    Time.time >= nextLockRequestTime)
                {
                    SolicitarInicioManipulacionServerRpc();

                    nextLockRequestTime =
                        Time.time +
                        LockRequestRetryInterval;
                }
            }

            return;
        }

        // -----------------------------------------------------
        // CLIENTES QUE NO MANIPULAN
        // -----------------------------------------------------

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

    private void LateUpdate()
    {
        if (!IsSpawned)
            return;

        // Mientras el servidor todavía no haya
        // concedido el lock, el XR Grab no puede
        // desplazar realmente el terreno.
        //
        // Esto reduce también el movimiento visual
        // transitorio durante una carrera de agarre.
        if (isGrabbedLocally &&
            !IsLocalClientManipulationOwner)
        {
            AplicarTransformacionDeRedExacta();
        }
    }

    [Rpc(
        SendTo.Server,
        InvokePermission =
            RpcInvokePermission.Everyone)]
    private void UpdateTransformServerRpc(
        Vector3 pos,
        Quaternion rot,
        Vector3 scale,
        RpcParams rpcParams = default)
    {
        ulong senderClientId =
            rpcParams.Receive.SenderClientId;

        // BARRERA AUTORITATIVA.
        //
        // Aunque otro cliente consiguiera enviar
        // un RPC, el servidor lo descarta.
        if (manipulationOwnerClientId.Value !=
            senderClientId)
        {
            return;
        }

        netPos.Value = pos;
        netRot.Value = rot;
        netScale.Value = scale;
    }

    // =========================================================
    // INICIO DE MANIPULACIÓN LOCAL
    // =========================================================

    public bool OnGrabLocally()
    {
        if (!IsSpawned ||
            NetworkManager == null)
        {
            return false;
        }

        // Otro usuario ya tiene el lock.
        if (IsLockedByOtherClient)
        {
            return false;
        }

        localGrabCount++;

        // Segunda mano DEL MISMO CLIENTE.
        if (localGrabCount > 1)
        {
            return true;
        }

        isGrabbedLocally = true;

        // Bloqueo inmediato de POIs/lazos.
        OnTerrainManipulationStateChanged?.Invoke(
            true);

        nextLockRequestTime =
            Time.time +
            LockRequestRetryInterval;

        SolicitarInicioManipulacionServerRpc();

        return true;
    }

    // =========================================================
    // SOLICITAR LOCK AL SERVIDOR
    // =========================================================

    [Rpc(
        SendTo.Server,
        InvokePermission =
            RpcInvokePermission.Everyone)]
    private void SolicitarInicioManipulacionServerRpc(
        RpcParams rpcParams = default)
    {
        ulong senderClientId =
            rpcParams.Receive.SenderClientId;

        // Primer cliente que llega obtiene el lock.
        if (manipulationOwnerClientId.Value ==
            NoManipulationOwner)
        {
            manipulationOwnerClientId.Value =
                senderClientId;

            return;
        }

        // Si ya pertenece al mismo cliente,
        // no existe conflicto.
        if (manipulationOwnerClientId.Value ==
            senderClientId)
        {
            return;
        }

        // Si pertenece a otro cliente:
        // no se modifica el propietario.
    }

    // =========================================================
    // RECHAZAR AGARRE LOCAL
    // =========================================================

    private void RechazarManipulacionLocal()
    {
        localGrabCount = 0;
        isGrabbedLocally = false;

        AplicarTransformacionDeRedExacta();

        OnLocalManipulationRejected?.Invoke();
    }

    // =========================================================
    // LIBERACIÓN LOCAL
    // =========================================================

    public void OnReleaseLocally()
    {
        localGrabCount =
            Mathf.Max(
                0,
                localGrabCount - 1);

        // Todavía queda otra mano
        // del mismo cliente seleccionando.
        if (localGrabCount > 0)
            return;

        if (!isGrabbedLocally)
            return;

        isGrabbedLocally = false;

        if (IsSpawned)
        {
            FinalizarManipulacionServerRpc(
                transform.position,
                transform.rotation,
                transform.localScale);
        }
    }

    // =========================================================
    // LIBERACIÓN AUTORITATIVA
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
        ulong senderClientId =
            rpcParams.Receive.SenderClientId;

        // Solo el propietario puede liberar
        // y escribir el estado final.
        if (manipulationOwnerClientId.Value !=
            senderClientId)
        {
            return;
        }

        netPos.Value =
            finalPos;

        netRot.Value =
            finalRot;

        netScale.Value =
            finalScale;

        manipulationOwnerClientId.Value =
            NoManipulationOwner;
    }

    // =========================================================
    // DESCONEXIÓN DEL PROPIETARIO
    // =========================================================

    private void OnClientDisconnected(
        ulong clientId)
    {
        if (!IsServer)
            return;

        if (manipulationOwnerClientId.Value ==
            clientId)
        {
            manipulationOwnerClientId.Value =
                NoManipulationOwner;
        }
    }

    // =========================================================
    // SNAP A MESA FIJA
    // =========================================================

    public void ForceSnapToTable(
        Vector3 worldPos,
        Quaternion worldRot)
    {
        if (!IsServer)
            return;

        // No permitir un snap mientras
        // algún cliente manipula el terreno.
        if (IsTerrainLockedByNetwork)
            return;

        netPos.Value =
            worldPos;

        netRot.Value =
            worldRot;

        transform.position =
            worldPos;

        transform.rotation =
            worldRot;
    }

    // =========================================================
    // APLICACIÓN EXACTA
    // =========================================================

    private void AplicarTransformacionDeRedExacta()
    {
        transform.position =
            netPos.Value;

        transform.rotation =
            netRot.Value;

        transform.localScale =
            netScale.Value;
    }
}
