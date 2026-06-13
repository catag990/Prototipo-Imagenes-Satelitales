using UnityEngine;
using Unity.Netcode;

public class NetworkTerrainSync : NetworkBehaviour
{
    private NetworkVariable<Vector3> netPos = new NetworkVariable<Vector3>();
    private NetworkVariable<Quaternion> netRot = new NetworkVariable<Quaternion>();
    private NetworkVariable<Vector3> netScale = new NetworkVariable<Vector3>(Vector3.one);

    private bool isGrabbedLocally = false;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // REFACTOR: Ahora usamos World Space, somos inmunes a bugs de jerarquía
            netPos.Value = transform.position;
            netRot.Value = transform.rotation;
            netScale.Value = transform.localScale;
        }
        else
        {
            transform.position = netPos.Value;
            transform.rotation = netRot.Value;
            transform.localScale = netScale.Value;
        }
    }

    void Update()
    {
        if (!IsSpawned) return;

        if (isGrabbedLocally)
        {
            UpdateTransformServerRpc(transform.position, transform.rotation, transform.localScale);
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, netPos.Value, Time.deltaTime * 10f);
            transform.rotation = Quaternion.Slerp(transform.rotation, netRot.Value, Time.deltaTime * 10f);
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

    // NUEVO: Método para que el Manager teletransporte el mapa a la mesa por la red
    public void ForceSnapToTable(Vector3 worldPos, Quaternion worldRot)
    {
        if (IsServer)
        {
            netPos.Value = worldPos;
            netRot.Value = worldRot;
        }
    }

    public void OnGrabLocally() => isGrabbedLocally = true;
    public void OnReleaseLocally() => isGrabbedLocally = false;
}