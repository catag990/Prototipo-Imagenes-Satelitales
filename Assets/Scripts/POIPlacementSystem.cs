using UnityEngine;
using System.Collections.Generic;

public class POIPlacementSystem : MonoBehaviour
{
    [Header("Configuración")]
    public GameObject poiPrefab; 
    public LayerMask terrainLayer;
    
    // Lista para gestionar los pines y permitir borrarlos después
    private List<GameObject> listaDePines = new List<GameObject>();

    // Ahora este método recibe el 'hit' directamente desde el InteractionManager
    public void PlacePOI(RaycastHit hit)
    {
        // Validar capa: El objeto golpeado debe pertenecer a 'terrainLayer'
        if (((1 << hit.collider.gameObject.layer) & terrainLayer) != 0)
        {
            // Instanciar pin orientado a la normal de la superficie
            GameObject newPOI = Instantiate(poiPrefab, hit.point, Quaternion.FromToRotation(Vector3.up, hit.normal));
            
            // Anclaje: Hacer que el pin se mueva con el terreno
            newPOI.transform.SetParent(hit.transform, true);
            
            // Guardar en lista para gestión futura (Borrar Todo)
            listaDePines.Add(newPOI);
            
            Debug.Log("POI colocado exitosamente en: " + hit.point);
        }
        else
        {
            Debug.LogWarning("El objeto golpeado no pertenece a la capa del terreno.");
        }
    }

    // Añade este método para registrar banderas creadas externamente
    public void RegisterPOI(GameObject newPOI)
    {
        // Anclarlo al contenedor si es necesario (opcional)
        // newPOI.transform.SetParent(this.transform, true);
        listaDePines.Add(newPOI);
    }

    // Asegúrate de que tu método de limpiar incluya el .Clear() al final
    public void ClearAllPOIs()
    {
        foreach (GameObject pin in listaDePines)
        {
            if (pin != null) Destroy(pin);
        }
        listaDePines.Clear(); // CRÍTICO: Vaciar la memoria de la lista
    }
}