

using UnityEngine;
#if UNITY_2019_3_OR_NEWER
using UnityEngine.VFX;
#endif




public class AdvancedEngineController : MonoBehaviour
{
    [Header("Particle System")]
    [SerializeField] private ParticleSystem fireParticles;
    [SerializeField] private ParticleSystem smokeParticles;
    
    #if UNITY_2019_3_OR_NEWER
    [Header("VFX Graph")]
    [SerializeField] private VisualEffect vfxGraph;
    #endif
    
    [Header("Engine Settings")]
    [SerializeField] private float currentThrust = 0.3f;
    [SerializeField] private float thrustLerpSpeed = 5f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource engineSound;
    [SerializeField] private AnimationCurve engineVolume = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    private float targetThrust;
    private EngineFireController fireController;
    
    private void Start()
    {
        targetThrust = currentThrust;
        fireController = GetComponent<EngineFireController>();
        
        if (fireParticles == null)
        {
            fireParticles = GetComponentInChildren<ParticleSystem>();
        }
    }
    
    private void Update()
    {

        currentThrust = Mathf.Lerp(currentThrust, targetThrust, thrustLerpSpeed * Time.deltaTime);
        
        if (fireController != null)
        {
            fireController.SetThrust(currentThrust);
        }
        
        UpdateVFXGraph();
        UpdateEngineAudio();
    }
    
    public void SetTargetThrust(float thrust)
    {
        targetThrust = Mathf.Clamp01(thrust);
    }
    
    public float GetCurrentThrust()
    {
        return currentThrust;
    }
    
    private void UpdateVFXGraph()
    {
        #if UNITY_2019_3_OR_NEWER
        if (vfxGraph != null)
        {

            vfxGraph.SetFloat("Thrust", currentThrust);
            vfxGraph.SetFloat("FireIntensity", currentThrust);
        }
        #endif
    }
    
    private void UpdateEngineAudio()
    {
        if (engineSound == null) return;
        
        float volumeValue = engineVolume.Evaluate(currentThrust);
        engineSound.volume = volumeValue;
        

        engineSound.pitch = Mathf.Lerp(0.8f, 1.2f, currentThrust);
    }
}


































































