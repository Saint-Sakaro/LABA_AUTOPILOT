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
    [SerializeField] private float alignmentStartHeight = 150f; // Высота, с которой начинаем выравнивание по нормали поверхности (м)
    
    [Header("Thrust Smoothing")]
    [SerializeField] private float thrustChangeRate = 2f; // Максимальная скорость изменения тяги (в секунду, 0-1)
    [SerializeField] private float rotationStabilizationStrength = 0.3f; // Сила стабилизации rotation через дифференциальную тягу (0-1)
    
    [Header("PID Controllers")]
    [SerializeField] private PIDController verticalSpeedPID = new PIDController(0.5f, 0.05f, 0.2f);
    [SerializeField] private PIDController horizontalSpeedPIDX = new PIDController(0.3f, 0.02f, 0.15f); // Для оси X (влево/вправо)
    [SerializeField] private PIDController horizontalSpeedPIDZ = new PIDController(0.3f, 0.02f, 0.15f); // Для оси Z (вперед/назад)
    [SerializeField] private PIDController orientationPID = new PIDController(2f, 0.1f, 0.5f); // Для выравнивания по нормали поверхности
    [SerializeField] private PIDController rollStabilizationPID = new PIDController(1.5f, 0.05f, 0.3f); // Для стабилизации крена (roll)
    [SerializeField] private PIDController yawStabilizationPID = new PIDController(1.5f, 0.05f, 0.3f); // Для стабилизации рыскания (yaw)
    [SerializeField] private PIDController pitchStabilizationPID = new PIDController(1.5f, 0.05f, 0.3f); // Для стабилизации тангажа (pitch)
    
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
    
    // Плавное изменение тяги
    private float currentThrust = 0f; // Текущая тяга (для плавного изменения)
    private float[] currentEngineThrusts = new float[4]; // Текущая тяга каждого двигателя (для плавного изменения)
    private bool isAligningToSurface = false; // Флаг, что идет выравнивание по нормали поверхности
    
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
        rollStabilizationPID.SetOutputLimits(-1f, 1f);
        yawStabilizationPID.SetOutputLimits(-1f, 1f);
        pitchStabilizationPID.SetOutputLimits(-1f, 1f);
        
        verticalSpeedPID.Reset();
        horizontalSpeedPIDX.Reset();
        horizontalSpeedPIDZ.Reset();
        orientationPID.Reset();
        rollStabilizationPID.Reset();
        yawStabilizationPID.Reset();
        pitchStabilizationPID.Reset();
    }
    
    private void Update()
    {
        if (!isActive) return;
        
        // Обновляем автопилот в зависимости от текущей фазы
        // (ControlFallSpeed обновит currentThrust)
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
        
        // Инициализируем тягу в зависимости от текущей скорости
        // Если корабль летит вверх, устанавливаем минимальную тягу (0)
        // Если корабль падает вниз, устанавливаем hoverThrust для компенсации гравитации
        // (используем уже определенные выше initialVelocity и initialVerticalSpeed)
        if (initialVerticalSpeed > 0f)
        {
            // Корабль летит вверх - устанавливаем минимальную тягу (0), чтобы позволить ему падать
            currentThrust = 0f;
        }
        else
        {
            // Корабль падает вниз - устанавливаем hoverThrust для компенсации гравитации
            float hoverThrust = CalculateHoverThrust();
            currentThrust = hoverThrust;
        }
        
        // Инициализируем тягу всех двигателей и применяем её
        int engineCount = shipController.GetEngineCount();
        for (int i = 0; i < engineCount && i < 4; i++)
        {
            currentEngineThrusts[i] = currentThrust;
            shipController.SetEngineThrust(i, currentThrust); // Применяем тягу к двигателю
        }
        
        // Если двигателей больше 4, устанавливаем тягу для остальных
        for (int i = 4; i < engineCount; i++)
        {
            shipController.SetEngineThrust(i, currentThrust);
        }
        
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
        
        // Стабилизация rotation через управление отдельными двигателями (после обновления базовой тяги)
        StabilizeRotation();
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
        
        // Начинаем выравнивание по нормали поверхности, если близко к земле
        if (verticalDistance <= alignmentStartHeight)
        {
            AlignToSurfaceNormal(targetSite.surfaceNormal);
        }
        
        // Управление: контроль скорости падения (на основе высоты) + движение к цели
        // НЕ передаем параметр, чтобы использовалась правильная скорость на основе высоты (10 м/с на 300м, плавно до 5 м/с на 100м)
        ControlFallSpeed();
        MoveTowardsTarget(targetPosition, brakingSpeed);
        
        // Стабилизация rotation через управление отдельными двигателями (после обновления базовой тяги)
        StabilizeRotation();
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
        
        // Стабилизация rotation через управление отдельными двигателями (после обновления базовой тяги)
        StabilizeRotation();
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
        
        // Если корабль летит вверх (положительная скорость), нужно уменьшить тягу
        // Если корабль падает вниз (отрицательная скорость), нужно увеличить тягу
        float targetThrust;
        
        if (currentVerticalSpeed > 0f)
        {
            // Корабль летит вверх - нужно его остановить
            // Устанавливаем тягу 0 и полагаемся на гравитацию
            // Гравитация будет замедлять корабль и в конечном итоге остановит его
            targetThrust = 0f;
        }
        else
        {
            // Корабль падает вниз - нормальная логика
            targetThrust = Mathf.Clamp01(hoverThrust + verticalCorrection * correctionMultiplier);
            
            // Если скорость падения критически высокая, используем максимальную тягу
            if (currentVerticalSpeed < -targetMaxSpeed * 1.5f)
            {
                targetThrust = 1f; // 100% тяги для экстренного торможения
            }
        }
        
        // Плавное изменение тяги (не резко!)
        float maxThrustChange = thrustChangeRate * Time.deltaTime;
        currentThrust = Mathf.MoveTowards(currentThrust, targetThrust, maxThrustChange);
        // НЕ вызываем SetThrust здесь, чтобы не перезаписать индивидуальные тяги двигателей
        // StabilizeRotation будет использовать currentThrust как базовую тягу и устанавливать индивидуальные тяги через SetEngineThrust
        
        // Логирование
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"LandingAutopilot: === КОНТРОЛЬ СКОРОСТИ ПАДЕНИЯ ===");
            Debug.Log($"  Высота до цели: {verticalDistance:F1}м");
            Debug.Log($"  Текущая скорость падения: {currentVerticalSpeed:F2}м/с");
            Debug.Log($"  Целевая максимальная скорость: {targetMaxSpeed:F2}м/с");
            Debug.Log($"  Целевая скорость (отрицательная): {targetVerticalSpeed:F2}м/с");
            Debug.Log($"  Ошибка скорости: {verticalSpeedError:F2}м/с");
            Debug.Log($"  Тяга: {currentThrust:F2}");
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
    /// Выравнивание корабля по нормали поверхности (как самолет)
    /// Использует управление двигателями для выравнивания, а не прямое изменение rotation
    /// </summary>
    private void AlignToSurfaceNormal(Vector3 surfaceNormal)
    {
        isAligningToSurface = true; // Устанавливаем флаг, что идет выравнивание
        
        Transform shipTransform = shipController.transform;
        Vector3 desiredUp = surfaceNormal.normalized;
        Vector3 currentUp = shipTransform.up;
        
        // Вычисляем угол между текущей ориентацией и желаемой
        float angleError = Vector3.Angle(currentUp, desiredUp);
        
        if (angleError < maxOrientationAngle)
        {
            isAligningToSurface = false; // Выравнивание завершено
            return; // Уже достаточно выровнен
        }
        
        // Вычисляем ось вращения (в локальных координатах корабля)
        Vector3 rotationAxis = Vector3.Cross(currentUp, desiredUp);
        if (rotationAxis.magnitude < 0.001f)
        {
            return; // Уже выровнен или противоположно направлен
        }
        rotationAxis.Normalize();
        
        // Преобразуем ось вращения в локальные координаты корабля
        Vector3 localRotationAxis = shipTransform.InverseTransformDirection(rotationAxis);
        
        // Вычисляем угловую скорость для выравнивания
        Vector3 angularVelocity = shipController.GetAngularVelocity();
        Vector3 localAngularVelocity = shipTransform.InverseTransformDirection(angularVelocity);
        
        // Вычисляем желаемую угловую скорость для выравнивания
        // Используем PID для плавного выравнивания
        float targetAngularSpeed = angleError * orientationSmoothing * 0.01f; // Пропорционально углу ошибки
        targetAngularSpeed = Mathf.Clamp(targetAngularSpeed, -orientationSmoothing, orientationSmoothing);
        
        // Вычисляем коррекцию для каждой оси (локальные координаты)
        // X - крен (roll), Y - рыскание (yaw), Z - тангаж (pitch)
        float rollCorrection = 0f;   // Вращение вокруг локальной оси X (forward)
        float yawCorrection = 0f;    // Вращение вокруг локальной оси Y (up)
        float pitchCorrection = 0f;  // Вращение вокруг локальной оси Z (right)
        
        // Проектируем ось вращения на локальные оси
        rollCorrection = -localRotationAxis.x * targetAngularSpeed;   // Крен
        yawCorrection = -localRotationAxis.y * targetAngularSpeed;    // Рыскание
        pitchCorrection = -localRotationAxis.z * targetAngularSpeed;  // Тангаж
        
        // Используем PID для плавного выравнивания
        float rollPID = orientationPID.Update(0f, localAngularVelocity.x, Time.deltaTime);
        float yawPID = orientationPID.Update(0f, localAngularVelocity.y, Time.deltaTime);
        float pitchPID = orientationPID.Update(0f, localAngularVelocity.z, Time.deltaTime);
        
        // Комбинируем коррекцию с PID
        rollCorrection += rollPID;
        yawCorrection += yawPID;
        pitchCorrection += pitchPID;
        
        // Ограничиваем коррекцию
        rollCorrection = Mathf.Clamp(rollCorrection, -1f, 1f);
        yawCorrection = Mathf.Clamp(yawCorrection, -1f, 1f);
        pitchCorrection = Mathf.Clamp(pitchCorrection, -1f, 1f);
        
        // Применяем коррекцию через управление двигателями
        // Для крена: разная тяга левых/правых двигателей
        // Для рыскания: поворот двигателей влево/вправо
        // Для тангажа: поворот двигателей вперед/назад
        
        int engineCount = shipController.GetEngineCount();
        if (engineCount >= 4)
        {
            // Предполагаем расположение: 0=левый передний, 1=правый передний, 2=левый задний, 3=правый задний
            // Крен (roll): увеличиваем тягу левых двигателей, уменьшаем правых
            // Тангаж (pitch): увеличиваем тягу передних двигателей, уменьшаем задних
            float baseThrust = currentThrust; // Используем текущую тягу как базовую
            
            // Если базовая тяга 0, не добавляем коррекцию (чтобы двигатели оставались на 0)
            // Коррекция нужна только для стабилизации, а не для создания подъемной силы
            float[] targetEngineThrusts = new float[4];
            if (baseThrust <= 0.001f)
            {
                // Базовая тяга 0 - устанавливаем все двигатели на 0
                for (int i = 0; i < 4; i++)
                {
                    targetEngineThrusts[i] = 0f;
                }
            }
            else
            {
                // Базовая тяга не 0 - добавляем коррекцию для стабилизации
                targetEngineThrusts[0] = baseThrust + rollCorrection * 0.2f - pitchCorrection * 0.2f; // Левый передний: +roll, -pitch
                targetEngineThrusts[1] = baseThrust - rollCorrection * 0.2f - pitchCorrection * 0.2f; // Правый передний: -roll, -pitch
                targetEngineThrusts[2] = baseThrust + rollCorrection * 0.2f + pitchCorrection * 0.2f; // Левый задний: +roll, +pitch
                targetEngineThrusts[3] = baseThrust - rollCorrection * 0.2f + pitchCorrection * 0.2f; // Правый задний: -roll, +pitch
            }
            
            // Плавное изменение тяги каждого двигателя (не резко!)
            float maxThrustChange = thrustChangeRate * Time.deltaTime;
            for (int i = 0; i < 4; i++)
            {
                targetEngineThrusts[i] = Mathf.Clamp01(targetEngineThrusts[i]);
                currentEngineThrusts[i] = Mathf.MoveTowards(currentEngineThrusts[i], targetEngineThrusts[i], maxThrustChange);
                shipController.SetEngineThrust(i, currentEngineThrusts[i]);
            }
        }
        
        // Рыскание и тангаж через поворот двигателей
        Vector2 movementDirection = shipController.GetMovementDirection();
        movementDirection.x += yawCorrection * 0.3f;   // Рыскание (поворот влево/вправо)
        movementDirection.y += pitchCorrection * 0.3f; // Тангаж (наклон вперед/назад)
        movementDirection.x = Mathf.Clamp(movementDirection.x, -1f, 1f);
        movementDirection.y = Mathf.Clamp(movementDirection.y, -1f, 1f);
        shipController.SetMovementDirection(movementDirection);
        
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"AlignToSurfaceNormal: angleError={angleError:F1}°, roll={rollCorrection:F2}, yaw={yawCorrection:F2}, pitch={pitchCorrection:F2}");
        }
        
        isAligningToSurface = false; // Сбрасываем флаг в конце метода
    }
    
    // ========== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ==========
    
    /// <summary>
    /// Стабилизация rotation через управление отдельными двигателями
    /// Использует дифференциальную тягу для компенсации крена/тангажа/рыскания
    /// НЕ работает когда идет выравнивание по нормали поверхности (AlignToSurfaceNormal имеет приоритет)
    /// </summary>
    private void StabilizeRotation()
    {
        // Если идет выравнивание по нормали поверхности, не стабилизируем rotation отдельно
        // (AlignToSurfaceNormal уже управляет двигателями)
        if (isAligningToSurface)
        {
            return;
        }
        
        Vector3 angularVelocity = shipController.GetAngularVelocity();
        Transform shipTransform = shipController.transform;
        
        // Преобразуем угловую скорость в локальные координаты
        Vector3 localAngularVelocity = shipTransform.InverseTransformDirection(angularVelocity);
        
        // Если угловая скорость очень мала, не стабилизируем (экономия ресурсов)
        if (localAngularVelocity.magnitude < 0.01f)
        {
            return;
        }
        
        // Используем отдельные PID для каждой оси rotation
        float rollCorrection = rollStabilizationPID.Update(0f, localAngularVelocity.x, Time.deltaTime);
        float yawCorrection = yawStabilizationPID.Update(0f, localAngularVelocity.y, Time.deltaTime);
        float pitchCorrection = pitchStabilizationPID.Update(0f, localAngularVelocity.z, Time.deltaTime);
        
        // Ограничиваем коррекцию
        rollCorrection = Mathf.Clamp(rollCorrection * rotationStabilizationStrength, -0.3f, 0.3f);
        yawCorrection = Mathf.Clamp(yawCorrection * rotationStabilizationStrength, -0.3f, 0.3f);
        pitchCorrection = Mathf.Clamp(pitchCorrection * rotationStabilizationStrength, -0.3f, 0.3f);
        
        int engineCount = shipController.GetEngineCount();
        if (engineCount >= 4)
        {
            // Предполагаем расположение: 0=левый передний, 1=правый передний, 2=левый задний, 3=правый задний
            // Крен (roll): увеличиваем тягу левых двигателей, уменьшаем правых
            // Тангаж (pitch): увеличиваем тягу передних двигателей, уменьшаем задних
            // Рыскание (yaw): поворот двигателей влево/вправо
            
            float baseThrust = currentThrust; // Используем текущую тягу как базовую
            
            // Вычисляем целевую тягу для каждого двигателя
            // Если базовая тяга 0, не добавляем коррекцию (чтобы двигатели оставались на 0)
            // Коррекция нужна только для стабилизации, а не для создания подъемной силы
            float[] targetEngineThrusts = new float[4];
            if (baseThrust <= 0.001f)
            {
                // Базовая тяга 0 - устанавливаем все двигатели на 0
                for (int i = 0; i < 4; i++)
                {
                    targetEngineThrusts[i] = 0f;
                }
            }
            else
            {
                // Базовая тяга не 0 - добавляем коррекцию для стабилизации rotation
                targetEngineThrusts[0] = baseThrust + rollCorrection - pitchCorrection; // Левый передний: +roll, -pitch
                targetEngineThrusts[1] = baseThrust - rollCorrection - pitchCorrection; // Правый передний: -roll, -pitch
                targetEngineThrusts[2] = baseThrust + rollCorrection + pitchCorrection; // Левый задний: +roll, +pitch
                targetEngineThrusts[3] = baseThrust - rollCorrection + pitchCorrection; // Правый задний: -roll, +pitch
            }
            
            // Плавное изменение тяги каждого двигателя (не резко!)
            float maxThrustChange = thrustChangeRate * Time.deltaTime;
            for (int i = 0; i < engineCount && i < 4; i++)
            {
                targetEngineThrusts[i] = Mathf.Clamp01(targetEngineThrusts[i]);
                currentEngineThrusts[i] = Mathf.MoveTowards(currentEngineThrusts[i], targetEngineThrusts[i], maxThrustChange);
                shipController.SetEngineThrust(i, currentEngineThrusts[i]);
            }
        }
        else
        {
            // Если двигателей меньше 4, устанавливаем базовую тягу для всех
            float maxThrustChange = thrustChangeRate * Time.deltaTime;
            for (int i = 0; i < engineCount; i++)
            {
                currentEngineThrusts[i] = Mathf.MoveTowards(currentEngineThrusts[i], currentThrust, maxThrustChange);
                shipController.SetEngineThrust(i, currentEngineThrusts[i]);
            }
        }
        
        // Рыскание через поворот двигателей
        Vector2 movementDirection = shipController.GetMovementDirection();
        movementDirection.x += yawCorrection; // Рыскание (поворот влево/вправо)
        movementDirection.x = Mathf.Clamp(movementDirection.x, -1f, 1f);
        shipController.SetMovementDirection(movementDirection);
        
        if (showDebugInfo && Time.frameCount % 60 == 0 && (Mathf.Abs(rollCorrection) > 0.01f || Mathf.Abs(yawCorrection) > 0.01f || Mathf.Abs(pitchCorrection) > 0.01f))
        {
            Debug.Log($"StabilizeRotation: roll={rollCorrection:F3}, yaw={yawCorrection:F3}, pitch={pitchCorrection:F3}, angularVel={localAngularVelocity}");
        }
    }
    
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
