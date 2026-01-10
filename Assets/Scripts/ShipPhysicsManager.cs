using UnityEngine;

public class ShipPhysicsManager : MonoBehaviour
{
    [SerializeField] private Rigidbody shipRigidbody;
    [SerializeField] private LiquidTank[] fuelTanks;
    [SerializeField] private bool isLanding = false;
    
    [Header("Параметры посадки")]
    [SerializeField] private float landingSpeed = 5f;
    [SerializeField] private float maxTiltAngle = 45f;
    [SerializeField] private float tiltRecoverySpeed = 2f;
    
    private bool isEnginesRunning = false;
    
    private float initialShipMass;
    private Vector3 initialCenterOfMass;
    
    private void Start()
    {
        if (shipRigidbody == null)
            shipRigidbody = GetComponent<Rigidbody>();
        
        initialShipMass = shipRigidbody.mass;
        initialCenterOfMass = shipRigidbody.centerOfMass;
        
        fuelTanks = GetComponentsInChildren<LiquidTank>();
        
        shipRigidbody.isKinematic = true;
        foreach (LiquidTank tank in fuelTanks)
        {
            tank.SetConsuming(true); 
        }
        
        Debug.Log("Корабль включен. Нажми 'Начать посадку' чтобы отключить мотры и упасть.");
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            StartLanding();
        }
        
        if (isLanding)
        {
            UpdateLanding();
        }
    }

    public void StartLanding()
    {
        if (isLanding) return; 
        
        isLanding = true;
        isEnginesRunning = false;
        
        shipRigidbody.isKinematic = false;
        shipRigidbody.useGravity = true; 
        shipRigidbody.linearVelocity = Vector3.zero; 
        
        foreach (LiquidTank tank in fuelTanks)
        {
            tank.SetConsuming(false);
        }
        
        Debug.Log("Посадка начата! Мотры отключены, корабль падает под гравитацией.");
    }
    
    private void UpdateLanding()
    {
        RaycastHit hit;
        float rayDistance = 2f;
        
        if (Physics.Raycast(transform.position, Vector3.down, out hit, rayDistance))
        {
            if (hit.collider.CompareTag("Ground"))
            {
                CompleteLanding();
            }
        }
        
        float currentTilt = Vector3.Angle(transform.up, Vector3.up);
        if (currentTilt > maxTiltAngle)
        {
            Debug.LogWarning($"ВНИМАНИЕ! Наклон корабля {currentTilt:F1}°! Корабль нестабилен!");
        }
    }
    
    private void CompleteLanding()
    {
        isLanding = false;
        shipRigidbody.linearVelocity = Vector3.zero;
        shipRigidbody.isKinematic = true; 
        
        foreach (LiquidTank tank in fuelTanks)
        {
            tank.SetConsuming(true);
        }
        
        Debug.Log("Посадка завершена!");
    }

    public void CreateLeakInRandomTank(float leakRate = 50f)
    {
        if (fuelTanks.Length == 0) return;
        
        int randomIndex = Random.Range(0, fuelTanks.Length);
        fuelTanks[randomIndex].CreateLeak(leakRate);
        
        Debug.Log($"Пробоина в баке {randomIndex}! Утечка {leakRate} л/сек");
    }
    

    public float GetTotalFuelMass()
    {
        float totalMass = 0;
        foreach (LiquidTank tank in fuelTanks)
        {
            totalMass += tank.GetCurrentMass();
        }
        return totalMass;
    }
}
