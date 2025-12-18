//  3. AdvancedEngineController.cs (Продвинутое управление с VFX Graph поддержкой)

using UnityEngine;
#if UNITY_2019_3_OR_NEWER
using UnityEngine.VFX;
#endif

/// <summary>
/// Расширенный контроллер двигателя с поддержкой VFX Graph
/// </summary>
public class AdvancedEngineController : MonoBehaviour
{
    [Header("Particle System")]
    [SerializeField] private ParticleSystem fireParticles;
    [SerializeField] private ParticleSystem smokeParticles;
    
    #if UNITY_2019_3_OR_NEWER
    [Header("VFX Graph")]
    [SerializeField] private VisualEffect vfxGraph;
    #endif
    
    [Header("Engine Settings")]
    [SerializeField] private float currentThrust = 0.3f;
    [SerializeField] private float thrustLerpSpeed = 5f; // Плавность переходов
    
    [Header("Audio")]
    [SerializeField] private AudioSource engineSound;
    [SerializeField] private AnimationCurve engineVolume = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    private float targetThrust;
    private EngineFireController fireController;
    
    private void Start()
    {
        targetThrust = currentThrust;
        fireController = GetComponent<EngineFireController>();
        
        if (fireParticles == null)
        {
            fireParticles = GetComponentInChildren<ParticleSystem>();
        }
    }
    
    private void Update()
    {
        // Плавный переход к целевой мощности
        currentThrust = Mathf.Lerp(currentThrust, targetThrust, thrustLerpSpeed * Time.deltaTime);
        
        if (fireController != null)
        {
            fireController.SetThrust(currentThrust);
        }
        
        UpdateVFXGraph();
        UpdateEngineAudio();
    }
    
    public void SetTargetThrust(float thrust)
    {
        targetThrust = Mathf.Clamp01(thrust);
    }
    
    public float GetCurrentThrust()
    {
        return currentThrust;
    }
    
    private void UpdateVFXGraph()
    {
        #if UNITY_2019_3_OR_NEWER
        if (vfxGraph != null)
        {
            // Отправляем параметры в VFX Graph
            vfxGraph.SetFloat("Thrust", currentThrust);
            vfxGraph.SetFloat("FireIntensity", currentThrust);
        }
        #endif
    }
    
    private void UpdateEngineAudio()
    {
        if (engineSound == null) return;
        
        float volumeValue = engineVolume.Evaluate(currentThrust);
        engineSound.volume = volumeValue;
        
        // Увеличиваем pitch с увеличением мощности
        engineSound.pitch = Mathf.Lerp(0.8f, 1.2f, currentThrust);
    }
}

//  4. ParticleSystemSetup.txt (Инструкция по настройке Particle System)

// ИНСТРУКЦИЯ ПО НАСТРОЙКЕ PARTICLE SYSTEM ДЛЯ ОГНЯ ДВИГАТЕЛЯ

// 1. СОЗДАНИЕ PARTICLE SYSTEM
//    - Правый клик в Hierarchy → Effects → Particle System
//    - Назови его "Engine_Fire"

// 2. ОСНОВНЫЕ ПАРАМЕТРЫ (Main)
//    ✓ Duration: 1
//    ✓ Looping: ON
//    ✓ Start Lifetime: 0.5 - 1.5
//    ✓ Start Speed: 8 - 15
//    ✓ Start Size: 1 - 2
//    ✓ Gravity Modifier: 0
//    ✓ Simulation Space: World
//    ✓ Play On Awake: ON
//    ✓ Max Particles: 500

// 3. EMISSION
//    ✓ Rate over Time: 50 (будет менялся скриптом)
//    ✓ Bursts: 0

// 4. SHAPE
//    ✓ Shape: Cone
//    ✓ Angle: 15-25 градусов
//    ✓ Radius: 0.2 - 0.3
//    ✓ Radius Thickness: 0
//    ✓ Emit From: Shell
//    ✓ Direction: Align with Shape - ON

// 5. VELOCITY OVER LIFETIME
//    ✓ X: 0
//    ✓ Y: 0.5 - 1 (немного вверх)
//    ✓ Z: 0
//    ✓ Speed Modifier: 1

// 6. SIZE OVER LIFETIME
//    ✓ Enabled: ON
//    ✓ Size: Убывающая кривая (начинаем с 1, заканчиваем с 0.3)

// 7. COLOR OVER LIFETIME
//    ✓ Enabled: ON
//    ✓ Color: Градиент от оранжевого → жёлтому → прозрачному

// 8. RENDERER
//    ✓ Material: Default-Particle (или свой огненный материал)
//    ✓ Render Mode: Billboard
//    ✓ Alignment: View

// ДОПОЛНИТЕЛЬНО ДЛЯ РЕАЛИЗМА:
//    - Создай второй Particle System для дыма (более слабый, серый)
//    - Используй шейдер для более реалистичного огня
// ```

// ## 5. ShaderExample.shadergraph (Примечание)

// Для максимально реалистичного огня, используй Shader Graph с:
// - Пан текстуры (движущиеся UV)
// - Noise для турбулентности
// - Граниент цветов от чёрного → красный → жёлтый → белый

// Рекомендуемые туториалы в YouTube:
// - "Unity Shader Graph Fire" - создание шейдера огня
// - "VFX Graph Tutorial" - использование VFX Graph
