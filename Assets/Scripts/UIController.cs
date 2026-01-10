using UnityEngine;
using TMPro;

public class UIController : MonoBehaviour
{
    [SerializeField] private ShipPhysicsManager shipManager;
    [SerializeField] private LiquidTank[] tanks;
    
    [SerializeField] private TextMeshProUGUI fuelText;
    [SerializeField] private TextMeshProUGUI centerOfMassText;
    [SerializeField] private TextMeshProUGUI statusText;
    
    private void Update()
    {
        UpdateUI();
    }
    
    private void UpdateUI()
    {
        if (shipManager == null) return;
        
        float totalFuel = shipManager.GetTotalFuelMass();
        if (fuelText != null)
        {
            fuelText.text = $"Топливо: {totalFuel:F1} кг";
        }
        
        Rigidbody shipRb = shipManager.GetComponent<Rigidbody>();
        if (shipRb != null && centerOfMassText != null)
        {
            Vector3 com = shipRb.centerOfMass;
            centerOfMassText.text = $"Центр массы: {com.x:F2}, {com.y:F2}, {com.z:F2}";
        }
        
        if (statusText != null && tanks != null)
        {
            int leakingTanks = 0;
            foreach (LiquidTank tank in tanks)
            {
                if (tank != null && tank.HasLeak())
                {
                    leakingTanks++;
                }
            }
            
            if (leakingTanks > 0)
            {
                statusText.text = $"УТЕЧКА! {leakingTanks} баков!";
                statusText.color = new Color(1, 0, 0, 1); 
            }
            else
            {
                statusText.text = "Система в норме";
                statusText.color = new Color(0, 1, 0, 1); 
            }
        }
    }
    
    public void OnLeakButtonPressed()
    {
        if (shipManager != null)
        {
            shipManager.CreateLeakInRandomTank(50f);
        }
    }

    public void OnLandingButtonPressed()
    {
        if (shipManager != null)
        {
            shipManager.StartLanding();
        }
    }
}
