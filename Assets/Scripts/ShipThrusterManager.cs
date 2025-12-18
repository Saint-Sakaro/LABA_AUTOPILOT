// 2. ShipThrusterManager.cs (Управление всеми четырьмя двигателями)

using UnityEngine;
using System.Collections.Generic;

public class ShipThrusterManager : MonoBehaviour
{
    [SerializeField] private List<EngineFireController> engines = new List<EngineFireController>();
    
    [Header("Input")]
    [SerializeField] private KeyCode increaseThrust = KeyCode.W;
    [SerializeField] private KeyCode decreaseThrust = KeyCode.S;
    [SerializeField] private float thrustChangePerSecond = 0.5f;
    
    [Header("Individual Engine Control")]
    [SerializeField] private bool allowIndividualEngineControl = true;
    
    private float globalThrust = 0.3f;
    
    private void Start()
    {
        // Автоматически находим все EngineFireController на дочерних объектах
        if (engines.Count == 0)
        {
            engines.AddRange(GetComponentsInChildren<EngineFireController>());
            Debug.Log($"Найдено двигателей: {engines.Count}");
        }
    }
    
    private void Update()
    {
        HandleInput();
    }
    
    private void HandleInput()
    {
        if (Input.GetKey(increaseThrust))
        {
            SetGlobalThrust(globalThrust + thrustChangePerSecond * Time.deltaTime);
        }
        
        if (Input.GetKey(decreaseThrust))
        {
            SetGlobalThrust(globalThrust - thrustChangePerSecond * Time.deltaTime);
        }
    }
    
    /// <summary>
    /// Устанавливает одинаковую мощность для всех двигателей
    /// </summary>
    public void SetGlobalThrust(float thrustPercent)
    {
        globalThrust = Mathf.Clamp01(thrustPercent);
        
        foreach (var engine in engines)
        {
            engine.SetThrust(globalThrust);
        }
    }
    
    /// <summary>
    /// Устанавливает мощность для конкретного двигателя (по индексу)
    /// </summary>
    public void SetEngineThrust(int engineIndex, float thrustPercent)
    {
        if (engineIndex >= 0 && engineIndex < engines.Count)
        {
            engines[engineIndex].SetThrust(thrustPercent);
        }
    }
    
    /// <summary>
    /// Устанавливает индивидуальную мощность для четырёх двигателей
    /// Полезно для наклона/поворота корабля
    /// </summary>
    public void SetIndividualThrust(float frontLeft, float frontRight, float backLeft, float backRight)
    {
        if (engines.Count >= 4)
        {
            engines[0].SetThrust(frontLeft);
            engines[1].SetThrust(frontRight);
            engines[2].SetThrust(backLeft);
            engines[3].SetThrust(backRight);
        }
    }
    
    /// <summary>
    /// Получить текущую глобальную мощность
    /// </summary>
    public float GetGlobalThrust()
    {
        return globalThrust;
    }
    
    /// <summary>
    /// Получить мощность конкретного двигателя
    /// </summary>
    public float GetEngineThrust(int engineIndex)
    {
        if (engineIndex >= 0 && engineIndex < engines.Count)
        {
            return engines[engineIndex].GetThrust();
        }
        return 0f;
    }
}
