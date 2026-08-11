using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Script encargado de gestionar la entrada del controlador para 
/// instanciar POIs o activar el modo de pintado, optimizado para VR.
/// </summary>
public class TriggerToPOI : MonoBehaviour
{
    [Header("Referencias")]
    public InteractionManager interactionManager;
    
    [Tooltip("Objeto desde donde sale el láser")]
    public Transform rayOrigin;

    [Header("Configuración del Rayo")]
    public float distanciaRayo = 100f;
    
    [Tooltip("La capa del terreno")]
    public LayerMask capaTerreno;

    [Tooltip("Capa de los menús")]
    public LayerMask capaUI;

    [Header("Configuración de Input")]
    public InputActionProperty triggerAction;

    private bool wasPressed = false;

    void Update()
    {
        float triggerValue = triggerAction.action != null ? triggerAction.action.ReadValue<float>() : 0f;
        bool isPressed = triggerValue > 0.5f;

        if (isPressed)
        {
            // Se une la capa del terreno y la de la UI para que el rayo pueda chocar con ambas
            LayerMask capaTerrenoYUI = capaTerreno | capaUI;

            // Se dispara un único rayo. Observa el primero que impactó.
            if (rayOrigin != null && Physics.Raycast(rayOrigin.position, rayOrigin.forward, out RaycastHit hit, distanciaRayo, capaTerrenoYUI))
            {
                // Si es capa UI
                if (((1 << hit.collider.gameObject.layer) & capaUI) != 0)
                {
                    // Simulación que el gatillo no está presionado para proteger el terreno.
                    isPressed = false; 
                }
                else
                {
                    // Hit al terreno de forma segura
                    interactionManager.ProcesarEntrada(hit, true);
                }
            }
        }
        
        if (!isPressed && wasPressed)
        {
            interactionManager.ProcesarEntrada(new RaycastHit(), false);
        }

        // Se guarda el estado para compararlo en el siguiente frame
        wasPressed = isPressed; 
    }
}