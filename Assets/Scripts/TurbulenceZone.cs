using UnityEngine;

/// <summary>
/// Зона турбулентности - создает случайные силы и моменты для корабля
/// </summary>
public class TurbulenceZone : MonoBehaviour
{
    [Header("Zone Settings")]
    [SerializeField] private Vector3 zoneSize = new Vector3(100f, 50f, 100f);
    [SerializeField] private float turbulenceStrength = 10f;
    [SerializeField] private float turbulenceFrequency = 1f;
    [SerializeField] private bool useNoise = true;
    
    // Публичные методы для настройки зоны программно
    public void SetZoneSize(Vector3 size) { zoneSize = size; }
    public void SetTurbulenceStrength(float strength) { turbulenceStrength = strength; }
    public void SetTurbulenceFrequency(float frequency) { turbulenceFrequency = frequency; }
    public void SetShowInGame(bool show) 
    { 
        bool wasShowing = showInGame;
        showInGame = show; 
        
        // Если зона уже создана, обновляем визуализацию
        if (Application.isPlaying)
        {
            // Всегда вызываем SetupGameVisualization, чтобы создать или удалить визуализацию
            SetupGameVisualization();
            UpdateGameVisualization();
            
            if (showDebugInfo && show && !wasShowing)
            {
                Debug.Log($"TurbulenceZone: Включена визуализация для зоны на позиции {transform.position}, размер={zoneSize}");
            }
        }
    }
    
    [Header("Force Settings")]
    [SerializeField] private float maxForceMagnitude = 100f; // Уменьшено - меньше линейного движения
    [SerializeField] private float maxTorqueMagnitude = 300f; // Увеличено - больше вращения
    [SerializeField] private float forceVariation = 0.3f;
    [SerializeField] private float rotationBias = 0.8f; // Предпочтение вращения над линейным движением (0-1)
    
    [Header("Visualization")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color gizmoColor = new Color(1f, 0.5f, 0f, 0.3f);
    [SerializeField] private bool showInGame = false; // Показывать зону в Game View
    [SerializeField] private GameObject visualCube; // Визуальный куб для Game View
    [SerializeField] private bool showDebugInfo = false; // Отладочная информация для этой зоны
    
    private float noiseOffset = 0f;
    private int noiseSeed = 0;
    private Material visualMaterial;
    
    private void Start()
    {
        noiseOffset = Random.Range(0f, 10000f);
        noiseSeed = Random.Range(0, 10000);
        
        // Визуализация будет создана в UpdateGameVisualization, если showInGame уже установлен
        // или через SetShowInGame, если он вызывается после Start
    }
    
    private void SetupGameVisualization()
    {
        if (!showInGame) 
        {
            // Если визуализация отключена, удаляем куб
            if (visualCube != null)
            {
                Destroy(visualCube);
                visualCube = null;
            }
            return;
        }
        
        // Создаем визуальный куб для Game View, если его нет
        if (visualCube == null)
        {
            visualCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visualCube.name = "TurbulenceZoneVisual";
            visualCube.transform.SetParent(transform);
            visualCube.transform.localPosition = Vector3.zero;
            visualCube.transform.localRotation = Quaternion.identity;
            visualCube.transform.localScale = zoneSize;
            
            // Удаляем коллайдер, чтобы не мешал физике
            Collider col = visualCube.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }
            
            // Создаем материал для визуализации
            MeshRenderer renderer = visualCube.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                visualMaterial = new Material(Shader.Find("Standard"));
                visualMaterial.color = gizmoColor;
                visualMaterial.SetFloat("_Mode", 3); // Transparent mode
                visualMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                visualMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                visualMaterial.SetInt("_ZWrite", 0);
                visualMaterial.DisableKeyword("_ALPHATEST_ON");
                visualMaterial.EnableKeyword("_ALPHABLEND_ON");
                visualMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                visualMaterial.renderQueue = 3000;
                
                renderer.material = visualMaterial;
                
                if (showDebugInfo)
                {
                    Debug.Log($"TurbulenceZone: Создан визуальный куб для зоны на позиции {transform.position}, размер={zoneSize}, цвет={gizmoColor}, showInGame={showInGame}");
                }
            }
            else
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning($"TurbulenceZone: Не удалось получить MeshRenderer для визуального куба на позиции {transform.position}!");
                }
            }
        }
        else
        {
            // Обновляем существующий куб
            visualCube.transform.localScale = zoneSize;
            if (visualMaterial != null)
            {
                visualMaterial.color = gizmoColor;
            }
        }
    }
    
    private void Update()
    {
        UpdateGameVisualization();
    }
    
    private void UpdateGameVisualization()
    {
        if (visualCube != null)
        {
            visualCube.SetActive(showInGame);
            
            if (showInGame)
            {
                // Обновляем размер и материал
                visualCube.transform.localScale = zoneSize;
                
                if (visualMaterial != null)
                {
                    visualMaterial.color = gizmoColor;
                }
            }
        }
        else if (showInGame && Application.isPlaying)
        {
            // Если куб должен быть виден, но его нет - создаем его
            SetupGameVisualization();
        }
    }
    
    private void OnValidate()
    {
        // Обновляем визуализацию при изменении параметров в редакторе
        if (Application.isPlaying && visualCube != null)
        {
            UpdateGameVisualization();
        }
    }
    
    /// <summary>
    /// Проверяет, находится ли позиция внутри зоны
    /// </summary>
    public bool IsPositionInside(Vector3 worldPosition)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPosition);
        return Mathf.Abs(localPos.x) <= zoneSize.x * 0.5f &&
               Mathf.Abs(localPos.y) <= zoneSize.y * 0.5f &&
               Mathf.Abs(localPos.z) <= zoneSize.z * 0.5f;
    }
    
    /// <summary>
    /// Получает силу турбулентности для заданной позиции (уменьшена для большего акцента на вращении)
    /// </summary>
    public Vector3 GetTurbulenceForce(Vector3 worldPosition, float deltaTime)
    {
        if (!IsPositionInside(worldPosition))
        {
            return Vector3.zero;
        }
        
        // Уменьшаем линейную силу для большей реалистичности (в турбулентности самолет больше крутится, чем движется)
        float forceReduction = 1f - rotationBias;
        
        Vector3 localPos = transform.InverseTransformPoint(worldPosition);
        
        if (useNoise)
        {
            // Используем 3D Perlin шум для процедурной турбулентности
            float time = Time.time * turbulenceFrequency + noiseOffset;
            float noiseX = Mathf.PerlinNoise(localPos.x * 0.1f + time, noiseSeed * 0.1f) * 2f - 1f;
            float noiseY = Mathf.PerlinNoise(localPos.y * 0.1f + time, (noiseSeed + 1000) * 0.1f) * 2f - 1f;
            float noiseZ = Mathf.PerlinNoise(localPos.z * 0.1f + time, (noiseSeed + 2000) * 0.1f) * 2f - 1f;
            
            // Нормализуем и применяем силу (уменьшенную)
            Vector3 noiseDirection = new Vector3(noiseX, noiseY, noiseZ).normalized;
            float noiseMagnitude = (Mathf.PerlinNoise(time * 0.5f, noiseSeed * 0.5f) * 2f - 1f) * forceVariation + 1f;
            
            float forceMagnitude = turbulenceStrength * maxForceMagnitude * noiseMagnitude * forceReduction;
            return transform.TransformDirection(noiseDirection) * forceMagnitude;
        }
        else
        {
            // Простая случайная сила (для тестирования) - уменьшенная
            float randomX = (Random.value * 2f - 1f);
            float randomY = (Random.value * 2f - 1f);
            float randomZ = (Random.value * 2f - 1f);
            Vector3 randomDirection = new Vector3(randomX, randomY, randomZ).normalized;
            
            float forceMagnitude = turbulenceStrength * maxForceMagnitude * forceReduction;
            return transform.TransformDirection(randomDirection) * forceMagnitude * deltaTime;
        }
    }
    
    /// <summary>
    /// Получает момент турбулентности (вращение) для заданной позиции - реалистичное вращение (крен, тангаж, рысканье)
    /// </summary>
    public Vector3 GetTurbulenceTorque(Vector3 worldPosition, float deltaTime)
    {
        if (!IsPositionInside(worldPosition))
        {
            return Vector3.zero;
        }
        
        Vector3 localPos = transform.InverseTransformPoint(worldPosition);
        
        if (useNoise)
        {
            // Используем 3D Perlin шум для процедурного вращения
            float time = Time.time * turbulenceFrequency * 0.7f + noiseOffset;
            
            // Разделяем вращение на три оси: крен (roll - X), тангаж (pitch - Y), рысканье (yaw - Z)
            // В реальной турбулентности крен обычно сильнее тангажа и рысканья
            float rollNoise = Mathf.PerlinNoise(localPos.x * 0.15f + time, (noiseSeed + 3000) * 0.1f) * 2f - 1f;
            float pitchNoise = Mathf.PerlinNoise(localPos.y * 0.15f + time, (noiseSeed + 4000) * 0.1f) * 2f - 1f;
            float yawNoise = Mathf.PerlinNoise(localPos.z * 0.15f + time, (noiseSeed + 5000) * 0.1f) * 2f - 1f;
            
            // Крен (roll) - вращение вокруг оси X (вперед-назад) - обычно самый сильный
            float rollMultiplier = 1.2f; // Усиливаем крен
            float pitchMultiplier = 0.9f; // Тангаж - немного слабее
            float yawMultiplier = 0.7f; // Рысканье - слабее всего
            
            Vector3 torqueAxis = new Vector3(
                rollNoise * rollMultiplier,
                pitchNoise * pitchMultiplier,
                yawNoise * yawMultiplier
            ).normalized;
            
            // Интенсивность вращения (с вариацией)
            float noiseMagnitude = (Mathf.PerlinNoise(time * 0.6f, (noiseSeed + 6000) * 0.5f) * 2f - 1f) * forceVariation + 1f;
            float torqueMagnitude = turbulenceStrength * maxTorqueMagnitude * noiseMagnitude * rotationBias;
            
            // Возвращаем момент в локальных координатах (корабль будет вращаться вокруг своих осей)
            return torqueAxis * torqueMagnitude;
        }
        else
        {
            // Простой случайный момент с предпочтением крена
            float rollRandom = (Random.value * 2f - 1f) * 1.2f;
            float pitchRandom = (Random.value * 2f - 1f) * 0.9f;
            float yawRandom = (Random.value * 2f - 1f) * 0.7f;
            Vector3 randomDirection = new Vector3(rollRandom, pitchRandom, yawRandom).normalized;
            
            float torqueMagnitude = turbulenceStrength * maxTorqueMagnitude * rotationBias;
            return randomDirection * torqueMagnitude * deltaTime;
        }
    }
    
    /// <summary>
    /// Получает коэффициент силы турбулентности (0-1) для плавного перехода
    /// </summary>
    public float GetTurbulenceIntensity(Vector3 worldPosition)
    {
        if (!IsPositionInside(worldPosition))
        {
            return 0f;
        }
        
        Vector3 localPos = transform.InverseTransformPoint(worldPosition);
        
        // Вычисляем расстояние от центра зоны (0 в центре, 1 на краю)
        float normalizedX = Mathf.Abs(localPos.x) / (zoneSize.x * 0.5f);
        float normalizedY = Mathf.Abs(localPos.y) / (zoneSize.y * 0.5f);
        float normalizedZ = Mathf.Abs(localPos.z) / (zoneSize.z * 0.5f);
        
        // Используем максимальное расстояние для плавного перехода
        float maxDistance = Mathf.Max(normalizedX, normalizedY, normalizedZ);
        
        // Плавное затухание к краям (квадратичная функция)
        float intensity = 1f - Mathf.Clamp01(maxDistance);
        intensity = intensity * intensity; // Квадратичное затухание для более плавного перехода
        
        return intensity * turbulenceStrength;
    }
    
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        
        // Сохраняем матрицу для правильной трансформации
        Matrix4x4 originalMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        
        // Рисуем полупрозрачный внутренний куб (визуализация области)
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, gizmoColor.a * 0.2f);
        Gizmos.DrawCube(Vector3.zero, zoneSize);
        
        // Рисуем контур куба (wireframe)
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(Vector3.zero, zoneSize);
        
        // Рисуем оси для лучшей ориентации
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f); // Красная ось X
        Gizmos.DrawLine(Vector3.zero, Vector3.right * zoneSize.x * 0.3f);
        Gizmos.color = new Color(0f, 1f, 0f, 0.5f); // Зеленая ось Y
        Gizmos.DrawLine(Vector3.zero, Vector3.up * zoneSize.y * 0.3f);
        Gizmos.color = new Color(0f, 0f, 1f, 0.5f); // Синяя ось Z
        Gizmos.DrawLine(Vector3.zero, Vector3.forward * zoneSize.z * 0.3f);
        
        // Восстанавливаем матрицу
        Gizmos.matrix = originalMatrix;
    }
    
    private void OnDrawGizmosSelected()
    {
        Matrix4x4 originalMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        
        // При выделении - желтый контур
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero, zoneSize);
        
        // Дополнительные линии для выделения (диагонали)
        Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
        Vector3 halfSize = zoneSize * 0.5f;
        // Верхние углы
        Gizmos.DrawLine(new Vector3(-halfSize.x, halfSize.y, -halfSize.z), new Vector3(halfSize.x, halfSize.y, halfSize.z));
        Gizmos.DrawLine(new Vector3(halfSize.x, halfSize.y, -halfSize.z), new Vector3(-halfSize.x, halfSize.y, halfSize.z));
        // Нижние углы
        Gizmos.DrawLine(new Vector3(-halfSize.x, -halfSize.y, -halfSize.z), new Vector3(halfSize.x, -halfSize.y, halfSize.z));
        Gizmos.DrawLine(new Vector3(halfSize.x, -halfSize.y, -halfSize.z), new Vector3(-halfSize.x, -halfSize.y, halfSize.z));
        
        // Информация о силе турбулентности (визуализация интенсивности цветом)
        float intensity = turbulenceStrength;
        Color intensityColor = Color.Lerp(Color.green, Color.red, Mathf.Clamp01(intensity * 0.1f));
        Gizmos.color = new Color(intensityColor.r, intensityColor.g, intensityColor.b, 0.4f);
        Gizmos.DrawCube(Vector3.zero, zoneSize * 0.98f);
        
        Gizmos.matrix = originalMatrix;
    }
    
    private void OnDestroy()
    {
        // Очищаем ресурсы при уничтожении
        if (visualMaterial != null)
        {
            Destroy(visualMaterial);
        }
        
        if (visualCube != null)
        {
            Destroy(visualCube);
        }
    }
}
