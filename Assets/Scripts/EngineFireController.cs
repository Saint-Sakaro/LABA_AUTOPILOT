using UnityEngine;
public class EngineFireController : MonoBehaviour
{
    [Header("Particle System References")]
    [SerializeField] private ParticleSystem fireParticles;
    [SerializeField] private ParticleSystem smokeParticles;
    
    [Header("Fire Settings")]
    [SerializeField] private float minThrust = 0f;
    [SerializeField] private float maxThrust = 1f;
    [SerializeField] private float currentThrust = 0f;
    
    [Header("Fire Intensity")]
    [SerializeField] private float minEmissionRate = 10f;
    [SerializeField] private float maxEmissionRate = 100f;
    [SerializeField] private float minFireSpeed = 5f;
    [SerializeField] private float maxFireSpeed = 20f;
    [SerializeField] private float minFireSize = 1f;
    [SerializeField] private float maxFireSize = 3f;
    
    [Header("Color based on Thrust")]
    [SerializeField] private Color lowThrustColor = new Color(1f, 0.5f, 0f, 1f); // Оранжевый
    [SerializeField] private Color maxThrustColor = new Color(1f, 1f, 0f, 1f);   // Жёлтый
    
    private void Start()
    {
        if (fireParticles == null)
        {
            Debug.LogError("Fire Particles не назначены на EngineFireController!");
            return;
        }
        
        // Начальное состояние - слабый огонь
        SetThrust(0.3f);
    }
    
    /// <summary>
    /// Устанавливает мощность двигателя (0-1)
    /// </summary>
    public void SetThrust(float thrustPercent)
    {
        // Клампируем значение между 0 и 1
        currentThrust = Mathf.Clamp01(thrustPercent);
        
        // Интерполируем параметры на основе мощности
        UpdateFireIntensity();
        UpdateFireColor();
        UpdateSmokeIntensity();
    }
    
    /// <summary>
    /// Постепенно увеличивает мощность
    /// </summary>
    public void IncreaseTrust(float amount, float deltaTime)
    {
        SetThrust(currentThrust + amount * deltaTime);
    }
    
    /// <summary>
    /// Постепенно уменьшает мощность
    /// </summary>
    public void DecreaseTrust(float amount, float deltaTime)
    {
        SetThrust(currentThrust - amount * deltaTime);
    }
    
    /// <summary>
    /// Получить текущую мощность (0-1)
    /// </summary>
    public float GetThrust()
    {
        return currentThrust;
    }
    
    // ============ ПРИВАТНЫЕ МЕТОДЫ ============
    
    private void UpdateFireIntensity()
    {
        if (fireParticles == null) return;
        
        // Получаем модули каждый раз заново (нельзя кэшировать!)
        var fireMain = fireParticles.main;
        var fireEmission = fireParticles.emission;
        
        // Обновляем скорость частиц огня
        float fireSpeed = Mathf.Lerp(minFireSpeed, maxFireSpeed, currentThrust);
        fireMain.startSpeed = new ParticleSystem.MinMaxCurve(fireSpeed);
        
        // Обновляем размер частиц
        float fireSize = Mathf.Lerp(minFireSize, maxFireSize, currentThrust);
        fireMain.startSize = new ParticleSystem.MinMaxCurve(fireSize);
        
        // Обновляем эмиссию (количество частиц)
        float emissionRate = Mathf.Lerp(minEmissionRate, maxEmissionRate, currentThrust);
        fireEmission.rateOverTime = new ParticleSystem.MinMaxCurve(emissionRate);
        
        // Обновляем альфу огня на основе мощности
        float alphaValue = Mathf.Lerp(0.5f, 1f, currentThrust);
        Color fireColor = fireMain.startColor.color;
        fireColor.a = alphaValue;
        fireMain.startColor = fireColor;
    }
    
    private void UpdateFireColor()
    {
        if (fireParticles == null) return;
        
        // Получаем модуль каждый раз заново
        var fireMain = fireParticles.main;
        
        // Интерполируем цвет от оранжевого к жёлтому
        Color newColor = Color.Lerp(lowThrustColor, maxThrustColor, currentThrust);
        fireMain.startColor = newColor;
    }
    
    private void UpdateSmokeIntensity()
    {
        if (smokeParticles == null) return;
        
        // Получаем модуль каждый раз заново
        var smokeEmission = smokeParticles.emission;
        
        // Дым увеличивается вместе с огнём
        float smokeEmissionRate = Mathf.Lerp(5f, 50f, currentThrust);
        smokeEmission.rateOverTime = new ParticleSystem.MinMaxCurve(smokeEmissionRate);
    }
}