using UnityEngine;
using UnityEngine.InputSystem;

public class MarkerSelectorVR : MonoBehaviour
{
    [Header("Referencias")]
    public InteractionManager interactionManager;
    public Transform origenRayoIzquierdo;

    [Header("Configuración de Input")]
    [Tooltip("DEBE SER ESTRICTAMENTE EL INPUT DE LA MANO IZQUIERDA (Ej: XRI LeftHand/Activate)")]
    public InputActionProperty triggerIzquierdoAction;

    [Header("Configuración del Rayo")]
    public float distanciaRayo = 100f;
    [Tooltip("La capa donde se instancian tus banderas y lazos (Usualmente 'Default')")]
    public LayerMask capaMarcadores; 

    private bool wasPressed = false;

    private void Update()
    {
        if (triggerIzquierdoAction.action == null) return;

        float triggerVal = triggerIzquierdoAction.action.ReadValue<float>();
        bool isPressed = triggerVal > 0.5f;

        if (isPressed && !wasPressed)
        {
            // Debugging para confirmar que no hay cruce de inputs
            Debug.Log("[MarkerSelectorVR] Acción de Selección (Mano Izquierda) detectada.");
            DispararRayoSeleccion();
        }
        wasPressed = isPressed;
    }

    private void DispararRayoSeleccion()
    {
        if (interactionManager == null || origenRayoIzquierdo == null) return;

        // Disparamos el rayo desde la mano izquierda
        if (Physics.Raycast(origenRayoIzquierdo.position, origenRayoIzquierdo.forward, out RaycastHit hit, distanciaRayo, capaMarcadores))
        {
            MarkerIdentity identity = hit.collider.GetComponent<MarkerIdentity>();
            if (identity == null)
            {
                identity = hit.collider.GetComponentInParent<MarkerIdentity>();
            }

            if (identity != null)
            {
                // Si tocamos un marcador, lo seleccionamos
                interactionManager.SeleccionarMarcador(identity.markerID);
            }
            else
            {
                // Si tocamos otra cosa en esta capa que no es un POI/Lazo, deseleccionamos
                interactionManager.DeseleccionarMarcador();
            }
        }
        else
        {
            // Si disparamos al aire o no tocamos nada válido, deseleccionamos
            interactionManager.DeseleccionarMarcador();
        }
    }
}