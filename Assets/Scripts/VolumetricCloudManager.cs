using UnityEngine;
using System.Collections.Generic;

public class VolumetricCloudManager : MonoBehaviour
{
    [SerializeField] private GameObject cloudPrefab;
    [SerializeField] private Transform cloudsContainer;
    
    [Header("Generation Settings")]
    [SerializeField] private int numberOfClouds = 50;
    [SerializeField] private float territoryRadius = 500f;  // Радиус территории
    [SerializeField] private Vector3 territoryCenter = Vector3.zero;  // Центр территории
    
    [Header("Height Settings")]
    [SerializeField] private float minHeight = 50f;  // Минимальная высота облаков
    [SerializeField] private float maxHeight = 200f;  // Максимальная высота облаков
    
    [Header("Wind")]
    [SerializeField] private Vector3 windDirection = Vector3.right;
    [SerializeField] private float windSpeed = 0.5f;
    
    private List<VolumetricCloud> allClouds = new List<VolumetricCloud>();
    private bool cloudsGenerated = false;
    
    private void Start()
    {
        if (cloudsContainer == null)
            cloudsContainer = transform;
        
        // Находим уже созданные облака
        allClouds.AddRange(GetComponentsInChildren<VolumetricCloud>());
        
        // Если облаков нет - генерируем новые
        if (allClouds.Count == 0)
        {
            GenerateClouds();
            cloudsGenerated = true;
        }
    }
    
    /// <summary>
    /// Генерирует облака случайно в пределах территории
    /// </summary>
    private void GenerateClouds()
    {
        for (int i = 0; i < numberOfClouds; i++)
        {
            // === РАНДОМНАЯ ПОЗИЦИЯ В ПРЕДЕЛАХ ТЕРРИТОРИИ ===
            Vector3 randomPosition = GetRandomPositionInTerritory();
            
            // Создаём облако
            VolumetricCloud cloud = CreateCloud(randomPosition);
            
            if (cloud != null)
            {
                // Случайная скорость ветра для каждого облака (для более живого эффекта)
                float randomWindSpeed = Random.Range(windSpeed * 0.5f, windSpeed * 1.5f);
                cloud.SetWind(GetRandomWindDirection(), randomWindSpeed);
            }
        }
        
        Debug.Log($"✅ Сгенерировано {allClouds.Count} облаков");
    }
    
    /// <summary>
    /// Получает случайную позицию внутри территории
    /// </summary>
    private Vector3 GetRandomPositionInTerritory()
    {
        // Случайная позиция в круге (X, Z)
        float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float randomDistance = Random.Range(0f, territoryRadius);
        
        float x = territoryCenter.x + Mathf.Cos(randomAngle) * randomDistance;
        float z = territoryCenter.z + Mathf.Sin(randomAngle) * randomDistance;
        
        // Случайная высота (Y)
        float y = territoryCenter.y + Random.Range(minHeight, maxHeight);
        
        return new Vector3(x, y, z);
    }
    
    /// <summary>
    /// Получает случайное направление ветра
    /// </summary>
    private Vector3 GetRandomWindDirection()
    {
        float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(randomAngle), 0, Mathf.Sin(randomAngle)).normalized;
    }
    
    /// <summary>
    /// Создаёт новое облако
    /// </summary>
    public VolumetricCloud CreateCloud(Vector3 position, float density = 1f)
    {
        GameObject cloudObj = Instantiate(cloudPrefab, position, Quaternion.identity, cloudsContainer);
        cloudObj.name = $"Cloud_{allClouds.Count + 1}";
        
        VolumetricCloud cloud = cloudObj.GetComponent<VolumetricCloud>();
        
        if (cloud != null)
        {
            cloud.SetDensity(density);
            allClouds.Add(cloud);
            cloud.SetWind(windDirection, windSpeed);
        }
        
        return cloud;
    }
    
    /// <summary>
    /// Переустановить глобальный ветер
    /// </summary>
    public void SetGlobalWind(Vector3 direction, float speed)
    {
        windDirection = direction.normalized;
        windSpeed = speed;
        
        // Применяем ко всем облакам
        foreach (VolumetricCloud cloud in allClouds)
        {
            cloud.SetWind(windDirection, windSpeed);
        }
    }
    
    /// <summary>
    /// Получить все облака
    /// </summary>
    public List<VolumetricCloud> GetAllClouds() => allClouds;
    
    /// <summary>
    /// Получить количество облаков
    /// </summary>
    public int GetCloudCount() => allClouds.Count;
    
    /// <summary>
    /// Очистить все облака и переустановить
    /// </summary>
    public void RegenerateClouds()
    {
        // Удаляем старые облака
        foreach (VolumetricCloud cloud in allClouds)
        {
            Destroy(cloud.gameObject);
        }
        allClouds.Clear();
        
        // Генерируем новые
        GenerateClouds();
    }
    
    /// <summary>
    /// Визуализация территории в сцене (для отладки)
    /// </summary>
    private void OnDrawGizmos()
    {
        // Рисуем окружность территории
        Gizmos.color = new Color(0, 1, 1, 0.3f);  // Голубой цвет
        DrawCircle(territoryCenter, territoryRadius, 64);
        
        // Центр территории
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(territoryCenter, 5f);
        
        // Высота облаков
        Gizmos.color = new Color(0, 1, 0, 0.3f);  // Зелёный цвет
        Vector3 minHeightPos = territoryCenter + Vector3.up * minHeight;
        Vector3 maxHeightPos = territoryCenter + Vector3.up * maxHeight;
        DrawCircle(minHeightPos, territoryRadius, 64);
        DrawCircle(maxHeightPos, territoryRadius, 64);
    }
    
    /// <summary>
    /// Вспомогательный метод для рисования окружности
    /// </summary>
    private void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angle = 0f;
        float angleStep = 360f / segments;
        Vector3 lastPoint = Vector3.zero;
        
        for (int i = 0; i <= segments; i++)
        {
            float rad = angle * Mathf.Deg2Rad;
            Vector3 point = center + new Vector3(
                Mathf.Cos(rad) * radius,
                0,
                Mathf.Sin(rad) * radius
            );
            
            if (i > 0)
                Gizmos.DrawLine(lastPoint, point);
            
            lastPoint = point;
            angle += angleStep;
        }
    }
}
