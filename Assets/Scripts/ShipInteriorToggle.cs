using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ShipInteriorToggle : MonoBehaviour
{
    [SerializeField] private GameObject outsides;
    [SerializeField] private GameObject insides;
    [SerializeField] private float fadeDuration = 3f;
    
    private bool isOutsidesVisible = true;
    private Coroutine currentFadeCoroutine;
    
    private List<Renderer> outsidesRenderers = new List<Renderer>();
    private List<Renderer> insidesRenderers = new List<Renderer>();
    
    private Dictionary<Renderer, Material[]> outsidesMaterialsDict = new Dictionary<Renderer, Material[]>();
    private Dictionary<Renderer, Material[]> insidesMaterialsDict = new Dictionary<Renderer, Material[]>();

    private void Start()
    {
        Debug.Log("=== ИНИЦИАЛИЗАЦИЯ ===");
        
        outsides.GetComponentsInChildren<Renderer>(outsidesRenderers);
        insides.GetComponentsInChildren<Renderer>(insidesRenderers);
        
        Debug.Log($"Outsides рендереров: {outsidesRenderers.Count}");
        Debug.Log($"Insides рендереров: {insidesRenderers.Count}");
        
        
        foreach (Renderer renderer in outsidesRenderers)
        {
            Material[] materialsInstance = new Material[renderer.materials.Length];
            for (int i = 0; i < renderer.materials.Length; i++)
            {
                materialsInstance[i] = new Material(renderer.materials[i]);
            }
            renderer.materials = materialsInstance;
            outsidesMaterialsDict[renderer] = materialsInstance;
        }
        
        
        foreach (Renderer renderer in insidesRenderers)
        {
            Material[] materialsInstance = new Material[renderer.materials.Length];
            for (int i = 0; i < renderer.materials.Length; i++)
            {
                materialsInstance[i] = new Material(renderer.materials[i]);
            }
            renderer.materials = materialsInstance;
            insidesMaterialsDict[renderer] = materialsInstance;
        }
        
        SetOutsidesAlpha(1f);
        SetInsidesAlpha(0f);
        
        Debug.Log("Готово Нажми H.");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            ToggleInterior();
        }
    }

    private void ToggleInterior()
    {
        if (currentFadeCoroutine != null)
        {
            StopCoroutine(currentFadeCoroutine);
        }
        
        if (isOutsidesVisible)
        {
            currentFadeCoroutine = StartCoroutine(FadeOutsidesToInsides());
        }
        else
        {
            currentFadeCoroutine = StartCoroutine(FadeInsidesToOutsides());
        }
        
        isOutsidesVisible = !isOutsidesVisible;
    }

    private IEnumerator FadeOutsidesToInsides()
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / fadeDuration;
            
            SetOutsidesAlpha(1f - t);
            SetInsidesAlpha(t);
            
            yield return null;
        }
        
        SetOutsidesAlpha(0f);
        SetInsidesAlpha(1f);
    }

    private IEnumerator FadeInsidesToOutsides()
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / fadeDuration;
            
            SetInsidesAlpha(1f - t);
            SetOutsidesAlpha(t);
            
            yield return null;
        }
        
        SetInsidesAlpha(0f);
        SetOutsidesAlpha(1f);
    }

    private void SetOutsidesAlpha(float alpha)
    {
        foreach (var kvp in outsidesMaterialsDict)
        {
            Material[] materials = kvp.Value;
            
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null) continue;
                
                if (materials[i].HasProperty("_BaseColor"))
                {
                    Color color = materials[i].GetColor("_BaseColor");
                    color.a = alpha;
                    materials[i].SetColor("_BaseColor", color);
                }
                else if (materials[i].HasProperty("_Color"))
                {
                    Color color = materials[i].GetColor("_Color");
                    color.a = alpha;
                    materials[i].SetColor("_Color", color);
                }
            }
        }
    }

    private void SetInsidesAlpha(float alpha)
    {
        foreach (var kvp in insidesMaterialsDict)
        {
            Material[] materials = kvp.Value;
            
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null) continue;
                
                
                if (materials[i].HasProperty("_Alpha"))
                {
                    materials[i].SetFloat("_Alpha", alpha);
                    Debug.Log($"Установлена _Alpha = {alpha} для {materials[i].name}");
                }
            }
        }
    }

    public void SetFadeDuration(float newDuration)
    {
        fadeDuration = Mathf.Max(0.1f, newDuration);
    }

    public bool IsOutsidesVisible()
    {
        return isOutsidesVisible;
    }

    private void OnDestroy()
    {
        foreach (var kvp in outsidesMaterialsDict)
        {
            foreach (Material mat in kvp.Value)
            {
                if (mat != null)
                    Destroy(mat);
            }
        }
        
        foreach (var kvp in insidesMaterialsDict)
        {
            foreach (Material mat in kvp.Value)
            {
                if (mat != null)
                    Destroy(mat);
            }
        }
    }
}