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
    [SerializeField] private Color lowThrustColor = new Color(1f, 0.5f, 0f, 1f);
    [SerializeField] private Color maxThrustColor = new Color(1f, 1f, 0f, 1f);

    private void Start()
    {
        if (fireParticles == null)
        {
            Debug.LogError("Fire Particles не назначены на EngineFireController!");
            return;
        }





        SetThrust(0.3f);
    }




    public void SetThrust(float thrustPercent)
    {

        currentThrust = Mathf.Clamp01(thrustPercent);


        UpdateFireIntensity();
        UpdateFireColor();
        UpdateSmokeIntensity();
    }




    public void IncreaseTrust(float amount, float deltaTime)
    {
        SetThrust(currentThrust + amount * deltaTime);
    }




    public void DecreaseTrust(float amount, float deltaTime)
    {
        SetThrust(currentThrust - amount * deltaTime);
    }




    public float GetThrust()
    {
        return currentThrust;
    }



    private void UpdateFireIntensity()
    {
        if (fireParticles == null) return;



        var fireMain = fireParticles.main;
        var fireEmission = fireParticles.emission;


        float fireSpeed = Mathf.Lerp(minFireSpeed, maxFireSpeed, currentThrust);
        fireMain.startSpeed = new ParticleSystem.MinMaxCurve(fireSpeed);


        float fireSize = Mathf.Lerp(minFireSize, maxFireSize, currentThrust);
        fireMain.startSize = new ParticleSystem.MinMaxCurve(fireSize);


        float emissionRate = Mathf.Lerp(minEmissionRate, maxEmissionRate, currentThrust);
        fireEmission.rateOverTime = new ParticleSystem.MinMaxCurve(emissionRate);


        float alphaValue = Mathf.Lerp(0.5f, 1f, currentThrust);
        Color fireColor = fireMain.startColor.color;
        fireColor.a = alphaValue;
        fireMain.startColor = fireColor;
    }

    private void UpdateFireColor()
    {
        if (fireParticles == null) return;


        var fireMain = fireParticles.main;


        Color newColor = Color.Lerp(lowThrustColor, maxThrustColor, currentThrust);
        fireMain.startColor = newColor;
    }

    private void UpdateSmokeIntensity()
    {
        if (smokeParticles == null) return;


        var smokeEmission = smokeParticles.emission;


        float smokeEmissionRate = Mathf.Lerp(5f, 50f, currentThrust);
        smokeEmission.rateOverTime = new ParticleSystem.MinMaxCurve(smokeEmissionRate);
    }
}