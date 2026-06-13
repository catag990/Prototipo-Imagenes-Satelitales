using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Script quirúrgico para sincronizar Posición, Rotación y Escala del Terreno.
/// Corregido: Elimina el "Feedback Loop" de red usando estados de agarre explícitos.
/// </summary>
public class NetworkTerrainSync : NetworkBehaviour
{
    private NetworkVariable<Vector3> netPos = new NetworkVariable<Vector3>();
    private NetworkVariable<Quaternion> netRot = new NetworkVariable<Quaternion>();
    private NetworkVariable<Vector3> netScale = new NetworkVariable<Vector3>(Vector3.one);

    // --- NUEVO: Bandera de Control Local ---
    private bool isGrabbedLocally = false;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            netPos.Value = transform.localPosition;
            netRot.Value = transform.localRotation;
            netScale.Value = transform.localScale;
        }
        else
        {
            // LATE-JOIN: Copia el estado exacto de la red instantáneamente
            transform.localPosition = netPos.Value;
            transform.localRotation = netRot.Value;
            transform.localScale = netScale.Value;
        }
    }

    void Update()
    {
        if (!IsSpawned) return;

        // 1. Si TÚ tienes agarrado el mapa físicamente, tú dictas la posición a la red
        if (isGrabbedLocally)
        {
            UpdateTransformServerRpc(transform.localPosition, transform.localRotation, transform.localScale);
        }
        // 2. Si NO lo tienes agarrado, tu mapa se interpola hacia donde diga la red
        else
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, netPos.Value, Time.deltaTime * 10f);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, netRot.Value, Time.deltaTime * 10f);
            transform.localScale = Vector3.Lerp(transform.localScale, netScale.Value, Time.deltaTime * 10f);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void UpdateTransformServerRpc(Vector3 pos, Quaternion rot, Vector3 scale)
    {
        netPos.Value = pos;
        netRot.Value = rot;
        netScale.Value = scale;
    }

    // --- MÉTODOS PARA EL XR GRAB INTERACTABLE ---
    public void OnGrabLocally()
    {
        isGrabbedLocally = true;
    }

    public void OnReleaseLocally()
    {
        isGrabbedLocally = false;
    }
}