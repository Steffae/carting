using UnityEngine;

public class KartTelemetryUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private KartController _kartController;
    [SerializeField] private KartEngine _kartEngine;

    [Header("Debug Colors")]
    [SerializeField] private Color _textColor = Color.white;
    [SerializeField] private int _fontSize = 16;

    private Rigidbody _rb;
    private Vector3 _lastPosition;
    private float _currentSpeed;
    private float[] _wheelLatSpeeds = new float[4]; // FL, FR, RL, RR

    private void Start()
    {
        if (_kartController != null)
            _rb = _kartController.GetComponent<Rigidbody>();

        _lastPosition = transform.position;
    }

    private void FixedUpdate()
    {
        if (_rb == null) return;

        // Рассчитываем скорость
        Vector3 displacement = transform.position - _lastPosition;
        _currentSpeed = displacement.magnitude / Time.fixedDeltaTime;
        _lastPosition = transform.position;

        // Собираем данные о колёсах (если есть доступ к трансформам)
        CalculateWheelData();
    }

    private void CalculateWheelData()
    {
        // Получаем трансформы колёс через рефлексию
        var fields = _kartController.GetType().GetFields(System.Reflection.BindingFlags.NonPublic |
                                                        System.Reflection.BindingFlags.Instance);

        // Ищем трансформы колёс
        Transform frontLeft = null, frontRight = null, rearLeft = null, rearRight = null;

        foreach (var field in fields)
        {
            if (field.FieldType == typeof(Transform))
            {
                Transform wheel = (Transform)field.GetValue(_kartController);
                if (wheel == null) continue;

                if (field.Name.Contains("front") && field.Name.Contains("Left"))
                    frontLeft = wheel;
                else if (field.Name.Contains("front") && field.Name.Contains("Right"))
                    frontRight = wheel;
                else if (field.Name.Contains("rear") && field.Name.Contains("Left"))
                    rearLeft = wheel;
                else if (field.Name.Contains("rear") && field.Name.Contains("Right"))
                    rearRight = wheel;
            }
        }

        // Рассчитываем боковые скорости
        if (_rb != null)
        {
            _wheelLatSpeeds[0] = GetLateralVelocity(frontLeft);
            _wheelLatSpeeds[1] = GetLateralVelocity(frontRight);
            _wheelLatSpeeds[2] = GetLateralVelocity(rearLeft);
            _wheelLatSpeeds[3] = GetLateralVelocity(rearRight);
        }
    }

    private float GetLateralVelocity(Transform wheel)
    {
        if (wheel == null || _rb == null) return 0f;

        Vector3 wheelPos = wheel.position;
        Vector3 wheelRight = wheel.right;
        Vector3 velocity = _rb.GetPointVelocity(wheelPos);

        return Vector3.Dot(velocity, wheelRight);
    }

    private void OnGUI()
    {
        if (_kartController == null || _kartEngine == null) return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = _fontSize;
        style.normal.textColor = _textColor;
        style.fontStyle = FontStyle.Bold;

        float startX = 10f;
        float startY = 10f;
        float lineHeight = 25f;

        // 1. СКОРОСТЬ
        float speedMps = _currentSpeed;
        float speedKph = speedMps * 3.6f;
        GUI.Label(new Rect(startX, startY, 350, 30),
                 $"Скорость: {speedMps:F1} м/с  ({speedKph:F1} км/ч)", style);

        // 2. RPM
        GUI.Label(new Rect(startX, startY + lineHeight * 1, 250, 30),
                 $"RPM: {_kartEngine.CurrentRpm:F0}", style);

        // 3. МОМЕНТ ДВИГАТЕЛЯ
        // Получаем CurrentTorque через рефлексию
        float currentTorque = _kartEngine.CurrentTorque;
        GUI.Label(new Rect(startX, startY + lineHeight * 2, 250, 30),
                 $"Крутящий момент: {currentTorque:F1} Н·м", style);

        // 4. ПРОДОЛЬНАЯ СИЛА ЗАДНЕЙ ОСИ (оцениваем через момент двигателя)
        // Fx = (момент * передаточное) / радиус колеса
        float gearRatio = 8f;
        float wheelRadius = 0.3f;
        float rearAxleFx = (currentTorque * gearRatio) / wheelRadius;
        GUI.Label(new Rect(startX, startY + lineHeight * 3, 300, 30),
                 $"Задняя ось Fx: {rearAxleFx:F1} Н", style);

        // 5. БОКОВАЯ СИЛА ПЕРЕДНЕЙ ОСИ (оцениваем через боковую скорость)
        float lateralStiffness = 80f;
        float frontAxleFy = -lateralStiffness * (_wheelLatSpeeds[0] + _wheelLatSpeeds[1]) * 0.5f;
        GUI.Label(new Rect(startX, startY + lineHeight * 4, 300, 30),
                 $"Передняя ось Fy: {frontAxleFy:F1} Н", style);

        // 6. БОКОВЫЕ СКОРОСТИ КОЛЁС
        string wheelInfo = $"v_lat: FL:{_wheelLatSpeeds[0]:F2} FR:{_wheelLatSpeeds[1]:F2} " +
                          $"RL:{_wheelLatSpeeds[2]:F2} RR:{_wheelLatSpeeds[3]:F2}";
        GUI.Label(new Rect(startX, startY + lineHeight * 5, 500, 30),
                 wheelInfo, style);

        // 7. РУЧНОЙ ТОРМОЗ (используем публичное свойство)
        GUI.Label(new Rect(startX, startY + lineHeight * 6, 200, 30),
                 $"Ручной тормоз: {(_kartController.IsHandbrakeActive ? "ВКЛ" : "ВЫКЛ")}",
                 _kartController.IsHandbrakeActive ?
                     new GUIStyle(style) { normal = { textColor = Color.red } } : style);

        // 8. ДОПОЛНИТЕЛЬНО: ТЕКУЩИЕ ВХОДЫ
        // Получаем приватные поля через рефлексию
        var throttleField = _kartController.GetType().GetField("_throttleInput",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var steerField = _kartController.GetType().GetField("_steerInput",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (throttleField != null && steerField != null)
        {
            float throttle = (float)throttleField.GetValue(_kartController);
            float steer = (float)steerField.GetValue(_kartController);
            GUI.Label(new Rect(startX, startY + lineHeight * 7, 300, 30),
                     $"Ввод: Газ: {throttle:F2}, Поворот: {steer:F2}", style);
        }
    }
}