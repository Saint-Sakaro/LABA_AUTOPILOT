using UnityEngine;
using TMPro;

/// <summary>
/// UI для отображения информации о корабле: скорость и тяга двигателей
/// </summary>
public class ShipUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShipController shipController;
    
    [Header("Speed Display")]
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private string speedFormat = "Speed: {0:F1} m/s";
    
    [Header("Engine Thrust Display")]
    [SerializeField] private TextMeshProUGUI[] engineThrustTexts = new TextMeshProUGUI[4];
    [SerializeField] private string thrustFormat = "Engine {0}: {1:P0}"; // P0 = процент без десятичных
    
    [Header("Update Settings")]
    [SerializeField] private float updateInterval = 0.1f; // Обновлять UI каждые 0.1 секунды
    
    private float updateTimer = 0f;
    
    private void Start()
    {
        // Автоматически находим ShipController, если не назначен
        if (shipController == null)
        {
            shipController = FindObjectOfType<ShipController>();
        }
        
        if (shipController == null)
        {
            Debug.LogError("ShipUI: ShipController не найден! Назначьте его в Inspector.");
            enabled = false;
            return;
        }
        
        // Проверяем наличие текстовых элементов
        if (speedText == null)
        {
            Debug.LogWarning("ShipUI: Speed Text не назначен!");
        }
        
        // Проверяем тексты для двигателей
        for (int i = 0; i < engineThrustTexts.Length; i++)
        {
            if (engineThrustTexts[i] == null)
            {
                Debug.LogWarning($"ShipUI: Engine {i + 1} Text не назначен!");
            }
        }
    }
    
    private void Update()
    {
        updateTimer += Time.deltaTime;
        
        // Обновляем UI с заданным интервалом
        if (updateTimer >= updateInterval)
        {
            UpdateUI();
            updateTimer = 0f;
        }
    }
    
    /// <summary>
    /// Обновляет все элементы UI
    /// </summary>
    private void UpdateUI()
    {
        if (shipController == null) return;
        
        // Обновляем скорость
        UpdateSpeed();
        
        // Обновляем тягу двигателей
        UpdateEngineThrusts();
    }
    
    /// <summary>
    /// Обновляет отображение скорости
    /// </summary>
    private void UpdateSpeed()
    {
        if (speedText == null) return;
        
        float speed = shipController.GetSpeed();
        speedText.text = string.Format(speedFormat, speed);
    }
    
    /// <summary>
    /// Обновляет отображение тяги каждого двигателя
    /// </summary>
    private void UpdateEngineThrusts()
    {
        for (int i = 0; i < engineThrustTexts.Length; i++)
        {
            if (engineThrustTexts[i] == null) continue;
            
            // Получаем тягу двигателя через ShipController
            // Нужно добавить метод GetEngineThrust в ShipController
            float thrust = shipController.GetEngineThrust(i);
            engineThrustTexts[i].text = string.Format(thrustFormat, i + 1, thrust);
        }
    }
}


