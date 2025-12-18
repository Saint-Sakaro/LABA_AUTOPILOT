using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Режимы среды для корабля
/// </summary>
public enum EnvironmentMode
{
    Space,       // Космос - минимальное сопротивление
    Atmosphere   // Атмосфера - больше сопротивление
}

/// <summary>
/// Основной контроллер управления космическим кораблем
/// Каждый двигатель создает силу в направлении своего transform.forward
/// W/S управляют глобальной тягой всех двигателей
/// Поддерживает гравитацию и разные режимы среды (космос/атмосфера)
/// </summary>
public class ShipController : MonoBehaviour
{
    [Header("Physics")]
    [SerializeField] private Rigidbody shipRigidbody;
    [SerializeField] private float mass = 20000f;              // Масса корабля в кг (20 тонн - реалистично для космического корабля)
    
    [Header("Environment Mode")]
    [SerializeField] private EnvironmentMode currentEnvironment = EnvironmentMode.Space;
    [SerializeField] private float atmosphereHeight = 100f;     // Высота начала атмосферы (от Y=0)
    [SerializeField] private bool autoDetectEnvironment = true;  // Автоматически определять режим по высоте
    
    [Header("Gravity")]
    [SerializeField] private bool useGravity = true;             // Включить гравитацию
    [SerializeField] private float gravityStrength = 9.81f;      // Сила гравитации (м/с²)
    [SerializeField] private Vector3 gravityDirection = Vector3.down; // Направление гравитации
    
    [Header("Thrust Settings")]
    [SerializeField] private float maxThrustForce = 100000f;    // Максимальная сила тяги одного двигателя в Н (100 кН - реалистично)
    [SerializeField] private float thrustChangeSpeed = 1f;     // Скорость изменения тяги (более плавно)
    [SerializeField] private float minThrustForBalance = 0.1f;  // Минимальная тяга для баланса при выборе одного двигателя
    
    [Header("Thrust to Weight Ratio Info")]
    [SerializeField] private bool showTWRInfo = true;          // Показывать информацию о TWR в консоли
    
    [Header("Engine Selection")]
    [SerializeField] private bool enableEngineSelection = true;  // Включить выбор двигателей
    [SerializeField] private KeyCode selectAllEngines = KeyCode.Alpha0; // Кнопка для выбора всех двигателей
    
    [Header("Movement Direction Control")]
    [SerializeField] private KeyCode selectForwardBackward = KeyCode.X;    // Кнопка для выбора направления вперед/назад
    [SerializeField] private KeyCode selectLeftRight = KeyCode.Z;          // Кнопка для выбора направления влево/вправо
    [SerializeField] private KeyCode increaseDirection = KeyCode.A;        // Увеличение смещения в выбранном направлении
    [SerializeField] private KeyCode decreaseDirection = KeyCode.D;        // Уменьшение смещения в выбранном направлении
    [SerializeField] private float directionChangeSpeed = 2f;              // Скорость изменения направления (единиц в секунду)
    [SerializeField] private float maxDirectionOffset = 1f;                // Максимальное смещение в направлении (0-1)
    [SerializeField] private float maxTiltAngle = 45f;                      // Максимальный угол наклона двигателей (градусов)
    [SerializeField] private float engineRotationSpeed = 360f;     // Скорость поворота двигателей (градусов в секунду) - увеличено для быстрого визуального отклика
    [SerializeField] private float maxEngineRotationAngle = 45f; // Максимальный угол поворота двигателя
    [SerializeField] private bool instantRotation = false;        // Мгновенный поворот (для теста, без плавной интерполяции)
    
    [Header("Engine Movement")]
    [SerializeField] private float engineMovementSpeed = 2f;     // Скорость перемещения двигателей (единиц в секунду)
    [SerializeField] private float maxEngineOffset = 2f;         // Максимальное смещение двигателя от начальной позиции
    
    [Header("Stabilization")]
    [SerializeField] private bool autoStabilize = true;       // Автоматическая стабилизация
    [SerializeField] private float stabilizationStrength = 50f; // Сила стабилизации (увеличено для уменьшения вращения)
    [SerializeField] private bool applyForceToCenter = false;   // Применять силу к центру масс (если двигатели симметричны)
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;      // Показывать отладочную информацию
    
    [Header("References")]
    [SerializeField] private ShipThrusterManager thrusterManager;
    
    // Список всех двигателей (EngineFireController)
    private List<EngineFireController> engines = new List<EngineFireController>();
    
    // Список выбранных двигателей (индексы)
    private HashSet<int> selectedEngines = new HashSet<int>();
    
    // Индивидуальная тяга для каждого двигателя (0-1)
    private float[] engineThrusts = new float[4];
    
    // Углы поворота для каждого двигателя (X и Y оси)
    private Vector2[] engineRotations = new Vector2[4]; // x = угол по X, y = угол по Y
    
    // Начальные углы поворота двигателей (базовое положение)
    private Vector2[] initialEngineRotations = new Vector2[4];
    
    // Начальные кватернионы поворота двигателей (для плавного поворота)
    private Quaternion[] initialEngineRotationsQuat = new Quaternion[4];
    
    // Начальные позиции двигателей (базовое положение)
    private Vector3[] initialEnginePositions = new Vector3[4];
    
    // Желаемое направление движения (вектор в локальных координатах корабля)
    // X = смещение влево/вправо (положительное = вправо, отрицательное = влево)
    // Z = смещение вперед/назад (положительное = вперед, отрицательное = назад)
    // Y всегда 0, так как двигатели создают тягу в горизонтальной плоскости
    private Vector2 desiredMovementDirection = Vector2.zero; // x = влево/вправо, y = вперед/назад
    
    // Текущая тяга (0-1) - используется для изменения выбранных двигателей
    private float currentThrust = 0f;
    
    // Текущее выбранное направление движения
    private enum MovementDirection { None, ForwardBackward, LeftRight }
    private MovementDirection currentMovementDirection = MovementDirection.None;
    
    private void Awake()
    {
        // Автоматически находим компоненты, если не назначены
        if (shipRigidbody == null)
        {
            shipRigidbody = GetComponent<Rigidbody>();
            if (shipRigidbody == null)
            {
                Debug.LogWarning("ShipController: Rigidbody не найден! Добавьте Rigidbody к кораблю.");
            }
        }
        
        if (thrusterManager == null)
        {
            thrusterManager = GetComponent<ShipThrusterManager>();
        }
        
        // Настраиваем Rigidbody
        if (shipRigidbody != null)
        {
            shipRigidbody.mass = mass;
            
            // Убеждаемся, что центр масс в правильном месте
            shipRigidbody.centerOfMass = Vector3.zero;
            
            // Отключаем встроенную гравитацию Unity - будем применять свою
            shipRigidbody.useGravity = false;
            
            // Применяем начальные настройки среды
            UpdateEnvironmentSettings();
            
            // Выводим информацию о TWR (Thrust to Weight Ratio)
            if (showTWRInfo && engines.Count > 0)
            {
                CalculateAndLogTWR();
            }
        }
        
        // Находим все двигатели
        FindAllEngines();
        
        // Выводим информацию о TWR после инициализации двигателей
        if (showTWRInfo && shipRigidbody != null && engines.Count > 0)
        {
            CalculateAndLogTWR();
        }
    }
    
    /// <summary>
    /// Находит все двигатели на корабле
    /// </summary>
    private void FindAllEngines()
    {
        engines.Clear();
        
        // Ищем двигатели напрямую на корабле и дочерних объектах
        engines.AddRange(GetComponentsInChildren<EngineFireController>());
        
        // Если не нашли, пробуем через ShipThrusterManager
        if (engines.Count == 0 && thrusterManager != null)
        {
            // Пробуем получить двигатели из ShipThrusterManager через рефлексию
            // Но лучше искать напрямую
            engines.AddRange(GetComponentsInChildren<EngineFireController>());
        }
        
        Debug.Log($"ShipController: Найдено двигателей: {engines.Count}");
        
        // Выводим информацию о каждом двигателе
        for (int i = 0; i < engines.Count; i++)
        {
            if (engines[i] != null)
            {
                Debug.Log($"  Двигатель {i + 1}: {engines[i].gameObject.name} в позиции {engines[i].transform.position}, направление {engines[i].transform.forward}");
            }
        }
        
        if (engines.Count == 0)
        {
            Debug.LogError("ShipController: Двигатели не найдены! Убедитесь, что EngineFireController добавлены к кораблю или его дочерним объектам.");
        }
        else
        {
            // Инициализируем массив тяги двигателей с начальной тягой
            engineThrusts = new float[engines.Count];
            engineRotations = new Vector2[engines.Count];
            initialEngineRotations = new Vector2[engines.Count];
            initialEngineRotationsQuat = new Quaternion[engines.Count];
            initialEnginePositions = new Vector3[engines.Count];
            
            for (int i = 0; i < engines.Count; i++)
            {
                engineThrusts[i] = 0f; // Начальная тяга - 0
                
                // Сохраняем начальные углы поворота и позицию двигателя
                if (engines[i] != null)
                {
                    // Отключаем Rigidbody у двигателя, если он есть (чтобы не мешал повороту и перемещению)
                    Rigidbody engineRb = engines[i].GetComponent<Rigidbody>();
                    if (engineRb != null)
                    {
                        engineRb.isKinematic = true; // Делаем kinematic, чтобы физика не мешала
                        Debug.Log($"ShipController: Двигатель {i + 1} ({engines[i].gameObject.name}) имеет Rigidbody - установлен isKinematic = true");
                    }
                    
                    // Сохраняем начальную позицию
                    initialEnginePositions[i] = engines[i].transform.localPosition;
                    
                    // Сохраняем начальные углы поворота (нормализуем в диапазон -180 до 180)
                    Vector3 localEuler = engines[i].transform.localEulerAngles;
                    float normalizedX = NormalizeAngle(localEuler.x);
                    float normalizedY = NormalizeAngle(localEuler.y);
                    Vector2 initialAngles = new Vector2(normalizedX, normalizedY);
                    initialEngineRotations[i] = initialAngles;
                    engineRotations[i] = initialAngles;
                    // Сохраняем полный начальный кватернион (включая Z угол)
                    initialEngineRotationsQuat[i] = engines[i].transform.localRotation;
                }
                else
                {
                    initialEnginePositions[i] = Vector3.zero;
                    initialEngineRotations[i] = Vector2.zero;
                    engineRotations[i] = Vector2.zero;
                    initialEngineRotationsQuat[i] = Quaternion.identity;
                }
            }
            
            // По умолчанию выбираем все двигатели
            SelectAllEngines();
        }
    }
    
    private void Update()
    {
        // Обрабатываем выбор двигателей (1-4, 0)
        if (enableEngineSelection)
        {
            HandleEngineSelection();
        }
        
        // Обрабатываем выбор оси поворота (X, Z)
        HandleMovementDirectionSelection();
        
        // Обрабатываем изменение направления движения (A/D)
        HandleMovementDirectionInput();
        
        // Если направление было изменено, обновляем углы поворота двигателей
        // (это делается в HandleMovementDirectionInput, но нужно убедиться, что углы применяются)
        
        // Постоянно обновляем поворот двигателей (плавное движение)
        UpdateEngineRotationsSmoothly();
        // Перемещение двигателей отключено - теперь двигатели только поворачиваются для создания тяги в нужном направлении
        // UpdateEnginePositionsSmoothly();
        
        // Обрабатываем ввод W/S для изменения тяги
        HandleThrustInput();
        
        // Автоматически определяем режим среды по высоте
        if (autoDetectEnvironment)
        {
            DetectEnvironmentMode();
        }
    }
    
    private void FixedUpdate()
    {
        // Применяем физику в FixedUpdate для стабильности
        
        // Применяем гравитацию (если включена)
        if (useGravity)
        {
            ApplyGravity();
        }
        
        // Каждый двигатель создает силу в своем направлении
        ApplyThrustFromEngines();
        
        // Автоматическая стабилизация (компенсирует вращающий момент)
        if (autoStabilize)
        {
            ApplyStabilization();
        }
    }
    
    /// <summary>
    /// Обработка выбора двигателей (1-4 для отдельных, 0 для всех)
    /// </summary>
    private void HandleEngineSelection()
    {
        // Кнопка 0 - выбрать все двигатели
        if (Input.GetKeyDown(selectAllEngines))
        {
            SelectAllEngines();
            Debug.Log("Выбраны все двигатели");
            return;
        }
        
        // Кнопки 1-4 - выбор отдельных двигателей
        for (int i = 1; i <= 4; i++)
        {
            KeyCode key = (KeyCode)((int)KeyCode.Alpha1 + (i - 1));
            if (Input.GetKeyDown(key))
            {
                int engineIndex = i - 1; // Индекс двигателя (0-3)
                SelectEngine(engineIndex);
                Debug.Log($"Выбран двигатель {i} (индекс {engineIndex})");
            }
        }
    }
    
    /// <summary>
    /// Выбрать конкретный двигатель (остальные сохраняют свое состояние)
    /// </summary>
    private void SelectEngine(int engineIndex)
    {
        if (engineIndex < 0 || engineIndex >= engines.Count)
        {
            Debug.LogWarning($"ShipController: Некорректный индекс двигателя: {engineIndex}");
            return;
        }
        
        // Очищаем выбор и добавляем только выбранный двигатель
        selectedEngines.Clear();
        selectedEngines.Add(engineIndex);
        
        // Устанавливаем текущую тягу равной тяге выбранного двигателя
        // (чтобы W/S продолжали изменять тягу этого двигателя)
        currentThrust = engineThrusts[engineIndex];
        
        // ВАЖНО: При выборе одного двигателя остальные должны получить такую же тягу
        // Это предотвратит резкое вращение при изменении тяги выбранного двигателя
        // Пользователь может потом настроить их индивидуально, если нужно
        for (int i = 0; i < engines.Count; i++)
        {
            if (i != engineIndex)
            {
                engineThrusts[i] = currentThrust;
            }
        }
        
        // Сбрасываем выбранное направление движения при выборе двигателя
        currentMovementDirection = MovementDirection.None;
        
        // Обновляем визуальные эффекты
        UpdateEngineVisuals();
    }
    
    /// <summary>
    /// Выбрать все двигатели
    /// </summary>
    private void SelectAllEngines()
    {
        selectedEngines.Clear();
        for (int i = 0; i < engines.Count; i++)
        {
            selectedEngines.Add(i);
        }
        
        // Устанавливаем текущую тягу равной средней тяге всех двигателей
        float averageThrust = 0f;
        bool hasAnyThrust = false;
        for (int i = 0; i < engines.Count; i++)
        {
            averageThrust += engineThrusts[i];
            if (engineThrusts[i] > 0.01f)
            {
                hasAnyThrust = true;
            }
        }
        
        if (engines.Count > 0)
        {
            averageThrust /= engines.Count;
        }
        
        // Если все двигатели на нуле, устанавливаем начальную тягу
        if (!hasAnyThrust)
        {
            averageThrust = 0f; // Начальная тяга - 0
            currentThrust = 0f;
        }
        else
        {
            currentThrust = averageThrust;
        }
        
        // Сбрасываем выбранное направление движения при выборе всех двигателей
        currentMovementDirection = MovementDirection.None;
        
        // Обновляем визуальные эффекты
        UpdateEngineVisuals();
    }
    
    /// <summary>
    /// Обработка выбора направления движения (X = вперед/назад, Z = влево/вправо)
    /// </summary>
    private void HandleMovementDirectionSelection()
    {
        if (Input.GetKeyDown(selectForwardBackward))
        {
            currentMovementDirection = MovementDirection.ForwardBackward;
            Debug.Log("Выбрано направление: Вперед/Назад (X)");
        }
        else if (Input.GetKeyDown(selectLeftRight))
        {
            currentMovementDirection = MovementDirection.LeftRight;
            Debug.Log("Выбрано направление: Влево/Вправо (Z)");
        }
    }
    
    /// <summary>
    /// Обработка изменения направления движения (A/D)
    /// A - увеличивает смещение в выбранном направлении
    /// D - уменьшает смещение в выбранном направлении
    /// </summary>
    private void HandleMovementDirectionInput()
    {
        // Если направление не выбрано, ничего не делаем
        if (currentMovementDirection == MovementDirection.None) return;
        if (selectedEngines.Count == 0) return;
        
        float directionDelta = 0f;
        
        // A - увеличение смещения
        if (Input.GetKey(increaseDirection))
        {
            directionDelta = directionChangeSpeed * Time.deltaTime;
        }
        // D - уменьшение смещения
        else if (Input.GetKey(decreaseDirection))
        {
            directionDelta = -directionChangeSpeed * Time.deltaTime;
        }
        
        // Если есть изменение направления
        if (Mathf.Abs(directionDelta) > 0.001f)
        {
            if (currentMovementDirection == MovementDirection.ForwardBackward)
            {
                // Изменяем смещение вперед/назад (Y компонента в desiredMovementDirection)
                desiredMovementDirection.y = Mathf.Clamp(desiredMovementDirection.y + directionDelta, -maxDirectionOffset, maxDirectionOffset);
            }
            else if (currentMovementDirection == MovementDirection.LeftRight)
            {
                // Изменяем смещение влево/вправо (X компонента в desiredMovementDirection)
                desiredMovementDirection.x = Mathf.Clamp(desiredMovementDirection.x + directionDelta, -maxDirectionOffset, maxDirectionOffset);
            }
            
            // Пересчитываем углы поворота двигателей на основе нового желаемого направления
            UpdateEngineRotationsFromMovementDirection();
        }
    }
    
    /// <summary>
    /// Вычисляет углы поворота двигателей на основе желаемого направления движения
    /// </summary>
    private void UpdateEngineRotationsFromMovementDirection()
    {
        if (selectedEngines.Count == 0) return;
        
        // Вычисляем углы поворота на основе желаемого направления движения
        // Если desiredMovementDirection.y > 0 (вперед), двигатели должны наклоняться назад (отрицательный угол X)
        // Если desiredMovementDirection.y < 0 (назад), двигатели должны наклоняться вперед (положительный угол X)
        // Если desiredMovementDirection.x > 0 (вправо), двигатели должны наклоняться влево (отрицательный угол Y)
        // Если desiredMovementDirection.x < 0 (влево), двигатели должны наклоняться вправо (положительный угол Y)
        
        // Используем настраиваемый максимальный угол наклона
        
        // Вычисляем углы поворота на основе желаемого направления
        // Угол X: наклон вперед/назад (положительный = наклон вперед, отрицательный = наклон назад)
        // Для движения вперед нужен наклон назад (отрицательный X)
        float targetAngleX = -desiredMovementDirection.y * maxTiltAngle;
        
        // Угол Y: наклон влево/вправо (положительный = наклон вправо, отрицательный = наклон влево)
        // Для движения вправо нужен наклон влево (отрицательный Y)
        float targetAngleY = -desiredMovementDirection.x * maxTiltAngle;
        
        // Применяем углы ко всем выбранным двигателям
        foreach (int engineIndex in selectedEngines)
        {
            if (engineIndex < 0 || engineIndex >= engines.Count) continue;
            if (engineIndex >= engineRotations.Length || engineIndex >= initialEngineRotations.Length) continue;
            
            // Устанавливаем углы поворота относительно начального положения
            engineRotations[engineIndex].x = initialEngineRotations[engineIndex].x + targetAngleX;
            engineRotations[engineIndex].y = initialEngineRotations[engineIndex].y + targetAngleY;
        }
        
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"Направление движения: ({desiredMovementDirection.x:F2}, {desiredMovementDirection.y:F2}), " +
                     $"Углы поворота: X={targetAngleX:F1}°, Y={targetAngleY:F1}°");
        }
    }
    
    /// <summary>
    /// Плавно обновляет поворот двигателей на основе сохраненных углов (вызывается каждый кадр)
    /// Все выбранные двигатели поворачиваются синхронно в одну сторону
    /// ВАЖНО: Поворот применяется относительно локальных осей КОРАБЛЯ, а не двигателя
    /// </summary>
    private void UpdateEngineRotationsSmoothly()
    {
        // Если двигатели не выбраны, применяем поворот ко всем двигателям
        if (selectedEngines.Count == 0)
        {
            // Если ничего не выбрано, используем все двигатели
            for (int i = 0; i < engines.Count; i++)
            {
                if (engines[i] == null) continue;
                if (i >= engineRotations.Length || i >= initialEngineRotations.Length) continue;
                
                ApplyEngineRotation(i);
            }
            return;
        }
        
        // Используем первый выбранный двигатель как эталон для синхронизации
        int firstSelectedIndex = selectedEngines.First();
        if (firstSelectedIndex < 0 || firstSelectedIndex >= engines.Count) return;
        if (engines[firstSelectedIndex] == null) return;
        if (firstSelectedIndex >= engineRotations.Length) return;
        
        // Получаем целевые углы из первого выбранного двигателя (для синхронизации)
        Vector2 targetAngles = engineRotations[firstSelectedIndex];
        Vector2 initialAngles = initialEngineRotations[firstSelectedIndex];
        
        // Вычисляем относительные углы поворота (разница от начального положения)
        float deltaX = targetAngles.x - initialAngles.x;
        float deltaY = targetAngles.y - initialAngles.y;
        
        // ВАЖНО: Применяем поворот относительно локальных осей КОРАБЛЯ, а не двигателя
        // Это гарантирует, что все двигатели наклоняются одинаково и создают поступательное движение
        // Локальные оси корабля: X = вправо, Y = вверх, Z = вперед
        Vector3 shipRight = transform.right;    // Локальная ось X корабля (вправо)
        Vector3 shipForward = transform.forward; // Локальная ось Z корабля (вперед)
        
        // Поворот вокруг локальной оси X корабля (наклон вперед/назад)
        // deltaX > 0 = наклон вперед, deltaX < 0 = наклон назад
        Quaternion xRotation = Quaternion.AngleAxis(deltaX, shipRight);
        
        // Поворот вокруг локальной оси Y корабля (наклон влево/вправо)
        // deltaY > 0 = наклон вправо, deltaY < 0 = наклон влево
        Quaternion yRotation = Quaternion.AngleAxis(deltaY, transform.up);
        
        // Комбинируем повороты: начальный поворот двигателя * поворот по Y корабля * поворот по X корабля
        // Порядок важен: сначала поворачиваем вокруг Y (влево/вправо), потом вокруг X (вперед/назад)
        Quaternion baseRotation = initialEngineRotationsQuat[firstSelectedIndex];
        Quaternion targetRotation = baseRotation * yRotation * xRotation;
        
        // Получаем текущий поворот первого выбранного двигателя
        Quaternion currentRotation = engines[firstSelectedIndex].transform.localRotation;
        
        // Плавно поворачиваем к целевому кватерниону (или мгновенно, если включен instantRotation)
        Quaternion newRotation;
        if (instantRotation)
        {
            // Мгновенный поворот для теста
            newRotation = targetRotation;
        }
        else
        {
            // Плавный поворот
            float maxRotationThisFrame = engineRotationSpeed * Time.deltaTime;
            newRotation = Quaternion.RotateTowards(currentRotation, targetRotation, maxRotationThisFrame);
        }
        
        // Отладочная информация (только для первого двигателя)
        if (showDebugInfo && Time.frameCount % 60 == 0) // Каждую секунду (при 60 FPS)
        {
            Vector3 engineForward = engines[firstSelectedIndex].transform.forward;
            Vector3 engineForwardLocal = transform.InverseTransformDirection(engineForward);
            Debug.Log($"Двигатель {firstSelectedIndex}: deltaX={deltaX:F2}°, deltaY={deltaY:F2}°, " +
                     $"engineForwardLocal={engineForwardLocal}, targetRotation={targetRotation.eulerAngles}");
        }
        
        // Применяем ОДИНАКОВЫЙ поворот ко ВСЕМ выбранным двигателям синхронно
        foreach (int engineIndex in selectedEngines)
        {
            if (engineIndex < 0 || engineIndex >= engines.Count) continue;
            if (engines[engineIndex] == null) continue;
            
            // Отключаем Rigidbody у двигателя, если он есть (чтобы не мешал повороту)
            Rigidbody engineRb = engines[engineIndex].GetComponent<Rigidbody>();
            if (engineRb != null)
            {
                engineRb.isKinematic = true; // Делаем kinematic, чтобы физика не мешала
            }
            
            // Синхронизируем углы всех выбранных двигателей с первым
            if (engineIndex < engineRotations.Length)
            {
                engineRotations[engineIndex] = engineRotations[firstSelectedIndex];
            }
            
            // Вычисляем поворот для этого двигателя (с учетом его начального положения)
            Quaternion engineBaseRotation = initialEngineRotationsQuat[engineIndex];
            Quaternion engineTargetRotation = engineBaseRotation * yRotation * xRotation;
            Quaternion engineCurrentRotation = engines[engineIndex].transform.localRotation;
            
            Quaternion engineNewRotation;
            if (instantRotation)
            {
                engineNewRotation = engineTargetRotation;
            }
            else
            {
                float maxRotationThisFrame = engineRotationSpeed * Time.deltaTime;
                engineNewRotation = Quaternion.RotateTowards(engineCurrentRotation, engineTargetRotation, maxRotationThisFrame);
            }
            
            // Применяем поворот к двигателю
            engines[engineIndex].transform.localRotation = engineNewRotation;
            
            // Дополнительная проверка: убеждаемся, что поворот применился
            if (showDebugInfo && engineIndex == firstSelectedIndex && Time.frameCount % 60 == 0)
            {
                Quaternion actualRotation = engines[engineIndex].transform.localRotation;
                Vector3 actualForward = engines[engineIndex].transform.forward;
                Vector3 actualForwardLocal = transform.InverseTransformDirection(actualForward);
                Debug.Log($"Применен поворот к двигателю {engineIndex}: {engines[engineIndex].gameObject.name}, " +
                         $"actualForwardLocal={actualForwardLocal}, actualRotation={actualRotation.eulerAngles}");
            }
        }
    }
    
    /// <summary>
    /// Применяет поворот к одному двигателю (для случая, когда двигатели не выбраны)
    /// </summary>
    private void ApplyEngineRotation(int engineIndex)
    {
        if (engineIndex < 0 || engineIndex >= engines.Count) return;
        if (engines[engineIndex] == null) return;
        if (engineIndex >= engineRotations.Length || engineIndex >= initialEngineRotations.Length) return;
        
        Vector2 angles = engineRotations[engineIndex];
        Vector2 initAngles = initialEngineRotations[engineIndex];
        
        float deltaX = angles.x - initAngles.x;
        float deltaY = angles.y - initAngles.y;
        
        // Применяем поворот относительно локальных осей КОРАБЛЯ
        Quaternion xRotation = Quaternion.AngleAxis(deltaX, transform.right);
        Quaternion yRotation = Quaternion.AngleAxis(deltaY, transform.up);
        
        Quaternion baseRot = initialEngineRotationsQuat[engineIndex];
        Quaternion targetRot = baseRot * yRotation * xRotation;
        
        Quaternion currentRot = engines[engineIndex].transform.localRotation;
        float maxRot = engineRotationSpeed * Time.deltaTime;
        Quaternion newRot = Quaternion.RotateTowards(currentRot, targetRot, maxRot);
        
        engines[engineIndex].transform.localRotation = newRot;
    }
    
    /// <summary>
    /// Плавно обновляет позицию двигателей относительно корабля (вызывается каждый кадр)
    /// ОТКЛЮЧЕНО: Двигатели теперь только поворачиваются, а не перемещаются
    /// </summary>
    private void UpdateEnginePositionsSmoothly()
    {
        // Метод отключен - двигатели теперь только поворачиваются для создания тяги в нужном направлении
        // Если нужно вернуть перемещение, раскомментируйте код ниже и обновите логику
        
        // Возвращаем двигатели к начальным позициям (если они были смещены)
        if (selectedEngines.Count == 0) return;
        
        foreach (int engineIndex in selectedEngines)
        {
            if (engineIndex < 0 || engineIndex >= engines.Count) continue;
            if (engines[engineIndex] == null) continue;
            if (engineIndex >= initialEnginePositions.Length) continue;
            
            // Плавно возвращаем к начальной позиции
            Vector3 currentPosition = engines[engineIndex].transform.localPosition;
            Vector3 targetPosition = initialEnginePositions[engineIndex];
            
            float maxMovementThisFrame = engineMovementSpeed * Time.deltaTime;
            Vector3 newPosition = Vector3.MoveTowards(currentPosition, targetPosition, maxMovementThisFrame);
            
            engines[engineIndex].transform.localPosition = newPosition;
        }
    }
    
    // УДАЛЕНО: Старые методы CalculateTargetRotationForDesiredVector и CalculateTargetAnglesForDesiredVector
    // Теперь используется UpdateEngineRotationsFromMovementDirection для управления поворотом двигателей
    
    /// <summary>
    /// Нормализует угол в диапазон -180 до 180
    /// </summary>
    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }
    
    /// <summary>
    /// Обработка ввода W/S для изменения тяги
    /// </summary>
    private void HandleThrustInput()
    {
        float targetThrust = currentThrust;
        
        // W - увеличиваем тягу
        if (Input.GetKey(KeyCode.W))
        {
            targetThrust = Mathf.Clamp01(currentThrust + thrustChangeSpeed * Time.deltaTime);
        }
        // S - уменьшаем тягу
        else if (Input.GetKey(KeyCode.S))
        {
            targetThrust = Mathf.Clamp01(currentThrust - thrustChangeSpeed * Time.deltaTime);
        }
        
        // Плавно изменяем тягу
        currentThrust = Mathf.Lerp(currentThrust, targetThrust, Time.deltaTime * thrustChangeSpeed);
        
        // Обновляем визуальные эффекты только выбранных двигателей
        UpdateEngineVisuals();
    }
    
    /// <summary>
    /// Обновляет визуальные эффекты двигателей
    /// Выбранные двигатели получают currentThrust, остальные сохраняют свою тягу
    /// </summary>
    private void UpdateEngineVisuals()
    {
        // Обновляем тягу для всех двигателей напрямую через EngineFireController
        for (int i = 0; i < engines.Count; i++)
        {
            if (engines[i] == null) continue;
            
            if (selectedEngines.Contains(i))
            {
                // Выбранные двигатели получают текущую тягу (обновляется через W/S)
                engineThrusts[i] = currentThrust;
                engines[i].SetThrust(currentThrust);
            }
            else
            {
                // Невыбранные двигатели сохраняют свою предыдущую тягу
                // НЕ меняем engineThrusts[i] - он остается как был
                engines[i].SetThrust(engineThrusts[i]);
            }
        }
        
        // Также обновляем через ShipThrusterManager, если он есть (для совместимости)
        if (thrusterManager != null)
        {
            for (int i = 0; i < engines.Count && i < 4; i++)
            {
                thrusterManager.SetEngineThrust(i, engineThrusts[i]);
            }
        }
    }
    
    /// <summary>
    /// Применяет силу от каждого двигателя в направлении его transform.forward
    /// Сила применяется к позиции двигателя, что создает реалистичный вращающий момент
    /// </summary>
    private void ApplyThrustFromEngines()
    {
        if (shipRigidbody == null || engines.Count == 0) return;
        
        // Вычисляем суммарную силу и момент для компенсации
        Vector3 totalForce = Vector3.zero;
        Vector3 totalTorque = Vector3.zero;
        Vector3 centerOfMass = shipRigidbody.worldCenterOfMass;
        
        // Проверяем, наклонены ли двигатели для поступательного движения
        bool enginesTilted = desiredMovementDirection.magnitude > 0.01f;
        
        // Проходим по ВСЕМ двигателям, но используем их индивидуальную тягу
        for (int i = 0; i < engines.Count; i++)
        {
            EngineFireController engine = engines[i];
            if (engine == null) continue;
            
            // Используем индивидуальную тягу каждого двигателя
            float engineThrust = engineThrusts[i];
            
            // Если тяга равна нулю, пропускаем
            if (engineThrust < 0.01f) continue;
            
            // Получаем направление двигателя
            // ВАЖНО: Если частицы двигателя летят вниз (transform.forward вниз),
            // то сила тяги должна быть направлена ВВЕРХ (противоположно направлению частиц)
            Vector3 engineDirection = -engine.transform.forward;
            
            // Получаем позицию двигателя в мировых координатах
            Vector3 enginePosition = engine.transform.position;
            
            // Вычисляем силу: максимальная сила * индивидуальная тяга двигателя
            float thrustForce = maxThrustForce * engineThrust;
            
            // Вектор силы (направлен ПРОТИВ направления выброса частиц)
            Vector3 force = engineDirection * thrustForce;
            
            // Если двигатели наклонены для поступательного движения, применяем силу к центру масс
            // чтобы избежать вращающего момента
            if (applyForceToCenter || enginesTilted)
            {
                // Применяем к центру масс для поступательного движения без вращения
                totalForce += force;
            }
            else
            {
                // Применяем силу к позиции двигателя (создает момент)
                shipRigidbody.AddForceAtPosition(force, enginePosition, ForceMode.Force);
                
                // Вычисляем момент для отладки
                Vector3 leverArm = enginePosition - centerOfMass;
                Vector3 torque = Vector3.Cross(leverArm, force);
                totalTorque += torque;
                
                // Отладочная информация
                if (showDebugInfo)
                {
                    Debug.Log($"Engine {i}: Thrust={engineThrust:F2}, Force={thrustForce:F1}N, " +
                              $"LeverArm={leverArm.magnitude:F2}m, Torque={torque.magnitude:F1}Nm");
                }
            }
        }
        
        // Если применяем к центру масс (когда двигатели наклонены или включена опция), применяем суммарную силу
        if ((applyForceToCenter || enginesTilted) && totalForce.magnitude > 0.01f)
        {
            shipRigidbody.AddForce(totalForce, ForceMode.Force);
            
            // Отладочная информация
            if (showDebugInfo && Time.frameCount % 60 == 0)
            {
                Vector3 totalForceLocal = transform.InverseTransformDirection(totalForce);
                Debug.Log($"Total Force Applied: {totalForce.magnitude:F1}N, Local: ({totalForceLocal.x:F2}, {totalForceLocal.y:F2}, {totalForceLocal.z:F2})");
            }
        }
        
        // Отладочная информация о суммарном моменте
        if (showDebugInfo && totalTorque.magnitude > 0.1f)
        {
            Debug.Log($"Total Torque: {totalTorque.magnitude:F1}Nm, Direction: {totalTorque.normalized}");
        }
    }
    
    /// <summary>
    /// Автоматически определяет режим среды по высоте корабля
    /// </summary>
    private void DetectEnvironmentMode()
    {
        if (shipRigidbody == null) return;
        
        float currentHeight = transform.position.y;
        EnvironmentMode newMode = currentHeight > atmosphereHeight ? EnvironmentMode.Space : EnvironmentMode.Atmosphere;
        
        // Обновляем настройки только если режим изменился
        if (newMode != currentEnvironment)
        {
            currentEnvironment = newMode;
            UpdateEnvironmentSettings();
        }
    }
    
    /// <summary>
    /// Обновляет настройки физики в зависимости от режима среды
    /// </summary>
    private void UpdateEnvironmentSettings()
    {
        if (shipRigidbody == null) return;
        
        switch (currentEnvironment)
        {
            case EnvironmentMode.Space:
                // Космос - минимальное сопротивление, но больше угловое сопротивление для стабилизации
                shipRigidbody.linearDamping = 0.05f;
                shipRigidbody.angularDamping = 5f;  // Увеличено для уменьшения вращения
                break;
                
            case EnvironmentMode.Atmosphere:
                // Атмосфера - больше сопротивление
                shipRigidbody.linearDamping = 0.3f;
                shipRigidbody.angularDamping = 10f; // Увеличено для уменьшения вращения
                break;
        }
    }
    
    /// <summary>
    /// Применяет гравитацию к кораблю
    /// </summary>
    private void ApplyGravity()
    {
        if (shipRigidbody == null) return;
        
        // Вычисляем силу гравитации: F = m * g
        Vector3 gravityForce = gravityDirection.normalized * gravityStrength * shipRigidbody.mass;
        
        // Применяем гравитацию к центру масс
        shipRigidbody.AddForce(gravityForce, ForceMode.Force);
    }
    
    /// <summary>
    /// Автоматическая стабилизация - компенсирует вращающий момент
    /// </summary>
    private void ApplyStabilization()
    {
        if (shipRigidbody == null) return;
        
        // Получаем угловую скорость
        Vector3 angularVelocity = shipRigidbody.angularVelocity;
        
        // Если угловая скорость слишком мала, не стабилизируем (чтобы не создавать дрожание)
        if (angularVelocity.magnitude < 0.001f) return;
        
        // Создаем противодействующий момент для стабилизации
        // Используем квадратичную зависимость для более сильного гашения больших скоростей
        float dampingFactor = 1f + angularVelocity.magnitude * 2f; // Усиливаем при больших скоростях
        Vector3 stabilizationTorque = -angularVelocity * stabilizationStrength * dampingFactor;
        shipRigidbody.AddTorque(stabilizationTorque, ForceMode.Force);
    }
    
    /// <summary>
    /// Получить текущую скорость корабля
    /// </summary>
    public float GetSpeed()
    {
        if (shipRigidbody == null) return 0f;
        return shipRigidbody.linearVelocity.magnitude;
    }
    
    /// <summary>
    /// Получить текущую тягу (0-1)
    /// </summary>
    public float GetCurrentThrust()
    {
        return currentThrust;
    }
    
    /// <summary>
    /// Получить тягу конкретного двигателя (0-1)
    /// </summary>
    public float GetEngineThrust(int engineIndex)
    {
        if (engineIndex >= 0 && engineIndex < engineThrusts.Length)
        {
            return engineThrusts[engineIndex];
        }
        return 0f;
    }
    
    /// <summary>
    /// Получить количество двигателей
    /// </summary>
    public int GetEngineCount()
    {
        return engines.Count;
    }
    
    /// <summary>
    /// Установить массу корабля
    /// </summary>
    public void SetMass(float newMass)
    {
        mass = newMass;
        if (shipRigidbody != null)
        {
            shipRigidbody.mass = mass;
        }
    }
    
    /// <summary>
    /// Получить текущий режим среды
    /// </summary>
    public EnvironmentMode GetEnvironmentMode()
    {
        return currentEnvironment;
    }
    
    /// <summary>
    /// Установить режим среды вручную
    /// </summary>
    public void SetEnvironmentMode(EnvironmentMode mode)
    {
        currentEnvironment = mode;
        UpdateEnvironmentSettings();
    }
    
    /// <summary>
    /// Получить текущую высоту корабля
    /// </summary>
    public float GetHeight()
    {
        return transform.position.y;
    }
    
    /// <summary>
    /// Включить/выключить гравитацию
    /// </summary>
    public void SetGravityEnabled(bool enabled)
    {
        useGravity = enabled;
    }
    
    /// <summary>
    /// Установить силу гравитации
    /// </summary>
    public void SetGravityStrength(float strength)
    {
        gravityStrength = strength;
    }
    
    /// <summary>
    /// Вычисляет и выводит информацию о TWR (Thrust to Weight Ratio)
    /// </summary>
    private void CalculateAndLogTWR()
    {
        if (shipRigidbody == null || engines.Count == 0) return;
        
        // Общая максимальная тяга всех двигателей
        float totalMaxThrust = maxThrustForce * engines.Count;
        
        // Вес корабля (масса * гравитация)
        float weight = mass * gravityStrength;
        
        // TWR = Тяга / Вес
        float twr = totalMaxThrust / weight;
        
        // Выводим информацию
        Debug.Log($"=== Ship Physics Info ===");
        Debug.Log($"Mass: {mass / 1000f:F1} tons ({mass:F0} kg)");
        Debug.Log($"Thrust per engine: {maxThrustForce / 1000f:F1} kN ({maxThrustForce:F0} N)");
        Debug.Log($"Total thrust ({engines.Count} engines): {totalMaxThrust / 1000f:F1} kN");
        Debug.Log($"Weight (at {gravityStrength:F2} m/s²): {weight / 1000f:F1} kN");
        Debug.Log($"TWR (Thrust to Weight Ratio): {twr:F2}");
        
        if (twr < 1.0f)
        {
            Debug.LogWarning("TWR < 1.0: Корабль не сможет взлететь!");
        }
        else if (twr < 1.5f)
        {
            Debug.LogWarning("TWR < 1.5: Медленный подъем, может быть недостаточно для посадки");
        }
        else if (twr > 3.0f)
        {
            Debug.LogWarning("TWR > 3.0: Очень высокая тяга, может быть слишком резко");
        }
        else
        {
            Debug.Log("TWR оптимален для космического корабля (1.5-3.0)");
        }
    }
    
    /// <summary>
    /// Получить текущий TWR (Thrust to Weight Ratio)
    /// </summary>
    public float GetCurrentTWR()
    {
        if (shipRigidbody == null || engines.Count == 0) return 0f;
        
        // Текущая общая тяга (с учетом индивидуальной тяги каждого двигателя)
        float totalCurrentThrust = 0f;
        for (int i = 0; i < engines.Count; i++)
        {
            totalCurrentThrust += maxThrustForce * engineThrusts[i];
        }
        
        // Вес корабля
        float weight = mass * gravityStrength;
        
        if (weight < 0.01f) return 0f;
        
        return totalCurrentThrust / weight;
    }
}

