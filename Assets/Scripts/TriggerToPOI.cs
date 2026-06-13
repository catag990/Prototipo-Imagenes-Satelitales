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
    
    [Tooltip("Asigna aquí el objeto desde donde sale el láser (ej. el Right Controller)")]
    public Transform rayOrigin;

    [Header("Configuración del Rayo")]
    public float distanciaRayo = 100f;
    
    [Tooltip("Selecciona aquí SOLO la capa de tu terreno para evitar colisiones con la UI")]
    public LayerMask capaTerreno;

    [Header("Configuración de Input")]
    public InputActionProperty triggerAction;

    // --- OPTIMIZACIÓN (Evitar spam en Update) ---
    private bool wasPressed = false;

    void Update()
    {
        float triggerValue = triggerAction.action != null ? triggerAction.action.ReadValue<float>() : 0f;
        bool isPressed = triggerValue > 0.5f;

        // REFACTOR: Solo ejecutamos el Raycast físico si el gatillo está intencionalmente presionado.
        // Esto ahorra 90 cálculos de colisión por segundo cuando el usuario está inactivo.
        if (isPressed)
        {
            if (rayOrigin != null && Physics.Raycast(rayOrigin.position, rayOrigin.forward, out RaycastHit hit, distanciaRayo, capaTerreno))
            {
                interactionManager.ProcesarEntrada(hit, true);
            }
        }
        // REFACTOR: Si se soltó el gatillo en este frame, enviamos la señal de "falso" UNA SOLA VEZ.
        else if (wasPressed)
        {
            interactionManager.ProcesarEntrada(new RaycastHit(), false);
        }

        // Guardamos el estado para compararlo en el siguiente frame
        wasPressed = isPressed; 
    }
}