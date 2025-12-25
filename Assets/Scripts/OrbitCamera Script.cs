using UnityEngine;

public class OrbitCameraFixed : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float distance = 10f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 30f;
    [SerializeField] private float sensitivity = 2f;
    [SerializeField] private float zoomSpeed = 5f;
    

    [SerializeField] private GameObject shipBodyVisuals;
    [SerializeField] private GameObject[] engineVisuals;
    [SerializeField] private float minDistanceForVisuals = 3f;
    
    private float rotationX = 0f;
    private float rotationY = 0f;
    
    private void Update()
    {
        if (target == null) return;
        

        if (Input.GetMouseButton(0))
        {
            float mouseX = Input.GetAxis("Mouse X") * sensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * sensitivity;
            
            rotationY += mouseX;
            rotationX -= mouseY;
            rotationX = Mathf.Clamp(rotationX, -90f, 90f);
        }
        

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        distance = Mathf.Clamp(distance - scroll * zoomSpeed, minDistance, maxDistance);
        

        Quaternion rotation = Quaternion.Euler(rotationX, rotationY, 0);
        Vector3 offset = rotation * Vector3.back * distance;
        transform.position = target.position + offset;
        transform.LookAt(target.position);
        
        ManageVisualsVisibility();
    }
    



    private void ManageVisualsVisibility()
    {
        bool shouldHideVisuals = distance < minDistanceForVisuals;
        
        if (shipBodyVisuals != null)
        {
            DisableRenderersOnly(shipBodyVisuals, shouldHideVisuals);
        }
        
        if (engineVisuals != null)
        {
            foreach (GameObject engine in engineVisuals)
            {
                if (engine != null)
                {
                    DisableRenderersOnly(engine, shouldHideVisuals);
                }
            }
        }
    }
    




    private void DisableRenderersOnly(GameObject obj, bool disable)
    {
        if (obj == null) return;
        

        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.enabled = !disable;
        }
        

        ParticleSystem ps = obj.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            if (disable)
                ps.Stop();
            else
                ps.Play();
        }
        

        foreach (MeshRenderer childRenderer in obj.GetComponentsInChildren<MeshRenderer>())
        {

            if (childRenderer.gameObject != obj)
            {
                childRenderer.enabled = !disable;
            }
        }
        

        foreach (ParticleSystem childPS in obj.GetComponentsInChildren<ParticleSystem>())
        {

            if (childPS.gameObject != obj)
            {
                if (disable)
                {
                    childPS.Stop();
                }
                else
                {
                    childPS.Play();
                }
            }
        }
    }
}
