using UnityEngine;
using System.Collections.Generic;

public class PlatformGenerator : MonoBehaviour
{
    [SerializeField] private int platformSize = 1000; // Размер одной платформы
    [SerializeField] private int loadRadius = 2; // Количество платформ вокруг корабля
    [SerializeField] private Material platformMaterial; // Ваш материал с шейдером травы
    
    private Dictionary<Vector2Int, GameObject> loadedPlatforms = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, GameObject> cachedPlatforms = new Dictionary<Vector2Int, GameObject>();
    
    private Vector2Int lastPlatformCoord = Vector2Int.zero;
    private Transform cameraTransform;

    private void Start()
    {
        // Получаем позицию камеры
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cameraTransform = mainCamera.transform;
        }
        else
        {
            cameraTransform = transform;
        }

        // Генерируем начальные платформы
        UpdatePlatforms();
    }

    private void Update()
    {
        // Проверяем, переместился ли корабль на новую платформу
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

        // Определяем, какие платформы нужны
        for (int x = -loadRadius; x <= loadRadius; x++)
        {
            for (int z = -loadRadius; z <= loadRadius; z++)
            {
                Vector2Int platformCoord = new Vector2Int(centerPlatform.x + x, centerPlatform.y + z);
                platformsNeeded.Add(platformCoord);
            }
        }

        // Выгружаем ненужные платформы (кешируем их)
        List<Vector2Int> platformsToRemove = new List<Vector2Int>();
        foreach (var platformCoord in loadedPlatforms.Keys)
        {
            if (!platformsNeeded.Contains(platformCoord))
            {
                cachedPlatforms[platformCoord] = loadedPlatforms[platformCoord];
                loadedPlatforms[platformCoord].SetActive(false);
                platformsToRemove.Add(platformCoord);
            }
        }

        foreach (var platformCoord in platformsToRemove)
        {
            loadedPlatforms.Remove(platformCoord);
        }

        // Загружаем нужные платформы
        foreach (var platformCoord in platformsNeeded)
        {
            if (!loadedPlatforms.ContainsKey(platformCoord))
            {
                GameObject platform;
                
                if (cachedPlatforms.ContainsKey(platformCoord))
                {
                    // Активируем кешированную платформу
                    platform = cachedPlatforms[platformCoord];
                    platform.SetActive(true);
                    cachedPlatforms.Remove(platformCoord);
                }
                else
                {
                    // Создаём новую платформу
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

        // Создаём меш с холмами
        Mesh mesh = CreatePlatformMesh(platformCoord);
        
        // Добавляем MeshFilter
        MeshFilter meshFilter = platformObject.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;
        
        // Добавляем MeshRenderer
        MeshRenderer meshRenderer = platformObject.AddComponent<MeshRenderer>();
        meshRenderer.material = platformMaterial;
        
        // Добавляем MeshCollider
        MeshCollider meshCollider = platformObject.AddComponent<MeshCollider>();
        meshCollider.convex = false;
        meshCollider.sharedMesh = mesh;

        return platformObject;
    }

    private Mesh CreatePlatformMesh(Vector2Int platformCoord)
    {
        Mesh mesh = new Mesh();
        mesh.name = "PlatformMesh";

        // Создаём вершины для платформы с холмами
        int verticesPerSide = 101; // 100 квадратов = 101 вершина
        Vector3[] vertices = new Vector3[verticesPerSide * verticesPerSide];
        Vector2[] uvs = new Vector2[verticesPerSide * verticesPerSide];

        float stepSize = platformSize / (verticesPerSide - 1);

        for (int z = 0; z < verticesPerSide; z++)
        {
            for (int x = 0; x < verticesPerSide; x++)
            {
                int index = z * verticesPerSide + x;
                
                // Локальные координаты внутри платформы
                float localX = x * stepSize;
                float localZ = z * stepSize;
                
                // Мировые координаты
                float worldX = platformCoord.x * platformSize + localX;
                float worldZ = platformCoord.y * platformSize + localZ;
                
                // Получаем высоту от холмов
                float height = HillGenerator.GetHeightAtPosition(new Vector3(worldX, 0, worldZ));
                
                vertices[index] = new Vector3(
                    localX,
                    height,
                    localZ
                );
                
                // UV координаты для текстуры
                uvs[index] = new Vector2(
                    x / (float)(verticesPerSide - 1),
                    z / (float)(verticesPerSide - 1)
                );
            }
        }

        // Создаём треугольники
        int[] triangles = new int[(verticesPerSide - 1) * (verticesPerSide - 1) * 6];
        int triIndex = 0;

        for (int z = 0; z < verticesPerSide - 1; z++)
        {
            for (int x = 0; x < verticesPerSide - 1; x++)
            {
                int vertexIndex = z * verticesPerSide + x;

                // Первый треугольник
                triangles[triIndex++] = vertexIndex;
                triangles[triIndex++] = vertexIndex + verticesPerSide + 1;
                triangles[triIndex++] = vertexIndex + 1;

                // Второй треугольник
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
    }

    // Визуализация для отладки
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