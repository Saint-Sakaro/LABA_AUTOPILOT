using UnityEngine;
using System.Collections.Generic;

public static class TreeGenerator
{
    private struct Tree
    {
        public Vector3 position;
        public int treeType;
        public float rotation;
    }

    private static Dictionary<Vector2Int, List<Tree>> treeCache = new Dictionary<Vector2Int, List<Tree>>();
    
    private const float TREE_SPACING = 200f;
    private const int SEED_OFFSET = 54321;
    
    private const float FOREST_SCALE = 1500f;     
    private const float FOREST_THRESHOLD = 0.4f; 
    
    private static float[] treeHeights = new float[] { 5.071409f, 3.997711f, 3.219089f, 3.977242f, 5.429212f };
    private static GameObject[] treePrefabs = new GameObject[5];

    public static void SetTreePrefabs(GameObject[] prefabs)
    {
        treePrefabs = prefabs;
    }

    public static List<TreeData> GetTreesForPlatform(Vector2Int platformCoord, int platformSize)
    {
        List<TreeData> treesForPlatform = new List<TreeData>();
        
        float minX = platformCoord.x * platformSize;
        float maxX = minX + platformSize;
        float minZ = platformCoord.y * platformSize;
        float maxZ = minZ + platformSize;
        
        float checkRadius = Mathf.Max(TREE_SPACING, platformSize);
        
        for (float x = minX - checkRadius; x < maxX + checkRadius; x += TREE_SPACING)
        {
            for (float z = minZ - checkRadius; z < maxZ + checkRadius; z += TREE_SPACING)
            {
                Vector2Int sectorCoord = GetSectorCoordinate(new Vector2(x, z));
                
                if (!treeCache.ContainsKey(sectorCoord))
                {
                    treeCache[sectorCoord] = GenerateTreesForSector(sectorCoord);
                }
                
                foreach (Tree tree in treeCache[sectorCoord])
                {
                    if (tree.position.x >= minX && tree.position.x < maxX &&
                        tree.position.z >= minZ && tree.position.z < maxZ)
                    {
                        treesForPlatform.Add(new TreeData(tree.position, tree.treeType, tree.rotation));
                    }
                }
            }
        }
        
        return treesForPlatform;
    }

    private static List<Tree> GenerateTreesForSector(Vector2Int sectorCoord)
    {
        List<Tree> trees = new List<Tree>();
        
        float sectorCenterX = sectorCoord.x * TREE_SPACING;
        float sectorCenterZ = sectorCoord.y * TREE_SPACING;
        
        float minSectorX = sectorCenterX - TREE_SPACING * 1.5f;
        float maxSectorX = sectorCenterX + TREE_SPACING * 1.5f;
        float minSectorZ = sectorCenterZ - TREE_SPACING * 1.5f;
        float maxSectorZ = sectorCenterZ + TREE_SPACING * 1.5f;
        
        int sectorSeed = (sectorCoord.x * 73856093) ^ (sectorCoord.y * 19349663) ^ SEED_OFFSET;
        Random.InitState(sectorSeed);
        
        int treesInSector = Random.Range(30, 51);
        
        for (int i = 0; i < treesInSector; i++)
        {
            float treeX = Random.Range(minSectorX, maxSectorX);
            float treeZ = Random.Range(minSectorZ, maxSectorZ);
            
            float forestNoise = GetForestNoise(treeX, treeZ);
            
            if (forestNoise > FOREST_THRESHOLD)
            {
                float heightAtPos = HillGenerator.GetHeightAtPosition(new Vector3(treeX, 0, treeZ));
                
                int treeType = Random.Range(0, 5);
                float rotation = Random.Range(0f, Mathf.PI * 2f);
                
                Vector3 treePos = new Vector3(
                    treeX,
                    heightAtPos + 17f,
                    treeZ
                );
                
                trees.Add(new Tree
                {
                    position = treePos,
                    treeType = treeType,
                    rotation = rotation
                });
            }
        }
        
        return trees;
    }

    private static float GetForestNoise(float x, float z)
    {
        float noise = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float maxValue = 0f;
        
        for (int i = 0; i < 3; i++)
        {
            float noiseX = (x / FOREST_SCALE) * frequency;
            float noiseZ = (z / FOREST_SCALE) * frequency;
            
            noise += Mathf.PerlinNoise(noiseX, noiseZ) * amplitude;
            
            maxValue += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }
        
        return noise / maxValue;
    }

    private static Vector2Int GetSectorCoordinate(Vector2 pos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(pos.x / TREE_SPACING),
            Mathf.FloorToInt(pos.y / TREE_SPACING)
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
        treeCache.Clear();
    }
}
