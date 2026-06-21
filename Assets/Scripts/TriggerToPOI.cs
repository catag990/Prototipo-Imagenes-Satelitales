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
    
    [Tooltip("Selecciona aquí SOLO la capa de tu terreno")]
    public LayerMask capaTerreno;

    // ---> NUEVA VARIABLE FÍSICA PARA BLOQUEAR LA UI <---
    [Tooltip("Selecciona aquí la capa de tus menús (usualmente 'UI')")]
    public LayerMask capaUI;

    [Header("Configuración de Input")]
    public InputActionProperty triggerAction;

    // --- OPTIMIZACIÓN (Evitar spam en Update) ---
    private bool wasPressed = false;

    void Update()
    {
        float triggerValue = triggerAction.action != null ? triggerAction.action.ReadValue<float>() : 0f;
        bool isPressed = triggerValue > 0.5f;

        if (isPressed)
        {
            // Unimos la capa del terreno y la de la UI para que el rayo pueda chocar con ambas
            LayerMask capaTerrenoYUI = capaTerreno | capaUI;

            // Disparamos un único rayo. Nos dirá qué fue lo primero que impactó.
            if (rayOrigin != null && Physics.Raycast(rayOrigin.position, rayOrigin.forward, out RaycastHit hit, distanciaRayo, capaTerrenoYUI))
            {
                // ¿Lo que golpeamos pertenece a la capa UI (el BoxCollider del Canvas)?
                if (((1 << hit.collider.gameObject.layer) & capaUI) != 0)
                {
                    // Golpeamos el menú. Simulamos que el gatillo no está presionado para proteger el terreno.
                    isPressed = false; 
                }
                else
                {
                    // Golpeamos el terreno de forma segura
                    interactionManager.ProcesarEntrada(hit, true);
                }
            }
        }
        
        // REFACTOR: Si se soltó el gatillo en este frame (o si pasamos de apuntar al terreno a la UI),
        // enviamos la señal de "falso" UNA SOLA VEZ para cortar la línea.
        if (!isPressed && wasPressed)
        {
            interactionManager.ProcesarEntrada(new RaycastHit(), false);
        }

        // Guardamos el estado para compararlo en el siguiente frame
        wasPressed = isPressed; 
    }
}