using UnityEngine;




public class LandingSiteIndicator : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private GameObject indicatorPrefab; 
    [SerializeField] private float indicatorHeight = 1f; 
    [SerializeField] private bool showDistance = true; 
    
    
    private float minIndicatorSize = 20f; 
    private float maxIndicatorSize = 100f; 
    private float indicatorSizeMultiplier = 1.2f; 
    
    [Header("Colors")]
    [SerializeField] private Color excellentColor = new Color(0f, 1f, 0f, 0.7f); 
    [SerializeField] private Color goodColor = new Color(0.3f, 1f, 0.3f, 0.6f); 
    [SerializeField] private Color acceptableColor = new Color(1f, 1f, 0f, 0.6f); 
    [SerializeField] private Color poorColor = new Color(1f, 0.4f, 0f, 0.6f); 
    
    private LandingSite site;
    private GameObject indicatorObject;
    private Transform shipTransform;
    private TextMesh distanceText;
    private bool indicatorCreated = false; 
    
    public void Initialize(LandingSite landingSite, Transform ship, float minSize = 20f, float maxSize = 100f, float sizeMultiplier = 1.2f)
    {
        
        if (indicatorCreated && indicatorObject != null)
        {
            return;
        }
        
        site = landingSite;
        shipTransform = ship;
        
        
        minIndicatorSize = minSize;
        maxIndicatorSize = maxSize;
        indicatorSizeMultiplier = sizeMultiplier;
        
        
        
        transform.position = landingSite.position + Vector3.up * indicatorHeight;
        
        transform.rotation = Quaternion.identity;
        
        CreateIndicator();
        UpdateVisuals();
    }
    
    private void CreateIndicator()
    {
        
        if (indicatorCreated || indicatorObject != null)
        {
            return;
        }
        
        if (indicatorPrefab != null)
        {
            indicatorObject = Instantiate(indicatorPrefab, transform);
            indicatorCreated = true;
        }
        else
        {
            
            indicatorObject = new GameObject("LandingSiteIndicator");
            indicatorObject.transform.parent = transform;
            indicatorObject.transform.localPosition = Vector3.zero;
            
            indicatorObject.transform.localRotation = Quaternion.identity;
            
            
            GameObject circle = new GameObject("CircleIndicator");
            circle.transform.parent = indicatorObject.transform;
            
            
            circle.transform.localPosition = Vector3.zero;
            
            
            
            Vector3 normal = site.surfaceNormal;
            if (normal == Vector3.zero || normal.magnitude < 0.1f)
            {
                normal = Vector3.up;
            }
            
            
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal);
            circle.transform.localRotation = rotation;
            
            
            float indicatorSize = Mathf.Max(site.size * indicatorSizeMultiplier, minIndicatorSize);
            indicatorSize = Mathf.Min(indicatorSize, maxIndicatorSize);
            
            
            Mesh circleMesh = CreateCircleMesh(indicatorSize, 32); 
            MeshFilter meshFilter = circle.AddComponent<MeshFilter>();
            meshFilter.mesh = circleMesh;
            
            MeshRenderer meshRenderer = circle.AddComponent<MeshRenderer>();
            
            
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = GetColorForScore(site.suitabilityScore);
            mat.SetFloat("_Surface", 1); 
            mat.SetFloat("_Blend", 0); 
            mat.renderQueue = 3000; 
            
            mat.SetInt("_Cull", 0); 
            meshRenderer.material = mat;
            
            indicatorCreated = true; 
            
            
            if (showDistance)
            {
                GameObject textObj = new GameObject("DistanceText");
                textObj.transform.parent = indicatorObject.transform;
                textObj.transform.localPosition = Vector3.up * (indicatorHeight + 2f);
                textObj.transform.localRotation = Quaternion.identity;
                
                distanceText = textObj.AddComponent<TextMesh>();
                distanceText.anchor = TextAnchor.MiddleCenter;
                distanceText.alignment = TextAlignment.Center;
                distanceText.fontSize = 20;
                distanceText.color = Color.white;
            }
        }
        
        
        transform.position = site.position + Vector3.up * indicatorHeight;
        
        transform.rotation = Quaternion.identity;
    }
    
    private void Update()
    {
        if (site == null || indicatorObject == null) return;
        
        
        
        
        Vector3 targetPosition = site.position + Vector3.up * indicatorHeight;
        if (transform.position != targetPosition)
        {
            transform.position = targetPosition;
        }
        
        
        
        if (transform.rotation != Quaternion.identity)
        {
            transform.rotation = Quaternion.identity;
        }
        
        
        
        if (indicatorObject != null && indicatorObject.transform.localRotation != Quaternion.identity)
        {
            indicatorObject.transform.localRotation = Quaternion.identity;
        }
        
        
        if (indicatorObject != null && site != null)
        {
            Transform circleTransform = indicatorObject.transform.Find("CircleIndicator");
            if (circleTransform != null)
            {
                Vector3 normal = site.surfaceNormal;
                if (normal == Vector3.zero || normal.magnitude < 0.1f)
                {
                    normal = Vector3.up;
                }
                Quaternion targetRotation = Quaternion.FromToRotation(Vector3.up, normal);
                if (circleTransform.localRotation != targetRotation)
                {
                    circleTransform.localRotation = targetRotation;
                }
            }
        }
        
        
        UpdateVisuals();
        
        
        if (distanceText != null && Camera.main != null)
        {
            distanceText.transform.LookAt(Camera.main.transform);
            distanceText.transform.Rotate(0, 180, 0);
        }
    }
    
    private void UpdateVisuals()
    {
        if (shipTransform != null && distanceText != null)
        {
            float distance = Vector3.Distance(shipTransform.position, site.position);
            distanceText.text = $"{distance:F0}м\n{site.suitabilityScore * 100f:F0}%";
        }
        
        
        if (indicatorObject != null)
        {
            Renderer[] renderers = indicatorObject.GetComponentsInChildren<Renderer>();
            Color targetColor = GetColorForScore(site.suitabilityScore);
            
            foreach (Renderer renderer in renderers)
            {
                if (renderer.material != null)
                {
                    renderer.material.color = targetColor;
                }
            }
        }
    }
    
    private Color GetColorForScore(float score)
    {
        
        if (score >= 0.85f) return excellentColor; 
        if (score >= 0.65f) return goodColor; 
        if (score >= 0.45f) return acceptableColor; 
        return poorColor; 
    }
    
    public LandingSite GetSite()
    {
        return site;
    }
    
    
    
    
    private Mesh CreateCircleMesh(float radius, int segments)
    {
        Mesh mesh = new Mesh();
        mesh.name = "CircleMesh";
        
        
        Vector3[] vertices = new Vector3[segments + 1];
        vertices[0] = Vector3.zero; 
        
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * 2f * Mathf.PI;
            vertices[i + 1] = new Vector3(
                Mathf.Cos(angle) * radius,
                0f, 
                Mathf.Sin(angle) * radius
            );
        }
        
        
        
        int[] triangles = new int[segments * 3 * 2]; 
        for (int i = 0; i < segments; i++)
        {
            int baseIndex = i * 3;
            int nextIndex = (i + 1) % segments + 1;
            
            
            triangles[baseIndex] = 0; 
            triangles[baseIndex + 1] = i + 1;
            triangles[baseIndex + 2] = nextIndex;
            
            
            int reverseBaseIndex = segments * 3 + baseIndex;
            triangles[reverseBaseIndex] = 0; 
            triangles[reverseBaseIndex + 1] = nextIndex;
            triangles[reverseBaseIndex + 2] = i + 1;
        }
        
        
        Vector2[] uvs = new Vector2[vertices.Length];
        uvs[0] = new Vector2(0.5f, 0.5f); 
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * 2f * Mathf.PI;
            uvs[i + 1] = new Vector2(
                0.5f + Mathf.Cos(angle) * 0.5f,
                0.5f + Mathf.Sin(angle) * 0.5f
            );
        }
        
        
        Vector3[] normals = new Vector3[vertices.Length];
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = Vector3.up;
        }
        
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.RecalculateBounds();
        
        return mesh;
    }
    
    public void Destroy()
    {
        if (indicatorObject != null)
        {
            Destroy(indicatorObject);
        }
        Destroy(gameObject);
    }
}
