using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;




public class ShipUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ShipController shipController;
    [SerializeField] private FuelManager fuelManager;

    [Header("Speed Display")]
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private string speedFormat = "Speed: {0:F1} m/s";

    [Header("Engine Thrust Display")]
    [SerializeField] private TextMeshProUGUI[] engineThrustTexts = new TextMeshProUGUI[4];
    [SerializeField] private string thrustFormat = "Engine {0}: {1:P0}";

    [Header("Center of Mass Control")]
    [SerializeField] private Slider centerOfMassSlider;
    [SerializeField] private TextMeshProUGUI centerOfMassText;
    [SerializeField] private string centerOfMassFormat = "Center of Mass: {0:F2} m";
    [SerializeField] private bool showCenterOfMassControl = true;

    [Header("Wind Control")]
    [SerializeField] private Slider windStrengthSlider;
    [SerializeField] private WindDirection3DVisualizer windDirection3DVisualizer;
    [SerializeField] private Slider windDirectionHorizontalSlider;
    [SerializeField] private Slider windDirectionVerticalSlider;
    [SerializeField] private TextMeshProUGUI windStrengthText;
    [SerializeField] private TextMeshProUGUI windDirectionHorizontalText;
    [SerializeField] private TextMeshProUGUI windDirectionVerticalText;
    [SerializeField] private string windStrengthFormat = "Wind Strength: {0:F0} N";
    [SerializeField] private string windDirectionHorizontalFormat = "Wind Horizontal: {0:F0}°";
    [SerializeField] private string windDirectionVerticalFormat = "Вертикальный угол: {0:F0}° (↑ вверх / ↓ вниз)";
    [SerializeField] private bool showWindControl = true;
    [SerializeField] private bool use3DWindVisualizer = true;

    [Header("Fuel Display")]
    [SerializeField] private bool showFuelDisplay = true;
    [SerializeField] private TextMeshProUGUI fuelText;
    [SerializeField] private string fuelFormat = "Топливо: {0:F1}/{1:F1} л ({2:P0})";
    [SerializeField] private Color normalFuelColor = Color.green;
    [SerializeField] private Color lowFuelColor = Color.yellow;
    [SerializeField] private Color criticalFuelColor = Color.red;
    
    [Header("Landing Radar Display")]
    [SerializeField] private bool showRadarDisplay = true;
    [SerializeField] private LandingRadar landingRadar;
    [SerializeField] private TextMeshProUGUI radarStatusText;
    [SerializeField] private TextMeshProUGUI radarBestSiteText;
    [SerializeField] private string radarStatusFormat = "Радар: {0} площадок";
    [SerializeField] private string radarBestSiteFormat = "Лучшая: {0:F0}м, {1:F0}%, {2:F1}°";
    [SerializeField] private Color radarGoodColor = Color.green;
    [SerializeField] private Color radarWarningColor = Color.yellow;
    
    [Header("Update Settings")]
    [SerializeField] private float updateInterval = 0.1f;

    private float updateTimer = 0f;

    private void Start()
    {

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
        
        // Находим FuelManager
        if (fuelManager == null)
        {
            fuelManager = FindObjectOfType<FuelManager>();
        }
        
        if (fuelManager == null && showFuelDisplay)
        {
            Debug.LogWarning("ShipUI: FuelManager не найден! UI топлива не будет отображаться.");
        }
        
        // Находим LandingRadar
        if (landingRadar == null && showRadarDisplay)
        {
            landingRadar = FindObjectOfType<LandingRadar>();
        }
        
        if (landingRadar == null && showRadarDisplay)
        {
            Debug.LogWarning("ShipUI: LandingRadar не найден! UI радара не будет отображаться.");
        }
        else if (landingRadar != null && showRadarDisplay)
        {
            // Подписываемся на обновления радара
            landingRadar.OnSitesUpdated += OnRadarSitesUpdated;
        }


        if (speedText == null)
        {
            Debug.LogWarning("ShipUI: Speed Text не назначен!");
        }


        for (int i = 0; i < engineThrustTexts.Length; i++)
        {
            if (engineThrustTexts[i] == null)
            {
                Debug.LogWarning($"ShipUI: Engine {i + 1} Text не назначен!");
            }
        }


        if (showCenterOfMassControl && centerOfMassSlider != null)
        {

            float maxOffset = shipController.GetMaxCenterOfMassOffset();
            centerOfMassSlider.minValue = -maxOffset;
            centerOfMassSlider.maxValue = maxOffset;
            centerOfMassSlider.value = shipController.GetCenterOfMassOffset();


            centerOfMassSlider.onValueChanged.AddListener(OnCenterOfMassSliderChanged);


            UpdateCenterOfMassText();
        }
        else if (showCenterOfMassControl && centerOfMassSlider == null)
        {
            Debug.LogWarning("ShipUI: Center of Mass Slider не назначен, но showCenterOfMassControl включен!");
        }


        if (showWindControl)
        {

            if (windStrengthSlider != null)
            {
                float maxWindStrength = shipController.GetMaxWindStrength();
                windStrengthSlider.minValue = 0f;
                windStrengthSlider.maxValue = maxWindStrength;
                windStrengthSlider.value = shipController.GetWindStrength();
                windStrengthSlider.onValueChanged.AddListener(OnWindStrengthSliderChanged);
                UpdateWindStrengthText();
            }
            else
            {
                Debug.LogWarning("ShipUI: Wind Strength Slider не назначен, но showWindControl включен!");
            }

            if (use3DWindVisualizer && windDirection3DVisualizer != null)
            {
                if (windDirectionHorizontalSlider != null)
                {
                    windDirectionHorizontalSlider.gameObject.SetActive(false);
                }
                if (windDirectionHorizontalText != null)
                {
                    windDirectionHorizontalText.gameObject.SetActive(false);
                }
                
                if (windDirectionVerticalSlider != null)
                {
                    windDirectionVerticalSlider.minValue = -90f;
                    windDirectionVerticalSlider.maxValue = 90f;
                    float currentStrength = shipController.GetWindVerticalStrength();
                    windDirectionVerticalSlider.value = currentStrength * 90f;
                    windDirectionVerticalSlider.onValueChanged.AddListener(OnWindDirectionVerticalSliderChanged);
                    windDirectionVerticalSlider.gameObject.SetActive(true);
                    UpdateWindDirectionVerticalText();
                }
                else
                {
                    Debug.LogWarning("ShipUI: Wind Direction Vertical Slider не назначен для вертикального компонента!");
                }
                
                if (windDirectionVerticalText != null)
                {
                    windDirectionVerticalText.gameObject.SetActive(true);
                }
            }
            else
            {
                if (windDirectionHorizontalSlider != null)
                {
                    windDirectionHorizontalSlider.minValue = 0f;
                    windDirectionHorizontalSlider.maxValue = 360f;
                    windDirectionHorizontalSlider.value = shipController.GetWindDirectionHorizontal();
                    windDirectionHorizontalSlider.onValueChanged.AddListener(OnWindDirectionHorizontalSliderChanged);
                    windDirectionHorizontalSlider.gameObject.SetActive(true);
                    UpdateWindDirectionHorizontalText();
                }
                else
                {
                    Debug.LogWarning("ShipUI: Wind Direction Horizontal Slider не назначен, но showWindControl включен!");
                }

                if (windDirectionVerticalSlider != null)
                {
                    windDirectionVerticalSlider.minValue = -90f;
                    windDirectionVerticalSlider.maxValue = 90f;
                    // Преобразуем силу (-1 до +1) в угол для слайдера
                    float currentStrength = shipController.GetWindVerticalStrength();
                    windDirectionVerticalSlider.value = currentStrength * 90f;
                    windDirectionVerticalSlider.onValueChanged.AddListener(OnWindDirectionVerticalSliderChanged);
                    windDirectionVerticalSlider.gameObject.SetActive(true);
                    UpdateWindDirectionVerticalText();
                }
                else
                {
                    Debug.LogWarning("ShipUI: Wind Direction Vertical Slider не назначен, но showWindControl включен!");
                }
                
                if (windDirectionHorizontalText != null)
                {
                    windDirectionHorizontalText.gameObject.SetActive(true);
                }
                if (windDirectionVerticalText != null)
                {
                    windDirectionVerticalText.gameObject.SetActive(true);
                }
            }
        }
    }

    private void Update()
    {
        updateTimer += Time.deltaTime;


        if (updateTimer >= updateInterval)
        {
            UpdateUI();
            updateTimer = 0f;
        }
    }




    private void UpdateUI()
    {
        if (shipController == null) return;


        UpdateSpeed();


        UpdateEngineThrusts();


        if (showCenterOfMassControl && centerOfMassText != null && (centerOfMassSlider == null || !centerOfMassSlider.gameObject.activeInHierarchy))
        {
            UpdateCenterOfMassText();
        }


        if (showWindControl)
        {
            UpdateWindStrengthText();
            UpdateWindDirectionHorizontalText();
            UpdateWindDirectionVerticalText();
        }
        
        // Обновляем отображение топлива
        if (showFuelDisplay)
        {
            UpdateFuelDisplay();
        }
        
        // Обновляем отображение радара
        if (showRadarDisplay)
        {
            UpdateRadarDisplay();
        }
    }




    private void UpdateSpeed()
    {
        if (speedText == null) return;

        float speed = shipController.GetSpeed();
        speedText.text = string.Format(speedFormat, speed);
    }




    private void UpdateEngineThrusts()
    {
        for (int i = 0; i < engineThrustTexts.Length; i++)
        {
            if (engineThrustTexts[i] == null) continue;



            float thrust = shipController.GetEngineThrust(i);
            engineThrustTexts[i].text = string.Format(thrustFormat, i + 1, thrust);
        }
    }




    private void UpdateCenterOfMassText()
    {
        if (centerOfMassText == null || shipController == null) return;

        float offset = shipController.GetCenterOfMassOffset();
        centerOfMassText.text = string.Format(centerOfMassFormat, offset);
    }




    private void OnCenterOfMassSliderChanged(float value)
    {
        if (shipController == null) return;


        shipController.SetCenterOfMassOffset(value);


        UpdateCenterOfMassText();
    }




    private void UpdateWindStrengthText()
    {
        if (windStrengthText == null || shipController == null) return;

        float strength = shipController.GetWindStrength();
        windStrengthText.text = string.Format(windStrengthFormat, strength);
    }




    private void UpdateWindDirectionHorizontalText()
    {
        if (windDirectionHorizontalText == null || shipController == null) return;

        float direction = shipController.GetWindDirectionHorizontal();
        windDirectionHorizontalText.text = string.Format(windDirectionHorizontalFormat, direction);
    }




    private void UpdateWindDirectionVerticalText()
    {
        if (windDirectionVerticalText == null || shipController == null) return;

        float strength = shipController.GetWindVerticalStrength();
        // Преобразуем силу (-1 до +1) в угол для отображения
        float angle = strength * 90f;
        windDirectionVerticalText.text = $"Вертикальная сила: {strength:F2} ({angle:F0}°)\n(↑ вверх / ↓ вниз)";
    }




    private void OnWindStrengthSliderChanged(float value)
    {
        if (shipController == null)
        {
            Debug.LogWarning("ShipUI: shipController is null in OnWindStrengthSliderChanged!");
            return;
        }

        shipController.SetWindStrength(value);
        UpdateWindStrengthText();


        Debug.Log($"Wind Strength изменен на: {value:F1}N");
    }




    private void OnWindDirectionHorizontalSliderChanged(float value)
    {
        if (shipController == null)
        {
            Debug.LogWarning("ShipUI: shipController is null in OnWindDirectionHorizontalSliderChanged!");
            return;
        }

        shipController.SetWindDirectionHorizontal(value);
        UpdateWindDirectionHorizontalText();


        Debug.Log($"Wind Horizontal Direction изменен на: {value:F1}°");
    }




    private void OnWindDirectionVerticalSliderChanged(float value)
    {
        if (shipController == null)
        {
            Debug.LogWarning("ShipUI: shipController is null in OnWindDirectionVerticalSliderChanged!");
            return;
        }

        // Преобразуем угол (-90 до +90) в силу (-1 до +1)
        float strength = value / 90f; // -90° = -1, 0° = 0, +90° = +1
        shipController.SetWindVerticalStrength(strength);
        UpdateWindDirectionVerticalText();

        Debug.Log($"Wind Vertical Strength изменен на: {strength:F2} (из угла {value:F1}°)");
    }




    private void UpdateFuelDisplay()
    {
        if (fuelManager == null) return;
        
        float totalFuel = fuelManager.GetTotalFuel();
        float totalMaxFuel = fuelManager.GetTotalMaxFuel();
        float fuelPercentage = fuelManager.GetFuelPercentage();
        
        // Обновляем текст
        if (fuelText != null)
        {
            fuelText.text = string.Format(fuelFormat, totalFuel, totalMaxFuel, fuelPercentage);
            
            // Меняем цвет текста в зависимости от уровня топлива
            if (fuelPercentage <= 0.1f) // Критический уровень
            {
                fuelText.color = criticalFuelColor;
            }
            else if (fuelPercentage <= 0.2f) // Низкий уровень
            {
                fuelText.color = lowFuelColor;
            }
            else
            {
                fuelText.color = normalFuelColor;
            }
        }
    }

    /// <summary>
    /// Обновляет отображение информации о радаре
    /// </summary>
    private void UpdateRadarDisplay()
    {
        if (landingRadar == null) return;
        
        List<LandingSite> sites = landingRadar.GetFoundSites();
        
        // Обновляем статус радара
        if (radarStatusText != null)
        {
            radarStatusText.text = string.Format(radarStatusFormat, sites.Count);
            radarStatusText.color = sites.Count > 0 ? radarGoodColor : radarWarningColor;
        }
        
        // Обновляем информацию о лучшей площадке
        if (radarBestSiteText != null)
        {
            if (sites.Count > 0)
            {
                LandingSite bestSite = sites[0];
                radarBestSiteText.text = string.Format(radarBestSiteFormat, 
                    bestSite.distanceFromShip, 
                    bestSite.suitabilityScore * 100f, 
                    bestSite.slopeAngle);
                radarBestSiteText.color = bestSite.suitabilityScore >= 0.7f ? radarGoodColor : radarWarningColor;
            }
            else
            {
                radarBestSiteText.text = "Площадки не найдены";
                radarBestSiteText.color = radarWarningColor;
            }
        }
    }
    
    /// <summary>
    /// Обработчик события обновления площадок радара
    /// </summary>
    private void OnRadarSitesUpdated(List<LandingSite> sites)
    {
        // Обновляем UI сразу при получении новых данных
        UpdateRadarDisplay();
    }
    
    private void OnDestroy()
    {
        if (centerOfMassSlider != null)
        {
            centerOfMassSlider.onValueChanged.RemoveListener(OnCenterOfMassSliderChanged);
        }

        if (windStrengthSlider != null)
        {
            windStrengthSlider.onValueChanged.RemoveListener(OnWindStrengthSliderChanged);
        }

        if (windDirectionHorizontalSlider != null)
        {
            windDirectionHorizontalSlider.onValueChanged.RemoveListener(OnWindDirectionHorizontalSliderChanged);
        }

        if (windDirectionVerticalSlider != null)
        {
            windDirectionVerticalSlider.onValueChanged.RemoveListener(OnWindDirectionVerticalSliderChanged);
        }
        
        // Отписываемся от событий радара
        if (landingRadar != null)
        {
            landingRadar.OnSitesUpdated -= OnRadarSitesUpdated;
        }
        
        // Отписываемся от событий FuelManager (если была подписка)
        // Примечание: В текущей реализации используется периодическое обновление через UpdateFuelDisplay()
        // вместо подписки на события, поэтому отписка не требуется
    }
}


