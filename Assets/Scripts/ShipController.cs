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
    [SerializeField] private float maxWindStrength = 1f;
    [SerializeField] private bool showWindGizmo = true;
    [SerializeField] private Color windGizmoColor = Color.cyan;

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

    [Header("References")]
    [SerializeField] private ShipThrusterManager thrusterManager;


    private List<EngineFireController> engines = new List<EngineFireController>();


    private HashSet<int> selectedEngines = new HashSet<int>();


    private float[] engineThrusts = new float[4];


    private Vector2[] engineRotations = new Vector2[4];


    private Vector2[] initialEngineRotations = new Vector2[4];


    private Quaternion[] initialEngineRotationsQuat = new Quaternion[4];


    private Vector3[] initialEnginePositions = new Vector3[4];





    private Vector2 desiredMovementDirection = Vector2.zero;


    private float currentThrust = 0f;


    private enum MovementDirection { None, ForwardBackward, LeftRight }
    private MovementDirection currentMovementDirection = MovementDirection.None;

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


        if (shipRigidbody != null)
        {
            shipRigidbody.mass = mass;


            shipRigidbody.isKinematic = false;
            shipRigidbody.useGravity = false;


            Collider shipCollider = GetComponent<Collider>();
            if (shipCollider == null)
            {

                shipCollider = GetComponentInChildren<Collider>();
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


        shipColliders = shipColliders.Distinct()
            .Where(c => c != null && 
                   !legNames.Contains(c.gameObject.name.ToLower()) &&
                   !legNames.Any(legName => c.gameObject.name.ToLower().Contains(legName)))
            .ToList();

        Debug.Log($"ShipController: Найдено {shipColliders.Count} коллайдеров корабля (исключая ноги)");

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


        if (autoDetectEnvironment)
        {
            DetectEnvironmentMode();
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


        ApplyThrustFromEngines();


        if (autoStabilize)
        {
            ApplyStabilization();
        }
    }




    private void HandleEngineSelection()
    {

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
        if (engineIndex < 0 || engineIndex >= engines.Count)
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


        if (!hasAnyThrust)
        {
            averageThrust = 0f;
            currentThrust = 0f;
        }
        else
        {
            currentThrust = averageThrust;
        }


        currentMovementDirection = MovementDirection.None;


        UpdateEngineVisuals();
    }




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






    private void HandleMovementDirectionInput()
    {

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












        float targetAngleX = -desiredMovementDirection.y * maxTiltAngle;



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
            Debug.Log($"Направление движения: ({desiredMovementDirection.x:F2}, {desiredMovementDirection.y:F2}), " +
                     $"Углы поворота: X={targetAngleX:F1}°, Y={targetAngleY:F1}°");
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
        float targetThrust = currentThrust;


        if (Input.GetKey(KeyCode.W))
        {
            targetThrust = Mathf.Clamp01(currentThrust + thrustChangeSpeed * Time.deltaTime);
        }

        else if (Input.GetKey(KeyCode.S))
        {
            targetThrust = Mathf.Clamp01(currentThrust - thrustChangeSpeed * Time.deltaTime);
        }


        currentThrust = Mathf.Lerp(currentThrust, targetThrust, Time.deltaTime * thrustChangeSpeed);


        UpdateEngineVisuals();
    }





    private void UpdateEngineVisuals()
    {

        for (int i = 0; i < engines.Count; i++)
        {
            if (engines[i] == null) continue;

            if (selectedEngines.Contains(i))
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
            for (int i = 0; i < engines.Count && i < 4; i++)
            {
                thrusterManager.SetEngineThrust(i, engineThrusts[i]);
            }
        }
    }





    private void ApplyThrustFromEngines()
    {
        if (shipRigidbody == null || engines.Count == 0) return;


        Vector3 totalForce = Vector3.zero;
        Vector3 totalTorque = Vector3.zero;
        Vector3 centerOfMass = shipRigidbody.worldCenterOfMass;


        bool enginesTilted = desiredMovementDirection.magnitude > 0.01f;


        for (int i = 0; i < engines.Count; i++)
        {
            EngineFireController engine = engines[i];
            if (engine == null) continue;


            float engineThrust = engineThrusts[i];


            if (engineThrust < 0.01f) continue;




            Vector3 engineDirection = -engine.transform.forward;


            Vector3 enginePosition = engine.transform.position;


            float thrustForce = maxThrustForce * engineThrust;


            Vector3 force = engineDirection * thrustForce;



            if (applyForceToCenter || enginesTilted)
            {

                totalForce += force;
            }
            else
            {

                shipRigidbody.AddForceAtPosition(force, enginePosition, ForceMode.Force);


                Vector3 leverArm = enginePosition - centerOfMass;
                Vector3 torque = Vector3.Cross(leverArm, force);
                totalTorque += torque;


                if (showDebugInfo)
                {
                    Debug.Log($"Engine {i}: Thrust={engineThrust:F2}, Force={thrustForce:F1}N, " +
                              $"LeverArm={leverArm.magnitude:F2}m, Torque={torque.magnitude:F1}Nm");
                }
            }
        }


        if ((applyForceToCenter || enginesTilted) && totalForce.magnitude > 0.01f)
        {
            shipRigidbody.AddForce(totalForce, ForceMode.Force);


            if (showDebugInfo && Time.frameCount % 60 == 0)
            {
                Vector3 totalForceLocal = transform.InverseTransformDirection(totalForce);
                Debug.Log($"Total Force Applied: {totalForce.magnitude:F1}N, Local: ({totalForceLocal.x:F2}, {totalForceLocal.y:F2}, {totalForceLocal.z:F2})");
            }
        }


        if (showDebugInfo && totalTorque.magnitude > 0.1f)
        {
            Debug.Log($"Total Torque: {totalTorque.magnitude:F1}Nm, Direction: {totalTorque.normalized}");
        }
    }




    private void DetectEnvironmentMode()
    {
        if (shipRigidbody == null) return;

        float currentHeight = transform.position.y;
        EnvironmentMode newMode = currentHeight > atmosphereHeight ? EnvironmentMode.Space : EnvironmentMode.Atmosphere;


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
        if (shipRigidbody == null) return;


        Vector3 gravityForce = gravityDirection.normalized * gravityStrength * shipRigidbody.mass;


        Vector3 centerOfMassWorld = shipRigidbody.worldCenterOfMass;



        shipRigidbody.AddForceAtPosition(gravityForce, centerOfMassWorld, ForceMode.Force);


        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Vector3 centerOfMassLocal = shipRigidbody.centerOfMass;
            Vector3 geometricCenter = transform.position;
            Vector3 offset = centerOfMassWorld - geometricCenter;

            if (offset.magnitude > 0.01f)
            {

                Vector3 leverArm = centerOfMassWorld - geometricCenter;
                Vector3 torque = Vector3.Cross(leverArm, gravityForce);

                Debug.Log($"Gravity: Force={gravityForce.magnitude:F1}N, " +
                         $"CenterOfMass offset={offset.magnitude:F2}m, " +
                         $"Torque={torque.magnitude:F1}Nm");
            }
        }
    }





    private void ApplyWind()
    {
        if (shipRigidbody == null)
        {
            if (showDebugInfo && Time.frameCount % 300 == 0)
            {
                Debug.LogWarning("ApplyWind: shipRigidbody is null!");
            }
            return;
        }


        float clampedWindStrength = Mathf.Clamp(windStrength, 0f, maxWindStrength);


        if (clampedWindStrength < 0.1f) return;




        float horizontalRad = windDirectionHorizontalAngle * Mathf.Deg2Rad;
        float verticalRad = windDirectionVerticalAngle * Mathf.Deg2Rad;



        float horizontalX = Mathf.Sin(horizontalRad);
        float horizontalZ = Mathf.Cos(horizontalRad);


        float horizontalLength = Mathf.Cos(verticalRad);
        float verticalY = Mathf.Sin(verticalRad);

        Vector3 windDirectionLocal = new Vector3(
            horizontalX * horizontalLength,
            verticalY,
            horizontalZ * horizontalLength
        ).normalized;


        Vector3 windDirectionWorld = transform.TransformDirection(windDirectionLocal);


        Vector3 windForce = windDirectionWorld * clampedWindStrength;



        Vector3 centerOfMassWorld = shipRigidbody.worldCenterOfMass;
        shipRigidbody.AddForceAtPosition(windForce, centerOfMassWorld, ForceMode.Force);


        if (showDebugInfo && Time.frameCount % 60 == 0)
        {
            Debug.Log($"Wind: Strength={clampedWindStrength:F1}N, " +
                     $"Horizontal={windDirectionHorizontalAngle:F1}°, " +
                     $"Vertical={windDirectionVerticalAngle:F1}°, " +
                     $"Force={windForce.magnitude:F1}N, " +
                     $"Direction Local={windDirectionLocal}, " +
                     $"Direction World={windDirectionWorld}");
        }
    }




    private void ApplyStabilization()
    {
        if (shipRigidbody == null) return;


        Vector3 angularVelocity = shipRigidbody.angularVelocity;



        if (angularVelocity.magnitude < 0.01f) return;



        float totalThrust = 0f;
        for (int i = 0; i < engineThrusts.Length; i++)
        {
            totalThrust += engineThrusts[i];
        }


        float stabilizationMultiplier = totalThrust > 0.01f ? 1f : 0.3f;



        float dampingFactor = 1f + angularVelocity.magnitude * 2f;
        Vector3 stabilizationTorque = -angularVelocity * stabilizationStrength * dampingFactor * stabilizationMultiplier;
        shipRigidbody.AddTorque(stabilizationTorque, ForceMode.Force);


        if (showDebugInfo && Time.frameCount % 60 == 0 && angularVelocity.magnitude > 0.01f)
        {
            Debug.Log($"Stabilization: AngularVel={angularVelocity.magnitude:F4}, TotalThrust={totalThrust:F2}, Multiplier={stabilizationMultiplier:F2}, Torque={stabilizationTorque.magnitude:F2}Nm");
        }
    }




    public float GetSpeed()
    {
        if (shipRigidbody == null) return 0f;
        return shipRigidbody.linearVelocity.magnitude;
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
        if (shipRigidbody == null) return;


        centerOfMassOffsetX = Mathf.Clamp(centerOfMassOffsetX, -maxCenterOfMassOffset, maxCenterOfMassOffset);


        shipRigidbody.centerOfMass = new Vector3(centerOfMassOffsetX, 0f, 0f);
    }





    public void SetCenterOfMassOffset(float offset)
    {
        centerOfMassOffsetX = Mathf.Clamp(offset, -maxCenterOfMassOffset, maxCenterOfMassOffset);
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
        windStrength = Mathf.Clamp(strength, 0f, maxWindStrength);


        if (windStrength > 0.1f && !useWind)
        {
            useWind = true;
            if (showDebugInfo)
            {
                Debug.Log($"Wind автоматически включен (сила = {windStrength:F1}N)");
            }
        }

        else if (windStrength < 0.1f && useWind)
        {
            useWind = false;
            if (showDebugInfo)
            {
                Debug.Log("Wind автоматически выключен (сила = 0)");
            }
        }
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
        if (windDirectionHorizontalAngle < 0f) windDirectionHorizontalAngle += 360f;
    }




    public float GetWindDirectionHorizontal()
    {
        return windDirectionHorizontalAngle;
    }





    public void SetWindDirectionVertical(float angle)
    {

        windDirectionVerticalAngle = Mathf.Clamp(angle, -90f, 90f);
    }




    public float GetWindDirectionVertical()
    {
        return windDirectionVerticalAngle;
    }






    public void SetWindDirection(float horizontalAngle, float verticalAngle)
    {
        SetWindDirectionHorizontal(horizontalAngle);
        SetWindDirectionVertical(verticalAngle);
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

            float horizontalRad = windDirectionHorizontalAngle * Mathf.Deg2Rad;
            float verticalRad = windDirectionVerticalAngle * Mathf.Deg2Rad;

            float horizontalX = Mathf.Sin(horizontalRad);
            float horizontalZ = Mathf.Cos(horizontalRad);
            float horizontalLength = Mathf.Cos(verticalRad);
            float verticalY = Mathf.Sin(verticalRad);

            Vector3 windDirectionLocal = new Vector3(
                horizontalX * horizontalLength,
                verticalY,
                horizontalZ * horizontalLength
            ).normalized;


            Vector3 windDirectionWorld = transform.TransformDirection(windDirectionLocal);


            Vector3 startPos = transform.position;


            float arrowLength = (windStrength / maxWindStrength) * 5f;
            Vector3 endPos = startPos + windDirectionWorld * arrowLength;


            Gizmos.color = windGizmoColor;
            Gizmos.DrawLine(startPos, endPos);


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
}

