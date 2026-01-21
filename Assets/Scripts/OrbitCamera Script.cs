using UnityEngine;

public class OrbitCameraFixed : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float distance = 10f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 30f;
    [SerializeField] private float sensitivity = 2f;
    [SerializeField] private float zoomSpeed = 5f;
    
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
    }
}