using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LandingAutopilot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShipController shipController;
    [SerializeField] private LandingRadar landingRadar;
    
    [Header("Speed Control")]
    [SerializeField] private float maxFallSpeed = 10f; // Максимальная скорость падения (м/с)
    [SerializeField] private float approachSpeed = 15f; // Скорость приближения (м/с)
    [SerializeField] private float brakingDistance = 100f; // Расстояние для начала торможения (м)
    [SerializeField] private float brakingSpeed = 3f; // Скорость при торможении (м/с)
    [SerializeField] private float landingSpeed = 0.5f; // Скорость финальной посадки (м/с)
    
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
        
        // Включаем минимальную тягу для контроля падения
        float hoverThrust = CalculateHoverThrust();
        shipController.SetThrust(hoverThrust);
        shipController.SetMovementDirection(Vector2.zero);
        
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
        
        // Управление: медленное движение к цели + контроль скорости
        ControlFallSpeed(brakingSpeed);
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
    /// Контролирует скорость падения (максимум maxFallSpeed)
    /// </summary>
    private void ControlFallSpeed(float maxSpeed = -1f)
    {
        if (maxSpeed < 0f) maxSpeed = maxFallSpeed;
        
        Vector3 shipVelocity = shipController.GetVelocity();
        float currentVerticalSpeed = shipVelocity.y;
        
        // Если падаем слишком быстро, увеличиваем тягу
        float targetVerticalSpeed = -maxSpeed; // Отрицательная = вниз
        float verticalSpeedError = targetVerticalSpeed - currentVerticalSpeed;
        
        float hoverThrust = CalculateHoverThrust();
        float verticalCorrection = verticalSpeedPID.Update(targetVerticalSpeed, currentVerticalSpeed, Time.deltaTime);
        
        // Корректируем тягу для контроля скорости
        float totalThrust = Mathf.Clamp01(hoverThrust + verticalCorrection * 0.3f);
        
        shipController.SetThrust(totalThrust);
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
        // 1. Вычисляем желаемую скорость к цели (в локальных координатах)
        // 2. Вычисляем текущую скорость (в локальных координатах)
        // 3. Вычисляем ошибку скорости
        // 4. Управляем двигателями для компенсации ошибки
        
        // Преобразуем горизонтальную разницу в локальные координаты
        Vector3 localHorizontalDelta = shipTransform.InverseTransformDirection(worldHorizontalDelta);
        
        // Вычисляем желаемую скорость к цели (пропорционально расстоянию, но ограниченную maxSpeed)
        float desiredSpeed = Mathf.Min(horizontalDistance * 0.3f, maxSpeed);
        Vector2 desiredVelocityLocal = new Vector2(localHorizontalDelta.x, localHorizontalDelta.z);
        if (desiredVelocityLocal.magnitude > 0.001f)
        {
            desiredVelocityLocal = desiredVelocityLocal.normalized * desiredSpeed;
        }
        else
        {
            desiredVelocityLocal = Vector2.zero;
        }
        
        // Вычисляем текущую горизонтальную скорость в локальных координатах
        Vector3 localVelocity3D = shipTransform.InverseTransformDirection(new Vector3(shipVelocity.x, 0f, shipVelocity.z));
        Vector2 currentVelocityLocal = new Vector2(localVelocity3D.x, localVelocity3D.z);
        
        // Вычисляем ошибку скорости
        Vector2 velocityError = desiredVelocityLocal - currentVelocityLocal;
        
        // Используем отдельные PID контроллеры для каждой оси
        float correctionX = horizontalSpeedPIDX.Update(desiredVelocityLocal.x, currentVelocityLocal.x, Time.deltaTime);
        float correctionZ = horizontalSpeedPIDZ.Update(desiredVelocityLocal.y, currentVelocityLocal.y, Time.deltaTime);
        
        // Преобразуем коррекцию в направление для SetMovementDirection
        // PID уже возвращает значение в диапазоне [-1, 1] благодаря SetOutputLimits
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
            Debug.Log($"  Желаемая скорость (локальная): ({desiredVelocityLocal.x:F2}, {desiredVelocityLocal.y:F2}) м/с");
            Debug.Log($"  Текущая скорость (локальная): ({currentVelocityLocal.x:F2}, {currentVelocityLocal.y:F2}) м/с");
            Debug.Log($"  Ошибка скорости: ({velocityError.x:F2}, {velocityError.y:F2}) м/с");
            Debug.Log($"  PID коррекция: X={correctionX:F3}, Z={correctionZ:F3}");
            Debug.Log($"  Направление двигателей: ({movementDirection.x:F3}, {movementDirection.y:F3})");
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
