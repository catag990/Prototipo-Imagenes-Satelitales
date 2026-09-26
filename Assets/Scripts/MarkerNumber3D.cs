using UnityEngine;
using TMPro;

public class MarkerNumber3D : MonoBehaviour
{
    [Tooltip("El componente de texto flotante de la bandera")]
    public TextMeshPro textComponent;

    // Se llama desde el InteractionManager cuando nace la bandera
    public void SetNumber(int number, Color colorTag)
    {
        if (textComponent != null)
        {
            textComponent.text = number.ToString();
            textComponent.color = colorTag;
        }
    }
}