using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Менеджер турбулентности - управляет всеми зонами турбулентности и применяет их к кораблю
/// </summary>
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
    [SerializeField] private GameObject turbulenceZonePrefab; // Префаб зоны (опционально)
    [SerializeField] private int zonesPerChunk = 3;
    [SerializeField] private float chunkSize = 500f;
    [SerializeField] private int loadRadius = 3;
    [SerializeField] private float minHeightAboveGround = 20f; // Уменьшено - зоны ближе к земле
    [SerializeField] private float maxHeightAboveGround = 200f; // Уменьшено - зоны не слишком высоко
    [SerializeField] private Vector3 minZoneSize = new Vector3(50f, 30f, 50f);
    [SerializeField] private Vector3 maxZoneSize = new Vector3(150f, 80f, 150f);
    [SerializeField] private float minTurbulenceStrength = 5f;
    [SerializeField] private float maxTurbulenceStrength = 15f;
    [SerializeField] private int generationSeed = 54321;
    [SerializeField] private bool showGeneratedZonesInGame = true; // Показывать генерируемые зоны в Game View
    
    [Header("Atmosphere Settings")]
    [SerializeField] private float atmosphereHeight = 1000f; // Высота атмосферы (как у облаков)
    [SerializeField] private bool useAtmosphereLimit = true;
    [SerializeField] private bool generateFullAtmosphereHeight = true; // Генерировать зоны во всем диапазоне высот атмосферы
    [SerializeField] private bool generateAroundShipHeight = true; // Генерировать зоны вокруг высоты корабля
    
    [Header("Tracking Settings")]
    [SerializeField] private Transform trackingTarget;
    [SerializeField] private float generationUpdateInterval = 1f; // Как у облаков - обновление каждую секунду
    [SerializeField] private float maxDistanceFromTarget = 1200f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    
    private float lastUpdateTime = 0f;
    private float lastGenerationUpdateTime = 0f;
    private float lastShipHeight = 0f; // Отслеживание изменения высоты корабля
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
        
        // Находим цель для отслеживания
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
        
        // Инициализируем генерацию зон, если включена
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
        
        // Обновляем генерацию зон (как у облаков - всегда проверяем)
        if (generateZonesAutomatically && trackingTarget != null)
        {
            if (Time.time - lastGenerationUpdateTime > generationUpdateInterval)
            {
                lastGenerationUpdateTime = Time.time;
                UpdateZoneGeneration();
            }
            
            // При полной генерации атмосферы также проверяем изменение высоты в реальном времени
            if (generateFullAtmosphereHeight && trackingTarget != null)
            {
                float currentShipHeight = trackingTarget.position.y;
                float heightDifference = Mathf.Abs(currentShipHeight - lastShipHeight);
                
                // Если высота изменилась значительно, сразу обновляем чанки
                if (heightDifference > 30f) // Более чувствительная проверка для полной генерации
                {
                    lastShipHeight = currentShipHeight;
                    // Быстро проверяем и догенерируем зоны в текущих чанках
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
                                // Чанк не существует - создаем его
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
            Debug.Log($"TurbulenceManager: Найдено {turbulenceZones.Count} зон турбулентности");
        }
    }
    
    private void UpdateTurbulenceZones()
    {
        if (shipController == null) return;
        
        // Обновляем список зон, если они изменились
        if (findZonesAutomatically && !generateZonesAutomatically)
        {
            turbulenceZones.RemoveAll(zone => zone == null);
        }
        
        // Удаление зон теперь обрабатывается через систему чанков в UpdateZoneGeneration()
        // RemoveDistantZones() больше не нужен, так как он конфликтует с системой чанков
    }
    
    /// <summary>
    /// Получает суммарную силу турбулентности для заданной позиции
    /// </summary>
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
    
    /// <summary>
    /// Получает суммарный момент турбулентности (вращение) для заданной позиции
    /// </summary>
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
    
    /// <summary>
    /// Добавляет зону турбулентности
    /// </summary>
    public void AddTurbulenceZone(TurbulenceZone zone)
    {
        if (zone != null && !turbulenceZones.Contains(zone))
        {
            turbulenceZones.Add(zone);
        }
    }
    
    /// <summary>
    /// Удаляет зону турбулентности
    /// </summary>
    public void RemoveTurbulenceZone(TurbulenceZone zone)
    {
        turbulenceZones.Remove(zone);
    }
    
    /// <summary>
    /// Получает все зоны турбулентности
    /// </summary>
    public List<TurbulenceZone> GetAllZones() => turbulenceZones;
    
    /// <summary>
    /// Получает количество активных зон
    /// </summary>
    public int GetZoneCount() => turbulenceZones.Count;
    
    // ========== ГЕНЕРАЦИЯ ЗОН ==========
    
    private void InitializeZoneGeneration()
    {
        if (trackingTarget == null) return;
        
        Vector2Int currentChunk = GetChunkCoordinate(trackingTarget.position);
        lastChunkCoord = currentChunk;
        lastShipHeight = trackingTarget.position.y; // Инициализируем отслеживание высоты
        
        // Генерируем зоны в начальных чанках
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
            Debug.Log($"TurbulenceManager: Инициализирована генерация зон в {zoneChunks.Count} чанках");
        }
    }
    
    private void UpdateZoneGeneration()
    {
        if (trackingTarget == null)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("TurbulenceManager: trackingTarget is null!");
            }
            return;
        }
        
        Vector3 shipPos = trackingTarget.position;
        Vector2Int currentChunk = GetChunkCoordinate(shipPos);
        
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"TurbulenceManager: Корабль на позиции {shipPos}, текущий чанк = {currentChunk}, последний чанк = {lastChunkCoord}, всего чанков = {zoneChunks.Count}");
        }
        bool chunkChanged = currentChunk != lastChunkCoord;
        
        if (chunkChanged)
        {
            lastChunkCoord = currentChunk;
        }
        
        // Определяем, какие чанки нужны (как у облаков)
        HashSet<Vector2Int> neededChunks = new HashSet<Vector2Int>();
        for (int x = -loadRadius; x <= loadRadius; x++)
        {
            for (int z = -loadRadius; z <= loadRadius; z++)
            {
                neededChunks.Add(new Vector2Int(currentChunk.x + x, currentChunk.y + z));
            }
        }
        
        // Удаляем далекие чанки (только при изменении горизонтальных координат)
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
        
        // Генерируем новые чанки, если их нет (ВСЕГДА проверяем, не только при изменении чанка)
        foreach (var chunkCoord in neededChunks)
        {
            if (!zoneChunks.ContainsKey(chunkCoord))
            {
                if (showDebugInfo)
                {
                    Debug.Log($"TurbulenceManager: Обнаружен отсутствующий чанк {chunkCoord}, генерирую...");
                }
                GenerateChunk(chunkCoord);
            }
        }
    }
    
    private Vector2Int GetChunkCoordinate(Vector3 worldPosition)
    {
        // Используем ту же логику, что и у облаков
        int chunkX = Mathf.FloorToInt(worldPosition.x / chunkSize);
        int chunkZ = Mathf.FloorToInt(worldPosition.z / chunkSize);
        
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"TurbulenceManager: GetChunkCoordinate - позиция {worldPosition} -> чанк ({chunkX}, {chunkZ}), chunkSize={chunkSize}");
        }
        
        return new Vector2Int(chunkX, chunkZ);
    }
    
    private void GenerateChunk(Vector2Int chunkCoord)
    {
        if (zoneChunks.ContainsKey(chunkCoord))
        {
            if (showDebugInfo)
            {
                Debug.Log($"TurbulenceManager: Чанк {chunkCoord} уже существует, пропускаю");
            }
            return; // Чанк уже сгенерирован
        }
        
        zoneChunks[chunkCoord] = new List<TurbulenceZone>();
        
        // Генерируем зоны в чанке
        int baseSeed = GetSeedForChunk(chunkCoord);
        
        if (showDebugInfo)
        {
            Debug.Log($"TurbulenceManager: Генерирую чанк {chunkCoord} с {zonesPerChunk} зонами, seed={baseSeed}");
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
                    Debug.Log($"TurbulenceManager: Создана зона {i} в чанке {chunkCoord} на позиции {zonePosition}");
                }
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"TurbulenceManager: Чанк {chunkCoord} сгенерирован, создано {zoneChunks[chunkCoord].Count} зон");
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
            Debug.Log($"TurbulenceManager: Догенерирую {count} зон в чанке {chunkCoord}, seed={baseSeed}");
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
                    Debug.Log($"TurbulenceManager: Создана дополнительная зона {zoneIndex} в чанке {chunkCoord} на позиции {zonePosition}");
                }
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"TurbulenceManager: В чанке {chunkCoord} теперь {zoneChunks[chunkCoord].Count} зон");
        }
    }
    
    private int GetSeedForChunk(Vector2Int chunkCoord)
    {
        // Используем координаты чанка для создания уникального сида
        return generationSeed + chunkCoord.x * 73856093 + chunkCoord.y * 19349663;
    }
    
    private Vector3 GetRandomPositionInChunk(Vector2Int chunkCoord, int zoneIndex, int baseSeed)
    {
        // Уникальный сид для каждой зоны в чанке
        Random.InitState(baseSeed + zoneIndex * 12345);
        
        float localX = Random.Range(0f, chunkSize);
        float localZ = Random.Range(0f, chunkSize);
        
        float worldX = chunkCoord.x * chunkSize + localX;
        float worldZ = chunkCoord.y * chunkSize + localZ;
        
        // Высота земли
        float groundHeight = HillGenerator.GetHeightAtPosition(new Vector3(worldX, 0f, worldZ));
        
        float y;
        
        if (generateFullAtmosphereHeight && useAtmosphereLimit)
        {
            // Генерируем зоны во всем диапазоне высот атмосферы (от минимальной высоты над землей до конца атмосферы)
            // Равномерное распределение по всей высоте атмосферы, независимо от высоты корабля
            float maxAtmosphereY = atmosphereHeight;
            float minZoneY = groundHeight + minHeightAboveGround;
            float maxZoneY = maxAtmosphereY - 10f; // Отступ от верхней границы атмосферы
            
            // Генерируем зоны во всем диапазоне высот атмосферы равномерно
            // Используем уникальный сид для высоты, чтобы каждая зона получала уникальную высоту
            Random.InitState(baseSeed + zoneIndex * 17 + (int)(Time.time * 50) % 1000);
            y = Random.Range(minZoneY, maxZoneY);
            
            // При полной генерации атмосферы НЕ учитываем высоту корабля - генерируем равномерно везде
            // Это гарантирует, что при взлете будут видны зоны на всех уровнях
        }
        else if (generateAroundShipHeight && trackingTarget != null)
        {
            // Генерируем зоны СТРОГО ВЫШЕ корабля (не ниже, не на той же высоте)
            float shipHeight = trackingTarget.position.y;
            
            // Минимальный отступ вверх от корабля (зона должна быть выше корабля)
            float minOffsetAboveShip = 50f;
            
            // Базовое смещение ВВЕРХ от корабля
            float baseOffsetUp = minOffsetAboveShip;
            
            // Максимальная высота генерации выше корабля
            float maxHeightAboveShip = maxHeightAboveGround;
            
            // Генерируем зону выше корабля
            Random.InitState(baseSeed + zoneIndex * 23);
            float offsetAboveShip = Random.Range(baseOffsetUp, maxHeightAboveShip);
            y = shipHeight + offsetAboveShip;
            
            // Ограничиваем максимальной высотой атмосферы, если включено
            if (useAtmosphereLimit)
            {
                float maxAtmosphereY = atmosphereHeight;
                y = Mathf.Min(y, maxAtmosphereY - 10f);
            }
            
            // Не ниже минимальной высоты над землей
            y = Mathf.Max(y, groundHeight + minHeightAboveGround);
        }
        else
        {
            // Старая логика: высота зоны над землей (но не слишком высоко от корабля)
            float heightAboveGround = Random.Range(minHeightAboveGround, maxHeightAboveGround);
            y = groundHeight + heightAboveGround;
            
            // Ограничиваем высоту зон - они должны быть в разумных пределах от корабля
            if (trackingTarget != null)
            {
                float shipHeight = trackingTarget.position.y;
                // Зоны должны быть в диапазоне от корабля до корабля + maxHeightAboveGround
                float minHeightFromShip = Mathf.Max(groundHeight + minHeightAboveGround, shipHeight - 50f); // Не ниже корабля более чем на 50м
                float maxHeightFromShip = shipHeight + maxHeightAboveGround; // Не выше корабля более чем на maxHeightAboveGround
                
                y = Mathf.Clamp(y, minHeightFromShip, maxHeightFromShip);
            }
            
            // Ограничиваем максимальной высотой атмосферы, если включено
            if (useAtmosphereLimit)
            {
                float maxAtmosphereY = atmosphereHeight;
                y = Mathf.Min(y, maxAtmosphereY - 10f);
            }
        }
        
        if (showDebugInfo)
        {
            string heightInfo = trackingTarget != null ? $", корабль Y={trackingTarget.position.y:F1}" : "";
            Debug.Log($"TurbulenceManager: Позиция зоны в чанке {chunkCoord}: X={worldX:F1}, Z={worldZ:F1}, Y={y:F1} " +
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
            // Создаем зону программно
            zoneObject = new GameObject($"TurbulenceZone_{chunkCoord.x}_{chunkCoord.y}_{zoneIndex}");
            zoneObject.transform.position = position;
            zoneObject.transform.SetParent(transform);
        }
        
        TurbulenceZone turbulenceZone = zoneObject.GetComponent<TurbulenceZone>();
        if (turbulenceZone == null)
        {
            turbulenceZone = zoneObject.AddComponent<TurbulenceZone>();
        }
        
        // Настраиваем случайные параметры
        int zoneSeed = baseSeed + zoneIndex * 12345;
        Random.InitState(zoneSeed);
        
        // Случайный размер зоны
        Vector3 randomSize = new Vector3(
            Random.Range(minZoneSize.x, maxZoneSize.x),
            Random.Range(minZoneSize.y, maxZoneSize.y),
            Random.Range(minZoneSize.z, maxZoneSize.z)
        );
        
        // Случайная сила турбулентности
        float randomStrength = Random.Range(minTurbulenceStrength, maxTurbulenceStrength);
        
        // Настраиваем зону
        turbulenceZone.SetZoneSize(randomSize);
        turbulenceZone.SetTurbulenceStrength(randomStrength);
        
        // Устанавливаем видимость в Game View ПОСЛЕ установки размера
        // Это важно, так как SetupGameVisualization использует zoneSize
        turbulenceZone.SetShowInGame(showGeneratedZonesInGame);
        
        // Принудительно создаем визуализацию сразу после настройки
        // Это нужно, так как Start может вызваться позже или showInGame может быть установлен после Start
        if (showGeneratedZonesInGame && Application.isPlaying)
        {
            // Вызываем SetupGameVisualization напрямую через рефлексию или публичный метод
            // Но лучше просто убедиться, что SetShowInGame правильно создает визуализацию
            // SetShowInGame уже вызывает SetupGameVisualization, так что должно работать
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"TurbulenceManager: Зона настроена - размер={randomSize}, сила={randomStrength:F1}, showInGame={showGeneratedZonesInGame}, позиция={position}");
        }
        
        return turbulenceZone;
    }
    
    // RemoveDistantZones() удален - удаление зон теперь обрабатывается только через систему чанков
    // Это предотвращает конфликты между удалением по расстоянию и удалением по чанкам
    // Зоны удаляются только когда их чанк выходит за пределы loadRadius
    
    private void RemoveChunk(Vector2Int chunkCoord)
    {
        if (!zoneChunks.ContainsKey(chunkCoord)) return;
        
        if (showDebugInfo)
        {
            Debug.Log($"TurbulenceManager: Удаляю чанк {chunkCoord} с {zoneChunks[chunkCoord].Count} зонами");
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
