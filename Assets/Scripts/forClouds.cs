using UnityEngine;

public class CloudGenerator : MonoBehaviour
{
    public GameObject cloudPrefab; // Префаб облака
    public float spawnHeight = -50f; // Высота спауна облаков
    public float maxDistance = 200f; // Максимальное расстояние
    public float cloudSpeed = 2f;
    public int cloudCount = 20;
    
    void Start()
    {
        // Генерируем облака снизу
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
    private float targetHeight = 20f; // Высота острова
    
    void Update()
    {
        // Облако движется вверх к острову
        transform.Translate(Vector3.up * speed * Time.deltaTime);
        
        // Если облако прошло выше острова, сбрасываем его вниз
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
