using UnityEngine;
using System.Collections.Generic;

public enum ToolMode { POI, Paint }

public class InteractionManager : MonoBehaviour
{
    public ToolMode currentTool = ToolMode.POI;
    public POIPlacementSystem poiSystem;
    public TerrainPainter painter;
    
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
                    poiSystem.PlacePOI(hit);
                    yaPuseUnPOI = true;
                }
            }
        }
        else
        {
            yaPuseUnPOI = false; 
        }
    }

    public void ResetEnvironment()
    {
        // 1. Limpiar todos los POIs
        poiSystem.ClearAllPOIs();
        // 2. Resetear la textura del terreno
        painter.ResetTexture();
        Debug.Log("Entorno reseteado correctamente.");
    }
}