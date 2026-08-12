using UnityEngine;
using System.Collections.Generic;

public class LassoTool : MonoBehaviour
{
    [Header("Configuración del Lazo")]
    [Tooltip(
        "Distancia mínima entre puntos " +
        "(ej. 0.1 = 10cm reales)")]
    public float distanciaMinima =
        0.1f;

    public Material materialLinea;

    public float anchoLinea =
        0.02f;

    private LineRenderer currentLine;

    private List<Vector3> puntosLazo =
        new List<Vector3>();

    private bool isDrawing =
        false;

    // =========================================================
    // INICIAR LAZO
    // =========================================================

    public void IniciarLazo(
        Vector3 puntoInicial)
    {
        // Limpiar cualquier preview anterior
        // por seguridad.
        CancelarLazo();

        isDrawing =
            true;

        puntosLazo.Clear();

        puntosLazo.Add(
            puntoInicial);

        GameObject lineaObj =
            new GameObject(
                "Lazo_Preview_Local");

        currentLine =
            lineaObj.AddComponent<
                LineRenderer>();

        currentLine.material =
            materialLinea;

        currentLine.startWidth =
            anchoLinea;

        currentLine.endWidth =
            anchoLinea;

        currentLine.positionCount =
            1;

        currentLine.SetPosition(
            0,
            puntoInicial);
    }

    // =========================================================
    // ACTUALIZAR LAZO
    // =========================================================

    public void ActualizarLazo(
        Vector3 nuevoPunto)
    {
        if (!isDrawing ||
            currentLine == null)
        {
            return;
        }

        if (puntosLazo.Count == 0)
            return;

        Vector3 ultimoPunto =
            puntosLazo[
                puntosLazo.Count - 1];

        if (Vector3.Distance(
                ultimoPunto,
                nuevoPunto) <
            distanciaMinima)
        {
            return;
        }

        puntosLazo.Add(
            nuevoPunto);

        currentLine.positionCount =
            puntosLazo.Count;

        currentLine.SetPosition(
            puntosLazo.Count - 1,
            nuevoPunto);
    }

    // =========================================================
    // FINALIZAR LAZO
    // =========================================================

    public Vector3[] TerminarLazo()
    {
        if (!isDrawing)
        {
            return new Vector3[0];
        }

        isDrawing =
            false;

        Vector3[] resultado =
            puntosLazo.ToArray();

        if (currentLine != null)
        {
            Destroy(
                currentLine.gameObject);

            currentLine =
                null;
        }

        puntosLazo.Clear();

        return resultado;
    }

    // =========================================================
    // CANCELAR LAZO
    // =========================================================

    public void CancelarLazo()
    {
        isDrawing =
            false;

        puntosLazo.Clear();

        if (currentLine != null)
        {
            Destroy(
                currentLine.gameObject);

            currentLine =
                null;
        }
    }
}