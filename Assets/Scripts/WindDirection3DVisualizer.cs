using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class WindDirection3DVisualizer : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("References")]
    [SerializeField] private ShipController shipController;
    
    [Header("Visualization")]
    [SerializeField] private RectTransform sphereContainer;
    [SerializeField] private RectTransform directionIndicator;
    [SerializeField] private Image sphereBackground;
    [SerializeField] private Image indicatorImage;
    [SerializeField] private Color sphereColor = new Color(0.2f, 0.4f, 0.8f, 0.3f);
    [SerializeField] private Color indicatorColor = Color.cyan;
    [SerializeField] private float sphereRadius = 100f;
    
    [Header("Direction Labels (Optional)")]
    [SerializeField] private bool showDirectionLabels = true;
    [SerializeField] private TextMeshProUGUI labelNorth;
    [SerializeField] private TextMeshProUGUI labelSouth;
    [SerializeField] private TextMeshProUGUI labelEast;
    [SerializeField] private TextMeshProUGUI labelWest;
    [SerializeField] private Color labelColor = Color.white;
    [SerializeField] private int labelFontSize = 14;
    
    [Header("Helper Lines (Optional)")]
    [SerializeField] private bool showHelperLines = true;
    [SerializeField] private Image horizontalLine;
    [SerializeField] private Image verticalLine;
    [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.3f);
    [SerializeField] private float lineWidth = 2f;
    
    [Header("3D Arrow Visualization (Optional)")]
    [SerializeField] private GameObject arrow3D;
    [SerializeField] private bool show3DArrow = true;
    [SerializeField] private float arrowLength = 2f;
    
    [Header("Display Text")]
    [SerializeField] private TextMeshProUGUI directionText;
    [SerializeField] private TextMeshProUGUI componentsText;
    [SerializeField] private bool show3DComponents = true;
    
    private Vector2 currentDirection = Vector2.zero;
    private bool isDragging = false;
    private Camera uiCamera;
    private Canvas parentCanvas;
    private bool initialized = false;
    
    [ContextMenu("Auto Setup Components")]
    public void AutoSetupComponents()
    {
        AutoFindComponents();
        InitializeVisualization();
        initialized = true;
        Debug.Log("WindDirection3DVisualizer: автоматическая настройка завершена");
    }
    
    private void Start()
    {
        AutoFindComponents();
        InitializeVisualization();
    }
    
    private void AutoFindComponents()
    {
        if (shipController == null)
        {
            shipController = FindObjectOfType<ShipController>();
        }
        
        if (shipController == null)
        {
            Debug.LogError("WindDirection3DVisualizer: shipController не найден");
            enabled = false;
            return;
        }
        
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            uiCamera = parentCanvas.worldCamera;
        }
        
        if (sphereContainer == null)
        {
            sphereContainer = GetComponent<RectTransform>();
        }
        
        if (sphereBackground == null)
        {
            sphereBackground = GetComponent<Image>();
            if (sphereBackground == null)
            {
                sphereBackground = GetComponentInChildren<Image>();
            }
        }
        
        if (directionIndicator == null)
        {
            Transform indicatorTransform = transform.Find("DirectionIndicator");
            if (indicatorTransform == null)
            {
                Image[] images = GetComponentsInChildren<Image>();
                foreach (Image img in images)
                {
                    if (img != sphereBackground && img.gameObject.name.Contains("Indicator"))
                    {
                        directionIndicator = img.rectTransform;
                        indicatorImage = img;
                        break;
                    }
                }
            }
            else
            {
                directionIndicator = indicatorTransform.GetComponent<RectTransform>();
                indicatorImage = indicatorTransform.GetComponent<Image>();
            }
        }
        
        if (indicatorImage == null && directionIndicator != null)
        {
            indicatorImage = directionIndicator.GetComponent<Image>();
        }
        
        if (directionText == null)
        {
            directionText = GetComponentInChildren<TextMeshProUGUI>();
        }
        
        if (sphereBackground != null && !sphereBackground.gameObject.name.Contains("Indicator"))
        {
            if (directionIndicator != null && directionIndicator.GetComponent<Image>() == sphereBackground)
            {
                sphereBackground = null;
            }
        }
    }
    
    private void InitializeVisualization()
    {
        if (sphereBackground != null)
        {
            sphereBackground.color = sphereColor;
            if (sphereContainer == null)
            {
                sphereContainer = sphereBackground.rectTransform;
            }
        }
        
        if (indicatorImage != null)
        {
            indicatorImage.color = indicatorColor;
            if (directionIndicator == null)
            {
                directionIndicator = indicatorImage.rectTransform;
            }
        }
        
        if (directionText == null)
        {
            GameObject textObj = new GameObject("DirectionText");
            textObj.transform.SetParent(transform, false);
            directionText = textObj.AddComponent<TextMeshProUGUI>();
            directionText.text = "Компас: Горизонтальное направление\n(Управляйте ползунком для вертикального угла)";
            directionText.fontSize = 13;
            directionText.alignment = TextAlignmentOptions.Center;
            directionText.color = Color.white;
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0f);
            textRect.anchorMax = new Vector2(0.5f, 0f);
            textRect.pivot = new Vector2(0.5f, 0f);
            textRect.anchoredPosition = new Vector2(0f, -130f);
            textRect.sizeDelta = new Vector2(300f, 50f);
        }
        
        if (show3DComponents && componentsText == null)
        {
            GameObject compObj = new GameObject("ComponentsText");
            compObj.transform.SetParent(transform, false);
            componentsText = compObj.AddComponent<TextMeshProUGUI>();
            componentsText.text = "3D Вектор:\nX = 0.00 (влево/вправо)\nY = 0.00 (вверх/вниз)\nZ = 1.00 (вперед/назад)";
            componentsText.fontSize = 11;
            componentsText.alignment = TextAlignmentOptions.Left;
            componentsText.color = new Color(0.8f, 0.8f, 1f);
            RectTransform compRect = compObj.GetComponent<RectTransform>();
            compRect.anchorMin = new Vector2(0f, 0f);
            compRect.anchorMax = new Vector2(0f, 0f);
            compRect.pivot = new Vector2(0f, 0f);
            compRect.anchoredPosition = new Vector2(-sphereRadius - 40f, -sphereRadius - 50f);
            compRect.sizeDelta = new Vector2(200f, 80f);
        }
        
        if (sphereContainer != null && directionIndicator != null)
        {
            if (sphereRadius <= 0 || sphereRadius > sphereContainer.rect.width / 2)
            {
                sphereRadius = Mathf.Min(sphereContainer.rect.width, sphereContainer.rect.height) / 2 - 10f;
            }
        }
        
        CreateDirectionLabels();
        CreateHelperLines();
        UpdateVisualization();
    }
    
    private void CreateDirectionLabels()
    {
        if (!showDirectionLabels || sphereContainer == null) return;
        
        if (labelNorth == null)
        {
            labelNorth = CreateLabel("N", new Vector2(0f, sphereRadius + 20f));
            labelNorth.text = "СЕВЕР (Z+)\n(0°)";
        }
        
        if (labelSouth == null)
        {
            labelSouth = CreateLabel("S", new Vector2(0f, -sphereRadius - 20f));
            labelSouth.text = "ЮГ (Z-)\n(180°)";
        }
        
        if (labelEast == null)
        {
            labelEast = CreateLabel("E", new Vector2(sphereRadius + 20f, 0f));
            labelEast.text = "ВОСТОК (X+)\n(90°)";
        }
        
        if (labelWest == null)
        {
            labelWest = CreateLabel("W", new Vector2(-sphereRadius - 20f, 0f));
            labelWest.text = "ЗАПАД (X-)\n(270°)";
        }
        
    }
    
    private TextMeshProUGUI CreateLabel(string name, Vector2 position)
    {
        GameObject labelObj = new GameObject(name);
        labelObj.transform.SetParent(sphereContainer, false);
        TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
        label.text = name;
        label.fontSize = labelFontSize;
        label.color = labelColor;
        label.alignment = TextAlignmentOptions.Center;
        RectTransform rect = labelObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(60f, 30f);
        return label;
    }
    
    private void CreateHelperLines()
    {
        if (!showHelperLines || sphereContainer == null) return;
        
        if (horizontalLine == null)
        {
            horizontalLine = CreateLine("HorizontalLine", new Vector2(sphereRadius * 2, lineWidth), Vector2.zero);
        }
        
        if (verticalLine == null)
        {
            verticalLine = CreateLine("VerticalLine", new Vector2(lineWidth, sphereRadius * 2), Vector2.zero);
        }
    }
    
    private Image CreateLine(string name, Vector2 size, Vector2 position)
    {
        GameObject lineObj = new GameObject(name);
        lineObj.transform.SetParent(sphereContainer, false);
        Image line = lineObj.AddComponent<Image>();
        line.color = lineColor;
        RectTransform rect = lineObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return line;
    }
    
    private void Update()
    {
        if (!isDragging && shipController != null)
        {
            float x, z;
            shipController.GetWindHorizontalXZ(out x, out z);
            
            Vector2 newDirection = new Vector2(x, z);
            if (Vector2.Distance(currentDirection, newDirection) > 0.01f)
            {
                currentDirection = newDirection;
                UpdateVisualization();
            }
        }
        
        if (show3DArrow && arrow3D != null && shipController != null)
        {
            Update3DArrow();
        }
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        isDragging = true;
        HandlePointerEvent(eventData);
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (isDragging)
        {
            HandlePointerEvent(eventData);
        }
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
    }
    
    private void HandlePointerEvent(PointerEventData eventData)
    {
        if (sphereContainer == null) return;
        
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            sphereContainer, 
            eventData.position, 
            uiCamera, 
            out localPoint
        );
        
        Vector2 center = sphereContainer.rect.center;
        Vector2 offset = localPoint - center;
        
        float clampedX = Mathf.Clamp(offset.x, -sphereRadius, sphereRadius);
        float clampedZ = Mathf.Clamp(offset.y, -sphereRadius, sphereRadius);
        
        float normalizedX = clampedX / sphereRadius;
        float normalizedZ = clampedZ / sphereRadius;
        
        float horizontalAngle = 0f;
        if (Mathf.Abs(normalizedX) > 0.01f || Mathf.Abs(normalizedZ) > 0.01f)
        {
            horizontalAngle = Mathf.Atan2(normalizedX, normalizedZ) * Mathf.Rad2Deg;
            horizontalAngle = (horizontalAngle + 360f) % 360f;
        }
        
        float horizontalStrength = Mathf.Clamp01(Mathf.Sqrt(normalizedX * normalizedX + normalizedZ * normalizedZ));
        
        if (shipController != null)
        {
            shipController.SetWindHorizontalXZ(normalizedX, normalizedZ);
        }
        
        currentDirection = new Vector2(normalizedX, normalizedZ);
        UpdateVisualization();
    }
    
    private void UpdateVisualization()
    {
        if (shipController != null)
        {
            float x, z;
            shipController.GetWindHorizontalXZ(out x, out z);
            
            currentDirection = new Vector2(x, z);
            
            if (directionIndicator != null && sphereContainer != null)
            {
                Vector2 position = new Vector2(x * sphereRadius, z * sphereRadius);
                directionIndicator.anchoredPosition = position;
            }
            
            float horizontalAngle = Mathf.Atan2(x, z) * Mathf.Rad2Deg;
            horizontalAngle = (horizontalAngle + 360f) % 360f;
            
            if (directionText != null)
            {
                string description = "";
                if (horizontalAngle >= 337.5f || horizontalAngle < 22.5f) description = "Дует с СЕВЕРА";
                else if (horizontalAngle >= 22.5f && horizontalAngle < 67.5f) description = "Дует с СЕВЕРО-ВОСТОКА";
                else if (horizontalAngle >= 67.5f && horizontalAngle < 112.5f) description = "Дует с ВОСТОКА";
                else if (horizontalAngle >= 112.5f && horizontalAngle < 157.5f) description = "Дует с ЮГО-ВОСТОКА";
                else if (horizontalAngle >= 157.5f && horizontalAngle < 202.5f) description = "Дует с ЮГА";
                else if (horizontalAngle >= 202.5f && horizontalAngle < 247.5f) description = "Дует с ЮГО-ЗАПАДА";
                else if (horizontalAngle >= 247.5f && horizontalAngle < 292.5f) description = "Дует с ЗАПАДА";
                else description = "Дует с СЕВЕРО-ЗАПАДА";
                
                directionText.text = $"{description}\n" +
                                   $"Горизонтальный угол: {horizontalAngle:F0}°";
                
                if (show3DComponents && componentsText != null)
                {
                    float verticalStrength = shipController.GetWindVerticalStrength();
                    
                    float windX = x;
                    float windZ = z;
                    float windY = verticalStrength;
                    
                    string xDesc = windX > 0 ? "→ ВОСТОК (X+)" : windX < 0 ? "← ЗАПАД (X-)" : "";
                    string yDesc = windY > 0 ? "↑ ВВЕРХ (Y+)" : windY < 0 ? "↓ ВНИЗ (Y-)" : "";
                    string zDesc = windZ > 0 ? "→ СЕВЕР (Z+)" : windZ < 0 ? "← ЮГ (Z-)" : "";
                    
                    componentsText.text = $"3D ВЕКТОР ВЕТРА (ГЛОБАЛЬНЫЙ):\n" +
                                         $"X = {windX:F2} {xDesc}\n" +
                                         $"Y = {windY:F2} {yDesc}\n" +
                                         $"Z = {windZ:F2} {zDesc}\n" +
                                         $"\n(Независимо от ориентации корабля)";
                }
            }
        }
    }
    
    private void Update3DArrow()
    {
        if (arrow3D == null || shipController == null) return;
        
        float x, z;
        shipController.GetWindHorizontalXZ(out x, out z);
        float verticalStrength = shipController.GetWindVerticalStrength();
        
        Vector3 windDirectionWorld = new Vector3(x, verticalStrength, z);
        if (windDirectionWorld.magnitude > 0.01f)
        {
            windDirectionWorld = windDirectionWorld.normalized;
        }
        
        arrow3D.transform.position = shipController.transform.position;
        arrow3D.transform.rotation = Quaternion.LookRotation(-windDirectionWorld);
        arrow3D.transform.localScale = Vector3.one * arrowLength;
        
        if (!arrow3D.activeSelf)
        {
            arrow3D.SetActive(true);
        }
    }
    
    private void OnValidate()
    {
        if (sphereBackground != null)
        {
            sphereBackground.color = sphereColor;
        }
        
        if (indicatorImage != null)
        {
            indicatorImage.color = indicatorColor;
        }
    }
}
