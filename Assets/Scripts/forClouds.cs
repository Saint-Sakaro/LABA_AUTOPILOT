using UnityEngine;

public class CloudGenerator : MonoBehaviour
{
    public GameObject cloudPrefab;
    public float spawnHeight = -50f;
    public float maxDistance = 200f;
    public float cloudSpeed = 2f;
    public int cloudCount = 20;
    
    void Start()
    {

        for (int i = 0; i < cloudCount; i++)
        {
            Vector3 randomPos = new Vector3(
                Random.Range(-maxDistance, maxDistance),
                spawnHeight,
                Random.Range(-maxDistance, maxDistance)
            );
            
            GameObject cloud = Instantiate(cloudPrefab, randomPos, Quaternion.identity);
            cloud.AddComponent<CloudMovement>().speed = cloudSpeed;
        }
    }
}

public class CloudMovement : MonoBehaviour
{
    public float speed = 2f;
    private float maxDistance = 200f;
    private float resetHeight = -100f;
    private float targetHeight = 20f;
    
    void Update()
    {

        transform.Translate(Vector3.up * speed * Time.deltaTime);
        

        if (transform.position.y > targetHeight)
        {
            transform.position = new Vector3(
                Random.Range(-maxDistance, maxDistance),
                resetHeight,
                Random.Range(-maxDistance, maxDistance)
            );
        }
    }
}
