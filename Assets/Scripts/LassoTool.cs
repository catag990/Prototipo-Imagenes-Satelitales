using UnityEngine;
using System.Collections.Generic;

public class LassoTool : MonoBehaviour
{
    [Header("Configuración del Lazo")]
    [Tooltip("Distancia mínima entre puntos (ej. 0.1 = 10cm reales)")]
    public float distanciaMinima = 0.1f; 
    public Material materialLinea; // Asigna un material "Unlit/Color" rojo o visible
    public float anchoLinea = 0.02f;

    private LineRenderer currentLine;
    private List<Vector3> puntosLazo = new List<Vector3>();
    private bool isDrawing = false;

    public void IniciarLazo(Vector3 puntoInicial)
    {
        isDrawing = true;
        puntosLazo.Clear();
        puntosLazo.Add(puntoInicial);

        // Crear contenedor puramente visual y temporal
        GameObject lineaObj = new GameObject("Lazo_Preview_Local");
        currentLine = lineaObj.AddComponent<LineRenderer>();
        currentLine.material = materialLinea;
        currentLine.startWidth = anchoLinea;
        currentLine.endWidth = anchoLinea;
        currentLine.positionCount = 1;
        currentLine.SetPosition(0, puntoInicial);
    }

    public void ActualizarLazo(Vector3 nuevoPunto)
    {
        if (!isDrawing || currentLine == null) return;

        // Threshold: Solo guardamos el punto si te moviste más de X centímetros
        Vector3 ultimoPunto = puntosLazo[puntosLazo.Count - 1];
        if (Vector3.Distance(ultimoPunto, nuevoPunto) >= distanciaMinima)
        {
            puntosLazo.Add(nuevoPunto);
            currentLine.positionCount = puntosLazo.Count;
            currentLine.SetPosition(puntosLazo.Count - 1, nuevoPunto);
        }
    }

    public Vector3[] TerminarLazo()
    {
        isDrawing = false;
        if (currentLine != null)
        {
            Destroy(currentLine.gameObject); // Borramos el preview visual
            currentLine = null;
        }
        return puntosLazo.ToArray(); // Devolvemos el paquete compacto
    }
}