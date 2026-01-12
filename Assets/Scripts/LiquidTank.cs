using UnityEngine;

public class LiquidTank : MonoBehaviour
{
    [Header("Жидкость")]
    [SerializeField] private float maxVolume = 500f; 
    [SerializeField] private float currentVolume = 500f; 
    [SerializeField] private float liquidDensity = 0.8f; 
    
    [Header("Убывание (расход топлива)")]
    [SerializeField] private float consumptionRate = 5f; 
    [SerializeField] private bool isConsuming = true; 
    
    [Header("Утечка (пробоина)")]
    [SerializeField] private float leakRate = 0f; 
    [SerializeField] private bool hasLeak = false;
    
    [Header("Визуализация")]
    [SerializeField] private Transform liquidVisualTransform;
    [SerializeField] private float tankHeight = 2f;
    [SerializeField] private Vector3 tankSize = new Vector3(1f, 2f, 1f);
    
    public delegate void LiquidChangedDelegate(float newVolume, float newMass);
    public event LiquidChangedDelegate OnLiquidChanged;
    
    private float currentMass;
    private Vector3 centerOfMass;
    private LiquidPhysics liquidPhysics;
    
    private void Start()
    {
        liquidPhysics = GetComponent<LiquidPhysics>();
        if (liquidPhysics == null)
        {
            liquidPhysics = gameObject.AddComponent<LiquidPhysics>();
        }
        
        currentMass = currentVolume * liquidDensity;
        centerOfMass = transform.position;
        UpdateVisuals();
    }
    
    private void Update()
    {
        // Расход топлива теперь управляется FuelManager, а не здесь
        // Оставляем только для обратной совместимости, если FuelManager не используется
        if (isConsuming && currentVolume > 0)
        {
            float consumption = consumptionRate * Time.deltaTime;
            DecreaseVolume(consumption);
        }
        
        if (hasLeak && currentVolume > 0)
        {
            float leak = leakRate * Time.deltaTime;
            DecreaseVolume(leak);
        }
        
        if (liquidPhysics != null)
        {
            liquidPhysics.UpdateLiquidProperties(currentVolume, currentMass, centerOfMass);
        }
    }
    
    public void DecreaseVolume(float amount)
    {
        currentVolume = Mathf.Max(0, currentVolume - amount);
        currentMass = currentVolume * liquidDensity;
        
        UpdateCenterOfMass();
        UpdateVisuals();
        
        OnLiquidChanged?.Invoke(currentVolume, currentMass);
    }
    

    public void CreateLeak(float leakRateValue)
    {
        hasLeak = true;
        leakRate = leakRateValue;
        Debug.Log($"Пробоина создана! Утечка: {leakRate} л/сек");
    }
    

    public void CloseLeak()
    {
        hasLeak = false;
        leakRate = 0f;
    }
    

    public void SetConsuming(bool value)
    {
        isConsuming = value;
    }
    
    private void UpdateCenterOfMass()
    {
        float fillPercentage = currentVolume / maxVolume;
        float heightOffset = (1f - fillPercentage) * (tankHeight / 2f);
        
        centerOfMass = transform.position + Vector3.down * heightOffset;
    }
    
    private void UpdateVisuals()
    {
        if (liquidVisualTransform != null)
        {
            float fillPercentage = currentVolume / maxVolume;
            
            Vector3 newScale = liquidVisualTransform.localScale;
            newScale.y = fillPercentage * tankHeight;
            liquidVisualTransform.localScale = newScale;
            
            float heightDifference = (tankHeight - newScale.y) / 2f;
            liquidVisualTransform.localPosition = new Vector3(0, -heightDifference, 0);
        }
    }
    
    public float GetCurrentVolume() => currentVolume;
    public float GetCurrentMass() => currentMass;
    public Vector3 GetCenterOfMass() => centerOfMass;
    public bool HasLeak() => hasLeak;
    public float GetFillPercentage() => currentVolume / maxVolume;
    public float GetMaxVolume() => maxVolume;
    
    /// <summary>
    /// Устанавливает потребление топлива извне (например, от FuelManager)
    /// </summary>
    public void SetConsumptionRate(float rate)
    {
        consumptionRate = rate;
    }
    
    /// <summary>
    /// Отключает автоматическое потребление (для использования с FuelManager)
    /// </summary>
    public void DisableAutoConsumption()
    {
        isConsuming = false;
    }
}