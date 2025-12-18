using UnityEngine;

public class OrbitCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target; // Трансформ космического корабля
    
    [Header("Orbit Settings")]
    public float orbitDistance = 30f; // Расстояние от корабля до камеры
    public float minDistance = 10f;   // Минимальное расстояние при зуме
    public float maxDistance = 100f;  // Максимальное расстояние при зуме
    
    [Header("Rotation Settings")]
    public float rotationSpeed = 3f;  // Скорость вращения камеры
    public float verticalSpeed = 2f;  // Скорость вертикального вращения
    public float minVerticalAngle = -45f;  // Минимальный угол вверх/вниз
    public float maxVerticalAngle = 60f;   // Максимальный угол вверх/вниз
    
    [Header("Zoom Settings")]
    public float zoomSpeed = 5f;      // Скорость зума (колесо мыши)
    
    [Header("Smoothing")]
    public float rotationDamping = 0.1f; // Сглаживание вращения
    
    [Header("Visibility Settings")]
    public GameObject shipBody;           // Корпус корабля
    public GameObject enginesParent;      // Родительский объект двигателей
    public float hideDistance = 35f;      // Расстояние, при котором скрывать корпус и двигатели
    
    private float horizontalAngle = 180f;  // Горизонтальный угол (вокруг Y)
    private float verticalAngle = 30f;     // Вертикальный угол (вокруг X)
    private Vector3 targetPosition;
    private Vector3 currentVelocity = Vector3.zero;
    private bool isHidden = false;         // Состояние видимости корпуса
    
    void Start()
    {
        if (target == null)
        {
            Debug.LogError("OrbitCamera: Target не назначена! Перетащите корабль в переменную Target.");
            return;
        }
        
        // Рассчитываем начальные углы из текущей позиции
        Vector3 directionToCamera = transform.position - target.position;
        horizontalAngle = Mathf.Atan2(directionToCamera.x, directionToCamera.z) * Mathf.Rad2Deg;
        verticalAngle = Mathf.Asin(directionToCamera.y / directionToCamera.magnitude) * Mathf.Rad2Deg;
    }
    
    void LateUpdate()
    {
        if (target == null)
            return;
        
        // Получаем ввод мыши
        HandleMouseInput();
        
        // Вычисляем новую позицию камеры
        UpdateCameraPosition();
        
        // Проверяем видимость корпуса и двигателей
        UpdateVisibility();
        
        // Ориентируем камеру на корабль
        transform.LookAt(target.position);
    }
    
    void HandleMouseInput()
    {
        // Вращение при зажатой левой кнопке мыши
        if (Input.GetMouseButton(0))
        {
            horizontalAngle += Input.GetAxis("Mouse X") * rotationSpeed;
            verticalAngle -= Input.GetAxis("Mouse Y") * verticalSpeed;
            
            // Ограничиваем вертикальный угол
            verticalAngle = Mathf.Clamp(verticalAngle, minVerticalAngle, maxVerticalAngle);
        }
        
        // Зум колесом мыши
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        orbitDistance -= scrollInput * zoomSpeed;
        orbitDistance = Mathf.Clamp(orbitDistance, minDistance, maxDistance);
    }
    
    void UpdateCameraPosition()
    {
        // Преобразуем углы в радианы
        float horizontalRad = horizontalAngle * Mathf.Deg2Rad;
        float verticalRad = verticalAngle * Mathf.Deg2Rad;
        
        // Вычисляем позицию камеры
        Vector3 newPosition = target.position + new Vector3(
            Mathf.Sin(horizontalRad) * orbitDistance * Mathf.Cos(verticalRad),
            Mathf.Sin(verticalRad) * orbitDistance,
            Mathf.Cos(horizontalRad) * orbitDistance * Mathf.Cos(verticalRad)
        );
        
        // Применяем сглаживание (опционально)
        transform.position = Vector3.SmoothDamp(transform.position, newPosition, ref currentVelocity, rotationDamping);
    }
    
    void UpdateVisibility()
    {
        // Вычисляем расстояние от камеры до корабля
        float distance = Vector3.Distance(transform.position, target.position);
        
        // Если расстояние <= 35, скрываем корпус и двигатели
        if (distance <= hideDistance && !isHidden)
        {
            if (shipBody != null)
                shipBody.SetActive(false);
            if (enginesParent != null)
                enginesParent.SetActive(false);
            isHidden = true;
        }
        // Если расстояние > 35, показываем корпус и двигатели
        else if (distance > hideDistance && isHidden)
        {
            if (shipBody != null)
                shipBody.SetActive(true);
            if (enginesParent != null)
                enginesParent.SetActive(true);
            isHidden = false;
        }
    }
    
    // Публичный метод для сброса камеры
    public void ResetCamera()
    {
        horizontalAngle = 180f;
        verticalAngle = 30f;
        orbitDistance = 30f;
    }
}
