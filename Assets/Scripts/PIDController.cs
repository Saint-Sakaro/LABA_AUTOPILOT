using UnityEngine;




[System.Serializable]
public class PIDController
{
    [Header("PID Parameters")]
    [SerializeField] private float kp = 1f; 
    [SerializeField] private float ki = 0f; 
    [SerializeField] private float kd = 0.1f; 
    
    [Header("Limits")]
    [SerializeField] private float minOutput = -1f; 
    [SerializeField] private float maxOutput = 1f; 
    [SerializeField] private float integralLimit = 10f; 
    
    
    private float integral = 0f;
    private float lastError = 0f;
    private float lastTime = 0f;
    
    
    
    
    public PIDController(float kp, float ki, float kd)
    {
        this.kp = kp;
        this.ki = ki;
        this.kd = kd;
        
        
        integral = 0f;
        lastError = 0f;
        lastTime = 0f;
    }
    
    
    
    
    public PIDController()
    {
        
        integral = 0f;
        lastError = 0f;
        lastTime = 0f;
    }
    
    
    
    
    
    
    
    
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
        
        
        float error = target - current;
        
        
        float proportional = kp * error;
        
        
        integral += error * deltaTime;
        
        integral = Mathf.Clamp(integral, -integralLimit, integralLimit);
        float integralTerm = ki * integral;
        
        
        float derivative = 0f;
        if (deltaTime > 0f)
        {
            derivative = (error - lastError) / deltaTime;
        }
        float derivativeTerm = kd * derivative;
        
        
        float output = proportional + integralTerm + derivativeTerm;
        
        
        output = Mathf.Clamp(output, minOutput, maxOutput);
        
        
        lastError = error;
        lastTime = Time.time;
        
        return output;
    }
    
    
    
    
    public void Reset()
    {
        integral = 0f;
        lastError = 0f;
        
        if (Time.time > 0f)
        {
            lastTime = Time.time;
        }
        else
        {
            lastTime = 0f;
        }
    }
    
    
    
    
    public void SetParameters(float kp, float ki, float kd)
    {
        this.kp = kp;
        this.ki = ki;
        this.kd = kd;
        Reset();
    }
    
    
    
    
    public void SetOutputLimits(float min, float max)
    {
        minOutput = min;
        maxOutput = max;
    }
    
    
    
    
    public void SetIntegralLimit(float limit)
    {
        integralLimit = limit;
    }
    
    
    public float GetKp() => kp;
    public float GetKi() => ki;
    public float GetKd() => kd;
    public float GetMinOutput() => minOutput;
    public float GetMaxOutput() => maxOutput;
}
