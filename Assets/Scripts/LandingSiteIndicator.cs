using UnityEngine;

/// <summary>
/// 3D индикатор посадочной площадки в мире
/// </summary>
public class LandingSiteIndicator : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private GameObject indicatorPrefab; // Префаб индикатора (если null, создается автоматически)
    [SerializeField] private float indicatorHeight = 1f; // Высота индикатора над землей (увеличено для видимости)
    [SerializeField] private bool showDistance = true; // Показывать расстояние до корабля
    
    // Настройки размера (устанавливаются из LandingRadar при инициализации)
    private float minIndicatorSize = 20f; // Минимальный размер индикатора
    private float maxIndicatorSize = 100f; // Максимальный размер индикатора
    private float indicatorSizeMultiplier = 1.2f; // Множитель размера индикатора
    
    [Header("Colors")]
    [SerializeField] private Color excellentColor = new Color(0f, 1f, 0f, 0.7f); // Зеленый (отлично) - увеличена непрозрачность
    [SerializeField] private Color goodColor = new Color(0.3f, 1f, 0.3f, 0.6f); // Светло-зеленый (хорошо)
    [SerializeField] private Color acceptableColor = new Color(1f, 1f, 0f, 0.6f); // Желтый (приемлемо)
    [SerializeField] private Color poorColor = new Color(1f, 0.4f, 0f, 0.6f); // Оранжевый (плохо)
    
    private LandingSite site;
    private GameObject indicatorObject;
    private Transform shipTransform;
    private TextMesh distanceText;
    private bool indicatorCreated = false; // Флаг, чтобы избежать дублирования
    
    public void Initialize(LandingSite landingSite, Transform ship, float minSize = 20f, float maxSize = 100f, float sizeMultiplier = 1.2f)
    {
        // Предотвращаем повторную инициализацию
        if (indicatorCreated && indicatorObject != null)
        {
            return;
        }
        
        site = landingSite;
        shipTransform = ship;
        
        // Сохраняем настройки размера
        minIndicatorSize = minSize;
        maxIndicatorSize = maxSize;
        indicatorSizeMultiplier = sizeMultiplier;
        
        // Устанавливаем позицию индикатора в мировых координатах (привязано к земле)
        // Добавляем высоту к позиции площадки, чтобы индикатор был над землей
        transform.position = landingSite.position + Vector3.up * indicatorHeight;
        // ВАЖНО: Устанавливаем поворот в identity, чтобы индикатор не поворачивался вместе с кораблем
        transform.rotation = Quaternion.identity;
        
        CreateIndicator();
        UpdateVisuals();
    }
    
    private void CreateIndicator()
    {
        // Предотвращаем дублирование индикаторов
        if (indicatorCreated || indicatorObject != null)
        {
            return;
        }
        
        if (indicatorPrefab != null)
        {
            indicatorObject = Instantiate(indicatorPrefab, transform);
            indicatorCreated = true;
        }
        else
        {
            // Создаем простой индикатор из примитивов
            indicatorObject = new GameObject("LandingSiteIndicator");
            indicatorObject.transform.parent = transform;
            indicatorObject.transform.localPosition = Vector3.zero;
            // ВАЖНО: Устанавливаем поворот в identity, чтобы индикатор не поворачивался
            indicatorObject.transform.localRotation = Quaternion.identity;
            
            // Создаем плоский круг через меш (вместо цилиндра)
            GameObject circle = new GameObject("CircleIndicator");
            circle.transform.parent = indicatorObject.transform;
            // Круг находится в локальных координатах относительно indicatorObject
            // Позиция indicatorObject уже установлена выше земли, поэтому круг в нуле
            circle.transform.localPosition = Vector3.zero;
            
            // Поворачиваем круг в соответствии с нормалью поверхности
            // Если нормаль не задана или равна нулю, используем Vector3.up
            Vector3 normal = site.surfaceNormal;
            if (normal == Vector3.zero || normal.magnitude < 0.1f)
            {
                normal = Vector3.up;
            }
            
            // Вычисляем поворот для ориентации круга по нормали поверхности
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal);
            circle.transform.localRotation = rotation;
            
            // Размер индикатора зависит от размера площадки
            float indicatorSize = Mathf.Max(site.size * indicatorSizeMultiplier, minIndicatorSize);
            indicatorSize = Mathf.Min(indicatorSize, maxIndicatorSize);
            
            // Создаем меш для плоского круга
            Mesh circleMesh = CreateCircleMesh(indicatorSize, 32); // 32 сегмента для гладкого круга
            MeshFilter meshFilter = circle.AddComponent<MeshFilter>();
            meshFilter.mesh = circleMesh;
            
            MeshRenderer meshRenderer = circle.AddComponent<MeshRenderer>();
            
            // Создаем материал
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = GetColorForScore(site.suitabilityScore);
            mat.SetFloat("_Surface", 1); // Transparent
            mat.SetFloat("_Blend", 0); // Alpha
            mat.renderQueue = 3000; // Transparent queue
            // ВАЖНО: Отключаем backface culling, чтобы круг был виден с обеих сторон
            mat.SetInt("_Cull", 0); // 0 = Off (двусторонний)
            meshRenderer.material = mat;
            
            indicatorCreated = true; // Отмечаем, что индикатор создан
            
            // Создаем текст с расстоянием
            if (showDistance)
            {
                GameObject textObj = new GameObject("DistanceText");
                textObj.transform.parent = indicatorObject.transform;
                textObj.transform.localPosition = Vector3.up * (indicatorHeight + 2f);
                textObj.transform.localRotation = Quaternion.identity;
                
                distanceText = textObj.AddComponent<TextMesh>();
                distanceText.anchor = TextAnchor.MiddleCenter;
                distanceText.alignment = TextAlignment.Center;
                distanceText.fontSize = 20;
                distanceText.color = Color.white;
            }
        }
        
        // Позиционируем индикатор (добавляем высоту к позиции площадки)
        transform.position = site.position + Vector3.up * indicatorHeight;
        // ВАЖНО: Устанавливаем поворот в identity, чтобы индикатор не поворачивался
        transform.rotation = Quaternion.identity;
    }
    
    private void Update()
    {
        if (site == null || indicatorObject == null) return;
        
        // ВАЖНО: Обновляем позицию индикатора в мировых координатах (привязано к земле)
        // Это гарантирует, что индикатор остается на месте, даже если корабль движется
        // Добавляем высоту к позиции площадки, чтобы индикатор был над землей
        Vector3 targetPosition = site.position + Vector3.up * indicatorHeight;
        if (transform.position != targetPosition)
        {
            transform.position = targetPosition;
        }
        
        // ВАЖНО: Всегда поддерживаем поворот в identity для основного объекта
        // (круг внутри будет повернут по нормали поверхности)
        if (transform.rotation != Quaternion.identity)
        {
            transform.rotation = Quaternion.identity;
        }
        
        // ВАЖНО: Убеждаемся, что indicatorObject тоже не поворачивается
        // (круг внутри будет повернут по нормали поверхности)
        if (indicatorObject != null && indicatorObject.transform.localRotation != Quaternion.identity)
        {
            indicatorObject.transform.localRotation = Quaternion.identity;
        }
        
        // Обновляем поворот круга по нормали поверхности (если она изменилась)
        if (indicatorObject != null && site != null)
        {
            Transform circleTransform = indicatorObject.transform.Find("CircleIndicator");
            if (circleTransform != null)
            {
                Vector3 normal = site.surfaceNormal;
                if (normal == Vector3.zero || normal.magnitude < 0.1f)
                {
                    normal = Vector3.up;
                }
                Quaternion targetRotation = Quaternion.FromToRotation(Vector3.up, normal);
                if (circleTransform.localRotation != targetRotation)
                {
                    circleTransform.localRotation = targetRotation;
                }
            }
        }
        
        // Обновляем визуализацию
        UpdateVisuals();
        
        // Поворачиваем текст к камере
        if (distanceText != null && Camera.main != null)
        {
            distanceText.transform.LookAt(Camera.main.transform);
            distanceText.transform.Rotate(0, 180, 0);
        }
    }
    
    private void UpdateVisuals()
    {
        if (shipTransform != null && distanceText != null)
        {
            float distance = Vector3.Distance(shipTransform.position, site.position);
            distanceText.text = $"{distance:F0}м\n{site.suitabilityScore * 100f:F0}%";
        }
        
        // Обновляем цвет в зависимости от пригодности
        if (indicatorObject != null)
        {
            Renderer[] renderers = indicatorObject.GetComponentsInChildren<Renderer>();
            Color targetColor = GetColorForScore(site.suitabilityScore);
            
            foreach (Renderer renderer in renderers)
            {
                if (renderer.material != null)
                {
                    renderer.material.color = targetColor;
                }
            }
        }
    }
    
    private Color GetColorForScore(float score)
    {
        // Более четкое разделение цветов для лучшей видимости различий
        if (score >= 0.85f) return excellentColor; // Отлично - зеленый
        if (score >= 0.65f) return goodColor; // Хорошо - желто-зеленый
        if (score >= 0.45f) return acceptableColor; // Приемлемо - желтый
        return poorColor; // Плохо - оранжевый
    }
    
    public LandingSite GetSite()
    {
        return site;
    }
    
    /// <summary>
    /// Создает меш для плоского круга на плоскости XZ
    /// </summary>
    private Mesh CreateCircleMesh(float radius, int segments)
    {
        Mesh mesh = new Mesh();
        mesh.name = "CircleMesh";
        
        // Вершины: центр + точки по окружности
        Vector3[] vertices = new Vector3[segments + 1];
        vertices[0] = Vector3.zero; // Центр
        
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * 2f * Mathf.PI;
            vertices[i + 1] = new Vector3(
                Mathf.Cos(angle) * radius,
                0f, // Y = 0, круг лежит на плоскости XZ
                Mathf.Sin(angle) * radius
            );
        }
        
        // Треугольники: от центра к каждой паре соседних точек
        // Создаем треугольники в обе стороны, чтобы круг был виден сверху и снизу
        int[] triangles = new int[segments * 3 * 2]; // Удваиваем для обеих сторон
        for (int i = 0; i < segments; i++)
        {
            int baseIndex = i * 3;
            int nextIndex = (i + 1) % segments + 1;
            
            // Первая сторона (видна сверху)
            triangles[baseIndex] = 0; // Центр
            triangles[baseIndex + 1] = i + 1;
            triangles[baseIndex + 2] = nextIndex;
            
            // Вторая сторона (видна снизу) - обратный порядок вершин
            int reverseBaseIndex = segments * 3 + baseIndex;
            triangles[reverseBaseIndex] = 0; // Центр
            triangles[reverseBaseIndex + 1] = nextIndex;
            triangles[reverseBaseIndex + 2] = i + 1;
        }
        
        // UV координаты
        Vector2[] uvs = new Vector2[vertices.Length];
        uvs[0] = new Vector2(0.5f, 0.5f); // Центр
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * 2f * Mathf.PI;
            uvs[i + 1] = new Vector2(
                0.5f + Mathf.Cos(angle) * 0.5f,
                0.5f + Mathf.Sin(angle) * 0.5f
            );
        }
        
        // Нормали (все направлены вверх по Y)
        Vector3[] normals = new Vector3[vertices.Length];
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = Vector3.up;
        }
        
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.RecalculateBounds();
        
        return mesh;
    }
    
    public void Destroy()
    {
        if (indicatorObject != null)
        {
            Destroy(indicatorObject);
        }
        Destroy(gameObject);
    }
}
