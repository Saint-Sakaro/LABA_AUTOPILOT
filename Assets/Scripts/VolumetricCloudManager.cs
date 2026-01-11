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
    [SerializeField] private float maxCloudHeightAboveShip = 500f; // Максимальная высота генерации облаков выше корабля
    
    [Header("Atmosphere Settings")]
    [SerializeField] private float atmosphereHeight = 1000f; // Высота атмосферы (увеличено до 1000м для генерации облаков выше)
    [SerializeField] private bool useAtmosphereLimit = true;
    [SerializeField] private bool generateAroundShipHeight = true;
    [SerializeField] private bool generateFullAtmosphereHeight = true; // Генерировать облака во всем диапазоне высот атмосферы
    [SerializeField] private bool syncWithShipAtmosphere = false; // Отключено, чтобы использовать значение из VolumetricCloudManager
    
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
    private int generationCounter = 0; // Счетчик для уникальности генерации
    private const int SEED_MULTIPLIER_X = 73856093;
    private const int SEED_MULTIPLIER_Z = 19349663;
    
    private void Start()
    {
        if (cloudsContainer == null)
            cloudsContainer = transform;
        
        // Находим корабль для отслеживания
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
        
        // Проверяем существующие облака
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
            
            // Если облака уже есть, группируем их по чанкам
            OrganizeExistingCloudsIntoChunks();
        }
        else
        {
            // Генерируем новые облака
            InitializeCloudGeneration();
        }
    }
    
    private void Update()
    {
        if (trackingTarget == null) return;
        
        // Обновляем чанки периодически
        if (Time.time - lastUpdateTime > updateInterval)
        {
            lastUpdateTime = Time.time;
            UpdateCloudChunks();
        }
        
        // Очищаем null-ссылки из списков (облака, которые были уничтожены)
        CleanupDestroyedClouds();
        
        // Удаляем облака, которые ушли слишком далеко или выше атмосферы
        RemoveDistantClouds();
        RemoveCloudsAboveAtmosphere();
        
        // Проверяем и догенерируем облака в существующих чанках периодически
        // При полной генерации атмосферы проверяем чаще, чтобы облака генерировались при взлете
        float refillInterval = generateFullAtmosphereHeight ? refillCheckInterval * 0.5f : refillCheckInterval;
        if (Time.time - lastRefillCheckTime > refillInterval)
        {
            lastRefillCheckTime = Time.time;
            RefillChunksWithLowClouds();
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
                // Быстро проверяем и догенерируем облака в текущих чанках
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
                            // Чанк не существует - создаем его
                            GenerateChunk(chunkCoord);
                        }
                    }
                }
            }
        }
        
        // Генерируем новые облака впереди по направлению ветра
        GenerateCloudsAhead();
    }
    
    private void InitializeCloudGeneration()
    {
        if (trackingTarget == null) return;
        
        Vector2Int currentChunk = GetChunkCoordinate(trackingTarget.position);
        lastChunkCoord = currentChunk;
        
        // Генерируем облака в начальных чанках
        for (int x = -loadRadius; x <= loadRadius; x++)
        {
            for (int z = -loadRadius; z <= loadRadius; z++)
            {
                Vector2Int chunkCoord = new Vector2Int(currentChunk.x + x, currentChunk.y + z);
                GenerateChunk(chunkCoord);
            }
        }
        
        Debug.Log($"✅ Инициализирована генерация облаков в {cloudChunks.Count} чанках");
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
        
        Debug.Log($"✅ Организовано {allClouds.Count} существующих облаков в {cloudChunks.Count} чанках");
    }
    
    private void UpdateCloudChunks()
    {
        if (trackingTarget == null) return;
        
        Vector2Int currentChunk = GetChunkCoordinate(trackingTarget.position);
        float currentShipHeight = trackingTarget.position.y;
        float heightDifference = Mathf.Abs(currentShipHeight - lastShipHeight);
        
        bool chunkChanged = currentChunk != lastChunkCoord;
        bool heightChangedSignificantly = heightDifference > 50f; // Значительное изменение высоты (50м)
        
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
            
            // Определяем, какие чанки нужны
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
            
            // Генерируем новые чанки или обновляем существующие при изменении высоты
            foreach (var chunkCoord in neededChunks)
            {
                if (!cloudChunks.ContainsKey(chunkCoord))
                {
                    // Новый чанк - генерируем полностью
                    GenerateChunk(chunkCoord);
                }
                else if (heightChangedSignificantly || generateFullAtmosphereHeight)
                {
                    // Высота изменилась или включена полная генерация - проверяем, нужно ли догенерировать облака
                    cloudChunks[chunkCoord].RemoveAll(cloud => cloud == null);
                    
                    if (generateFullAtmosphereHeight)
                    {
                        // При полной генерации проверяем распределение облаков по всей высоте атмосферы
                        // При взлете всегда догенерируем облака, если их меньше нормы
                        int totalClouds = cloudChunks[chunkCoord].Count;
                        
                        // Если облаков меньше нормы, генерируем новые во всем диапазоне высот атмосферы
                        if (totalClouds < cloudsPerChunk)
                        {
                            int cloudsToGenerate = cloudsPerChunk - totalClouds;
                            // При изменении высоты генерируем больше облаков
                            if (heightChangedSignificantly)
                            {
                                cloudsToGenerate = Mathf.Max(cloudsToGenerate, cloudsPerChunk / 2);
                            }
                            GenerateCloudsInChunk(chunkCoord, cloudsToGenerate);
                        }
                        else if (heightChangedSignificantly)
                        {
                            // При изменении высоты все равно добавляем немного облаков на новых уровнях
                            GenerateCloudsInChunk(chunkCoord, Mathf.Max(1, cloudsPerChunk / 3));
                        }
                    }
                    else
                    {
                        // Проверяем, есть ли облака выше текущей высоты корабля
                        int cloudsAboveShip = 0;
                        foreach (var cloud in cloudChunks[chunkCoord])
                        {
                            if (cloud != null && cloud.transform.position.y > currentShipHeight)
                            {
                                cloudsAboveShip++;
                            }
                        }
                        
                        // Если облаков выше корабля мало или нет - генерируем новые
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
        // Проверяем высоту корабля - если он выше атмосферы и полная генерация не включена, не генерируем облака
        if (useAtmosphereLimit && trackingTarget != null && !generateFullAtmosphereHeight)
        {
            float shipHeight = trackingTarget.position.y;
            float maxAtmosphereY = GetAtmosphereHeight();
            
            if (shipHeight > maxAtmosphereY)
            {
                // Корабль выше атмосферы и полная генерация не включена - не генерируем облака
                return;
            }
        }
        
        if (cloudChunks.ContainsKey(chunkCoord))
        {
            // Чанк уже существует, проверяем количество облаков
            if (cloudChunks[chunkCoord].Count >= cloudsPerChunk)
            {
                return;
            }
        }
        else
        {
            cloudChunks[chunkCoord] = new List<VolumetricCloud>();
        }
        
        // Увеличиваем счетчик генерации для уникальности
        generationCounter++;
        
        int cloudsToGenerate = cloudsPerChunk - cloudChunks[chunkCoord].Count;
        int generatedCount = 0;
        int attempts = 0;
        int maxAttempts = cloudsToGenerate * 5; // Увеличиваем попытки
        
        // Получаем текущее количество облаков для уникальности индексов
        int existingCloudCount = cloudChunks[chunkCoord].Count;
        
        // Получаем текущую высоту корабля для генерации облаков выше
        float currentShipHeight = trackingTarget != null ? trackingTarget.position.y : 0f;
        
        // Пытаемся сгенерировать облака, но пропускаем те, что выше атмосферы
        while (generatedCount < cloudsToGenerate && attempts < maxAttempts)
        {
            attempts++;
            
            // Используем уникальный индекс: existingCloudCount + attempts + generationCounter
            // Это гарантирует, что каждое облако получит уникальную позицию
            int uniqueIndex = existingCloudCount + attempts + generationCounter * 1000;
            Vector3 position = GetRandomPositionInChunk(chunkCoord, uniqueIndex);
            
            // Дополнительная проверка высоты атмосферы
            if (useAtmosphereLimit)
            {
                float maxAtmosphereY = GetAtmosphereHeight();
                float groundHeight = HillGenerator.GetHeightAtPosition(new Vector3(position.x, 0, position.z));
                float minAtmosphereY = groundHeight + minHeightAboveGround;
                
                if (generateFullAtmosphereHeight)
                {
                    // При полной генерации просто ограничиваем границами атмосферы
                    // Позиция уже должна быть в диапазоне от GetRandomPositionInChunk,
                    // но добавляем проверку на всякий случай
                    if (position.y > maxAtmosphereY)
                    {
                        position.y = maxAtmosphereY - 10f; // Отступ от верхней границы
                    }
                    if (position.y < minAtmosphereY)
                    {
                        position.y = minAtmosphereY;
                    }
                }
                else
                {
                    // Проверяем, что облако выше корабля (если generateAroundShipHeight включен)
                    if (generateAroundShipHeight && trackingTarget != null)
                    {
                        if (position.y <= currentShipHeight)
                        {
                            // Если облако ниже или на уровне корабля, поднимаем его выше
                            position.y = currentShipHeight + Random.Range(50f, maxCloudHeightAboveShip * 0.7f);
                        }
                        
                        float minYAboveShip = currentShipHeight + 50f;
                        minAtmosphereY = Mathf.Max(minAtmosphereY, minYAboveShip);
                    }
                    
                    // Пропускаем, если позиция выше атмосферы
                    if (position.y > maxAtmosphereY)
                    {
                        // Если выше атмосферы, генерируем выше корабля, но ниже атмосферы
                        if (generateAroundShipHeight && trackingTarget != null && currentShipHeight < maxAtmosphereY)
                        {
                            position.y = currentShipHeight + Random.Range(50f, Mathf.Min(maxCloudHeightAboveShip * 0.7f, maxAtmosphereY - currentShipHeight - 10f));
                        }
                        else
                        {
                            continue; // Пропускаем, если корабль тоже выше атмосферы
                        }
                    }
                    
                    // Проверяем минимальную высоту (но не ниже корабля)
                    if (position.y < minAtmosphereY)
                    {
                        // Генерируем на минимальной высоте, но выше корабля
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
                // Небольшая вариация скорости ветра для каждого облака
                float cloudWindSpeed = windSpeed * Random.Range(0.8f, 1.2f);
                cloud.SetWind(windDirection, cloudWindSpeed);
                cloudChunks[chunkCoord].Add(cloud);
                generatedCount++;
            }
        }
    }
    
    private Vector3 GetRandomPositionInChunk(Vector2Int chunkCoord, int cloudIndex)
    {
        // Используем разные сиды для X и Z, чтобы предотвратить столбцы из облаков
        // Это гарантирует уникальность позиций каждого облака
        int baseSeed = GetSeedForChunk(chunkCoord);
        int uniqueSeedX = baseSeed + cloudIndex * 73856093 + generationCounter + (int)(Time.time * 100) % 10000;
        int uniqueSeedZ = baseSeed + cloudIndex * 19349663 + generationCounter * 2 + (int)(Time.time * 200) % 10000;
        
        Random.InitState(uniqueSeedX);
        float localX = Random.Range(0f, chunkSize);
        
        Random.InitState(uniqueSeedZ);
        float localZ = Random.Range(0f, chunkSize);
        
        float worldX = chunkCoord.x * chunkSize + localX;
        float worldZ = chunkCoord.y * chunkSize + localZ;
        
        // Высота земли в этой точке
        float groundHeight = HillGenerator.GetHeightAtPosition(new Vector3(worldX, 0f, worldZ));
        
        float y;
        
        if (generateFullAtmosphereHeight && useAtmosphereLimit)
        {
            // Генерируем облака во всем диапазоне высот атмосферы (от минимальной высоты над землей до конца атмосферы)
            // Равномерное распределение по всей высоте атмосферы, независимо от высоты корабля
            float maxAtmosphereY = GetAtmosphereHeight();
            float minCloudY = groundHeight + minHeightAboveGround;
            float maxCloudY = maxAtmosphereY - 10f; // Отступ от верхней границы атмосферы
            
            // Генерируем облака во всем диапазоне высот атмосферы равномерно
            // Используем уникальный сид для высоты, чтобы каждое облако получало уникальную высоту
            Random.InitState(baseSeed + cloudIndex * 17 + generationCounter * 1000 + (int)(Time.time * 50) % 1000);
            y = Random.Range(minCloudY, maxCloudY);
            
            // При полной генерации атмосферы НЕ учитываем высоту корабля - генерируем равномерно везде
            // Это гарантирует, что при взлете будут видны облака на всех уровнях
        }
        else if (generateAroundShipHeight && trackingTarget != null)
        {
            // Генерируем облака СТРОГО ВЫШЕ корабля (не ниже, не на той же высоте)
            float shipHeight = trackingTarget.position.y;
            
            // Минимальный отступ вверх от корабля (облако должно быть выше корабля)
            float minOffsetAboveShip = 50f;
            
            // Определяем, откуда дует ветер (по Y)
            float windY = windDirection.normalized.y;
            
            // Базовое смещение ВВЕРХ от корабля
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
            // Классический способ: фиксированная высота над землей
            y = groundHeight + Random.Range(minHeightAboveGround, maxHeightAboveGround);
        }
        
        // Ограничение по высоте атмосферы (но не ниже корабля!)
        if (useAtmosphereLimit)
        {
            float maxAtmosphereY = GetAtmosphereHeight();
            if (y > maxAtmosphereY)
            {
                // Если выше атмосферы, ограничиваем высоту
                // Но если корабль выше атмосферы, генерируем на высоте корабля + отступ
                float shipHeight = trackingTarget != null ? trackingTarget.position.y : 0f;
                if (shipHeight > maxAtmosphereY)
                {
                    // Корабль выше атмосферы - генерируем выше корабля, но не выше максимальной высоты
                    y = Mathf.Min(shipHeight + 50f, maxAtmosphereY - 10f);
                }
                else
                {
                    float atmosphereMargin = 10f; // Отступ от верхней границы атмосферы
                    y = maxAtmosphereY - atmosphereMargin;
                }
            }
        }
        
        // Финальная проверка: облако должно быть выше земли и ниже атмосферы
        float finalMinYAboveGround = groundHeight + minHeightAboveGround;
        
        if (generateFullAtmosphereHeight)
        {
            // При полной генерации атмосферы просто проверяем границы
            y = Mathf.Max(y, finalMinYAboveGround);
            
            if (useAtmosphereLimit)
            {
                float maxAtmosphereY = GetAtmosphereHeight();
                y = Mathf.Min(y, maxAtmosphereY - 10f); // Отступ от верхней границы
            }
        }
        else if (trackingTarget != null && generateAroundShipHeight)
        {
            float shipHeight = trackingTarget.position.y;
            // Гарантируем, что облако выше корабля (если не полная генерация)
            float minYAboveShip = shipHeight + 50f;
            float maxYAboveShip = shipHeight + maxCloudHeightAboveShip;
            
            y = Mathf.Max(y, minYAboveShip, finalMinYAboveGround);
            y = Mathf.Min(y, maxYAboveShip);
            
            // Ограничение по высоте атмосферы
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
        // Пытаемся получить высоту атмосферы из ShipController, если синхронизация включена
        if (syncWithShipAtmosphere)
        {
            ShipController shipController = FindObjectOfType<ShipController>();
            if (shipController != null)
            {
                // Используем рефлексию для получения атмосферной высоты
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
            
            // Удаляем облака, которые выше атмосферы
            if (cloud.transform.position.y > maxAtmosphereY)
            {
                cloudsToRemove.Add(cloud);
            }
        }
        
        foreach (VolumetricCloud cloud in cloudsToRemove)
        {
            if (cloud != null)
            {
                // Удаляем из чанка
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
                // Удаляем из чанка
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
                // Удаляем null из списка
                allClouds.Remove(cloud);
            }
        }
    }
    
    private void CleanupDestroyedClouds()
    {
        // Удаляем null-ссылки из allClouds (облака, которые были уничтожены)
        allClouds.RemoveAll(cloud => cloud == null);
        
        // Очищаем null-ссылки из чанков
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
        
        // Определяем приоритетные чанки - с той стороны, ОТКУДА ДУЕТ ВЕТЕР
        HashSet<Vector2Int> priorityChunks = new HashSet<Vector2Int>();
        
        // Добавляем чанки с той стороны, откуда дует ветер (противоположно направлению ветра)
        Vector3 windSourcePosition = trackingTarget.position - windDirNormalized * spawnDistanceFromTarget * 0.8f;
        Vector2Int windSourceChunk = GetChunkCoordinate(windSourcePosition);
        priorityChunks.Add(windSourceChunk);
        
        // Также добавляем соседние чанки в направлении источника ветра
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
                // Для вертикального ветра учитываем горизонтальный разброс
                offsetPos.x += offset * chunkSize * 0.5f;
                offsetPos.z += offset * chunkSize * 0.5f;
            }
            priorityChunks.Add(GetChunkCoordinate(offsetPos));
        }
        
        // Сначала обрабатываем приоритетные чанки (откуда дует ветер)
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
                // Приоритетный чанк не существует - создаем его
                GenerateChunk(chunkCoord);
            }
        }
        
        // Затем проверяем остальные чанки в радиусе видимости (но с меньшим приоритетом)
        for (int x = -loadRadius; x <= loadRadius; x++)
        {
            for (int z = -loadRadius; z <= loadRadius; z++)
            {
                Vector2Int chunkCoord = new Vector2Int(currentChunk.x + x, currentChunk.y + z);
                
                // Пропускаем уже обработанные приоритетные чанки
                if (priorityChunks.Contains(chunkCoord))
                    continue;
                
                if (cloudChunks.ContainsKey(chunkCoord))
                {
                    cloudChunks[chunkCoord].RemoveAll(cloud => cloud == null);
                    int currentCloudCount = cloudChunks[chunkCoord].Count;
                    
                    // Для неприоритетных чанков генерируем только если совсем пусто
                    if (currentCloudCount == 0)
                    {
                        int generateAmount = Mathf.Min(cloudsPerChunk / 2, cloudsPerChunk);
                        GenerateCloudsInChunk(chunkCoord, generateAmount);
                    }
                    else if (currentCloudCount < cloudsPerChunk / 2)
                    {
                        // Генерируем только если облаков очень мало
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
        
        // Проверяем высоту корабля - если он выше атмосферы, не генерируем облака
        if (useAtmosphereLimit && trackingTarget != null)
        {
            float shipHeight = trackingTarget.position.y;
            float maxAtmosphereY = GetAtmosphereHeight();
            
            if (shipHeight > maxAtmosphereY)
            {
                return;
            }
        }
        
        // Увеличиваем счетчик генерации для уникальности
        generationCounter++;
        
        int generatedCount = 0;
        int attempts = 0;
        int maxAttempts = count * 5; // Увеличиваем количество попыток
        
        // Получаем текущее количество облаков для уникальности индексов
        int existingCloudCount = cloudChunks.ContainsKey(chunkCoord) ? cloudChunks[chunkCoord].Count : 0;
        
        if (!cloudChunks.ContainsKey(chunkCoord))
        {
            cloudChunks[chunkCoord] = new List<VolumetricCloud>();
        }
        
        // Получаем текущую высоту корабля для генерации облаков выше
        float currentShipHeight = trackingTarget != null ? trackingTarget.position.y : 0f;
        
        while (generatedCount < count && attempts < maxAttempts)
        {
            attempts++;
            
            // Используем уникальный индекс: existingCloudCount + attempts + generationCounter + время
            // Это гарантирует уникальность каждой позиции и предотвращает столбцы
            int uniqueIndex = existingCloudCount + attempts + generationCounter * 1000 + (int)(Time.time * 100) % 1000;
            Vector3 position = GetRandomPositionInChunk(chunkCoord, uniqueIndex);
            
            // Проверяем, что облако выше корабля (если generateAroundShipHeight включен)
            if (generateAroundShipHeight && trackingTarget != null)
            {
                if (position.y <= currentShipHeight)
                {
                    // Если облако ниже или на уровне корабля, поднимаем его выше
                    position.y = currentShipHeight + Random.Range(50f, maxCloudHeightAboveShip * 0.7f);
                }
            }
            
            // Дополнительная проверка высоты атмосферы
            if (useAtmosphereLimit)
            {
                float maxAtmosphereY = GetAtmosphereHeight();
                float groundHeight = HillGenerator.GetHeightAtPosition(new Vector3(position.x, 0, position.z));
                float minAtmosphereY = groundHeight + minHeightAboveGround;
                
                // Проверяем, что облако выше корабля (если generateAroundShipHeight включен)
                if (generateAroundShipHeight && trackingTarget != null)
                {
                    float minYAboveShip = currentShipHeight + 50f;
                    minAtmosphereY = Mathf.Max(minAtmosphereY, minYAboveShip);
                }
                
                // Пропускаем, если позиция выше атмосферы
                if (position.y > maxAtmosphereY)
                {
                    // Если выше атмосферы, генерируем выше корабля, но ниже атмосферы
                    if (generateAroundShipHeight && trackingTarget != null && currentShipHeight < maxAtmosphereY)
                    {
                        position.y = currentShipHeight + Random.Range(50f, Mathf.Min(maxCloudHeightAboveShip * 0.7f, maxAtmosphereY - currentShipHeight - 10f));
                    }
                    else
                    {
                        continue; // Пропускаем, если корабль тоже выше атмосферы
                    }
                }
                
                // Проверяем минимальную высоту (но не ниже корабля)
                if (position.y < minAtmosphereY)
                {
                    // Генерируем на минимальной высоте, но выше корабля
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
        
        // Отладочная информация (можно удалить позже)
        if (generatedCount < count && attempts >= maxAttempts)
        {
            Debug.LogWarning($"⚠️ Не удалось сгенерировать все облака в чанке {chunkCoord}: {generatedCount}/{count} (попыток: {attempts})");
        }
    }
    
    private void GenerateCloudsAhead()
    {
        if (trackingTarget == null || windSpeed < 0.1f) return;
        
        // Проверяем, не слишком ли высоко корабль (выше атмосферы)
        float shipHeight = trackingTarget.position.y;
        float maxAtmosphereY = GetAtmosphereHeight();
        
        if (useAtmosphereLimit && shipHeight > maxAtmosphereY)
        {
            // Корабль выше атмосферы - не генерируем облака
            return;
        }
        
        Vector3 windDirNormalized = windDirection.normalized;
        
        // Генерируем облака с той стороны, ОТКУДА дует ветер (противоположно направлению ветра)
        // Облака должны появляться "навстречу" ветру и двигаться по направлению ветра
        Vector3 sourcePosition = trackingTarget.position - windDirNormalized * spawnDistanceFromTarget;
        Vector2Int sourceChunk = GetChunkCoordinate(sourcePosition);
        
        // Также генерируем впереди по направлению ветра
        Vector3 aheadPosition = trackingTarget.position + windDirNormalized * spawnDistanceFromTarget;
        Vector2Int aheadChunk = GetChunkCoordinate(aheadPosition);
        
        // Генерируем облака с источника ветра (откуда дует)
        if (!cloudChunks.ContainsKey(sourceChunk) || cloudChunks[sourceChunk].Count < cloudsPerChunk)
        {
            // Догенерируем часть облаков, если их мало
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
        
        // Также генерируем впереди по направлению ветра
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
        
        // Визуализация загруженных чанков
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
        
        // Визуализация области удаления
        Gizmos.color = new Color(1, 0, 0, 0.2f);
        Gizmos.DrawWireSphere(trackingTarget.position, maxDistanceFromTarget);
        
        // Визуализация области генерации впереди
        if (windSpeed > 0.1f)
        {
            Vector3 aheadPos = trackingTarget.position + windDirection.normalized * spawnDistanceFromTarget;
            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.DrawWireSphere(aheadPos, chunkSize * 0.5f);
        }
    }
}
