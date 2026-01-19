using UnityEngine;

/// <summary>
/// PID-регулятор для плавного управления
/// </summary>
[System.Serializable]
public class PIDController
{
    [Header("PID Parameters")]
    [SerializeField] private float kp = 1f; // Пропорциональный коэффициент
    [SerializeField] private float ki = 0f; // Интегральный коэффициент
    [SerializeField] private float kd = 0.1f; // Дифференциальный коэффициент
    
    [Header("Limits")]
    [SerializeField] private float minOutput = -1f; // Минимальный выход
    [SerializeField] private float maxOutput = 1f; // Максимальный выход
    [SerializeField] private float integralLimit = 10f; // Ограничение интеграла (предотвращение накопления ошибки)
    
    // Внутренние переменные
    private float integral = 0f;
    private float lastError = 0f;
    private float lastTime = 0f;
    
    /// <summary>
    /// Создает новый PID-регулятор
    /// </summary>
    public PIDController(float kp, float ki, float kd)
    {
        this.kp = kp;
        this.ki = ki;
        this.kd = kd;
        // Не вызываем Reset() здесь, так как Time.time недоступен в конструкторе
        // Reset() должен быть вызван в Awake() или Start()
        integral = 0f;
        lastError = 0f;
        lastTime = 0f;
    }
    
    /// <summary>
    /// Создает новый PID-регулятор с настройками по умолчанию
    /// </summary>
    public PIDController()
    {
        // Не вызываем Reset() здесь, так как Time.time недоступен в конструкторе
        integral = 0f;
        lastError = 0f;
        lastTime = 0f;
    }
    
    /// <summary>
    /// Вычисляет выходное значение PID-регулятора
    /// </summary>
    /// <param name="target">Целевое значение</param>
    /// <param name="current">Текущее значение</param>
    /// <param name="deltaTime">Время с последнего вызова (если 0, используется Time.deltaTime)</param>
    /// <returns>Выходное значение регулятора</returns>
    public float Update(float target, float current, float deltaTime = 0f)
    {
        if (deltaTime <= 0f)
        {
            deltaTime = Time.deltaTime;
        }
        
        if (deltaTime <= 0f)
        {
            return 0f;
        }
        
        // Вычисляем ошибку
        float error = target - current;
        
        // Пропорциональная составляющая
        float proportional = kp * error;
        
        // Интегральная составляющая
        integral += error * deltaTime;
        // Ограничиваем интеграл для предотвращения накопления ошибки
        integral = Mathf.Clamp(integral, -integralLimit, integralLimit);
        float integralTerm = ki * integral;
        
        // Дифференциальная составляющая
        float derivative = 0f;
        if (deltaTime > 0f)
        {
            derivative = (error - lastError) / deltaTime;
        }
        float derivativeTerm = kd * derivative;
        
        // Суммируем все составляющие
        float output = proportional + integralTerm + derivativeTerm;
        
        // Ограничиваем выход
        output = Mathf.Clamp(output, minOutput, maxOutput);
        
        // Сохраняем значения для следующего вызова
        lastError = error;
        lastTime = Time.time;
        
        return output;
    }
    
    /// <summary>
    /// Сбрасывает внутреннее состояние регулятора
    /// </summary>
    public void Reset()
    {
        integral = 0f;
        lastError = 0f;
        // Time.time может быть недоступен в конструкторе, поэтому проверяем
        if (Time.time > 0f)
        {
            lastTime = Time.time;
        }
        else
        {
            lastTime = 0f;
        }
    }
    
    /// <summary>
    /// Устанавливает параметры PID
    /// </summary>
    public void SetParameters(float kp, float ki, float kd)
    {
        this.kp = kp;
        this.ki = ki;
        this.kd = kd;
        Reset();
    }
    
    /// <summary>
    /// Устанавливает ограничения выхода
    /// </summary>
    public void SetOutputLimits(float min, float max)
    {
        minOutput = min;
        maxOutput = max;
    }
    
    /// <summary>
    /// Устанавливает ограничение интеграла
    /// </summary>
    public void SetIntegralLimit(float limit)
    {
        integralLimit = limit;
    }
    
    // Геттеры для параметров
    public float GetKp() => kp;
    public float GetKi() => ki;
    public float GetKd() => kd;
    public float GetMinOutput() => minOutput;
    public float GetMaxOutput() => maxOutput;
}
