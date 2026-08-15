using UnityEngine;
using System.Collections.Generic;

public class POIPlacementSystem : MonoBehaviour
{
    [Header("Configuración")]
    public GameObject poiPrefab;
    public LayerMask terrainLayer;

    private List<GameObject> listaDePines =
        new List<GameObject>();

    // =========================================================
    // COLOCACIÓN LOCAL
    // =========================================================

    public void PlacePOI(
        RaycastHit hit)
    {
        if (hit.collider == null)
        {
            return;
        }

        if (((1 << hit.collider.gameObject.layer)
             & terrainLayer) != 0)
        {
            GameObject newPOI =
                Instantiate(
                    poiPrefab,
                    hit.point,
                    Quaternion.FromToRotation(
                        Vector3.up,
                        hit.normal));

            newPOI.transform.SetParent(
                hit.transform,
                true);

            listaDePines.Add(
                newPOI);

            Debug.Log(
                "POI colocado exitosamente en: " +
                hit.point);
        }
        else
        {
            Debug.LogWarning(
                "El objeto golpeado no pertenece " +
                "a la capa del terreno.");
        }
    }

    // =========================================================
    // REGISTRO DE POI EXTERNO
    // =========================================================

    public void RegisterPOI(
        GameObject newPOI)
    {
        if (newPOI == null)
        {
            return;
        }

        // Evitar duplicados.
        if (!listaDePines.Contains(
            newPOI))
        {
            listaDePines.Add(
                newPOI);
        }
    }

    // =========================================================
    // ELIMINACIÓN INDIVIDUAL
    // =========================================================

    public void UnregisterPOI(
        GameObject poi)
    {
        // Limpiar referencias destruidas.
        listaDePines.RemoveAll(
            item => item == null);

        if (poi == null)
        {
            return;
        }

        listaDePines.Remove(
            poi);
    }

    // =========================================================
    // RESET GLOBAL
    // =========================================================

    public void ClearAllPOIs()
    {
        foreach (GameObject pin
                 in listaDePines)
        {
            if (pin != null)
            {
                Destroy(pin);
            }
        }

        listaDePines.Clear();
    }
}