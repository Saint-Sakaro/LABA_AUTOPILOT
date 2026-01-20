using UnityEngine;
using System.Collections;

public class MusicVolumeController : MonoBehaviour
{
    private AudioSource audioSource;
    
    [SerializeField] private float quietVolume = 0.2f;      // Приглушенная громкость
    [SerializeField] private float normalVolume = 0.8f;     // Нормальная громкость
    [SerializeField] private float fadeDuration = 1.0f;     // Длительность перехода (секунды)
    
    private bool isOpened = false;  // Открыт объект или нет
    private Coroutine fadeCoroutine;
    
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        
        // Устанавливаем начальный звук на приглушенный
        audioSource.volume = quietVolume;
        audioSource.Play();
    }
    
    void Update()
    {
        // При нажатии на H переключаем состояние
        if (Input.GetKeyDown(KeyCode.H))
        {
            isOpened = !isOpened;
            
            // Останавливаем предыдущую анимацию, если она была
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            
            // Запускаем новую анимацию перехода громкости
            float targetVolume = isOpened ? normalVolume : quietVolume;
            fadeCoroutine = StartCoroutine(FadeVolume(targetVolume));
        }
    }
    
    // Корутина для плавного перехода громкости
    IEnumerator FadeVolume(float targetVolume)
    {
        float startVolume = audioSource.volume;
        float elapsedTime = 0f;
        
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            
            // Плавный переход от начальной громкости к целевой
            audioSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsedTime / fadeDuration);
            
            yield return null;
        }
        
        // Гарантируем точное значение в конце
        audioSource.volume = targetVolume;
    }
}
