using UnityEngine;
using System.Collections.Generic;

public static class RockGenerator
{
    private struct Rock
    {
        public Vector3 position;
        public int rockType;
        public float rotation;
    }

    private static Dictionary<Vector2Int, List<Rock>> rockCache = new Dictionary<Vector2Int, List<Rock>>();
    
    private const float ROCK_SPACING = 600f;
    private const int SEED_OFFSET = 99999;
    
    private static float[] rockHeights = new float[] { 5000f, 5000f, 5000f, 5000f, 5000f, 5000f, 5000f, 5000f, 5000f, 5000f, 5000f, 5000f, 5000f, 5000f, 5000f, 5000f };
    private static GameObject[] rockPrefabs = new GameObject[16];

    public static void SetRockPrefabs(GameObject[] prefabs)
    {
        rockPrefabs = prefabs;
    }

    public static List<RockData> GetRocksForPlatform(Vector2Int platformCoord, int platformSize)
    {
        List<RockData> rocksForPlatform = new List<RockData>();
        
        float minX = platformCoord.x * platformSize;
        float maxX = minX + platformSize;
        float minZ = platformCoord.y * platformSize;
        float maxZ = minZ + platformSize;
        
        float checkRadius = Mathf.Max(ROCK_SPACING, platformSize);
        
        for (float x = minX - checkRadius; x < maxX + checkRadius; x += ROCK_SPACING)
        {
            for (float z = minZ - checkRadius; z < maxZ + checkRadius; z += ROCK_SPACING)
            {
                Vector2Int sectorCoord = GetSectorCoordinate(new Vector2(x, z));
                
                if (!rockCache.ContainsKey(sectorCoord))
                {
                    rockCache[sectorCoord] = GenerateRocksForSector(sectorCoord);
                }
                
                foreach (Rock rock in rockCache[sectorCoord])
                {
                    if (rock.position.x >= minX - 100f && rock.position.x < maxX + 100f &&
                        rock.position.z >= minZ - 100f && rock.position.z < maxZ + 100f)
                    {
                        rocksForPlatform.Add(new RockData(rock.position, rock.rockType, rock.rotation));
                    }
                }
            }
        }
        
        return rocksForPlatform;
    }

    private static List<Rock> GenerateRocksForSector(Vector2Int sectorCoord)
    {
        List<Rock> rocks = new List<Rock>();
        
        float sectorCenterX = sectorCoord.x * ROCK_SPACING;
        float sectorCenterZ = sectorCoord.y * ROCK_SPACING;
        
        int rockCounter = 0;
        for (float x = sectorCenterX - ROCK_SPACING * 1.5f; x <= sectorCenterX + ROCK_SPACING * 1.5f; x += ROCK_SPACING)
        {
            for (float z = sectorCenterZ - ROCK_SPACING * 1.5f; z <= sectorCenterZ + ROCK_SPACING * 1.5f; z += ROCK_SPACING)
            {
                // Уникальный seed для каждой позиции в цикле
                int gridX = Mathf.FloorToInt(x / ROCK_SPACING);
                int gridZ = Mathf.FloorToInt(z / ROCK_SPACING);
                int uniqueSeed = (gridX * 73856093) ^ (gridZ * 19349663) ^ SEED_OFFSET ^ rockCounter;
                
                Random.InitState(uniqueSeed);
                rockCounter++;
                
                // 25% вероятность появления камня
                if (Random.value < 0.25f)
                {
                    float offsetX = Random.Range(-ROCK_SPACING * 0.4f, ROCK_SPACING * 0.4f);
                    float offsetZ = Random.Range(-ROCK_SPACING * 0.4f, ROCK_SPACING * 0.4f);
                    float rockX = x + offsetX;
                    float rockZ = z + offsetZ;
                    
                    float heightAtPos = HillGenerator.GetHeightAtPosition(new Vector3(rockX, 0, rockZ));
                    int rockType = Random.Range(0, 16); // 16 типов камней
                    float rotation = Random.Range(0f, Mathf.PI * 2f);
                    
                    Vector3 rockPos = new Vector3(
                        rockX,
                        heightAtPos + 17f,
                        rockZ
                    );
                    
                    rocks.Add(new Rock
                    {
                        position = rockPos,
                        rockType = rockType,
                        rotation = rotation
                    });
                }
            }
        }
        
        return rocks;
    }

    private static Vector2Int GetSectorCoordinate(Vector2 pos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(pos.x / ROCK_SPACING),
            Mathf.FloorToInt(pos.y / ROCK_SPACING)
        );
    }

    private static int GetSeedForPosition(float x, float z)
    {
        int seedX = Mathf.RoundToInt(x) * 73856093;
        int seedZ = Mathf.RoundToInt(z) * 19349663;
        return Mathf.Abs(seedX ^ seedZ) + SEED_OFFSET;
    }

    public static void ClearCache()
    {
        rockCache.Clear();
    }
}
