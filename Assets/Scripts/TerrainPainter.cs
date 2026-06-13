using UnityEngine;

public class TerrainPainter : MonoBehaviour
{
    // REFACTOR: Cambiado a Color32. Usa 4 bytes en lugar de 16.
    public Color32 paintColor = new Color32(255, 0, 0, 255);
    [Range(1, 20)] public int brushSize = 5;

    private Texture2D dynamicTexture;
    private Color32[] pixelsOriginales; // REFACTOR: Arreglo Color32
    private Renderer terrainRenderer;

    // --- VARIABLES DE OPTIMIZACIÓN ---
    private Color32[] brushColorsCache; // REFACTOR: Arreglo Color32
    private int cachedBrushSize = -1;

    void Start()
    {
        terrainRenderer = GetComponent<Renderer>();
        // Añadida validación de material para evitar NullReferenceExceptions
        if (terrainRenderer != null && terrainRenderer.material.mainTexture != null)
        {
            Texture2D originalTex = (Texture2D)terrainRenderer.material.mainTexture;
            
            // REFACTOR: GetPixels32 es abismalmente más rápido y ligero que GetPixels
            pixelsOriginales = originalTex.GetPixels32(); 

            // REFACTOR: Forzamos formato RGBA32 y el 'false' final deshabilita la creación de MipMaps dinámicos
            dynamicTexture = new Texture2D(originalTex.width, originalTex.height, TextureFormat.RGBA32, false);
            dynamicTexture.SetPixels32(pixelsOriginales);
            dynamicTexture.Apply(false); // REFACTOR: Apply(false) evita recalcular MipMaps
            
            terrainRenderer.material.mainTexture = dynamicTexture;
            
            ActualizarCachePincel();
        }
    }

    private void ActualizarCachePincel()
    {
        int anchoBloque = (brushSize * 2) + 1;
        brushColorsCache = new Color32[anchoBloque * anchoBloque];
        for (int i = 0; i < brushColorsCache.Length; i++)
        {
            brushColorsCache[i] = paintColor;
        }
        cachedBrushSize = brushSize;
    }

    public void PaintAt(Vector2 textureCoord)
    {
        if (dynamicTexture == null) return;

        if (brushSize != cachedBrushSize) ActualizarCachePincel();

        int x = (int)(textureCoord.x * dynamicTexture.width);
        int y = (int)(textureCoord.y * dynamicTexture.height);

        int anchoBloque = (brushSize * 2) + 1;
        int inicioX = x - brushSize;
        int inicioY = y - brushSize;

        bool tocaBorde = inicioX < 0 || inicioY < 0 || 
                         inicioX + anchoBloque >= dynamicTexture.width || 
                         inicioY + anchoBloque >= dynamicTexture.height;

        if (tocaBorde)
        {
            PintarBordeSeguro(x, y);
        }
        else
        {
            // REFACTOR: Usamos SetPixels32
            dynamicTexture.SetPixels32(inicioX, inicioY, anchoBloque, anchoBloque, brushColorsCache);
            dynamicTexture.Apply(false); // CRÍTICO: false para evitar caída de frames en VR
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
        dynamicTexture.Apply(false); // CRÍTICO: false
    }

    public void ResetTexture()
    {
        if (dynamicTexture != null && pixelsOriginales != null)
        {
            dynamicTexture.SetPixels32(pixelsOriginales);
            dynamicTexture.Apply(false); // CRÍTICO: false
        }
    }
}