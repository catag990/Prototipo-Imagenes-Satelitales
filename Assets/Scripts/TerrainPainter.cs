using UnityEngine;

public class TerrainPainter : MonoBehaviour
{
    public Color paintColor = Color.red;
    [Range(1, 20)] public int brushSize = 5;

    private Texture2D dynamicTexture;
    private Color[] pixelsOriginales;
    private Renderer terrainRenderer;

    // --- VARIABLES DE OPTIMIZACIÓN ---
    private Color[] brushColorsCache;
    private int cachedBrushSize = -1;

    void Start()
    {
        terrainRenderer = GetComponent<Renderer>() ?? GetComponentInChildren<Renderer>();
        if (terrainRenderer != null)
        {
            Texture2D originalTex = (Texture2D)terrainRenderer.material.mainTexture;
            pixelsOriginales = originalTex.GetPixels(); // Guardamos el estado inicial

            dynamicTexture = new Texture2D(originalTex.width, originalTex.height);
            dynamicTexture.SetPixels(pixelsOriginales);
            dynamicTexture.Apply();
            terrainRenderer.material.mainTexture = dynamicTexture;
            
            // Inicializamos la caché del pincel
            ActualizarCachePincel();
        }
    }

    // Precalcula el bloque de color UNA SOLA VEZ, ahorrando miles de ciclos de CPU
    private void ActualizarCachePincel()
    {
        int anchoBloque = (brushSize * 2) + 1;
        brushColorsCache = new Color[anchoBloque * anchoBloque];
        for (int i = 0; i < brushColorsCache.Length; i++)
        {
            brushColorsCache[i] = paintColor;
        }
        cachedBrushSize = brushSize;
    }

    public void PaintAt(Vector2 textureCoord)
    {
        if (dynamicTexture == null) return;

        // Si cambiaste el tamaño del pincel en el Inspector, se actualiza la caché
        if (brushSize != cachedBrushSize) ActualizarCachePincel();

        int x = (int)(textureCoord.x * dynamicTexture.width);
        int y = (int)(textureCoord.y * dynamicTexture.height);

        int anchoBloque = (brushSize * 2) + 1;
        int inicioX = x - brushSize;
        int inicioY = y - brushSize;

        // Validamos si el pincel está tocando el borde exacto de la imagen
        bool tocaBorde = inicioX < 0 || inicioY < 0 || 
                         inicioX + anchoBloque >= dynamicTexture.width || 
                         inicioY + anchoBloque >= dynamicTexture.height;

        if (tocaBorde)
        {
            // MÉTODO LENTO (Fallback): Solo se usa en el 1% del mapa (los bordes) 
            // para evitar errores de salida de índice (Out of Bounds).
            PintarBordeSeguro(x, y);
        }
        else
        {
            // MÉTODO HIPER-OPTIMIZADO: Estampa el bloque entero de memoria de un solo golpe.
            dynamicTexture.SetPixels(inicioX, inicioY, anchoBloque, anchoBloque, brushColorsCache);
            dynamicTexture.Apply();
        }
    }

    private void PintarBordeSeguro(int x, int y)
    {
        for (int i = -brushSize; i <= brushSize; i++)
        {
            for (int j = -brushSize; j <= brushSize; j++)
            {
                int px = Mathf.Clamp(x + i, 0, dynamicTexture.width - 1);
                int py = Mathf.Clamp(y + j, 0, dynamicTexture.height - 1);
                dynamicTexture.SetPixel(px, py, paintColor);
            }
        }
        dynamicTexture.Apply();
    }

    public void ResetTexture()
    {
        if (dynamicTexture != null && pixelsOriginales != null)
        {
            // SetPixels es instantáneo comparado con un ciclo for
            dynamicTexture.SetPixels(pixelsOriginales);
            dynamicTexture.Apply();
        }
    }
}