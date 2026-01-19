using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LandingAutopilot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShipController shipController;
    [SerializeField] private LandingRadar landingRadar;
    
    [Header("Speed Control")]
    [SerializeField] private float maxFallSpeed = 10f; // Максимальная скорость падения на большой высоте (м/с)
    [SerializeField] private float brakingStartHeight = 300f; // Высота начала торможения (м) - начинаем тормозить заранее
    [SerializeField] private float brakingSpeed = 10f; // Скорость между 300м и 100м (м/с)
    [SerializeField] private float slowFallHeight = 100f; // Высота, к которой нужно снизить скорость до slowFallSpeed (м)
    [SerializeField] private float slowFallSpeed = 5f; // Скорость к 100м (м/с)
    [SerializeField] private float finalLandingSpeed = 3f; // Финальная скорость посадки (м/с) - используется очень близко к земле
    [SerializeField] private float approachSpeed = 15f; // Скорость приближения (м/с) - не используется
    [SerializeField] private float brakingDistance = 100f; // Расстояние для начала торможения (м) - не используется
    [SerializeField] private float landingSpeed = 0.5f; // Скорость финальной посадки (м/с) - используется в фазе Landing
    
    [Header("Orientation Control")]
    [SerializeField] private float orientationSmoothing = 5f; // Плавность выравнивания
    [SerializeField] private float maxOrientationAngle = 5f; // Максимальный угол отклонения для посадки (градусы)
    
    [Header("PID Controllers")]
    [SerializeField] private PIDController verticalSpeedPID = new PIDController(0.5f, 0.05f, 0.2f);
    [SerializeField] private PIDController horizontalSpeedPIDX = new PIDController(0.3f, 0.02f, 0.15f); // Для оси X (влево/вправо)
    [SerializeField] private PIDController horizontalSpeedPIDZ = new PIDController(0.3f, 0.02f, 0.15f); // Для оси Z (вперед/назад)
    [SerializeField] private PIDController orientationPID = new PIDController(2f, 0.1f, 0.5f);
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    
    // Фазы посадки
    public enum LandingPhase
    {
        None,           // Автопилот не активен
        WaitingForSite, // Ожидание визуализации точек радаром
        Approaching,    // Приближение к площадке (нормальная скорость)
        Braking,        // Торможение за 100м (медленная скорость)
        Landing         // Финальная посадка (очень медленно, выравнивание)
    }
    
    private LandingPhase currentPhase = LandingPhase.None;
    private bool isActive = false;
    private LandingSite targetSite = null;
    private Vector3 initialScanPosition; // Позиция корабля при первом сканировании
    
    // События
    public delegate void AutopilotStateChangedDelegate(bool isActive);
    public event AutopilotStateChangedDelegate OnAutopilotStateChanged;
    
    public delegate void LandingPhaseChangedDelegate(LandingPhase phase);
    public event LandingPhaseChangedDelegate OnLandingPhaseChanged;
    
    private void Start()
    {
        // Находим необходимые компоненты
        if (shipController == null)
        {
            shipController = FindObjectOfType<ShipController>();
        }
        
        if (landingRadar == null)
        {
            landingRadar = FindObjectOfType<LandingRadar>();
        }
        
        // Проверяем наличие компонентов
        if (shipController == null)
        {
            Debug.LogError("LandingAutopilot: ShipController не найден!");
            enabled = false;
            return;
        }
        
        if (landingRadar == null)
        {
            Debug.LogError("LandingAutopilot: LandingRadar не найден!");
            enabled = false;
            return;
        }
        
        // Проверяем и исправляем значения скорости, если они неправильные
        if (brakingSpeed < 8f || brakingSpeed > 15f)
        {
            Debug.LogWarning($"LandingAutopilot: brakingSpeed имеет неправильное значение ({brakingSpeed}м/с). Устанавливаю правильное значение: 10м/с");
            brakingSpeed = 10f;
        }
        
        if (slowFallSpeed < 4f || slowFallSpeed > 8f)
        {
            Debug.LogWarning($"LandingAutopilot: slowFallSpeed имеет неправильное значение ({slowFallSpeed}м/с). Устанавливаю правильное значение: 5м/с");
            slowFallSpeed = 5f;
        }
        
        if (finalLandingSpeed < 2f || finalLandingSpeed > 5f)
        {
            Debug.LogWarning($"LandingAutopilot: finalLandingSpeed имеет неправильное значение ({finalLandingSpeed}м/с). Устанавливаю правильное значение: 3м/с");
            finalLandingSpeed = 3f;
        }
        
        // Инициализируем PID-регуляторы
        InitializePIDControllers();
        
        if (showDebugInfo)
        {
            Debug.Log("LandingAutopilot: Инициализирован успешно");
        }
    }
    
    private void Awake()
    {
        InitializePIDControllers();
    }
    
    /// <summary>
    /// Инициализирует PID-регуляторы
    /// </summary>
    private void InitializePIDControllers()
    {
        verticalSpeedPID.SetOutputLimits(-1f, 1f);
        horizontalSpeedPIDX.SetOutputLimits(-1f, 1f);
        horizontalSpeedPIDZ.SetOutputLimits(-1f, 1f);
        orientationPID.SetOutputLimits(-1f, 1f);
        
        verticalSpeedPID.Reset();
        horizontalSpeedPIDX.Reset();
        horizontalSpeedPIDZ.Reset();
        orientationPID.Reset();
    }
    
    private void Update()
    {
        if (!isActive) return;
        
        // Обновляем автопилот в зависимости от текущей фазы
        switch (currentPhase)
        {
            case LandingPhase.WaitingForSite:
                UpdateWaitingForSite();
                break;
            case LandingPhase.Approaching:
                UpdateApproaching();
                break;
            case LandingPhase.Braking:
                UpdateBraking();
                break;
            case LandingPhase.Landing:
                UpdateLanding();
                break;
        }
    }
    
    /// <summary>
    /// Запускает автопилот
    /// </summary>
    public void StartLanding()
    {
        if (isActive)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("LandingAutopilot: Автопилот уже активен!");
            }
            return;
        }
        
        // Проверяем наличие компонентов
        if (shipController == null)
        {
            shipController = FindObjectOfType<ShipController>();
            if (shipController == null)
            {
                Debug.LogError("LandingAutopilot: ShipController не найден! Невозможно запустить автопилот.");
                return;
            }
        }
        
        if (landingRadar == null)
        {
            landingRadar = FindObjectOfType<LandingRadar>();
            if (landingRadar == null)
            {
                Debug.LogError("LandingAutopilot: LandingRadar не найден! Невозможно запустить автопилот.");
                return;
            }
        }
        
        // Проверяем максимальный TWR
        float maxTWR = shipController.GetMaxTWR();
        if (maxTWR < 1.0f)
        {
            Debug.LogError($"LandingAutopilot: Невозможно запустить автопилот! Максимальный TWR < 1.0 ({maxTWR:F2}) - корабль не может остановить падение.");
            return;
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"LandingAutopilot: Максимальный TWR проверен: {maxTWR:F2} (OK)");
        }
        
        // Активируем автопилот в ShipController
        shipController.SetAutopilotActive(true);
        
        // Сбрасываем PID-регуляторы
        InitializePIDControllers();
        
        // Запоминаем начальную позицию для отслеживания смещения
        initialScanPosition = shipController.transform.position;
        
        // КРИТИЧНО: Проверяем начальную скорость корабля
        Vector3 initialVelocity = shipController.GetVelocity();
        float initialVerticalSpeed = initialVelocity.y;
        Vector2 initialHorizontalVelocity = new Vector2(initialVelocity.x, initialVelocity.z);
        float initialHorizontalSpeed = initialHorizontalVelocity.magnitude;
        
        if (showDebugInfo)
        {
            Debug.Log($"LandingAutopilot: Начальная скорость при включении:");
            Debug.Log($"  Вертикальная (Y): {initialVerticalSpeed:F2} м/с");
            Debug.Log($"  Горизонтальная (X, Z): ({initialVelocity.x:F2}, {initialVelocity.z:F2}) м/с, Magnitude: {initialHorizontalSpeed:F2} м/с");
        }
        
        // Если вертикальная скорость слишком высокая, сразу начинаем её компенсировать
        if (initialVerticalSpeed < -maxFallSpeed)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning($"LandingAutopilot: ⚠️ Начальная скорость падения слишком высокая: {initialVerticalSpeed:F2} м/с (максимум: {-maxFallSpeed:F2} м/с)");
            }
        }
        
        // Если горизонтальная скорость значительная, компенсируем её
        if (initialHorizontalSpeed > 2f)
        {
            Transform shipTransform = shipController.transform;
            Vector3 localHorizontalVelocity = shipTransform.InverseTransformDirection(new Vector3(initialVelocity.x, 0f, initialVelocity.z));
            Vector2 localHorizontalVelocity2D = new Vector2(localHorizontalVelocity.x, localHorizontalVelocity.z);
            Vector2 compensationDirection = -localHorizontalVelocity2D.normalized;
            float compensationStrength = Mathf.Min(1f, initialHorizontalSpeed / 10f);
            
            shipController.SetMovementDirection(compensationDirection * compensationStrength);
            
            if (showDebugInfo)
            {
                Debug.LogWarning($"LandingAutopilot: ⚠️ Начальная горизонтальная скорость: {initialHorizontalSpeed:F2} м/с. Компенсирую: {compensationDirection * compensationStrength}");
            }
        }
        else
        {
            shipController.SetMovementDirection(Vector2.zero);
        }
        
        // Включаем минимальную тягу для контроля падения
        float hoverThrust = CalculateHoverThrust();
        shipController.SetThrust(hoverThrust);
        
        isActive = true;
        currentPhase = LandingPhase.WaitingForSite;
        targetSite = null;
        
        OnAutopilotStateChanged?.Invoke(true);
        OnLandingPhaseChanged?.Invoke(currentPhase);
        
        if (showDebugInfo)
        {
            Debug.Log("LandingAutopilot: Автопилот запущен. Ожидаю визуализации точек радаром...");
        }
    }
    
    /// <summary>
    /// Останавливает автопилот
    /// </summary>
    public void StopLanding()
    {
        if (!isActive) return;
        
        // Деактивируем автопилот в ShipController
        shipController.SetAutopilotActive(false);
        
        // Отключаем тягу
        shipController.SetThrust(0f);
        shipController.SetMovementDirection(Vector2.zero);
        
        isActive = false;
        currentPhase = LandingPhase.None;
        targetSite = null;
        
        OnAutopilotStateChanged?.Invoke(false);
        OnLandingPhaseChanged?.Invoke(currentPhase);
        
        if (showDebugInfo)
        {
            Debug.Log("LandingAutopilot: Автопилот остановлен.");
        }
    }
    
    /// <summary>
    /// Проверяет, активен ли автопилот
    /// </summary>
    public bool IsActive()
    {
        return isActive;
    }
    
    /// <summary>
    /// Получает текущую фазу посадки
    /// </summary>
    public LandingPhase GetCurrentPhase()
    {
        return currentPhase;
    }
    
    /// <summary>
    /// Получает целевую посадочную площадку
    /// </summary>
    public LandingSite GetTargetSite()
    {
        return targetSite;
    }
    
    // ========== ФАЗЫ ПОСАДКИ ==========
    
    /// <summary>
    /// Ожидание визуализации точек радаром
    /// </summary>
    private void UpdateWaitingForSite()
    {
        // Контролируем скорость падения, пока ждем точки
        ControlFallSpeed();
        
        // Проверяем, есть ли визуализированные точки
        List<LandingSite> sites = landingRadar != null ? landingRadar.GetFoundSites() : null;
        
        if (sites == null || sites.Count == 0)
        {
            if (showDebugInfo && Time.frameCount % 120 == 0)
            {
                Debug.Log($"LandingAutopilot: Ожидаю визуализации точек радаром... (радар: {(landingRadar != null ? "найден" : "НЕ НАЙДЕН")})");
            }
            return;
        }
        
        if (showDebugInfo && Time.frameCount % 120 == 0)
        {
            Debug.Log($"LandingAutopilot: Найдено {sites.Count} площадок. Проверяю визуализацию...");
        }
        
        // Проверяем, визуализированы ли точки (есть ли индикаторы)
        bool hasVisualized = landingRadar.HasVisualizedSites();
        if (!hasVisualized)
        {
            if (showDebugInfo && Time.frameCount % 120 == 0)
            {
                Debug.Log($"LandingAutopilot: Точки найдены ({sites.Count}), но еще не визуализированы. Ожидаю...");
            }
            return;
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"LandingAutopilot: Точки визуализированы! Выбираю лучшую площадку из {sites.Count}...");
        }
        
        // Выбираем лучшую площадку
        LandingSite selectedSite = SelectBestLandingSite(sites);
        
        if (selectedSite == null)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("LandingAutopilot: Не удалось выбрать площадку для посадки!");
            }
            return; // Продолжаем ждать
        }
        
        targetSite = selectedSite;
        currentPhase = LandingPhase.Approaching;
        OnLandingPhaseChanged?.Invoke(currentPhase);
        
        Vector3 currentShipPos = shipController.transform.position;
        float actualDistance = Vector3.Distance(currentShipPos, targetSite.position);
        
        if (showDebugInfo)
        {
            Debug.Log($"LandingAutopilot: === ВЫБРАНА ПЛОЩАДКА ===");
            Debug.Log($"  Позиция корабля: ({currentShipPos.x:F2}, {currentShipPos.y:F2}, {currentShipPos.z:F2})");
            Debug.Log($"  Позиция площадки: ({targetSite.position.x:F2}, {targetSite.position.y:F2}, {targetSite.position.z:F2})");
            Debug.Log($"  Расстояние: {actualDistance:F1}м");
            Debug.Log($"  Пригодность: {targetSite.suitabilityScore * 100f:F0}%");
            Debug.Log($"  Размер площадки: {targetSite.size:F1}м");
            Debug.Log($"  Направление к площадке (мировые): ({targetSite.position.x - currentShipPos.x:F2}, {targetSite.position.z - currentShipPos.z:F2})");
        }
    }
    
    /// <summary>
    /// Приближение к площадке (нормальная скорость)
    /// </summary>
    private void UpdateApproaching()
    {
        // Проверяем доступность текущей точки
        if (!IsSiteAvailable(targetSite))
        {
            // Ищем новую точку
            TrySwitchToNewSite();
            return;
        }
        
        Vector3 shipPosition = shipController.transform.position;
        Vector3 targetPosition = targetSite.position;
        Vector3 shipVelocity = shipController.GetVelocity();
        
        float totalDistance = Vector3.Distance(shipPosition, targetPosition);
        
        // Детальное логирование координат
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"LandingAutopilot: === ПРИБЛИЖЕНИЕ ===");
            Debug.Log($"  Корабль: ({shipPosition.x:F1}, {shipPosition.y:F1}, {shipPosition.z:F1})");
            Debug.Log($"  Цель: ({targetPosition.x:F1}, {targetPosition.y:F1}, {targetPosition.z:F1})");
            Debug.Log($"  Расстояние: {totalDistance:F1}м");
            Debug.Log($"  Скорость корабля: ({shipVelocity.x:F2}, {shipVelocity.y:F2}, {shipVelocity.z:F2}) м/с");
            Debug.Log($"  Направление к цели (мировые): ({targetPosition.x - shipPosition.x:F1}, {targetPosition.z - shipPosition.z:F1})");
        }
        
        // Если близко к площадке, переходим к торможению
        if (totalDistance <= brakingDistance)
        {
            currentPhase = LandingPhase.Braking;
            OnLandingPhaseChanged?.Invoke(currentPhase);
            if (showDebugInfo)
            {
                Debug.Log($"LandingAutopilot: Переход к фазе торможения (расстояние: {totalDistance:F1}м)");
            }
            return;
        }
        
        // Управление: контроль скорости падения + движение к цели
        ControlFallSpeed();
        MoveTowardsTarget(targetPosition, approachSpeed);
    }
    
    /// <summary>
    /// Торможение за 100м (медленная скорость)
    /// </summary>
    private void UpdateBraking()
    {
        // Проверяем доступность текущей точки
        if (!IsSiteAvailable(targetSite))
        {
            TrySwitchToNewSite();
            return;
        }
        
        Vector3 shipPosition = shipController.transform.position;
        Vector3 targetPosition = targetSite.position;
        Vector3 shipVelocity = shipController.GetVelocity();
        
        float horizontalDistance = Vector2.Distance(
            new Vector2(shipPosition.x, shipPosition.z),
            new Vector2(targetPosition.x, targetPosition.z)
        );
        float verticalDistance = targetPosition.y - shipPosition.y;
        float totalDistance = Vector3.Distance(shipPosition, targetPosition);
        
        // Если очень близко, переходим к финальной посадке
        if (totalDistance < 5f && Mathf.Abs(verticalDistance) < 3f)
        {
            currentPhase = LandingPhase.Landing;
            OnLandingPhaseChanged?.Invoke(currentPhase);
            if (showDebugInfo)
            {
                Debug.Log($"LandingAutopilot: Переход к фазе финальной посадки (расстояние: {totalDistance:F1}м)");
            }
            return;
        }
        
        // Управление: контроль скорости падения (на основе высоты) + движение к цели
        // НЕ передаем параметр, чтобы использовалась правильная скорость на основе высоты (10 м/с на 300м, плавно до 5 м/с на 100м)
        ControlFallSpeed();
        MoveTowardsTarget(targetPosition, brakingSpeed);
    }
    
    /// <summary>
    /// Финальная посадка (очень медленно, выравнивание)
    /// </summary>
    private void UpdateLanding()
    {
        // Проверяем доступность текущей точки
        if (!IsSiteAvailable(targetSite))
        {
            TrySwitchToNewSite();
            return;
        }
        
        Vector3 shipPosition = shipController.transform.position;
        Vector3 targetPosition = targetSite.position;
        Vector3 shipVelocity = shipController.GetVelocity();
        
        float horizontalDistance = Vector2.Distance(
            new Vector2(shipPosition.x, shipPosition.z),
            new Vector2(targetPosition.x, targetPosition.z)
        );
        float verticalDistance = targetPosition.y - shipPosition.y;
        float totalDistance = Vector3.Distance(shipPosition, targetPosition);
        
        // Если корабль коснулся земли или очень близко
        if (totalDistance < 0.5f || verticalDistance < 0.2f)
        {
            shipController.SetThrust(0f);
            shipController.SetMovementDirection(Vector2.zero);
            StopLanding();
            if (showDebugInfo)
            {
                Debug.Log("LandingAutopilot: Посадка завершена!");
            }
            return;
        }
        
        // Выравнивание по нормали поверхности
        AlignToSurfaceNormal(targetSite.surfaceNormal);
        
        // Управление: очень медленное движение к цели
        ControlFallSpeed(landingSpeed);
        MoveTowardsTarget(targetPosition, landingSpeed);
    }
    
    // ========== УПРАВЛЕНИЕ ==========
    
    /// <summary>
    /// Контролирует скорость падения с адаптивной логикой:
    /// - На большой высоте (> brakingStartHeight/300м): максимальная скорость (динамическая, в зависимости от TWR)
    /// - На 300м: скорость = brakingSpeed (10 м/с)
    /// - Между 300м и 100м: плавное уменьшение от 10 м/с до 5 м/с
    /// - На 100м: скорость = slowFallSpeed (5 м/с)
    /// - Ниже 100м: плавное замедление от 5 м/с до finalLandingSpeed (3 м/с)
    /// </summary>
    private void ControlFallSpeed(float maxSpeed = -1f)
    {
        Vector3 shipVelocity = shipController.GetVelocity();
        float currentVerticalSpeed = shipVelocity.y;
        
        // Вычисляем высоту до цели (если есть цель)
        float verticalDistance = float.MaxValue;
        if (targetSite != null)
        {
            Vector3 shipPosition = shipController.transform.position;
            verticalDistance = shipPosition.y - targetSite.position.y;
        }
        else
        {
            // Если цели нет, используем текущую высоту корабля (от Y=0)
            verticalDistance = shipController.transform.position.y;
        }
        
        // Вычисляем целевую скорость падения в зависимости от высоты
        float targetMaxSpeed;
        
        // ДИАГНОСТИКА: Логируем высоту и вычисленную скорость
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"ControlFallSpeed: verticalDistance={verticalDistance:F1}м, brakingStartHeight={brakingStartHeight}м, slowFallHeight={slowFallHeight}м");
        }
        
        if (verticalDistance > brakingStartHeight)
        {
            // На большой высоте: динамическая скорость без ограничений
            // Вычисляем максимальную безопасную скорость на основе способности затормозить к 300м
            float maxTWR = shipController.GetMaxTWR();
            float gravity = shipController.GetGravityStrength();
            
            // Вычисляем расстояние до точки начала торможения
            float distanceToBrakingStart = verticalDistance - brakingStartHeight;
            
            // Физика торможения: v² = v₀² + 2*a*s
            // Где: v - конечная скорость (slowFallSpeed), v₀ - начальная скорость, a - ускорение (отрицательное при торможении), s - расстояние
            // Ускорение торможения: a = (TWR - 1) * g (положительное, так как это ускорение вверх против падения)
            // При торможении: v² = v₀² - 2*a*s (ускорение направлено против скорости)
            // Переписываем: v₀² = v² + 2*a*s
            // v₀ = sqrt(v² + 2*a*s)
            
            float brakingAcceleration = (maxTWR - 1f) * gravity; // Ускорение при 100% тяги (вверх)
            float targetSpeedAtBrakingStart = brakingSpeed; // Скорость, которую хотим иметь к 300м (10 м/с)
            
            // Вычисляем максимальную безопасную скорость на текущей высоте
            // Используем 90% способности (более агрессивно, но с запасом)
            float safeBrakingAcceleration = brakingAcceleration * 0.9f;
            float maxSafeSpeedSquared = targetSpeedAtBrakingStart * targetSpeedAtBrakingStart + 2f * safeBrakingAcceleration * distanceToBrakingStart;
            
            if (maxSafeSpeedSquared > 0f)
            {
                targetMaxSpeed = Mathf.Sqrt(maxSafeSpeedSquared);
                // Убираем жесткое ограничение - используем физически вычисленную скорость
                // Ограничиваем только разумным максимумом для безопасности (например, 100 м/с)
                targetMaxSpeed = Mathf.Min(targetMaxSpeed, 100f);
            }
            else
            {
                // Если формула дает отрицательное значение (не должно быть), используем brakingSpeed (10 м/с)
                targetMaxSpeed = brakingSpeed;
            }
        }
        else if (verticalDistance > slowFallHeight)
        {
            // Между brakingStartHeight (300м) и slowFallHeight (100м): плавное уменьшение от brakingSpeed (10 м/с) до slowFallSpeed (5 м/с)
            // На высоте 300м: скорость = brakingSpeed (10 м/с)
            // На высоте 100м: скорость = slowFallSpeed (5 м/с)
            float distanceFromBrakingStart = verticalDistance - slowFallHeight; // От 0 (на 100м) до 200 (на 300м)
            float totalBrakingDistance = brakingStartHeight - slowFallHeight; // 200м
            float t = distanceFromBrakingStart / totalBrakingDistance; // t от 0 (на 100м) до 1 (на 300м)
            t = Mathf.Clamp01(t);
            targetMaxSpeed = Mathf.Lerp(slowFallSpeed, brakingSpeed, t); // От 5 м/с (на 100м) до 10 м/с (на 300м)
        }
        else
        {
            // Ниже slowFallHeight (100м): плавное замедление от slowFallSpeed (5 м/с) до finalLandingSpeed (3 м/с)
            // Используем линейную интерполяцию в зависимости от высоты
            // На высоте 100м: скорость = slowFallSpeed (5 м/с)
            // На высоте 0м: скорость = finalLandingSpeed (3 м/с)
            float t = verticalDistance / slowFallHeight; // t от 0 (на земле) до 1 (на 100м)
            t = Mathf.Clamp01(t);
            targetMaxSpeed = Mathf.Lerp(finalLandingSpeed, slowFallSpeed, t);
        }
        
        // Если передан явный maxSpeed, используем его (для фаз Braking и Landing)
        if (maxSpeed > 0f)
        {
            targetMaxSpeed = maxSpeed;
        }
        
        // Если падаем слишком быстро, увеличиваем тягу
        float targetVerticalSpeed = -targetMaxSpeed; // Отрицательная = вниз
        float verticalSpeedError = targetVerticalSpeed - currentVerticalSpeed;
        
        float hoverThrust = CalculateHoverThrust();
        float verticalCorrection = verticalSpeedPID.Update(targetVerticalSpeed, currentVerticalSpeed, Time.deltaTime);
        
        // Корректируем тягу для контроля скорости
        // Если ошибка скорости большая, увеличиваем влияние коррекции
        float correctionMultiplier = 1f;
        if (Mathf.Abs(verticalSpeedError) > 5f)
        {
            // Если ошибка > 5 м/с, увеличиваем влияние коррекции (до 1.0 вместо 0.3)
            correctionMultiplier = Mathf.Lerp(0.3f, 1.0f, Mathf.Clamp01((Mathf.Abs(verticalSpeedError) - 5f) / 10f));
        }
        
        float totalThrust = Mathf.Clamp01(hoverThrust + verticalCorrection * correctionMultiplier);
        
        // Если скорость падения критически высокая, используем максимальную тягу
        if (currentVerticalSpeed < -targetMaxSpeed * 1.5f)
        {
            totalThrust = 1f; // 100% тяги для экстренного торможения
        }
        
        shipController.SetThrust(totalThrust);
        
        // Логирование
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"LandingAutopilot: === КОНТРОЛЬ СКОРОСТИ ПАДЕНИЯ ===");
            Debug.Log($"  Высота до цели: {verticalDistance:F1}м");
            Debug.Log($"  Текущая скорость падения: {currentVerticalSpeed:F2}м/с");
            Debug.Log($"  Целевая максимальная скорость: {targetMaxSpeed:F2}м/с");
            Debug.Log($"  Целевая скорость (отрицательная): {targetVerticalSpeed:F2}м/с");
            Debug.Log($"  Ошибка скорости: {verticalSpeedError:F2}м/с");
            Debug.Log($"  Тяга: {totalThrust:F2}");
            if (verticalDistance > brakingStartHeight)
            {
                Debug.Log($"  Режим: БЫСТРОЕ ПАДЕНИЕ (высота > {brakingStartHeight}м)");
            float maxTWR = shipController.GetMaxTWR();
            float gravity = shipController.GetGravityStrength();
            float brakingAcceleration = (maxTWR - 1f) * gravity;
            Debug.Log($"  TWR: {maxTWR:F2}, Ускорение торможения: {brakingAcceleration:F2} м/с²");
            if (verticalDistance > brakingStartHeight)
            {
                float distanceToBrakingStart = verticalDistance - brakingStartHeight;
                Debug.Log($"  Расстояние до точки торможения: {distanceToBrakingStart:F1}м");
            }
            }
            else if (verticalDistance > slowFallHeight)
            {
                float t = (verticalDistance - slowFallHeight) / (brakingStartHeight - slowFallHeight);
                t = Mathf.Clamp01(t);
                float currentSpeed = Mathf.Lerp(slowFallSpeed, brakingSpeed, t);
                Debug.Log($"  Режим: ТОРМОЖЕНИЕ (высота {slowFallHeight}-{brakingStartHeight}м)");
                Debug.Log($"    ФАКТИЧЕСКИЕ ЗНАЧЕНИЯ: brakingSpeed={brakingSpeed}м/с, slowFallSpeed={slowFallSpeed}м/с, t={t:F2}");
                Debug.Log($"    Вычисленная скорость: {currentSpeed:F1}м/с, targetMaxSpeed={targetMaxSpeed:F1}м/с");
            }
            else
            {
                Debug.Log($"  Режим: ФИНАЛЬНОЕ ЗАМЕДЛЕНИЕ (высота < {slowFallHeight}м, от {slowFallSpeed}м/с до {finalLandingSpeed}м/с)");
            }
        }
    }
    
    /// <summary>
    /// Движение к цели с учетом инерции
    /// НОВЫЙ ПОДХОД: Управление по скорости вместо управления по направлению
    /// </summary>
    private void MoveTowardsTarget(Vector3 targetPosition, float maxSpeed)
    {
        Transform shipTransform = shipController.transform;
        Vector3 shipPosition = shipTransform.position;
        Vector3 shipVelocity = shipController.GetVelocity();
        
        // Вычисляем горизонтальное расстояние до цели
        Vector3 worldDelta = (targetPosition - shipPosition);
        Vector3 worldHorizontalDelta = new Vector3(worldDelta.x, 0f, worldDelta.z);
        float horizontalDistance = worldHorizontalDelta.magnitude;
        
        // Если очень близко, просто компенсируем скорость
        if (horizontalDistance < 2.0f)
        {
            Vector2 worldHorizontalVelocity = new Vector2(shipVelocity.x, shipVelocity.z);
            if (worldHorizontalVelocity.magnitude > 0.5f)
            {
                Vector3 localVelocityForCompensation = shipTransform.InverseTransformDirection(new Vector3(shipVelocity.x, 0f, shipVelocity.z));
                Vector2 localVelocity2D = new Vector2(localVelocityForCompensation.x, localVelocityForCompensation.z);
                Vector2 compensationDirection = -localVelocity2D.normalized;
                float compensationStrength = Mathf.Clamp01(localVelocity2D.magnitude / maxSpeed);
                shipController.SetMovementDirection(compensationDirection * compensationStrength);
            }
            else
            {
                shipController.SetMovementDirection(Vector2.zero);
            }
            return;
        }
        
        // НОВЫЙ ПОДХОД: Управление по скорости
        // ВАЖНО: В Unity координаты:
        //   X = влево/вправо (горизонтальная ось)
        //   Y = вверх/вниз (вертикальная ось)
        //   Z = вперед/назад (горизонтальная ось)
        // SetMovementDirection использует Vector2(x, y), где:
        //   x = влево/вправо (локальный X корабля)
        //   y = вперед/назад (локальный Z корабля, НЕ Unity Y!)
        
        // Преобразуем горизонтальную разницу в локальные координаты
        Vector3 localHorizontalDelta = shipTransform.InverseTransformDirection(worldHorizontalDelta);
        // localHorizontalDelta: (X=влево/вправо, Y=может быть небольшое значение из-за наклона, Z=вперед/назад)
        // Игнорируем Y компоненту (вертикальную), используем только X и Z
        localHorizontalDelta.y = 0f; // Принудительно обнуляем вертикальную компоненту
        
        // Вычисляем желаемую скорость к цели (пропорционально расстоянию, но ограниченную maxSpeed)
        float desiredSpeed = Mathf.Min(horizontalDistance * 0.3f, maxSpeed);
        // ВАЖНО: Vector2 для SetMovementDirection: x=локальный X, y=локальный Z (не Unity Y!)
        Vector2 desiredVelocityLocal = new Vector2(
            localHorizontalDelta.x,  // влево/вправо (локальный X)
            localHorizontalDelta.z   // вперед/назад (локальный Z, но хранится в y Vector2)
        );
        if (desiredVelocityLocal.magnitude > 0.001f)
        {
            desiredVelocityLocal = desiredVelocityLocal.normalized * desiredSpeed;
        }
        else
        {
            desiredVelocityLocal = Vector2.zero;
        }
        
        // Вычисляем текущую горизонтальную скорость в локальных координатах
        // Берем только X и Z компоненты (горизонтальные), игнорируем Y (вертикальную)
        Vector3 worldHorizontalVelocity3D = new Vector3(shipVelocity.x, 0f, shipVelocity.z);
        Vector3 localVelocity3D = shipTransform.InverseTransformDirection(worldHorizontalVelocity3D);
        // localVelocity3D: (X=влево/вправо, Y=0, Z=вперед/назад)
        Vector2 currentVelocityLocal = new Vector2(
            localVelocity3D.x,  // влево/вправо (локальный X)
            localVelocity3D.z    // вперед/назад (локальный Z, но хранится в y Vector2)
        );
        
        // Вычисляем ошибку скорости
        Vector2 velocityError = desiredVelocityLocal - currentVelocityLocal;
        
        // КРИТИЧНО: Проверяем, направлена ли текущая скорость ОТ цели
        // Вычисляем проекцию текущей скорости на направление к цели
        Vector2 desiredDirection = desiredVelocityLocal.magnitude > 0.001f ? desiredVelocityLocal.normalized : Vector2.zero;
        float velocityTowardsTarget = Vector2.Dot(currentVelocityLocal, desiredDirection);
        float currentSpeedMagnitude = currentVelocityLocal.magnitude;
        
        // Если скорость направлена ОТ цели (отрицательная проекция) И скорость значительная (> 5 м/с)
        // ИЛИ если ошибка очень большая (> 20 м/с)
        // сначала компенсируем эту скорость
        float errorMagnitude = velocityError.magnitude;
        bool movingAwayFromTarget = velocityTowardsTarget < -2f && currentSpeedMagnitude > 5f;
        bool errorTooLarge = errorMagnitude > 20f;
        
        if (movingAwayFromTarget || errorTooLarge)
        {
            // Компенсируем скорость, направленную от цели
            // Направляем двигатели ПРОТИВ текущей скорости
            Vector2 compensationDirection = -currentVelocityLocal.normalized;
            float compensationStrength = Mathf.Min(1f, Mathf.Max(currentSpeedMagnitude / 30f, errorMagnitude / 50f));
            shipController.SetMovementDirection(compensationDirection * compensationStrength);
            
            if (showDebugInfo && Time.frameCount % 60 == 0)
            {
                string reason = movingAwayFromTarget ? "СКОРОСТЬ НАПРАВЛЕНА ОТ ЦЕЛИ" : "ОШИБКА СЛИШКОМ БОЛЬШАЯ";
                Debug.LogWarning($"LandingAutopilot: ⚠️ {reason}! " +
                               $"Скорость к цели: {velocityTowardsTarget:F2}м/с, " +
                               $"Текущая скорость: ({currentVelocityLocal.x:F2}, {currentVelocityLocal.y:F2}) м/с (Magnitude: {currentSpeedMagnitude:F2}м/с), " +
                               $"Ошибка: {errorMagnitude:F1}м/с, " +
                               $"Компенсация: {compensationDirection * compensationStrength}");
            }
            return;
        }
        
        // Используем отдельные PID контроллеры для каждой оси
        // correctionX - коррекция для оси X (влево/вправо)
        float correctionX = horizontalSpeedPIDX.Update(desiredVelocityLocal.x, currentVelocityLocal.x, Time.deltaTime);
        // correctionZ - коррекция для оси Z (вперед/назад), но хранится в y Vector2
        float correctionZ = horizontalSpeedPIDZ.Update(desiredVelocityLocal.y, currentVelocityLocal.y, Time.deltaTime);
        
        // Преобразуем коррекцию в направление для SetMovementDirection
        // PID уже возвращает значение в диапазоне [-1, 1] благодаря SetOutputLimits
        // movementDirection.x = влево/вправо (локальный X)
        // movementDirection.y = вперед/назад (локальный Z, НЕ Unity Y!)
        Vector2 movementDirection = new Vector2(correctionX, correctionZ);
        
        // Ограничиваем общую силу (если нужно)
        if (movementDirection.magnitude > 1f)
        {
            movementDirection = movementDirection.normalized;
        }
        
        shipController.SetMovementDirection(movementDirection);
        
        // Логирование
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"LandingAutopilot: === УПРАВЛЕНИЕ ПО СКОРОСТИ ===");
            Debug.Log($"  Горизонтальное расстояние: {horizontalDistance:F1}м");
            Debug.Log($"  МИРОВЫЕ КООРДИНАТЫ (Unity):");
            Debug.Log($"    worldHorizontalDelta: X={worldHorizontalDelta.x:F2} (влево/вправо), Y={worldHorizontalDelta.y:F2} (вверх/вниз), Z={worldHorizontalDelta.z:F2} (вперед/назад)");
            Debug.Log($"    shipVelocity: X={shipVelocity.x:F2} (влево/вправо), Y={shipVelocity.y:F2} (вверх/вниз), Z={shipVelocity.z:F2} (вперед/назад)");
            Debug.Log($"  ЛОКАЛЬНЫЕ КООРДИНАТЫ КОРАБЛЯ:");
            Debug.Log($"    localHorizontalDelta: X={localHorizontalDelta.x:F2} (влево/вправо), Y={localHorizontalDelta.y:F2} (вверх/вниз), Z={localHorizontalDelta.z:F2} (вперед/назад)");
            Debug.Log($"    localVelocity3D: X={localVelocity3D.x:F2} (влево/вправо), Y={localVelocity3D.y:F2} (вверх/вниз), Z={localVelocity3D.z:F2} (вперед/назад)");
            Debug.Log($"  ВАЖНО: Проверка что мы НЕ используем Y (вертикальную ось):");
            Debug.Log($"    localHorizontalDelta.y должно быть 0: {localHorizontalDelta.y:F3} {(Mathf.Abs(localHorizontalDelta.y) < 0.001f ? "✓" : "✗ ОШИБКА!")}");
            Debug.Log($"    localVelocity3D.y должно быть 0: {localVelocity3D.y:F3} {(Mathf.Abs(localVelocity3D.y) < 0.001f ? "✓" : "✗ ОШИБКА!")}");
            Debug.Log($"  СКОРОСТИ (для SetMovementDirection, Vector2):");
            Debug.Log($"    desiredVelocityLocal: x={desiredVelocityLocal.x:F2} (локальный X=влево/вправо), y={desiredVelocityLocal.y:F2} (локальный Z=вперед/назад, НЕ Unity Y!)");
            Debug.Log($"    currentVelocityLocal: x={currentVelocityLocal.x:F2} (локальный X=влево/вправо), y={currentVelocityLocal.y:F2} (локальный Z=вперед/назад, НЕ Unity Y!)");
            Debug.Log($"    Ошибка: x={velocityError.x:F2}, y={velocityError.y:F2} м/с, Magnitude={errorMagnitude:F2}м/с");
            Debug.Log($"  PID КОРРЕКЦИЯ:");
            Debug.Log($"    correctionX={correctionX:F3} (для оси X=влево/вправо)");
            Debug.Log($"    correctionZ={correctionZ:F3} (для оси Z=вперед/назад)");
            Debug.Log($"  НАПРАВЛЕНИЕ ДВИГАТЕЛЕЙ (SetMovementDirection):");
            Debug.Log($"    movementDirection.x={movementDirection.x:F3} → desiredMovementDirection.x (влево/вправо) → targetAngleY (поворот влево/вправо)");
            Debug.Log($"    movementDirection.y={movementDirection.y:F3} → desiredMovementDirection.y (вперед/назад) → targetAngleX (наклон вперед/назад)");
            Debug.Log($"  ПРОВЕРКА НАПРАВЛЕНИЯ:");
            Debug.Log($"    Проекция текущей скорости на направление к цели: {velocityTowardsTarget:F2}м/с");
            Debug.Log($"      Если > 0 → движемся К цели, если < 0 → движемся ОТ цели");
            Debug.Log($"    Текущая скорость (Magnitude): {currentSpeedMagnitude:F2}м/с");
            Debug.Log($"    Ошибка скорости (Magnitude): {errorMagnitude:F2}м/с");
            Debug.Log($"  ПРОВЕРКА ЗНАКОВ:");
            Debug.Log($"    Если localHorizontalDelta.z > 0 (цель впереди) → desiredVelocityLocal.y > 0 → correctionZ > 0 → movementDirection.y > 0");
            Debug.Log($"    Если localHorizontalDelta.z < 0 (цель сзади) → desiredVelocityLocal.y < 0 → correctionZ < 0 → movementDirection.y < 0");
            Debug.Log($"    Фактически: localHorizontalDelta.z={localHorizontalDelta.z:F3}, desiredVelocityLocal.y={desiredVelocityLocal.y:F3}, movementDirection.y={movementDirection.y:F3}");
            Debug.Log($"  ПРОВЕРКА ДВИЖЕНИЯ:");
            Debug.Log($"    Движемся от цели: {movingAwayFromTarget}, Ошибка слишком большая: {errorTooLarge}");
            if (movingAwayFromTarget || errorTooLarge)
            {
                Debug.LogWarning($"    ⚠️ КОМПЕНСАЦИЯ АКТИВНА - сначала останавливаем движение от цели!");
            }
        }
        
        
    }
    
    /// <summary>
    /// Выравнивание корабля по нормали поверхности
    /// </summary>
    private void AlignToSurfaceNormal(Vector3 surfaceNormal)
    {
        // Вычисляем желаемую ориентацию: корабль должен быть ориентирован так,
        // чтобы его "вверх" совпадал с нормалью поверхности
        Vector3 desiredUp = surfaceNormal.normalized;
        Transform shipTransform = shipController.transform;
        Vector3 currentUp = shipTransform.up;
        
        // Вычисляем угол между текущей ориентацией и желаемой
        float angleError = Vector3.Angle(currentUp, desiredUp);
        
        if (angleError < maxOrientationAngle)
        {
            return; // Уже достаточно выровнен
        }
        
        // Вычисляем ось вращения
        Vector3 rotationAxis = Vector3.Cross(currentUp, desiredUp);
        if (rotationAxis.magnitude < 0.001f)
        {
            return; // Уже выровнен или противоположно направлен
        }
        rotationAxis.Normalize();
        
        // Вычисляем целевой поворот
        Quaternion targetRotation = Quaternion.FromToRotation(currentUp, desiredUp) * shipTransform.rotation;
        
        // Плавное выравнивание
        shipTransform.rotation = Quaternion.Slerp(shipTransform.rotation, targetRotation, orientationSmoothing * Time.deltaTime);
    }
    
    // ========== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ==========
    
    /// <summary>
    /// Рассчитывает минимальную тягу для компенсации гравитации
    /// </summary>
    private float CalculateHoverThrust()
    {
        float mass = shipController.GetMass();
        float gravityStrength = shipController.GetGravityStrength();
        float maxThrustForce = shipController.GetMaxThrustForce();
        int engineCount = shipController.GetEngineCount();
        
        float weight = mass * gravityStrength;
        float totalMaxThrust = maxThrustForce * engineCount;
        
        if (totalMaxThrust <= 0f) return 0f;
        
        // Минимальная тяга для компенсации гравитации + небольшой запас
        float hoverThrust = Mathf.Clamp01((weight / totalMaxThrust) + 0.05f);
        
        return hoverThrust;
    }
    
    /// <summary>
    /// Выбирает лучшую посадочную площадку
    /// </summary>
    private LandingSite SelectBestLandingSite(List<LandingSite> sites)
    {
        if (sites == null || sites.Count == 0) return null;
        
        Vector3 shipPosition = shipController.transform.position;
        
        // Выбираем лучшую площадку (приоритет: пригодность, затем расстояние)
        LandingSite bestSite = sites
            .OrderByDescending(site => site.suitabilityScore)
            .ThenBy(site => Vector3.Distance(shipPosition, site.position))
            .First();
        
        return bestSite;
    }
    
    /// <summary>
    /// Проверяет, доступна ли площадка
    /// </summary>
    private bool IsSiteAvailable(LandingSite site)
    {
        if (site == null) return false;
        
        // Проверяем, есть ли площадка в текущем списке радара
        List<LandingSite> sites = landingRadar != null ? landingRadar.GetFoundSites() : null;
        if (sites == null || sites.Count == 0) return false;
        
        // Проверяем, есть ли эта площадка в списке (по позиции)
        return sites.Any(s => Vector3.Distance(s.position, site.position) < 1f);
    }
    
    /// <summary>
    /// Пытается переключиться на новую площадку (если текущая недоступна)
    /// </summary>
    private void TrySwitchToNewSite()
    {
        List<LandingSite> sites = landingRadar != null ? landingRadar.GetFoundSites() : null;
        if (sites == null || sites.Count == 0)
        {
            // Нет доступных площадок - возвращаемся к ожиданию
            currentPhase = LandingPhase.WaitingForSite;
            targetSite = null;
            OnLandingPhaseChanged?.Invoke(currentPhase);
            if (showDebugInfo)
            {
                Debug.LogWarning("LandingAutopilot: Текущая площадка недоступна, ожидаю новые точки...");
            }
            return;
        }
        
        // Выбираем новую лучшую площадку
        LandingSite newSite = SelectBestLandingSite(sites);
        if (newSite != null)
        {
            targetSite = newSite;
            if (showDebugInfo)
            {
                Debug.Log($"LandingAutopilot: Переключился на новую площадку (расстояние: {Vector3.Distance(shipController.transform.position, newSite.position):F1}м)");
            }
        }
    }
}
