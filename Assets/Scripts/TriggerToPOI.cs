using UnityEngine;
using UnityEngine.InputSystem;

public class TriggerToPOI : MonoBehaviour
{
    // =========================================================
    // REFERENCIAS
    // =========================================================

    [Header("Referencias")]
    public InteractionManager interactionManager;

    [Tooltip("El punto de origen desde donde sale el láser del controlador. Si se deja vacío, usará la posición de este mismo objeto.")]
    public Transform origenRayo;

    // =========================================================
    // CONFIGURACIÓN DE RAYO Y CAPAS
    // =========================================================

    [Header("Configuración del Rayo Nativo")]
    public float distanciaRayo = 100f;
    public LayerMask capaTerreno;
    public LayerMask capaUI; // Nueva capa para detectar tus menús físicos

    // =========================================================
    // INPUT
    // =========================================================

    [Header("Configuración de Input")]
    public InputActionProperty triggerAction;

    // =========================================================
    // ESTADO
    // =========================================================

    // Estado físico real del trigger durante el frame anterior.
    private bool wasPhysicallyPressed = false;

    // Si esta pulsación tocó UI, queda reservada para la UI
    // hasta soltar físicamente el trigger.
    private bool uiConsumedThisPress = false;

    // =========================================================
    // AWAKE
    // =========================================================

    private void Awake()
    {
        if (origenRayo == null)
        {
            // Fallback: Si se te olvida asignarlo, usará el objeto donde pongas el script
            origenRayo = transform; 
        }
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        float triggerValue = triggerAction.action != null
            ? triggerAction.action.ReadValue<float>()
            : 0f;

        bool isPhysicallyPressed = triggerValue > 0.5f;

        // -----------------------------------------------------
        // GATILLO FÍSICAMENTE PRESIONADO
        // -----------------------------------------------------
        if (isPhysicallyPressed)
        {
            ProcesarTriggerPresionado();
        }

        // -----------------------------------------------------
        // LIBERACIÓN FÍSICA REAL
        // -----------------------------------------------------
        if (!isPhysicallyPressed && wasPhysicallyPressed)
        {
            if (interactionManager != null)
            {
                interactionManager.ProcesarEntrada(default, false);
            }

            // Finaliza la captura de UI.
            uiConsumedThisPress = false;
        }

        wasPhysicallyPressed = isPhysicallyPressed;
    }

    // =========================================================
    // PROCESAR PULSACIÓN
    // =========================================================

    private void ProcesarTriggerPresionado()
    {
        if (interactionManager == null || origenRayo == null)
        {
            return;
        }

        // -----------------------------------------------------
        // LANZAMIENTO DEL RAYO FÍSICO NATIVO
        // -----------------------------------------------------
        
        // Optimización: Solo evaluamos colisiones contra el Terreno y la UI
        LayerMask mascaraCombinada = capaTerreno | capaUI;

        bool hayImpacto = Physics.Raycast(
            origenRayo.position, 
            origenRayo.forward, 
            out RaycastHit hit, 
            distanciaRayo,
            mascaraCombinada
        );

        if (!hayImpacto)
        {
            return;
        }

        // =====================================================
        // UI TIENE PRIORIDAD ESPACIAL
        // =====================================================

        bool hitUI = EsCapaUI(hit.collider.gameObject.layer);

        if (hitUI)
        {
            // Solo necesitamos ejecutar la cancelación una vez por pulsación física.
            if (!uiConsumedThisPress)
            {
                interactionManager.CancelarMarcacionPorUI();
                uiConsumedThisPress = true;
            }

            // MUY IMPORTANTE: NO llamar ProcesarEntrada(false). 
            // El usuario no ha soltado el gatillo.
            return;
        }

        // =====================================================
        // ESTA PULSACIÓN YA FUE CAPTURADA POR UI
        // =====================================================

        // Aunque el usuario arrastre el rayo desde el botón hacia el terreno sin soltar,
        // no se permite crear un POI o lazo.
        if (uiConsumedThisPress)
        {
            return;
        }

        // =====================================================
        // IMPACTO 3D (TERRENO)
        // =====================================================

        if (EsCapaTerreno(hit.collider.gameObject.layer))
        {
            interactionManager.ProcesarEntrada(hit, true);
        }
    }

    // =========================================================
    // VALIDACIÓN DE CAPAS
    // =========================================================

    private bool EsCapaTerreno(int layer)
    {
        return (capaTerreno.value & (1 << layer)) != 0;
    }

    private bool EsCapaUI(int layer)
    {
        return (capaUI.value & (1 << layer)) != 0;
    }
}