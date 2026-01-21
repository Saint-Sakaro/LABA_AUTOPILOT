using UnityEngine;




public class TurbulenceZone : MonoBehaviour
{
    [Header("Zone Settings")]
    [SerializeField] private Vector3 zoneSize = new Vector3(100f, 50f, 100f);
    [SerializeField] private float turbulenceStrength = 10f;
    [SerializeField] private float turbulenceFrequency = 1f;
    [SerializeField] private bool useNoise = true;
    
    
    public void SetZoneSize(Vector3 size) { zoneSize = size; }
    public void SetTurbulenceStrength(float strength) { turbulenceStrength = strength; }
    public void SetTurbulenceFrequency(float frequency) { turbulenceFrequency = frequency; }
    public void SetShowInGame(bool show) 
    { 
        bool wasShowing = showInGame;
        showInGame = show; 
        
        
        if (Application.isPlaying)
        {
            
            SetupGameVisualization();
            UpdateGameVisualization();
            
            if (showDebugInfo && show && !wasShowing)
            {
                Debug.Log($"TurbulenceZone: включена визуализация для зоны на позиции {transform.position}, размер={zoneSize}");
            }
        }
    }
    
    [Header("Force Settings")]
    [SerializeField] private float maxForceMagnitude = 100f; 
    [SerializeField] private float maxTorqueMagnitude = 300f; 
    [SerializeField] private float forceVariation = 0.3f;
    [SerializeField] private float rotationBias = 0.8f; 
    
    [Header("Visualization")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color gizmoColor = new Color(1f, 0.5f, 0f, 0.3f);
    [SerializeField] private bool showInGame = false; 
    [SerializeField] private GameObject visualCube; 
    [SerializeField] private bool showDebugInfo = false; 
    
    private float noiseOffset = 0f;
    private int noiseSeed = 0;
    private Material visualMaterial;
    
    private void Start()
    {
        noiseOffset = Random.Range(0f, 10000f);
        noiseSeed = Random.Range(0, 10000);
        
        
        
    }
    
    private void SetupGameVisualization()
    {
        if (!showInGame) 
        {
            
            if (visualCube != null)
            {
                Destroy(visualCube);
                visualCube = null;
            }
            return;
        }
        
        
        if (visualCube == null)
        {
            visualCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visualCube.name = "TurbulenceZoneVisual";
            visualCube.transform.SetParent(transform);
            visualCube.transform.localPosition = Vector3.zero;
            visualCube.transform.localRotation = Quaternion.identity;
            visualCube.transform.localScale = zoneSize;
            
            
            Collider col = visualCube.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }
            
            
            MeshRenderer renderer = visualCube.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                visualMaterial = new Material(Shader.Find("Standard"));
                visualMaterial.color = gizmoColor;
                visualMaterial.SetFloat("_Mode", 3); 
                visualMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                visualMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                visualMaterial.SetInt("_ZWrite", 0);
                visualMaterial.DisableKeyword("_ALPHATEST_ON");
                visualMaterial.EnableKeyword("_ALPHABLEND_ON");
                visualMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                visualMaterial.renderQueue = 3000;
                
                renderer.material = visualMaterial;
                
                if (showDebugInfo)
                {
                    Debug.Log($"TurbulenceZone: создан визуальный куб для зоны на позиции {transform.position}, размер={zoneSize}, цвет={gizmoColor}, showInGame={showInGame}");
                }
            }
            else
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning($"TurbulenceZone: не удалось получить MeshRenderer для визуального куба на позиции {transform.position}");
                }
            }
        }
        else
        {
            
            visualCube.transform.localScale = zoneSize;
            if (visualMaterial != null)
            {
                visualMaterial.color = gizmoColor;
            }
        }
    }
    
    private void Update()
    {
        UpdateGameVisualization();
    }
    
    private void UpdateGameVisualization()
    {
        if (visualCube != null)
        {
            visualCube.SetActive(showInGame);
            
            if (showInGame)
            {
                
                visualCube.transform.localScale = zoneSize;
                
                if (visualMaterial != null)
                {
                    visualMaterial.color = gizmoColor;
                }
            }
        }
        else if (showInGame && Application.isPlaying)
        {
            
            SetupGameVisualization();
        }
    }
    
    private void OnValidate()
    {
        
        if (Application.isPlaying && visualCube != null)
        {
            UpdateGameVisualization();
        }
    }
    
    
    
    
    public bool IsPositionInside(Vector3 worldPosition)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPosition);
        return Mathf.Abs(localPos.x) <= zoneSize.x * 0.5f &&
               Mathf.Abs(localPos.y) <= zoneSize.y * 0.5f &&
               Mathf.Abs(localPos.z) <= zoneSize.z * 0.5f;
    }
    
    
    
    
    public Vector3 GetTurbulenceForce(Vector3 worldPosition, float deltaTime)
    {
        if (!IsPositionInside(worldPosition))
        {
            return Vector3.zero;
        }
        
        
        float forceReduction = 1f - rotationBias;
        
        Vector3 localPos = transform.InverseTransformPoint(worldPosition);
        
        if (useNoise)
        {
            
            float time = Time.time * turbulenceFrequency + noiseOffset;
            float noiseX = Mathf.PerlinNoise(localPos.x * 0.1f + time, noiseSeed * 0.1f) * 2f - 1f;
            float noiseY = Mathf.PerlinNoise(localPos.y * 0.1f + time, (noiseSeed + 1000) * 0.1f) * 2f - 1f;
            float noiseZ = Mathf.PerlinNoise(localPos.z * 0.1f + time, (noiseSeed + 2000) * 0.1f) * 2f - 1f;
            
            
            Vector3 noiseDirection = new Vector3(noiseX, noiseY, noiseZ).normalized;
            float noiseMagnitude = (Mathf.PerlinNoise(time * 0.5f, noiseSeed * 0.5f) * 2f - 1f) * forceVariation + 1f;
            
            float forceMagnitude = turbulenceStrength * maxForceMagnitude * noiseMagnitude * forceReduction;
            return transform.TransformDirection(noiseDirection) * forceMagnitude;
        }
        else
        {
            
            float randomX = (Random.value * 2f - 1f);
            float randomY = (Random.value * 2f - 1f);
            float randomZ = (Random.value * 2f - 1f);
            Vector3 randomDirection = new Vector3(randomX, randomY, randomZ).normalized;
            
            float forceMagnitude = turbulenceStrength * maxForceMagnitude * forceReduction;
            return transform.TransformDirection(randomDirection) * forceMagnitude * deltaTime;
        }
    }
    
    
    
    
    public Vector3 GetTurbulenceTorque(Vector3 worldPosition, float deltaTime)
    {
        if (!IsPositionInside(worldPosition))
        {
            return Vector3.zero;
        }
        
        Vector3 localPos = transform.InverseTransformPoint(worldPosition);
        
        if (useNoise)
        {
            
            float time = Time.time * turbulenceFrequency * 0.7f + noiseOffset;
            
            
            
            float rollNoise = Mathf.PerlinNoise(localPos.x * 0.15f + time, (noiseSeed + 3000) * 0.1f) * 2f - 1f;
            float pitchNoise = Mathf.PerlinNoise(localPos.y * 0.15f + time, (noiseSeed + 4000) * 0.1f) * 2f - 1f;
            float yawNoise = Mathf.PerlinNoise(localPos.z * 0.15f + time, (noiseSeed + 5000) * 0.1f) * 2f - 1f;
            
            
            float rollMultiplier = 1.2f; 
            float pitchMultiplier = 0.9f; 
            float yawMultiplier = 0.7f; 
            
            Vector3 torqueAxis = new Vector3(
                rollNoise * rollMultiplier,
                pitchNoise * pitchMultiplier,
                yawNoise * yawMultiplier
            ).normalized;
            
            
            float noiseMagnitude = (Mathf.PerlinNoise(time * 0.6f, (noiseSeed + 6000) * 0.5f) * 2f - 1f) * forceVariation + 1f;
            float torqueMagnitude = turbulenceStrength * maxTorqueMagnitude * noiseMagnitude * rotationBias;
            
            
            return torqueAxis * torqueMagnitude;
        }
        else
        {
            
            float rollRandom = (Random.value * 2f - 1f) * 1.2f;
            float pitchRandom = (Random.value * 2f - 1f) * 0.9f;
            float yawRandom = (Random.value * 2f - 1f) * 0.7f;
            Vector3 randomDirection = new Vector3(rollRandom, pitchRandom, yawRandom).normalized;
            
            float torqueMagnitude = turbulenceStrength * maxTorqueMagnitude * rotationBias;
            return randomDirection * torqueMagnitude * deltaTime;
        }
    }
    
    
    
    
    public float GetTurbulenceIntensity(Vector3 worldPosition)
    {
        if (!IsPositionInside(worldPosition))
        {
            return 0f;
        }
        
        Vector3 localPos = transform.InverseTransformPoint(worldPosition);
        
        
        float normalizedX = Mathf.Abs(localPos.x) / (zoneSize.x * 0.5f);
        float normalizedY = Mathf.Abs(localPos.y) / (zoneSize.y * 0.5f);
        float normalizedZ = Mathf.Abs(localPos.z) / (zoneSize.z * 0.5f);
        
        
        float maxDistance = Mathf.Max(normalizedX, normalizedY, normalizedZ);
        
        
        float intensity = 1f - Mathf.Clamp01(maxDistance);
        intensity = intensity * intensity; 
        
        return intensity * turbulenceStrength;
    }
    
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        
        
        Matrix4x4 originalMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        
        
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, gizmoColor.a * 0.2f);
        Gizmos.DrawCube(Vector3.zero, zoneSize);
        
        
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(Vector3.zero, zoneSize);
        
        
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f); 
        Gizmos.DrawLine(Vector3.zero, Vector3.right * zoneSize.x * 0.3f);
        Gizmos.color = new Color(0f, 1f, 0f, 0.5f); 
        Gizmos.DrawLine(Vector3.zero, Vector3.up * zoneSize.y * 0.3f);
        Gizmos.color = new Color(0f, 0f, 1f, 0.5f); 
        Gizmos.DrawLine(Vector3.zero, Vector3.forward * zoneSize.z * 0.3f);
        
        
        Gizmos.matrix = originalMatrix;
    }
    
    private void OnDrawGizmosSelected()
    {
        Matrix4x4 originalMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.zero, zoneSize);
        
        
        Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
        Vector3 halfSize = zoneSize * 0.5f;
        
        Gizmos.DrawLine(new Vector3(-halfSize.x, halfSize.y, -halfSize.z), new Vector3(halfSize.x, halfSize.y, halfSize.z));
        Gizmos.DrawLine(new Vector3(halfSize.x, halfSize.y, -halfSize.z), new Vector3(-halfSize.x, halfSize.y, halfSize.z));
        
        Gizmos.DrawLine(new Vector3(-halfSize.x, -halfSize.y, -halfSize.z), new Vector3(halfSize.x, -halfSize.y, halfSize.z));
        Gizmos.DrawLine(new Vector3(halfSize.x, -halfSize.y, -halfSize.z), new Vector3(-halfSize.x, -halfSize.y, halfSize.z));
        
        
        float intensity = turbulenceStrength;
        Color intensityColor = Color.Lerp(Color.green, Color.red, Mathf.Clamp01(intensity * 0.1f));
        Gizmos.color = new Color(intensityColor.r, intensityColor.g, intensityColor.b, 0.4f);
        Gizmos.DrawCube(Vector3.zero, zoneSize * 0.98f);
        
        Gizmos.matrix = originalMatrix;
    }
    
    private void OnDestroy()
    {
        
        if (visualMaterial != null)
        {
            Destroy(visualMaterial);
        }
        
        if (visualCube != null)
        {
            Destroy(visualCube);
        }
    }
}
