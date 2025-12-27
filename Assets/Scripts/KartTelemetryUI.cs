using UnityEngine;
using System.Reflection;
using System;

public class KartTelemetryUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private KartController _kartController;
    [SerializeField] private KartEngine _kartEngine;
    [SerializeField] private CarSuspension _carSuspension;
    [SerializeField] private KartAero _kartAero;

    [Header("Debug Colors")]
    [SerializeField] private Color _textColor = Color.white;
    [SerializeField] private int _fontSize = 16;

    private Rigidbody _rb;
    private Vector3 _lastPosition;
    private float _currentSpeed;
    private float[] _wheelLatSpeeds = new float[4];

    // Данные подвески
    private float[] _springForces = new float[4];
    private float[] _damperForces = new float[4];
    private float[] _totalSuspensionForces = new float[4];
    private float[] _wheelDistances = new float[4];
    private float[] _suspensionCompression = new float[4];

    // Кэшированные значения через рефлексию
    private float _restLength;
    private float _springStiffness;
    private float _damperStiffness;
    private float _springTravel;
    private float _suspensionWheelRadius;
    private float _frontAntiRollStiffness;
    private float _rearAntiRollStiffness;

    // Аэродинамика
    private float _airDensity;
    private float _dragCoefficient;
    private float _frontalArea;
    private float _wingArea;
    private float _liftCoefficientSlope;
    private float _wingAngleDeg;
    private float _groundEffectStrength;
    private float _groundRayLength;

    private void Start()
    {
        if (_kartController != null)
            _rb = _kartController.GetComponent<Rigidbody>();

        _lastPosition = transform.position;

        // Кэшируем значения через рефлексию при старте
        CacheSuspensionValues();
        CacheAeroValues();
    }

    private void CacheSuspensionValues()
    {
        if (_carSuspension == null) return;

        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Type type = typeof(CarSuspension);

        // Получаем значения через рефлексию
        FieldInfo restLengthField = type.GetField("restLength", flags);
        FieldInfo springStiffnessField = type.GetField("springStiffness", flags);
        FieldInfo damperStiffnessField = type.GetField("damperStiffness", flags);
        FieldInfo springTravelField = type.GetField("springTravel", flags);
        FieldInfo wheelRadiusField = type.GetField("wheelRadius", flags);
        FieldInfo frontARBField = type.GetField("frontAntiRollStiffness", flags);
        FieldInfo rearARBField = type.GetField("rearAntiRollStiffness", flags);

        if (restLengthField != null) _restLength = (float)restLengthField.GetValue(_carSuspension);
        if (springStiffnessField != null) _springStiffness = (float)springStiffnessField.GetValue(_carSuspension);
        if (damperStiffnessField != null) _damperStiffness = (float)damperStiffnessField.GetValue(_carSuspension);
        if (springTravelField != null) _springTravel = (float)springTravelField.GetValue(_carSuspension);
        if (wheelRadiusField != null) _suspensionWheelRadius = (float)wheelRadiusField.GetValue(_carSuspension);
        if (frontARBField != null) _frontAntiRollStiffness = (float)frontARBField.GetValue(_carSuspension);
        if (rearARBField != null) _rearAntiRollStiffness = (float)rearARBField.GetValue(_carSuspension);
    }

    private void CacheAeroValues()
    {
        if (_kartAero == null) return;

        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Type type = typeof(KartAero);

        FieldInfo airDensityField = type.GetField("airDensity", flags);
        FieldInfo dragCoefficientField = type.GetField("dragCoefficient", flags);
        FieldInfo frontalAreaField = type.GetField("frontalArea", flags);
        FieldInfo wingAreaField = type.GetField("wingArea", flags);
        FieldInfo liftCoefficientSlopeField = type.GetField("liftCoefficientSlope", flags);
        FieldInfo wingAngleDegField = type.GetField("wingAngleDeg", flags);
        FieldInfo groundEffectStrengthField = type.GetField("groundEffectStrength", flags);
        FieldInfo groundRayLengthField = type.GetField("groundRayLength", flags);

        if (airDensityField != null) _airDensity = (float)airDensityField.GetValue(_kartAero);
        if (dragCoefficientField != null) _dragCoefficient = (float)dragCoefficientField.GetValue(_kartAero);
        if (frontalAreaField != null) _frontalArea = (float)frontalAreaField.GetValue(_kartAero);
        if (wingAreaField != null) _wingArea = (float)wingAreaField.GetValue(_kartAero);
        if (liftCoefficientSlopeField != null) _liftCoefficientSlope = (float)liftCoefficientSlopeField.GetValue(_kartAero);
        if (wingAngleDegField != null) _wingAngleDeg = (float)wingAngleDegField.GetValue(_kartAero);
        if (groundEffectStrengthField != null) _groundEffectStrength = (float)groundEffectStrengthField.GetValue(_kartAero);
        if (groundRayLengthField != null) _groundRayLength = (float)groundRayLengthField.GetValue(_kartAero);
    }

    private void FixedUpdate()
    {
        if (_rb == null) return;

        Vector3 displacement = transform.position - _lastPosition;
        _currentSpeed = displacement.magnitude / Time.fixedDeltaTime;
        _lastPosition = transform.position;

        CalculateWheelData();

        if (_carSuspension != null)
        {
            GetSuspensionData();
        }
    }

    private void CalculateWheelData()
    {
        var fields = _kartController.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance);

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

    private void GetSuspensionData()
    {
        // Получаем трансформы колёс из CarSuspension
        Transform fl = null, fr = null, rl = null, rr = null;

        var pivotFields = _carSuspension.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var field in pivotFields)
        {
            if (field.FieldType == typeof(Transform))
            {
                Transform pivot = (Transform)field.GetValue(_carSuspension);
                if (pivot == null) continue;

                if (field.Name == "fl" || (field.Name.Contains("front") && field.Name.Contains("Left")))
                    fl = pivot;
                else if (field.Name == "fr" || (field.Name.Contains("front") && field.Name.Contains("Right")))
                    fr = pivot;
                else if (field.Name == "rl" || (field.Name.Contains("rear") && field.Name.Contains("Left")))
                    rl = pivot;
                else if (field.Name == "rr" || (field.Name.Contains("rear") && field.Name.Contains("Right")))
                    rr = pivot;
            }
        }

        // Проверяем расстояние до земли
        CheckWheelDistance(fl, 0);
        CheckWheelDistance(fr, 1);
        CheckWheelDistance(rl, 2);
        CheckWheelDistance(rr, 3);

        // Получаем сжатие пружин
        FieldInfo lastFLcompressionField = _carSuspension.GetType().GetField("lastFLcompression", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo lastFRcompressionField = _carSuspension.GetType().GetField("lastFRcompression", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo lastRLcompressionField = _carSuspension.GetType().GetField("lastRLcompression", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo lastRRcompressionField = _carSuspension.GetType().GetField("lastRRcompression", BindingFlags.NonPublic | BindingFlags.Instance);

        if (lastFLcompressionField != null && lastFRcompressionField != null &&
            lastRLcompressionField != null && lastRRcompressionField != null)
        {
            _suspensionCompression[0] = (float)lastFLcompressionField.GetValue(_carSuspension);
            _suspensionCompression[1] = (float)lastFRcompressionField.GetValue(_carSuspension);
            _suspensionCompression[2] = (float)lastRLcompressionField.GetValue(_carSuspension);
            _suspensionCompression[3] = (float)lastRRcompressionField.GetValue(_carSuspension);

            // Рассчитываем силы
            for (int i = 0; i < 4; i++)
            {
                _springForces[i] = _suspensionCompression[i] * _springStiffness;
                _damperForces[i] = _suspensionCompression[i] * 10f * _damperStiffness; // Упрощённый расчёт
                _totalSuspensionForces[i] = _springForces[i] + _damperForces[i];
            }
        }
    }

    private void CheckWheelDistance(Transform wheelPivot, int wheelIndex)
    {
        if (wheelPivot == null) return;

        Vector3 origin = wheelPivot.position;
        Vector3 direction = -wheelPivot.up;
        float maxDist = _restLength + _springTravel + _suspensionWheelRadius;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDist))
        {
            _wheelDistances[wheelIndex] = hit.distance;
        }
        else
        {
            _wheelDistances[wheelIndex] = maxDist;
        }
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
        int currentLine = 0;

        // СКОРОСТЬ
        float speedMps = _currentSpeed;
        float speedKph = speedMps * 3.6f;
        GUI.Label(new Rect(startX, startY + lineHeight * currentLine++, 400, 30),
                 $"Скорость: {speedMps:F2} м/с  ({speedKph:F1} км/ч)", style);

        // ОБОРОТЫ ДВИГАТЕЛЯ
        GUI.Label(new Rect(startX, startY + lineHeight * currentLine++, 250, 30),
                 $"RPM: {_kartEngine.CurrentRpm:F0}", style);

        // --- АЭРОДИНАМИКА ---
        if (_kartAero != null && _rb != null)
        {
            Vector3 v = _rb.linearVelocity;
            float speed = v.magnitude;

            if (speed > 0.01f)
            {
                // СИЛА АЭРОДИНАМИЧЕСКОГО СОПРОТИВЛЕНИЯ
                float dragForce = 0.5f * _airDensity * _dragCoefficient * _frontalArea * speed * speed;
                GUI.Label(new Rect(startX, startY + lineHeight * currentLine++, 400, 30),
                         $"Сопротивление: {dragForce:F1} Н (Cx={_dragCoefficient:F2})", style);

                // СИЛА ПРИЖИМА КРЫЛА
                float alphaRad = _wingAngleDeg * Mathf.Deg2Rad;
                float Cl = _liftCoefficientSlope * alphaRad;
                float downforce = 0.5f * _airDensity * Cl * _wingArea * speed * speed;
                GUI.Label(new Rect(startX, startY + lineHeight * currentLine++, 400, 30),
                         $"Прижим крыла: {downforce:F1} Н (α={_wingAngleDeg}°)", style);
            }
        }

        // --- ПОДВЕСКА ---
        if (_carSuspension != null)
        {
            // СИЛЫ ПОДВЕСКИ НА КАЖДОМ КОЛЕСЕ
            GUI.Label(new Rect(startX, startY + lineHeight * currentLine++, 500, 30),
                     $"Силы подвески:", style);

            for (int i = 0; i < 4; i++)
            {
                string wheelName = i == 0 ? "FL" : i == 1 ? "FR" : i == 2 ? "RL" : "RR";
                GUI.Label(new Rect(startX + 20, startY + lineHeight * currentLine++, 500, 30),
                         $"{wheelName}: Пружина={_springForces[i]:F0} Н, Демпфер={_damperForces[i]:F0} Н, Итого={_totalSuspensionForces[i]:F0} Н", style);
            }

            // РАССТОЯНИЕ ОТ КАЖДОГО КОЛЕСА ДО ЗЕМЛИ
            GUI.Label(new Rect(startX, startY + lineHeight * currentLine++, 400, 30),
                     $"Дистанция до земли:", style);

            for (int i = 0; i < 4; i++)
            {
                string wheelName = i == 0 ? "FL" : i == 1 ? "FR" : i == 2 ? "RL" : "RR";
                GUI.Label(new Rect(startX + 20, startY + lineHeight * currentLine++, 300, 30),
                         $"{wheelName}: {_wheelDistances[i]:F3} м", style);
            }

            // СТЕПЕНЬ СЖАТИЯ ПОДВЕСКИ
            GUI.Label(new Rect(startX, startY + lineHeight * currentLine++, 400, 30),
                     $"Сжатие подвески:", style);

            for (int i = 0; i < 4; i++)
            {
                string wheelName = i == 0 ? "FL" : i == 1 ? "FR" : i == 2 ? "RL" : "RR";
                float compressionPercent = (_suspensionCompression[i] / _springTravel) * 100f;
                GUI.Label(new Rect(startX + 20, startY + lineHeight * currentLine++, 350, 30),
                         $"{wheelName}: {_suspensionCompression[i]:F4} м ({compressionPercent:F1}%)", style);
            }
        }

        // ВЫСОТА ЦЕНТРА МАСС
        if (_rb != null)
        {
            Vector3 centerOfMass = transform.TransformPoint(_rb.centerOfMass);
            float comHeight = centerOfMass.y;
            float terrainHeight = 0f;

            if (Physics.Raycast(centerOfMass + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f))
            {
                terrainHeight = hit.point.y;
            }

            float heightAboveGround = comHeight - terrainHeight;

            GUI.Label(new Rect(startX, startY + lineHeight * currentLine++, 450, 30),
                     $"Центр масс: Высота={comHeight:F3} м, Над землёй={heightAboveGround:F3} м", style);

            // Дополнительно: угол крена
            Vector3 upVector = transform.up;
            float rollAngle = Vector3.SignedAngle(upVector, Vector3.up, transform.forward);
            GUI.Label(new Rect(startX, startY + lineHeight * currentLine++, 300, 30),
                     $"Крен: {rollAngle:F2}°", style);
        }

        // --- ОСТАЛЬНЫЕ ПАРАМЕТРЫ ---
        currentLine++;

        // КРУТЯЩИЙ МОМЕНТ
        float currentTorque = _kartEngine.CurrentTorque;
        GUI.Label(new Rect(startX, startY + lineHeight * currentLine++, 250, 30),
                 $"Крутящий момент: {currentTorque:F1} Н·м", style);

        // ПРОДОЛЬНАЯ СИЛА ЗАДНЕЙ ОСИ
        float gearRatio = 8f;
        float wheelRadius = 0.3f;
        float rearAxleFx = (currentTorque * gearRatio) / wheelRadius;
        GUI.Label(new Rect(startX, startY + lineHeight * currentLine++, 300, 30),
                 $"Задняя ось Fx: {rearAxleFx:F1} Н", style);

        // БОКОВАЯ СИЛА ПЕРЕДНЕЙ ОСИ
        float lateralStiffness = 80f;
        float frontAxleFy = -lateralStiffness * (_wheelLatSpeeds[0] + _wheelLatSpeeds[1]) * 0.5f;
        GUI.Label(new Rect(startX, startY + lineHeight * currentLine++, 300, 30),
                 $"Передняя ось Fy: {frontAxleFy:F1} Н", style);

        // БОКОВЫЕ СКОРОСТИ
        string wheelInfo = $"v_lat: FL:{_wheelLatSpeeds[0]:F2} FR:{_wheelLatSpeeds[1]:F2} " +
                          $"RL:{_wheelLatSpeeds[2]:F2} RR:{_wheelLatSpeeds[3]:F2}";
        GUI.Label(new Rect(startX, startY + lineHeight * currentLine++, 500, 30),
                 wheelInfo, style);

        // РУЧНОЙ ТОРМОЗ
        GUI.Label(new Rect(startX, startY + lineHeight * currentLine++, 200, 30),
                 $"Ручной тормоз: {(_kartController.IsHandbrakeActive ? "ВКЛ" : "ВЫКЛ")}",
                 _kartController.IsHandbrakeActive ?
                     new GUIStyle(style) { normal = { textColor = Color.red } } : style);

        // ВХОДЫ
        FieldInfo throttleField = _kartController.GetType().GetField("_throttleInput",
            BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo steerField = _kartController.GetType().GetField("_steerInput",
            BindingFlags.NonPublic | BindingFlags.Instance);

        if (throttleField != null && steerField != null)
        {
            float throttle = (float)throttleField.GetValue(_kartController);
            float steer = (float)steerField.GetValue(_kartController);
            GUI.Label(new Rect(startX, startY + lineHeight * currentLine++, 300, 30),
                     $"Ввод: Газ: {throttle:F2}, Поворот: {steer:F2}", style);
        }
    }
}