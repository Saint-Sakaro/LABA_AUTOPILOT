using UnityEngine;

public class CameraFarPlaneSetup : MonoBehaviour
{
    [SerializeField] private float farClipPlane = 10000f;

    private void Start()
    {
        Camera camera = GetComponent<Camera>();
        if (camera != null)
        {
            camera.farClipPlane = farClipPlane;
            Debug.Log($"Camera far clip plane установлена на: {farClipPlane}");
        }
    }
}