using UnityEngine;
using System.Collections.Generic;




public class TurbulenceManager : MonoBehaviour
{
    [Header("Turbulence Settings")]
    [SerializeField] private bool enableTurbulence = true;
    [SerializeField] private float globalTurbulenceStrength = 1f;
    [SerializeField] private bool findZonesAutomatically = true;
    
    [Header("Zone Management")]
    [SerializeField] private List<TurbulenceZone> turbulenceZones = new List<TurbulenceZone>();
    [SerializeField] private float updateInterval = 0.1f;
    
    [Header("Zone Generation")]
    [SerializeField] private bool generateZonesAutomatically = true;
    [SerializeField] private GameObject turbulenceZonePrefab; 
    [SerializeField] private int zonesPerChunk = 3;
    [SerializeField] private float chunkSize = 500f;
    [SerializeField] private int loadRadius = 3;
    [SerializeField] private float minHeightAboveGround = 20f; 
    [SerializeField] private float maxHeightAboveGround = 200f; 
    [SerializeField] private Vector3 minZoneSize = new Vector3(50f, 30f, 50f);
    [SerializeField] private Vector3 maxZoneSize = new Vector3(150f, 80f, 150f);
    [SerializeField] private float minTurbulenceStrength = 5f;
    [SerializeField] private float maxTurbulenceStrength = 15f;
    [SerializeField] private int generationSeed = 54321;
    [SerializeField] private bool showGeneratedZonesInGame = true; 
    
    [Header("Atmosphere Settings")]
    [SerializeField] private float atmosphereHeight = 1000f; 
    [SerializeField] private bool useAtmosphereLimit = true;
    [SerializeField] private bool generateFullAtmosphereHeight = true; 
    [SerializeField] private bool generateAroundShipHeight = true; 
    
    [Header("Tracking Settings")]
    [SerializeField] private Transform trackingTarget;
    [SerializeField] private float generationUpdateInterval = 1f; 
    [SerializeField] private float maxDistanceFromTarget = 1200f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    
    private float lastUpdateTime = 0f;
    private float lastGenerationUpdateTime = 0f;
    private float lastShipHeight = 0f; 
    private ShipController shipController;
    private Dictionary<Vector2Int, List<TurbulenceZone>> zoneChunks = new Dictionary<Vector2Int, List<TurbulenceZone>>();
    private Vector2Int lastChunkCoord = Vector2Int.zero;
    
    private void Start()
    {
        if (findZonesAutomatically && !generateZonesAutomatically)
        {
            FindAllTurbulenceZones();
        }
        
        shipController = FindObjectOfType<ShipController>();
        
        
        if (trackingTarget == null)
        {
            if (shipController != null)
            {
                trackingTarget = shipController.transform;
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
        
        
        if (generateZonesAutomatically)
        {
            InitializeZoneGeneration();
        }
    }
    
    private void Update()
    {
        if (!enableTurbulence || shipController == null) return;
        
        if (Time.time - lastUpdateTime > updateInterval)
        {
            lastUpdateTime = Time.time;
            UpdateTurbulenceZones();
        }
        
        
        if (generateZonesAutomatically && trackingTarget != null)
        {
            if (Time.time - lastGenerationUpdateTime > generationUpdateInterval)
            {
                lastGenerationUpdateTime = Time.time;
                UpdateZoneGeneration();
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
                            if (zoneChunks.ContainsKey(chunkCoord))
                            {
                                zoneChunks[chunkCoord].RemoveAll(zone => zone == null);
                                if (zoneChunks[chunkCoord].Count < zonesPerChunk)
                                {
                                    int toGenerate = Mathf.Max(1, (zonesPerChunk - zoneChunks[chunkCoord].Count) / 2);
                                    GenerateZonesInChunk(chunkCoord, toGenerate);
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
        }
    }
    
    private void FindAllTurbulenceZones()
    {
        turbulenceZones.Clear();
        turbulenceZones.AddRange(FindObjectsOfType<TurbulenceZone>());
        
        if (showDebugInfo)
        {
            Debug.Log($"TurbulenceManager: найдено {turbulenceZones.Count} зон турбулентности");
        }
    }
    
    private void UpdateTurbulenceZones()
    {
        if (shipController == null) return;
        
        
        if (findZonesAutomatically && !generateZonesAutomatically)
        {
            turbulenceZones.RemoveAll(zone => zone == null);
        }
        
        
        
    }
    
    
    
    
    public Vector3 GetTurbulenceForce(Vector3 worldPosition, float deltaTime)
    {
        if (!enableTurbulence || turbulenceZones.Count == 0) return Vector3.zero;
        
        Vector3 totalForce = Vector3.zero;
        
        foreach (TurbulenceZone zone in turbulenceZones)
        {
            if (zone == null) continue;
            
            float intensity = zone.GetTurbulenceIntensity(worldPosition);
            if (intensity > 0.01f)
            {
                Vector3 zoneForce = zone.GetTurbulenceForce(worldPosition, deltaTime);
                totalForce += zoneForce * intensity * globalTurbulenceStrength;
            }
        }
        
        return totalForce;
    }
    
    
    
    
    public Vector3 GetTurbulenceTorque(Vector3 worldPosition, float deltaTime)
    {
        if (!enableTurbulence || turbulenceZones.Count == 0) return Vector3.zero;
        
        Vector3 totalTorque = Vector3.zero;
        
        foreach (TurbulenceZone zone in turbulenceZones)
        {
            if (zone == null) continue;
            
            float intensity = zone.GetTurbulenceIntensity(worldPosition);
            if (intensity > 0.01f)
            {
                Vector3 zoneTorque = zone.GetTurbulenceTorque(worldPosition, deltaTime);
                totalTorque += zoneTorque * intensity * globalTurbulenceStrength;
            }
        }
        
        return totalTorque;
    }
    
    
    
    
    public void AddTurbulenceZone(TurbulenceZone zone)
    {
        if (zone != null && !turbulenceZones.Contains(zone))
        {
            turbulenceZones.Add(zone);
        }
    }
    
    
    
    
    public void RemoveTurbulenceZone(TurbulenceZone zone)
    {
        turbulenceZones.Remove(zone);
    }
    
    
    
    
    public List<TurbulenceZone> GetAllZones() => turbulenceZones;
    
    
    
    
    public int GetZoneCount() => turbulenceZones.Count;
    
    
    
    private void InitializeZoneGeneration()
    {
        if (trackingTarget == null) return;
        
        Vector2Int currentChunk = GetChunkCoordinate(trackingTarget.position);
        lastChunkCoord = currentChunk;
        lastShipHeight = trackingTarget.position.y; 
        
        
        for (int x = -loadRadius; x <= loadRadius; x++)
        {
            for (int z = -loadRadius; z <= loadRadius; z++)
            {
                Vector2Int chunkCoord = new Vector2Int(currentChunk.x + x, currentChunk.y + z);
                GenerateChunk(chunkCoord);
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"TurbulenceManager: инициализирована генерация зон в {zoneChunks.Count} чанках");
        }
    }
    
    private void UpdateZoneGeneration()
    {
        if (trackingTarget == null)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("TurbulenceManager: trackingTarget is null");
            }
            return;
        }
        
        Vector3 shipPos = trackingTarget.position;
        Vector2Int currentChunk = GetChunkCoordinate(shipPos);
        
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"TurbulenceManager: корабль на позиции {shipPos}, текущий чанк = {currentChunk}, последний чанк = {lastChunkCoord}, всего чанков = {zoneChunks.Count}");
        }
        bool chunkChanged = currentChunk != lastChunkCoord;
        
        if (chunkChanged)
        {
            lastChunkCoord = currentChunk;
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
            foreach (var chunkCoord in zoneChunks.Keys)
            {
                if (!neededChunks.Contains(chunkCoord))
                {
                    RemoveChunk(chunkCoord);
                    chunksToRemove.Add(chunkCoord);
                }
            }
            
            foreach (var chunkCoord in chunksToRemove)
            {
                zoneChunks.Remove(chunkCoord);
            }
        }
        
        
        foreach (var chunkCoord in neededChunks)
        {
            if (!zoneChunks.ContainsKey(chunkCoord))
            {
                if (showDebugInfo)
                {
                    Debug.Log($"TurbulenceManager: обнаружен отсутствующий чанк {chunkCoord}, генерирую.");
                }
                GenerateChunk(chunkCoord);
            }
        }
    }
    
    private Vector2Int GetChunkCoordinate(Vector3 worldPosition)
    {
        
        int chunkX = Mathf.FloorToInt(worldPosition.x / chunkSize);
        int chunkZ = Mathf.FloorToInt(worldPosition.z / chunkSize);
        
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"TurbulenceManager: getChunkCoordinate - позиция {worldPosition} -> чанк ({chunkX}, {chunkZ}), chunkSize={chunkSize}");
        }
        
        return new Vector2Int(chunkX, chunkZ);
    }
    
    private void GenerateChunk(Vector2Int chunkCoord)
    {
        if (zoneChunks.ContainsKey(chunkCoord))
        {
            if (showDebugInfo)
            {
                Debug.Log($"TurbulenceManager: чанк {chunkCoord} уже существует, пропускаю");
            }
            return; 
        }
        
        zoneChunks[chunkCoord] = new List<TurbulenceZone>();
        
        
        int baseSeed = GetSeedForChunk(chunkCoord);
        
        if (showDebugInfo)
        {
            Debug.Log($"TurbulenceManager: генерирую чанк {chunkCoord} с {zonesPerChunk} зонами, seed={baseSeed}");
        }
        
        for (int i = 0; i < zonesPerChunk; i++)
        {
            Vector3 zonePosition = GetRandomPositionInChunk(chunkCoord, i, baseSeed);
            TurbulenceZone zone = CreateTurbulenceZone(zonePosition, chunkCoord, i, baseSeed);
            
            if (zone != null)
            {
                zoneChunks[chunkCoord].Add(zone);
                turbulenceZones.Add(zone);
                
                if (showDebugInfo)
                {
                    Debug.Log($"TurbulenceManager: создана зона {i} в чанке {chunkCoord} на позиции {zonePosition}");
                }
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"TurbulenceManager: чанк {chunkCoord} сгенерирован, создано {zoneChunks[chunkCoord].Count} зон");
        }
    }
    
    private void GenerateZonesInChunk(Vector2Int chunkCoord, int count)
    {
        if (!zoneChunks.ContainsKey(chunkCoord))
        {
            zoneChunks[chunkCoord] = new List<TurbulenceZone>();
        }
        
        int baseSeed = GetSeedForChunk(chunkCoord);
        int startIndex = zoneChunks[chunkCoord].Count;
        
        if (showDebugInfo)
        {
            Debug.Log($"TurbulenceManager: догенерирую {count} зон в чанке {chunkCoord}, seed={baseSeed}");
        }
        
        for (int i = 0; i < count; i++)
        {
            int zoneIndex = startIndex + i;
            Vector3 zonePosition = GetRandomPositionInChunk(chunkCoord, zoneIndex, baseSeed);
            TurbulenceZone zone = CreateTurbulenceZone(zonePosition, chunkCoord, zoneIndex, baseSeed);
            
            if (zone != null)
            {
                zoneChunks[chunkCoord].Add(zone);
                turbulenceZones.Add(zone);
                
                if (showDebugInfo)
                {
                    Debug.Log($"TurbulenceManager: создана дополнительная зона {zoneIndex} в чанке {chunkCoord} на позиции {zonePosition}");
                }
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"TurbulenceManager: в чанке {chunkCoord} теперь {zoneChunks[chunkCoord].Count} зон");
        }
    }
    
    private int GetSeedForChunk(Vector2Int chunkCoord)
    {
        
        return generationSeed + chunkCoord.x * 73856093 + chunkCoord.y * 19349663;
    }
    
    private Vector3 GetRandomPositionInChunk(Vector2Int chunkCoord, int zoneIndex, int baseSeed)
    {
        
        Random.InitState(baseSeed + zoneIndex * 12345);
        
        float localX = Random.Range(0f, chunkSize);
        float localZ = Random.Range(0f, chunkSize);
        
        float worldX = chunkCoord.x * chunkSize + localX;
        float worldZ = chunkCoord.y * chunkSize + localZ;
        
        
        float groundHeight = HillGenerator.GetHeightAtPosition(new Vector3(worldX, 0f, worldZ));
        
        float y;
        
        if (generateFullAtmosphereHeight && useAtmosphereLimit)
        {
            
            
            float maxAtmosphereY = atmosphereHeight;
            float minZoneY = groundHeight + minHeightAboveGround;
            float maxZoneY = maxAtmosphereY - 10f; 
            
            
            
            Random.InitState(baseSeed + zoneIndex * 17 + (int)(Time.time * 50) % 1000);
            y = Random.Range(minZoneY, maxZoneY);
            
            
            
        }
        else if (generateAroundShipHeight && trackingTarget != null)
        {
            
            float shipHeight = trackingTarget.position.y;
            
            
            float minOffsetAboveShip = 50f;
            
            
            float baseOffsetUp = minOffsetAboveShip;
            
            
            float maxHeightAboveShip = maxHeightAboveGround;
            
            
            Random.InitState(baseSeed + zoneIndex * 23);
            float offsetAboveShip = Random.Range(baseOffsetUp, maxHeightAboveShip);
            y = shipHeight + offsetAboveShip;
            
            
            if (useAtmosphereLimit)
            {
                float maxAtmosphereY = atmosphereHeight;
                y = Mathf.Min(y, maxAtmosphereY - 10f);
            }
            
            
            y = Mathf.Max(y, groundHeight + minHeightAboveGround);
        }
        else
        {
            
            float heightAboveGround = Random.Range(minHeightAboveGround, maxHeightAboveGround);
            y = groundHeight + heightAboveGround;
            
            
            if (trackingTarget != null)
            {
                float shipHeight = trackingTarget.position.y;
                
                float minHeightFromShip = Mathf.Max(groundHeight + minHeightAboveGround, shipHeight - 50f); 
                float maxHeightFromShip = shipHeight + maxHeightAboveGround; 
                
                y = Mathf.Clamp(y, minHeightFromShip, maxHeightFromShip);
            }
            
            
            if (useAtmosphereLimit)
            {
                float maxAtmosphereY = atmosphereHeight;
                y = Mathf.Min(y, maxAtmosphereY - 10f);
            }
        }
        
        if (showDebugInfo)
        {
            string heightInfo = trackingTarget != null ? $", корабль Y={trackingTarget.position.y:F1}" : "";
            Debug.Log($"TurbulenceManager: позиция зоны в чанке {chunkCoord}: X={worldX:F1}, Z={worldZ:F1}, Y={y:F1}" +
                     $"(земля={groundHeight:F1}{heightInfo}, атмосфера до {atmosphereHeight:F1})");
        }
        
        return new Vector3(worldX, y, worldZ);
    }
    
    private TurbulenceZone CreateTurbulenceZone(Vector3 position, Vector2Int chunkCoord, int zoneIndex, int baseSeed)
    {
        GameObject zoneObject;
        
        if (turbulenceZonePrefab != null)
        {
            zoneObject = Instantiate(turbulenceZonePrefab, position, Quaternion.identity, transform);
        }
        else
        {
            
            zoneObject = new GameObject($"TurbulenceZone_{chunkCoord.x}_{chunkCoord.y}_{zoneIndex}");
            zoneObject.transform.position = position;
            zoneObject.transform.SetParent(transform);
        }
        
        TurbulenceZone turbulenceZone = zoneObject.GetComponent<TurbulenceZone>();
        if (turbulenceZone == null)
        {
            turbulenceZone = zoneObject.AddComponent<TurbulenceZone>();
        }
        
        
        int zoneSeed = baseSeed + zoneIndex * 12345;
        Random.InitState(zoneSeed);
        
        
        Vector3 randomSize = new Vector3(
            Random.Range(minZoneSize.x, maxZoneSize.x),
            Random.Range(minZoneSize.y, maxZoneSize.y),
            Random.Range(minZoneSize.z, maxZoneSize.z)
        );
        
        
        float randomStrength = Random.Range(minTurbulenceStrength, maxTurbulenceStrength);
        
        
        turbulenceZone.SetZoneSize(randomSize);
        turbulenceZone.SetTurbulenceStrength(randomStrength);
        
        
        
        turbulenceZone.SetShowInGame(showGeneratedZonesInGame);
        
        
        
        if (showGeneratedZonesInGame && Application.isPlaying)
        {
            
            
            
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"TurbulenceManager: зона настроена - размер={randomSize}, сила={randomStrength:F1}, showInGame={showGeneratedZonesInGame}, позиция={position}");
        }
        
        return turbulenceZone;
    }
    
    
    
    
    
    private void RemoveChunk(Vector2Int chunkCoord)
    {
        if (!zoneChunks.ContainsKey(chunkCoord)) return;
        
        if (showDebugInfo)
        {
            Debug.Log($"TurbulenceManager: удаляю чанк {chunkCoord} с {zoneChunks[chunkCoord].Count} зонами");
        }
        
        foreach (TurbulenceZone zone in zoneChunks[chunkCoord])
        {
            if (zone != null)
            {
                turbulenceZones.Remove(zone);
                Destroy(zone.gameObject);
            }
        }
        
        zoneChunks[chunkCoord].Clear();
    }
}
