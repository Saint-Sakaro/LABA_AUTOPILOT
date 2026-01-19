using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Менеджер топлива - управляет расходом топлива в зависимости от работы двигателей
/// </summary>
public class FuelManager : MonoBehaviour
{
    [Header("Fuel Settings")]
    [SerializeField] private float baseConsumptionRate = 10f; // Базовый расход топлива (л/сек) при 100% тяги
    [SerializeField] private float minConsumptionRate = 0.5f; // Минимальный расход при холостом ходу (л/сек)
    [SerializeField] private float consumptionMultiplier = 1f; // Множитель расхода (для настройки)
    
    [Header("Tank Settings")]
    [SerializeField] private bool autoFindTanks = true; // Автоматически находить баки
    [SerializeField] private List<LiquidTank> fuelTanks = new List<LiquidTank>();
    
    [Header("Engine Settings")]
    [SerializeField] private ShipController shipController; // Ссылка на ShipController для получения тяги двигателей
    
    [Header("UI")]
    [SerializeField] private bool showFuelInfo = true; // Показывать информацию о топливе в консоли
    
    [Header("Low Fuel Warning")]
    [SerializeField] private bool enableLowFuelWarning = true;
    [SerializeField] private float lowFuelThreshold = 0.2f; // Порог низкого топлива (20%)
    [SerializeField] private float criticalFuelThreshold = 0.1f; // Критический уровень (10%)
    
    // События
    public delegate void FuelChangedDelegate(float totalFuel, float totalMaxFuel, float fuelPercentage);
    public event FuelChangedDelegate OnFuelChanged;
    
    public delegate void LowFuelWarningDelegate(float fuelPercentage);
    public event LowFuelWarningDelegate OnLowFuelWarning;
    
    public delegate void CriticalFuelWarningDelegate(float fuelPercentage);
    public event CriticalFuelWarningDelegate OnCriticalFuelWarning;
    
    // Статус
    private float lastTotalFuel = -1f;
    private bool lowFuelWarningShown = false;
    private bool criticalFuelWarningShown = false;
    
    private void Start()
    {
        if (autoFindTanks)
        {
            FindAllTanks();
        }
        
        if (shipController == null)
        {
            shipController = FindObjectOfType<ShipController>();
        }
        
        // Инициализируем отслеживание топлива
        UpdateFuelStatus();
    }
    
    private void Update()
    {
        if (shipController == null || fuelTanks.Count == 0)
        {
            return;
        }
        
        // Получаем суммарную тягу всех двигателей
        float totalThrust = GetTotalEngineThrust();
        
        // Рассчитываем расход топлива на основе тяги
        float consumptionRate = CalculateConsumptionRate(totalThrust);
        
        // Распределяем расход между баками
        ConsumeFuel(consumptionRate * Time.deltaTime);
        
        // Обновляем статус топлива
        UpdateFuelStatus();
    }
    
    /// <summary>
    /// Находит все баки с топливом в иерархии корабля
    /// </summary>
    private void FindAllTanks()
    {
        fuelTanks.Clear();
        
        // Ищем баки в иерархии корабля
        Transform shipTransform = transform;
        if (shipController != null)
        {
            shipTransform = shipController.transform;
        }
        
        // Ищем в дочерних объектах
        LiquidTank[] allTanks = shipTransform.GetComponentsInChildren<LiquidTank>();
        foreach (var tank in allTanks)
        {
            if (tank != null && !fuelTanks.Contains(tank))
            {
                fuelTanks.Add(tank);
            }
        }
        
        // Также ищем в родительском объекте (если баки на уровне корабля)
        if (shipTransform.parent != null)
        {
            LiquidTank[] parentTanks = shipTransform.parent.GetComponentsInChildren<LiquidTank>();
            foreach (var tank in parentTanks)
            {
                if (tank != null && !fuelTanks.Contains(tank))
                {
                    fuelTanks.Add(tank);
                }
            }
        }
        
        Debug.Log($"FuelManager: Найдено {fuelTanks.Count} баков с топливом");
    }
    
    /// <summary>
    /// Получает суммарную тягу всех двигателей
    /// </summary>
    private float GetTotalEngineThrust()
    {
        if (shipController == null)
        {
            return 0f;
        }
        
        // Используем публичный метод из ShipController
        return shipController.GetTotalEngineThrust();
    }
    
    /// <summary>
    /// Рассчитывает скорость расхода топлива на основе тяги
    /// </summary>
    private float CalculateConsumptionRate(float totalThrust)
    {
        // Расход пропорционален тяге: при 0% тяги - минимальный расход, при 100% - максимальный
        float consumptionRate = Mathf.Lerp(minConsumptionRate, baseConsumptionRate, totalThrust);
        return consumptionRate * consumptionMultiplier;
    }
    
    /// <summary>
    /// Расходует топливо, распределяя его между баками пропорционально их объему
    /// </summary>
    private void ConsumeFuel(float totalConsumption)
    {
        if (fuelTanks.Count == 0 || totalConsumption <= 0f)
        {
            return;
        }
        
        // Вычисляем общий объем всех баков
        float totalVolume = 0f;
        foreach (var tank in fuelTanks)
        {
            if (tank != null)
            {
                totalVolume += tank.GetCurrentVolume();
            }
        }
        
        if (totalVolume <= 0f)
        {
            return; // Топливо закончилось
        }
        
        // Распределяем расход пропорционально объему в каждом баке
        foreach (var tank in fuelTanks)
        {
            if (tank == null || tank.GetCurrentVolume() <= 0f)
            {
                continue;
            }
            
            float tankVolume = tank.GetCurrentVolume();
            float tankRatio = tankVolume / totalVolume;
            float tankConsumption = totalConsumption * tankRatio;
            
            // Расходуем топливо из бака
            tank.DecreaseVolume(tankConsumption);
        }
    }
    
    /// <summary>
    /// Обновляет статус топлива и проверяет предупреждения
    /// </summary>
    private void UpdateFuelStatus()
    {
        float totalFuel = GetTotalFuel();
        float totalMaxFuel = GetTotalMaxFuel();
        float fuelPercentage = totalMaxFuel > 0f ? totalFuel / totalMaxFuel : 0f;
        
        // Проверяем, изменилось ли топливо
        if (Mathf.Abs(totalFuel - lastTotalFuel) > 0.1f)
        {
            lastTotalFuel = totalFuel;
            OnFuelChanged?.Invoke(totalFuel, totalMaxFuel, fuelPercentage);
            
            if (showFuelInfo && Time.frameCount % 60 == 0)
            {
                Debug.Log($"FuelManager: Топливо: {totalFuel:F1}/{totalMaxFuel:F1} л ({fuelPercentage * 100f:F1}%)");
            }
        }
        
        // Проверяем предупреждения о низком топливе
        if (enableLowFuelWarning)
        {
            if (fuelPercentage <= criticalFuelThreshold && !criticalFuelWarningShown)
            {
                criticalFuelWarningShown = true;
                OnCriticalFuelWarning?.Invoke(fuelPercentage);
                Debug.LogWarning($"FuelManager: ⚠️ КРИТИЧЕСКИЙ УРОВЕНЬ ТОПЛИВА! Осталось {fuelPercentage * 100f:F1}%");
            }
            else if (fuelPercentage > criticalFuelThreshold)
            {
                criticalFuelWarningShown = false;
            }
            
            if (fuelPercentage <= lowFuelThreshold && !lowFuelWarningShown)
            {
                lowFuelWarningShown = true;
                OnLowFuelWarning?.Invoke(fuelPercentage);
                Debug.LogWarning($"FuelManager: ⚠️ Низкий уровень топлива! Осталось {fuelPercentage * 100f:F1}%");
            }
            else if (fuelPercentage > lowFuelThreshold)
            {
                lowFuelWarningShown = false;
            }
        }
    }
    
    /// <summary>
    /// Получает общее количество топлива во всех баках
    /// </summary>
    public float GetTotalFuel()
    {
        float total = 0f;
        foreach (var tank in fuelTanks)
        {
            if (tank != null)
            {
                total += tank.GetCurrentVolume();
            }
        }
        return total;
    }
    
    /// <summary>
    /// Получает максимальную вместимость всех баков
    /// </summary>
    public float GetTotalMaxFuel()
    {
        float total = 0f;
        foreach (var tank in fuelTanks)
        {
            if (tank != null)
            {
                total += tank.GetMaxVolume();
            }
        }
        return total;
    }
    
    /// <summary>
    /// Получает процент заполнения топливом
    /// </summary>
    public float GetFuelPercentage()
    {
        float totalMax = GetTotalMaxFuel();
        if (totalMax <= 0f) return 0f;
        return GetTotalFuel() / totalMax;
    }
    
    /// <summary>
    /// Добавляет бак в список
    /// </summary>
    public void AddTank(LiquidTank tank)
    {
        if (tank != null && !fuelTanks.Contains(tank))
        {
            fuelTanks.Add(tank);
        }
    }
    
    /// <summary>
    /// Удаляет бак из списка
    /// </summary>
    public void RemoveTank(LiquidTank tank)
    {
        fuelTanks.Remove(tank);
    }
    
    /// <summary>
    /// Получает список всех баков
    /// </summary>
    public List<LiquidTank> GetTanks()
    {
        return new List<LiquidTank>(fuelTanks);
    }
    
    /// <summary>
    /// Получает базовый расход топлива (л/сек при 100% тяги)
    /// </summary>
    public float GetBaseConsumptionRate()
    {
        return baseConsumptionRate;
    }
    
    /// <summary>
    /// Получает минимальный расход топлива (л/сек при 0% тяги)
    /// </summary>
    public float GetMinConsumptionRate()
    {
        return minConsumptionRate;
    }
    
    /// <summary>
    /// Получает множитель расхода топлива
    /// </summary>
    public float GetConsumptionMultiplier()
    {
        return consumptionMultiplier;
    }
    
    /// <summary>
    /// Рассчитывает скорость расхода топлива для заданной тяги (0-1)
    /// Публичный метод для использования другими скриптами
    /// </summary>
    public float GetConsumptionRateForThrust(float thrust)
    {
        float clampedThrust = Mathf.Clamp01(thrust);
        float consumptionRate = Mathf.Lerp(minConsumptionRate, baseConsumptionRate, clampedThrust);
        return consumptionRate * consumptionMultiplier;
    }
}
