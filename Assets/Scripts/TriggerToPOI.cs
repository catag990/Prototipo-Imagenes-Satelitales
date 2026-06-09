using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Script encargado de gestionar la entrada del controlador para 
/// instanciar POIs o activar el modo de pintado, independiente de XRI.
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
    public InputActionProperty triggerAction; // Asigna aquí la acción de 'Activate Value'

    void Update()
    {
        // Leemos el valor analógico del gatillo
        float triggerValue = triggerAction.action != null ? triggerAction.action.ReadValue<float>() : 0f;
        bool isPressed = triggerValue > 0.5f;

        // Validamos que tengamos un origen desde donde disparar
        if (rayOrigin != null)
        {
            // Lanzamos nuestro propio rayo físico hacia adelante
            if (Physics.Raycast(rayOrigin.position, rayOrigin.forward, out RaycastHit hit, distanciaRayo, capaTerreno))
            {
                // Si golpea el terreno, enviamos los datos al Manager
                interactionManager.ProcesarEntrada(hit, isPressed);
            }
            else
            {
                // Si está apuntando al cielo o a la UI, no hay interacción con el terreno
                interactionManager.ProcesarEntrada(new RaycastHit(), false);
            }
        }
    }
}