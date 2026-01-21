using UnityEngine;




[System.Serializable]
public class LandingSite
{
    
    
    
    public Vector3 position;
    
    
    
    
    public float suitabilityScore;
    
    
    
    
    public float size;
    
    
    
    
    public float slopeAngle;
    
    
    
    
    public float flatness;
    
    
    
    
    public float distanceToObstacle;
    
    
    
    
    public float distanceFromShip;
    
    
    
    
    public bool hasObstacles;
    
    
    
    
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
    
    
    
    
    public string GetDescription()
    {
        return $"Площадка: Расстояние={distanceFromShip:F0}м, " +
               $"Размер={size:F0}м, " +
               $"Наклон={slopeAngle:F1}°, " +
               $"Пригодность={suitabilityScore * 100f:F0}%";
    }
}
