using UnityEngine;
using UnityEngine.InputSystem;

public class MarkerSelectorVR : MonoBehaviour
{
    [Header("Referencias")]
    public InteractionManager interactionManager;
    public Transform origenRayoIzquierdo;

    [Header("Configuración de Input")]
    [Tooltip("El InputAction del gatillo del controlador izquierdo")]
    public InputActionProperty triggerIzquierdoAction;

    [Header("Configuración del Rayo")]
    public float distanciaRayo = 100f;
    [Tooltip("La capa donde se instancian tus banderas y lazos (Usualmente 'Default')")]
    public LayerMask capaMarcadores; 

    private bool wasPressed = false;

    private void Update()
    {
        float triggerVal = triggerIzquierdoAction.action != null ? triggerIzquierdoAction.action.ReadValue<float>() : 0f;
        bool isPressed = triggerVal > 0.5f;

        if (isPressed && !wasPressed)
        {
            DispararRayoSeleccion();
        }
        wasPressed = isPressed;
    }

    private void DispararRayoSeleccion()
    {
        if (interactionManager == null || origenRayoIzquierdo == null) return;

        // Disparar rayo desde la mano izquierda
        if (Physics.Raycast(origenRayoIzquierdo.position, origenRayoIzquierdo.forward, out RaycastHit hit, distanciaRayo, capaMarcadores))
        {
            // Buscar el carnet de identidad en el objeto golpeado o en sus padres
            MarkerIdentity identity = hit.collider.GetComponent<MarkerIdentity>();
            if (identity == null)
            {
                identity = hit.collider.GetComponentInParent<MarkerIdentity>();
            }

            if (identity != null)
            {
                interactionManager.SeleccionarMarcador(identity.markerID);
            }
        }
    }
}