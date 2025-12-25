using UnityEngine;
using System.Collections.Generic;

public class VolumetricCloudManager : MonoBehaviour
{
    [SerializeField] private GameObject cloudPrefab;
    [SerializeField] private Transform cloudsContainer;
    
    [Header("Generation Settings")]
    [SerializeField] private int numberOfClouds = 50;
    [SerializeField] private float territoryRadius = 500f;
    [SerializeField] private Vector3 territoryCenter = Vector3.zero;
    
    [Header("Height Settings")]
    [SerializeField] private float minHeight = 50f;
    [SerializeField] private float maxHeight = 200f;
    
    [Header("Wind")]
    [SerializeField] private Vector3 windDirection = Vector3.right;
    [SerializeField] private float windSpeed = 0.5f;
    
    private List<VolumetricCloud> allClouds = new List<VolumetricCloud>();
    private bool cloudsGenerated = false;
    
    private void Start()
    {
        if (cloudsContainer == null)
            cloudsContainer = transform;
        

        allClouds.AddRange(GetComponentsInChildren<VolumetricCloud>());
        

        if (allClouds.Count == 0)
        {
            GenerateClouds();
            cloudsGenerated = true;
        }
    }
    



    private void GenerateClouds()
    {
        for (int i = 0; i < numberOfClouds; i++)
        {

            Vector3 randomPosition = GetRandomPositionInTerritory();
            

            VolumetricCloud cloud = CreateCloud(randomPosition);
            
            if (cloud != null)
            {

                float randomWindSpeed = Random.Range(windSpeed * 0.5f, windSpeed * 1.5f);
                cloud.SetWind(GetRandomWindDirection(), randomWindSpeed);
            }
        }
        
        Debug.Log($"✅ Сгенерировано {allClouds.Count} облаков");
    }
    



    private Vector3 GetRandomPositionInTerritory()
    {

        float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float randomDistance = Random.Range(0f, territoryRadius);
        
        float x = territoryCenter.x + Mathf.Cos(randomAngle) * randomDistance;
        float z = territoryCenter.z + Mathf.Sin(randomAngle) * randomDistance;
        

        float y = territoryCenter.y + Random.Range(minHeight, maxHeight);
        
        return new Vector3(x, y, z);
    }
    



    private Vector3 GetRandomWindDirection()
    {
        float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(randomAngle), 0, Mathf.Sin(randomAngle)).normalized;
    }
    



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
    



    public void SetGlobalWind(Vector3 direction, float speed)
    {
        windDirection = direction.normalized;
        windSpeed = speed;
        

        foreach (VolumetricCloud cloud in allClouds)
        {
            cloud.SetWind(windDirection, windSpeed);
        }
    }
    



    public List<VolumetricCloud> GetAllClouds() => allClouds;
    



    public int GetCloudCount() => allClouds.Count;
    



    public void RegenerateClouds()
    {

        foreach (VolumetricCloud cloud in allClouds)
        {
            Destroy(cloud.gameObject);
        }
        allClouds.Clear();
        

        GenerateClouds();
    }
    



    private void OnDrawGizmos()
    {

        Gizmos.color = new Color(0, 1, 1, 0.3f);
        DrawCircle(territoryCenter, territoryRadius, 64);
        

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(territoryCenter, 5f);
        

        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Vector3 minHeightPos = territoryCenter + Vector3.up * minHeight;
        Vector3 maxHeightPos = territoryCenter + Vector3.up * maxHeight;
        DrawCircle(minHeightPos, territoryRadius, 64);
        DrawCircle(maxHeightPos, territoryRadius, 64);
    }
    



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
