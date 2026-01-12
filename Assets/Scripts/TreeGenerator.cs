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
    
    private static float[] treeHeights = new float[] { 507.141f, 399.7712f, 321.909f, 397.7243f, 542.9211f };
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
                    // ИСПРАВЛЕНО: убран запас в 100 единиц - деревья только в пределах платформы
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
        
        int treeCounter = 0;
        for (float x = sectorCenterX - TREE_SPACING * 1.5f; x <= sectorCenterX + TREE_SPACING * 1.5f; x += TREE_SPACING)
        {
            for (float z = sectorCenterZ - TREE_SPACING * 1.5f; z <= sectorCenterZ + TREE_SPACING * 1.5f; z += TREE_SPACING)
            {
                int gridX = Mathf.FloorToInt(x / TREE_SPACING);
                int gridZ = Mathf.FloorToInt(z / TREE_SPACING);
                int uniqueSeed = (gridX * 73856093) ^ (gridZ * 19349663) ^ SEED_OFFSET ^ treeCounter;
                
                Random.InitState(uniqueSeed);
                treeCounter++;
                
                if (Random.value < 0.3f)
                {
                    float offsetX = Random.Range(-TREE_SPACING * 0.4f, TREE_SPACING * 0.4f);
                    float offsetZ = Random.Range(-TREE_SPACING * 0.4f, TREE_SPACING * 0.4f);
                    
                    float treeX = x + offsetX;
                    float treeZ = z + offsetZ;
                    
                    float heightAtPos = HillGenerator.GetHeightAtPosition(new Vector3(treeX, 0, treeZ));
                    
                    int treeType = Random.Range(0, 5);
                    float rotation = Random.Range(0f, Mathf.PI * 2f);
                    
                    Vector3 treePos = new Vector3(
                        treeX,
                        heightAtPos + 30,
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
        }
        
        return trees;
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