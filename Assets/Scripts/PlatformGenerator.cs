using UnityEngine;
using System.Collections.Generic;

public class PlatformGenerator : MonoBehaviour
{
    [SerializeField] private int platformSize = 1000;
    [SerializeField] private int loadRadius = 2;
    [SerializeField] private Material platformMaterial;
    [SerializeField] private GameObject[] treePrefabs = new GameObject[5];

    [SerializeField] private GameObject[] rockPrefabs = new GameObject[16];
    private Dictionary<Vector2Int, List<GameObject>> platformRocks = new Dictionary<Vector2Int, List<GameObject>>();

    
    private Dictionary<Vector2Int, GameObject> loadedPlatforms = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, GameObject> cachedPlatforms = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, List<GameObject>> platformTrees = new Dictionary<Vector2Int, List<GameObject>>();
    
    private Vector2Int lastPlatformCoord = Vector2Int.zero;
    private Transform cameraTransform;

    private void Start()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cameraTransform = mainCamera.transform;
        }
        else
        {
            cameraTransform = transform;
        }

        TreeGenerator.SetTreePrefabs(treePrefabs);
        RockGenerator.SetRockPrefabs(rockPrefabs);

        UpdatePlatforms();
    }

    private void Update()
    {
        Vector2Int currentPlatformCoord = GetPlatformCoordinate(cameraTransform.position);
        
        if (currentPlatformCoord != lastPlatformCoord)
        {
            lastPlatformCoord = currentPlatformCoord;
            UpdatePlatforms();
        }
    }

    private void UpdatePlatforms()
    {
        Vector2Int centerPlatform = lastPlatformCoord;
        HashSet<Vector2Int> platformsNeeded = new HashSet<Vector2Int>();

        for (int x = -loadRadius; x <= loadRadius; x++)
        {
            for (int z = -loadRadius; z <= loadRadius; z++)
            {
                Vector2Int platformCoord = new Vector2Int(centerPlatform.x + x, centerPlatform.y + z);
                platformsNeeded.Add(platformCoord);
            }
        }

        List<Vector2Int> platformsToRemove = new List<Vector2Int>();
        foreach (var platformCoord in loadedPlatforms.Keys)
        {
            if (!platformsNeeded.Contains(platformCoord))
            {
                cachedPlatforms[platformCoord] = loadedPlatforms[platformCoord];
                loadedPlatforms[platformCoord].SetActive(false);
                
                // Отключаем деревья
                if (platformTrees.ContainsKey(platformCoord))
                {
                    foreach (var tree in platformTrees[platformCoord])
                    {
                        tree.SetActive(false);
                    }
                }
                
                platformsToRemove.Add(platformCoord);
            }
        }

        foreach (var platformCoord in platformsToRemove)
        {
            loadedPlatforms.Remove(platformCoord);
        }

        foreach (var platformCoord in platformsNeeded)
        {
            if (!loadedPlatforms.ContainsKey(platformCoord))
            {
                GameObject platform;
                
                if (cachedPlatforms.ContainsKey(platformCoord))
                {
                    platform = cachedPlatforms[platformCoord];
                    platform.SetActive(true);
                    cachedPlatforms.Remove(platformCoord);
                    
                    // Включаем деревья
                    if (platformTrees.ContainsKey(platformCoord))
                    {
                        foreach (var tree in platformTrees[platformCoord])
                        {
                            tree.SetActive(true);
                        }
                    }
                }
                else
                {
                    platform = CreatePlatform(platformCoord);
                }
                
                loadedPlatforms[platformCoord] = platform;
            }
        }
    }

    private GameObject CreatePlatform(Vector2Int platformCoord)
    {
        GameObject platformObject = new GameObject($"Platform_{platformCoord.x}_{platformCoord.y}");
        platformObject.transform.parent = transform;
        platformObject.transform.position = new Vector3(
            platformCoord.x * platformSize,
            0,
            platformCoord.y * platformSize
        );

        Mesh mesh = CreatePlatformMesh(platformCoord);
        
        MeshFilter meshFilter = platformObject.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;
        
        MeshRenderer meshRenderer = platformObject.AddComponent<MeshRenderer>();
        meshRenderer.material = platformMaterial;
        
        MeshCollider meshCollider = platformObject.AddComponent<MeshCollider>();
        meshCollider.convex = false;
        meshCollider.sharedMesh = mesh;

        // Создаём деревья для этой платформы
        CreateTreesForPlatform(platformCoord, platformObject.transform);
        CreateRocksForPlatform(platformCoord, platformObject.transform);

        return platformObject;
    }

    private void CreateTreesForPlatform(Vector2Int platformCoord, Transform parentTransform)
    {
        List<TreeData> treesData = TreeGenerator.GetTreesForPlatform(platformCoord, platformSize);
        List<GameObject> trees = new List<GameObject>();


        foreach (var treeData in treesData)
        {
            if (treeData.treeType >= 0 && treeData.treeType < treePrefabs.Length && treePrefabs[treeData.treeType] != null)
            {
                GameObject treeInstance = Instantiate(treePrefabs[treeData.treeType]);
                treeInstance.transform.parent = parentTransform;
                treeInstance.transform.position = treeData.position;
                treeInstance.transform.rotation = Quaternion.Euler(-90f, treeData.rotation * Mathf.Rad2Deg, 0);
                // treeInstance.transform.localScale = new Vector3(1f, 1f, 1f); // Масштаб 50x
                    
                trees.Add(treeInstance);
            }
            else
            {
                Debug.LogWarning($"Префаб дерева типа {treeData.treeType} не установлен или индекс неверный!");
            }
        }

        platformTrees[platformCoord] = trees;
    }


    private void CreateRocksForPlatform(Vector2Int platformCoord, Transform parentTransform)
    {
        List<RockData> rocksData = RockGenerator.GetRocksForPlatform(platformCoord, platformSize);
        List<GameObject> rocks = new List<GameObject>();

        foreach (var rockData in rocksData)
        {
            if (rockData.rockType >= 0 && rockData.rockType < rockPrefabs.Length && rockPrefabs[rockData.rockType] != null)
            {
                GameObject rockInstance = Instantiate(rockPrefabs[rockData.rockType]);
                rockInstance.transform.parent = parentTransform;
                rockInstance.transform.position = rockData.position;
                rockInstance.transform.rotation = Quaternion.Euler(-90f, rockData.rotation * Mathf.Rad2Deg, 0);
                // rockInstance.transform.localScale = new Vector3(50f, 50f, 50f);
                
                rocks.Add(rockInstance);
            }
        }

        platformRocks[platformCoord] = rocks;
    }


    private Mesh CreatePlatformMesh(Vector2Int platformCoord)
    {
        Mesh mesh = new Mesh();
        mesh.name = "PlatformMesh";

        int verticesPerSide = 101;
        Vector3[] vertices = new Vector3[verticesPerSide * verticesPerSide];
        Vector2[] uvs = new Vector2[verticesPerSide * verticesPerSide];

        float stepSize = platformSize / (verticesPerSide - 1);

        for (int z = 0; z < verticesPerSide; z++)
        {
            for (int x = 0; x < verticesPerSide; x++)
            {
                int index = z * verticesPerSide + x;
                
                float localX = x * stepSize;
                float localZ = z * stepSize;
                
                float worldX = platformCoord.x * platformSize + localX;
                float worldZ = platformCoord.y * platformSize + localZ;
                
                float height = HillGenerator.GetHeightAtPosition(new Vector3(worldX, 0, worldZ));
                
                vertices[index] = new Vector3(
                    localX,
                    height,
                    localZ
                );
                
                uvs[index] = new Vector2(
                    x / (float)(verticesPerSide - 1),
                    z / (float)(verticesPerSide - 1)
                );
            }
        }

        int[] triangles = new int[(verticesPerSide - 1) * (verticesPerSide - 1) * 6];
        int triIndex = 0;

        for (int z = 0; z < verticesPerSide - 1; z++)
        {
            for (int x = 0; x < verticesPerSide - 1; x++)
            {
                int vertexIndex = z * verticesPerSide + x;

                triangles[triIndex++] = vertexIndex;
                triangles[triIndex++] = vertexIndex + verticesPerSide + 1;
                triangles[triIndex++] = vertexIndex + 1;

                triangles[triIndex++] = vertexIndex;
                triangles[triIndex++] = vertexIndex + verticesPerSide;
                triangles[triIndex++] = vertexIndex + verticesPerSide + 1;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private Vector2Int GetPlatformCoordinate(Vector3 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / platformSize),
            Mathf.FloorToInt(position.z / platformSize)
        );
    }

    private void OnDestroy()
    {
        foreach (var platform in loadedPlatforms.Values)
        {
            if (platform != null)
                Destroy(platform);
        }
        
        foreach (var platform in cachedPlatforms.Values)
        {
            if (platform != null)
                Destroy(platform);
        }
        
        loadedPlatforms.Clear();
        cachedPlatforms.Clear();
        platformTrees.Clear();
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Vector2Int centerPlatform = GetPlatformCoordinate(cameraTransform != null ? cameraTransform.position : transform.position);
        
        Gizmos.color = Color.green;
        for (int x = -loadRadius; x <= loadRadius; x++)
        {
            for (int z = -loadRadius; z <= loadRadius; z++)
            {
                Vector3 platformCenter = new Vector3(
                    (centerPlatform.x + x) * platformSize + platformSize * 0.5f,
                    0,
                    (centerPlatform.y + z) * platformSize + platformSize * 0.5f
                );
                
                Gizmos.DrawWireCube(platformCenter, new Vector3(platformSize, 10, platformSize));
            }
        }
    }
}

public class TreeData
{
    public Vector3 position;
    public int treeType;
    public float rotation;

    public TreeData(Vector3 pos, int type, float rot)
    {
        position = pos;
        treeType = type;
        rotation = rot;
    }
}
