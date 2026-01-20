using UnityEngine;
using System.Collections.Generic;
using System.Linq;




public enum EnvironmentMode
{
    Space,
    Atmosphere
}







public class ShipController : MonoBehaviour
{
    [Header("Physics")]
    [SerializeField] private Rigidbody shipRigidbody;
    [SerializeField] private float mass = 20000f;

    [Header("Center of Mass")]
    [SerializeField] private float centerOfMassOffsetX = 0f;
    [SerializeField] private float maxCenterOfMassOffset = 5f;
    [SerializeField] private bool showCenterOfMassGizmo = true;
    [SerializeField] private Color centerOfMassGizmoColor = Color.red;

    [Header("Environment Mode")]
    [SerializeField] private EnvironmentMode currentEnvironment = EnvironmentMode.Space;
    [SerializeField] private float atmosphereHeight = 100f;
    [SerializeField] private bool autoDetectEnvironment = true;

    [Header("Gravity")]
    [SerializeField] private bool useGravity = true;
    [SerializeField] private float gravityStrength = 9.81f;
    [SerializeField] private Vector3 gravityDirection = Vector3.down;

    [Header("Wind")]
    [SerializeField] private bool useWind = false;
    [SerializeField] private float windStrength = 0f;
    [SerializeField] private float windDirectionHorizontalAngle = 0f;
    [SerializeField] private float windDirectionVerticalAngle = 0f;
    [SerializeField] private float windHorizontalStrength = 1f; // Сила горизонтального ветра (X, Z) - контролируется компасом (0-1)
    [SerializeField] private float windHorizontalX = 0f; // Компонента X горизонтального ветра (-1 до +1) - для квадратного компаса
    [SerializeField] private float windHorizontalZ = 1f; // Компонента Z горизонтального ветра (-1 до +1) - для квадратного компаса
    [SerializeField] private float windVerticalStrength = 0f; // Сила вертикального ветра (Y) - контролируется слайдером (-1 до +1)
    [SerializeField] private bool useSquareCompass = true; // Использовать квадратный компас (X, Z напрямую) или круглый (угол + сила)
    [SerializeField] private float maxWindStrength = 1f;
    [SerializeField] private bool showWindGizmo = true;
    [SerializeField] private Color windGizmoColor = Color.cyan;
    
    [Header("Wind Physics - Ship Rotation")]
    [SerializeField] private bool enableWindRotation = true;
    [SerializeField] private float windLeverArmMultiplier = 0.5f;
    [SerializeField] private Vector3 shipSize = new Vector3(4f, 2f, 6f);
    [SerializeField] private bool autoCalculateShipSize = true;
    [SerializeField] private float windInfluencePower = 1.2f;
    [SerializeField] private bool useRealisticSurfaceArea = true;
    [SerializeField] private float surfaceAreaMultiplier = 1.0f;
    
    [Header("Wind Physics - Rectangular Shape")]
    [SerializeField] private float sideWindRotationMultiplier = 1.5f; // Усиление вращения от бокового ветра для прямоугольника
    [SerializeField] private float frontBackRotationMultiplier = 1.2f; // Усиление вращения от фронтального/заднего ветра
    [SerializeField] private float rectangularShapeFactor = 1.3f; // Фактор формы для прямоугольного корпуса

    [Header("Thrust Settings")]
    [SerializeField] private float maxThrustForce = 100000f;
    [SerializeField] private float thrustChangeSpeed = 1f;
    [SerializeField] private float minThrustForBalance = 0.1f;

    [Header("Thrust to Weight Ratio Info")]
    [SerializeField] private bool showTWRInfo = true;

    [Header("Engine Selection")]
    [SerializeField] private bool enableEngineSelection = true;
    [SerializeField] private KeyCode selectAllEngines = KeyCode.Alpha0;

    [Header("Movement Direction Control")]
    [SerializeField] private KeyCode selectForwardBackward = KeyCode.X;
    [SerializeField] private KeyCode selectLeftRight = KeyCode.Z;
    [SerializeField] private KeyCode increaseDirection = KeyCode.A;
    [SerializeField] private KeyCode decreaseDirection = KeyCode.D;
    [SerializeField] private float directionChangeSpeed = 2f;
    [SerializeField] private float maxDirectionOffset = 1f;
    [SerializeField] private float maxTiltAngle = 45f;
    [SerializeField] private float engineRotationSpeed = 360f;
    [SerializeField] private float maxEngineRotationAngle = 45f;
    [SerializeField] private bool instantRotation = false;

    [Header("Engine Movement")]
    [SerializeField] private float engineMovementSpeed = 2f;
    [SerializeField] private float maxEngineOffset = 2f;

    [Header("Stabilization")]
    [SerializeField] private bool autoStabilize = true;
    [SerializeField] private float stabilizationStrength = 50f;
    [SerializeField] private bool applyForceToCenter = false;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    [SerializeField] private bool showAutopilotTorqueDebug = false;
    [SerializeField] private int autopilotTorqueDebugFrameInterval = 30;

    [Header("References")]
    [SerializeField] private ShipThrusterManager thrusterManager;
    [SerializeField] private VolumetricCloudManager cloudManager;
    [SerializeField] private TurbulenceManager turbulenceManager;
    [SerializeField] private FuelManager fuelManager;
    
    [Header("Turbulence")]
    [SerializeField] private bool useTurbulence = true;
    [SerializeField] private float turbulenceForceMultiplier = 1f;
    [SerializeField] private float turbulenceTorqueMultiplier = 1f;


    private List<EngineFireController> engines = new List<EngineFireController>();


    private HashSet<int> selectedEngines = new HashSet<int>();


    private float[] engineThrusts = new float[4];
    private float cloudWindUpdateTimer = 0f;
    private const float cloudWindUpdateInterval = 1f;


    private Vector2[] engineRotations = new Vector2[4];


    private Vector2[] initialEngineRotations = new Vector2[4];


    private Quaternion[] initialEngineRotationsQuat = new Quaternion[4];


    private Vector3[] initialEnginePositions = new Vector3[4];





    private Vector2 desiredMovementDirection = Vector2.zero;


    private float currentThrust = 0f;


    private enum MovementDirection { None, ForwardBackward, LeftRight }
    private MovementDirection currentMovementDirection = MovementDirection.None;
    
    // Автопилот
    private bool autopilotActive = false;

    private void Awake()
    {

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
        
        // Находим FuelManager
        if (fuelManager == null)
        {
            fuelManager = FindObjectOfType<FuelManager>();
        }


        if (shipRigidbody != null)
        {
            shipRigidbody.mass = mass;


            shipRigidbody.isKinematic = false;
            shipRigidbody.useGravity = false;


            Collider shipCollider = GetComponent<Collider>();
            if (shipCollider == null)
            {
                Collider[] allColliders = GetComponentsInChildren<Collider>();
                foreach (Collider col in allColliders)
                {
                    if (col == null) continue;
                    string objName = col.gameObject.name.ToLower();
                    if (objName.Contains("tank") || objName.Contains("liquid") || objName.Contains("leak"))
                    {
                        continue;
                    }
                    shipCollider = col;
                    break;
                }
            }

            if (shipCollider == null)
            {

                Debug.LogWarning("ShipController: У корабля нет Collider! Автоматически создаю Box Collider.");
                shipCollider = gameObject.AddComponent<BoxCollider>();


                Renderer renderer = GetComponent<Renderer>();
                if (renderer == null)
                {
                    renderer = GetComponentInChildren<Renderer>();
                }

                if (renderer != null)
                {

                    Bounds bounds = renderer.bounds;
                    BoxCollider boxCollider = shipCollider as BoxCollider;
                    if (boxCollider != null)
                    {

                        boxCollider.center = transform.InverseTransformPoint(bounds.center);
                        boxCollider.size = bounds.size;
                        Debug.Log($"ShipController: Box Collider автоматически настроен. Size: {boxCollider.size}, Center: {boxCollider.center}");
                    }
                }
                else
                {

                    BoxCollider boxCollider = shipCollider as BoxCollider;
                    if (boxCollider != null)
                    {
                        boxCollider.size = new Vector3(2f, 1f, 4f);
                        boxCollider.center = Vector3.zero;
                        Debug.Log("ShipController: Box Collider создан с размером по умолчанию. Настройте размер вручную в Inspector.");
                    }
                }
            }
            else
            {

                shipCollider.enabled = true;
                Debug.Log($"ShipController: Найден Collider: {shipCollider.GetType().Name} на объекте {shipCollider.gameObject.name}");
            }


            SetupLegColliders();


            UpdateCenterOfMass();


            shipRigidbody.useGravity = false;


            UpdateEnvironmentSettings();


            if (showTWRInfo && engines.Count > 0)
            {
                CalculateAndLogTWR();
            }
        }


        FindAllEngines();


        if (showTWRInfo && shipRigidbody != null && engines.Count > 0)
        {
            CalculateAndLogTWR();
        }
        
        UpdateCloudWind();
    }




    private void FindAllEngines()
    {
        engines.Clear();


        engines.AddRange(GetComponentsInChildren<EngineFireController>());


        if (engines.Count == 0 && thrusterManager != null)
        {


            engines.AddRange(GetComponentsInChildren<EngineFireController>());
        }

        Debug.Log($"ShipController: Найдено двигателей: {engines.Count}");


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

            engineThrusts = new float[engines.Count];
            engineRotations = new Vector2[engines.Count];
            initialEngineRotations = new Vector2[engines.Count];
            initialEngineRotationsQuat = new Quaternion[engines.Count];
            initialEnginePositions = new Vector3[engines.Count];

            for (int i = 0; i < engines.Count; i++)
            {
                engineThrusts[i] = 0f;


                if (engines[i] != null)
                {

                    Rigidbody engineRb = engines[i].GetComponent<Rigidbody>();
                    if (engineRb != null)
                    {
                        engineRb.isKinematic = true;
                        Debug.Log($"ShipController: Двигатель {i + 1} ({engines[i].gameObject.name}) имеет Rigidbody - установлен isKinematic = true");
                    }


                    initialEnginePositions[i] = engines[i].transform.localPosition;


                    Vector3 localEuler = engines[i].transform.localEulerAngles;
                    float normalizedX = NormalizeAngle(localEuler.x);
                    float normalizedY = NormalizeAngle(localEuler.y);
                    Vector2 initialAngles = new Vector2(normalizedX, normalizedY);
                    initialEngineRotations[i] = initialAngles;
                    engineRotations[i] = initialAngles;

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


            SelectAllEngines();
        }
    }




    private void SetupLegColliders()
    {

        Transform stepersParent = null;
        foreach (Transform child in transform)
        {
            if (child.name.ToLower().Contains("step") || child.name.ToLower().Contains("leg"))
            {
                stepersParent = child;
                break;
            }
        }


        List<Transform> legObjects = new List<Transform>();

        if (stepersParent != null)
        {

            foreach (Transform leg in stepersParent)
            {
                if (leg.name.ToLower().Contains("step") || leg.name.ToLower().Contains("leg"))
                {
                    legObjects.Add(leg);
                }
            }
        }
        else
        {

            foreach (Transform child in transform)
            {
                if (child.name.ToLower().Contains("step") || child.name.ToLower().Contains("leg"))
                {
                    legObjects.Add(child);
                }
            }
        }


        if (legObjects.Count == 0)
        {
            Transform[] allChildren = GetComponentsInChildren<Transform>();
            foreach (Transform t in allChildren)
            {
                if (t != transform && (t.name.Contains("Steper") || t.name.Contains("steper") || 
                    t.name.Contains("Leg") || t.name.Contains("leg")))
                {
                    legObjects.Add(t);
                }
            }
        }


        int collidersCreated = 0;
        int collidersFound = 0;
        int rigidbodiesFixed = 0;



        List<Collider> shipColliders = new List<Collider>();
        shipColliders.AddRange(GetComponents<Collider>());
        shipColliders.AddRange(GetComponentsInChildren<Collider>());


        HashSet<string> legNames = new HashSet<string>();
        foreach (Transform leg in legObjects)
        {
            if (leg != null)
            {
                legNames.Add(leg.name.ToLower());

                foreach (Transform child in leg.GetComponentsInChildren<Transform>())
                {
                    legNames.Add(child.name.ToLower());
                }
            }
        }

        HashSet<string> tankNames = new HashSet<string>();
        Transform tanksParent = transform.Find("tanks");
        if (tanksParent != null)
        {
            tankNames.Add("tanks");
            foreach (Transform tank in tanksParent)
            {
                if (tank != null)
                {
                    tankNames.Add(tank.name.ToLower());
                    foreach (Transform child in tank.GetComponentsInChildren<Transform>())
                    {
                        tankNames.Add(child.name.ToLower());
                    }
                }
            }
        }


        shipColliders = shipColliders.Distinct()
            .Where(c => c != null && 
                   !legNames.Contains(c.gameObject.name.ToLower()) &&
                   !legNames.Any(legName => c.gameObject.name.ToLower().Contains(legName)) &&
                   !tankNames.Contains(c.gameObject.name.ToLower()) &&
                   !tankNames.Any(tankName => c.gameObject.name.ToLower().Contains(tankName)))
            .ToList();

        Debug.Log($"ShipController: Найдено {shipColliders.Count} коллайдеров корабля (исключая ноги и баки)");

        foreach (Transform leg in legObjects)
        {
            if (leg == null) continue;


            Collider legCollider = leg.GetComponent<Collider>();

            if (legCollider == null)
            {

                legCollider = leg.gameObject.AddComponent<BoxCollider>();


                Renderer legRenderer = leg.GetComponent<Renderer>();
                if (legRenderer != null)
                {
                    Bounds bounds = legRenderer.bounds;
                    BoxCollider boxCollider = legCollider as BoxCollider;
                    if (boxCollider != null)
                    {

                        boxCollider.center = leg.InverseTransformPoint(bounds.center);
                        boxCollider.size = bounds.size;
                    }
                }
                else
                {

                    BoxCollider boxCollider = legCollider as BoxCollider;
                    if (boxCollider != null)
                    {
                        boxCollider.size = new Vector3(0.5f, 1f, 0.5f);
                        boxCollider.center = Vector3.zero;
                    }
                }

                collidersCreated++;
                Debug.Log($"ShipController: Создан Box Collider для ноги: {leg.name}");
            }
            else
            {

                legCollider.enabled = true;
                collidersFound++;
            }







            Rigidbody legRigidbody = leg.GetComponent<Rigidbody>();
            if (legRigidbody != null)
            {
                DestroyImmediate(legRigidbody);
                rigidbodiesFixed++;
                Debug.Log($"ShipController: Rigidbody удален с ноги {leg.name} - нога теперь часть корабля");
            }


            FixedJoint oldFixedJoint = leg.GetComponent<FixedJoint>();
            if (oldFixedJoint != null)
            {
                DestroyImmediate(oldFixedJoint);
            }

            ConfigurableJoint configJoint = leg.GetComponent<ConfigurableJoint>();
            if (configJoint != null)
            {
                DestroyImmediate(configJoint);
            }



            if (leg.gameObject.layer != gameObject.layer)
            {
                leg.gameObject.layer = gameObject.layer;
            }





            if (legCollider != null)
            {
                legCollider.isTrigger = false;

                legCollider.enabled = true;





                Debug.Log($"ShipController: Коллайдер ноги {leg.name} настроен как часть корабля (составной коллайдер)");
            }
        }

        if (legObjects.Count > 0)
        {
            Debug.Log($"ShipController: Обработано ног: {legObjects.Count}, создано коллайдеров: {collidersCreated}, найдено существующих: {collidersFound}, исправлено Rigidbody: {rigidbodiesFixed}");
        }
        else
        {
            Debug.LogWarning("ShipController: Ноги корабля (stepers) не найдены! Убедитесь, что объекты с именами, содержащими 'Steper' или 'leg', существуют в иерархии корабля.");
        }
    }

    private void Update()
    {
        autopilotTorqueDebugFrameInterval = Mathf.Max(1, autopilotTorqueDebugFrameInterval);


        if (shipRigidbody != null)
        {

            Vector3 currentCenterOfMass = shipRigidbody.centerOfMass;
            Vector3 expectedCenterOfMass = new Vector3(centerOfMassOffsetX, 0f, 0f);

            if (Vector3.Distance(currentCenterOfMass, expectedCenterOfMass) > 0.001f)
            {
                UpdateCenterOfMass();
            }
        }


        if (enableEngineSelection)
        {
            HandleEngineSelection();
        }


        HandleMovementDirectionSelection();


        HandleMovementDirectionInput();





        UpdateEngineRotationsSmoothly();




        HandleThrustInput();
        
        // Проверяем топливо и отключаем двигатели, если топливо закончилось
        CheckFuelAndDisableEnginesIfEmpty();


        if (autoDetectEnvironment)
        {
            DetectEnvironmentMode();
        }
        
        cloudWindUpdateTimer += Time.deltaTime;
        if (cloudWindUpdateTimer >= cloudWindUpdateInterval)
        {
            cloudWindUpdateTimer = 0f;
            UpdateCloudWind();
        }
    }

    private void FixedUpdate()
    {



        if (shipRigidbody != null)
        {

            if (shipRigidbody.constraints == RigidbodyConstraints.FreezeAll)
            {
                Debug.LogWarning("ShipController: Rigidbody заблокирован! Разблокирую движение.");
                shipRigidbody.constraints = RigidbodyConstraints.None;
            }


            if (showDebugInfo && Time.frameCount % 60 == 0)
            {
                Debug.Log($"Ship Velocity: {shipRigidbody.linearVelocity.magnitude:F2} m/s, Angular: {shipRigidbody.angularVelocity.magnitude:F4} rad/s");
            }
        }


        if (useGravity)
        {
            ApplyGravity();
        }


        if (useWind)
        {
            ApplyWind();
        }
        
        if (useTurbulence)
        {
            ApplyTurbulence();
        }

        ApplyThrustFromEngines();


        if (autoStabilize)
        {
            ApplyStabilization();
        }
    }




    private void HandleEngineSelection()
    {
        // Если автопилот активен, игнорируем ручное управление
        if (autopilotActive) return;

        if (Input.GetKeyDown(selectAllEngines))
        {
            SelectAllEngines();
            Debug.Log("Выбраны все двигатели");
            return;
        }


        for (int i = 1; i <= 4; i++)
        {
            KeyCode key = (KeyCode)((int)KeyCode.Alpha1 + (i - 1));
            if (Input.GetKeyDown(key))
            {
                int engineIndex = i - 1;
                SelectEngine(engineIndex);
                Debug.Log($"Выбран двигатель {i} (индекс {engineIndex})");
            }
        }
    }




    private void SelectEngine(int engineIndex)
    {
        if (engineIndex < 0)
        {
            Debug.LogWarning($"ShipController: Некорректный индекс двигателя: {engineIndex}");
            return;
        }
        if (engineIndex >= engines.Count)
        {
            Debug.LogWarning($"ShipController: Некорректный индекс двигателя: {engineIndex}");
            return;
        }

        selectedEngines.Clear();
        selectedEngines.Add(engineIndex);
        currentThrust = engineThrusts[engineIndex];

        for (int i = 0; i < engines.Count; i++)
        {
            if (i != engineIndex)
            {
                engineThrusts[i] = currentThrust;
            }
        }

        currentMovementDirection = MovementDirection.None;
        UpdateEngineVisuals();
    }




    private void SelectAllEngines()
    {
        selectedEngines.Clear();
        for (int i = 0; i < engines.Count; i++)
        {
            selectedEngines.Add(i);
        }

        float sum = 0f;
        bool hasThrust = false;
        for (int i = 0; i < engines.Count; i++)
        {
            sum = sum + engineThrusts[i];
            if (engineThrusts[i] > 0.01f)
            {
                hasThrust = true;
            }
        }

        float avg = 0f;
        if (engines.Count > 0)
        {
            avg = sum / engines.Count;
        }

        if (hasThrust == false)
        {
            avg = 0f;
            currentThrust = 0f;
        }
        else
        {
            currentThrust = avg;
        }

        currentMovementDirection = MovementDirection.None;
        UpdateEngineVisuals();
    }




    private void HandleMovementDirectionSelection()
    {
        // Если автопилот активен, игнорируем ручное управление
        if (autopilotActive) return;
        
        if (Input.GetKeyDown(selectForwardBackward))
        {
            currentMovementDirection = MovementDirection.ForwardBackward;
            Debug.Log("Выбрано направление: Вперед/Назад (X)");
        }
        
        if (Input.GetKeyDown(selectLeftRight))
        {
            currentMovementDirection = MovementDirection.LeftRight;
            Debug.Log("Выбрано направление: Влево/Вправо (Z)");
        }
    }






    private void HandleMovementDirectionInput()
    {
        // Если автопилот активен, игнорируем ручное управление
        if (autopilotActive) return;

        if (currentMovementDirection == MovementDirection.None) return;
        if (selectedEngines.Count == 0) return;

        float directionDelta = 0f;


        if (Input.GetKey(increaseDirection))
        {
            directionDelta = directionChangeSpeed * Time.deltaTime;
        }

        else if (Input.GetKey(decreaseDirection))
        {
            directionDelta = -directionChangeSpeed * Time.deltaTime;
        }


        if (Mathf.Abs(directionDelta) > 0.001f)
        {
            if (currentMovementDirection == MovementDirection.ForwardBackward)
            {

                desiredMovementDirection.y = Mathf.Clamp(desiredMovementDirection.y + directionDelta, -maxDirectionOffset, maxDirectionOffset);
            }
            else if (currentMovementDirection == MovementDirection.LeftRight)
            {

                desiredMovementDirection.x = Mathf.Clamp(desiredMovementDirection.x + directionDelta, -maxDirectionOffset, maxDirectionOffset);
            }


            UpdateEngineRotationsFromMovementDirection();
        }
    }




    private void UpdateEngineRotationsFromMovementDirection()
    {
        if (selectedEngines.Count == 0) return;












        // ВАЖНО: Двигатели изначально направлены ВНИЗ (для создания тяги вверх)
        // Когда desiredMovementDirection.y < 0 (назад), нужно наклонять двигатель НАЗАД (отрицательный угол)
        // Поэтому убираем минус перед desiredMovementDirection.y
        float targetAngleX = desiredMovementDirection.y * maxTiltAngle;



        float targetAngleY = -desiredMovementDirection.x * maxTiltAngle;


        foreach (int engineIndex in selectedEngines)
        {
            if (engineIndex < 0 || engineIndex >= engines.Count) continue;
            if (engineIndex >= engineRotations.Length || engineIndex >= initialEngineRotations.Length) continue;


            engineRotations[engineIndex].x = initialEngineRotations[engineIndex].x + targetAngleX;
            engineRotations[engineIndex].y = initialEngineRotations[engineIndex].y + targetAngleY;
        }

        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"ShipController: === ПОВОРОТ ДВИГАТЕЛЕЙ ===");
            Debug.Log($"  ВАЖНО: В Unity координаты: X (влево/вправо), Y (вверх/вниз), Z (вперед/назад)");
            Debug.Log($"  Входные данные (SetMovementDirection, Vector2):");
            Debug.Log($"    desiredMovementDirection.x={desiredMovementDirection.x:F3} (влево/вправо, локальный X корабля)");
            Debug.Log($"    desiredMovementDirection.y={desiredMovementDirection.y:F3} (вперед/назад, локальный Z корабля, НЕ Unity Y!)");
            Debug.Log($"  Вычисленные углы поворота:");
            Debug.Log($"    targetAngleX={targetAngleX:F1}° = desiredMovementDirection.y * maxTiltAngle (наклон вперед/назад, вокруг оси X/shipRight)");
            Debug.Log($"    targetAngleY={targetAngleY:F1}° = -desiredMovementDirection.x * maxTiltAngle (поворот влево/вправо, вокруг оси Y/transform.up)");
            Debug.Log($"  ПРОВЕРКА (двигатели направлены ВНИЗ):");
            Debug.Log($"    Если desiredMovementDirection.y > 0 (вперед, локальный Z) → targetAngleX > 0 (наклон вперед) → двигатель наклоняется вперед → forward направлен вперед → engineDirection назад → сила вперед ✓");
            Debug.Log($"    Если desiredMovementDirection.y < 0 (назад, локальный Z) → targetAngleX < 0 (наклон назад) → двигатель наклоняется назад → forward направлен назад → engineDirection вперед → сила назад ✓");
            Debug.Log($"    Если desiredMovementDirection.x > 0 (вправо, локальный X) → targetAngleY < 0 (поворот влево) → двигатель поворачивается влево → forward направлен влево → engineDirection вправо → сила вправо ✓");
            Debug.Log($"  Фактически:");
            Debug.Log($"    desiredMovementDirection.y={desiredMovementDirection.y:F3} → targetAngleX={targetAngleX:F1}°");
            Debug.Log($"    desiredMovementDirection.x={desiredMovementDirection.x:F3} → targetAngleY={targetAngleY:F1}°");
        }
    }






    private void UpdateEngineRotationsSmoothly()
    {

        if (selectedEngines.Count == 0)
        {

            for (int i = 0; i < engines.Count; i++)
            {
                if (engines[i] == null) continue;
                if (i >= engineRotations.Length || i >= initialEngineRotations.Length) continue;

                ApplyEngineRotation(i);
            }
            return;
        }


        int firstSelectedIndex = selectedEngines.First();
        if (firstSelectedIndex < 0 || firstSelectedIndex >= engines.Count) return;
        if (engines[firstSelectedIndex] == null) return;
        if (firstSelectedIndex >= engineRotations.Length) return;


        Vector2 targetAngles = engineRotations[firstSelectedIndex];
        Vector2 initialAngles = initialEngineRotations[firstSelectedIndex];


        float deltaX = targetAngles.x - initialAngles.x;
        float deltaY = targetAngles.y - initialAngles.y;




        Vector3 shipRight = transform.right;
        Vector3 shipForward = transform.forward;



        Quaternion xRotation = Quaternion.AngleAxis(deltaX, shipRight);



        Quaternion yRotation = Quaternion.AngleAxis(deltaY, transform.up);



        Quaternion baseRotation = initialEngineRotationsQuat[firstSelectedIndex];
        Quaternion targetRotation = baseRotation * yRotation * xRotation;


        Quaternion currentRotation = engines[firstSelectedIndex].transform.localRotation;


        Quaternion newRotation;
        if (instantRotation)
        {

            newRotation = targetRotation;
        }
        else
        {

            float maxRotationThisFrame = engineRotationSpeed * Time.deltaTime;
            newRotation = Quaternion.RotateTowards(currentRotation, targetRotation, maxRotationThisFrame);
        }


        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Vector3 engineForward = engines[firstSelectedIndex].transform.forward;
            Vector3 engineForwardLocal = transform.InverseTransformDirection(engineForward);
            Debug.Log($"Двигатель {firstSelectedIndex}: deltaX={deltaX:F2}°, deltaY={deltaY:F2}°, " +
                     $"engineForwardLocal={engineForwardLocal}, targetRotation={targetRotation.eulerAngles}");
        }


        foreach (int engineIndex in selectedEngines)
        {
            if (engineIndex < 0 || engineIndex >= engines.Count) continue;
            if (engines[engineIndex] == null) continue;


            Rigidbody engineRb = engines[engineIndex].GetComponent<Rigidbody>();
            if (engineRb != null)
            {
                engineRb.isKinematic = true;
            }


            if (engineIndex < engineRotations.Length)
            {
                engineRotations[engineIndex] = engineRotations[firstSelectedIndex];
            }


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


            engines[engineIndex].transform.localRotation = engineNewRotation;


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




    private void ApplyEngineRotation(int engineIndex)
    {
        if (engineIndex < 0 || engineIndex >= engines.Count) return;
        if (engines[engineIndex] == null) return;
        if (engineIndex >= engineRotations.Length || engineIndex >= initialEngineRotations.Length) return;

        Vector2 angles = engineRotations[engineIndex];
        Vector2 initAngles = initialEngineRotations[engineIndex];

        float deltaX = angles.x - initAngles.x;
        float deltaY = angles.y - initAngles.y;


        Quaternion xRotation = Quaternion.AngleAxis(deltaX, transform.right);
        Quaternion yRotation = Quaternion.AngleAxis(deltaY, transform.up);

        Quaternion baseRot = initialEngineRotationsQuat[engineIndex];
        Quaternion targetRot = baseRot * yRotation * xRotation;

        Quaternion currentRot = engines[engineIndex].transform.localRotation;
        float maxRot = engineRotationSpeed * Time.deltaTime;
        Quaternion newRot = Quaternion.RotateTowards(currentRot, targetRot, maxRot);

        engines[engineIndex].transform.localRotation = newRot;
    }





    private void UpdateEnginePositionsSmoothly()
    {




        if (selectedEngines.Count == 0) return;

        foreach (int engineIndex in selectedEngines)
        {
            if (engineIndex < 0 || engineIndex >= engines.Count) continue;
            if (engines[engineIndex] == null) continue;
            if (engineIndex >= initialEnginePositions.Length) continue;


            Vector3 currentPosition = engines[engineIndex].transform.localPosition;
            Vector3 targetPosition = initialEnginePositions[engineIndex];

            float maxMovementThisFrame = engineMovementSpeed * Time.deltaTime;
            Vector3 newPosition = Vector3.MoveTowards(currentPosition, targetPosition, maxMovementThisFrame);

            engines[engineIndex].transform.localPosition = newPosition;
        }
    }







    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }




    private void HandleThrustInput()
    {
        // Если автопилот активен, игнорируем ручное управление
        if (autopilotActive) return;
        
        // Проверяем топливо перед обработкой ввода
        bool hasFuel = HasFuel();
        
        float targetThrust = currentThrust;

        if (Input.GetKey(KeyCode.W) == true && hasFuel)
        {
            float newThrust = currentThrust + thrustChangeSpeed * Time.deltaTime;
            if (newThrust > 1f)
            {
                targetThrust = 1f;
            }
            else if (newThrust < 0f)
            {
                targetThrust = 0f;
            }
            else
            {
                targetThrust = newThrust;
            }
        }
        else if (Input.GetKey(KeyCode.S) == true)
        {
            float newThrust = currentThrust - thrustChangeSpeed * Time.deltaTime;
            if (newThrust > 1f)
            {
                targetThrust = 1f;
            }
            else if (newThrust < 0f)
            {
                targetThrust = 0f;
            }
            else
            {
                targetThrust = newThrust;
            }
        }
        
        // Если нет топлива, принудительно устанавливаем тягу в 0
        if (!hasFuel)
        {
            targetThrust = 0f;
        }

        float lerpFactor = Time.deltaTime * thrustChangeSpeed;
        currentThrust = currentThrust + (targetThrust - currentThrust) * lerpFactor;
        if (currentThrust > 1f)
        {
            currentThrust = 1f;
        }
        if (currentThrust < 0f)
        {
            currentThrust = 0f;
        }

        UpdateEngineVisuals();
    }





    private void UpdateEngineVisuals()
    {
        for (int i = 0; i < engines.Count; i++)
        {
            if (engines[i] == null)
            {
                continue;
            }

            bool isSelected = selectedEngines.Contains(i);
            if (isSelected)
            {
                engineThrusts[i] = currentThrust;
                engines[i].SetThrust(currentThrust);
            }
            else
            {
                engines[i].SetThrust(engineThrusts[i]);
            }
        }

        if (thrusterManager != null)
        {
            int maxEngines = engines.Count;
            if (maxEngines > 4)
            {
                maxEngines = 4;
            }
            for (int i = 0; i < maxEngines; i++)
            {
                thrusterManager.SetEngineThrust(i, engineThrusts[i]);
            }
        }
    }





    private void ApplyThrustFromEngines()
    {
        if (shipRigidbody == null)
        {
            return;
        }
        
        if (engines.Count == 0)
        {
            return;
        }
        
        // Проверяем топливо - если его нет, не применяем тягу
        if (!HasFuel())
        {
            // Отключаем все двигатели
            for (int i = 0; i < engines.Count; i++)
            {
                if (i < engineThrusts.Length)
                {
                    engineThrusts[i] = 0f;
                }
                if (engines[i] != null)
                {
                    engines[i].SetThrust(0f);
                }
            }
            currentThrust = 0f;
            return;
        }

        Vector3 totalForce = new Vector3(0f, 0f, 0f);
        Vector3 totalTorque = new Vector3(0f, 0f, 0f);
        Vector3 centerOfMass = shipRigidbody.worldCenterOfMass;

        float movementX = desiredMovementDirection.x;
        float movementY = desiredMovementDirection.y;
        float movementMagnitude = Mathf.Sqrt(movementX * movementX + movementY * movementY);
        bool enginesTilted = movementMagnitude > 0.01f;

        for (int i = 0; i < engines.Count; i++)
        {
            EngineFireController engine = engines[i];
            if (engine == null)
            {
                continue;
            }

            float engineThrust = engineThrusts[i];

            if (engineThrust < 0.01f)
            {
                continue;
            }

            Vector3 engineForward = engine.transform.forward;
            Vector3 engineDirection = new Vector3(-engineForward.x, -engineForward.y, -engineForward.z);

            Vector3 enginePosition = engine.transform.position;

            float thrustForce = maxThrustForce * engineThrust;

            Vector3 force = new Vector3(
                engineDirection.x * thrustForce,
                engineDirection.y * thrustForce,
                engineDirection.z * thrustForce
            );

            if (applyForceToCenter == true)
            {
                totalForce = new Vector3(
                    totalForce.x + force.x,
                    totalForce.y + force.y,
                    totalForce.z + force.z
                );
            }
            else
            {
                shipRigidbody.AddForceAtPosition(force, enginePosition, ForceMode.Force);
                
                Vector3 leverArm = new Vector3(
                    enginePosition.x - centerOfMass.x,
                    enginePosition.y - centerOfMass.y,
                    enginePosition.z - centerOfMass.z
                );
                Vector3 torque = Vector3.Cross(leverArm, force);
                totalTorque = new Vector3(
                    totalTorque.x + torque.x,
                    totalTorque.y + torque.y,
                    totalTorque.z + torque.z
                );
                
                if (showDebugInfo == true)
                {
                    float leverArmLength = Mathf.Sqrt(leverArm.x * leverArm.x + leverArm.y * leverArm.y + leverArm.z * leverArm.z);
                    float torqueLength = Mathf.Sqrt(torque.x * torque.x + torque.y * torque.y + torque.z * torque.z);
                    Debug.Log($"Engine {i}: Thrust={engineThrust:F2}, Force={thrustForce:F1}N, " +
                              $"LeverArm={leverArmLength:F2}m, Torque={torqueLength:F1}Nm");
                }
            }
        }

        if (applyForceToCenter == true)
        {
            float totalForceLength = Mathf.Sqrt(totalForce.x * totalForce.x + totalForce.y * totalForce.y + totalForce.z * totalForce.z);
            if (totalForceLength > 0.01f)
            {
                shipRigidbody.AddForce(totalForce, ForceMode.Force);

                if (showDebugInfo == true)
                {
                    if (Time.frameCount % 60 == 0)
                    {
                        Vector3 totalForceLocal = transform.InverseTransformDirection(totalForce);
                        Debug.Log($"ShipController: === ПРИМЕНЕНИЕ СИЛЫ ===");
                        Debug.Log($"  enginesTilted={enginesTilted}, applyForceToCenter={applyForceToCenter}");
                        Debug.Log($"  desiredMovementDirection: ({desiredMovementDirection.x:F3}, {desiredMovementDirection.y:F3})");
                        Debug.Log($"  Total Force (мировые): ({totalForce.x:F1}, {totalForce.y:F1}, {totalForce.z:F1}) N, Magnitude: {totalForceLength:F1}N");
                        Debug.Log($"  Total Force (локальные): ({totalForceLocal.x:F2}, {totalForceLocal.y:F2}, {totalForceLocal.z:F2})");
                        Debug.Log($"    X (влево/вправо): {totalForceLocal.x:F2}");
                        Debug.Log($"    Y (вверх/вниз): {totalForceLocal.y:F2}");
                        Debug.Log($"    Z (вперед/назад): {totalForceLocal.z:F2}");
                        
                        // Показываем направление каждого двигателя
                        for (int i = 0; i < engines.Count && i < 4; i++)
                        {
                            if (engines[i] != null)
                            {
                                Vector3 engineForward = engines[i].transform.forward;
                                Vector3 engineForwardLocal = transform.InverseTransformDirection(engineForward);
                                float thrust = (i < engineThrusts.Length) ? engineThrusts[i] : 0f;
                                Debug.Log($"  Двигатель {i}: Thrust={thrust:F2}, Forward (локальный)={engineForwardLocal}");
                            }
                        }
                    }
                }
            }
        }

        if (showAutopilotTorqueDebug && autopilotActive && Time.frameCount % autopilotTorqueDebugFrameInterval == 0)
        {
            float totalTorqueLength = Mathf.Sqrt(totalTorque.x * totalTorque.x + totalTorque.y * totalTorque.y + totalTorque.z * totalTorque.z);
            Vector3 angularVelocity = shipRigidbody != null ? shipRigidbody.angularVelocity : Vector3.zero;
            Debug.Log(
                $"Autopilot Torque: total={totalTorqueLength:F1}Nm, " +
                $"dir={(totalTorqueLength > 0.1f ? (totalTorque / totalTorqueLength) : Vector3.zero)}, " +
                $"angVel={angularVelocity.x:F3},{angularVelocity.y:F3},{angularVelocity.z:F3}"
            );
        }
    }




    private void DetectEnvironmentMode()
    {
        if (shipRigidbody == null)
        {
            return;
        }

        float currentHeight = transform.position.y;
        EnvironmentMode newMode = EnvironmentMode.Space;
        if (currentHeight > atmosphereHeight)
        {
            newMode = EnvironmentMode.Space;
        }
        else
        {
            newMode = EnvironmentMode.Atmosphere;
        }

        if (newMode != currentEnvironment)
        {
            currentEnvironment = newMode;
            UpdateEnvironmentSettings();
        }
    }




    private void UpdateEnvironmentSettings()
    {
        if (shipRigidbody == null) return;

        switch (currentEnvironment)
        {
            case EnvironmentMode.Space:

                shipRigidbody.linearDamping = 0.05f;
                shipRigidbody.angularDamping = 5f;
                break;

            case EnvironmentMode.Atmosphere:

                shipRigidbody.linearDamping = 0.3f;
                shipRigidbody.angularDamping = 10f;
                break;
        }
    }






    private void ApplyGravity()
    {
        if (shipRigidbody == null)
        {
            return;
        }

        float gravityDirLength = Mathf.Sqrt(gravityDirection.x * gravityDirection.x + gravityDirection.y * gravityDirection.y + gravityDirection.z * gravityDirection.z);
        Vector3 normalizedGravityDir = new Vector3(
            gravityDirection.x / gravityDirLength,
            gravityDirection.y / gravityDirLength,
            gravityDirection.z / gravityDirLength
        );
        
        float forceMagnitude = gravityStrength * shipRigidbody.mass;
        Vector3 gravityForce = new Vector3(
            normalizedGravityDir.x * forceMagnitude,
            normalizedGravityDir.y * forceMagnitude,
            normalizedGravityDir.z * forceMagnitude
        );

        Vector3 centerOfMassWorld = shipRigidbody.worldCenterOfMass;

        shipRigidbody.AddForceAtPosition(gravityForce, centerOfMassWorld, ForceMode.Force);

        if (showDebugInfo == true)
        {
            if (Time.frameCount % 60 == 0)
            {
                Vector3 centerOfMassLocal = shipRigidbody.centerOfMass;
                Vector3 geometricCenter = transform.position;
                Vector3 offset = new Vector3(
                    centerOfMassWorld.x - geometricCenter.x,
                    centerOfMassWorld.y - geometricCenter.y,
                    centerOfMassWorld.z - geometricCenter.z
                );

                float offsetLength = Mathf.Sqrt(offset.x * offset.x + offset.y * offset.y + offset.z * offset.z);
                if (offsetLength > 0.01f)
                {
                    Vector3 leverArm = new Vector3(
                        centerOfMassWorld.x - geometricCenter.x,
                        centerOfMassWorld.y - geometricCenter.y,
                        centerOfMassWorld.z - geometricCenter.z
                    );
                    Vector3 torque = Vector3.Cross(leverArm, gravityForce);

                    float gravityForceLength = Mathf.Sqrt(gravityForce.x * gravityForce.x + gravityForce.y * gravityForce.y + gravityForce.z * gravityForce.z);
                    float torqueLength = Mathf.Sqrt(torque.x * torque.x + torque.y * torque.y + torque.z * torque.z);
                    Debug.Log($"Gravity: Force={gravityForceLength:F1}N, " +
                             $"CenterOfMass offset={offsetLength:F2}m, " +
                             $"Torque={torqueLength:F1}Nm");
                }
            }
        }
    }





    private void ApplyWind()
    {
        if (shipRigidbody == null)
        {
            if (showDebugInfo == true)
            {
                if (Time.frameCount % 300 == 0)
                {
                    Debug.LogWarning("ApplyWind: shipRigidbody is null!");
                }
            }
            return;
        }

        float clampedWindStrength = windStrength;
        if (windStrength < 0f)
        {
            clampedWindStrength = 0f;
        }
        if (windStrength > maxWindStrength)
        {
            clampedWindStrength = maxWindStrength;
        }

        if (clampedWindStrength < 0.1f)
        {
            return;
        }

        // Горизонтальная составляющая (X, Z)
        float horizontalX;
        float horizontalZ;
        float horizontalForceStrength;
        
        if (useSquareCompass)
        {
            // Квадратный компас: используем X и Z напрямую
            horizontalX = Mathf.Clamp(windHorizontalX, -1f, 1f);
            horizontalZ = Mathf.Clamp(windHorizontalZ, -1f, 1f);
            // Сила = длина вектора (для квадрата может быть до sqrt(2), ограничиваем до 1)
            float vectorLength = Mathf.Sqrt(horizontalX * horizontalX + horizontalZ * horizontalZ);
            horizontalForceStrength = clampedWindStrength * Mathf.Clamp01(vectorLength);
        }
        else
        {
            // Круглый компас: используем угол и силу
            float degToRad = 0.0174532925f;
            float horizontalRad = windDirectionHorizontalAngle * degToRad;
            horizontalX = Mathf.Sin(horizontalRad);
            horizontalZ = Mathf.Cos(horizontalRad);
            horizontalForceStrength = clampedWindStrength * windHorizontalStrength;
        }

        // Вертикальная составляющая (Y) - контролируется слайдером
        float verticalForceStrength = clampedWindStrength * windVerticalStrength; // Сила вертикального ветра

        Vector3 windForce = new Vector3(
            horizontalX * horizontalForceStrength,
            verticalForceStrength,
            horizontalZ * horizontalForceStrength
        );

        Vector3 centerOfMassWorld = shipRigidbody.worldCenterOfMass;
        
        if (enableWindRotation)
        {
            // Нормализуем вектор силы для расчета центра давления
            Vector3 windDirectionNormalized = windForce.magnitude > 0.01f ? windForce.normalized : Vector3.zero;
            Vector3 windDirectionLocal = transform.InverseTransformDirection(windDirectionNormalized);
            Vector3 centerOfPressure = CalculateCenterOfPressure(windDirectionLocal);
            shipRigidbody.AddForceAtPosition(windForce, centerOfPressure, ForceMode.Force);
            
            if (showDebugInfo && Time.frameCount % 60 == 0)
            {
                Vector3 leverArm = centerOfPressure - centerOfMassWorld;
                float torqueMagnitude = Vector3.Cross(leverArm, windForce).magnitude;
                Debug.Log($"Wind: Strength={clampedWindStrength:F1}N, " +
                         $"Center of Pressure={centerOfPressure}, " +
                         $"Lever Arm={leverArm.magnitude:F2}m, " +
                         $"Torque={torqueMagnitude:F2}N*m");
            }
        }
        else
        {
            shipRigidbody.AddForceAtPosition(windForce, centerOfMassWorld, ForceMode.Force);
        }
        
        if (showDebugInfo == true)
        {
            if (Time.frameCount % 60 == 0 && !enableWindRotation)
            {
                float windForceLength = Mathf.Sqrt(windForce.x * windForce.x + windForce.y * windForce.y + windForce.z * windForce.z);
                Debug.Log($"Wind (Global): Strength={clampedWindStrength:F1}N, " +
                         $"Horizontal Angle={windDirectionHorizontalAngle:F1}°, Horizontal Strength={windHorizontalStrength:F2}, " +
                         $"Vertical Strength={windVerticalStrength:F2}, " +
                         $"Force={windForceLength:F1}N");
            }
        }
    }
    
    private void ApplyTurbulence()
    {
        if (shipRigidbody == null) return;
        
        if (turbulenceManager == null)
        {
            turbulenceManager = FindObjectOfType<TurbulenceManager>();
            if (turbulenceManager == null)
            {
                return;
            }
        }
        
        Vector3 shipPosition = transform.position;
        float deltaTime = Time.fixedDeltaTime;
        
        // Получаем силу и момент турбулентности
        Vector3 turbulenceForce = turbulenceManager.GetTurbulenceForce(shipPosition, deltaTime);
        Vector3 turbulenceTorque = turbulenceManager.GetTurbulenceTorque(shipPosition, deltaTime);
        
        // Применяем силу турбулентности к центру масс (уменьшенная для большего акцента на вращении)
        if (turbulenceForce.magnitude > 0.01f)
        {
            Vector3 centerOfMass = shipRigidbody.worldCenterOfMass;
            Vector3 force = turbulenceForce * turbulenceForceMultiplier;
            shipRigidbody.AddForceAtPosition(force, centerOfMass, ForceMode.Force);
            
            if (showDebugInfo && Time.frameCount % 60 == 0)
            {
                Debug.Log($"Turbulence Force: {force.magnitude:F2}N, Direction={force.normalized}");
            }
        }
        
        // Применяем момент турбулентности (вращение) - основной эффект турбулентности
        if (turbulenceTorque.magnitude > 0.01f)
        {
            // Преобразуем момент в локальные координаты корабля для реалистичного вращения
            Vector3 torqueLocal = transform.InverseTransformDirection(turbulenceTorque);
            Vector3 torque = transform.TransformDirection(torqueLocal) * turbulenceTorqueMultiplier;
            
            shipRigidbody.AddTorque(torque, ForceMode.Force);
            
            if (showDebugInfo && Time.frameCount % 60 == 0)
            {
                Vector3 eulerTorque = torqueLocal;
                Debug.Log($"Turbulence Torque: {torque.magnitude:F2}N*m, " +
                         $"Roll (X)={eulerTorque.x:F2}, Pitch (Y)={eulerTorque.y:F2}, Yaw (Z)={eulerTorque.z:F2}");
            }
        }
    }

    private Vector3 CalculateCenterOfPressure(Vector3 windDirectionLocal)
    {
        if (shipRigidbody == null)
        {
            return transform.position;
        }
        
        Vector3 centerOfMassLocal = shipRigidbody.centerOfMass;
        
        if (autoCalculateShipSize)
        {
            Bounds shipBounds = CalculateShipBounds();
            Vector3 localSize = transform.InverseTransformVector(shipBounds.size);
            shipSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        }
        
        float forwardDot = Vector3.Dot(windDirectionLocal, Vector3.forward);
        float rightDot = Vector3.Dot(windDirectionLocal, Vector3.right);
        float upDot = Vector3.Dot(windDirectionLocal, Vector3.up);
        
        float absForwardDot = Mathf.Abs(forwardDot);
        float absRightDot = Mathf.Abs(rightDot);
        float absUpDot = Mathf.Abs(upDot);
        
        float forwardInfluence;
        float rightInfluence;
        float upInfluence;
        
        if (useRealisticSurfaceArea)
        {
            float frontBackArea = shipSize.x * shipSize.y;
            float leftRightArea = shipSize.z * shipSize.y;
            float topBottomArea = shipSize.x * shipSize.z;
            
            float totalArea = frontBackArea + leftRightArea + topBottomArea;
            if (totalArea < 0.001f) totalArea = 1f;
            
            float frontBackExposedArea = absForwardDot * frontBackArea;
            float leftRightExposedArea = absRightDot * leftRightArea;
            float topBottomExposedArea = absUpDot * topBottomArea;
            
            float totalExposedArea = frontBackExposedArea + leftRightExposedArea + topBottomExposedArea;
            if (totalExposedArea < 0.001f) totalExposedArea = 1f;
            
            forwardInfluence = Mathf.Pow(frontBackExposedArea / totalExposedArea, windInfluencePower);
            rightInfluence = Mathf.Pow(leftRightExposedArea / totalExposedArea, windInfluencePower);
            upInfluence = Mathf.Pow(topBottomExposedArea / totalExposedArea, windInfluencePower);
        }
        else
        {
            forwardInfluence = Mathf.Pow(absForwardDot, windInfluencePower);
            rightInfluence = Mathf.Pow(absRightDot, windInfluencePower);
            upInfluence = Mathf.Pow(absUpDot, windInfluencePower);
        }
        
        float totalInfluence = forwardInfluence + rightInfluence + upInfluence;
        if (totalInfluence < 0.001f)
        {
            totalInfluence = 1f;
        }
        
        forwardInfluence = forwardInfluence / totalInfluence;
        rightInfluence = rightInfluence / totalInfluence;
        upInfluence = upInfluence / totalInfluence;
        
        Vector3 centerOfPressureLocal = centerOfMassLocal;
        
        float maxDimension = Mathf.Max(shipSize.x, Mathf.Max(shipSize.y, shipSize.z));
        
        // Определяем, какой тип ветра доминирует (перед расчетом прямоугольного усиления)
        bool isSideWind = rightInfluence > forwardInfluence && rightInfluence > upInfluence;
        bool isFrontBackWind = forwardInfluence > rightInfluence && forwardInfluence > upInfluence;
        
        // Для прямоугольного корпуса учитываем соотношение сторон
        // Длинный корпус (Z > X) - больше влияние бокового ветра
        // Широкий корпус (X > Z) - больше влияние фронтального ветра
        float aspectRatioZ = shipSize.z / Mathf.Max(shipSize.x, 0.001f); // Длина/ширина
        float aspectRatioX = shipSize.x / Mathf.Max(shipSize.z, 0.001f); // Ширина/длина
        
        // Усиление для прямоугольного корпуса на основе соотношения сторон
        float rectangularBoost = 1f;
        if (isSideWind && aspectRatioZ > 1.2f)
        {
            // Длинный корпус - боковой ветер создает большее вращение
            rectangularBoost = Mathf.Min(aspectRatioZ * 0.3f + 1f, 2f);
        }
        else if (isFrontBackWind && aspectRatioX > 1.2f)
        {
            // Широкий корпус - фронтальный ветер создает большее вращение
            rectangularBoost = Mathf.Min(aspectRatioX * 0.2f + 1f, 1.8f);
        }
        
        // Усиление вращения для прямоугольного корпуса
        // Для прямоугольника боковой ветер создает большее вращение из-за большей площади поверхности
        float forwardMultiplier = frontBackRotationMultiplier;
        float rightMultiplier = sideWindRotationMultiplier; // Боковой ветер - наибольшее влияние
        float upMultiplier = 1.0f; // Вертикальный ветер - базовое влияние
        
        // Применяем множители в зависимости от типа ветра
        if (isSideWind)
        {
            // Боковой ветер для прямоугольника - усиливаем смещение
            rightMultiplier *= rectangularShapeFactor;
        }
        else if (isFrontBackWind)
        {
            // Фронтальный/задний ветер - умеренное усиление
            forwardMultiplier *= rectangularShapeFactor * 0.8f;
        }
        
        float forwardOffset = Mathf.Sign(forwardDot) * shipSize.z * 0.5f * (1f + windLeverArmMultiplier) * forwardInfluence * surfaceAreaMultiplier * forwardMultiplier * rectangularBoost;
        float rightOffset = Mathf.Sign(rightDot) * shipSize.x * 0.5f * (1f + windLeverArmMultiplier) * rightInfluence * surfaceAreaMultiplier * rightMultiplier * rectangularBoost;
        float upOffset = Mathf.Sign(upDot) * shipSize.y * 0.5f * (1f + windLeverArmMultiplier) * upInfluence * surfaceAreaMultiplier * upMultiplier;
        
        // Для прямоугольного корпуса боковой ветер создает вращение вокруг продольной оси (roll)
        // и вращение вокруг вертикальной оси (yaw)
        if (forwardInfluence > 0.5f)
        {
            centerOfPressureLocal.z = centerOfMassLocal.z + forwardOffset;
        }
        else
        {
            centerOfPressureLocal.z = centerOfMassLocal.z + forwardOffset * 0.5f;
        }
        
        // Боковой ветер - основное влияние для прямоугольника
        if (rightInfluence > 0.5f)
        {
            // Для бокового ветра смещаем центр давления дальше от центра масс
            centerOfPressureLocal.x = centerOfMassLocal.x + rightOffset;
        }
        else
        {
            centerOfPressureLocal.x = centerOfMassLocal.x + rightOffset * 0.5f;
        }
        
        if (upInfluence > 0.5f)
        {
            centerOfPressureLocal.y = centerOfMassLocal.y + upOffset;
        }
        else
        {
            centerOfPressureLocal.y = centerOfMassLocal.y + upOffset * 0.5f;
        }
        
        Vector3 centerOfPressureWorld = transform.TransformPoint(centerOfPressureLocal);
        
        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"Center of Pressure: Local={centerOfPressureLocal}, " +
                     $"Forward Influence={forwardInfluence:F2}, Right={rightInfluence:F2}, Up={upInfluence:F2}, " +
                     $"Ship Size={shipSize}, Aspect Ratio Z={aspectRatioZ:F2}, X={aspectRatioX:F2}, " +
                     $"Rectangular Boost={rectangularBoost:F2}, Lever Arm Mult={windLeverArmMultiplier}");
        }
        
        return centerOfPressureWorld;
    }

    private Bounds CalculateShipBounds()
    {
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        bool boundsInitialized = false;
        
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            
            string objName = renderer.gameObject.name.ToLower();
            if (objName.Contains("tank") || objName.Contains("liquid") || objName.Contains("leak"))
            {
                continue;
            }
            
            if (!boundsInitialized)
            {
                bounds = renderer.bounds;
                boundsInitialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        
        if (!boundsInitialized)
        {
            Collider shipCollider = GetComponent<Collider>();
            if (shipCollider != null)
            {
                bounds = shipCollider.bounds;
            }
            else
            {
                bounds = new Bounds(transform.position, shipSize);
            }
        }
        
        return bounds;
    }




    private void ApplyStabilization()
    {
        if (shipRigidbody == null)
        {
            return;
        }

        Vector3 angularVelocity = shipRigidbody.angularVelocity;

        float angularVelLength = Mathf.Sqrt(angularVelocity.x * angularVelocity.x + angularVelocity.y * angularVelocity.y + angularVelocity.z * angularVelocity.z);
        if (angularVelLength < 0.01f)
        {
            return;
        }

        float totalThrust = 0f;
        for (int i = 0; i < engineThrusts.Length; i++)
        {
            totalThrust = totalThrust + engineThrusts[i];
        }

        float stabilizationMultiplier = 1f;
        if (totalThrust > 0.01f)
        {
            stabilizationMultiplier = 1f;
        }
        else
        {
            stabilizationMultiplier = 0.3f;
        }

        float dampingFactor = 1f + angularVelLength * 2f;
        Vector3 stabilizationTorque = new Vector3(
            -angularVelocity.x * stabilizationStrength * dampingFactor * stabilizationMultiplier,
            -angularVelocity.y * stabilizationStrength * dampingFactor * stabilizationMultiplier,
            -angularVelocity.z * stabilizationStrength * dampingFactor * stabilizationMultiplier
        );
        shipRigidbody.AddTorque(stabilizationTorque, ForceMode.Force);

        if (showDebugInfo == true)
        {
            if (Time.frameCount % 60 == 0)
            {
                if (angularVelLength > 0.01f)
                {
                    float stabilizationTorqueLength = Mathf.Sqrt(stabilizationTorque.x * stabilizationTorque.x + stabilizationTorque.y * stabilizationTorque.y + stabilizationTorque.z * stabilizationTorque.z);
                    Debug.Log($"Stabilization: AngularVel={angularVelLength:F4}, TotalThrust={totalThrust:F2}, Multiplier={stabilizationMultiplier:F2}, Torque={stabilizationTorqueLength:F2}Nm");
                }
            }
        }
    }




    public float GetSpeed()
    {
        if (shipRigidbody == null)
        {
            return 0f;
        }
        Vector3 velocity = shipRigidbody.linearVelocity;
        float speed = Mathf.Sqrt(velocity.x * velocity.x + velocity.y * velocity.y + velocity.z * velocity.z);
        return speed;
    }




    public float GetCurrentThrust()
    {
        return currentThrust;
    }




    public float GetEngineThrust(int engineIndex)
    {
        if (engineIndex >= 0 && engineIndex < engineThrusts.Length)
        {
            return engineThrusts[engineIndex];
        }
        return 0f;
    }




    public int GetEngineCount()
    {
        return engines.Count;
    }





    private void UpdateCenterOfMass()
    {
        if (shipRigidbody == null)
        {
            return;
        }

        if (centerOfMassOffsetX > maxCenterOfMassOffset)
        {
            centerOfMassOffsetX = maxCenterOfMassOffset;
        }
        if (centerOfMassOffsetX < -maxCenterOfMassOffset)
        {
            centerOfMassOffsetX = -maxCenterOfMassOffset;
        }

        shipRigidbody.centerOfMass = new Vector3(centerOfMassOffsetX, 0f, 0f);
    }





    public void SetCenterOfMassOffset(float offset)
    {
        if (offset > maxCenterOfMassOffset)
        {
            centerOfMassOffsetX = maxCenterOfMassOffset;
        }
        else if (offset < -maxCenterOfMassOffset)
        {
            centerOfMassOffsetX = -maxCenterOfMassOffset;
        }
        else
        {
            centerOfMassOffsetX = offset;
        }
        UpdateCenterOfMass();
    }




    public float GetCenterOfMassOffset()
    {
        return centerOfMassOffsetX;
    }




    public float GetMaxCenterOfMassOffset()
    {
        return maxCenterOfMassOffset;
    }






    public void SetWindStrength(float strength)
    {
        if (strength < 0f)
        {
            windStrength = 0f;
        }
        else if (strength > maxWindStrength)
        {
            windStrength = maxWindStrength;
        }
        else
        {
            windStrength = strength;
        }

        if (windStrength > 0.1f)
        {
            if (useWind == false)
            {
                useWind = true;
                if (showDebugInfo)
                {
                    Debug.Log($"Wind автоматически включен (сила = {windStrength:F1}N)");
                }
            }
        }
        else
        {
            if (useWind == true)
            {
                useWind = false;
                if (showDebugInfo)
                {
                    Debug.Log("Wind автоматически выключен (сила = 0)");
                }
            }
        }
        
        UpdateCloudWind();
    }




    public float GetWindStrength()
    {
        return windStrength;
    }




    public float GetMaxWindStrength()
    {
        return maxWindStrength;
    }





    public void SetWindDirectionHorizontal(float angle)
    {
        windDirectionHorizontalAngle = angle % 360f;
        if (windDirectionHorizontalAngle < 0f)
        {
            windDirectionHorizontalAngle = windDirectionHorizontalAngle + 360f;
        }
        
        UpdateCloudWind();
    }




    public float GetWindDirectionHorizontal()
    {
        return windDirectionHorizontalAngle;
    }





    public void SetWindDirectionVertical(float angle)
    {
        // Устаревший метод - теперь используем SetWindVerticalStrength
        // Оставляем для обратной совместимости, но не используем угол
        windDirectionVerticalAngle = Mathf.Clamp(angle, -90f, 90f);
        
        UpdateCloudWind();
    }
    
    public void SetWindVerticalStrength(float strength)
    {
        windVerticalStrength = Mathf.Clamp(strength, -1f, 1f);
        UpdateCloudWind();
    }
    
    public float GetWindVerticalStrength()
    {
        return windVerticalStrength;
    }
    
    public void SetWindHorizontalStrength(float strength)
    {
        windHorizontalStrength = Mathf.Clamp01(strength);
        UpdateCloudWind();
    }
    
    public float GetWindHorizontalStrength()
    {
        return windHorizontalStrength;
    }
    
    public void SetWindHorizontalXZ(float x, float z)
    {
        windHorizontalX = Mathf.Clamp(x, -1f, 1f);
        windHorizontalZ = Mathf.Clamp(z, -1f, 1f);
        
        // Обновляем угол и силу для обратной совместимости
        float length = Mathf.Sqrt(x * x + z * z);
        if (length > 0.01f)
        {
            windDirectionHorizontalAngle = Mathf.Atan2(x, z) * Mathf.Rad2Deg;
            windDirectionHorizontalAngle = (windDirectionHorizontalAngle + 360f) % 360f;
            windHorizontalStrength = Mathf.Clamp01(length);
        }
        else
        {
            windHorizontalStrength = 0f;
        }
        
        UpdateCloudWind();
    }
    
    public void GetWindHorizontalXZ(out float x, out float z)
    {
        x = windHorizontalX;
        z = windHorizontalZ;
    }




    public float GetWindDirectionVertical()
    {
        return windDirectionVerticalAngle;
    }






    public void SetWindDirection(float horizontalAngle, float verticalAngle)
    {
        SetWindDirectionHorizontal(horizontalAngle);
        SetWindDirectionVertical(verticalAngle);
        UpdateCloudWind();
    }






    public void GetWindDirection(out float horizontalAngle, out float verticalAngle)
    {
        horizontalAngle = windDirectionHorizontalAngle;
        verticalAngle = windDirectionVerticalAngle;
    }




    public void SetWindEnabled(bool enabled)
    {
        useWind = enabled;
    }




    public bool IsWindEnabled()
    {
        return useWind;
    }
    
    /// <summary>
    /// Получает суммарную тягу всех двигателей (0-1)
    /// </summary>
    public float GetTotalEngineThrust()
    {
        if (engines.Count == 0)
        {
            return 0f;
        }
        
        float totalThrust = 0f;
        foreach (float thrust in engineThrusts)
        {
            totalThrust += thrust;
        }
        
        // Возвращаем среднюю тягу всех двигателей
        return totalThrust / engines.Count;
    }
    
    /// <summary>
    /// Получает массив тяги всех двигателей
    /// </summary>
    public float[] GetEngineThrusts()
    {
        return (float[])engineThrusts.Clone();
    }
    
    /// <summary>
    /// Проверяет, есть ли топливо
    /// </summary>
    private bool HasFuel()
    {
        if (fuelManager == null)
        {
            return true; // Если FuelManager не найден, разрешаем работу двигателей
        }
        
        float totalFuel = fuelManager.GetTotalFuel();
        return totalFuel > 0.1f; // Небольшой запас для предотвращения дрожания
    }
    
    /// <summary>
    /// Проверяет топливо и отключает двигатели, если топливо закончилось
    /// </summary>
    private void CheckFuelAndDisableEnginesIfEmpty()
    {
        if (!HasFuel() && currentThrust > 0.01f)
        {
            // Топливо закончилось, но двигатели еще работают - отключаем их
            currentThrust = 0f;
            for (int i = 0; i < engines.Count; i++)
            {
                if (i < engineThrusts.Length)
                {
                    engineThrusts[i] = 0f;
                }
                if (engines[i] != null)
                {
                    engines[i].SetThrust(0f);
                }
            }
            
            if (showDebugInfo && Time.frameCount % 60 == 0)
            {
                Debug.LogWarning("ShipController: Топливо закончилось! Двигатели отключены.");
            }
        }
    }

    private void UpdateCloudWind()
    {
        if (cloudManager == null)
        {
            cloudManager = FindObjectOfType<VolumetricCloudManager>();
            if (cloudManager == null)
            {
                return;
            }
        }

        // Вычисляем направление ветра
        float horizontalX, horizontalZ;
        if (useSquareCompass)
        {
            horizontalX = windHorizontalX;
            horizontalZ = windHorizontalZ;
        }
        else
        {
            float degToRad = Mathf.Deg2Rad;
            float horizontalRad = windDirectionHorizontalAngle * degToRad;
            horizontalX = Mathf.Sin(horizontalRad) * windHorizontalStrength;
            horizontalZ = Mathf.Cos(horizontalRad) * windHorizontalStrength;
        }
        
        // Вертикальная составляющая (Y)
        float verticalY = windVerticalStrength;
        
        // Формируем вектор направления ветра
        Vector3 windDirection = new Vector3(horizontalX, verticalY, horizontalZ);
        
        // Нормализуем только если вектор не нулевой
        if (windDirection.magnitude > 0.01f)
        {
            windDirection = windDirection.normalized;
        }

        float cloudWindSpeed = Mathf.Max(0f, windStrength / maxWindStrength * 5f);

        cloudManager.SetGlobalWind(windDirection, cloudWindSpeed);
    }

    private void OnDrawGizmos()
    {

        if (showCenterOfMassGizmo && shipRigidbody != null)
        {

            Vector3 centerOfMassWorld = transform.TransformPoint(shipRigidbody.centerOfMass);


            Gizmos.color = centerOfMassGizmoColor;


            Vector3 boxSize = new Vector3(0.5f, 0.5f, 0.5f);
            Gizmos.DrawWireCube(centerOfMassWorld, boxSize);


            Gizmos.color = centerOfMassGizmoColor * 0.5f;
            Gizmos.DrawLine(transform.position, centerOfMassWorld);


            Gizmos.color = centerOfMassGizmoColor;
            Gizmos.DrawSphere(centerOfMassWorld, 0.2f);
        }


        if (showWindGizmo && useWind && windStrength > 0.1f)
        {
            float horizontalX, horizontalZ;
            if (useSquareCompass)
            {
                horizontalX = windHorizontalX;
                horizontalZ = windHorizontalZ;
            }
            else
            {
                float degToRad = Mathf.Deg2Rad;
                float horizontalRad = windDirectionHorizontalAngle * degToRad;
                horizontalX = Mathf.Sin(horizontalRad) * windHorizontalStrength;
                horizontalZ = Mathf.Cos(horizontalRad) * windHorizontalStrength;
            }
            
            float verticalY = windVerticalStrength;

            Vector3 windDirectionWorld = new Vector3(horizontalX, verticalY, horizontalZ);
            if (windDirectionWorld.magnitude > 0.01f)
            {
                windDirectionWorld = windDirectionWorld.normalized;
            }
            
            // Преобразуем в локальные координаты для расчета центра давления
            Vector3 windDirectionLocal = transform.InverseTransformDirection(windDirectionWorld);


            Vector3 startPos = transform.position;


            float arrowLength = (windStrength / maxWindStrength) * 5f;
            Vector3 endPos = startPos + windDirectionWorld * arrowLength;


            Gizmos.color = windGizmoColor;
            Gizmos.DrawLine(startPos, endPos);
            
            if (enableWindRotation && shipRigidbody != null && Application.isPlaying)
            {
                Vector3 centerOfPressure = CalculateCenterOfPressure(windDirectionLocal);
                Vector3 centerOfMassWorld = shipRigidbody.worldCenterOfMass;
                
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(centerOfPressure, 0.3f);
                
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(centerOfMassWorld, centerOfPressure);
                
                Gizmos.color = windGizmoColor;
                Vector3 windForceDirection = windDirectionWorld * arrowLength;
                Gizmos.DrawLine(centerOfPressure, centerOfPressure + windForceDirection);
            }


            Vector3 arrowHeadSize = windDirectionWorld * 0.5f;


            Vector3 perpendicular;
            if (Mathf.Abs(Vector3.Dot(windDirectionWorld, Vector3.up)) > 0.9f)
            {

                perpendicular = Vector3.Cross(windDirectionWorld, transform.forward).normalized * 0.3f;
            }
            else
            {
                perpendicular = Vector3.Cross(windDirectionWorld, Vector3.up).normalized * 0.3f;
            }

            Gizmos.DrawLine(endPos, endPos - arrowHeadSize + perpendicular);
            Gizmos.DrawLine(endPos, endPos - arrowHeadSize - perpendicular);


            Gizmos.DrawSphere(startPos, 0.15f);
        }
    }





    private void OnValidate()
    {


        if (shipRigidbody != null)
        {
            UpdateCenterOfMass();
        }
    }





    public void SetMass(float newMass)
    {
        mass = newMass;
        if (shipRigidbody != null)
        {
            shipRigidbody.mass = mass;
        }
    }




    public EnvironmentMode GetEnvironmentMode()
    {
        return currentEnvironment;
    }




    public void SetEnvironmentMode(EnvironmentMode mode)
    {
        currentEnvironment = mode;
        UpdateEnvironmentSettings();
    }




    public float GetHeight()
    {
        return transform.position.y;
    }




    public void SetGravityEnabled(bool enabled)
    {
        useGravity = enabled;
    }




    public void SetGravityStrength(float strength)
    {
        gravityStrength = strength;
    }




    private void CalculateAndLogTWR()
    {
        if (shipRigidbody == null || engines.Count == 0) return;


        float totalMaxThrust = maxThrustForce * engines.Count;


        float weight = mass * gravityStrength;


        float twr = totalMaxThrust / weight;


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




    public float GetCurrentTWR()
    {
        if (shipRigidbody == null || engines.Count == 0) return 0f;


        float totalCurrentThrust = 0f;
        for (int i = 0; i < engines.Count; i++)
        {
            totalCurrentThrust += maxThrustForce * engineThrusts[i];
        }


        float weight = mass * gravityStrength;

        if (weight < 0.01f) return 0f;

        return totalCurrentThrust / weight;
    }
    
    /// <summary>
    /// Получает максимальный TWR (когда все двигатели на 100% тяги)
    /// </summary>
    public float GetMaxTWR()
    {
        if (shipRigidbody == null || engines.Count == 0) return 0f;
        
        float totalMaxThrust = maxThrustForce * engines.Count;
        float weight = mass * gravityStrength;
        
        if (weight < 0.01f) return 0f;
        
        return totalMaxThrust / weight;
    }
    
    // ========== АВТОПИЛОТ: Публичные методы для управления ==========
    
    /// <summary>
    /// Включает/выключает режим автопилота (отключает ручное управление)
    /// </summary>
    public void SetAutopilotActive(bool active)
    {
        autopilotActive = active;
        if (active)
        {
            // Выбираем все двигатели для автопилота
            SelectAllEngines();
            if (showDebugInfo)
            {
                Debug.Log("ShipController: Автопилот активирован. Ручное управление отключено.");
            }
        }
        else
        {
            if (showDebugInfo)
            {
                Debug.Log("ShipController: Автопилот деактивирован. Ручное управление включено.");
            }
        }
    }
    
    /// <summary>
    /// Проверяет, активен ли автопилот
    /// </summary>
    public bool IsAutopilotActive()
    {
        return autopilotActive;
    }
    
    /// <summary>
    /// Устанавливает общую тягу для всех выбранных двигателей (0-1)
    /// Используется автопилотом для управления кораблем
    /// </summary>
    public void SetThrust(float thrust)
    {
        if (!autopilotActive)
        {
            Debug.LogWarning("ShipController: SetThrust вызван, но автопилот не активен! Используйте SetAutopilotActive(true) сначала.");
            return;
        }
        
        currentThrust = Mathf.Clamp01(thrust);
        UpdateEngineVisuals();
    }
    
    /// <summary>
    /// Устанавливает тягу конкретного двигателя (0-1)
    /// Используется автопилотом для точного управления
    /// </summary>
    public void SetEngineThrust(int engineIndex, float thrust)
    {
        if (!autopilotActive)
        {
            Debug.LogWarning("ShipController: SetEngineThrust вызван, но автопилот не активен!");
            return;
        }
        
        if (engineIndex >= 0 && engineIndex < engineThrusts.Length)
        {
            engineThrusts[engineIndex] = Mathf.Clamp01(thrust);
            if (engineIndex < engines.Count && engines[engineIndex] != null)
            {
                engines[engineIndex].SetThrust(engineThrusts[engineIndex]);
            }
        }
    }
    
    /// <summary>
    /// Устанавливает направление движения (поворот двигателей)
    /// direction.x = -1..1 (влево/вправо)
    /// direction.y = -1..1 (вперед/назад)
    /// Используется автопилотом для управления направлением полета
    /// </summary>
    public void SetMovementDirection(Vector2 direction)
    {
        if (!autopilotActive)
        {
            Debug.LogWarning("ShipController: SetMovementDirection вызван, но автопилот не активен!");
            return;
        }
        
        desiredMovementDirection.x = Mathf.Clamp(direction.x, -maxDirectionOffset, maxDirectionOffset);
        desiredMovementDirection.y = Mathf.Clamp(direction.y, -maxDirectionOffset, maxDirectionOffset);
        
        // Обновляем поворот двигателей
        UpdateEngineRotationsFromMovementDirection();
    }
    
    /// <summary>
    /// Получает текущее направление движения
    /// </summary>
    public Vector2 GetMovementDirection()
    {
        return desiredMovementDirection;
    }
    
    /// <summary>
    /// Устанавливает поворот конкретного двигателя
    /// rotation.x = угол наклона вперед/назад (в градусах, вокруг оси X)
    /// rotation.y = угол поворота влево/вправо (в градусах, вокруг оси Y)
    /// Используется автопилотом для точного управления отдельными двигателями
    /// </summary>
    public void SetEngineRotation(int engineIndex, Vector2 rotation)
    {
        if (!autopilotActive)
        {
            Debug.LogWarning("ShipController: SetEngineRotation вызван, но автопилот не активен!");
            return;
        }
        
        if (engineIndex >= 0 && engineIndex < engineRotations.Length && engineIndex < initialEngineRotations.Length)
        {
            // Сохраняем углы относительно начальной ориентации
            engineRotations[engineIndex].x = initialEngineRotations[engineIndex].x + rotation.x;
            engineRotations[engineIndex].y = initialEngineRotations[engineIndex].y + rotation.y;
            
            // Применяем поворот немедленно
            ApplyEngineRotation(engineIndex);
        }
    }
    
    /// <summary>
    /// Получает текущую скорость корабля
    /// </summary>
    public Vector3 GetVelocity()
    {
        if (shipRigidbody == null) return Vector3.zero;
        return shipRigidbody.linearVelocity;
    }
    
    /// <summary>
    /// Получает текущую угловую скорость корабля
    /// </summary>
    public Vector3 GetAngularVelocity()
    {
        if (shipRigidbody == null) return Vector3.zero;
        return shipRigidbody.angularVelocity;
    }
    
    public Vector3 GetWorldCenterOfMass()
    {
        if (shipRigidbody == null) return transform.position;
        return shipRigidbody.worldCenterOfMass;
    }

    public Transform GetEngineTransform(int engineIndex)
    {
        if (engineIndex < 0 || engineIndex >= engines.Count) return null;
        return engines[engineIndex] != null ? engines[engineIndex].transform : null;
    }

    /// <summary>
    /// Применяет момент (torque) от автопилота напрямую к Rigidbody
    /// </summary>
    public void ApplyAutopilotTorque(Vector3 torqueWorld)
    {
        if (!autopilotActive) return;
        if (shipRigidbody == null) return;
        shipRigidbody.AddTorque(torqueWorld, ForceMode.Force);
    }
    
    /// <summary>
    /// Получает массу корабля
    /// </summary>
    public float GetMass()
    {
        return mass;
    }
    
    /// <summary>
    /// Получает силу гравитации
    /// </summary>
    public float GetGravityStrength()
    {
        return gravityStrength;
    }
    
    /// <summary>
    /// Получает максимальную тягу одного двигателя
    /// </summary>
    public float GetMaxThrustForce()
    {
        return maxThrustForce;
    }
}

