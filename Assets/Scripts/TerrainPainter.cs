using UnityEngine;

public class TerrainPainter : MonoBehaviour
{
    public Color paintColor = Color.red;
    [Range(1, 20)] public int brushSize = 5;

    private Texture2D dynamicTexture;
    private Color[] pixelsOriginales;
    private Renderer terrainRenderer;

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
        }
    }

    public void PaintAt(RaycastHit hit)
    {
        if (dynamicTexture == null) return;
        int x = (int)(hit.textureCoord.x * dynamicTexture.width);
        int y = (int)(hit.textureCoord.y * dynamicTexture.height);

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
            dynamicTexture.SetPixels(pixelsOriginales);
            dynamicTexture.Apply();
        }
    }
}