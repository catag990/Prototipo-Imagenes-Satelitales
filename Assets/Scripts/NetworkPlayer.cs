using UnityEngine;
using Unity.Netcode;

public class NetworkPlayer : NetworkBehaviour
{
    public Transform root;
    public Transform head;
    public Transform leftHand;
    public Transform rightHand;

    // --- OPTIMIZACIÓN DE RED ---
    [Header("Umbrales de Tolerancia")]
    public float positionThreshold = 0.005f; // Ignorar micromovimientos < 5 milímetros
    public float rotationThreshold = 0.5f;   // Ignorar microrotaciones < 0.5 grados

    void Update()
    {
        // Añadida validación de VRRigReferences nulo para evitar crashes en conexiones tardías (Late-Joining)
        if (IsOwner && VRRigReferences.Singleton != null)
        {
            UpdateTransformSync(root, VRRigReferences.Singleton.root);
            UpdateTransformSync(head, VRRigReferences.Singleton.head);
            UpdateTransformSync(leftHand, VRRigReferences.Singleton.leftHand);
            UpdateTransformSync(rightHand, VRRigReferences.Singleton.rightHand);
        }
    }

    // Método encapsulado para aplicar la regla de Simplicity First (evita repetir código 8 veces)
    private void UpdateTransformSync(Transform networkItem, Transform rigItem)
    {
        // Solo actualiza (y por ende gasta red) si el movimiento fue intencional/significativo
        if (Vector3.Distance(networkItem.position, rigItem.position) > positionThreshold)
        {
            networkItem.position = rigItem.position;
        }

        if (Quaternion.Angle(networkItem.rotation, rigItem.rotation) > rotationThreshold)
        {
            networkItem.rotation = rigItem.rotation;
        }
    }
}