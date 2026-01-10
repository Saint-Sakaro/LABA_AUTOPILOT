using UnityEngine;
using System.Collections.Generic;

public static class HillGenerator
{
    private struct Hill
    {
        public Vector2 position;
        public float radius;
        public float height;
    }

    private static Dictionary<Vector2Int, List<Hill>> hillCache = new Dictionary<Vector2Int, List<Hill>>();
    
    private const float HILL_SPACING = 800f; // Расстояние между "точками генерации" холмов - УМЕНЬШЕНО!
    private const int SEED_OFFSET = 12345;

    public static float GetHeightAtPosition(Vector3 worldPos)
    {
        Vector2 pos = new Vector2(worldPos.x, worldPos.z);
        
        // Определяем в каком "секторе" находимся для генерации холмов
        Vector2Int sectorCoord = GetSectorCoordinate(pos);
        
        // Получаем или генерируем холмы для этого сектора
        if (!hillCache.ContainsKey(sectorCoord))
        {
            hillCache[sectorCoord] = GenerateHillsForSector(sectorCoord);
        }

        List<Hill> hills = hillCache[sectorCoord];
        
        // Проверяем влияние каждого холма на текущую позицию
        float totalHeight = 0f;
        
        foreach (Hill hill in hills)
        {
            float distToHill = Vector2.Distance(pos, hill.position);
            
            // Если внутри радиуса холма
            if (distToHill < hill.radius)
            {
                // Гладкое убывание высоты к краям холма (парабола)
                float t = distToHill / hill.radius; // от 0 (центр) до 1 (край)
                float heightFalloff = (1f - t * t) * (1f - t * t); // Гладкая парабола
                
                totalHeight += hill.height * heightFalloff;
            }
        }
        
        return totalHeight;
    }

    private static List<Hill> GenerateHillsForSector(Vector2Int sectorCoord)
    {
        List<Hill> hills = new List<Hill>();
        
        // Генерируем холмы в сетке для этого сектора
        float sectorCenterX = sectorCoord.x * HILL_SPACING;
        float sectorCenterY = sectorCoord.y * HILL_SPACING;
        
        // Для каждого потенциального положения холма
        for (float x = sectorCenterX - HILL_SPACING; x <= sectorCenterX + HILL_SPACING; x += HILL_SPACING)
        {
            for (float z = sectorCenterY - HILL_SPACING; z <= sectorCenterY + HILL_SPACING; z += HILL_SPACING)
            {
                // Проверяем, должен ли здесь быть холм (случайно, но воспроизводимо)
                Random.InitState(GetSeedForPosition(x, z));
                
                // Вероятность появления холма (измените это значение)
                // 0.4 = 40% (мало холмов)
                // 0.6 = 60% (средне)
                // 0.8 = 80% (много)
                // 0.9 = 90% (очень много)
                if (Random.value < 0.9f)  // ← ИЗМЕНИТЕ ЗДЕСЬ для управления количеством
                {
                    // Добавляем случайное смещение холма
                    float offsetX = Random.Range(-HILL_SPACING * 0.3f, HILL_SPACING * 0.3f);
                    float offsetZ = Random.Range(-HILL_SPACING * 0.3f, HILL_SPACING * 0.3f);
                    
                    float hillX = x + offsetX;
                    float hillZ = z + offsetZ;
                    
                    // Генерируем параметры холма
                    float hillRadius = Random.Range(80f, 1000f); // Радиус холма
                    float hillHeight = Random.Range(30, 200f); // Высота холма
                    
                    hills.Add(new Hill
                    {
                        position = new Vector2(hillX, hillZ),
                        radius = hillRadius,
                        height = hillHeight
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

    public static void ClearCache()
    {
        hillCache.Clear();
    }
}