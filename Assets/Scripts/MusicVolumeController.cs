using UnityEngine;
using System.Collections;

public class MusicVolumeController : MonoBehaviour
{
    private AudioSource audioSource;
    
    [SerializeField] private float quietVolume = 0.2f;      
    [SerializeField] private float normalVolume = 0.8f;     
    [SerializeField] private float fadeDuration = 1.0f;     
    
    private bool isOpened = false;  
    private Coroutine fadeCoroutine;
    
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        
        
        audioSource.volume = quietVolume;
        audioSource.Play();
    }
    
    void Update()
    {
        
        if (Input.GetKeyDown(KeyCode.H))
        {
            isOpened = !isOpened;
            
            
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            
            
            float targetVolume = isOpened ? normalVolume : quietVolume;
            fadeCoroutine = StartCoroutine(FadeVolume(targetVolume));
        }
    }
    
    
    IEnumerator FadeVolume(float targetVolume)
    {
        float startVolume = audioSource.volume;
        float elapsedTime = 0f;
        
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            
            
            audioSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsedTime / fadeDuration);
            
            yield return null;
        }
        
        
        audioSource.volume = targetVolume;
    }
}
