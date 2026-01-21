using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LandingAutopilot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShipController shipController;
    [SerializeField] private LandingRadar landingRadar;
    
    [Header("Speed Control")]
    [SerializeField] private float maxFallSpeed = 10f; 
    [SerializeField] private float brakingStartHeight = 300f; 
    [SerializeField] private float brakingSpeed = 10f; 
    [SerializeField] private float slowFallHeight = 100f; 
    [SerializeField] private float slowFallSpeed = 5f; 
    [SerializeField] private float finalLandingSpeed = 3f; 
    [SerializeField] private float approachSpeed = 15f; 
    [SerializeField] private float brakingDistance = 100f; 
    [SerializeField] private float landingSpeed = 0.5f; 
    
    [Header("Approach Alignment")]
    [SerializeField] private bool holdDescentUntilOverPoint = true;
    [SerializeField] private float overPointHeight = 150f; 
    [SerializeField] private float overPointHorizontalTolerance = 15f; 
    [SerializeField] private float maxDescentSpeedWhenNotAligned = 0.5f; 
    [SerializeField] private float overPointHorizontalSpeed = 25f; 
    
    [Header("Landing Stop")]
    [SerializeField] private float landingTransitionHeight = 3f; 
    [SerializeField] private float landingTransitionHorizontalTolerance = 5f; 
    [SerializeField] private float landingStopHeight = 0.5f; 
    [SerializeField] private float landingStopVerticalSpeed = 0.5f; 
    [SerializeField] private float landingStopHorizontalSpeed = 1.0f; 
    
    [Header("Orientation Control")]
    [SerializeField] private float orientationSmoothing = 5f; 
    [SerializeField] private float maxOrientationAngle = 5f; 
    [SerializeField] private float alignmentStartHeight = 150f; 
    
    [Header("Geometry Thrust Stabilization")]
    [SerializeField] private bool useGeometryThrustStabilization = false;
    [SerializeField] private float geometryTorqueStrength = 2000f;
    [SerializeField] private float geometryTorqueDamping = 200f;
    [SerializeField] private float maxGeometryThrustDelta = 0.25f;
    
    [Header("Thrust Smoothing")]
    [SerializeField] private float thrustChangeRate = 2f; 
    [SerializeField] private float rotationStabilizationStrength = 1.0f; 
    
    [Header("PID Controllers")]
    [SerializeField] private PIDController verticalSpeedPID = new PIDController(0.5f, 0.05f, 0.2f);
    [SerializeField] private PIDController horizontalSpeedPIDX = new PIDController(0.3f, 0.02f, 0.15f); 
    [SerializeField] private PIDController horizontalSpeedPIDZ = new PIDController(0.3f, 0.02f, 0.15f); 
    [SerializeField] private PIDController orientationPID = new PIDController(2f, 0.1f, 0.5f); 
    [SerializeField] private PIDController rollStabilizationPID = new PIDController(2.0f, 0.1f, 0.4f); 
    [SerializeField] private PIDController yawStabilizationPID = new PIDController(2.0f, 0.1f, 0.4f); 
    [SerializeField] private PIDController pitchStabilizationPID = new PIDController(4.0f, 0.2f, 0.8f); 
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool invertRotationCorrection = false;
    
    [Header("Rotation vs Movement")]
    [SerializeField] private bool dampMovementWhenUnstable = true;
    [SerializeField] private float pauseMovementAboveAngle = 6f;
    [SerializeField] private float resumeMovementBelowAngle = 3f;
    [SerializeField] private float minMovementScaleWhenUnstable = 0.2f;
    
    [Header("Rotation Stabilization Mode")]
    [SerializeField] private bool useStabilizationMode = true;
    [SerializeField] private float stabilizationAngleEnter = 10f;
    [SerializeField] private float stabilizationAngleExit = 6f;
    [SerializeField] private float stabilizationTorqueMultiplier = 1.8f;
    [SerializeField] private float stabilizationDampingMultiplier = 1.6f;
    [SerializeField] private float stabilizationMaxDeltaMultiplier = 1.4f;
    
    public enum LandingPhase
    {
        None,           
        WaitingForSite, 
        Approaching,    
        Braking,        
        Landing         
    }
    
    private LandingPhase currentPhase = LandingPhase.None;
    private bool isActive = false;
    private LandingSite targetSite = null;
    private Vector3 initialScanPosition; 
    
    private float currentThrust = 0f; 
    private float[] currentEngineThrusts = new float[4]; 
    private bool isAligningToSurface = false; 
    private bool movementPausedForRotation = false;
    private float movementDampingFactor = 1f;
    private bool isRotationStabilizationMode = false;
    
    public delegate void AutopilotStateChangedDelegate(bool isActive);
    public event AutopilotStateChangedDelegate OnAutopilotStateChanged;
    
    public delegate void LandingPhaseChangedDelegate(LandingPhase phase);
    public event LandingPhaseChangedDelegate OnLandingPhaseChanged;
    
    private void Start()
    {
        if (shipController == null)
        {
            shipController = FindObjectOfType<ShipController>();
        }
        
        if (landingRadar == null)
        {
            landingRadar = FindObjectOfType<LandingRadar>();
        }
        
        if (shipController == null)
        {
            Debug.LogError("автопилот: shipController не найден");
            enabled = false;
            return;
        }
        
        if (landingRadar == null)
        {
            Debug.LogError("автопилот: landingRadar не найден");
            enabled = false;
            return;
        }
        
        if (brakingSpeed < 8f || brakingSpeed > 15f)
        {
            Debug.LogWarning($"автопилот: brakingSpeed имеет неправильное значение ({brakingSpeed}м/с). Устанавливаю правильное значение: 10м/с");
            brakingSpeed = 10f;
        }
        
        if (slowFallSpeed < 4f || slowFallSpeed > 8f)
        {
            Debug.LogWarning($"автопилот: slowFallSpeed имеет неправильное значение ({slowFallSpeed}м/с). Устанавливаю правильное значение: 5м/с");
            slowFallSpeed = 5f;
        }
        
        if (finalLandingSpeed < 2f || finalLandingSpeed > 5f)
        {
            Debug.LogWarning($"автопилот: finalLandingSpeed имеет неправильное значение ({finalLandingSpeed}м/с). Устанавливаю правильное значение: 3м/с");
            finalLandingSpeed = 3f;
        }
        
        if (alignmentStartHeight < 100f || alignmentStartHeight > 500f)
        {
            Debug.LogWarning($"автопилот: alignmentStartHeight имеет неправильное значение ({alignmentStartHeight}м). Устанавливаю правильное значение: 300м");
            alignmentStartHeight = 300f;
        }
        
        InitializePIDControllers();
        
        if (showDebugInfo)
        {
            Debug.Log("автопилот: инициализирован успешно");
        }
    }
    
    private void Awake()
    {
        InitializePIDControllers();
    }
    
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
        pauseMovementAboveAngle = Mathf.Max(0.1f, pauseMovementAboveAngle);
        resumeMovementBelowAngle = Mathf.Clamp(resumeMovementBelowAngle, 0.05f, pauseMovementAboveAngle);
        minMovementScaleWhenUnstable = Mathf.Clamp01(minMovementScaleWhenUnstable);
        stabilizationAngleExit = Mathf.Clamp(stabilizationAngleExit, 0.1f, stabilizationAngleEnter);
        
        UpdateRotationStabilizationMode();
        
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
    
    private void UpdateRotationStabilizationMode()
    {
        if (!useStabilizationMode || shipController == null)
        {
            isRotationStabilizationMode = false;
            return;
        }
        
        float upError = Vector3.Angle(shipController.transform.up, Vector3.up);
        if (isRotationStabilizationMode)
        {
            if (upError <= stabilizationAngleExit)
            {
                isRotationStabilizationMode = false;
            }
        }
        else
        {
            if (upError >= stabilizationAngleEnter)
            {
                isRotationStabilizationMode = true;
            }
        }
    }
    
    private float GetTargetSurfaceHeight()
    {
        if (targetSite == null)
        {
            return shipController != null ? shipController.transform.position.y : 0f;
        }
        
        if (landingRadar != null)
        {
            return landingRadar.GetSurfaceHeightAtPosition(targetSite.position);
        }
        
        return targetSite.position.y;
    }
    
    private float GetShipBottomHeight()
    {
        if (shipController == null)
        {
            return transform.position.y;
        }
        
        Collider[] colliders = shipController.GetComponentsInChildren<Collider>();
        float minY = float.MaxValue;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null || !col.enabled || col.isTrigger)
            {
                continue;
            }
            
            float bottom = col.bounds.min.y;
            if (bottom < minY)
            {
                minY = bottom;
            }
        }
        
        if (minY == float.MaxValue)
        {
            return shipController.transform.position.y;
        }
        
        return minY;
    }
    
    public void StartLanding()
    {
        if (isActive)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("автопилот: автопилот уже активен");
            }
            return;
        }
        
        if (shipController == null)
        {
            shipController = FindObjectOfType<ShipController>();
            if (shipController == null)
            {
                Debug.LogError("автопилот: shipController не найден Невозможно запустить автопилот.");
                return;
            }
        }
        
        if (landingRadar == null)
        {
            landingRadar = FindObjectOfType<LandingRadar>();
            if (landingRadar == null)
            {
                Debug.LogError("автопилот: landingRadar не найден Невозможно запустить автопилот.");
                return;
            }
        }
        
        float maxTWR = shipController.GetMaxTWR();
        if (maxTWR < 1.0f)
        {
            Debug.LogError($"автопилот: невозможно запустить автопилот Максимальный TWR < 1.0 ({maxTWR:F2}) - корабль не может остановить падение.");
            return;
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"автопилот: максимальный TWR проверен: {maxTWR:F2} (OK)");
        }
        
        shipController.SetAutopilotActive(true);
        
        InitializePIDControllers();
        
        initialScanPosition = shipController.transform.position;
        
        Vector3 initialVelocity = shipController.GetVelocity();
        float initialVerticalSpeed = initialVelocity.y;
        Vector2 initialHorizontalVelocity = new Vector2(initialVelocity.x, initialVelocity.z);
        float initialHorizontalSpeed = initialHorizontalVelocity.magnitude;
        
        if (showDebugInfo)
        {
            Debug.Log($"автопилот: начальная скорость при включении:");
            Debug.Log($"Вертикальная (Y): {initialVerticalSpeed:F2} м/с");
            Debug.Log($"Горизонтальная (X, Z): ({initialVelocity.x:F2}, {initialVelocity.z:F2}) м/с, Magnitude: {initialHorizontalSpeed:F2} м/с");
        }
        
        if (initialVerticalSpeed < -maxFallSpeed)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning($"автопилот: ️ Начальная скорость падения слишком высокая: {initialVerticalSpeed:F2} м/с (максимум: {-maxFallSpeed:F2} м/с)");
            }
        }
        
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
                Debug.LogWarning($"автопилот: ️ Начальная горизонтальная скорость: {initialHorizontalSpeed:F2} м/с. Компенсирую: {compensationDirection * compensationStrength}");
            }
        }
        else
        {
            shipController.SetMovementDirection(Vector2.zero);
        }
        
        if (initialVerticalSpeed > 0f)
        {
            
            currentThrust = 0f;
        }
        else
        {
            
            float hoverThrust = CalculateHoverThrust();
            currentThrust = hoverThrust;
        }
        
        int engineCount = shipController.GetEngineCount();
        EnsureEngineThrustArray();
        for (int i = 0; i < engineCount; i++)
        {
            currentEngineThrusts[i] = currentThrust;
            shipController.SetEngineThrust(i, currentThrust);
        }
        
        isActive = true;
        currentPhase = LandingPhase.WaitingForSite;
        targetSite = null;
        
        OnAutopilotStateChanged?.Invoke(true);
        OnLandingPhaseChanged?.Invoke(currentPhase);
        
        if (showDebugInfo)
        {
            Debug.Log("автопилот: автопилот запущен. Ожидаю визуализации точек радаром.");
        }
    }
    
    public void StopLanding()
    {
        if (!isActive) return;
        
        shipController.SetAutopilotActive(false);
        
        shipController.SetThrust(0f);
        shipController.SetMovementDirection(Vector2.zero);
        
        isActive = false;
        currentPhase = LandingPhase.None;
        targetSite = null;
        
        OnAutopilotStateChanged?.Invoke(false);
        OnLandingPhaseChanged?.Invoke(currentPhase);
        
        if (showDebugInfo)
        {
            Debug.Log("автопилот: автопилот остановлен.");
        }
    }
    
    public bool IsActive()
    {
        return isActive;
    }
    
    public LandingPhase GetCurrentPhase()
    {
        return currentPhase;
    }
    
    public LandingSite GetTargetSite()
    {
        return targetSite;
    }
    
    private void UpdateWaitingForSite()
    {
        
        ControlFallSpeed();
        
        List<LandingSite> sites = landingRadar != null ? landingRadar.GetFoundSites() : null;
        
        if (sites == null || sites.Count == 0)
        {
            if (showDebugInfo && Time.frameCount % 120 == 0)
            {
                Debug.Log($"автопилот: ожидаю визуализации точек радаром. (радар: {(landingRadar == null ? "НЕ НАЙДЕН" : "найден")})");
            }
            return;
        }
        
        if (showDebugInfo && Time.frameCount % 120 == 0)
        {
            Debug.Log($"автопилот: найдено {sites.Count} площадок. Проверяю визуализацию.");
        }
        
        bool hasVisualized = landingRadar.HasVisualizedSites();
        if (!hasVisualized)
        {
            if (showDebugInfo && Time.frameCount % 120 == 0)
            {
                Debug.Log($"автопилот: точки найдены ({sites.Count}), но еще не визуализированы. Ожидаю.");
            }
            return;
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"автопилот: точки визуализированы Выбираю лучшую площадку из {sites.Count}.");
        }
        
        LandingSite selectedSite = SelectBestLandingSite(sites);
        
        if (selectedSite == null)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("автопилот: не удалось выбрать площадку для посадки");
            }
            return; 
        }
        
        targetSite = selectedSite;
        currentPhase = LandingPhase.Approaching;
        OnLandingPhaseChanged?.Invoke(currentPhase);
        
        Vector3 currentShipPos = shipController.transform.position;
        float actualDistance = Vector3.Distance(currentShipPos, targetSite.position);
        
        if (showDebugInfo)
        {
            Debug.Log($"автопилот: === ВЫБРАНА ПЛОЩАДКА ===");
            Debug.Log($"Позиция корабля: ({currentShipPos.x:F2}, {currentShipPos.y:F2}, {currentShipPos.z:F2})");
            Debug.Log($"Позиция площадки: ({targetSite.position.x:F2}, {targetSite.position.y:F2}, {targetSite.position.z:F2})");
            Debug.Log($"Расстояние: {actualDistance:F1}м");
            Debug.Log($"Пригодность: {targetSite.suitabilityScore * 100f:F0}%");
            Debug.Log($"Размер площадки: {targetSite.size:F1}м");
            Debug.Log($"Направление к площадке (мировые): ({targetSite.position.x - currentShipPos.x:F2}, {targetSite.position.z - currentShipPos.z:F2})");
        }
    }
    
    private void UpdateApproaching()
    {
        
        if (!IsSiteAvailable(targetSite))
        {
            
            TrySwitchToNewSite();
            return;
        }
        
        Vector3 shipPosition = shipController.transform.position;
        float targetSurfaceHeight = GetTargetSurfaceHeight();
        Vector3 targetPosition = new Vector3(targetSite.position.x, targetSurfaceHeight, targetSite.position.z);
        Vector3 shipVelocity = shipController.GetVelocity();
        
        float horizontalDistance = Vector2.Distance(
            new Vector2(shipPosition.x, shipPosition.z),
            new Vector2(targetPosition.x, targetPosition.z)
        );
        float totalDistance = Vector3.Distance(shipPosition, targetPosition);
        
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"автопилот: === ПРИБЛИЖЕНИЕ ===");
            Debug.Log($"корабль: ({shipPosition.x:F1}, {shipPosition.y:F1}, {shipPosition.z:F1})");
            Debug.Log($"Цель: ({targetPosition.x:F1}, {targetPosition.y:F1}, {targetPosition.z:F1})");
            Debug.Log($"Расстояние: {totalDistance:F1}м");
            Debug.Log($"Скорость корабля: ({shipVelocity.x:F2}, {shipVelocity.y:F2}, {shipVelocity.z:F2}) м/с");
            Debug.Log($"Направление к цели (мировые): ({targetPosition.x - shipPosition.x:F1}, {targetPosition.z - shipPosition.z:F1})");
        }
        
        if (totalDistance <= brakingDistance)
        {
            currentPhase = LandingPhase.Braking;
            OnLandingPhaseChanged?.Invoke(currentPhase);
            if (showDebugInfo)
            {
                Debug.Log($"автопилот: переход к фазе торможения (расстояние: {totalDistance:F1}м)");
            }
            return;
        }
        
        float verticalDistance = shipPosition.y - targetSurfaceHeight;
        bool holdForOverPoint = holdDescentUntilOverPoint
            && verticalDistance <= overPointHeight
            && horizontalDistance > overPointHorizontalTolerance;
        if (holdForOverPoint)
        {
            ControlFallSpeed(maxDescentSpeedWhenNotAligned);
            MoveTowardsTarget(targetPosition, overPointHorizontalSpeed);
        }
        else
        {
        ControlFallSpeed();
        MoveTowardsTarget(targetPosition, approachSpeed);
        }
        ApplyMovementPauseIfNeeded();
        
        if (verticalDistance <= alignmentStartHeight && targetSite != null)
        {
            if (showDebugInfo && Time.frameCount % 60 == 0)
            {
                Debug.Log($"автопилот: выравнивание по нормали поверхности включено (высота: {verticalDistance:F1}м, alignmentStartHeight: {alignmentStartHeight:F1}м)");
            }
            AlignToSurfaceNormal(targetSite.surfaceNormal);
        }
        else if (showDebugInfo && Time.frameCount % 60 == 0 && targetSite != null)
        {
            Debug.Log($"автопилот: выравнивание НЕ включено (высота: {verticalDistance:F1}м > alignmentStartHeight: {alignmentStartHeight:F1}м)");
        }
        
        StabilizeRotation(Vector3.up);
    }
    
    private void UpdateBraking()
    {
        
        if (!IsSiteAvailable(targetSite))
        {
            TrySwitchToNewSite();
            return;
        }
        
        Vector3 shipPosition = shipController.transform.position;
        float targetSurfaceHeight = GetTargetSurfaceHeight();
        Vector3 targetPosition = new Vector3(targetSite.position.x, targetSurfaceHeight, targetSite.position.z);
        Vector3 shipVelocity = shipController.GetVelocity();
        
        float horizontalDistance = Vector2.Distance(
            new Vector2(shipPosition.x, shipPosition.z),
            new Vector2(targetPosition.x, targetPosition.z)
        );
        float verticalDistance = shipPosition.y - targetSurfaceHeight;
        float totalDistance = Vector3.Distance(shipPosition, targetPosition);
        float shipSurfaceHeight = landingRadar != null ? landingRadar.GetSurfaceHeightAtPosition(shipPosition) : targetSurfaceHeight;
        float shipBottomHeight = GetShipBottomHeight();
        float shipHeightAboveSurface = shipBottomHeight - shipSurfaceHeight;
        
        if ((totalDistance < 5f && Mathf.Abs(verticalDistance) < 3f) ||
            (shipHeightAboveSurface <= landingTransitionHeight && horizontalDistance <= landingTransitionHorizontalTolerance))
        {
            currentPhase = LandingPhase.Landing;
            OnLandingPhaseChanged?.Invoke(currentPhase);
            if (showDebugInfo)
            {
                Debug.Log($"автопилот: переход к фазе финальной посадки (расстояние: {totalDistance:F1}м)");
            }
            return;
        }
        
        if (verticalDistance <= alignmentStartHeight)
        {
            AlignToSurfaceNormal(targetSite.surfaceNormal);
        }
        
        bool holdForOverPoint = holdDescentUntilOverPoint
            && verticalDistance <= overPointHeight
            && horizontalDistance > overPointHorizontalTolerance;
        if (holdForOverPoint)
        {
            ControlFallSpeed(maxDescentSpeedWhenNotAligned);
            MoveTowardsTarget(targetPosition, overPointHorizontalSpeed);
        }
        else
        {
        ControlFallSpeed();
        MoveTowardsTarget(targetPosition, brakingSpeed);
        }
        ApplyMovementPauseIfNeeded();
        
        StabilizeRotation(Vector3.up);
    }
    
    private void UpdateLanding()
    {
        
        if (!IsSiteAvailable(targetSite))
        {
            TrySwitchToNewSite();
            return;
        }
        
        Vector3 shipPosition = shipController.transform.position;
        float targetSurfaceHeight = GetTargetSurfaceHeight();
        Vector3 targetPosition = new Vector3(targetSite.position.x, targetSurfaceHeight, targetSite.position.z);
        Vector3 shipVelocity = shipController.GetVelocity();
        
        float horizontalDistance = Vector2.Distance(
            new Vector2(shipPosition.x, shipPosition.z),
            new Vector2(targetPosition.x, targetPosition.z)
        );
        float verticalDistance = shipPosition.y - targetSurfaceHeight;
        float totalDistance = Vector3.Distance(shipPosition, targetPosition);
        float shipSurfaceHeight = landingRadar != null ? landingRadar.GetSurfaceHeightAtPosition(shipPosition) : targetSurfaceHeight;
        float shipBottomHeight = GetShipBottomHeight();
        float shipHeightAboveSurface = shipBottomHeight - shipSurfaceHeight;
        float horizontalSpeed = new Vector2(shipVelocity.x, shipVelocity.z).magnitude;
        
        if (totalDistance < 0.5f || verticalDistance < 0.2f ||
            (shipHeightAboveSurface <= landingStopHeight &&
             Mathf.Abs(shipVelocity.y) <= landingStopVerticalSpeed &&
             horizontalSpeed <= landingStopHorizontalSpeed))
        {
            shipController.SetThrust(0f);
            shipController.SetMovementDirection(Vector2.zero);
            StopLanding();
            if (showDebugInfo)
            {
                Debug.Log("автопилот: посадка завершена");
            }
            return;
        }
        
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            bool stopBySurface = shipHeightAboveSurface <= landingStopHeight
                && Mathf.Abs(shipVelocity.y) <= landingStopVerticalSpeed
                && horizontalSpeed <= landingStopHorizontalSpeed;
            Debug.Log($"автопилот: landing check — hAboveSurface={shipHeightAboveSurface:F2} (<= {landingStopHeight})," +
                      $"shipBottomY={shipBottomHeight:F2}, surfaceY={shipSurfaceHeight:F2}, " +
                      $"vY={shipVelocity.y:F2} (<= {landingStopVerticalSpeed}), hSpeed={horizontalSpeed:F2} (<= {landingStopHorizontalSpeed}), " +
                      $"stopBySurface={stopBySurface}, totalDist={totalDistance:F2}, vertDist={verticalDistance:F2}");
        }
        
        AlignToSurfaceNormal(targetSite.surfaceNormal);
        
        ControlFallSpeed(landingSpeed);
        MoveTowardsTarget(targetPosition, landingSpeed);
        ApplyMovementPauseIfNeeded();
        
        StabilizeRotation(Vector3.up);
    }
    
    private void ControlFallSpeed(float maxSpeed = -1f)
    {
        Vector3 shipVelocity = shipController.GetVelocity();
        float currentVerticalSpeed = shipVelocity.y;
        
        float verticalDistance = float.MaxValue;
        float horizontalDistance = float.MaxValue;
        if (targetSite != null)
        {
            Vector3 shipPosition = shipController.transform.position;
            float targetSurfaceHeight = GetTargetSurfaceHeight();
            verticalDistance = shipPosition.y - targetSurfaceHeight;
            horizontalDistance = Vector2.Distance(
                new Vector2(shipPosition.x, shipPosition.z),
                new Vector2(targetSite.position.x, targetSite.position.z)
            );
        }
        else
        {
            
            verticalDistance = shipController.transform.position.y;
        }
        
        float targetMaxSpeed;
        
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"падение: verticalDistance={verticalDistance:F1}м, brakingStartHeight={brakingStartHeight}м, slowFallHeight={slowFallHeight}м");
        }
        
        if (verticalDistance > brakingStartHeight)
        {
            
            float maxTWR = shipController.GetMaxTWR();
            float gravity = shipController.GetGravityStrength();
            
            float distanceToBrakingStart = verticalDistance - brakingStartHeight;
            
            float brakingAcceleration = (maxTWR - 1f) * gravity; 
            float targetSpeedAtBrakingStart = brakingSpeed; 
            
            float safeBrakingAcceleration = brakingAcceleration * 0.9f;
            float maxSafeSpeedSquared = targetSpeedAtBrakingStart * targetSpeedAtBrakingStart + 2f * safeBrakingAcceleration * distanceToBrakingStart;
            
            if (maxSafeSpeedSquared > 0f)
            {
                targetMaxSpeed = Mathf.Sqrt(maxSafeSpeedSquared);
                
                targetMaxSpeed = Mathf.Min(targetMaxSpeed, 100f);
            }
            else
            {
                
                targetMaxSpeed = brakingSpeed;
            }
        }
        else if (verticalDistance > slowFallHeight)
        {
            
            float distanceFromBrakingStart = verticalDistance - slowFallHeight; 
            float totalBrakingDistance = brakingStartHeight - slowFallHeight; 
            float t = distanceFromBrakingStart / totalBrakingDistance; 
            t = Mathf.Clamp01(t);
            targetMaxSpeed = Mathf.Lerp(slowFallSpeed, brakingSpeed, t); 
        }
        else
        {
            
            float t = verticalDistance / slowFallHeight; 
            t = Mathf.Clamp01(t);
            targetMaxSpeed = Mathf.Lerp(finalLandingSpeed, slowFallSpeed, t);
        }
        
        if (maxSpeed > 0f)
        {
            targetMaxSpeed = maxSpeed;
        }
        
        if (targetSite != null && holdDescentUntilOverPoint && verticalDistance <= overPointHeight
            && horizontalDistance > overPointHorizontalTolerance)
        {
            targetMaxSpeed = Mathf.Min(targetMaxSpeed, maxDescentSpeedWhenNotAligned);
        }
        
        float targetVerticalSpeed = -targetMaxSpeed; 
        float verticalSpeedError = targetVerticalSpeed - currentVerticalSpeed;
        
        float hoverThrust = CalculateHoverThrust();
        float verticalCorrection = verticalSpeedPID.Update(targetVerticalSpeed, currentVerticalSpeed, Time.deltaTime);
        
        float correctionMultiplier = 1f;
        if (Mathf.Abs(verticalSpeedError) > 5f)
        {
            
            correctionMultiplier = Mathf.Lerp(0.3f, 1.0f, Mathf.Clamp01((Mathf.Abs(verticalSpeedError) - 5f) / 10f));
        }
        
        float targetThrust;
        
        if (currentVerticalSpeed > 0f)
        {
            
            targetThrust = 0f;
        }
        else
        {
            
            if (currentVerticalSpeed > targetVerticalSpeed)
            {
                
                float speedDifference = currentVerticalSpeed - targetVerticalSpeed; 
                float normalizedDifference = Mathf.Clamp01(speedDifference / targetMaxSpeed); 
                
                if (normalizedDifference > 0.5f)
                {
                    
                    targetThrust = hoverThrust * (1f - normalizedDifference) * 0.2f; 
                }
                else
                {
                    
                    targetThrust = hoverThrust * (1f - normalizedDifference * 0.5f); 
                }
                
                targetThrust = Mathf.Max(0f, targetThrust);
            }
            else
            {
                
                targetThrust = Mathf.Clamp01(hoverThrust + verticalCorrection * correctionMultiplier);
                
                if (currentVerticalSpeed < -targetMaxSpeed * 1.5f)
                {
                    targetThrust = 1f; 
                }
            }
        }
        
        float maxThrustChange = thrustChangeRate * Time.deltaTime;
        if (currentVerticalSpeed > targetVerticalSpeed && targetThrust < currentThrust)
        {
            
            maxThrustChange *= 3f;
        }
        currentThrust = Mathf.MoveTowards(currentThrust, targetThrust, maxThrustChange);
        
        if (!isAligningToSurface)
        {
            
            int engineCount = shipController.GetEngineCount();
            EnsureEngineThrustArray();
            float maxEngineThrustChange = thrustChangeRate * Time.deltaTime;
            if (currentVerticalSpeed > targetVerticalSpeed && targetThrust < currentThrust)
            {
                
                maxEngineThrustChange *= 3f;
            }
            for (int i = 0; i < engineCount; i++)
            {
                
                currentEngineThrusts[i] = Mathf.MoveTowards(currentEngineThrusts[i], currentThrust, maxEngineThrustChange);
                shipController.SetEngineThrust(i, currentEngineThrusts[i]);
            }
        }
        
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"автопилот: === КОНТРОЛЬ СКОРОСТИ ПАДЕНИЯ ===");
            Debug.Log($"Высота до цели: {verticalDistance:F1}м");
            Debug.Log($"Текущая скорость падения: {currentVerticalSpeed:F2}м/с");
            Debug.Log($"Целевая максимальная скорость: {targetMaxSpeed:F2}м/с");
            Debug.Log($"Целевая скорость (отрицательная): {targetVerticalSpeed:F2}м/с");
            Debug.Log($"Ошибка скорости: {verticalSpeedError:F2}м/с");
            Debug.Log($"hoverThrust: {hoverThrust:F3}");
            Debug.Log($"verticalCorrection (от PID): {verticalCorrection:F3}");
            Debug.Log($"correctionMultiplier: {correctionMultiplier:F3}");
            Debug.Log($"targetThrust: {targetThrust:F3}");
            Debug.Log($"Тяга (текущая): {currentThrust:F3}");
            int engineCount = shipController.GetEngineCount();
            if (engineCount >= 4)
            {
                Debug.Log($"Тяга двигателей: e0={currentEngineThrusts[0]:F3}, E1={currentEngineThrusts[1]:F3}, E2={currentEngineThrusts[2]:F3}, E3={currentEngineThrusts[3]:F3}");
            }
            Debug.Log($"ЛОГИКА: currentVerticalSpeed={currentVerticalSpeed:F2}, targetVerticalSpeed={targetVerticalSpeed:F2}");
            if (currentVerticalSpeed > 0f)
            {
                Debug.Log($"→ Корабль летит ВВЕРХ → targetThrust=0");
            }
            else if (currentVerticalSpeed > targetVerticalSpeed)
            {
                Debug.Log($"→ Падаем МЕДЛЕННЕЕ целевой скорости → УМЕНЬШАЕМ тягу");
            }
            else
            {
                Debug.Log($"→ Падаем БЫСТРЕЕ целевой скорости → УВЕЛИЧИВАЕМ тягу");
            }
            if (verticalDistance > brakingStartHeight)
            {
                Debug.Log($"Режим: бЫСТРОЕ ПАДЕНИЕ (высота > {brakingStartHeight}м)");
            float maxTWR = shipController.GetMaxTWR();
            float gravity = shipController.GetGravityStrength();
            float brakingAcceleration = (maxTWR - 1f) * gravity;
            Debug.Log($"TWR: {maxTWR:F2}, Ускорение торможения: {brakingAcceleration:F2} м/с²");
            if (verticalDistance > brakingStartHeight)
            {
                float distanceToBrakingStart = verticalDistance - brakingStartHeight;
                Debug.Log($"Расстояние до точки торможения: {distanceToBrakingStart:F1}м");
            }
            }
            else if (verticalDistance > slowFallHeight)
            {
                float t = (verticalDistance - slowFallHeight) / (brakingStartHeight - slowFallHeight);
                t = Mathf.Clamp01(t);
                float currentSpeed = Mathf.Lerp(slowFallSpeed, brakingSpeed, t);
                Debug.Log($"Режим: тОРМОЖЕНИЕ (высота {slowFallHeight}-{brakingStartHeight}м)");
                Debug.Log($"ФАКТИЧЕСКИЕ ЗНАЧЕНИЯ: brakingSpeed={brakingSpeed}м/с, slowFallSpeed={slowFallSpeed}м/с, t={t:F2}");
                Debug.Log($"Вычисленная скорость: {currentSpeed:F1}м/с, targetMaxSpeed={targetMaxSpeed:F1}м/с");
            }
            else
            {
                Debug.Log($"Режим: фИНАЛЬНОЕ ЗАМЕДЛЕНИЕ (высота < {slowFallHeight}м, от {slowFallSpeed}м/с до {finalLandingSpeed}м/с)");
            }
        }
    }
    
    private void MoveTowardsTarget(Vector3 targetPosition, float maxSpeed)
    {
        Transform shipTransform = shipController.transform;
        Vector3 shipPosition = shipTransform.position;
        Vector3 shipVelocity = shipController.GetVelocity();
        
        Vector3 worldDelta = (targetPosition - shipPosition);
        Vector3 worldHorizontalDelta = new Vector3(worldDelta.x, 0f, worldDelta.z);
        float horizontalDistance = worldHorizontalDelta.magnitude;
        
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
        
        Vector3 localHorizontalDelta = shipTransform.InverseTransformDirection(worldHorizontalDelta);
        
        localHorizontalDelta.y = 0f; 
        
        float desiredSpeed = Mathf.Min(horizontalDistance * 0.3f, maxSpeed);
        
        Vector2 desiredVelocityLocal = new Vector2(
            localHorizontalDelta.x,  
            localHorizontalDelta.z   
        );
        if (desiredVelocityLocal.magnitude > 0.001f)
        {
            desiredVelocityLocal = desiredVelocityLocal.normalized * desiredSpeed;
        }
        else
        {
            desiredVelocityLocal = Vector2.zero;
        }
        
        Vector3 worldHorizontalVelocity3D = new Vector3(shipVelocity.x, 0f, shipVelocity.z);
        Vector3 localVelocity3D = shipTransform.InverseTransformDirection(worldHorizontalVelocity3D);
        
        localVelocity3D.y = 0f;
        
        Vector2 currentVelocityLocal = new Vector2(
            localVelocity3D.x,  
            localVelocity3D.z    
        );
        
        Vector2 velocityError = desiredVelocityLocal - currentVelocityLocal;
        
        Vector2 desiredDirection = desiredVelocityLocal.magnitude > 0.001f ? desiredVelocityLocal.normalized : Vector2.zero;
        float velocityTowardsTarget = Vector2.Dot(currentVelocityLocal, desiredDirection);
        float currentSpeedMagnitude = currentVelocityLocal.magnitude;
        
        float errorMagnitude = velocityError.magnitude;
        bool movingAwayFromTarget = velocityTowardsTarget < -2f && currentSpeedMagnitude > 5f;
        bool errorTooLarge = errorMagnitude > 20f;
        
        if (movingAwayFromTarget || errorTooLarge)
        {
            
            Vector2 compensationDirection = -currentVelocityLocal.normalized;
            float compensationStrength = Mathf.Min(1f, Mathf.Max(currentSpeedMagnitude / 30f, errorMagnitude / 50f));
            shipController.SetMovementDirection(compensationDirection * compensationStrength);
            
            if (showDebugInfo && Time.frameCount % 60 == 0)
            {
                string reason = movingAwayFromTarget ? "СКОРОСТЬ НАПРАВЛЕНА ОТ ЦЕЛИ" : "ОШИБКА СЛИШКОМ БОЛЬШАЯ";
                Debug.LogWarning($"автопилот: ️ {reason}" +
                               $"Скорость к цели: {velocityTowardsTarget:F2}м/с, " +
                               $"Текущая скорость: ({currentVelocityLocal.x:F2}, {currentVelocityLocal.y:F2}) м/с (Magnitude: {currentSpeedMagnitude:F2}м/с), " +
                               $"Ошибка: {errorMagnitude:F1}м/с, " +
                               $"Компенсация: {compensationDirection * compensationStrength}");
            }
            return;
        }
        
        float correctionX = horizontalSpeedPIDX.Update(desiredVelocityLocal.x, currentVelocityLocal.x, Time.deltaTime);
        
        float correctionZ = horizontalSpeedPIDZ.Update(desiredVelocityLocal.y, currentVelocityLocal.y, Time.deltaTime);
        
        Vector2 movementDirection = new Vector2(correctionX, correctionZ);
        
        if (movementDirection.magnitude > 1f)
        {
            movementDirection = movementDirection.normalized;
        }
        
        shipController.SetMovementDirection(movementDirection);
        
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"автопилот: === УПРАВЛЕНИЕ ПО СКОРОСТИ ===");
            Debug.Log($"Горизонтальное расстояние: {horizontalDistance:F1}м");
            Debug.Log($"МИРОВЫЕ КООРДИНАТЫ (Unity):");
            Debug.Log($"worldHorizontalDelta: x={worldHorizontalDelta.x:F2} (влево/вправо), Y={worldHorizontalDelta.y:F2} (вверх/вниз), Z={worldHorizontalDelta.z:F2} (вперед/назад)");
            Debug.Log($"shipVelocity: x={shipVelocity.x:F2} (влево/вправо), Y={shipVelocity.y:F2} (вверх/вниз), Z={shipVelocity.z:F2} (вперед/назад)");
            Debug.Log($"ЛОКАЛЬНЫЕ КООРДИНАТЫ КОРАБЛЯ:");
            Debug.Log($"localHorizontalDelta: x={localHorizontalDelta.x:F2} (влево/вправо), Y={localHorizontalDelta.y:F2} (вверх/вниз), Z={localHorizontalDelta.z:F2} (вперед/назад)");
            Debug.Log($"localVelocity3D: x={localVelocity3D.x:F2} (влево/вправо), Y={localVelocity3D.y:F2} (вверх/вниз), Z={localVelocity3D.z:F2} (вперед/назад)");
            Debug.Log($": проверка что мы НЕ используем Y (вертикальную ось):");
            Debug.Log($"localHorizontalDelta.y должно быть 0: {localHorizontalDelta.y:F3} {(Mathf.Abs(localHorizontalDelta.y) < 0.001f ?"✓" : "✗ ОШИБКА!")}");
            Debug.Log($"localVelocity3D.y должно быть 0: {localVelocity3D.y:F3} {(Mathf.Abs(localVelocity3D.y) < 0.001f ?"✓" : "✗ ОШИБКА!")}");
            Debug.Log($"СКОРОСТИ (для SetMovementDirection, Vector2):");
            Debug.Log($"desiredVelocityLocal: x={desiredVelocityLocal.x:F2} (локальный X=влево/вправо), y={desiredVelocityLocal.y:F2} (локальный Z=вперед/назад, НЕ Unity Y)");
            Debug.Log($"currentVelocityLocal: x={currentVelocityLocal.x:F2} (локальный X=влево/вправо), y={currentVelocityLocal.y:F2} (локальный Z=вперед/назад, НЕ Unity Y)");
            Debug.Log($"Ошибка: x={velocityError.x:F2}, y={velocityError.y:F2} м/с, Magnitude={errorMagnitude:F2}м/с");
            Debug.Log($"PID КОРРЕКЦИЯ:");
            Debug.Log($"correctionX={correctionX:F3} (для оси X=влево/вправо)");
            Debug.Log($"correctionZ={correctionZ:F3} (для оси Z=вперед/назад)");
            Debug.Log($"НАПРАВЛЕНИЕ ДВИГАТЕЛЕЙ (SetMovementDirection):");
            Debug.Log($"movementDirection.x={movementDirection.x:F3} → desiredMovementDirection.x (влево/вправо) → targetAngleY (поворот влево/вправо)");
            Debug.Log($"movementDirection.y={movementDirection.y:F3} → desiredMovementDirection.y (вперед/назад) → targetAngleX (наклон вперед/назад)");
            Debug.Log($"ПРОВЕРКА НАПРАВЛЕНИЯ:");
            Debug.Log($"Проекция текущей скорости на направление к цели: {velocityTowardsTarget:F2}м/с");
            Debug.Log($"Если > 0 → движемся К цели, если < 0 → движемся ОТ цели");
            Debug.Log($"Текущая скорость (Magnitude): {currentSpeedMagnitude:F2}м/с");
            Debug.Log($"Ошибка скорости (Magnitude): {errorMagnitude:F2}м/с");
            Debug.Log($"ПРОВЕРКА ЗНАКОВ:");
            Debug.Log($"Если localHorizontalDelta.z > 0 (цель впереди) → desiredVelocityLocal.y > 0 → correctionZ > 0 → movementDirection.y > 0");
            Debug.Log($"Если localHorizontalDelta.z < 0 (цель сзади) → desiredVelocityLocal.y < 0 → correctionZ < 0 → movementDirection.y < 0");
            Debug.Log($"Фактически: localHorizontalDelta.z={localHorizontalDelta.z:F3}, desiredVelocityLocal.y={desiredVelocityLocal.y:F3}, movementDirection.y={movementDirection.y:F3}");
            Debug.Log($"ПРОВЕРКА ДВИЖЕНИЯ:");
            Debug.Log($"Движемся от цели: {movingAwayFromTarget}, Ошибка слишком большая: {errorTooLarge}");
            if (movingAwayFromTarget || errorTooLarge)
            {
                Debug.LogWarning($"️ КОМПЕНСАЦИЯ АКТИВНА - сначала останавливаем движение от цели");
            }
        }
        
    }
    
    private void AlignToSurfaceNormal(Vector3 surfaceNormal)
    {
        isAligningToSurface = true; 
        
        Transform shipTransform = shipController.transform;
        
        Vector3 desiredUp = Vector3.up;
        Vector3 currentUp = shipTransform.up;
        Vector3 currentForward = shipTransform.forward;
        Vector3 currentRight = shipTransform.right;
        
        Vector3 desiredForward = Vector3.ProjectOnPlane(currentForward, desiredUp).normalized;
        
        if (desiredForward.magnitude < 0.1f && targetSite != null)
        {
            Vector3 toTarget = (targetSite.position - shipTransform.position).normalized;
            desiredForward = Vector3.ProjectOnPlane(toTarget, desiredUp).normalized;
            
            if (desiredForward.magnitude < 0.1f)
            {
                desiredForward = Vector3.ProjectOnPlane(currentRight, desiredUp).normalized;
            }
            
            if (desiredForward.magnitude < 0.1f)
            {
                
                if (Mathf.Abs(desiredUp.y) > 0.9f)
                {
                    
                    desiredForward = Vector3.ProjectOnPlane(Vector3.forward, desiredUp).normalized;
                }
                else
                {
                    
                    desiredForward = Vector3.ProjectOnPlane(Vector3.up, desiredUp).normalized;
                }
            }
        }
        
        Vector3 desiredRight = Vector3.Cross(desiredForward, desiredUp).normalized;
        
        Quaternion targetRotation = Quaternion.LookRotation(desiredForward, desiredUp);
        
        Quaternion currentRotation = shipTransform.rotation;
        
        Quaternion rotationError = Quaternion.Inverse(currentRotation) * targetRotation;
        
        rotationError.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f; 
        
        Vector3 angularVelocity = shipController.GetAngularVelocity();
        Vector3 localAngularVelocity = shipTransform.InverseTransformDirection(angularVelocity);
        
        float speedMultiplier = angle > 10f ? 0.05f : 0.03f; 
        float targetAngularSpeed = angle * orientationSmoothing * speedMultiplier; 
        targetAngularSpeed = Mathf.Clamp(targetAngularSpeed, -orientationSmoothing, orientationSmoothing);
        
        Vector3 localAxis = shipTransform.InverseTransformDirection(axis);
        
        float rollCorrection = -localAxis.x * targetAngularSpeed;   
        float yawCorrection = -localAxis.y * targetAngularSpeed;    
        float pitchCorrection = -localAxis.z * targetAngularSpeed;  
        
        float rollPID = orientationPID.Update(0f, localAngularVelocity.x, Time.deltaTime);
        float yawPID = orientationPID.Update(0f, localAngularVelocity.y, Time.deltaTime);
        float pitchPID = orientationPID.Update(0f, localAngularVelocity.z, Time.deltaTime);
        
        rollCorrection += rollPID;
        yawCorrection += yawPID;
        pitchCorrection += pitchPID;
        
        float maxCorrection = angle > 10f ? 2f : 1f;
        rollCorrection = Mathf.Clamp(rollCorrection, -maxCorrection, maxCorrection);
        yawCorrection = Mathf.Clamp(yawCorrection, -maxCorrection, maxCorrection);
        pitchCorrection = Mathf.Clamp(pitchCorrection, -maxCorrection, maxCorrection);
        
        float angleError = Vector3.Angle(currentUp, desiredUp);
        
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"автопилот: === ВЫРАВНИВАНИЕ === (ошибка угла: {angleError:F1}°, totalAngle: {angle:F1}°)");
        }
        
        bool isAligned = angleError < maxOrientationAngle && angle < 5f && localAngularVelocity.magnitude < 0.1f;
        
        float alignmentStrength = isAligned ? 0.3f : 1.0f; 
        
        if (isAligned)
        {
            isAligningToSurface = false; 
        }
        
        if (useGeometryThrustStabilization)
        {
            float angleRad = angle * Mathf.Deg2Rad;
            float torqueStrength = geometryTorqueStrength * (isRotationStabilizationMode ? stabilizationTorqueMultiplier : 1f);
            float torqueDamping = geometryTorqueDamping * (isRotationStabilizationMode ? stabilizationDampingMultiplier : 1f);
            float maxDelta = maxGeometryThrustDelta * (isRotationStabilizationMode ? stabilizationMaxDeltaMultiplier : 1f);
            Vector3 desiredTorque = axis.normalized * angleRad * torqueStrength - angularVelocity * torqueDamping;
            bool applied = ApplyTorqueWithThrust(desiredTorque, currentThrust, 0.05f, maxDelta, 5f);
            if (applied)
            {
                return;
            }
        }
        
        int engineCount = shipController.GetEngineCount();
        EnsureEngineThrustArray();
        float[] targetEngineThrusts = new float[engineCount]; 
        float rollStrength = 0f; 
        float pitchStrength = 0f; 
        float yawStrength = 0f; 
        
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
            
            float baseThrust = currentThrust; 
            
            for (int i = 0; i < engineCount; i++)
            {
                targetEngineThrusts[i] = baseThrust;
            }
            
            if (baseThrust <= 0.001f)
            {
                
                for (int i = 0; i < engineCount; i++)
                {
                    targetEngineThrusts[i] = 0f;
                }
            }
            else
            {
                
                float thrustCorrectionStrength = angleError > 10f ? 0.4f : 0.3f; 
                
                rollStrength = rollCorrection * thrustCorrectionStrength * alignmentStrength;
                pitchStrength = pitchCorrection * thrustCorrectionStrength * alignmentStrength;
                yawStrength = yawCorrection * thrustCorrectionStrength * alignmentStrength;
                
                targetEngineThrusts[frontLeft] = baseThrust + rollStrength - pitchStrength - yawStrength;
                targetEngineThrusts[frontRight] = baseThrust - rollStrength - pitchStrength + yawStrength;
                targetEngineThrusts[backLeft] = baseThrust + rollStrength + pitchStrength - yawStrength;
                targetEngineThrusts[backRight] = baseThrust - rollStrength + pitchStrength + yawStrength;
            }
            
            float minEngineThrustForAlignment = angleError > 5f ? 0.02f : 0.05f; 
            float minThrust = Mathf.Min(
                targetEngineThrusts[frontLeft],
                targetEngineThrusts[frontRight],
                targetEngineThrusts[backLeft],
                targetEngineThrusts[backRight]
            );
            if (minThrust < minEngineThrustForAlignment)
            {
                
                float reductionFactor = (baseThrust - minEngineThrustForAlignment) / (baseThrust - minThrust);
                rollStrength *= reductionFactor;
                pitchStrength *= reductionFactor;
                yawStrength *= reductionFactor;
                
                targetEngineThrusts[frontLeft] = baseThrust + rollStrength - pitchStrength - yawStrength;
                targetEngineThrusts[frontRight] = baseThrust - rollStrength - pitchStrength + yawStrength;
                targetEngineThrusts[backLeft] = baseThrust + rollStrength + pitchStrength - yawStrength;
                targetEngineThrusts[backRight] = baseThrust - rollStrength + pitchStrength + yawStrength;
            }
            
            float alignmentThrustChangeRate = thrustChangeRate * 5.0f; 
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
        
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"автопилот: === ВЫРАВНИВАНИЕ ПО НОРМАЛИ ПОВЕРХНОСТИ ===");
            Debug.Log($"НОРМАЛЬ ПОВЕРХНОСТИ:");
            Debug.Log($"surfaceNormal: {surfaceNormal}");
            Debug.Log($"desiredUp: {desiredUp}");
            Debug.Log($"ТЕКУЩАЯ ОРИЕНТАЦИЯ КОРАБЛЯ:");
            Debug.Log($"currentUp: {currentUp}");
            Debug.Log($"currentForward: {currentForward}");
            Debug.Log($"currentRight: {currentRight}");
            Debug.Log($"Rotation (Euler): {shipTransform.rotation.eulerAngles}° (X={shipTransform.rotation.eulerAngles.x:F1}°, Y={shipTransform.rotation.eulerAngles.y:F1}°, Z={shipTransform.rotation.eulerAngles.z:F1}°)");
            Debug.Log($"ЖЕЛАЕМАЯ ОРИЕНТАЦИЯ:");
            Debug.Log($"desiredForward: {desiredForward}");
            Debug.Log($"desiredRight: {desiredRight}");
            Debug.Log($"targetRotation (Euler): {targetRotation.eulerAngles}° (X={targetRotation.eulerAngles.x:F1}°, Y={targetRotation.eulerAngles.y:F1}°, Z={targetRotation.eulerAngles.z:F1}°)");
            Debug.Log($"ОШИБКА ОРИЕНТАЦИИ:");
            Debug.Log($"angleError (по нормали): {angleError:F1}°");
            Debug.Log($"totalAngleError: {angle:F1}° (Axis: {axis})");
            Debug.Log($"rotationError (Euler): {rotationError.eulerAngles}° (X={rotationError.eulerAngles.x:F1}°, Y={rotationError.eulerAngles.y:F1}°, Z={rotationError.eulerAngles.z:F1}°)");
            Debug.Log($"localAxis: {localAxis} (X={localAxis.x:F3}, Y={localAxis.y:F3}, Z={localAxis.z:F3})");
            Debug.Log($"УГЛОВАЯ СКОРОСТЬ:");
            Debug.Log($"angularVelocity (world): {angularVelocity} (X={angularVelocity.x:F3}, Y={angularVelocity.y:F3}, Z={angularVelocity.z:F3})");
            Debug.Log($"localAngularVelocity: {localAngularVelocity} (X={localAngularVelocity.x:F3}, Y={localAngularVelocity.y:F3}, Z={localAngularVelocity.z:F3})");
            Debug.Log($"targetAngularSpeed: {targetAngularSpeed:F3}");
            Debug.Log($"КОРРЕКЦИЯ:");
            Debug.Log($"rollCorrection: {rollCorrection:F3} (крен, вокруг локальной оси X/forward)");
            Debug.Log($"yawCorrection: {yawCorrection:F3} (рыскание, вокруг локальной оси Y/up)");
            Debug.Log($"pitchCorrection: {pitchCorrection:F3} (тангаж, вокруг локальной оси Z/right)");
            Debug.Log($"rollPID: {rollPID:F3}, yawPID: {yawPID:F3}, pitchPID: {pitchPID:F3}");
            Debug.Log($"УПРАВЛЕНИЕ ДВИГАТЕЛЯМИ:");
            Debug.Log($"baseThrust: {currentThrust:F3}");
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
                Debug.Log($"Engine FL({frontLeft}): {targetEngineThrusts[frontLeft]:F3} (roll:{rollStrength:F3}, pitch:{-pitchStrength:F3}, yaw:{-yawStrength:F3})");
                Debug.Log($"Engine FR({frontRight}): {targetEngineThrusts[frontRight]:F3} (roll:{-rollStrength:F3}, pitch:{-pitchStrength:F3}, yaw:{yawStrength:F3})");
                Debug.Log($"Engine BL({backLeft}): {targetEngineThrusts[backLeft]:F3} (roll:{rollStrength:F3}, pitch:{pitchStrength:F3}, yaw:{-yawStrength:F3})");
                Debug.Log($"Engine BR({backRight}): {targetEngineThrusts[backRight]:F3} (roll:{-rollStrength:F3}, pitch:{pitchStrength:F3}, yaw:{yawStrength:F3})");
            }
            Debug.Log($"ВСЕ коррекции rotation через дифференциальную тягу двигателей (НЕ через movementDirection)");
            Debug.Log($"СТАТУС:");
            Debug.Log($"isAligningToSurface: {isAligningToSurface}, angleError: {angleError:F1}°, totalAngle: {angle:F1}°, isAligned: {isAligned}, alignmentStrength: {alignmentStrength:F2}");
        }
        
    }
    
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
    
    private void StabilizeRotation(Vector3 surfaceNormal = default)
    {
        
        if (surfaceNormal == default || surfaceNormal.magnitude < 0.1f)
        {
            surfaceNormal = Vector3.up;
        }
        else
        {
            surfaceNormal = surfaceNormal.normalized;
        }
        
        if (isAligningToSurface)
        {
            return;
        }

        Vector3 angularVelocity = shipController.GetAngularVelocity();
        Transform shipTransform = shipController.transform;
        
        Vector3 localAngularVelocity = shipTransform.InverseTransformDirection(angularVelocity);
        
        Vector3 desiredUp = Vector3.up; 
        Vector3 currentUp = shipTransform.up;
        float orientationError = Vector3.Angle(currentUp, desiredUp);
        
        if (useGeometryThrustStabilization)
        {
            Vector3 correctionAxis = Vector3.Cross(currentUp, desiredUp);
            float torqueStrength = geometryTorqueStrength * (isRotationStabilizationMode ? stabilizationTorqueMultiplier : 1f);
            float torqueDamping = geometryTorqueDamping * (isRotationStabilizationMode ? stabilizationDampingMultiplier : 1f);
            float maxDelta = maxGeometryThrustDelta * (isRotationStabilizationMode ? stabilizationMaxDeltaMultiplier : 1f);
            Vector3 desiredTorque = correctionAxis * torqueStrength - angularVelocity * torqueDamping;
            bool applied = ApplyTorqueWithThrust(desiredTorque, currentThrust, 0.05f, maxDelta, 5f);
            if (applied)
            {
                return;
            }
        }
        
        float orientationCorrectionSpeed;
        if (orientationError > 10f)
        {
            orientationCorrectionSpeed = orientationError * 0.25f; 
        }
        else if (orientationError > 5f)
        {
            orientationCorrectionSpeed = orientationError * 0.22f; 
        }
        else
        {
            
            orientationCorrectionSpeed = orientationError * 0.20f; 
        }
        
        Vector3 desiredAngularVelocity = Vector3.Cross(currentUp, desiredUp).normalized * orientationCorrectionSpeed;
        if (invertRotationCorrection)
        {
            desiredAngularVelocity = -desiredAngularVelocity;
        }
        Vector3 localDesiredAngularVelocity = shipTransform.InverseTransformDirection(desiredAngularVelocity);
        
        Vector3 targetAngularVelocity = -localAngularVelocity + localDesiredAngularVelocity;
        
        float velocityMagnitude = localAngularVelocity.magnitude;
        if (velocityMagnitude < 0.15f) 
        {
            if (orientationError > 10f)
            {
                targetAngularVelocity = localDesiredAngularVelocity * 2.2f; 
            }
            else if (orientationError > 7f)
            {
                targetAngularVelocity = localDesiredAngularVelocity * 2.3f; 
            }
            else if (orientationError > 5f)
            {
                targetAngularVelocity = localDesiredAngularVelocity * 2.1f; 
            }
            else if (orientationError > 2f)
            {
                targetAngularVelocity = localDesiredAngularVelocity * 2.5f; 
            }
            else if (orientationError > 0.5f)
            {
                
                targetAngularVelocity = localDesiredAngularVelocity * 2.0f; 
            }
            else
            {
                
                targetAngularVelocity = localDesiredAngularVelocity * 1.5f; 
            }
        }
        
        float rollTarget = targetAngularVelocity.x;
        float rollCurrent = localAngularVelocity.x;
        float pitchTarget = targetAngularVelocity.z;
        float pitchCurrent = localAngularVelocity.z;
        float rollCorrection = rollStabilizationPID.Update(rollTarget, rollCurrent, Time.deltaTime);
        float yawCorrection = yawStabilizationPID.Update(targetAngularVelocity.y, localAngularVelocity.y, Time.deltaTime);
        float pitchCorrection = pitchStabilizationPID.Update(pitchTarget, pitchCurrent, Time.deltaTime);
        
        float baseThrustForCorrection = currentThrust;
        float minStabilizationThrustForCorrection = 0.15f;
        float effectiveBaseThrustForCorrection = Mathf.Max(baseThrustForCorrection, minStabilizationThrustForCorrection);
        
        float correctionMultiplier = 1.0f;
        float maxCorrectionRelative;
        if (orientationError > 10f)
        {
            maxCorrectionRelative = 0.85f; 
            correctionMultiplier = 1.1f; 
        }
        else if (orientationError > 5f)
        {
            maxCorrectionRelative = 0.80f; 
            correctionMultiplier = 1.05f;
        }
        else
        {
            
            maxCorrectionRelative = 0.70f; 
            correctionMultiplier = 1.3f; 
        }
        float maxCorrectionAbsolute = effectiveBaseThrustForCorrection * maxCorrectionRelative;
        
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
            
            float baseThrust = currentThrust; 
            
            float minStabilizationThrust = 0.15f; 
            float effectiveBaseThrust = Mathf.Max(baseThrust, minStabilizationThrust);
            
            float[] targetEngineThrusts = new float[engineCount];
            for (int i = 0; i < engineCount; i++)
            {
                targetEngineThrusts[i] = effectiveBaseThrust;
            }
            
            targetEngineThrusts[frontLeft] = effectiveBaseThrust + rollCorrection - pitchCorrection - yawCorrection;
            targetEngineThrusts[frontRight] = effectiveBaseThrust - rollCorrection - pitchCorrection + yawCorrection;
            targetEngineThrusts[backLeft] = effectiveBaseThrust + rollCorrection + pitchCorrection - yawCorrection;
            targetEngineThrusts[backRight] = effectiveBaseThrust - rollCorrection + pitchCorrection + yawCorrection;
            
            float minEngineThrustForProtection;
            if (orientationError > 7f)
            {
                minEngineThrustForProtection = 0.01f; 
            }
            else if (orientationError > 5f)
            {
                minEngineThrustForProtection = 0.02f; 
            }
            else
            {
                minEngineThrustForProtection = 0.05f; 
            }
            float minThrust = Mathf.Min(
                targetEngineThrusts[frontLeft],
                targetEngineThrusts[frontRight],
                targetEngineThrusts[backLeft],
                targetEngineThrusts[backRight]
            );
            if (minThrust < minEngineThrustForProtection)
            {
                
                float reductionFactor = (effectiveBaseThrust - minEngineThrustForProtection) / (effectiveBaseThrust - minThrust);
                rollCorrection *= reductionFactor;
                pitchCorrection *= reductionFactor;
                yawCorrection *= reductionFactor;
                
                targetEngineThrusts[frontLeft] = effectiveBaseThrust + rollCorrection - pitchCorrection - yawCorrection;
                targetEngineThrusts[frontRight] = effectiveBaseThrust - rollCorrection - pitchCorrection + yawCorrection;
                targetEngineThrusts[backLeft] = effectiveBaseThrust + rollCorrection + pitchCorrection - yawCorrection;
                targetEngineThrusts[backRight] = effectiveBaseThrust - rollCorrection + pitchCorrection + yawCorrection;
            }
            
            float stabilizationThrustChangeRate = thrustChangeRate * 5.0f; 
            float maxThrustChange = stabilizationThrustChangeRate * Time.deltaTime;
            
            for (int i = 0; i < engineCount; i++)
            {
                targetEngineThrusts[i] = Mathf.Clamp01(targetEngineThrusts[i]);
                
                targetEngineThrusts[i] = Mathf.Max(targetEngineThrusts[i], minEngineThrustForProtection);
                currentEngineThrusts[i] = Mathf.MoveTowards(currentEngineThrusts[i], targetEngineThrusts[i], maxThrustChange);
                
                currentEngineThrusts[i] = Mathf.Max(currentEngineThrusts[i], minEngineThrustForProtection);
                shipController.SetEngineThrust(i, currentEngineThrusts[i]);
            }
            
            if (showDebugInfo && Time.frameCount % 60 == 0)
            {
                
                Vector3 shipForward = shipTransform.forward;
                Vector3 shipRight = shipTransform.right;
                Vector3 shipUp = shipTransform.up;
                
                Debug.Log($"автопилот: === СТАБИЛИЗАЦИЯ ROTATION ===");
                Debug.Log($"ЛОКАЛЬНЫЕ ОСИ КОРАБЛЯ:");
                Debug.Log($"ship.forward (локальная X): {shipForward} (вперед)");
                Debug.Log($"ship.up (локальная Y): {shipUp} (вверх)");
                Debug.Log($"ship.right (локальная Z): {shipRight} (вправо)");
                Debug.Log($"Rotation (Euler): {shipTransform.rotation.eulerAngles}° (X={shipTransform.rotation.eulerAngles.x:F1}°, Y={shipTransform.rotation.eulerAngles.y:F1}°, Z={shipTransform.rotation.eulerAngles.z:F1}°)");
                Debug.Log($"ОСИ ROTATION:");
                Debug.Log($"Roll (крен, вокруг forward/X): localAngularVelocity.x = {localAngularVelocity.x:F3}");
                Debug.Log($"Yaw (рыскание, вокруг up/Y): localAngularVelocity.y = {localAngularVelocity.y:F3}");
                Debug.Log($"Pitch (тангаж, вокруг right/Z): localAngularVelocity.z = {localAngularVelocity.z:F3}");
                Debug.Log($"Ориентация (ВРЕМЕННО: выравнивание к вертикали Vector3.up):");
                Debug.Log($"desiredUp (Vector3.up): {desiredUp}");
                Debug.Log($"currentUp: {currentUp}");
                Debug.Log($"orientationError (относительно вертикали): {orientationError:F1}°");
                
                Vector3 crossAxis = Vector3.Cross(currentUp, desiredUp);
                Vector3 crossAxisNormalized = crossAxis.normalized;
                Debug.Log($"ДИАГНОСТИКА ВРАЩЕНИЯ:");
                Debug.Log($"Vector3.Cross(currentUp, desiredUp): {crossAxis} (magnitude: {crossAxis.magnitude:F3})");
                Debug.Log($"Нормализованная ось: {crossAxisNormalized}");
                Debug.Log($"В мировых координатах:");
                Debug.Log($"crossAxis.x = {crossAxisNormalized.x:F3} (влево/вправо в мире)");
                Debug.Log($"crossAxis.y = {crossAxisNormalized.y:F3} (вверх/вниз в мире)");
                Debug.Log($"crossAxis.z = {crossAxisNormalized.z:F3} (вперед/назад в мире)");
                Debug.Log($"В локальных координатах корабля:");
                Debug.Log($"localDesiredAngularVelocity.x = {localDesiredAngularVelocity.x:F3} (крен, вокруг forward/X)");
                Debug.Log($"localDesiredAngularVelocity.y = {localDesiredAngularVelocity.y:F3} (рыскание, вокруг up/Y)");
                Debug.Log($"localDesiredAngularVelocity.z = {localDesiredAngularVelocity.z:F3} (тангаж, вокруг right/Z)");
                Debug.Log($"ИНТЕРПРЕТАЦИЯ:");
                if (localDesiredAngularVelocity.x > 0.01f)
                    Debug.Log($"X > 0: крен ВЛЕВО (левая сторона вниз) → увеличить E0, E2 (левые)");
                else if (localDesiredAngularVelocity.x < -0.01f)
                    Debug.Log($"X < 0: крен ВПРАВО (правая сторона вниз) → увеличить E1, E3 (правые)");
                if (localDesiredAngularVelocity.y > 0.01f)
                    Debug.Log($"Y > 0: поворот ВЛЕВО (нос поворачивается влево) → увеличить E1, E3 (правые)");
                else if (localDesiredAngularVelocity.y < -0.01f)
                    Debug.Log($"Y < 0: поворот ВПРАВО (нос поворачивается вправо) → увеличить E0, E2 (левые)");
                if (localDesiredAngularVelocity.z > 0.01f)
                    Debug.Log($"Z > 0: нос ВВЕРХ (задняя часть вниз) → увеличить E2, E3 (задние)");
                else if (localDesiredAngularVelocity.z < -0.01f)
                    Debug.Log($"Z < 0: нос ВНИЗ (передняя часть вниз) → увеличить E0, E1 (передние)");
                Debug.Log($"Угловая скорость:");
                Debug.Log($"localAngularVelocity: ({localAngularVelocity.x:F3}, {localAngularVelocity.y:F3}, {localAngularVelocity.z:F3}) рад/с (magnitude: {localAngularVelocity.magnitude:F3})");
                Debug.Log($"localDesiredAngularVelocity: ({localDesiredAngularVelocity.x:F3}, {localDesiredAngularVelocity.y:F3}, {localDesiredAngularVelocity.z:F3}) рад/с (magnitude: {localDesiredAngularVelocity.magnitude:F3})");
                Debug.Log($"targetAngularVelocity: ({targetAngularVelocity.x:F3}, {targetAngularVelocity.y:F3}, {targetAngularVelocity.z:F3}) рад/с (magnitude: {targetAngularVelocity.magnitude:F3})");
                Debug.Log($"Коррекции:");
                Debug.Log($"rollCorrection: {rollCorrection:F3}, yawCorrection: {yawCorrection:F3}, pitchCorrection: {pitchCorrection:F3}");
                Debug.Log($"correctionMultiplier: {correctionMultiplier:F2}, maxCorrectionAbsolute: {maxCorrectionAbsolute:F3} (70% от baseThrust={effectiveBaseThrustForCorrection:F3})");
                Debug.Log($"baseThrust: {baseThrust:F3}, effectiveBaseThrust: {effectiveBaseThrust:F3}");
                Debug.Log($"Тяга двигателей:");
                bool logHasQuad = TryGetEngineQuadrants(out int logFrontLeft, out int logFrontRight, out int logBackLeft, out int logBackRight);
                if (!logHasQuad)
                {
                    logFrontLeft = 0;
                    logFrontRight = 1;
                    logBackLeft = 2;
                    logBackRight = 3;
                }
                Debug.Log($"FL({logFrontLeft}): {targetEngineThrusts[logFrontLeft]:F3} (base={effectiveBaseThrust:F3} + roll={rollCorrection:F3} - pitch={pitchCorrection:F3} - yaw={yawCorrection:F3})");
                Debug.Log($"FR({logFrontRight}): {targetEngineThrusts[logFrontRight]:F3} (base={effectiveBaseThrust:F3} - roll={rollCorrection:F3} - pitch={pitchCorrection:F3} + yaw={yawCorrection:F3})");
                Debug.Log($"BL({logBackLeft}): {targetEngineThrusts[logBackLeft]:F3} (base={effectiveBaseThrust:F3} + roll={rollCorrection:F3} + pitch={pitchCorrection:F3} - yaw={yawCorrection:F3})");
                Debug.Log($"BR({logBackRight}): {targetEngineThrusts[logBackRight]:F3} (base={effectiveBaseThrust:F3} - roll={rollCorrection:F3} + pitch={pitchCorrection:F3} + yaw={yawCorrection:F3})");
                Debug.Log($"ЛОГИКА КОРРЕКЦИЙ:");
                Debug.Log($"Roll: {rollCorrection:F3} (roll > 0 → увеличиваем FL, BL; roll < 0 → увеличиваем FR, BR)");
                Debug.Log($"Pitch: {pitchCorrection:F3} (pitch > 0 → увеличиваем FL, FR; pitch < 0 → увеличиваем BL, BR)");
                Debug.Log($"Yaw: {yawCorrection:F3} (yaw > 0 → увеличиваем FR, BR; yaw < 0 → увеличиваем FL, BL)");
            }
        }
        else
        {
            
            float maxThrustChange = thrustChangeRate * Time.deltaTime;
            for (int i = 0; i < engineCount; i++)
            {
                currentEngineThrusts[i] = Mathf.MoveTowards(currentEngineThrusts[i], currentThrust, maxThrustChange);
                shipController.SetEngineThrust(i, currentEngineThrusts[i]);
            }
        }
        
        if (!isAligningToSurface && engineCount < 4)
        {
            Vector2 movementDirection = shipController.GetMovementDirection();
            movementDirection.x += yawCorrection * 0.5f; 
            movementDirection.x = Mathf.Clamp(movementDirection.x, -1f, 1f);
            shipController.SetMovementDirection(movementDirection);
        }
        
    }
    
    private void ApplyMovementPauseIfNeeded()
    {
        if (isRotationStabilizationMode)
        {
            movementPausedForRotation = true;
            movementDampingFactor = 0f;
            Vector2 currentMovementStab = shipController.GetMovementDirection();
            if (currentMovementStab.sqrMagnitude > 0f)
            {
                shipController.SetMovementDirection(Vector2.zero);
            }
            return;
        }
        
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
    
    private float CalculateHoverThrust()
    {
        float mass = shipController.GetMass();
        float gravityStrength = shipController.GetGravityStrength();
        float maxThrustForce = shipController.GetMaxThrustForce();
        int engineCount = shipController.GetEngineCount();
        
        float weight = mass * gravityStrength;
        float totalMaxThrust = maxThrustForce * engineCount;
        
        if (totalMaxThrust <= 0f) return 0f;
        
        float hoverThrust = Mathf.Clamp01((weight / totalMaxThrust) + 0.05f);
        
        return hoverThrust;
    }
    
    private LandingSite SelectBestLandingSite(List<LandingSite> sites)
    {
        if (sites == null || sites.Count == 0) return null;
        
        Vector3 shipPosition = shipController.transform.position;
        
        LandingSite bestSite = sites
            .OrderByDescending(site => site.suitabilityScore)
            .ThenBy(site => Vector3.Distance(shipPosition, site.position))
            .First();
        
        return bestSite;
    }
    
    private bool IsSiteAvailable(LandingSite site)
    {
        if (site == null) return false;
        
        List<LandingSite> sites = landingRadar != null ? landingRadar.GetFoundSites() : null;
        if (sites == null || sites.Count == 0) return false;
        
        return sites.Any(s => Vector3.Distance(s.position, site.position) < 1f);
    }
    
    private void TrySwitchToNewSite()
    {
        List<LandingSite> sites = landingRadar != null ? landingRadar.GetFoundSites() : null;
        if (sites == null || sites.Count == 0)
        {
            
            currentPhase = LandingPhase.WaitingForSite;
            targetSite = null;
            OnLandingPhaseChanged?.Invoke(currentPhase);
            if (showDebugInfo)
            {
                Debug.LogWarning("автопилот: текущая площадка недоступна, ожидаю новые точки.");
            }
            return;
        }
        
        LandingSite newSite = SelectBestLandingSite(sites);
        if (newSite != null)
        {
            targetSite = newSite;
            if (showDebugInfo)
            {
                Debug.Log($"автопилот: переключился на новую площадку (расстояние: {Vector3.Distance(shipController.transform.position, newSite.position):F1}м)");
            }
        }
    }
}
