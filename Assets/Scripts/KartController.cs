using UnityEngine;
using UnityEngine.InputSystem;

public class KartController : MonoBehaviour
{
    [Header("Physics")]
    [SerializeField] private float _gravity = 9.81f;

    [Header("Wheel attachment points")]
    [SerializeField] private Transform _frontLeftWheel;
    [SerializeField] private Transform _frontRightWheel;
    [SerializeField] private Transform _rearLeftWheel;
    [SerializeField] private Transform _rearRightWheel;

    [Header("Weight distribution")]
    [Range(0f, 1f)]
    [SerializeField] private float _frontAxleShare = 0.5f;

    [Header("Steering")]
    [SerializeField] private float _maxSteerAngle = 30f;

    private Quaternion _frontLeftInitialLocalRot;
    private Quaternion _frontRightInitialLocalRot;

    [Header("Input (New Input System)")]
    [SerializeField] private InputActionReference _moveActionRef;

    private float _throttleInput; // -1..1
    private float _steerInput;    // -1..1

    [Header("Engine & drivetrain")]
    [SerializeField] private KartEngine _engine;
    [SerializeField] private float _gearRatio = 8f;
    [SerializeField] private float _drivetrainEfficiency = 0.9f;
    [SerializeField] private float _wheelRadius = 0.3f;

    [Header("Rolling resistance")]
    [SerializeField] private float _rollingResistance = 0.5f;

    [Header("Tyre friction")]
    [SerializeField] private float _frictionCoefficient = 1.0f; // μ
    [SerializeField] private float _lateralStiffness = 80f;     // Cα

    private Rigidbody _rb;

    private float _frontLeftNormalForce;
    private float _frontRightNormalForce;
    private float _rearLeftNormalForce;
    private float _rearRightNormalForce;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        Initialize();
    }

    private void Start()
    {
        ComputeStaticWheelLoads();
    }

    private void Update()
    {
        ReadInput();
        RotateFrontWheels();
    }

    private void ComputeStaticWheelLoads()
    {
        float mass = _rb.mass;
        float totalWeight = mass * _gravity;

        float frontWeight = totalWeight * _frontAxleShare;
        float rearWeight = totalWeight * (1f - _frontAxleShare);

        _frontLeftNormalForce = frontWeight * 0.5f;
        _frontRightNormalForce = frontWeight * 0.5f;

        _rearLeftNormalForce = rearWeight * 0.5f;
        _rearRightNormalForce = rearWeight * 0.5f;
    }

    private void Initialize()
    {
        if (_frontLeftWheel != null)
            _frontLeftInitialLocalRot = _frontLeftWheel.localRotation;

        if (_frontRightWheel != null)
            _frontRightInitialLocalRot = _frontRightWheel.localRotation;
    }

    private void OnEnable()
    {
        if (_moveActionRef != null && _moveActionRef.action != null)
            _moveActionRef.action.Enable();
    }

    private void OnDisable()
    {
        if (_moveActionRef != null && _moveActionRef.action != null)
            _moveActionRef.action.Disable();
    }

    private void ReadInput()
    {
        Vector2 move = _moveActionRef.action.ReadValue<Vector2>();
        _steerInput = Mathf.Clamp(move.x, -1f, 1f);
        _throttleInput = Mathf.Clamp(move.y, -1f, 1f);
    }

    private void RotateFrontWheels()
    {
        float steerAngle = _maxSteerAngle * _steerInput;
        Quaternion steerRotation = Quaternion.Euler(0f, steerAngle, 0f);

        if (_frontLeftWheel != null)
            _frontLeftWheel.localRotation = _frontLeftInitialLocalRot * steerRotation;

        if (_frontRightWheel != null)
            _frontRightWheel.localRotation = _frontRightInitialLocalRot * steerRotation;
    }

    private void FixedUpdate()
    {
        ApplyWheelForces(_frontLeftWheel, _frontLeftNormalForce, isDriven: false);
        ApplyWheelForces(_frontRightWheel, _frontRightNormalForce, isDriven: false);

        ApplyWheelForces(_rearLeftWheel, _rearLeftNormalForce, isDriven: true);
        ApplyWheelForces(_rearRightWheel, _rearRightNormalForce, isDriven: true);
    }

    private void ApplyWheelForces(Transform wheel, float normalForce, bool isDriven)
    {
        if (wheel == null || _rb == null) return;

        Vector3 wheelPos = wheel.position;

        Vector3 wheelForward = wheel.forward;
        Vector3 wheelRight = wheel.right;

        Vector3 v = _rb.GetPointVelocity(wheelPos);

        float vLong = Vector3.Dot(v, wheelForward);
        float vLat = Vector3.Dot(v, wheelRight);

        float Fx = 0f;
        float Fy = 0f;

        // 1) продольная сила от двигателя — только задняя ось
        if (isDriven)
        {
            float speedAlongForward = Vector3.Dot(_rb.linearVelocity, transform.forward);

            float engineTorque = _engine.Simulate(
                _throttleInput,
                speedAlongForward,
                Time.fixedDeltaTime
            );


            float totalWheelTorque = engineTorque * _gearRatio * _drivetrainEfficiency;
            float wheelTorque = totalWheelTorque * 0.5f;
            Fx += wheelTorque / _wheelRadius;
        }

        // 2) сопротивление качению
        Fx += -_rollingResistance * vLong;

        // 3) боковая сила
        Fy += -_lateralStiffness * vLat;

        // 4) фрикционный круг
        float frictionLimit = _frictionCoefficient * normalForce; // μ * N
        float forceLength = Mathf.Sqrt(Fx * Fx + Fy * Fy);

        if (forceLength > frictionLimit && forceLength > 1e-6f)
        {
            float scale = frictionLimit / forceLength;
            Fx *= scale;
            Fy *= scale;
        }

        // 5) мировая сила
        Vector3 force = wheelForward * Fx + wheelRight * Fy;

        _rb.AddForceAtPosition(force, wheelPos, ForceMode.Force);
    }

}
