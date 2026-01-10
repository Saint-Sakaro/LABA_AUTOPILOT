using UnityEngine;

public class CloudTextureGenerator : MonoBehaviour
{
    public static Texture2D GenerateCloudTexture(int resolution = 256, float scale = 50f, int seed = 0)
    {
        Random.InitState(seed);
        
        Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.ARGB32, false, true);
        Color[] pixels = new Color[resolution * resolution];
        
        float noiseOffset = Random.Range(0f, 10000f);
        
        //Perlin Noise
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float xCoord = (float)x / resolution * scale + noiseOffset;
                float yCoord = (float)y / resolution * scale + noiseOffset;
                
                //FBM
                float value = 0f;
                float amplitude = 1f;
                float frequency = 1f;
                float maxValue = 0f;
                
                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * Mathf.PerlinNoise(xCoord * frequency, yCoord * frequency);
                    maxValue += amplitude;
                    amplitude *= 0.5f;
                    frequency *= 2f;
                }
                
                value /= maxValue;
                
                value = Mathf.Pow(value, 0.8f);
                
                float centerX = (float)x / resolution - 0.5f;
                float centerY = (float)y / resolution - 0.5f;
                float distFromCenter = Mathf.Sqrt(centerX * centerX + centerY * centerY);
                float centerFade = 1f - Mathf.Clamp01(distFromCenter * 2.5f);
                
                value *= centerFade;
                
                pixels[y * resolution + x] = new Color(1f, 1f, 1f, value);
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        texture.name = "GeneratedCloudTexture";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Trilinear;
        
        return texture;
    }
}