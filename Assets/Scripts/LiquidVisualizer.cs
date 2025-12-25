using UnityEngine;

public class LiquidVisualizer : MonoBehaviour
{
    [SerializeField] private Material liquidMaterial;
    [SerializeField] private Color liquidColor = new Color(0.2f, 0.6f, 0.9f, 0.7f);
    [SerializeField] private float waveAmplitude = 0.1f;
    [SerializeField] private float waveSpeed = 2f;
    [SerializeField] private float transparency = 0.5f;
    
    private MeshRenderer meshRenderer;
    private LiquidTank attachedTank;
    private float time = 0f;
    
    private void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        attachedTank = GetComponentInParent<LiquidTank>();
        

        if (meshRenderer != null && liquidMaterial != null)
        {
            Material newMaterial = new Material(liquidMaterial);
            meshRenderer.material = newMaterial;
        }
    }
    
    private void Update()
    {
        if (meshRenderer == null) return;
        
        time += Time.deltaTime;
        

        Rigidbody shipRb = GetComponentInParent<Rigidbody>();
        if (shipRb != null && meshRenderer != null && meshRenderer.material != null)
        {

            float velocityMagnitude = shipRb.linearVelocity.magnitude;
            

            meshRenderer.material.SetFloat("_ShipVelocityMagnitude", velocityMagnitude);
            meshRenderer.material.SetFloat("_ShipVelocityX", shipRb.linearVelocity.x);
            meshRenderer.material.SetFloat("_ShipVelocityY", Mathf.Abs(shipRb.linearVelocity.y));
            meshRenderer.material.SetFloat("_ShipVelocityZ", shipRb.linearVelocity.z);
        }
        

        if (meshRenderer.material != null)
        {
            meshRenderer.material.SetFloat("_Time", time);
            meshRenderer.material.SetFloat("_WaveAmplitude", waveAmplitude);
            meshRenderer.material.SetFloat("_WaveSpeed", waveSpeed);
            

            meshRenderer.material.SetFloat("_Transparency", transparency);
            

            meshRenderer.material.SetColor("_BaseColor", liquidColor);
        }
        

        if (attachedTank != null)
        {
            float fillPercentage = attachedTank.GetFillPercentage();
            Color dynamicColor = Color.Lerp(
                new Color(0.2f, 0.2f, 0.2f, 0.3f),
                liquidColor,
                fillPercentage
            );

        }
    }
}
