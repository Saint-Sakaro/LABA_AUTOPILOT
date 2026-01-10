using UnityEngine;

public class LiquidLeakManager : MonoBehaviour
{
    [SerializeField] private ParticleSystem leakParticleSystem;
    [SerializeField] private float leakParticleSpeed = 5f;
    
    private LiquidTank attachedTank;
    private ParticleSystem.EmissionModule emissionModule;
    
    private void Start()
    {
        attachedTank = GetComponent<LiquidTank>();
        
        if (leakParticleSystem != null)
        {
            emissionModule = leakParticleSystem.emission;
            emissionModule.enabled = false;
            
            Debug.Log("LiquidLeakManager инициализирован для: " + gameObject.name);
        }
        else
        {
            Debug.LogError("LeakParticleSystem НЕ ПРИВЯЗАН на " + gameObject.name + "!", gameObject);
        }
    }
    
    private void Update()
    {
        if (attachedTank != null && leakParticleSystem != null)
        {
            var emissionModule = leakParticleSystem.emission;
            
            if (attachedTank.HasLeak())
            {
                if (!emissionModule.enabled)
                {
                    emissionModule.enabled = true;
                    
                    if (!leakParticleSystem.isPlaying)
                    {
                        leakParticleSystem.Play();
                    }
                    
                    Debug.Log("✅ Пробоина! Emission включен, частицы запущены!");
                }
            }
            else
            {
                if (emissionModule.enabled)
                {
                    emissionModule.enabled = false;
                    
                    if (leakParticleSystem.isPlaying)
                    {
                        leakParticleSystem.Stop();
                    }
                    
                    Debug.Log("❌ Пробоина закрыта, Emission выключен!");
                }
            }
        }
    }
}