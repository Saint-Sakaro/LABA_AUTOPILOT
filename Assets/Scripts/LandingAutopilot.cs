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
    
    [Header("Direct Torque Stabilization")]
    [SerializeField] private bool useDirectTorqueStabilization = false;
    [SerializeField] private float directTorqueStrength = 2000f;
    [SerializeField] private float directTorqueDamping = 200f;
    
    [Header("Geometry Thrust Stabilization")]
    [SerializeField] private bool useGeometryThrustStabilization = false;
    [SerializeField] private float geometryTorqueStrength = 2000f;
    [SerializeField] private float geometryTorqueDamping = 200f;
    [SerializeField] private float maxGeometryThrustDelta = 0.25f;
    
    [Header("Thrust Smoothing")]
    [SerializeField] private float thrustChangeRate = 2f; // Максимальная скорость изменения тяги (в секунду, 0-1)
    [SerializeField] private float rotationStabilizationStrength = 1.0f; // Сила стабилизации rotation через дифференциальную тягу (0-1) - установлено в 1.0 для максимальной эффективности выравнивания к (0,0,0)
    
    [Header("PID Controllers")]
    [SerializeField] private PIDController verticalSpeedPID = new PIDController(0.5f, 0.05f, 0.2f);
    [SerializeField] private PIDController horizontalSpeedPIDX = new PIDController(0.3f, 0.02f, 0.15f); // Для оси X (влево/вправо)
    [SerializeField] private PIDController horizontalSpeedPIDZ = new PIDController(0.3f, 0.02f, 0.15f); // Для оси Z (вперед/назад)
    [SerializeField] private PIDController orientationPID = new PIDController(2f, 0.1f, 0.5f); // Для выравнивания по нормали поверхности
    [SerializeField] private PIDController rollStabilizationPID = new PIDController(2.0f, 0.1f, 0.4f); // Для стабилизации крена (roll) - увеличено для лучшей стабилизации
    [SerializeField] private PIDController yawStabilizationPID = new PIDController(2.0f, 0.1f, 0.4f); // Для стабилизации рыскания (yaw) - увеличено для лучшей стабилизации
    [SerializeField] private PIDController pitchStabilizationPID = new PIDController(4.0f, 0.2f, 0.8f); // Для стабилизации тангажа (pitch) - значительно увеличено для точного выравнивания к (0,0,0)
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool showRotationDebug = false;
    [SerializeField] private int rotationDebugFrameInterval = 30;
    [SerializeField] private bool invertRotationCorrection = false;
    [SerializeField] private bool swapRollPitchAxes = false;
    
    [Header("Rotation vs Movement")]
    [SerializeField] private bool dampMovementWhenUnstable = true;
    [SerializeField] private float pauseMovementAboveAngle = 6f;
    [SerializeField] private float resumeMovementBelowAngle = 3f;
    [SerializeField] private float minMovementScaleWhenUnstable = 0.2f;
    
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
    private bool movementPausedForRotation = false;
    private float movementDampingFactor = 1f;
    
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
        
        // Проверяем alignmentStartHeight (должен быть примерно равен brakingStartHeight или выше)
        if (alignmentStartHeight < 100f || alignmentStartHeight > 500f)
        {
            Debug.LogWarning($"LandingAutopilot: alignmentStartHeight имеет неправильное значение ({alignmentStartHeight}м). Устанавливаю правильное значение: 300м");
            alignmentStartHeight = 300f;
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
        rotationDebugFrameInterval = Mathf.Max(1, rotationDebugFrameInterval);
        pauseMovementAboveAngle = Mathf.Max(0.1f, pauseMovementAboveAngle);
        resumeMovementBelowAngle = Mathf.Clamp(resumeMovementBelowAngle, 0.05f, pauseMovementAboveAngle);
        minMovementScaleWhenUnstable = Mathf.Clamp01(minMovementScaleWhenUnstable);
        
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
        EnsureEngineThrustArray();
        for (int i = 0; i < engineCount; i++)
        {
            currentEngineThrusts[i] = currentThrust;
            shipController.SetEngineThrust(i, currentThrust); // Применяем тягу к двигателю
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
        ApplyMovementPauseIfNeeded();
        
        // Выравнивание по нормали поверхности, если близко к земле (с высоты alignmentStartHeight)
        // Вызываем ПОСЛЕ MoveTowardsTarget, чтобы коррекции выравнивания добавлялись к направлению движения
        float verticalDistance = shipPosition.y - targetPosition.y;
        if (verticalDistance <= alignmentStartHeight && targetSite != null)
        {
            if (showDebugInfo && Time.frameCount % 60 == 0)
            {
                Debug.Log($"LandingAutopilot: Выравнивание по нормали поверхности включено (высота: {verticalDistance:F1}м, alignmentStartHeight: {alignmentStartHeight:F1}м)");
            }
            AlignToSurfaceNormal(targetSite.surfaceNormal);
        }
        else if (showDebugInfo && Time.frameCount % 60 == 0 && targetSite != null)
        {
            Debug.Log($"LandingAutopilot: Выравнивание НЕ включено (высота: {verticalDistance:F1}м > alignmentStartHeight: {alignmentStartHeight:F1}м)");
        }
        
        // ВРЕМЕННО: Стабилизация rotation к вертикали (0, 0, 0), игнорируя нормаль поверхности
        StabilizeRotation(Vector3.up);
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
        ApplyMovementPauseIfNeeded();
        
        // ВРЕМЕННО: Стабилизация rotation к вертикали (0, 0, 0), игнорируя нормаль поверхности
        StabilizeRotation(Vector3.up);
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
        ApplyMovementPauseIfNeeded();
        
        // ВРЕМЕННО: Стабилизация rotation к вертикали (0, 0, 0), игнорируя нормаль поверхности
        StabilizeRotation(Vector3.up);
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
        
        // Вычисляем целевую скорость и ошибку
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
        
        // ЛОГИКА УПРАВЛЕНИЯ ТЯГОЙ:
        // - Если currentVerticalSpeed > targetVerticalSpeed (падаем МЕДЛЕННЕЕ, чем нужно) → УМЕНЬШАЕМ тягу
        // - Если currentVerticalSpeed < targetVerticalSpeed (падаем БЫСТРЕЕ, чем нужно) → УВЕЛИЧИВАЕМ тягу
        // - Если currentVerticalSpeed > 0 (летим ВВЕРХ) → УСТАНАВЛИВАЕМ тягу 0
        float targetThrust;
        
        if (currentVerticalSpeed > 0f)
        {
            // Корабль летит вверх - нужно его остановить
            // Устанавливаем тягу 0 и полагаемся на гравитацию
            targetThrust = 0f;
        }
        else
        {
            // Корабль падает вниз
            // ЛОГИКА:
            // - Если currentVerticalSpeed > targetVerticalSpeed (падаем МЕДЛЕННЕЕ, чем нужно) → УМЕНЬШАЕМ тягу
            // - Если currentVerticalSpeed < targetVerticalSpeed (падаем БЫСТРЕЕ, чем нужно) → УВЕЛИЧИВАЕМ тягу
            
            // verticalCorrection от PID: положительная = нужно увеличить тягу (тормозить), отрицательная = нужно уменьшить тягу (ускорить падение)
            // Но для правильной работы нужно учесть знак ошибки:
            // - Если падаем медленнее (currentVerticalSpeed > targetVerticalSpeed), ошибка отрицательная, нужно УМЕНЬШАТЬ тягу
            // - Если падаем быстрее (currentVerticalSpeed < targetVerticalSpeed), ошибка положительная, нужно УВЕЛИЧИВАТЬ тягу
            
            // Проверяем, падаем ли мы медленнее или быстрее целевой скорости
            if (currentVerticalSpeed > targetVerticalSpeed)
            {
                // Падаем МЕДЛЕННЕЕ целевой скорости - нужно УМЕНЬШАТЬ тягу до минимума, чтобы ускорить падение
                // Чем больше разница, тем меньше должна быть тяга (вплоть до 0)
                float speedDifference = currentVerticalSpeed - targetVerticalSpeed; // Положительная разница (например, -5.57 - (-100) = 94.43)
                float normalizedDifference = Mathf.Clamp01(speedDifference / targetMaxSpeed); // Нормализуем (0-1)
                
                // Если разница большая (падаем намного медленнее), устанавливаем тягу близко к 0
                // Если разница маленькая, можем оставить небольшую тягу для стабилизации
                if (normalizedDifference > 0.5f)
                {
                    // Большая разница - устанавливаем тягу очень маленькую (0-10% от hover)
                    targetThrust = hoverThrust * (1f - normalizedDifference) * 0.2f; // От 0 до 20% от hover
                }
                else
                {
                    // Малая разница - уменьшаем тягу пропорционально
                    targetThrust = hoverThrust * (1f - normalizedDifference * 0.5f); // От 50% до 100% от hover
                }
                
                // Минимальная тяга - 0 (не можем иметь отрицательную)
                targetThrust = Mathf.Max(0f, targetThrust);
            }
            else
            {
                // Падаем БЫСТРЕЕ целевой скорости - нужно УВЕЛИЧИВАТЬ тягу, чтобы замедлить падение
                // verticalCorrection уже положительная (нужно увеличить тягу)
                targetThrust = Mathf.Clamp01(hoverThrust + verticalCorrection * correctionMultiplier);
                
                // Если скорость падения критически высокая (падаем слишком быстро), используем максимальную тягу
                if (currentVerticalSpeed < -targetMaxSpeed * 1.5f)
                {
                    targetThrust = 1f; // 100% тяги для экстренного торможения
                }
            }
        }
        
        // Плавное изменение тяги (не резко!)
        // Но если нужно быстро уменьшить тягу (падаем медленнее), увеличиваем скорость изменения
        float maxThrustChange = thrustChangeRate * Time.deltaTime;
        if (currentVerticalSpeed > targetVerticalSpeed && targetThrust < currentThrust)
        {
            // Падаем медленнее и нужно уменьшить тягу - увеличиваем скорость изменения в 3 раза
            maxThrustChange *= 3f;
        }
        currentThrust = Mathf.MoveTowards(currentThrust, targetThrust, maxThrustChange);
        
        // Если НЕ идет выравнивание по нормали поверхности, обновляем базовую тягу всех двигателей
        // (если идет выравнивание, AlignToSurfaceNormal сам управляет тягой)
        if (!isAligningToSurface)
        {
            // Обновляем базовую тягу всех двигателей до currentThrust
            // StabilizeRotation будет использовать currentThrust как базовую и добавлять коррекцию
            int engineCount = shipController.GetEngineCount();
            EnsureEngineThrustArray();
            float maxEngineThrustChange = thrustChangeRate * Time.deltaTime;
            if (currentVerticalSpeed > targetVerticalSpeed && targetThrust < currentThrust)
            {
                // Падаем медленнее и нужно уменьшить тягу - увеличиваем скорость изменения в 3 раза
                maxEngineThrustChange *= 3f;
            }
            for (int i = 0; i < engineCount; i++)
            {
                // Плавно обновляем базовую тягу каждого двигателя до currentThrust
                // StabilizeRotation добавит коррекцию для стабилизации rotation
                currentEngineThrusts[i] = Mathf.MoveTowards(currentEngineThrusts[i], currentThrust, maxEngineThrustChange);
                shipController.SetEngineThrust(i, currentEngineThrusts[i]);
            }
        }
        
        // Логирование
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"LandingAutopilot: === КОНТРОЛЬ СКОРОСТИ ПАДЕНИЯ ===");
            Debug.Log($"  Высота до цели: {verticalDistance:F1}м");
            Debug.Log($"  Текущая скорость падения: {currentVerticalSpeed:F2}м/с");
            Debug.Log($"  Целевая максимальная скорость: {targetMaxSpeed:F2}м/с");
            Debug.Log($"  Целевая скорость (отрицательная): {targetVerticalSpeed:F2}м/с");
            Debug.Log($"  Ошибка скорости: {verticalSpeedError:F2}м/с");
            Debug.Log($"  hoverThrust: {hoverThrust:F3}");
            Debug.Log($"  verticalCorrection (от PID): {verticalCorrection:F3}");
            Debug.Log($"  correctionMultiplier: {correctionMultiplier:F3}");
            Debug.Log($"  targetThrust: {targetThrust:F3}");
            Debug.Log($"  Тяга (текущая): {currentThrust:F3}");
            int engineCount = shipController.GetEngineCount();
            if (engineCount >= 4)
            {
                Debug.Log($"  Тяга двигателей: E0={currentEngineThrusts[0]:F3}, E1={currentEngineThrusts[1]:F3}, E2={currentEngineThrusts[2]:F3}, E3={currentEngineThrusts[3]:F3}");
            }
            Debug.Log($"  ЛОГИКА: currentVerticalSpeed={currentVerticalSpeed:F2}, targetVerticalSpeed={targetVerticalSpeed:F2}");
            if (currentVerticalSpeed > 0f)
            {
                Debug.Log($"    → Корабль летит ВВЕРХ → targetThrust=0");
            }
            else if (currentVerticalSpeed > targetVerticalSpeed)
            {
                Debug.Log($"    → Падаем МЕДЛЕННЕЕ целевой скорости → УМЕНЬШАЕМ тягу");
            }
            else
            {
                Debug.Log($"    → Падаем БЫСТРЕЕ целевой скорости → УВЕЛИЧИВАЕМ тягу");
            }
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
        // КРИТИЧНО: Если корабль наклонен, локальная Y может иметь значение из-за проекции
        // Принудительно обнуляем локальную Y компоненту (вертикальную в локальных координатах)
        localVelocity3D.y = 0f;
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
    /// ВРЕМЕННО: Выравнивание корабля к вертикали (rotation = 0, 0, 0), игнорируя нормаль поверхности
    /// Использует управление двигателями для выравнивания, а не прямое изменение rotation
    /// </summary>
    private void AlignToSurfaceNormal(Vector3 surfaceNormal)
    {
        isAligningToSurface = true; // Устанавливаем флаг, что идет выравнивание
        
        Transform shipTransform = shipController.transform;
        // ВРЕМЕННО: Выравниваемся к вертикали, игнорируя нормаль поверхности
        Vector3 desiredUp = Vector3.up;
        Vector3 currentUp = shipTransform.up;
        Vector3 currentForward = shipTransform.forward;
        Vector3 currentRight = shipTransform.right;
        
        // Вычисляем желаемое направление "вперед" корабля
        // Проектируем текущий forward на плоскость поверхности, чтобы сохранить направление движения
        Vector3 desiredForward = Vector3.ProjectOnPlane(currentForward, desiredUp).normalized;
        
        // Если проекция слишком мала (корабль почти перпендикулярен поверхности), используем направление к цели
        if (desiredForward.magnitude < 0.1f && targetSite != null)
        {
            Vector3 toTarget = (targetSite.position - shipTransform.position).normalized;
            desiredForward = Vector3.ProjectOnPlane(toTarget, desiredUp).normalized;
            
            // Если и это не помогло, используем текущий right, спроектированный на плоскость
            if (desiredForward.magnitude < 0.1f)
            {
                desiredForward = Vector3.ProjectOnPlane(currentRight, desiredUp).normalized;
            }
            
            // Если все еще не получилось, используем произвольное направление
            if (desiredForward.magnitude < 0.1f)
            {
                // Выбираем направление, перпендикулярное нормали
                if (Mathf.Abs(desiredUp.y) > 0.9f)
                {
                    // Нормаль почти вертикальна, используем Vector3.forward
                    desiredForward = Vector3.ProjectOnPlane(Vector3.forward, desiredUp).normalized;
                }
                else
                {
                    // Используем Vector3.up, спроектированный на плоскость
                    desiredForward = Vector3.ProjectOnPlane(Vector3.up, desiredUp).normalized;
                }
            }
        }
        
        // Вычисляем желаемое направление "вправо" корабля
        Vector3 desiredRight = Vector3.Cross(desiredForward, desiredUp).normalized;
        
        // Вычисляем желаемую ориентацию
        Quaternion targetRotation = Quaternion.LookRotation(desiredForward, desiredUp);
        
        // Вычисляем текущую ориентацию
        Quaternion currentRotation = shipTransform.rotation;
        
        // Вычисляем ошибку ориентации
        Quaternion rotationError = Quaternion.Inverse(currentRotation) * targetRotation;
        
        // Преобразуем ошибку в углы Эйлера (в локальных координатах)
        rotationError.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f; // Нормализуем угол до -180..180
        
        // Вычисляем угловую скорость для выравнивания
        Vector3 angularVelocity = shipController.GetAngularVelocity();
        Vector3 localAngularVelocity = shipTransform.InverseTransformDirection(angularVelocity);
        
        // Вычисляем желаемую угловую скорость для выравнивания
        // Увеличиваем коэффициент для более агрессивного выравнивания при больших ошибках
        float speedMultiplier = angle > 10f ? 0.05f : 0.03f; // Больше для больших ошибок
        float targetAngularSpeed = angle * orientationSmoothing * speedMultiplier; // Пропорционально углу ошибки
        targetAngularSpeed = Mathf.Clamp(targetAngularSpeed, -orientationSmoothing, orientationSmoothing);
        
        // Преобразуем ось вращения в локальные координаты корабля
        Vector3 localAxis = shipTransform.InverseTransformDirection(axis);
        
        // Вычисляем коррекцию для каждой оси (локальные координаты)
        // X - крен (roll), Y - рыскание (yaw), Z - тангаж (pitch)
        float rollCorrection = -localAxis.x * targetAngularSpeed;   // Крен
        float yawCorrection = -localAxis.y * targetAngularSpeed;    // Рыскание
        float pitchCorrection = -localAxis.z * targetAngularSpeed;  // Тангаж
        
        // Используем PID для плавного выравнивания
        float rollPID = orientationPID.Update(0f, localAngularVelocity.x, Time.deltaTime);
        float yawPID = orientationPID.Update(0f, localAngularVelocity.y, Time.deltaTime);
        float pitchPID = orientationPID.Update(0f, localAngularVelocity.z, Time.deltaTime);
        
        // Комбинируем коррекцию с PID
        rollCorrection += rollPID;
        yawCorrection += yawPID;
        pitchCorrection += pitchPID;
        
        // Ограничиваем коррекцию, но если ошибка большая, увеличиваем силу
        // Если угол ошибки большой (> 10°), увеличиваем максимальную коррекцию
        float maxCorrection = angle > 10f ? 2f : 1f;
        rollCorrection = Mathf.Clamp(rollCorrection, -maxCorrection, maxCorrection);
        yawCorrection = Mathf.Clamp(yawCorrection, -maxCorrection, maxCorrection);
        pitchCorrection = Mathf.Clamp(pitchCorrection, -maxCorrection, maxCorrection);
        
        // Проверяем, достаточно ли выровнен корабль
        float angleError = Vector3.Angle(currentUp, desiredUp);
        
        // Логирование выравнивания (каждый 60 кадр)
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"LandingAutopilot: === ВЫРАВНИВАНИЕ === (ошибка угла: {angleError:F1}°, totalAngle: {angle:F1}°)");
        }
        
        bool isAligned = angleError < maxOrientationAngle && angle < 5f && localAngularVelocity.magnitude < 0.1f;
        
        // Если корабль выровнен, уменьшаем силу коррекций (но продолжаем их применять для компенсации ветра/турбулентности)
        float alignmentStrength = isAligned ? 0.3f : 1.0f; // Если выровнен, используем 30% силы коррекций
        
        if (isAligned)
        {
            isAligningToSurface = false; // Выравнивание завершено, но продолжаем контролировать rotation
        }
        
        if (useGeometryThrustStabilization)
        {
            float angleRad = angle * Mathf.Deg2Rad;
            Vector3 desiredTorque = axis.normalized * angleRad * geometryTorqueStrength - angularVelocity * geometryTorqueDamping;
            bool applied = ApplyTorqueWithThrust(desiredTorque, currentThrust, 0.05f, maxGeometryThrustDelta, 5f);
            if (applied)
            {
                return;
            }
        }
        
        // Применяем коррекцию через управление двигателями
        // Для крена: разная тяга левых/правых двигателей
        // Для рыскания: поворот двигателей влево/вправо
        // Для тангажа: поворот двигателей вперед/назад
        
        int engineCount = shipController.GetEngineCount();
        EnsureEngineThrustArray();
        float[] targetEngineThrusts = new float[engineCount]; // Для логирования и применения
        float rollStrength = 0f; // Для логирования
        float pitchStrength = 0f; // Для логирования
        float yawStrength = 0f; // Для логирования
        
        if (engineCount >= 4)
        {
            bool hasQuad = TryGetEngineQuadrants(out int frontLeft, out int frontRight, out int backLeft, out int backRight);
            if (!hasQuad)
            {
                frontLeft = 0;
                frontRight = 1;
                backLeft = 2;
                backRight = 3;
            }
            
            // Крен (roll): увеличиваем тягу левых двигателей, уменьшаем правых
            // Тангаж (pitch): увеличиваем тягу передних двигателей, уменьшаем задних
            float baseThrust = currentThrust; // Используем текущую тягу как базовую
            
            for (int i = 0; i < engineCount; i++)
            {
                targetEngineThrusts[i] = baseThrust;
            }
            
            // Если базовая тяга 0, не добавляем коррекцию (чтобы двигатели оставались на 0)
            // Коррекция нужна только для стабилизации, а не для создания подъемной силы
            if (baseThrust <= 0.001f)
            {
                // Базовая тяга 0 - устанавливаем все двигатели на 0
                for (int i = 0; i < engineCount; i++)
                {
                    targetEngineThrusts[i] = 0f;
                }
            }
            else
            {
                // Базовая тяга не 0 - добавляем коррекцию для стабилизации
                // Учитываем alignmentStrength: если корабль уже выровнен, уменьшаем силу коррекций
                // Увеличиваем коэффициент для более сильной коррекции при больших ошибках
                float thrustCorrectionStrength = angleError > 10f ? 0.4f : 0.3f; // Больше для больших ошибок
                
                // ВСЕ коррекции rotation ТОЛЬКО через дифференциальную тягу двигателей:
                // 1. Крен (roll): разная тяга левых/правых двигателей
                //    - Крен влево (roll > 0): увеличиваем левые (E0, E2), уменьшаем правые (E1, E3)
                //    - Крен вправо (roll < 0): увеличиваем правые (E1, E3), уменьшаем левые (E0, E2)
                // 2. Тангаж (pitch): разная тяга передних/задних двигателей
                //    - Нос вниз (pitch > 0): увеличиваем передние (E0, E1), уменьшаем задние (E2, E3)
                //    - Нос вверх (pitch < 0): увеличиваем задние (E2, E3), уменьшаем передние (E0, E1)
                // 3. Рыскание (yaw): диагональная дифференциальная тяга
                //    - Поворот влево (yaw > 0): увеличиваем правые двигатели (E1, E3), уменьшаем левые (E0, E2)
                //    - Поворот вправо (yaw < 0): увеличиваем левые двигатели (E0, E2), уменьшаем правые (E1, E3)
                
                rollStrength = rollCorrection * thrustCorrectionStrength * alignmentStrength;
                pitchStrength = pitchCorrection * thrustCorrectionStrength * alignmentStrength;
                yawStrength = yawCorrection * thrustCorrectionStrength * alignmentStrength;
                
                // Применяем ВСЕ три коррекции одновременно:
                // FL: +roll, -pitch, -yaw
                // FR: -roll, -pitch, +yaw
                // BL: +roll, +pitch, -yaw
                // BR: -roll, +pitch, +yaw
                targetEngineThrusts[frontLeft] = baseThrust + rollStrength - pitchStrength - yawStrength;
                targetEngineThrusts[frontRight] = baseThrust - rollStrength - pitchStrength + yawStrength;
                targetEngineThrusts[backLeft] = baseThrust + rollStrength + pitchStrength - yawStrength;
                targetEngineThrusts[backRight] = baseThrust - rollStrength + pitchStrength + yawStrength;
            }
            
            // Дополнительная защита: проверяем, что ни один двигатель не стал отрицательным или слишком малым
            // Используем адаптивный минимум в зависимости от ошибки ориентации для лучшей стабилизации
            float minEngineThrustForAlignment = angleError > 5f ? 0.02f : 0.05f; // 2% для больших ошибок, 5% для малых
            float minThrust = Mathf.Min(
                targetEngineThrusts[frontLeft],
                targetEngineThrusts[frontRight],
                targetEngineThrusts[backLeft],
                targetEngineThrusts[backRight]
            );
            if (minThrust < minEngineThrustForAlignment)
            {
                // Уменьшаем все коррекции пропорционально, чтобы минимальная тяга стала >= minEngineThrustForAlignment
                float reductionFactor = (baseThrust - minEngineThrustForAlignment) / (baseThrust - minThrust);
                rollStrength *= reductionFactor;
                pitchStrength *= reductionFactor;
                yawStrength *= reductionFactor;
                // Пересчитываем тягу двигателей с уменьшенными коррекциями
                targetEngineThrusts[frontLeft] = baseThrust + rollStrength - pitchStrength - yawStrength;
                targetEngineThrusts[frontRight] = baseThrust - rollStrength - pitchStrength + yawStrength;
                targetEngineThrusts[backLeft] = baseThrust + rollStrength + pitchStrength - yawStrength;
                targetEngineThrusts[backRight] = baseThrust - rollStrength + pitchStrength + yawStrength;
            }
            
            // Плавное изменение тяги каждого двигателя (не резко!)
            // Для выравнивания используем более быструю скорость изменения для быстрой реакции
            float alignmentThrustChangeRate = thrustChangeRate * 5.0f; // Увеличено для более быстрого выравнивания
            float maxThrustChange = alignmentThrustChangeRate * Time.deltaTime;
            
            for (int i = 0; i < engineCount; i++)
            {
                float target = Mathf.Clamp01(targetEngineThrusts[i]);
                if (baseThrust > 0.001f)
                {
                    target = Mathf.Max(target, minEngineThrustForAlignment);
                }
                currentEngineThrusts[i] = Mathf.MoveTowards(currentEngineThrusts[i], target, maxThrustChange);
                if (baseThrust > 0.001f)
                {
                currentEngineThrusts[i] = Mathf.Max(currentEngineThrusts[i], minEngineThrustForAlignment);
                }
                shipController.SetEngineThrust(i, currentEngineThrusts[i]);
            }
        }
        
        // ВАЖНО: ВСЕ коррекции rotation теперь выполняются ТОЛЬКО через дифференциальную тягу двигателей!
        // НЕ изменяем movementDirection для коррекции rotation, чтобы не мешать горизонтальному движению к цели.
        // movementDirection должен управляться только MoveTowardsTarget для движения к точке посадки.
        
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"LandingAutopilot: === ВЫРАВНИВАНИЕ ПО НОРМАЛИ ПОВЕРХНОСТИ ===");
            Debug.Log($"  НОРМАЛЬ ПОВЕРХНОСТИ:");
            Debug.Log($"    surfaceNormal: {surfaceNormal}");
            Debug.Log($"    desiredUp: {desiredUp}");
            Debug.Log($"  ТЕКУЩАЯ ОРИЕНТАЦИЯ КОРАБЛЯ:");
            Debug.Log($"    currentUp: {currentUp}");
            Debug.Log($"    currentForward: {currentForward}");
            Debug.Log($"    currentRight: {currentRight}");
            Debug.Log($"    Rotation (Euler): {shipTransform.rotation.eulerAngles}° (X={shipTransform.rotation.eulerAngles.x:F1}°, Y={shipTransform.rotation.eulerAngles.y:F1}°, Z={shipTransform.rotation.eulerAngles.z:F1}°)");
            Debug.Log($"  ЖЕЛАЕМАЯ ОРИЕНТАЦИЯ:");
            Debug.Log($"    desiredForward: {desiredForward}");
            Debug.Log($"    desiredRight: {desiredRight}");
            Debug.Log($"    targetRotation (Euler): {targetRotation.eulerAngles}° (X={targetRotation.eulerAngles.x:F1}°, Y={targetRotation.eulerAngles.y:F1}°, Z={targetRotation.eulerAngles.z:F1}°)");
            Debug.Log($"  ОШИБКА ОРИЕНТАЦИИ:");
            Debug.Log($"    angleError (по нормали): {angleError:F1}°");
            Debug.Log($"    totalAngleError: {angle:F1}° (Axis: {axis})");
            Debug.Log($"    rotationError (Euler): {rotationError.eulerAngles}° (X={rotationError.eulerAngles.x:F1}°, Y={rotationError.eulerAngles.y:F1}°, Z={rotationError.eulerAngles.z:F1}°)");
            Debug.Log($"    localAxis: {localAxis} (X={localAxis.x:F3}, Y={localAxis.y:F3}, Z={localAxis.z:F3})");
            Debug.Log($"  УГЛОВАЯ СКОРОСТЬ:");
            Debug.Log($"    angularVelocity (world): {angularVelocity} (X={angularVelocity.x:F3}, Y={angularVelocity.y:F3}, Z={angularVelocity.z:F3})");
            Debug.Log($"    localAngularVelocity: {localAngularVelocity} (X={localAngularVelocity.x:F3}, Y={localAngularVelocity.y:F3}, Z={localAngularVelocity.z:F3})");
            Debug.Log($"    targetAngularSpeed: {targetAngularSpeed:F3}");
            Debug.Log($"  КОРРЕКЦИЯ:");
            Debug.Log($"    rollCorrection: {rollCorrection:F3} (крен, вокруг локальной оси X/forward)");
            Debug.Log($"    yawCorrection: {yawCorrection:F3} (рыскание, вокруг локальной оси Y/up)");
            Debug.Log($"    pitchCorrection: {pitchCorrection:F3} (тангаж, вокруг локальной оси Z/right)");
            Debug.Log($"    rollPID: {rollPID:F3}, yawPID: {yawPID:F3}, pitchPID: {pitchPID:F3}");
            Debug.Log($"  УПРАВЛЕНИЕ ДВИГАТЕЛЯМИ:");
            Debug.Log($"    baseThrust: {currentThrust:F3}");
            if (engineCount >= 4)
            {
                bool hasQuad = TryGetEngineQuadrants(out int frontLeft, out int frontRight, out int backLeft, out int backRight);
                if (!hasQuad)
                {
                    frontLeft = 0;
                    frontRight = 1;
                    backLeft = 2;
                    backRight = 3;
                }
                Debug.Log($"    Engine FL({frontLeft}): {targetEngineThrusts[frontLeft]:F3} (roll:{rollStrength:F3}, pitch:{-pitchStrength:F3}, yaw:{-yawStrength:F3})");
                Debug.Log($"    Engine FR({frontRight}): {targetEngineThrusts[frontRight]:F3} (roll:{-rollStrength:F3}, pitch:{-pitchStrength:F3}, yaw:{yawStrength:F3})");
                Debug.Log($"    Engine BL({backLeft}): {targetEngineThrusts[backLeft]:F3} (roll:{rollStrength:F3}, pitch:{pitchStrength:F3}, yaw:{-yawStrength:F3})");
                Debug.Log($"    Engine BR({backRight}): {targetEngineThrusts[backRight]:F3} (roll:{-rollStrength:F3}, pitch:{pitchStrength:F3}, yaw:{yawStrength:F3})");
            }
            Debug.Log($"    ВСЕ коррекции rotation через дифференциальную тягу двигателей (НЕ через movementDirection)");
            Debug.Log($"  СТАТУС:");
            Debug.Log($"    isAligningToSurface: {isAligningToSurface}, angleError: {angleError:F1}°, totalAngle: {angle:F1}°, isAligned: {isAligned}, alignmentStrength: {alignmentStrength:F2}");
        }
        
        // Флаг isAligningToSurface остается true, пока корабль не выровнен
        // (сбрасывается только когда isAligned = true)
    }
    
    // ========== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ==========
    
    private void EnsureEngineThrustArray()
    {
        int engineCount = shipController.GetEngineCount();
        if (currentEngineThrusts == null || currentEngineThrusts.Length != engineCount)
        {
            float[] newThrusts = new float[engineCount];
            if (currentEngineThrusts != null)
            {
                int copyCount = Mathf.Min(currentEngineThrusts.Length, engineCount);
                for (int i = 0; i < copyCount; i++)
                {
                    newThrusts[i] = currentEngineThrusts[i];
                }
            }
            currentEngineThrusts = newThrusts;
        }
    }
    
    private bool ApplyTorqueWithThrust(
        Vector3 desiredTorqueWorld,
        float baseThrust,
        float minThrust,
        float maxDeltaThrust,
        float changeRateMultiplier)
    {
        int engineCount = shipController.GetEngineCount();
        if (engineCount < 3) return false;
        
        EnsureEngineThrustArray();
        
        Vector3 centerOfMass = shipController.GetWorldCenterOfMass();
        float maxThrustForce = shipController.GetMaxThrustForce();
        Vector3[] torqueColumns = new Vector3[engineCount];
        bool[] valid = new bool[engineCount];
        int validCount = 0;
        
        for (int i = 0; i < engineCount; i++)
        {
            Transform engineTransform = shipController.GetEngineTransform(i);
            if (engineTransform == null) continue;
            
            Vector3 r = engineTransform.position - centerOfMass;
            Vector3 forceDir = -engineTransform.forward;
            Vector3 torquePerThrust = Vector3.Cross(r, forceDir * maxThrustForce);
            torqueColumns[i] = torquePerThrust;
            valid[i] = true;
            validCount++;
        }
        
        if (validCount < 3) return false;
        
        float m00 = 0f, m01 = 0f, m02 = 0f;
        float m10 = 0f, m11 = 0f, m12 = 0f;
        float m20 = 0f, m21 = 0f, m22 = 0f;
        
        for (int i = 0; i < engineCount; i++)
        {
            if (!valid[i]) continue;
            Vector3 c = torqueColumns[i];
            m00 += c.x * c.x;
            m01 += c.x * c.y;
            m02 += c.x * c.z;
            m10 += c.y * c.x;
            m11 += c.y * c.y;
            m12 += c.y * c.z;
            m20 += c.z * c.x;
            m21 += c.z * c.y;
            m22 += c.z * c.z;
        }
        
        if (!TryInvertMatrix3x3(m00, m01, m02, m10, m11, m12, m20, m21, m22,
                out float i00, out float i01, out float i02,
                out float i10, out float i11, out float i12,
                out float i20, out float i21, out float i22))
        {
            return false;
        }
        
        Vector3 v = new Vector3(
            i00 * desiredTorqueWorld.x + i01 * desiredTorqueWorld.y + i02 * desiredTorqueWorld.z,
            i10 * desiredTorqueWorld.x + i11 * desiredTorqueWorld.y + i12 * desiredTorqueWorld.z,
            i20 * desiredTorqueWorld.x + i21 * desiredTorqueWorld.y + i22 * desiredTorqueWorld.z
        );
        
        float maxChange = thrustChangeRate * changeRateMultiplier * Time.deltaTime;
        for (int i = 0; i < engineCount; i++)
        {
            float target = baseThrust;
            if (valid[i])
            {
                float delta = Vector3.Dot(torqueColumns[i], v);
                delta = Mathf.Clamp(delta, -maxDeltaThrust, maxDeltaThrust);
                target = baseThrust + delta;
            }
            
            target = Mathf.Clamp01(target);
            if (minThrust > 0f)
            {
                target = Mathf.Max(target, minThrust);
            }
            
            currentEngineThrusts[i] = Mathf.MoveTowards(currentEngineThrusts[i], target, maxChange);
            if (minThrust > 0f)
            {
                currentEngineThrusts[i] = Mathf.Max(currentEngineThrusts[i], minThrust);
            }
            shipController.SetEngineThrust(i, currentEngineThrusts[i]);
        }
        
        return true;
    }
    
    private bool TryInvertMatrix3x3(
        float m00, float m01, float m02,
        float m10, float m11, float m12,
        float m20, float m21, float m22,
        out float i00, out float i01, out float i02,
        out float i10, out float i11, out float i12,
        out float i20, out float i21, out float i22)
    {
        float det = m00 * (m11 * m22 - m12 * m21)
                  - m01 * (m10 * m22 - m12 * m20)
                  + m02 * (m10 * m21 - m11 * m20);
        
        if (Mathf.Abs(det) < 1e-6f)
        {
            i00 = i01 = i02 = 0f;
            i10 = i11 = i12 = 0f;
            i20 = i21 = i22 = 0f;
            return false;
        }
        
        float invDet = 1f / det;
        i00 = (m11 * m22 - m12 * m21) * invDet;
        i01 = (m02 * m21 - m01 * m22) * invDet;
        i02 = (m01 * m12 - m02 * m11) * invDet;
        i10 = (m12 * m20 - m10 * m22) * invDet;
        i11 = (m00 * m22 - m02 * m20) * invDet;
        i12 = (m02 * m10 - m00 * m12) * invDet;
        i20 = (m10 * m21 - m11 * m20) * invDet;
        i21 = (m01 * m20 - m00 * m21) * invDet;
        i22 = (m00 * m11 - m01 * m10) * invDet;
        
        return true;
    }
    
    private bool TryGetEngineQuadrants(out int frontLeft, out int frontRight, out int backLeft, out int backRight)
    {
        frontLeft = -1;
        frontRight = -1;
        backLeft = -1;
        backRight = -1;
        
        int engineCount = shipController.GetEngineCount();
        if (engineCount < 4) return false;
        
        float bestFL = float.NegativeInfinity;
        float bestFR = float.NegativeInfinity;
        float bestBL = float.NegativeInfinity;
        float bestBR = float.NegativeInfinity;
        
        for (int i = 0; i < engineCount; i++)
        {
            Transform engineTransform = shipController.GetEngineTransform(i);
            if (engineTransform == null) continue;
            
            Vector3 localPos = shipController.transform.InverseTransformPoint(engineTransform.position);
            float score = Mathf.Abs(localPos.x) + Mathf.Abs(localPos.z);
            
            if (localPos.z >= 0f)
            {
                if (localPos.x <= 0f)
                {
                    if (score > bestFL)
                    {
                        bestFL = score;
                        frontLeft = i;
                    }
                }
                else
                {
                    if (score > bestFR)
                    {
                        bestFR = score;
                        frontRight = i;
                    }
                }
            }
            else
            {
                if (localPos.x <= 0f)
                {
                    if (score > bestBL)
                    {
                        bestBL = score;
                        backLeft = i;
                    }
                }
                else
                {
                    if (score > bestBR)
                    {
                        bestBR = score;
                        backRight = i;
                    }
                }
            }
        }
        
        if (frontLeft >= 0 && frontRight >= 0 && backLeft >= 0 && backRight >= 0)
        {
            return true;
        }
        
        // Fallback: сортируем по Z, затем выбираем левый/правый по X
        List<(int index, Vector3 localPos)> engines = new List<(int, Vector3)>();
        for (int i = 0; i < engineCount; i++)
        {
            Transform engineTransform = shipController.GetEngineTransform(i);
            if (engineTransform == null) continue;
            Vector3 localPos = shipController.transform.InverseTransformPoint(engineTransform.position);
            engines.Add((i, localPos));
        }
        
        if (engines.Count < 4) return false;
        
        var sortedByZ = engines.OrderByDescending(e => e.localPos.z).ToList();
        int half = sortedByZ.Count / 2;
        var frontGroup = sortedByZ.Take(half).ToList();
        var backGroup = sortedByZ.Skip(half).ToList();
        
        var frontLeftEntry = frontGroup.OrderBy(e => e.localPos.x).FirstOrDefault();
        var frontRightEntry = frontGroup.OrderByDescending(e => e.localPos.x).FirstOrDefault();
        var backLeftEntry = backGroup.OrderBy(e => e.localPos.x).FirstOrDefault();
        var backRightEntry = backGroup.OrderByDescending(e => e.localPos.x).FirstOrDefault();
        
        frontLeft = frontLeftEntry.index;
        frontRight = frontRightEntry.index;
        backLeft = backLeftEntry.index;
        backRight = backRightEntry.index;
        
        return frontLeft >= 0 && frontRight >= 0 && backLeft >= 0 && backRight >= 0;
    }
    
    /// <summary>
    /// Стабилизация rotation через управление отдельными двигателями
    /// Использует дифференциальную тягу для компенсации крена/тангажа/рыскания
    /// НЕ работает когда идет выравнивание по нормали поверхности (AlignToSurfaceNormal имеет приоритет)
    /// </summary>
    /// <summary>
    /// Стабилизация rotation относительно нормали поверхности точки посадки
    /// </summary>
    /// <param name="surfaceNormal">Нормаль поверхности (если null, используется Vector3.up для вертикальной ориентации)</param>
    private void StabilizeRotation(Vector3 surfaceNormal = default)
    {
        // Если нормаль не задана, используем вертикаль
        if (surfaceNormal == default || surfaceNormal.magnitude < 0.1f)
        {
            surfaceNormal = Vector3.up;
        }
        else
        {
            surfaceNormal = surfaceNormal.normalized;
        }
        
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
        
        // ВАЖНО: Компенсируем не только угловую скорость, но и саму ориентацию!
        // Если корабль наклонен, но не вращается, он все равно должен выравниваться
        
        // ВРЕМЕННО: Выравниваемся к вертикали (rotation = 0, 0, 0), игнорируя нормаль поверхности
        Vector3 desiredUp = Vector3.up; // Ориентация вертикально вверх
        Vector3 currentUp = shipTransform.up;
        float orientationError = Vector3.Angle(currentUp, desiredUp);
        
        // Геометрическое распределение тяги по двигателям для стабилизации
        if (useGeometryThrustStabilization)
        {
            Vector3 correctionAxis = Vector3.Cross(currentUp, desiredUp);
            Vector3 desiredTorque = correctionAxis * geometryTorqueStrength - angularVelocity * geometryTorqueDamping;
            bool applied = ApplyTorqueWithThrust(desiredTorque, currentThrust, 0.05f, maxGeometryThrustDelta, 5f);
            if (applied)
            {
                return;
            }
        }
        
        // Прямое стабилизирующее torque (без дифференциальной тяги)
        if (useDirectTorqueStabilization)
        {
            Vector3 correctionAxis = Vector3.Cross(currentUp, desiredUp);
            Vector3 torque = correctionAxis * directTorqueStrength - angularVelocity * directTorqueDamping;
            shipController.ApplyAutopilotTorque(torque);
            return;
        }
        
        // Вычисляем желаемую угловую скорость для выравнивания (пропорционально ошибке ориентации)
        // Если корабль наклонен, нужно его выровнять
        // Увеличены коэффициенты для более агрессивного и эффективного выравнивания к (0, 0, 0)
        // Используем более высокий коэффициент для малых ошибок (0-5°), чтобы обеспечить точное выравнивание
        float orientationCorrectionSpeed;
        if (orientationError > 10f)
        {
            orientationCorrectionSpeed = orientationError * 0.25f; // Для больших ошибок
        }
        else if (orientationError > 5f)
        {
            orientationCorrectionSpeed = orientationError * 0.22f; // Для средних ошибок
        }
        else
        {
            // Для малых ошибок (0-5°) используем более высокий коэффициент для точного выравнивания
            orientationCorrectionSpeed = orientationError * 0.20f; // Увеличено для лучшей стабилизации малых ошибок
        }
        
        Vector3 desiredAngularVelocity = Vector3.Cross(currentUp, desiredUp).normalized * orientationCorrectionSpeed;
        if (invertRotationCorrection)
        {
            desiredAngularVelocity = -desiredAngularVelocity;
        }
        Vector3 localDesiredAngularVelocity = shipTransform.InverseTransformDirection(desiredAngularVelocity);
        
        // Комбинируем компенсацию угловой скорости И ориентации
        // Целевая угловая скорость = компенсация текущей угловой скорости + выравнивание ориентации
        Vector3 targetAngularVelocity = -localAngularVelocity + localDesiredAngularVelocity;
        
        // ВАЖНО: Усиливаем выравнивание для ВСЕХ ошибок ориентации, не только > 5°
        // Для точного выравнивания к (0, 0, 0) нужна более агрессивная реакция
        // Для очень малых ошибок используем меньшее усиление, чтобы избежать перерегулирования
        float velocityMagnitude = localAngularVelocity.magnitude;
        if (velocityMagnitude < 0.15f) // Если угловая скорость мала (увеличено с 0.1 для большей чувствительности)
        {
            if (orientationError > 10f)
            {
                targetAngularVelocity = localDesiredAngularVelocity * 2.2f; // Очень большие ошибки (> 10°) - усилено с 1.8
            }
            else if (orientationError > 7f)
            {
                targetAngularVelocity = localDesiredAngularVelocity * 2.3f; // Большие ошибки (7-10°) - усилено для быстрого выравнивания
            }
            else if (orientationError > 5f)
            {
                targetAngularVelocity = localDesiredAngularVelocity * 2.1f; // Средне-большие ошибки (5-7°) - усилено
            }
            else if (orientationError > 2f)
            {
                targetAngularVelocity = localDesiredAngularVelocity * 2.5f; // Средние ошибки (2-5°) - усилено с 2.0
            }
            else if (orientationError > 0.5f)
            {
                // Маленькие ошибки (0.5-2°) - умеренное усиление для плавного выравнивания
                targetAngularVelocity = localDesiredAngularVelocity * 2.0f; // Уменьшено с 2.5 для плавности
            }
            else
            {
                // Очень маленькие ошибки (< 0.5°) - минимальное усиление для финального выравнивания без перерегулирования
                targetAngularVelocity = localDesiredAngularVelocity * 1.5f; // Уменьшено с 3.0 для избежания перерегулирования
            }
        }
        
        // Используем отдельные PID для каждой оси rotation
        // Цель: достичь targetAngularVelocity (компенсировать текущую скорость + выровнять ориентацию)
        float rollTarget = targetAngularVelocity.x;
        float rollCurrent = localAngularVelocity.x;
        float pitchTarget = targetAngularVelocity.z;
        float pitchCurrent = localAngularVelocity.z;
        if (swapRollPitchAxes)
        {
            rollTarget = targetAngularVelocity.z;
            rollCurrent = localAngularVelocity.z;
            pitchTarget = targetAngularVelocity.x;
            pitchCurrent = localAngularVelocity.x;
        }
        
        float rollCorrection = rollStabilizationPID.Update(rollTarget, rollCurrent, Time.deltaTime);
        float yawCorrection = yawStabilizationPID.Update(targetAngularVelocity.y, localAngularVelocity.y, Time.deltaTime);
        float pitchCorrection = pitchStabilizationPID.Update(pitchTarget, pitchCurrent, Time.deltaTime);
        
        // ВАЖНО: Получаем текущую базовую тягу ДО применения коррекций, чтобы ограничить их
        float baseThrustForCorrection = currentThrust;
        float minStabilizationThrustForCorrection = 0.15f;
        float effectiveBaseThrustForCorrection = Mathf.Max(baseThrustForCorrection, minStabilizationThrustForCorrection);
        
        // Ограничиваем коррекцию относительно базовой тяги, чтобы не создавать отрицательную тягу
        // ВАЖНО: При применении rollCorrection и pitchCorrection одновременно (E2 = base + roll + pitch),
        // сумма коррекций не должна превышать baseThrust, иначе получим отрицательную тягу
        // Для точного выравнивания к (0, 0, 0) используем адаптивные ограничения
        // Увеличены ограничения для больших ошибок, чтобы обеспечить быстрое выравнивание
        float correctionMultiplier = 1.0f;
        float maxCorrectionRelative;
        if (orientationError > 10f)
        {
            maxCorrectionRelative = 0.85f; // Большие ошибки (> 10°) - 85% для быстрого выравнивания
            correctionMultiplier = 1.1f; // Дополнительное усиление для больших ошибок
        }
        else if (orientationError > 5f)
        {
            maxCorrectionRelative = 0.80f; // Средние ошибки (5-10°) - 80%
            correctionMultiplier = 1.05f;
        }
        else
        {
            // Маленькие ошибки (0-5°) - используем большее ограничение для точного выравнивания
            maxCorrectionRelative = 0.70f; // Увеличено с 60% для лучшего выравнивания
            correctionMultiplier = 1.3f; // Больше усиление для малых ошибок
        }
        float maxCorrectionAbsolute = effectiveBaseThrustForCorrection * maxCorrectionRelative;
        
        // Ограничиваем коррекцию, но учитываем базовую тягу
        rollCorrection = Mathf.Clamp(rollCorrection * rotationStabilizationStrength * correctionMultiplier, -maxCorrectionAbsolute, maxCorrectionAbsolute);
        yawCorrection = Mathf.Clamp(yawCorrection * rotationStabilizationStrength * correctionMultiplier, -maxCorrectionAbsolute, maxCorrectionAbsolute);
        pitchCorrection = Mathf.Clamp(pitchCorrection * rotationStabilizationStrength * correctionMultiplier, -maxCorrectionAbsolute, maxCorrectionAbsolute);
        
        int engineCount = shipController.GetEngineCount();
        EnsureEngineThrustArray();
        if (engineCount >= 4)
        {
            bool hasQuad = TryGetEngineQuadrants(out int frontLeft, out int frontRight, out int backLeft, out int backRight);
            if (!hasQuad)
            {
                frontLeft = 0;
                frontRight = 1;
                backLeft = 2;
                backRight = 3;
            }
            
            float baseThrust = currentThrust; // Используем текущую тягу как базовую
            
            // ВАЖНО: Даже при низкой базовой тяге используем минимальную тягу для стабилизации rotation
            // Это позволяет компенсировать rotation даже во время падения
            float minStabilizationThrust = 0.15f; // Минимальная тяга (15%) для стабилизации rotation
            float effectiveBaseThrust = Mathf.Max(baseThrust, minStabilizationThrust);
            
            // Вычисляем целевую тягу для каждого двигателя
            // Всегда используем эффективную базовую тягу + коррекцию для стабилизации
            float[] targetEngineThrusts = new float[engineCount];
            for (int i = 0; i < engineCount; i++)
            {
                targetEngineThrusts[i] = effectiveBaseThrust;
            }
            
            // Добавляем коррекцию для стабилизации rotation
            // Используем effectiveBaseThrust, который включает минимальную тягу для стабилизации
            // ВАЖНО: Коррекции уже ограничены относительно базовой тяги в коде выше (maxCorrectionAbsolute)
            // ВСЕ коррекции rotation ТОЛЬКО через дифференциальную тягу двигателей:
            // 1. Крен (roll): разная тяга левых/правых двигателей
            // 2. Тангаж (pitch): разная тяга передних/задних двигателей
            // 3. Рыскание (yaw): диагональная дифференциальная тяга (поворот влево/вправо)
            targetEngineThrusts[frontLeft] = effectiveBaseThrust + rollCorrection - pitchCorrection - yawCorrection;
            targetEngineThrusts[frontRight] = effectiveBaseThrust - rollCorrection - pitchCorrection + yawCorrection;
            targetEngineThrusts[backLeft] = effectiveBaseThrust + rollCorrection + pitchCorrection - yawCorrection;
            targetEngineThrusts[backRight] = effectiveBaseThrust - rollCorrection + pitchCorrection + yawCorrection;
            
            // Дополнительная защита: проверяем, что ни один двигатель не стал отрицательным или слишком малым
            // Используем адаптивный минимум в зависимости от ошибки ориентации для лучшей стабилизации
            // Для очень больших ошибок (> 7°) используем меньший минимум (1%) для максимальной коррекции
            // Для средних ошибок (5-7°) используем 2% для баланса
            // Для малых ошибок (< 5°) используем 5% для стабильности
            float minEngineThrustForProtection;
            if (orientationError > 7f)
            {
                minEngineThrustForProtection = 0.01f; // 1% для очень больших ошибок - максимальный диапазон коррекции
            }
            else if (orientationError > 5f)
            {
                minEngineThrustForProtection = 0.02f; // 2% для средних ошибок
            }
            else
            {
                minEngineThrustForProtection = 0.05f; // 5% для малых ошибок - стабильность важнее
            }
            float minThrust = Mathf.Min(
                targetEngineThrusts[frontLeft],
                targetEngineThrusts[frontRight],
                targetEngineThrusts[backLeft],
                targetEngineThrusts[backRight]
            );
            if (minThrust < minEngineThrustForProtection)
            {
                // Уменьшаем все коррекции пропорционально, чтобы минимальная тяга стала >= minEngineThrustForProtection
                float reductionFactor = (effectiveBaseThrust - minEngineThrustForProtection) / (effectiveBaseThrust - minThrust);
                rollCorrection *= reductionFactor;
                pitchCorrection *= reductionFactor;
                yawCorrection *= reductionFactor;
                // Пересчитываем тягу двигателей с уменьшенными коррекциями
                targetEngineThrusts[frontLeft] = effectiveBaseThrust + rollCorrection - pitchCorrection - yawCorrection;
                targetEngineThrusts[frontRight] = effectiveBaseThrust - rollCorrection - pitchCorrection + yawCorrection;
                targetEngineThrusts[backLeft] = effectiveBaseThrust + rollCorrection + pitchCorrection - yawCorrection;
                targetEngineThrusts[backRight] = effectiveBaseThrust - rollCorrection + pitchCorrection + yawCorrection;
            }
            
            // Плавное изменение тяги каждого двигателя (не резко!)
            // Для стабилизации rotation используем более быструю скорость изменения, чтобы быстрее реагировать на ошибки
            // Значительно увеличена скорость изменения тяги для быстрого выравнивания к (0, 0, 0)
            float stabilizationThrustChangeRate = thrustChangeRate * 5.0f; // Увеличено с 2.5 до 5.0 для более быстрой реакции
            float maxThrustChange = stabilizationThrustChangeRate * Time.deltaTime;
            
            // ВАЖНО: Обеспечиваем минимальную тягу для каждого двигателя для стабильности
            // Используем ту же переменную minEngineThrustForProtection, что и выше, для согласованности
            
            for (int i = 0; i < engineCount; i++)
            {
                targetEngineThrusts[i] = Mathf.Clamp01(targetEngineThrusts[i]);
                // Обеспечиваем минимальную тягу для стабильности (используем ту же переменную, что и выше)
                targetEngineThrusts[i] = Mathf.Max(targetEngineThrusts[i], minEngineThrustForProtection);
                currentEngineThrusts[i] = Mathf.MoveTowards(currentEngineThrusts[i], targetEngineThrusts[i], maxThrustChange);
                // Также применяем минимум к текущей тяге для плавности
                currentEngineThrusts[i] = Mathf.Max(currentEngineThrusts[i], minEngineThrustForProtection);
                shipController.SetEngineThrust(i, currentEngineThrusts[i]);
            }
            
            // ВАЖНО: Все коррекции rotation (включая yaw) теперь выполняются ТОЛЬКО через дифференциальную тягу двигателей!
            // Рыскание (yaw) уже учтено в дифференциальной тяге выше (E0, E2 меньше, E1, E3 больше для поворота влево).
            // НЕ изменяем movementDirection для коррекции rotation, чтобы не мешать горизонтальному движению.
            
            // Логирование для отладки
            if (showDebugInfo && Time.frameCount % 60 == 0)
            {
                // Диагностика: проверяем фактическое расположение двигателей
                // shipTransform уже объявлен выше в методе
                Vector3 shipForward = shipTransform.forward;
                Vector3 shipRight = shipTransform.right;
                Vector3 shipUp = shipTransform.up;
                
                Debug.Log($"LandingAutopilot: === СТАБИЛИЗАЦИЯ ROTATION ===");
                Debug.Log($"  ЛОКАЛЬНЫЕ ОСИ КОРАБЛЯ:");
                Debug.Log($"    ship.forward (локальная X): {shipForward} (вперед)");
                Debug.Log($"    ship.up (локальная Y): {shipUp} (вверх)");
                Debug.Log($"    ship.right (локальная Z): {shipRight} (вправо)");
                Debug.Log($"    Rotation (Euler): {shipTransform.rotation.eulerAngles}° (X={shipTransform.rotation.eulerAngles.x:F1}°, Y={shipTransform.rotation.eulerAngles.y:F1}°, Z={shipTransform.rotation.eulerAngles.z:F1}°)");
                Debug.Log($"  ОСИ ROTATION:");
                Debug.Log($"    Roll (крен, вокруг forward/X): localAngularVelocity.x = {localAngularVelocity.x:F3}");
                Debug.Log($"    Yaw (рыскание, вокруг up/Y): localAngularVelocity.y = {localAngularVelocity.y:F3}");
                Debug.Log($"    Pitch (тангаж, вокруг right/Z): localAngularVelocity.z = {localAngularVelocity.z:F3}");
                Debug.Log($"  Ориентация (ВРЕМЕННО: выравнивание к вертикали Vector3.up):");
                Debug.Log($"    desiredUp (Vector3.up): {desiredUp}");
                Debug.Log($"    currentUp: {currentUp}");
                Debug.Log($"    orientationError (относительно вертикали): {orientationError:F1}°");
                
                // ДИАГНОСТИКА: Проверяем направление вращения
                Vector3 crossAxis = Vector3.Cross(currentUp, desiredUp);
                Vector3 crossAxisNormalized = crossAxis.normalized;
                Debug.Log($"  ДИАГНОСТИКА ВРАЩЕНИЯ:");
                Debug.Log($"    Vector3.Cross(currentUp, desiredUp): {crossAxis} (magnitude: {crossAxis.magnitude:F3})");
                Debug.Log($"    Нормализованная ось: {crossAxisNormalized}");
                Debug.Log($"    В мировых координатах:");
                Debug.Log($"      crossAxis.x = {crossAxisNormalized.x:F3} (влево/вправо в мире)");
                Debug.Log($"      crossAxis.y = {crossAxisNormalized.y:F3} (вверх/вниз в мире)");
                Debug.Log($"      crossAxis.z = {crossAxisNormalized.z:F3} (вперед/назад в мире)");
                Debug.Log($"    В локальных координатах корабля:");
                Debug.Log($"      localDesiredAngularVelocity.x = {localDesiredAngularVelocity.x:F3} (крен, вокруг forward/X)");
                Debug.Log($"      localDesiredAngularVelocity.y = {localDesiredAngularVelocity.y:F3} (рыскание, вокруг up/Y)");
                Debug.Log($"      localDesiredAngularVelocity.z = {localDesiredAngularVelocity.z:F3} (тангаж, вокруг right/Z)");
                Debug.Log($"    ИНТЕРПРЕТАЦИЯ:");
                if (localDesiredAngularVelocity.x > 0.01f)
                    Debug.Log($"      X > 0: Крен ВЛЕВО (левая сторона вниз) → нужно увеличить E0, E2 (левые)");
                else if (localDesiredAngularVelocity.x < -0.01f)
                    Debug.Log($"      X < 0: Крен ВПРАВО (правая сторона вниз) → нужно увеличить E1, E3 (правые)");
                if (localDesiredAngularVelocity.y > 0.01f)
                    Debug.Log($"      Y > 0: Поворот ВЛЕВО (нос поворачивается влево) → нужно увеличить E1, E3 (правые)");
                else if (localDesiredAngularVelocity.y < -0.01f)
                    Debug.Log($"      Y < 0: Поворот ВПРАВО (нос поворачивается вправо) → нужно увеличить E0, E2 (левые)");
                if (localDesiredAngularVelocity.z > 0.01f)
                    Debug.Log($"      Z > 0: Нос ВВЕРХ (задняя часть вниз) → нужно увеличить E2, E3 (задние)");
                else if (localDesiredAngularVelocity.z < -0.01f)
                    Debug.Log($"      Z < 0: Нос ВНИЗ (передняя часть вниз) → нужно увеличить E0, E1 (передние)");
                Debug.Log($"  Угловая скорость:");
                Debug.Log($"    localAngularVelocity: ({localAngularVelocity.x:F3}, {localAngularVelocity.y:F3}, {localAngularVelocity.z:F3}) рад/с (magnitude: {localAngularVelocity.magnitude:F3})");
                Debug.Log($"    localDesiredAngularVelocity: ({localDesiredAngularVelocity.x:F3}, {localDesiredAngularVelocity.y:F3}, {localDesiredAngularVelocity.z:F3}) рад/с (magnitude: {localDesiredAngularVelocity.magnitude:F3})");
                Debug.Log($"    targetAngularVelocity: ({targetAngularVelocity.x:F3}, {targetAngularVelocity.y:F3}, {targetAngularVelocity.z:F3}) рад/с (magnitude: {targetAngularVelocity.magnitude:F3})");
                Debug.Log($"  Коррекции:");
                Debug.Log($"    rollCorrection: {rollCorrection:F3}, yawCorrection: {yawCorrection:F3}, pitchCorrection: {pitchCorrection:F3}");
                Debug.Log($"    correctionMultiplier: {correctionMultiplier:F2}, maxCorrectionAbsolute: {maxCorrectionAbsolute:F3} (70% от baseThrust={effectiveBaseThrustForCorrection:F3})");
                Debug.Log($"    baseThrust: {baseThrust:F3}, effectiveBaseThrust: {effectiveBaseThrust:F3}");
                Debug.Log($"  Тяга двигателей:");
                bool logHasQuad = TryGetEngineQuadrants(out int logFrontLeft, out int logFrontRight, out int logBackLeft, out int logBackRight);
                if (!logHasQuad)
                {
                    logFrontLeft = 0;
                    logFrontRight = 1;
                    logBackLeft = 2;
                    logBackRight = 3;
                }
                Debug.Log($"    FL({logFrontLeft}): {targetEngineThrusts[logFrontLeft]:F3} (base={effectiveBaseThrust:F3} + roll={rollCorrection:F3} - pitch={pitchCorrection:F3} - yaw={yawCorrection:F3})");
                Debug.Log($"    FR({logFrontRight}): {targetEngineThrusts[logFrontRight]:F3} (base={effectiveBaseThrust:F3} - roll={rollCorrection:F3} - pitch={pitchCorrection:F3} + yaw={yawCorrection:F3})");
                Debug.Log($"    BL({logBackLeft}): {targetEngineThrusts[logBackLeft]:F3} (base={effectiveBaseThrust:F3} + roll={rollCorrection:F3} + pitch={pitchCorrection:F3} - yaw={yawCorrection:F3})");
                Debug.Log($"    BR({logBackRight}): {targetEngineThrusts[logBackRight]:F3} (base={effectiveBaseThrust:F3} - roll={rollCorrection:F3} + pitch={pitchCorrection:F3} + yaw={yawCorrection:F3})");
                Debug.Log($"  ЛОГИКА КОРРЕКЦИЙ:");
                Debug.Log($"    Roll: {rollCorrection:F3} (roll > 0 → увеличиваем FL, BL; roll < 0 → увеличиваем FR, BR)");
                Debug.Log($"    Pitch: {pitchCorrection:F3} (pitch > 0 → увеличиваем FL, FR; pitch < 0 → увеличиваем BL, BR)");
                Debug.Log($"    Yaw: {yawCorrection:F3} (yaw > 0 → увеличиваем FR, BR; yaw < 0 → увеличиваем FL, BL)");
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
        
        // Рыскание через поворот двигателей (только если не идет выравнивание по нормали)
        // (Если двигателей >= 4, yaw уже управляется выше внутри блока)
        if (!isAligningToSurface && engineCount < 4)
        {
            Vector2 movementDirection = shipController.GetMovementDirection();
            movementDirection.x += yawCorrection * 0.5f; // Рыскание (поворот влево/вправо)
            movementDirection.x = Mathf.Clamp(movementDirection.x, -1f, 1f);
            shipController.SetMovementDirection(movementDirection);
        }
        
        if (showRotationDebug && Time.frameCount % rotationDebugFrameInterval == 0)
        {
            Debug.Log(
                $"Autopilot Rotation: phase={currentPhase}, " +
                $"upErr={orientationError:F2}°, " +
                $"angVel(local)={localAngularVelocity.x:F3},{localAngularVelocity.y:F3},{localAngularVelocity.z:F3}, " +
                $"corr={rollCorrection:F3},{yawCorrection:F3},{pitchCorrection:F3}, " +
                $"invert={invertRotationCorrection}, swapRP={swapRollPitchAxes}, pauseMove={movementPausedForRotation}, moveScale={movementDampingFactor:F2}"
            );
        }
    }
    
    private void ApplyMovementPauseIfNeeded()
    {
        if (!dampMovementWhenUnstable)
        {
            movementPausedForRotation = false;
            movementDampingFactor = 1f;
            return;
        }
        
        Transform shipTransform = shipController.transform;
        float upError = Vector3.Angle(shipTransform.up, Vector3.up);
        
        if (upError >= pauseMovementAboveAngle)
        {
            movementDampingFactor = minMovementScaleWhenUnstable;
        }
        else if (upError <= resumeMovementBelowAngle)
        {
            movementDampingFactor = 1f;
        }
        else
        {
            float t = Mathf.InverseLerp(pauseMovementAboveAngle, resumeMovementBelowAngle, upError);
            movementDampingFactor = Mathf.Lerp(minMovementScaleWhenUnstable, 1f, t);
        }
        
        movementPausedForRotation = movementDampingFactor <= 0.01f;
        
        Vector2 currentMovement = shipController.GetMovementDirection();
        if (currentMovement.sqrMagnitude > 0f && movementDampingFactor < 0.999f)
        {
            shipController.SetMovementDirection(currentMovement * movementDampingFactor);
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
