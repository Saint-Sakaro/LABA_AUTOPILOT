using UnityEngine;
using System.Collections.Generic;

public class VolumetricCloudManager : MonoBehaviour
{
    [SerializeField] private GameObject cloudPrefab;
    [SerializeField] private Transform cloudsContainer;
    
    [Header("Generation Settings")]
    [SerializeField] private int cloudsPerChunk = 10;
    [SerializeField] private float chunkSize = 500f;
    [SerializeField] private int loadRadius = 2;
    
    [Header("Height Settings")]
    [SerializeField] private float minHeightAboveGround = 30f;
    [SerializeField] private float maxHeightAboveGround = 200f;
    [SerializeField] private float verticalSpread = 150f;
    [SerializeField] private float maxCloudHeightAboveShip = 500f; 
    
    [Header("Atmosphere Settings")]
    [SerializeField] private float atmosphereHeight = 1000f; 
    [SerializeField] private bool useAtmosphereLimit = true;
    [SerializeField] private bool generateAroundShipHeight = true;
    [SerializeField] private bool generateFullAtmosphereHeight = true; 
    [SerializeField] private bool syncWithShipAtmosphere = false; 
    
    [Header("Seed Settings")]
    [SerializeField] private int generationSeed = 12345;
    
    [Header("Wind")]
    [SerializeField] private Vector3 windDirection = Vector3.right;
    [SerializeField] private float windSpeed = 0.5f;
    
    [Header("Tracking Settings")]
    [SerializeField] private Transform trackingTarget;
    [SerializeField] private float updateInterval = 1f;
    [SerializeField] private float refillCheckInterval = 2f;
    [SerializeField] private float maxDistanceFromTarget = 800f;
    [SerializeField] private float spawnDistanceFromTarget = 400f;
    
    private Dictionary<Vector2Int, List<VolumetricCloud>> cloudChunks = new Dictionary<Vector2Int, List<VolumetricCloud>>();
    private List<VolumetricCloud> allClouds = new List<VolumetricCloud>();
    private Vector2Int lastChunkCoord = Vector2Int.zero;
    private float lastShipHeight = 0f;
    private float lastUpdateTime = 0f;
    private float lastRefillCheckTime = 0f;
    private int generationCounter = 0; 
    private const int SEED_MULTIPLIER_X = 73856093;
    private const int SEED_MULTIPLIER_Z = 19349663;
    
    private void Start()
    {
        if (cloudsContainer == null)
            cloudsContainer = transform;
        
        
        if (trackingTarget == null)
        {
            ShipController ship = FindObjectOfType<ShipController>();
            if (ship != null)
            {
                trackingTarget = ship.transform;
            }
            else
            {
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    trackingTarget = mainCam.transform;
                }
            }
        }
        
        
        VolumetricCloud[] existingClouds = GetComponentsInChildren<VolumetricCloud>();
        if (existingClouds.Length > 0)
        {
            foreach (VolumetricCloud cloud in existingClouds)
            {
                if (cloud != null && !allClouds.Contains(cloud))
                {
                    allClouds.Add(cloud);
                }
            }
            
            
            OrganizeExistingCloudsIntoChunks();
        }
        else
        {
            
            InitializeCloudGeneration();
        }
    }
    
    private void Update()
    {
        if (trackingTarget == null) return;
        
        
        if (Time.time - lastUpdateTime > updateInterval)
        {
            lastUpdateTime = Time.time;
            UpdateCloudChunks();
        }
        
        
        CleanupDestroyedClouds();
        
        
        RemoveDistantClouds();
        RemoveCloudsAboveAtmosphere();
        
        
        
        float refillInterval = generateFullAtmosphereHeight ? refillCheckInterval * 0.5f : refillCheckInterval;
        if (Time.time - lastRefillCheckTime > refillInterval)
        {
            lastRefillCheckTime = Time.time;
            RefillChunksWithLowClouds();
        }
        
        
        if (generateFullAtmosphereHeight && trackingTarget != null)
        {
            float currentShipHeight = trackingTarget.position.y;
            float heightDifference = Mathf.Abs(currentShipHeight - lastShipHeight);
            
            
            if (heightDifference > 30f) 
            {
                lastShipHeight = currentShipHeight;
                
                Vector2Int currentChunk = GetChunkCoordinate(trackingTarget.position);
                for (int x = -1; x <= 1; x++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        Vector2Int chunkCoord = new Vector2Int(currentChunk.x + x, currentChunk.y + z);
                        if (cloudChunks.ContainsKey(chunkCoord))
                        {
                            cloudChunks[chunkCoord].RemoveAll(cloud => cloud == null);
                            if (cloudChunks[chunkCoord].Count < cloudsPerChunk)
                            {
                                int toGenerate = Mathf.Max(1, (cloudsPerChunk - cloudChunks[chunkCoord].Count) / 2);
                                GenerateCloudsInChunk(chunkCoord, toGenerate);
                            }
                        }
                        else
                        {
                            
                            GenerateChunk(chunkCoord);
                        }
                    }
                }
            }
        }
        
        
        GenerateCloudsAhead();
    }
    
    private void InitializeCloudGeneration()
    {
        if (trackingTarget == null) return;
        
        Vector2Int currentChunk = GetChunkCoordinate(trackingTarget.position);
        lastChunkCoord = currentChunk;
        
        
        for (int x = -loadRadius; x <= loadRadius; x++)
        {
            for (int z = -loadRadius; z <= loadRadius; z++)
            {
                Vector2Int chunkCoord = new Vector2Int(currentChunk.x + x, currentChunk.y + z);
                GenerateChunk(chunkCoord);
            }
        }
        
        Debug.Log($"Инициализирована генерация облаков в {cloudChunks.Count} чанках");
    }
    
    private void OrganizeExistingCloudsIntoChunks()
    {
        foreach (VolumetricCloud cloud in allClouds)
        {
            if (cloud == null) continue;
            
            Vector2Int chunkCoord = GetChunkCoordinate(cloud.transform.position);
            
            if (!cloudChunks.ContainsKey(chunkCoord))
            {
                cloudChunks[chunkCoord] = new List<VolumetricCloud>();
            }
            
            if (!cloudChunks[chunkCoord].Contains(cloud))
            {
                cloudChunks[chunkCoord].Add(cloud);
            }
        }
        
        Debug.Log($"Организовано {allClouds.Count} существующих облаков в {cloudChunks.Count} чанках");
    }
    
    private void UpdateCloudChunks()
    {
        if (trackingTarget == null) return;
        
        Vector2Int currentChunk = GetChunkCoordinate(trackingTarget.position);
        float currentShipHeight = trackingTarget.position.y;
        float heightDifference = Mathf.Abs(currentShipHeight - lastShipHeight);
        
        bool chunkChanged = currentChunk != lastChunkCoord;
        bool heightChangedSignificantly = heightDifference > 50f; 
        
        if (chunkChanged || heightChangedSignificantly)
        {
            if (chunkChanged)
            {
                lastChunkCoord = currentChunk;
            }
            
            if (heightChangedSignificantly)
            {
                lastShipHeight = currentShipHeight;
            }
            
            
            HashSet<Vector2Int> neededChunks = new HashSet<Vector2Int>();
            for (int x = -loadRadius; x <= loadRadius; x++)
            {
                for (int z = -loadRadius; z <= loadRadius; z++)
                {
                    neededChunks.Add(new Vector2Int(currentChunk.x + x, currentChunk.y + z));
                }
            }
            
            
            if (chunkChanged)
            {
                List<Vector2Int> chunksToRemove = new List<Vector2Int>();
                foreach (var chunkCoord in cloudChunks.Keys)
                {
                    if (!neededChunks.Contains(chunkCoord))
                    {
                        RemoveChunk(chunkCoord);
                        chunksToRemove.Add(chunkCoord);
                    }
                }
                
                foreach (var chunkCoord in chunksToRemove)
                {
                    cloudChunks.Remove(chunkCoord);
                }
            }
            
            
            foreach (var chunkCoord in neededChunks)
            {
                if (!cloudChunks.ContainsKey(chunkCoord))
                {
                    
                    GenerateChunk(chunkCoord);
                }
                else if (heightChangedSignificantly || generateFullAtmosphereHeight)
                {
                    
                    cloudChunks[chunkCoord].RemoveAll(cloud => cloud == null);
                    
                    if (generateFullAtmosphereHeight)
                    {
                        
                        
                        int totalClouds = cloudChunks[chunkCoord].Count;
                        
                        
                        if (totalClouds < cloudsPerChunk)
                        {
                            int cloudsToGenerate = cloudsPerChunk - totalClouds;
                            
                            if (heightChangedSignificantly)
                            {
                                cloudsToGenerate = Mathf.Max(cloudsToGenerate, cloudsPerChunk / 2);
                            }
                            GenerateCloudsInChunk(chunkCoord, cloudsToGenerate);
                        }
                        else if (heightChangedSignificantly)
                        {
                            
                            GenerateCloudsInChunk(chunkCoord, Mathf.Max(1, cloudsPerChunk / 3));
                        }
                    }
                    else
                    {
                        
                        int cloudsAboveShip = 0;
                        foreach (var cloud in cloudChunks[chunkCoord])
                        {
                            if (cloud != null && cloud.transform.position.y > currentShipHeight)
                            {
                                cloudsAboveShip++;
                            }
                        }
                        
                        
                        if (cloudsAboveShip < cloudsPerChunk / 2)
                        {
                            int cloudsToGenerate = Mathf.Max(1, (cloudsPerChunk / 2) - cloudsAboveShip);
                            GenerateCloudsInChunk(chunkCoord, cloudsToGenerate);
                        }
                    }
                }
            }
        }
    }
    
    private void GenerateChunk(Vector2Int chunkCoord)
    {
        
        if (useAtmosphereLimit && trackingTarget != null && !generateFullAtmosphereHeight)
        {
            float shipHeight = trackingTarget.position.y;
            float maxAtmosphereY = GetAtmosphereHeight();
            
            if (shipHeight > maxAtmosphereY)
            {
                
                return;
            }
        }
        
        if (cloudChunks.ContainsKey(chunkCoord))
        {
            
            if (cloudChunks[chunkCoord].Count >= cloudsPerChunk)
            {
                return;
            }
        }
        else
        {
            cloudChunks[chunkCoord] = new List<VolumetricCloud>();
        }
        
        
        generationCounter++;
        
        int cloudsToGenerate = cloudsPerChunk - cloudChunks[chunkCoord].Count;
        int generatedCount = 0;
        int attempts = 0;
        int maxAttempts = cloudsToGenerate * 5; 
        
        
        int existingCloudCount = cloudChunks[chunkCoord].Count;
        
        
        float currentShipHeight = trackingTarget != null ? trackingTarget.position.y : 0f;
        
        
        while (generatedCount < cloudsToGenerate && attempts < maxAttempts)
        {
            attempts++;
            
            
            
            int uniqueIndex = existingCloudCount + attempts + generationCounter * 1000;
            Vector3 position = GetRandomPositionInChunk(chunkCoord, uniqueIndex);
            
            
            if (useAtmosphereLimit)
            {
                float maxAtmosphereY = GetAtmosphereHeight();
                float groundHeight = HillGenerator.GetHeightAtPosition(new Vector3(position.x, 0, position.z));
                float minAtmosphereY = groundHeight + minHeightAboveGround;
                
                if (generateFullAtmosphereHeight)
                {
                    
                    
                    
                    if (position.y > maxAtmosphereY)
                    {
                        position.y = maxAtmosphereY - 10f; 
                    }
                    if (position.y < minAtmosphereY)
                    {
                        position.y = minAtmosphereY;
                    }
                }
                else
                {
                    
                    if (generateAroundShipHeight && trackingTarget != null)
                    {
                        if (position.y <= currentShipHeight)
                        {
                            
                            position.y = currentShipHeight + Random.Range(50f, maxCloudHeightAboveShip * 0.7f);
                        }
                        
                        float minYAboveShip = currentShipHeight + 50f;
                        minAtmosphereY = Mathf.Max(minAtmosphereY, minYAboveShip);
                    }
                    
                    
                    if (position.y > maxAtmosphereY)
                    {
                        
                        if (generateAroundShipHeight && trackingTarget != null && currentShipHeight < maxAtmosphereY)
                        {
                            position.y = currentShipHeight + Random.Range(50f, Mathf.Min(maxCloudHeightAboveShip * 0.7f, maxAtmosphereY - currentShipHeight - 10f));
                        }
                        else
                        {
                            continue; 
                        }
                    }
                    
                    
                    if (position.y < minAtmosphereY)
                    {
                        
                        if (generateAroundShipHeight && trackingTarget != null)
                        {
                            position.y = Mathf.Max(minAtmosphereY, currentShipHeight + 50f);
                        }
                        else
                        {
                            position.y = minAtmosphereY;
                        }
                    }
                }
            }
            
            VolumetricCloud cloud = CreateCloud(position);
            
            if (cloud != null)
            {
                
                float cloudWindSpeed = windSpeed * Random.Range(0.8f, 1.2f);
                cloud.SetWind(windDirection, cloudWindSpeed);
                cloudChunks[chunkCoord].Add(cloud);
                generatedCount++;
            }
        }
    }
    
    private Vector3 GetRandomPositionInChunk(Vector2Int chunkCoord, int cloudIndex)
    {
        
        
        int baseSeed = GetSeedForChunk(chunkCoord);
        int uniqueSeedX = baseSeed + cloudIndex * 73856093 + generationCounter + (int)(Time.time * 100) % 10000;
        int uniqueSeedZ = baseSeed + cloudIndex * 19349663 + generationCounter * 2 + (int)(Time.time * 200) % 10000;
        
        Random.InitState(uniqueSeedX);
        float localX = Random.Range(0f, chunkSize);
        
        Random.InitState(uniqueSeedZ);
        float localZ = Random.Range(0f, chunkSize);
        
        float worldX = chunkCoord.x * chunkSize + localX;
        float worldZ = chunkCoord.y * chunkSize + localZ;
        
        
        float groundHeight = HillGenerator.GetHeightAtPosition(new Vector3(worldX, 0f, worldZ));
        
        float y;
        
        if (generateFullAtmosphereHeight && useAtmosphereLimit)
        {
            
            
            float maxAtmosphereY = GetAtmosphereHeight();
            float minCloudY = groundHeight + minHeightAboveGround;
            float maxCloudY = maxAtmosphereY - 10f; 
            
            
            
            Random.InitState(baseSeed + cloudIndex * 17 + generationCounter * 1000 + (int)(Time.time * 50) % 1000);
            y = Random.Range(minCloudY, maxCloudY);
            
            
            
        }
        else if (generateAroundShipHeight && trackingTarget != null)
        {
            
            float shipHeight = trackingTarget.position.y;
            
            
            float minOffsetAboveShip = 50f;
            
            
            float windY = windDirection.normalized.y;
            
            
            float baseOffsetUp = minOffsetAboveShip;
            
            if (Mathf.Abs(windY) > 0.1f)
            {
                if (windY < 0)
                {
                    baseOffsetUp = Mathf.Max(minOffsetAboveShip, spawnDistanceFromTarget * 0.25f + verticalSpread * 0.4f);
                }
                else
                {
                    baseOffsetUp = Mathf.Max(minOffsetAboveShip, verticalSpread * 0.5f);
                }
            }
            else
            {
                baseOffsetUp = Mathf.Max(minOffsetAboveShip, verticalSpread * 0.4f);
            }
            
            float maxOffsetFromShip = maxCloudHeightAboveShip;
            float actualOffsetUp = Mathf.Min(baseOffsetUp, maxOffsetFromShip * 0.6f);
            float baseHeight = shipHeight + actualOffsetUp;
            
            float maxSpread = Mathf.Min(verticalSpread * 0.7f, maxCloudHeightAboveShip - actualOffsetUp);
            maxSpread = Mathf.Max(0f, maxSpread);
            float cloudHeightRelativeToBase = Random.Range(0f, maxSpread);
            y = baseHeight + cloudHeightRelativeToBase;
            
            float maxAllowedY = shipHeight + maxCloudHeightAboveShip;
            y = Mathf.Min(y, maxAllowedY);
            
            if (y <= shipHeight)
            {
                y = shipHeight + Random.Range(minOffsetAboveShip, minOffsetAboveShip + verticalSpread * 0.3f);
            }
            
            float minCloudYAboveGround = groundHeight + minHeightAboveGround;
            if (y < minCloudYAboveGround && minCloudYAboveGround > shipHeight)
            {
                y = Mathf.Max(minCloudYAboveGround, shipHeight + minOffsetAboveShip);
            }
            else if (y < shipHeight)
            {
                y = shipHeight + Random.Range(minOffsetAboveShip, minOffsetAboveShip + verticalSpread * 0.3f);
            }
        }
        else
        {
            
            y = groundHeight + Random.Range(minHeightAboveGround, maxHeightAboveGround);
        }
        
        
        if (useAtmosphereLimit)
        {
            float maxAtmosphereY = GetAtmosphereHeight();
            if (y > maxAtmosphereY)
            {
                
                
                float shipHeight = trackingTarget != null ? trackingTarget.position.y : 0f;
                if (shipHeight > maxAtmosphereY)
                {
                    
                    y = Mathf.Min(shipHeight + 50f, maxAtmosphereY - 10f);
                }
                else
                {
                    float atmosphereMargin = 10f; 
                    y = maxAtmosphereY - atmosphereMargin;
                }
            }
        }
        
        
        float finalMinYAboveGround = groundHeight + minHeightAboveGround;
        
        if (generateFullAtmosphereHeight)
        {
            
            y = Mathf.Max(y, finalMinYAboveGround);
            
            if (useAtmosphereLimit)
            {
                float maxAtmosphereY = GetAtmosphereHeight();
                y = Mathf.Min(y, maxAtmosphereY - 10f); 
            }
        }
        else if (trackingTarget != null && generateAroundShipHeight)
        {
            float shipHeight = trackingTarget.position.y;
            
            float minYAboveShip = shipHeight + 50f;
            float maxYAboveShip = shipHeight + maxCloudHeightAboveShip;
            
            y = Mathf.Max(y, minYAboveShip, finalMinYAboveGround);
            y = Mathf.Min(y, maxYAboveShip);
            
            
            if (useAtmosphereLimit)
            {
                float maxAtmosphereY = GetAtmosphereHeight();
                y = Mathf.Min(y, Mathf.Min(maxAtmosphereY, maxYAboveShip));
            }
        }
        else
        {
            y = Mathf.Max(y, finalMinYAboveGround);
            
            if (useAtmosphereLimit)
            {
                float maxAtmosphereY = GetAtmosphereHeight();
                y = Mathf.Min(y, maxAtmosphereY);
            }
        }
        
        return new Vector3(worldX, y, worldZ);
    }
    
    private float GetAtmosphereHeight()
    {
        
        if (syncWithShipAtmosphere)
        {
            ShipController shipController = FindObjectOfType<ShipController>();
            if (shipController != null)
            {
                
                System.Reflection.FieldInfo field = typeof(ShipController).GetField("atmosphereHeight", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    float shipAtmosphereHeight = (float)field.GetValue(shipController);
                    if (shipAtmosphereHeight > 0f)
                    {
                        return shipAtmosphereHeight;
                    }
                }
            }
        }
        
        return atmosphereHeight;
    }
    
    private void RemoveCloudsAboveAtmosphere()
    {
        if (!useAtmosphereLimit || trackingTarget == null) return;
        
        float maxAtmosphereY = GetAtmosphereHeight();
        List<VolumetricCloud> cloudsToRemove = new List<VolumetricCloud>();
        
        foreach (VolumetricCloud cloud in allClouds)
        {
            if (cloud == null)
            {
                cloudsToRemove.Add(cloud);
                continue;
            }
            
            
            if (cloud.transform.position.y > maxAtmosphereY)
            {
                cloudsToRemove.Add(cloud);
            }
        }
        
        foreach (VolumetricCloud cloud in cloudsToRemove)
        {
            if (cloud != null)
            {
                
                Vector2Int chunkCoord = GetChunkCoordinate(cloud.transform.position);
                if (cloudChunks.ContainsKey(chunkCoord))
                {
                    cloudChunks[chunkCoord].Remove(cloud);
                }
                
                allClouds.Remove(cloud);
                Destroy(cloud.gameObject);
            }
            else
            {
                allClouds.Remove(cloud);
            }
        }
    }
    
    private Vector2Int GetChunkCoordinate(Vector3 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / chunkSize),
            Mathf.FloorToInt(position.z / chunkSize)
        );
    }
    
    private int GetSeedForChunk(Vector2Int chunkCoord)
    {
        int seedX = chunkCoord.x * SEED_MULTIPLIER_X;
        int seedZ = chunkCoord.y * SEED_MULTIPLIER_Z;
        return (seedX ^ seedZ) + generationSeed;
    }
    
    private void RemoveChunk(Vector2Int chunkCoord)
    {
        if (!cloudChunks.ContainsKey(chunkCoord)) return;
        
        foreach (VolumetricCloud cloud in cloudChunks[chunkCoord])
        {
            if (cloud != null)
            {
                allClouds.Remove(cloud);
                Destroy(cloud.gameObject);
            }
        }
        
        cloudChunks[chunkCoord].Clear();
    }
    
    private void RemoveDistantClouds()
    {
        if (trackingTarget == null) return;
        
        List<VolumetricCloud> cloudsToRemove = new List<VolumetricCloud>();
        
        foreach (VolumetricCloud cloud in allClouds)
        {
            if (cloud == null)
            {
                cloudsToRemove.Add(cloud);
                continue;
            }
            
            float distance = Vector3.Distance(cloud.transform.position, trackingTarget.position);
            if (distance > maxDistanceFromTarget)
            {
                cloudsToRemove.Add(cloud);
            }
        }
        
        foreach (VolumetricCloud cloud in cloudsToRemove)
        {
            if (cloud != null)
            {
                
                Vector2Int chunkCoord = GetChunkCoordinate(cloud.transform.position);
                if (cloudChunks.ContainsKey(chunkCoord))
                {
                    cloudChunks[chunkCoord].Remove(cloud);
                }
                
                allClouds.Remove(cloud);
                Destroy(cloud.gameObject);
            }
            else
            {
                
                allClouds.Remove(cloud);
            }
        }
    }
    
    private void CleanupDestroyedClouds()
    {
        
        allClouds.RemoveAll(cloud => cloud == null);
        
        
        foreach (var chunkKey in cloudChunks.Keys)
        {
            cloudChunks[chunkKey].RemoveAll(cloud => cloud == null);
        }
    }
    
    private void RefillChunksWithLowClouds()
    {
        if (trackingTarget == null) return;
        
        Vector2Int currentChunk = GetChunkCoordinate(trackingTarget.position);
        Vector3 windDirNormalized = windDirection.normalized;
        
        
        HashSet<Vector2Int> priorityChunks = new HashSet<Vector2Int>();
        
        
        Vector3 windSourcePosition = trackingTarget.position - windDirNormalized * spawnDistanceFromTarget * 0.8f;
        Vector2Int windSourceChunk = GetChunkCoordinate(windSourcePosition);
        priorityChunks.Add(windSourceChunk);
        
        
        for (int offset = -1; offset <= 1; offset++)
        {
            Vector3 offsetPos = windSourcePosition;
            if (Mathf.Abs(windDirNormalized.x) > 0.5f)
            {
                offsetPos.z += offset * chunkSize;
            }
            else if (Mathf.Abs(windDirNormalized.z) > 0.5f)
            {
                offsetPos.x += offset * chunkSize;
            }
            else
            {
                
                offsetPos.x += offset * chunkSize * 0.5f;
                offsetPos.z += offset * chunkSize * 0.5f;
            }
            priorityChunks.Add(GetChunkCoordinate(offsetPos));
        }
        
        
        foreach (Vector2Int chunkCoord in priorityChunks)
        {
            if (cloudChunks.ContainsKey(chunkCoord))
            {
                cloudChunks[chunkCoord].RemoveAll(cloud => cloud == null);
                int currentCloudCount = cloudChunks[chunkCoord].Count;
                
                if (currentCloudCount < cloudsPerChunk)
                {
                    int cloudsNeeded = cloudsPerChunk - currentCloudCount;
                    int generateAmount = currentCloudCount == 0 
                        ? Mathf.Min(cloudsNeeded, cloudsPerChunk)
                        : Mathf.Min(cloudsNeeded, Mathf.Max(2, cloudsPerChunk / 2));
                    
                    GenerateCloudsInChunk(chunkCoord, generateAmount);
                }
            }
            else
            {
                
                GenerateChunk(chunkCoord);
            }
        }
        
        
        for (int x = -loadRadius; x <= loadRadius; x++)
        {
            for (int z = -loadRadius; z <= loadRadius; z++)
            {
                Vector2Int chunkCoord = new Vector2Int(currentChunk.x + x, currentChunk.y + z);
                
                
                if (priorityChunks.Contains(chunkCoord))
                    continue;
                
                if (cloudChunks.ContainsKey(chunkCoord))
                {
                    cloudChunks[chunkCoord].RemoveAll(cloud => cloud == null);
                    int currentCloudCount = cloudChunks[chunkCoord].Count;
                    
                    
                    if (currentCloudCount == 0)
                    {
                        int generateAmount = Mathf.Min(cloudsPerChunk / 2, cloudsPerChunk);
                        GenerateCloudsInChunk(chunkCoord, generateAmount);
                    }
                    else if (currentCloudCount < cloudsPerChunk / 2)
                    {
                        
                        int cloudsNeeded = (cloudsPerChunk / 2) - currentCloudCount;
                        GenerateCloudsInChunk(chunkCoord, Mathf.Min(cloudsNeeded, cloudsPerChunk / 3));
                    }
                }
            }
        }
    }
    
    private void GenerateCloudsInChunk(Vector2Int chunkCoord, int count)
    {
        if (count <= 0) return;
        
        
        if (useAtmosphereLimit && trackingTarget != null)
        {
            float shipHeight = trackingTarget.position.y;
            float maxAtmosphereY = GetAtmosphereHeight();
            
            if (shipHeight > maxAtmosphereY)
            {
                return;
            }
        }
        
        
        generationCounter++;
        
        int generatedCount = 0;
        int attempts = 0;
        int maxAttempts = count * 5; 
        
        
        int existingCloudCount = cloudChunks.ContainsKey(chunkCoord) ? cloudChunks[chunkCoord].Count : 0;
        
        if (!cloudChunks.ContainsKey(chunkCoord))
        {
            cloudChunks[chunkCoord] = new List<VolumetricCloud>();
        }
        
        
        float currentShipHeight = trackingTarget != null ? trackingTarget.position.y : 0f;
        
        while (generatedCount < count && attempts < maxAttempts)
        {
            attempts++;
            
            
            
            int uniqueIndex = existingCloudCount + attempts + generationCounter * 1000 + (int)(Time.time * 100) % 1000;
            Vector3 position = GetRandomPositionInChunk(chunkCoord, uniqueIndex);
            
            
            if (generateAroundShipHeight && trackingTarget != null)
            {
                if (position.y <= currentShipHeight)
                {
                    
                    position.y = currentShipHeight + Random.Range(50f, maxCloudHeightAboveShip * 0.7f);
                }
            }
            
            
            if (useAtmosphereLimit)
            {
                float maxAtmosphereY = GetAtmosphereHeight();
                float groundHeight = HillGenerator.GetHeightAtPosition(new Vector3(position.x, 0, position.z));
                float minAtmosphereY = groundHeight + minHeightAboveGround;
                
                
                if (generateAroundShipHeight && trackingTarget != null)
                {
                    float minYAboveShip = currentShipHeight + 50f;
                    minAtmosphereY = Mathf.Max(minAtmosphereY, minYAboveShip);
                }
                
                
                if (position.y > maxAtmosphereY)
                {
                    
                    if (generateAroundShipHeight && trackingTarget != null && currentShipHeight < maxAtmosphereY)
                    {
                        position.y = currentShipHeight + Random.Range(50f, Mathf.Min(maxCloudHeightAboveShip * 0.7f, maxAtmosphereY - currentShipHeight - 10f));
                    }
                    else
                    {
                        continue; 
                    }
                }
                
                
                if (position.y < minAtmosphereY)
                {
                    
                    if (generateAroundShipHeight && trackingTarget != null)
                    {
                        position.y = Mathf.Max(minAtmosphereY, currentShipHeight + 50f);
                    }
                    else
                    {
                        position.y = minAtmosphereY;
                    }
                }
            }
            
            VolumetricCloud cloud = CreateCloud(position);
            
            if (cloud != null)
            {
                float cloudWindSpeed = windSpeed * Random.Range(0.8f, 1.2f);
                cloud.SetWind(windDirection, cloudWindSpeed);
                cloudChunks[chunkCoord].Add(cloud);
                generatedCount++;
            }
        }
        
        
        if (generatedCount < count && attempts >= maxAttempts)
        {
            Debug.LogWarning($"️ Не удалось сгенерировать все облака в чанке {chunkCoord}: {generatedCount}/{count} (попыток: {attempts})");
        }
    }
    
    private void GenerateCloudsAhead()
    {
        if (trackingTarget == null || windSpeed < 0.1f) return;
        
        
        float shipHeight = trackingTarget.position.y;
        float maxAtmosphereY = GetAtmosphereHeight();
        
        if (useAtmosphereLimit && shipHeight > maxAtmosphereY)
        {
            
            return;
        }
        
        Vector3 windDirNormalized = windDirection.normalized;
        
        
        
        Vector3 sourcePosition = trackingTarget.position - windDirNormalized * spawnDistanceFromTarget;
        Vector2Int sourceChunk = GetChunkCoordinate(sourcePosition);
        
        
        Vector3 aheadPosition = trackingTarget.position + windDirNormalized * spawnDistanceFromTarget;
        Vector2Int aheadChunk = GetChunkCoordinate(aheadPosition);
        
        
        if (!cloudChunks.ContainsKey(sourceChunk) || cloudChunks[sourceChunk].Count < cloudsPerChunk)
        {
            
            if (cloudChunks.ContainsKey(sourceChunk))
            {
                int currentCount = cloudChunks[sourceChunk].Count;
                int needed = cloudsPerChunk - currentCount;
                if (needed > 0)
                {
                    int toGenerate = Mathf.Min(needed, cloudsPerChunk / 2);
                    GenerateCloudsInChunk(sourceChunk, toGenerate);
                }
            }
            else
            {
                GenerateChunk(sourceChunk);
            }
        }
        
        
        if (!cloudChunks.ContainsKey(aheadChunk) || cloudChunks[aheadChunk].Count < cloudsPerChunk)
        {
            if (cloudChunks.ContainsKey(aheadChunk))
            {
                int currentCount = cloudChunks[aheadChunk].Count;
                int needed = cloudsPerChunk - currentCount;
                if (needed > 0)
                {
                    int toGenerate = Mathf.Min(needed, cloudsPerChunk / 2);
                    GenerateCloudsInChunk(aheadChunk, toGenerate);
                }
            }
            else
            {
                GenerateChunk(aheadChunk);
            }
        }
    }

    public VolumetricCloud CreateCloud(Vector3 position, float density = 1f)
    {
        if (cloudPrefab == null) return null;
        
        GameObject cloudObj = Instantiate(cloudPrefab, position, Quaternion.identity, cloudsContainer);
        cloudObj.name = $"Cloud_{allClouds.Count + 1}";
        
        VolumetricCloud cloud = cloudObj.GetComponent<VolumetricCloud>();
        
        if (cloud != null)
        {
            cloud.SetDensity(density);
            allClouds.Add(cloud);
            cloud.SetWind(windDirection, windSpeed);
        }
        else
        {
            Destroy(cloudObj);
        }
        
        return cloud;
    }

    public void SetGlobalWind(Vector3 direction, float speed)
    {
        windDirection = direction.normalized;
        windSpeed = speed;

        foreach (VolumetricCloud cloud in allClouds)
        {
            if (cloud != null)
        {
            cloud.SetWind(windDirection, windSpeed);
            }
        }
    }

    public List<VolumetricCloud> GetAllClouds() => allClouds;

    public int GetCloudCount() => allClouds.Count;

    public void RegenerateClouds()
    {
        foreach (VolumetricCloud cloud in allClouds)
        {
            if (cloud != null)
        {
            Destroy(cloud.gameObject);
            }
        }
        allClouds.Clear();
        cloudChunks.Clear();
        
        InitializeCloudGeneration();
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || trackingTarget == null) return;
        
        Vector2Int currentChunk = GetChunkCoordinate(trackingTarget.position);
        
        
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        for (int x = -loadRadius; x <= loadRadius; x++)
        {
            for (int z = -loadRadius; z <= loadRadius; z++)
            {
                Vector2Int chunkCoord = new Vector2Int(currentChunk.x + x, currentChunk.y + z);
                Vector3 chunkCenter = new Vector3(
                    chunkCoord.x * chunkSize + chunkSize * 0.5f,
                    0,
                    chunkCoord.y * chunkSize + chunkSize * 0.5f
                );
                
                Gizmos.DrawWireCube(chunkCenter, new Vector3(chunkSize, maxHeightAboveGround, chunkSize));
            }
        }
        
        
        Gizmos.color = new Color(1, 0, 0, 0.2f);
        Gizmos.DrawWireSphere(trackingTarget.position, maxDistanceFromTarget);
        
        
        if (windSpeed > 0.1f)
        {
            Vector3 aheadPos = trackingTarget.position + windDirection.normalized * spawnDistanceFromTarget;
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.DrawWireSphere(aheadPos, chunkSize * 0.5f);
        }
    }
}
