using UnityEngine;
using System.Collections.Generic;

public static class HillGenerator
{
    private struct Hill
    {
        public Vector2 position;
        public float radiusX;
        public float radiusZ;
        public float rotation;
        public float height;
    }

    private static Dictionary<Vector2Int, List<Hill>> hillCache = new Dictionary<Vector2Int, List<Hill>>();
    
    private const float HILL_SPACING = 900f;
    private const int SEED_OFFSET = 12345;

    public static float GetHeightAtPosition(Vector3 worldPos)
    {
        Vector2 pos = new Vector2(worldPos.x, worldPos.z);
        Vector2Int sectorCoord = GetSectorCoordinate(pos);
        
        if (!hillCache.ContainsKey(sectorCoord))
        {
            hillCache[sectorCoord] = GenerateHillsForSector(sectorCoord);
        }

        List<Hill> hills = hillCache[sectorCoord];
        float totalHeight = 0f;
        
        foreach (Hill hill in hills)
        {
            float distToHill = GetEllipseDistance(pos, hill);
            
            if (distToHill < 1f)
            {
                float heightFalloff = (1f - distToHill * distToHill) * (1f - distToHill * distToHill);
                totalHeight += hill.height * heightFalloff;
            }
        }
        
        return totalHeight;
    }

    private static float GetEllipseDistance(Vector2 pos, Hill hill)
    {
        Vector2 relPos = pos - hill.position;
        
        float cos = Mathf.Cos(-hill.rotation);
        float sin = Mathf.Sin(-hill.rotation);
        float rotX = relPos.x * cos - relPos.y * sin;
        float rotY = relPos.x * sin + relPos.y * cos;
        
        float distX = rotX / hill.radiusX;
        float distY = rotY / hill.radiusZ;
        float ellipseDist = Mathf.Sqrt(distX * distX + distY * distY);
        
        return ellipseDist;
    }

    private static List<Hill> GenerateHillsForSector(Vector2Int sectorCoord)
    {
        List<Hill> hills = new List<Hill>();
        
        float sectorCenterX = sectorCoord.x * HILL_SPACING;
        float sectorCenterY = sectorCoord.y * HILL_SPACING;
        
        for (float x = sectorCenterX - HILL_SPACING; x <= sectorCenterX + HILL_SPACING; x += HILL_SPACING)
        {
            for (float z = sectorCenterY - HILL_SPACING; z <= sectorCenterY + HILL_SPACING; z += HILL_SPACING)
            {
                Random.InitState(GetSeedForPosition(x, z));
                
                if (Random.value < 0.9f)
                {
                    float offsetX = Random.Range(-HILL_SPACING * 0.3f, HILL_SPACING * 0.3f);
                    float offsetZ = Random.Range(-HILL_SPACING * 0.3f, HILL_SPACING * 0.3f);
                    
                    float hillX = x + offsetX;
                    float hillZ = z + offsetZ;
                    
                    float radiusX = Random.Range(80f, 1000f);
                    float radiusZ = Random.Range(80f, 1000f);
                    float rotation = Random.Range(0f, Mathf.PI * 2f);
                    float height = Random.Range(30f, 200f);
                    
                    hills.Add(new Hill
                    {
                        position = new Vector2(hillX, hillZ),
                        radiusX = radiusX,
                        radiusZ = radiusZ,
                        rotation = rotation,
                        height = height
                    });
                }
            }
        }
        
        return hills;
    }

    private static Vector2Int GetSectorCoordinate(Vector2 pos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(pos.x / HILL_SPACING),
            Mathf.FloorToInt(pos.y / HILL_SPACING)
        );
    }

    private static int GetSeedForPosition(float x, float z)
    {
        int seedX = (int)(x / HILL_SPACING) * 73856093;
        int seedZ = (int)(z / HILL_SPACING) * 19349663;
        return (seedX ^ seedZ) + SEED_OFFSET;
    }

    /// <summary>
    /// Вычисляет нормаль поверхности в указанной позиции
    /// </summary>
    public static Vector3 GetSurfaceNormal(Vector3 position, float sampleDistance = 1f)
    {
        float centerHeight = GetHeightAtPosition(position);
        
        // Вычисляем высоты в четырех направлениях
        Vector3 right = position + Vector3.right * sampleDistance;
        right.y = GetHeightAtPosition(right);
        
        Vector3 forward = position + Vector3.forward * sampleDistance;
        forward.y = GetHeightAtPosition(forward);
        
        Vector3 left = position + Vector3.left * sampleDistance;
        left.y = GetHeightAtPosition(left);
        
        Vector3 back = position + Vector3.back * sampleDistance;
        back.y = GetHeightAtPosition(back);
        
        // Вычисляем два вектора для определения нормали
        Vector3 v1 = (right - left).normalized;
        Vector3 v2 = (forward - back).normalized;
        
        // Нормаль = векторное произведение
        Vector3 normal = Vector3.Cross(v2, v1).normalized;
        
        // Убеждаемся, что нормаль направлена вверх
        if (normal.y < 0)
        {
            normal = -normal;
        }
        
        return normal;
    }
    
    public static void ClearCache()
    {
        hillCache.Clear();
    }
}
