using UnityEngine;
using System.Collections.Generic;
using System.Linq;




public class FuelManager : MonoBehaviour
{
    [Header("Fuel Settings")]
    [SerializeField] private float baseConsumptionRate = 10f; 
    [SerializeField] private float minConsumptionRate = 0.5f; 
    [SerializeField] private float consumptionMultiplier = 1f; 
    
    [Header("Tank Settings")]
    [SerializeField] private bool autoFindTanks = true; 
    [SerializeField] private List<LiquidTank> fuelTanks = new List<LiquidTank>();
    
    [Header("Engine Settings")]
    [SerializeField] private ShipController shipController; 
    
    [Header("UI")]
    [SerializeField] private bool showFuelInfo = true; 
    
    [Header("Low Fuel Warning")]
    [SerializeField] private bool enableLowFuelWarning = true;
    [SerializeField] private float lowFuelThreshold = 0.2f; 
    [SerializeField] private float criticalFuelThreshold = 0.1f; 
    
    
    public delegate void FuelChangedDelegate(float totalFuel, float totalMaxFuel, float fuelPercentage);
    public event FuelChangedDelegate OnFuelChanged;
    
    public delegate void LowFuelWarningDelegate(float fuelPercentage);
    public event LowFuelWarningDelegate OnLowFuelWarning;
    
    public delegate void CriticalFuelWarningDelegate(float fuelPercentage);
    public event CriticalFuelWarningDelegate OnCriticalFuelWarning;
    
    
    private float lastTotalFuel = -1f;
    private bool lowFuelWarningShown = false;
    private bool criticalFuelWarningShown = false;
    
    private void Start()
    {
        if (autoFindTanks)
        {
            FindAllTanks();
        }
        
        if (shipController == null)
        {
            shipController = FindObjectOfType<ShipController>();
        }
        
        
        UpdateFuelStatus();
    }
    
    private void Update()
    {
        if (shipController == null || fuelTanks.Count == 0)
        {
            return;
        }
        
        
        float totalThrust = GetTotalEngineThrust();
        
        
        float consumptionRate = CalculateConsumptionRate(totalThrust);
        
        
        ConsumeFuel(consumptionRate * Time.deltaTime);
        
        
        UpdateFuelStatus();
    }
    
    
    
    
    private void FindAllTanks()
    {
        fuelTanks.Clear();
        
        
        Transform shipTransform = transform;
        if (shipController != null)
        {
            shipTransform = shipController.transform;
        }
        
        
        LiquidTank[] allTanks = shipTransform.GetComponentsInChildren<LiquidTank>();
        foreach (var tank in allTanks)
        {
            if (tank != null && !fuelTanks.Contains(tank))
            {
                fuelTanks.Add(tank);
            }
        }
        
        
        if (shipTransform.parent != null)
        {
            LiquidTank[] parentTanks = shipTransform.parent.GetComponentsInChildren<LiquidTank>();
            foreach (var tank in parentTanks)
            {
                if (tank != null && !fuelTanks.Contains(tank))
                {
                    fuelTanks.Add(tank);
                }
            }
        }
        
        Debug.Log($"FuelManager: найдено {fuelTanks.Count} баков с топливом");
    }
    
    
    
    
    private float GetTotalEngineThrust()
    {
        if (shipController == null)
        {
            return 0f;
        }
        
        
        return shipController.GetTotalEngineThrust();
    }
    
    
    
    
    private float CalculateConsumptionRate(float totalThrust)
    {
        
        float consumptionRate = Mathf.Lerp(minConsumptionRate, baseConsumptionRate, totalThrust);
        return consumptionRate * consumptionMultiplier;
    }
    
    
    
    
    private void ConsumeFuel(float totalConsumption)
    {
        if (fuelTanks.Count == 0 || totalConsumption <= 0f)
        {
            return;
        }
        
        
        float totalVolume = 0f;
        foreach (var tank in fuelTanks)
        {
            if (tank != null)
            {
                totalVolume += tank.GetCurrentVolume();
            }
        }
        
        if (totalVolume <= 0f)
        {
            return; 
        }
        
        
        foreach (var tank in fuelTanks)
        {
            if (tank == null || tank.GetCurrentVolume() <= 0f)
            {
                continue;
            }
            
            float tankVolume = tank.GetCurrentVolume();
            float tankRatio = tankVolume / totalVolume;
            float tankConsumption = totalConsumption * tankRatio;
            
            
            tank.DecreaseVolume(tankConsumption);
        }
    }
    
    
    
    
    private void UpdateFuelStatus()
    {
        float totalFuel = GetTotalFuel();
        float totalMaxFuel = GetTotalMaxFuel();
        float fuelPercentage = totalMaxFuel > 0f ? totalFuel / totalMaxFuel : 0f;
        
        
        if (Mathf.Abs(totalFuel - lastTotalFuel) > 0.1f)
        {
            lastTotalFuel = totalFuel;
            OnFuelChanged?.Invoke(totalFuel, totalMaxFuel, fuelPercentage);
            
            if (showFuelInfo && Time.frameCount % 60 == 0)
            {
                Debug.Log($"FuelManager: топливо: {totalFuel:F1}/{totalMaxFuel:F1} л ({fuelPercentage * 100f:F1}%)");
            }
        }
        
        
        if (enableLowFuelWarning)
        {
            if (fuelPercentage <= criticalFuelThreshold && !criticalFuelWarningShown)
            {
                criticalFuelWarningShown = true;
                OnCriticalFuelWarning?.Invoke(fuelPercentage);
                Debug.LogWarning($"FuelManager: ️ КРИТИЧЕСКИЙ УРОВЕНЬ ТОПЛИВА Осталось {fuelPercentage * 100f:F1}%");
            }
            else if (fuelPercentage > criticalFuelThreshold)
            {
                criticalFuelWarningShown = false;
            }
            
            if (fuelPercentage <= lowFuelThreshold && !lowFuelWarningShown)
            {
                lowFuelWarningShown = true;
                OnLowFuelWarning?.Invoke(fuelPercentage);
                Debug.LogWarning($"FuelManager: ️ Низкий уровень топлива Осталось {fuelPercentage * 100f:F1}%");
            }
            else if (fuelPercentage > lowFuelThreshold)
            {
                lowFuelWarningShown = false;
            }
        }
    }
    
    
    
    
    public float GetTotalFuel()
    {
        float total = 0f;
        foreach (var tank in fuelTanks)
        {
            if (tank != null)
            {
                total += tank.GetCurrentVolume();
            }
        }
        return total;
    }
    
    
    
    
    public float GetTotalMaxFuel()
    {
        float total = 0f;
        foreach (var tank in fuelTanks)
        {
            if (tank != null)
            {
                total += tank.GetMaxVolume();
            }
        }
        return total;
    }
    
    
    
    
    public float GetFuelPercentage()
    {
        float totalMax = GetTotalMaxFuel();
        if (totalMax <= 0f) return 0f;
        return GetTotalFuel() / totalMax;
    }
    
    
    
    
    public void AddTank(LiquidTank tank)
    {
        if (tank != null && !fuelTanks.Contains(tank))
        {
            fuelTanks.Add(tank);
        }
    }
    
    
    
    
    public void RemoveTank(LiquidTank tank)
    {
        fuelTanks.Remove(tank);
    }
    
    
    
    
    public List<LiquidTank> GetTanks()
    {
        return new List<LiquidTank>(fuelTanks);
    }
    
    
    
    
    public float GetBaseConsumptionRate()
    {
        return baseConsumptionRate;
    }
    
    
    
    
    public float GetMinConsumptionRate()
    {
        return minConsumptionRate;
    }
    
    
    
    
    public float GetConsumptionMultiplier()
    {
        return consumptionMultiplier;
    }
    
    
    
    
    
    public float GetConsumptionRateForThrust(float thrust)
    {
        float clampedThrust = Mathf.Clamp01(thrust);
        float consumptionRate = Mathf.Lerp(minConsumptionRate, baseConsumptionRate, clampedThrust);
        return consumptionRate * consumptionMultiplier;
    }
}
