using UnityEngine;
using System.Collections.Generic;

public enum ToolMode { POI, Paint }

public class InteractionManager : MonoBehaviour
{
    public ToolMode currentTool = ToolMode.POI;
    public POIPlacementSystem poiSystem;
    public TerrainPainter painter;
    
    // Referencia al prefab
    public GameObject flagPrefab;
    
    private bool yaPuseUnPOI = false;

    public void SetToolPOI() => currentTool = ToolMode.POI;
    public void SetToolPaint() => currentTool = ToolMode.Paint;

    public void ProcesarEntrada(RaycastHit hit, bool estaPresionado)
    {
        if (estaPresionado)
        {
            if (currentTool == ToolMode.Paint)
            {
                painter.PaintAt(hit);
            }
            else if (currentTool == ToolMode.POI)
            {
                if (!yaPuseUnPOI)
                {
                    InstanciarBandera(hit);
                    yaPuseUnPOI = true;
                }
            }
        }
        else
        {
            yaPuseUnPOI = false; 
        }
    }

    private void InstanciarBandera(RaycastHit hit)
    {
        Quaternion rotacionPerpendicular = Quaternion.FromToRotation(Vector3.up, hit.normal);
        GameObject nuevaBandera = Instantiate(flagPrefab, hit.point, rotacionPerpendicular);

        // Acceso al MeshRenderer para cambiar el color
        MeshRenderer renderer = nuevaBandera.GetComponentInChildren<MeshRenderer>();
        if (renderer != null)
        {
            // Cambiamos el color directamente en el material instanciado
            // Nota: Si usas URP, usa "_BaseColor". Si usas Standard, usa "_Color"
            renderer.material.SetColor("_BaseColor", Color.red); 
        }
        poiSystem.RegisterPOI(nuevaBandera);
    }

    public void ResetEnvironment()
    {
        poiSystem.ClearAllPOIs();
        painter.ResetTexture();
        Debug.Log("Entorno reseteado correctamente.");
    }
}