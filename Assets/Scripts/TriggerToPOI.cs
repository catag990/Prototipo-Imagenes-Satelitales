using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class TriggerToPOI : MonoBehaviour
{
    public InteractionManager interactionManager;
    public XRRayInteractor rayInteractor;
    public InputActionProperty triggerAction;

    void Update()
    {
        bool isPressed = triggerAction.action.IsPressed() || 
                        (Keyboard.current != null && Keyboard.current.tKey.isPressed);

        if (rayInteractor != null && rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            interactionManager.ProcesarEntrada(hit, isPressed);
        }
        else
        {
            interactionManager.ProcesarEntrada(new RaycastHit(), false);
        }
    }
}