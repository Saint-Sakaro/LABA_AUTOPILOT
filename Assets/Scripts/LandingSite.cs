using UnityEngine;

/// <summary>
/// Структура данных для хранения информации о посадочной площадке
/// </summary>
[System.Serializable]
public class LandingSite
{
    /// <summary>
    /// Центр площадки в мировых координатах
    /// </summary>
    public Vector3 position;
    
    /// <summary>
    /// Оценка пригодности площадки (0-1, где 1 - идеальная)
    /// </summary>
    public float suitabilityScore;
    
    /// <summary>
    /// Размер площадки (радиус в метрах)
    /// </summary>
    public float size;
    
    /// <summary>
    /// Наклон площадки в градусах
    /// </summary>
    public float slopeAngle;
    
    /// <summary>
    /// Ровность площадки (стандартное отклонение высот, чем меньше - тем ровнее)
    /// </summary>
    public float flatness;
    
    /// <summary>
    /// Расстояние до ближайшего препятствия
    /// </summary>
    public float distanceToObstacle;
    
    /// <summary>
    /// Расстояние от корабля до площадки
    /// </summary>
    public float distanceFromShip;
    
    /// <summary>
    /// Есть ли препятствия в зоне посадки
    /// </summary>
    public bool hasObstacles;
    
    /// <summary>
    /// Нормаль поверхности (для ориентации площадки)
    /// </summary>
    public Vector3 surfaceNormal;
    
    public LandingSite(Vector3 pos, float score, float siteSize, float slope, float flat, float obstacleDist, float shipDist, bool obstacles, Vector3 normal = default)
    {
        position = pos;
        suitabilityScore = score;
        size = siteSize;
        slopeAngle = slope;
        flatness = flat;
        distanceToObstacle = obstacleDist;
        distanceFromShip = shipDist;
        hasObstacles = obstacles;
        surfaceNormal = normal == default ? Vector3.up : normal;
    }
    
    /// <summary>
    /// Получить текстовое описание площадки
    /// </summary>
    public string GetDescription()
    {
        return $"Площадка: Расстояние={distanceFromShip:F0}м, " +
               $"Размер={size:F0}м, " +
               $"Наклон={slopeAngle:F1}°, " +
               $"Пригодность={suitabilityScore * 100f:F0}%";
    }
}
