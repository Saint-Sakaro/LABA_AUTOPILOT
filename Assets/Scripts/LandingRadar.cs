using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Радар для поиска посадочных площадок под кораблем
/// </summary>
public class LandingRadar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform shipTransform; // Трансформ корабля (если не назначен, ищется автоматически)
    
    [Header("Basic Settings")]
    [SerializeField] private float scanRadius = 200f; // Радиус сканирования под кораблем (метры)
    [SerializeField] private float gridResolution = 5f; // Разрешение сетки сканирования (метры)
    [SerializeField] private float scanUpdateInterval = 2f; // Интервал обновления сканирования (секунды)

    [Header("Surface Sampling")]
    [SerializeField] private LayerMask surfaceMask = ~0; // Слои для определения поверхности посадки
    [SerializeField] private float surfaceRaycastHeight = 500f; // Высота старта луча для поиска поверхности
    [SerializeField] private bool useSurfaceMask = true; // Если false, берем самый дальний хит без маски (земля ниже деревьев)
    [SerializeField] private bool showSurfaceDebug = false;
    [SerializeField] private bool usePoissonSampling = true;
    [SerializeField] private bool useObstacleGridSampling = true;
    
    [Header("3D Visualization")]
    [SerializeField] private bool use3DIndicators = true; // Использовать 3D индикаторы
    [SerializeField] private GameObject indicatorPrefab; // Префаб индикатора (опционально)
    [SerializeField] private Transform indicatorsContainer; // Контейнер для индикаторов
    
    [Header("Indicator Size")]
    [SerializeField] private float minIndicatorSize = 20f;
    [SerializeField] private float maxIndicatorSize = 100f;
    [SerializeField] private float indicatorSizeMultiplier = 1.2f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool visualizeAllPoints = true; // Показывать все проверяемые точки (и подходящие, и не подходящие)
    [SerializeField] private Color validPointColor = Color.green; // Цвет для подходящих точек
    [SerializeField] private Color invalidPointColor = Color.red; // Цвет для неподходящих точек
    [SerializeField] private float pointVisualSize = 2f; // Размер визуализации точек
    
    [Header("Obstacle Detection")]
    [SerializeField] private LayerMask obstacleMask = ~0; // Слои препятствий (деревья, камни, здания)
    [SerializeField] private bool earlyRejectObstacles = true;
    [SerializeField] private float obstacleClearanceRadius = 20f; // Минимальный отступ от препятствий для точки
    [SerializeField] private bool showObstacleDebug = false;
    [SerializeField] private bool excludeGroundByName = true;
    [SerializeField] private string[] excludedObstacleNameKeywords = { "ground", "terrain", "platform" };
    [SerializeField] private float maxObstacleFootprint = 200f; // Игнорировать слишком большие коллайдеры
    
    [Header("Performance")]
    [SerializeField] private bool groupSitesDuringScan = true;
    [SerializeField] private int groupSitesEvery = 40;
    [SerializeField] private int maxSitesDuringScan = 200;
    
    // Параметры из кода (не настраиваются в Inspector)
    private const float MIN_LANDING_SITE_SIZE = 60f; // Минимальный размер площадки (метры)
    private const float MAX_SLOPE_ANGLE = 15f; // Максимальный допустимый наклон (градусы)
    private const float MAX_FLATNESS_DEVIATION = 8f; // Максимальное отклонение высот (метры)
    private const float MIN_OBSTACLE_DISTANCE = 20f; // Минимальное расстояние до препятствий (метры)
    private const float FLATNESS_CHECK_RADIUS = 15f; // Радиус проверки ровности (метры)
    private const int FLATNESS_CHECK_POINTS = 8; // Количество точек для проверки ровности
    private const float OBSTACLE_CHECK_HEIGHT = 200f; // Высота проверки препятствий (метры)
    private const int MAX_RESULTS = 100; // Максимальное количество площадок (увеличено для отладки)
    private const float MIN_DISTANCE_BETWEEN_SITES = 50f; // Минимальное расстояние между площадками (метры) - увеличено для предотвращения наложения
    private const int MAX_POINTS_PER_FRAME = 30; // Максимальное количество точек за кадр (уменьшено для предотвращения зависаний)
    private const int OBSTACLE_OVERLAP_BUFFER_SIZE = 256;
    private const int OBSTACLE_BROADPHASE_BUFFER_SIZE = 2048;
    
    // Внутренние переменные
    private List<LandingSite> foundSites = new List<LandingSite>();
    private Collider[] obstacleOverlapBuffer = new Collider[OBSTACLE_OVERLAP_BUFFER_SIZE];
    private Collider[] obstacleBroadphaseBuffer = new Collider[OBSTACLE_BROADPHASE_BUFFER_SIZE];
    private System.Random poissonRng = new System.Random();
    
    /// <summary>
    /// Получает список найденных площадок
    /// </summary>
    public List<LandingSite> GetFoundSites()
    {
        return foundSites;
    }
    private List<LandingSiteIndicator> siteIndicators = new List<LandingSiteIndicator>();
    private float lastScanTime = 0f;
    
    // Для распределенного сканирования
    private List<Vector2> scanPoints = new List<Vector2>();
    private int currentScanIndex = 0;
    private bool isScanning = false;
    private Vector3 lastShipPosition; // Последняя позиция корабля при начале сканирования
    
    // Для контроля визуализации
    private bool hasVisualizedSites = false; // Были ли визуализированы точки
    private Vector3 firstVisualizationPosition; // Позиция корабля при первой визуализации
    private const float VISUALIZATION_UPDATE_DISTANCE = 150f; // Расстояние смещения для обновления визуализации (м)
    
    // События
    public delegate void SitesUpdatedDelegate(List<LandingSite> sites);
    public event SitesUpdatedDelegate OnSitesUpdated;
    
    private void Start()
    {
        // Находим корабль
        if (shipTransform == null)
        {
            ShipController shipController = FindObjectOfType<ShipController>();
            if (shipController != null)
            {
                shipTransform = shipController.transform;
                if (showDebugInfo)
                {
                    Debug.Log($"LandingRadar: Автоматически найден корабль: {shipTransform.name}");
                }
            }
            else
            {
                shipTransform = transform;
                Debug.LogWarning("LandingRadar: ShipController не найден в сцене! Радар будет использовать свой transform. Убедитесь, что в сцене есть объект с компонентом ShipController.");
            }
        }
        else
        {
            if (showDebugInfo)
            {
                Debug.Log($"LandingRadar: Используется назначенный корабль: {shipTransform.name}");
            }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"LandingRadar: obstacleMask={obstacleMask.value}, earlyReject={earlyRejectObstacles}, useObstacleGrid={useObstacleGridSampling}, usePoisson={usePoissonSampling}");
        }
    }
    
    private bool TryGetObstaclesNonAlloc(Vector3 start, Vector3 end, float radius, out int count)
    {
        count = Physics.OverlapCapsuleNonAlloc(
            start,
            end,
            radius,
            obstacleOverlapBuffer,
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );
        if (showDebugInfo && Time.frameCount % 120 == 0)
        {
            Debug.Log($"LandingRadar: OverlapCapsuleNonAlloc count={count}, radius={radius:F1}");
        }
        return count > 0;
    }
    
    private bool IsValidObstacle(Collider col, float groundHeight)
    {
        if (col == null) return false;
        // Исключаем корабль
        if (shipTransform != null && (col.transform == shipTransform || col.transform.IsChildOf(shipTransform)))
        {
            return false;
        }
        if (excludeGroundByName)
        {
            string lowerName = col.name.ToLowerInvariant();
            for (int i = 0; i < excludedObstacleNameKeywords.Length; i++)
            {
                if (lowerName.Contains(excludedObstacleNameKeywords[i]))
                {
                    if (showObstacleDebug)
                    {
                        Debug.Log($"LandingRadar: Obstacle '{col.name}' ignored (name filter).");
                    }
                    return false;
                }
            }
        }
        if (maxObstacleFootprint > 0f)
        {
            Vector3 size = col.bounds.size;
            if (size.x > maxObstacleFootprint && size.z > maxObstacleFootprint)
            {
                if (showObstacleDebug)
                {
                    Debug.Log($"LandingRadar: Obstacle '{col.name}' ignored (too large).");
                }
                return false;
            }
        }
        // Препятствие должно быть выше земли
        float obstacleHeight = col.bounds.max.y;
        float heightAboveGround = obstacleHeight - groundHeight;
        if (heightAboveGround <= 0.5f || heightAboveGround > OBSTACLE_CHECK_HEIGHT)
        {
            if (showObstacleDebug)
            {
                Debug.Log($"LandingRadar: Obstacle '{col.name}' ignored (height out of range, aboveGround={heightAboveGround:F1}).");
            }
            return false;
        }
        return true;
    }
    
    private void Update()
    {
        if (shipTransform == null) return;
        
        // Если точки уже визуализированы, проверяем смещение корабля
        if (hasVisualizedSites)
        {
            Vector3 currentPosition = shipTransform.position;
            float deltaX = Mathf.Abs(currentPosition.x - firstVisualizationPosition.x);
            float deltaZ = Mathf.Abs(currentPosition.z - firstVisualizationPosition.z);
            
            // Если сместились на 50м по X или Z, разрешаем новое сканирование
            if (deltaX >= VISUALIZATION_UPDATE_DISTANCE || deltaZ >= VISUALIZATION_UPDATE_DISTANCE)
            {
                if (showDebugInfo)
                {
                    Debug.Log($"LandingRadar: Корабль сместился на {Mathf.Max(deltaX, deltaZ):F1}м от позиции визуализации. Разрешаю новое сканирование.");
                }
                hasVisualizedSites = false; // Разрешаем новое сканирование и визуализацию
                // Останавливаем текущее сканирование, если оно активно
                if (isScanning)
                {
                    isScanning = false;
                    if (showDebugInfo)
                    {
                        Debug.Log("LandingRadar: Остановлено текущее сканирование для начала нового.");
                    }
                }
            }
            else
            {
                // Если не сместились достаточно, НЕ начинаем новое сканирование
                return; // Выходим, не проверяя сканирование
            }
        }
        
        // Проверяем, нужно ли начать новое сканирование
        // Если еще не было сканирования, запускаем сразу (не ждем интервал)
        if (!isScanning)
        {
            if (lastScanTime == 0f || Time.time - lastScanTime >= scanUpdateInterval)
            {
                StartNewScan();
            }
        }
        
        // Продолжаем сканирование, если оно активно
        if (isScanning)
        {
            ContinueScanning();
        }
    }

    public float GetSurfaceHeightAtPosition(Vector3 worldPos)
    {
        float startY = Mathf.Max(worldPos.y + surfaceRaycastHeight, surfaceRaycastHeight);
        Vector3 start = new Vector3(worldPos.x, startY, worldPos.z);
        float maxDistance = surfaceRaycastHeight * 2f;
        int mask = useSurfaceMask ? surfaceMask : ~0;
        RaycastHit[] hits = Physics.RaycastAll(start, Vector3.down, maxDistance, mask, QueryTriggerInteraction.Ignore);
        if (hits != null && hits.Length > 0)
        {
            float bestDistance = -1f;
            float bestY = worldPos.y;
            Collider bestCollider = null;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null) continue;
                if (hit.distance > bestDistance)
                {
                    bestDistance = hit.distance;
                    bestY = hit.point.y;
                    bestCollider = hit.collider;
                }
            }
            
            if (bestDistance >= 0f)
            {
                if (showSurfaceDebug && Time.frameCount % 60 == 0)
                {
                    string colName = bestCollider != null ? bestCollider.name : "null";
                    int colLayer = bestCollider != null ? bestCollider.gameObject.layer : -1;
                    Debug.Log($"LandingRadar: SurfaceHit y={bestY:F2}, dist={bestDistance:F1}, collider={colName}, layer={colLayer}, mask={(useSurfaceMask ? surfaceMask.value : -1)}");
                }
                return bestY;
            }
        }
        else if (showSurfaceDebug && Time.frameCount % 60 == 0)
        {
            Debug.LogWarning($"LandingRadar: SurfaceRaycast miss at ({worldPos.x:F1}, {worldPos.z:F1}), fallback to HillGenerator.");
        }
        
        return HillGenerator.GetHeightAtPosition(worldPos);
    }
    
    /// <summary>
    /// Начинает новое сканирование (с приоритетом точек под кораблем)
    /// </summary>
    private void StartNewScan()
    {
        if (shipTransform == null) return;
        
        // Получаем актуальные координаты корабля
        lastShipPosition = shipTransform.position;
        Vector3 shipPositionGround = new Vector3(lastShipPosition.x, 0f, lastShipPosition.z);
        
        isScanning = true;
        currentScanIndex = 0;
        scanPoints.Clear();
        foundSites.Clear();
        
        List<Vector2> points;
        if (useObstacleGridSampling)
        {
            points = GenerateObstacleGridPoints(shipPositionGround, scanRadius, Mathf.Max(1f, gridResolution));
        }
        else if (usePoissonSampling)
        {
            points = GeneratePoissonPoints(shipPositionGround, scanRadius, Mathf.Max(1f, gridResolution), 30);
        }
        else
        {
            points = GenerateGridPoints(shipPositionGround, scanRadius, gridResolution);
        }
        
        // Сортируем по расстоянию - сначала ближайшие точки (прямо под кораблем)
        scanPoints = points
            .OrderBy(p => Vector2.Distance(p, new Vector2(shipPositionGround.x, shipPositionGround.z)))
            .ToList();
        
        if (showDebugInfo)
        {
            string mode = useObstacleGridSampling ? "ObstacleGrid" : (usePoissonSampling ? "Poisson" : "Grid");
            Debug.Log($"LandingRadar: Начато сканирование {scanPoints.Count} точек ({mode}) под кораблем (радиус: {scanRadius}м, шаг: {gridResolution}м). Приоритет: ближайшие точки");
        }
    }
    
    private List<Vector2> GenerateObstacleGridPoints(Vector3 center, float radius, float step)
    {
        List<Vector2> points = new List<Vector2>();
        if (step <= 0f) return points;
        
        float clearance = Mathf.Max(0.1f, obstacleClearanceRadius);
        float searchRadius = radius + clearance;
        Vector3 broadphaseCenter = new Vector3(center.x, center.y, center.z);
        int obstacleCount = Physics.OverlapSphereNonAlloc(
            broadphaseCenter,
            searchRadius,
            obstacleBroadphaseBuffer,
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );
        
        if (showDebugInfo)
        {
            Debug.Log($"LandingRadar: ObstacleGrid broadphase count={obstacleCount}, searchRadius={searchRadius:F1}, clearance={clearance:F1}");
        }
        if (showObstacleDebug)
        {
            if (obstacleCount == 0)
            {
                Debug.Log("LandingRadar: ObstacleGrid broadphase found 0 obstacles.");
            }
            else
            {
                int logCount = Mathf.Min(obstacleCount, 5);
                for (int i = 0; i < logCount; i++)
                {
                    Collider col = obstacleBroadphaseBuffer[i];
                    if (col == null) continue;
                    Debug.Log($"LandingRadar: Broadphase obstacle '{col.name}' layer={LayerMask.LayerToName(col.gameObject.layer)} bounds={col.bounds}");
                }
            }
        }
        
        int gridSize = Mathf.CeilToInt((radius * 2f) / step);
        int centerX = gridSize / 2;
        int centerZ = gridSize / 2;
        bool[] blocked = new bool[gridSize * gridSize];
        
        float minX = center.x - radius;
        float minZ = center.z - radius;
        
        for (int i = 0; i < obstacleCount; i++)
        {
            Collider col = obstacleBroadphaseBuffer[i];
            if (col == null) continue;
            float groundHeight = HillGenerator.GetHeightAtPosition(new Vector3(col.bounds.center.x, 0f, col.bounds.center.z));
            if (!IsValidObstacle(col, groundHeight)) continue;
            
            Bounds b = col.bounds;
            float inflate = Mathf.Min(clearance, step * 1.5f);
            float minBx = b.min.x - inflate;
            float maxBx = b.max.x + inflate;
            float minBz = b.min.z - inflate;
            float maxBz = b.max.z + inflate;
            
            int startX = Mathf.Clamp(Mathf.FloorToInt((minBx - minX) / step), 0, gridSize - 1);
            int endX = Mathf.Clamp(Mathf.FloorToInt((maxBx - minX) / step), 0, gridSize - 1);
            int startZ = Mathf.Clamp(Mathf.FloorToInt((minBz - minZ) / step), 0, gridSize - 1);
            int endZ = Mathf.Clamp(Mathf.FloorToInt((maxBz - minZ) / step), 0, gridSize - 1);
            
            for (int x = startX; x <= endX; x++)
            {
                for (int z = startZ; z <= endZ; z++)
                {
                    blocked[x + z * gridSize] = true;
                }
            }
        }
        
        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                if (blocked[x + z * gridSize]) continue;
                
                float worldX = center.x + (x - centerX) * step;
                float worldZ = center.z + (z - centerZ) * step;
                
                float distanceFromCenter = Vector2.Distance(
                    new Vector2(worldX, worldZ),
                    new Vector2(center.x, center.z)
                );
                if (distanceFromCenter <= radius)
                {
                    points.Add(new Vector2(worldX, worldZ));
                }
            }
        }
        
        return points;
    }
    
    private List<Vector2> GenerateGridPoints(Vector3 center, float radius, float step)
    {
        List<Vector2> points = new List<Vector2>();
        int gridSize = Mathf.CeilToInt((radius * 2f) / step);
        int centerX = gridSize / 2;
        int centerZ = gridSize / 2;
        
        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                float worldX = center.x + (x - centerX) * step;
                float worldZ = center.z + (z - centerZ) * step;
                
                float distanceFromCenter = Vector2.Distance(
                    new Vector2(worldX, worldZ),
                    new Vector2(center.x, center.z)
                );
                
                if (distanceFromCenter <= radius)
                {
                    points.Add(new Vector2(worldX, worldZ));
                }
            }
        }
        
        return points;
    }
    
    private List<Vector2> GeneratePoissonPoints(Vector3 center, float radius, float minDistance, int attemptsPerPoint)
    {
        List<Vector2> points = new List<Vector2>();
        if (minDistance <= 0f) return points;
        
        Vector2 center2D = new Vector2(center.x, center.z);
        Vector2 initial = center2D;
        points.Add(initial);
        List<Vector2> active = new List<Vector2> { initial };
        
        float cellSize = minDistance / Mathf.Sqrt(2f);
        int gridSize = Mathf.CeilToInt((radius * 2f) / cellSize);
        int gridWidth = gridSize;
        int gridHeight = gridSize;
        Vector2?[] grid = new Vector2?[gridWidth * gridHeight];
        
        int cx = Mathf.FloorToInt((initial.x - (center2D.x - radius)) / cellSize);
        int cz = Mathf.FloorToInt((initial.y - (center2D.y - radius)) / cellSize);
        grid[cx + cz * gridWidth] = initial;
        
        while (active.Count > 0)
        {
            int index = poissonRng.Next(active.Count);
            Vector2 point = active[index];
            bool found = false;
            
            for (int i = 0; i < attemptsPerPoint; i++)
            {
                float angle = (float)poissonRng.NextDouble() * Mathf.PI * 2f;
                float distance = minDistance * (1f + (float)poissonRng.NextDouble());
                Vector2 candidate = point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                
                if (Vector2.Distance(candidate, center2D) > radius)
                {
                    continue;
                }
                
                int gx = Mathf.FloorToInt((candidate.x - (center2D.x - radius)) / cellSize);
                int gz = Mathf.FloorToInt((candidate.y - (center2D.y - radius)) / cellSize);
                
                if (gx < 0 || gz < 0 || gx >= gridWidth || gz >= gridHeight)
                {
                    continue;
                }
                
                bool ok = true;
                int minX = Mathf.Max(gx - 2, 0);
                int maxX = Mathf.Min(gx + 2, gridWidth - 1);
                int minZ = Mathf.Max(gz - 2, 0);
                int maxZ = Mathf.Min(gz + 2, gridHeight - 1);
                
                for (int x = minX; x <= maxX && ok; x++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        Vector2? neighbor = grid[x + z * gridWidth];
                        if (neighbor.HasValue && Vector2.Distance(candidate, neighbor.Value) < minDistance)
                        {
                            ok = false;
                            break;
                        }
                    }
                }
                
                if (!ok) continue;
                
                points.Add(candidate);
                active.Add(candidate);
                grid[gx + gz * gridWidth] = candidate;
                found = true;
                break;
            }
            
            if (!found)
            {
                active.RemoveAt(index);
            }
        }
        
        return points;
    }
    
    /// <summary>
    /// Продолжает сканирование (распределенное по кадрам)
    /// </summary>
    private void ContinueScanning()
    {
        if (shipTransform == null || scanPoints.Count == 0)
        {
            FinishScanning();
            return;
        }
        
        int pointsProcessed = 0;
        
        while (currentScanIndex < scanPoints.Count && pointsProcessed < MAX_POINTS_PER_FRAME)
        {
            Vector2 point = scanPoints[currentScanIndex];
            float worldX = point.x;
            float worldZ = point.y;
            
            // Получаем высоту
            float groundHeight = HillGenerator.GetHeightAtPosition(new Vector3(worldX, 0f, worldZ));
            
            // Используем актуальные координаты корабля для вычисления расстояния
            Vector3 shipPosition = shipTransform.position;
            float distanceFromShip = Vector2.Distance(
                new Vector2(worldX, worldZ),
                new Vector2(shipPosition.x, shipPosition.z)
            );
            
            // Отладочная информация для точек под кораблем
            bool isUnderShip = distanceFromShip < 20f;
            string rejectionReason = "";
            
            // Оцениваем площадку в этой точке
            LandingSite site = EvaluateLandingSite(new Vector3(worldX, groundHeight, worldZ), distanceFromShip, out rejectionReason);
            
            // Визуализация для отладки - показываем ВСЕ точки
            if (visualizeAllPoints)
            {
                Vector3 pointPos = new Vector3(worldX, groundHeight + 0.5f, worldZ);
                Color pointColor;
                
                if (site != null)
                {
                    // Подходящая точка - зеленый
                    pointColor = validPointColor;
                }
                else
                {
                    // Неподходящая точка - красный
                    pointColor = invalidPointColor;
                }
                
                // Рисуем линию от земли вверх (увеличено время отображения)
                Debug.DrawLine(pointPos, pointPos + Vector3.up * pointVisualSize, pointColor, 1f);
                
                // Для точек под кораблем рисуем дополнительный маркер (крест)
                if (isUnderShip)
                {
                    float crossSize = 3f;
                    Debug.DrawLine(pointPos + Vector3.up * pointVisualSize, pointPos + Vector3.up * pointVisualSize + Vector3.forward * crossSize, Color.yellow, 1f);
                    Debug.DrawLine(pointPos + Vector3.up * pointVisualSize, pointPos + Vector3.up * pointVisualSize + Vector3.back * crossSize, Color.yellow, 1f);
                    Debug.DrawLine(pointPos + Vector3.up * pointVisualSize, pointPos + Vector3.up * pointVisualSize + Vector3.left * crossSize, Color.yellow, 1f);
                    Debug.DrawLine(pointPos + Vector3.up * pointVisualSize, pointPos + Vector3.up * pointVisualSize + Vector3.right * crossSize, Color.yellow, 1f);
                }
            }
            
            // Логирование для точек под кораблем и ровных участков
            // Проверяем, является ли точка ровной (для логирования)
            bool isFlatArea = false;
            if (site != null)
            {
                isFlatArea = site.flatness < 2f && site.slopeAngle < 5f;
            }
            
            if ((isUnderShip || isFlatArea) && showDebugInfo)
            {
                if (site != null)
                {
                    Debug.Log($"LandingRadar: ✓ Точка ({distanceFromShip:F1}м) - ПОДХОДИТ! Позиция: ({worldX:F1}, {groundHeight:F1}, {worldZ:F1}), " +
                             $"Ровность: {site.flatness:F2}м, Наклон: {site.slopeAngle:F1}°, Размер: {site.size:F1}м, Препятствия: {site.distanceToObstacle:F1}м, Оценка: {site.suitabilityScore * 100f:F1}%");
                }
                else
                {
                    Debug.LogWarning($"LandingRadar: ✗ Точка ({distanceFromShip:F1}м) - ОТБРОШЕНА! Позиция: ({worldX:F1}, {groundHeight:F1}, {worldZ:F1}), Причина: {rejectionReason}");
                }
            }
            
            // Добавляем площадку, если она прошла все проверки
            if (site != null)
            {
                foundSites.Add(site);
                
                if (groupSitesDuringScan && foundSites.Count >= groupSitesEvery && (foundSites.Count % groupSitesEvery == 0))
                {
                    foundSites = GroupNearbySites(foundSites);
                    if (foundSites.Count > maxSitesDuringScan)
                    {
                        foundSites = foundSites
                            .OrderByDescending(s => s.suitabilityScore)
                            .ThenBy(s => s.distanceFromShip)
                            .Take(maxSitesDuringScan)
                            .ToList();
                    }
                }
            }
            
            currentScanIndex++;
            pointsProcessed++;
        }
        
        // Если все точки обработаны, завершаем сканирование
        if (currentScanIndex >= scanPoints.Count)
        {
            FinishScanning();
        }
    }
    
    /// <summary>
    /// Завершает сканирование и обновляет результаты
    /// </summary>
    private void FinishScanning()
    {
        isScanning = false;
        lastScanTime = Time.time;
        
        // Обрабатываем результаты
        ProcessResults();
        
        if (showDebugInfo)
        {
            Debug.Log($"LandingRadar: Сканирование завершено! Обработано {currentScanIndex} точек, найдено {foundSites.Count} площадок");
        }
    }
    
    /// <summary>
    /// Обрабатывает результаты сканирования
    /// </summary>
    private void ProcessResults()
    {
        
        // Сортируем по пригодности
        foundSites = foundSites
            .OrderByDescending(s => s.suitabilityScore)
            .ThenBy(s => s.distanceFromShip)
            .ToList();
        
        // Группируем близкие площадки
        foundSites = GroupNearbySites(foundSites);
        
        // Ограничиваем количество результатов
        foundSites = foundSites.Take(MAX_RESULTS).ToList();
        
        // Обновляем 3D индикаторы
        Update3DIndicators();
        
        // Уведомляем о новых результатах
        OnSitesUpdated?.Invoke(foundSites);
        
        if (showDebugInfo)
        {
            if (foundSites.Count > 0)
            {
                Debug.Log($"LandingRadar: Найдено {foundSites.Count} посадочных площадок. Лучшая: {foundSites[0].GetDescription()}");
            }
            else
            {
                Debug.LogWarning("LandingRadar: Площадки для посадки не найдены под кораблем");
            }
        }
    }
    
    /// <summary>
    /// Группирует близкие площадки, оставляя только лучшую из каждой группы
    /// </summary>
    private List<LandingSite> GroupNearbySites(List<LandingSite> sites)
    {
        if (sites.Count == 0) return sites;
        
        List<LandingSite> groupedSites = new List<LandingSite>();
        HashSet<int> usedIndices = new HashSet<int>();
        
        for (int i = 0; i < sites.Count; i++)
        {
            if (usedIndices.Contains(i)) continue;
            
            LandingSite currentSite = sites[i];
            List<int> nearbyIndices = new List<int> { i };
            
            // Находим все близкие площадки
            // Учитываем размер обеих площадок при определении минимального расстояния
            for (int j = i + 1; j < sites.Count; j++)
            {
                if (usedIndices.Contains(j)) continue;
                
                LandingSite otherSite = sites[j];
                float distance = Vector3.Distance(currentSite.position, otherSite.position);
                
                // Минимальное расстояние = сумма радиусов площадок + базовое расстояние
                // Используем полный размер площадок (диаметр), чтобы они не перекрывались
                float minDistance = (currentSite.size + otherSite.size) + MIN_DISTANCE_BETWEEN_SITES;
                
                if (distance < minDistance)
                {
                    nearbyIndices.Add(j);
                }
            }
            
            // Выбираем лучшую площадку из группы (приоритет без препятствий)
            LandingSite bestSite = currentSite;
            foreach (int idx in nearbyIndices)
            {
                LandingSite candidate = sites[idx];
                
                // Приоритет площадкам без препятствий
                if (bestSite.hasObstacles && !candidate.hasObstacles)
                {
                    bestSite = candidate;
                }
                else if (bestSite.hasObstacles == candidate.hasObstacles)
                {
                    if (candidate.suitabilityScore > bestSite.suitabilityScore)
                    {
                        bestSite = candidate;
                    }
                }
            }
            
            groupedSites.Add(bestSite);
            
            // Помечаем все площадки из группы как использованные
            foreach (int idx in nearbyIndices)
            {
                usedIndices.Add(idx);
            }
        }
        
        return groupedSites;
    }
    
    /// <summary>
    /// Проверяет, были ли визуализированы точки
    /// </summary>
    public bool HasVisualizedSites()
    {
        return hasVisualizedSites && siteIndicators.Count > 0;
    }
    
    /// <summary>
    /// Обновляет 3D индикаторы для найденных площадок
    /// </summary>
    private void Update3DIndicators()
    {
        if (!use3DIndicators) return;
        
        // Если точки уже визуализированы и корабль не сместился, не обновляем визуализацию
        if (hasVisualizedSites)
        {
            return; // Пропускаем обновление визуализации
        }
        
        // Удаляем старые индикаторы
        for (int i = siteIndicators.Count - 1; i >= 0; i--)
        {
            if (siteIndicators[i] == null || !foundSites.Contains(siteIndicators[i].GetSite()))
            {
                if (siteIndicators[i] != null)
                {
                    siteIndicators[i].Destroy();
                }
                siteIndicators.RemoveAt(i);
            }
        }
        
        // Создаем новые индикаторы
        foreach (var site in foundSites)
        {
            // Проверяем, есть ли уже индикатор для этой площадки
            // Сравниваем по позиции, так как объекты могут быть разными
            bool hasIndicator = siteIndicators.Any(ind => 
                ind != null && 
                ind.GetSite() != null && 
                Vector3.Distance(ind.GetSite().position, site.position) < 0.1f
            );
            
            if (!hasIndicator)
            {
                GameObject indicatorObj = new GameObject($"LandingSiteIndicator_{site.position}");
                // ВАЖНО: Если indicatorsContainer не установлен, создаем индикаторы в корне сцены
                // чтобы они не поворачивались вместе с кораблем
                if (indicatorsContainer != null)
                {
                    indicatorObj.transform.parent = indicatorsContainer;
                }
                // Если indicatorsContainer == null, индикатор будет в корне сцены (не дочерний объект)
                
                // Устанавливаем позицию и поворот перед инициализацией
                indicatorObj.transform.position = site.position;
                indicatorObj.transform.rotation = Quaternion.identity;
                
                LandingSiteIndicator indicator = indicatorObj.AddComponent<LandingSiteIndicator>();
                indicator.Initialize(site, shipTransform, minIndicatorSize, maxIndicatorSize, indicatorSizeMultiplier);
                siteIndicators.Add(indicator);
            }
        }
        
        // После создания индикаторов помечаем, что визуализация выполнена
        if (siteIndicators.Count > 0 && !hasVisualizedSites)
        {
            hasVisualizedSites = true;
            firstVisualizationPosition = shipTransform != null ? shipTransform.position : Vector3.zero;
            if (showDebugInfo)
            {
                Debug.Log($"LandingRadar: Визуализация точек завершена. Создано {siteIndicators.Count} индикаторов. Позиция корабля: {firstVisualizationPosition}");
            }
        }
    }
    
    /// <summary>
    /// Оценивает пригодность площадки в указанной позиции
    /// </summary>
    private LandingSite EvaluateLandingSite(Vector3 position, float distanceFromShip, out string rejectionReason)
    {
        rejectionReason = "";
        
        if (earlyRejectObstacles)
        {
            float centerGroundHeight = HillGenerator.GetHeightAtPosition(position);
            float bottom = centerGroundHeight;
            float top = centerGroundHeight + OBSTACLE_CHECK_HEIGHT;
            float quickRadius = MIN_LANDING_SITE_SIZE * 0.5f;
            if (TryGetObstaclesNonAlloc(
                    new Vector3(position.x, bottom, position.z),
                    new Vector3(position.x, top, position.z),
                    quickRadius,
                    out int count))
            {
                if (showDebugInfo && Time.frameCount % 120 == 0)
                {
                    Debug.Log($"LandingRadar: EarlyReject check count={count}, pos=({position.x:F1},{position.y:F1},{position.z:F1})");
                }
                for (int i = 0; i < count; i++)
                {
                    Collider col = obstacleOverlapBuffer[i];
                    if (IsValidObstacle(col, centerGroundHeight))
                    {
                        if (showDebugInfo)
                        {
                            Debug.Log($"LandingRadar: EarlyReject obstacle={col.name}");
                        }
                        rejectionReason = "Препятствие рядом (быстрый отсев)";
                        return null;
                    }
                }
            }
        }
        
        // Проверяем ровность
        float flatness = CheckFlatness(position);
        if (flatness > MAX_FLATNESS_DEVIATION)
        {
            rejectionReason = $"Ровность: {flatness:F2}м > {MAX_FLATNESS_DEVIATION}м";
            return null; // Слишком неровная
        }
        
        // Проверяем наклон
        float slopeAngle = CheckSlope(position);
        if (slopeAngle > MAX_SLOPE_ANGLE)
        {
            rejectionReason = $"Наклон: {slopeAngle:F1}° > {MAX_SLOPE_ANGLE}°";
            return null; // Слишком крутой наклон
        }
        
        // Определяем, является ли точка очень ровной (для отладки)
        bool isVeryFlat = flatness < 1f && slopeAngle < 3f;
        
        // Проверяем размер площадки (вычисляем максимальный размер)
        // ВАЖНО: Теперь проверяем препятствия ВО ВРЕМЯ определения размера
        float siteSize = CheckSiteSizeWithObstacles(position);
        // Минимальный размер остается как есть - не меняем проверку
        float minSizeRequired = MIN_LANDING_SITE_SIZE * 0.5f;
        
        // Для отладки: логируем размер для ровных участков
        if (isVeryFlat && showDebugInfo)
        {
            Debug.Log($"LandingRadar: ОТЛАДКА ровной точки - Ровность: {flatness:F2}м, Наклон: {slopeAngle:F1}°, Размер: {siteSize:F1}м (требуется: {minSizeRequired:F1}м)");
        }
        
        if (siteSize < minSizeRequired)
        {
            rejectionReason = $"Размер: {siteSize:F1}м < {minSizeRequired:F1}м";
            if (isVeryFlat && showDebugInfo)
            {
                Debug.LogWarning($"LandingRadar: ВАЖНО! Ровная точка отброшена из-за размера! Ровность: {flatness:F2}м, Наклон: {slopeAngle:F1}°, Размер: {siteSize:F1}м");
            }
            return null; // Слишком маленькая
        }
        
        // Дополнительная проверка препятствий ВНУТРИ площадки (на всякий случай)
        // Используем размер площадки для проверки - если препятствия внутри площадки, отбрасываем
        bool hasObstaclesInside = CheckObstaclesInside(position, siteSize);
        
        if (hasObstaclesInside)
        {
            rejectionReason = $"Препятствия внутри площадки (размер: {siteSize:F1}м)";
            if (isVeryFlat && showDebugInfo)
            {
                Debug.LogWarning($"LandingRadar: ВАЖНО! Ровная точка отброшена - препятствия ВНУТРИ площадки! Размер площадки: {siteSize:F1}м");
            }
            return null; // Препятствия внутри площадки - площадка непригодна
        }
        
        // Дополнительно проверяем препятствия на краю площадки
        float obstacleCheckRadius = isVeryFlat ? Mathf.Min(siteSize, 30f) : siteSize;
        float obstacleDistance = CheckObstaclesAtEdge(position, siteSize, obstacleCheckRadius);
        bool hasObstaclesAtEdge = obstacleDistance < MIN_OBSTACLE_DISTANCE;
        
        // Для отладки: логируем препятствия для ровных участков
        if (isVeryFlat && showDebugInfo)
        {
            Debug.Log($"LandingRadar: ОТЛАДКА препятствий для ровной точки - Расстояние до препятствий на краю: {obstacleDistance:F1}м, " +
                     $"Радиус проверки: {obstacleCheckRadius:F1}м, MIN_OBSTACLE_DISTANCE: {MIN_OBSTACLE_DISTANCE}м, hasObstaclesAtEdge: {hasObstaclesAtEdge}");
        }
        
        // Если препятствия слишком близко к краю, отбрасываем
        // Для ровных участков делаем более мягкую проверку
        float obstacleThreshold = isVeryFlat ? MIN_OBSTACLE_DISTANCE * 0.2f : MIN_OBSTACLE_DISTANCE * 0.5f;
        if (hasObstaclesAtEdge && obstacleDistance < obstacleThreshold)
        {
            rejectionReason = $"Препятствия слишком близко к краю: {obstacleDistance:F1}м < {obstacleThreshold:F1}м";
            if (isVeryFlat && showDebugInfo)
            {
                Debug.LogWarning($"LandingRadar: ВАЖНО! Ровная точка отброшена из-за препятствий на краю! Расстояние: {obstacleDistance:F1}м, Порог: {obstacleThreshold:F1}м");
            }
            return null; // Препятствия слишком близко к краю
        }
        
        // Вычисляем общую оценку пригодности
        float suitabilityScore = CalculateSuitabilityScore(
            flatness, slopeAngle, siteSize, obstacleDistance, distanceFromShip
        );
        
        // Для отладки: логируем успешное создание площадки для ровных участков
        if (isVeryFlat && showDebugInfo)
        {
            Debug.Log($"LandingRadar: ✓✓✓ РОВНАЯ ТОЧКА ПРИНЯТА! Ровность: {flatness:F2}м, Наклон: {slopeAngle:F1}°, " +
                     $"Размер: {siteSize:F1}м, Препятствия: {obstacleDistance:F1}м, Оценка: {suitabilityScore * 100f:F1}%");
        }
        
        rejectionReason = "OK"; // Все проверки пройдены
        // Препятствий внутри нет (иначе площадка была бы отброшена выше)
        // Но могут быть препятствия на краю
        
        // Вычисляем нормаль поверхности для ориентации площадки
        Vector3 surfaceNormal = HillGenerator.GetSurfaceNormal(position, 2f);
        
        return new LandingSite(
            position,
            suitabilityScore,
            siteSize,
            slopeAngle,
            flatness,
            obstacleDistance,
            distanceFromShip,
            hasObstaclesAtEdge,
            surfaceNormal
        );
    }
    
    /// <summary>
    /// Проверяет ровность площадки
    /// </summary>
    private float CheckFlatness(Vector3 center)
    {
        List<float> heights = new List<float>();
        heights.Add(HillGenerator.GetHeightAtPosition(center));
        
        for (int i = 0; i < FLATNESS_CHECK_POINTS; i++)
        {
            float angle = (360f / FLATNESS_CHECK_POINTS) * i * Mathf.Deg2Rad;
            Vector3 checkPos = center + new Vector3(
                Mathf.Cos(angle) * FLATNESS_CHECK_RADIUS,
                0f,
                Mathf.Sin(angle) * FLATNESS_CHECK_RADIUS
            );
            heights.Add(HillGenerator.GetHeightAtPosition(checkPos));
        }
        
        // Вычисляем стандартное отклонение
        float mean = heights.Average();
        float variance = heights.Sum(h => Mathf.Pow(h - mean, 2)) / heights.Count;
        return Mathf.Sqrt(variance);
    }
    
    /// <summary>
    /// Проверяет наклон площадки
    /// </summary>
    private float CheckSlope(Vector3 center)
    {
        float centerHeight = HillGenerator.GetHeightAtPosition(center);
        
        // Проверяем высоты в четырех направлениях
        Vector3[] directions = {
            Vector3.forward,
            Vector3.back,
            Vector3.left,
            Vector3.right
        };
        
        float maxSlope = 0f;
        foreach (Vector3 dir in directions)
        {
            Vector3 checkPos = center + dir * FLATNESS_CHECK_RADIUS;
            float checkHeight = HillGenerator.GetHeightAtPosition(checkPos);
            float heightDiff = Mathf.Abs(checkHeight - centerHeight);
            float distance = FLATNESS_CHECK_RADIUS;
            float angle = Mathf.Atan2(heightDiff, distance) * Mathf.Rad2Deg;
            
            if (angle > maxSlope)
            {
                maxSlope = angle;
            }
        }
        
        return maxSlope;
    }
    
    /// <summary>
    /// Проверяет размер площадки с учетом препятствий - вычисляет МАКСИМАЛЬНЫЙ размер без препятствий
    /// </summary>
    private float CheckSiteSizeWithObstacles(Vector3 center)
    {
        float centerHeight = HillGenerator.GetHeightAtPosition(center);
        float maxRadius = 0f;
        
        float startRadius = MIN_LANDING_SITE_SIZE * 0.5f;
        float maxPossibleRadius = MIN_LANDING_SITE_SIZE * 3f; // Увеличено максимальное расширение для больших площадок
        
        // Начинаем с минимального радиуса и проверяем, можем ли расширить
        // Шаг уменьшен для более точного определения максимального размера
        for (float radius = startRadius; radius <= maxPossibleRadius; radius += 3f)
        {
            int validPoints = 0;
            bool hasObstaclesAtRadius = false;
            
            // Проверяем больше точек по окружности для более точной оценки
            // Увеличено количество точек для более тщательной проверки препятствий
            int checkPoints = 16; // Увеличено с 12 до 16 для более точной проверки
            for (int i = 0; i < checkPoints; i++)
            {
                float angle = (360f / checkPoints) * i * Mathf.Deg2Rad;
                Vector3 checkPos = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0f,
                    Mathf.Sin(angle) * radius
                );
                
                float height = HillGenerator.GetHeightAtPosition(checkPos);
                float heightDiff = Mathf.Abs(height - centerHeight);
                
                // Проверяем ровность
                if (heightDiff <= MAX_FLATNESS_DEVIATION)
                {
                    validPoints++;
                }
                
                // ВАЖНО: Проверяем препятствия на этой точке и вокруг нее
                // Используем больший радиус для проверки, чтобы не пропустить препятствия между точками
                float groundHeight = HillGenerator.GetHeightAtPosition(checkPos);
                float bottom = groundHeight;
                float top = groundHeight + OBSTACLE_CHECK_HEIGHT;
                // Увеличиваем радиус проверки до 5м, чтобы покрыть промежутки между точками
                float checkRadius = Mathf.Max(5f, radius * 0.1f); // Минимум 5м или 10% радиуса
                TryGetObstaclesNonAlloc(
                    new Vector3(checkPos.x, bottom, checkPos.z),
                    new Vector3(checkPos.x, top, checkPos.z),
                    checkRadius,
                    out int obstacleCount
                );
                
                // Фильтруем препятствия
                for (int o = 0; o < obstacleCount; o++)
                {
                    Collider col = obstacleOverlapBuffer[o];
                    // Исключаем корабль
                    if (IsValidObstacle(col, groundHeight))
                    {
                        // Найдено препятствие на этом радиусе
                        hasObstaclesAtRadius = true;
                        break;
                    }
                }
                
                if (hasObstaclesAtRadius)
                {
                    break; // Прервать проверку точек, если найдено препятствие
                }
            }
            
            // Если найдены препятствия на этом радиусе, не можем расширить дальше
            if (hasObstaclesAtRadius)
            {
                break; // Останавливаем расширение
            }
            
            // ДОПОЛНИТЕЛЬНАЯ ПРОВЕРКА: Проверяем всю площадь круга целиком
            // Это гарантирует, что мы не пропустим препятствия между точками
            float centerGroundHeight = HillGenerator.GetHeightAtPosition(center);
            float circleBottom = centerGroundHeight;
            float circleTop = centerGroundHeight + OBSTACLE_CHECK_HEIGHT;
            TryGetObstaclesNonAlloc(
                new Vector3(center.x, circleBottom, center.z),
                new Vector3(center.x, circleTop, center.z),
                radius * 0.95f, // Проверяем 95% радиуса
                out int circleObstacleCount
            );
            
            // Фильтруем препятствия в круге
            for (int o = 0; o < circleObstacleCount; o++)
            {
                Collider col = obstacleOverlapBuffer[o];
                // Исключаем корабль
                if (!IsValidObstacle(col, centerGroundHeight))
                {
                    continue;
                }
                
                // Проверяем расстояние от центра до препятствия
                Vector3 obstaclePos = col.bounds.center;
                float distanceFromCenter = Vector2.Distance(
                    new Vector2(center.x, center.z),
                    new Vector2(obstaclePos.x, obstaclePos.z)
                );
                
                // Учитываем радиус препятствия
                float obstacleRadius = Mathf.Max(col.bounds.size.x, col.bounds.size.z) * 0.5f;
                if (distanceFromCenter + obstacleRadius > radius * 0.95f)
                {
                    continue; // Препятствие за пределами круга
                }
                
                // Получаем высоту земли под препятствием
                float obstacleGroundHeight = HillGenerator.GetHeightAtPosition(obstaclePos);
                
                // Найдено препятствие внутри круга
                hasObstaclesAtRadius = true;
                break;
            }
            
            // Если найдены препятствия в круге, не можем расширить дальше
            if (hasObstaclesAtRadius)
            {
                break; // Останавливаем расширение
            }
            
            // Если хотя бы 75% точек валидны, считаем радиус пригодным
            int requiredValidPoints = Mathf.CeilToInt(checkPoints * 0.75f);
            if (validPoints >= requiredValidPoints)
            {
                maxRadius = radius;
            }
            else
            {
                // Не можем расширить дальше из-за неровности
                break;
            }
        }
        
        // Если даже минимальный радиус не прошел проверку, возвращаем его все равно
        // (проверка размера будет сделана позже с более мягким порогом)
        // Это важно для ровных участков, где может быть проблема с проверкой
        if (maxRadius < startRadius)
        {
            maxRadius = startRadius;
        }
        
        return maxRadius;
    }
    
    /// <summary>
    /// Проверяет наличие препятствий ВНУТРИ площадки
    /// </summary>
    private bool CheckObstaclesInside(Vector3 position, float siteSize)
    {
        // Получаем высоту земли в центре площадки
        float centerGroundHeight = HillGenerator.GetHeightAtPosition(position);
        
        // ВАЖНО: Используем OverlapSphere для проверки ВСЕЙ площади круга сразу
        // Это гарантирует, что мы не пропустим препятствия между проверяемыми точками
        float checkRadius = siteSize * 0.95f; // Проверяем 95% размера площадки
        float bottom = centerGroundHeight;
        float top = centerGroundHeight + OBSTACLE_CHECK_HEIGHT;
        
        // Используем OverlapCapsule для проверки всей цилиндрической области площадки
        TryGetObstaclesNonAlloc(
            new Vector3(position.x, bottom, position.z),
            new Vector3(position.x, top, position.z),
            checkRadius,
            out int obstacleCount
        );
        
        if (obstacleCount == 0)
        {
            return false; // Нет препятствий
        }
        
        // Фильтруем препятствия
        for (int o = 0; o < obstacleCount; o++)
        {
            Collider col = obstacleOverlapBuffer[o];
            // Исключаем корабль
            if (!IsValidObstacle(col, centerGroundHeight))
            {
                continue;
            }
            
            // Получаем позицию препятствия в горизонтальной плоскости
            Vector3 obstaclePos = col.bounds.center;
            float distanceFromCenter = Vector2.Distance(
                new Vector2(position.x, position.z),
                new Vector2(obstaclePos.x, obstaclePos.z)
            );
            
            // Проверяем, что препятствие действительно внутри площадки (с учетом радиуса препятствия)
            float obstacleRadius = Mathf.Max(col.bounds.size.x, col.bounds.size.z) * 0.5f;
            if (distanceFromCenter + obstacleRadius > checkRadius)
            {
                continue; // Препятствие за пределами площадки
            }
            
            // Получаем высоту земли под препятствием
            float obstacleGroundHeight = HillGenerator.GetHeightAtPosition(obstaclePos);
            
            // Найдено препятствие внутри площадки
            if (showDebugInfo)
            {
                Debug.LogWarning($"LandingRadar: Найдено препятствие внутри площадки! Позиция: {obstaclePos}, Расстояние от центра: {distanceFromCenter:F1}м, Размер площадки: {siteSize:F1}м");
            }
            return true;
        }
        
        return false; // Нет препятствий внутри
    }
    
    /// <summary>
    /// Проверяет наличие препятствий на краю площадки
    /// </summary>
    private float CheckObstaclesAtEdge(Vector3 position, float siteSize, float checkRadius)
    {
        // Используем OverlapCapsule для более точной проверки препятствий на краю
        // Проверяем от земли до высоты препятствий
        float bottom = position.y;
        float top = position.y + OBSTACLE_CHECK_HEIGHT;
        TryGetObstaclesNonAlloc(
            new Vector3(position.x, bottom, position.z),
            new Vector3(position.x, top, position.z),
            checkRadius,
            out int obstacleCount
        );
        
        if (obstacleCount == 0)
        {
            return checkRadius * 2f; // Нет препятствий
        }
        
        // Фильтруем препятствия: исключаем корабль и триггеры
        List<Collider> validObstacles = new List<Collider>();
        for (int o = 0; o < obstacleCount; o++)
        {
            Collider col = obstacleOverlapBuffer[o];
            // Исключаем корабль
            float groundHeight = position.y;
            if (!IsValidObstacle(col, groundHeight))
            {
                continue;
            }
            
            validObstacles.Add(col);
        }
        
        if (validObstacles.Count == 0)
        {
            return checkRadius * 2f; // Нет валидных препятствий
        }
        
        // Находим ближайшее препятствие
        float minDistance = float.MaxValue;
        foreach (Collider col in validObstacles)
        {
            // Используем расстояние от центра препятствия до точки, а не ClosestPoint
            // ClosestPoint может возвращать 0, если точка внутри коллайдера
            Vector3 obstacleCenter = col.bounds.center;
            float distance = Vector3.Distance(new Vector3(position.x, position.y, position.z), 
                                             new Vector3(obstacleCenter.x, position.y, obstacleCenter.z));
            
            // Вычитаем радиус препятствия, чтобы получить расстояние до его края
            float obstacleRadius = Mathf.Max(col.bounds.size.x, col.bounds.size.z) * 0.5f;
            distance = Mathf.Max(0f, distance - obstacleRadius);
            
            if (distance < minDistance)
            {
                minDistance = distance;
            }
        }
        
        return minDistance;
    }
    
    /// <summary>
    /// Вычисляет общую оценку пригодности площадки
    /// </summary>
    private float CalculateSuitabilityScore(float flatness, float slopeAngle, float siteSize, 
                                           float obstacleDistance, float distanceFromShip)
    {
        float score = 0f;
        float maxScore = 100f;
        
        // Ровность (0-30 баллов)
        float flatnessScore = Mathf.Clamp01(1f - (flatness / MAX_FLATNESS_DEVIATION)) * 30f;
        score += flatnessScore;
        
        // Наклон (0-30 баллов)
        float slopeScore = Mathf.Clamp01(1f - (slopeAngle / MAX_SLOPE_ANGLE)) * 30f;
        score += slopeScore;
        
        // Размер (0-20 баллов)
        float sizeScore = Mathf.Clamp01(siteSize / (MIN_LANDING_SITE_SIZE * 2f)) * 20f;
        score += sizeScore;
        
        // Расстояние до препятствий (0-20 баллов)
        float obstacleScore = 0f;
        if (obstacleDistance >= MIN_OBSTACLE_DISTANCE * 2f)
        {
            obstacleScore = 20f;
        }
        else if (obstacleDistance >= MIN_OBSTACLE_DISTANCE)
        {
            obstacleScore = 10f + 10f * ((obstacleDistance - MIN_OBSTACLE_DISTANCE) / MIN_OBSTACLE_DISTANCE);
        }
        else
        {
            obstacleScore = 5f * (obstacleDistance / MIN_OBSTACLE_DISTANCE);
        }
        score += obstacleScore;
        
        return score / maxScore; // Нормализуем к 0-1
    }
}
