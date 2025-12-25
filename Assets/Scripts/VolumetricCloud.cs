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
    
    private float pulsateTimer = 0f;
    
    private void Start()
    {
        if (cloudParticles == null)
            cloudParticles = GetComponentInChildren<ParticleSystem>();
        
        if (cloudMaterial == null)
            cloudMaterial = cloudParticles.GetComponent<ParticleSystemRenderer>().material;
        

        if (cloudMaterial.mainTexture == null)
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
    



    private void DisableVelocityOverLifetime()
    {
        var velocity = cloudParticles.velocityOverLifetime;
        velocity.enabled = false;
    }
    
    private void Update()
    {

        UpdatePulsation();
        

        transform.position += windDirection.normalized * windSpeed * Time.deltaTime;
        

        cloudMaterial.SetFloat("_Density", density);
    }
    
    private void UpdatePulsation()
    {
        if (pulsate)
        {
            pulsateTimer += Time.deltaTime * pulsateSpeed;
            
            float pulsateAlpha = Mathf.Lerp(minAlpha, maxAlpha, 
                (Mathf.Sin(pulsateTimer * Mathf.PI) + 1f) * 0.5f);
            
            SetAlpha(pulsateAlpha);
        }
        else
        {
            SetAlpha(alpha);
        }
    }
    
    public void SetAlpha(float newAlpha)
    {
        alpha = Mathf.Clamp01(newAlpha);
        Color color = cloudMaterial.color;
        color.a = alpha;
        cloudMaterial.color = color;
    }
    
    
    public void SetDensity(float newDensity)
    {
        density = Mathf.Clamp(newDensity, 0.1f, 2f);
        cloudMaterial.SetFloat("_Density", density);
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
