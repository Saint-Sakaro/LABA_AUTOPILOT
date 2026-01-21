using UnityEngine;

public class LiquidPhysics : MonoBehaviour
{
    private float currentVolume;
    private float currentMass;
    private Vector3 liquidCenterOfMass;
    private Rigidbody shipRigidbody;
    
    private void Start()
    {
        shipRigidbody = GetComponent<Rigidbody>();
        if (shipRigidbody == null)
        {
            Debug.LogError("Rigidbody не найден на корабле");
        }
    }
    
    public void UpdateLiquidProperties(float volume, float mass, Vector3 centerOfMass)
    {
        currentVolume = volume;
        currentMass = mass;
        liquidCenterOfMass = centerOfMass;
        
        UpdateShipCenterOfMass();
    }
    
    private void UpdateShipCenterOfMass()
    {
        if (shipRigidbody == null) return;
        
        LiquidTank[] allTanks = GetComponentsInChildren<LiquidTank>();
        
        float totalMass = shipRigidbody.mass; 
        Vector3 shipCenterOfMass = transform.position;
        
        float weightedX = shipRigidbody.mass * shipCenterOfMass.x;
        float weightedY = shipRigidbody.mass * shipCenterOfMass.y;
        float weightedZ = shipRigidbody.mass * shipCenterOfMass.z;
        
        foreach (LiquidTank tank in allTanks)
        {
            if (tank.gameObject == gameObject) continue; 
            
            float tankMass = tank.GetCurrentMass();
            Vector3 tankCenterOfMass = tank.GetCenterOfMass();
            
            totalMass += tankMass;
            weightedX += tankMass * tankCenterOfMass.x;
            weightedY += tankMass * tankCenterOfMass.y;
            weightedZ += tankMass * tankCenterOfMass.z;
        }
        
        Vector3 newCenterOfMass = new Vector3(
            weightedX / totalMass,
            weightedY / totalMass,
            weightedZ / totalMass
        );
        
        Vector3 localCenterOfMass = transform.position - newCenterOfMass;
        shipRigidbody.centerOfMass = localCenterOfMass;
        
        Debug.Log($"Новый центр массы корабля: {newCenterOfMass} | Общая масса: {totalMass:F2} кг");
    }
    

    public float GetLiquidMass()
    {
        return currentMass;
    }
    
    public Vector3 GetLiquidCenterOfMass()
    {
        return liquidCenterOfMass;
    }
}