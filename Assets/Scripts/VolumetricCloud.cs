using UnityEngine;

public class VolumetricCloud : MonoBehaviour
{
    [SerializeField] private ParticleSystem cloudParticles;
    [SerializeField] private Material cloudMaterial;
    
    [Header("Cloud Properties")]
    [SerializeField] private float density = 1f;
    [SerializeField] private float alpha = 0.7f;
    
    [Header("Movement")]
    [SerializeField] private Vector3 windDirection = Vector3.right;
    [SerializeField] private float windSpeed = 0.5f;
    
    [Header("Visual")]
    [SerializeField] private float minAlpha = 0.4f;
    [SerializeField] private float maxAlpha = 0.9f;
    [SerializeField] private bool pulsate = true;
    [SerializeField] private float pulsateSpeed = 0.5f;
    
    [Header("Ground Interaction")]
    [SerializeField] private bool checkGroundHeight = true;
    [SerializeField] private float minCloudHeight = 30f;
    [SerializeField] private float criticalHeight = 15f;
    [SerializeField] private float safetyMargin = 10f;
    [SerializeField] private float dissolveSpeed = 2f;
    [SerializeField] private bool destroyOnGroundContact = true;
    
    private float pulsateTimer = 0f;
    private float baseAlpha = 0.7f;
    private bool isDissolving = false;
    
    private void Start()
    {
        if (cloudParticles == null)
            cloudParticles = GetComponentInChildren<ParticleSystem>();
        
        if (cloudParticles != null)
        {
            if (cloudMaterial == null)
                cloudMaterial = cloudParticles.GetComponent<ParticleSystemRenderer>().material;

            if (cloudMaterial != null && cloudMaterial.mainTexture == null)
            {
                Texture2D cloudTexture = CloudTextureGenerator.GenerateCloudTexture(
                    256, 
                    50f, 
                    Random.Range(0, 10000)
                );
                cloudMaterial.mainTexture = cloudTexture;
            }
            
            DisableVelocityOverLifetime();
        }
        
        baseAlpha = alpha;
    }
    
    private void DisableVelocityOverLifetime()
    {
        if (cloudParticles == null) return;
        var velocity = cloudParticles.velocityOverLifetime;
        velocity.enabled = false;  
    }
    
    private void Update()
    {
        if (checkGroundHeight)
        {
            UpdateGroundInteraction();
        }
        
        if (!isDissolving)
        {
            UpdatePulsation();
        }
        
        transform.position += windDirection.normalized * windSpeed * Time.deltaTime;
        
        if (cloudMaterial != null)
        {
            cloudMaterial.SetFloat("_Density", density);
        }
    }
    
    private void UpdateGroundInteraction()
    {
        Vector3 cloudPos = transform.position;
        float groundHeight = HillGenerator.GetHeightAtPosition(cloudPos);
        float cloudHeight = cloudPos.y - groundHeight;
        
        if (cloudHeight < criticalHeight)
        {
            // Критическая высота - быстрое растворение
            isDissolving = true;
            float dissolveFactor = Mathf.Clamp01(cloudHeight / criticalHeight);
            float targetAlpha = dissolveFactor * baseAlpha * 0.1f; // Почти невидимо
            
            float currentAlpha = alpha;
            alpha = Mathf.Lerp(currentAlpha, targetAlpha, Time.deltaTime * dissolveSpeed * 2f);
            SetAlpha(alpha);
            
            // Полное удаление при достижении земли
            if (destroyOnGroundContact && cloudHeight < safetyMargin)
            {
                Destroy(gameObject);
                return;
            }
        }
        else if (cloudHeight < minCloudHeight)
        {
            // Приближение к минимальной высоте - постепенное рассеивание
            float distanceToMin = cloudHeight - criticalHeight;
            float range = minCloudHeight - criticalHeight;
            float fadeFactor = Mathf.Clamp01(distanceToMin / range);
            
            // Уменьшаем плотность и альфа при приближении к земле
            float targetAlpha = baseAlpha * fadeFactor;
            
            if (pulsate)
            {
                pulsateTimer += Time.deltaTime * pulsateSpeed;
                float pulsateFactor = (Mathf.Sin(pulsateTimer * Mathf.PI) + 1f) * 0.5f;
                float pulsateAlpha = Mathf.Lerp(minAlpha, maxAlpha, pulsateFactor);
                targetAlpha = pulsateAlpha * fadeFactor;
            }
            
            alpha = Mathf.Lerp(alpha, targetAlpha, Time.deltaTime * dissolveSpeed);
            density = Mathf.Lerp(density, 1f * fadeFactor, Time.deltaTime * dissolveSpeed * 0.5f);
            SetAlpha(alpha);
            SetDensity(density);
        }
        else
        {
            // Нормальная высота - восстанавливаем значения
            if (isDissolving)
            {
                // Постепенно восстанавливаем облако, если оно поднялось выше
                alpha = Mathf.Lerp(alpha, baseAlpha, Time.deltaTime * dissolveSpeed * 0.5f);
                density = Mathf.Lerp(density, 1f, Time.deltaTime * dissolveSpeed * 0.3f);
                SetAlpha(alpha);
                SetDensity(density);
                
                // Если восстановилось достаточно - прекращаем растворение
                if (alpha > baseAlpha * 0.9f)
                {
                    isDissolving = false;
                }
            }
        }
    }
    
    private void UpdatePulsation()
    {
        if (pulsate && !isDissolving)
        {
            pulsateTimer += Time.deltaTime * pulsateSpeed;
            
            float pulsateAlpha = Mathf.Lerp(minAlpha, maxAlpha, 
                (Mathf.Sin(pulsateTimer * Mathf.PI) + 1f) * 0.5f);
            
            SetAlpha(pulsateAlpha);
        }
        else if (!isDissolving)
        {
            SetAlpha(alpha);
        }
    }
    
    public void SetAlpha(float newAlpha)
    {
        alpha = Mathf.Clamp01(newAlpha);
        if (cloudMaterial != null)
        {
            Color color = cloudMaterial.color;
            color.a = alpha;
            cloudMaterial.color = color;
        }
    }
    
    
    public void SetDensity(float newDensity)
    {
        density = Mathf.Clamp(newDensity, 0.1f, 2f);
        if (cloudMaterial != null)
        {
            cloudMaterial.SetFloat("_Density", density);
        }
    }
    
    public float GetAlpha() => alpha;
    
    public void SetPulsate(bool enabled)
    {
        pulsate = enabled;
    }
    
    public void SetPulsateSpeed(float speed)
    {
        pulsateSpeed = Mathf.Max(0f, speed);
    }

    public void SetWind(Vector3 direction, float speed)
    {
        windDirection = direction.normalized;
        windSpeed = Mathf.Max(0f, speed);
    }

}
