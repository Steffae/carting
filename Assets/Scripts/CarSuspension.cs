using UnityEngine;

public class CarSuspension : MonoBehaviour
{
    [Header("Suspension Points")]
    [SerializeField] private Transform fl;     // Переднее левое крепление подвески
    [SerializeField] private Transform fr;     // Переднее правое
    [SerializeField] private Transform rl;     // Заднее левое
    [SerializeField] private Transform rr;     // Заднее правое

    [Header("Suspension Settings")]
    [SerializeField] private float restLength = 0.4f;
    // Нормальная (не нагруженная) длина подвески — расстояние до колеса в покое.

    [SerializeField] private float springTravel = 0.2f;
    // Максимальное сжатие подвески: ход пружины.

    [SerializeField] private float springStiffness = 20000f;
    // Жёсткость пружины k (N/m). Чем выше — тем жестче подвеска.

    [SerializeField] private float damperStiffness = 3500f;
    // Коэффициент демпфера: сопротивление скорости сжатия/расширения.

    [SerializeField] private float wheelRadius = 0.35f;
    // Радиус колеса — нужен чтобы понять, где именно оно “касается” дороги.

    [Header("Anti-Roll Bar")]
    [SerializeField] private float frontAntiRollStiffness = 8000f; // жёсткость переднего ARB
    [SerializeField] private float rearAntiRollStiffness = 6000f; // жёсткость заднего ARB

    private Rigidbody rb;
    // Ссылка на Rigidbody авто.

    private float lastFLcompression;
    private float lastFRcompression;
    private float lastRLcompression;
    private float lastRRcompression;
    // Храним предыдущие значения сжатия пружины,
    // чтобы вычислять скорость сжатия для амортизатора.

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();        // Берём Rigidbody при запуске.
    }

    private void FixedUpdate()
    {
        // 1) сначала симулируем подвески - эти методы обновляют last*compression
        SimulateWheel(fl, ref lastFLcompression);
        SimulateWheel(fr, ref lastFRcompression);
        SimulateWheel(rl, ref lastRLcompression);
        SimulateWheel(rr, ref lastRRcompression);

        // 2) затем применяем анти-ролл силы, используя уже вычисленные compression
        ApplyAntiRollBars();
    }

    private void SimulateWheel(Transform pivot, ref float lastCompression)
    {
        Vector3 origin = pivot.position;
        Vector3 direction = -pivot.up;

        float maxDist = restLength + springTravel + wheelRadius;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDist))
        {
            float currentLength = hit.distance - wheelRadius;

            // Ограничение хода подвески
            currentLength = Mathf.Clamp(currentLength, restLength - springTravel, restLength + springTravel);

            // Сжатие пружины (x = Lrest - Lcurrent)
            float compression = restLength - currentLength;

            // Сила пружины: F = k * x
            float springForce = compression * springStiffness;

            // Скорость сжатия (v)
            float compressionVelocity = (compression - lastCompression) / Time.fixedDeltaTime;

            // Сила демпфера: F = c * v
            float damperForce = compressionVelocity * damperStiffness;

            lastCompression = compression;

            float totalForce = springForce + damperForce;

            // Направление силы вверх по оси подвески
            Vector3 force = pivot.up * totalForce;

            // Применение силы создаёт вертикальные колебания и крен
            rb.AddForceAtPosition(force, pivot.position, ForceMode.Force);
        }
    }

    /// Вычисляет и применяет силы анти-ролла для передней и задней оси.
    private void ApplyAntiRollBars()
    {
        // Передняя ось
        float frontDiff = lastFLcompression - lastFRcompression;
        // Сила ARB (положительная -> тянет/давит в одну сторону)
        float frontForce = frontDiff * frontAntiRollStiffness;

        // если колесо "в контакте" — применяем силы (в противном случае игнорируем)
        // логика: если сжатие > 0 — колесо в контакте (можно сделать флаг touched, если есть)
        if (lastFLcompression > -0.0001f) // простая проверка контакта
            rb.AddForceAtPosition(-transform.up * frontForce, fl.position, ForceMode.Force);
        if (lastFRcompression > -0.0001f)
            rb.AddForceAtPosition(transform.up * frontForce, fr.position, ForceMode.Force);

        // Задняя ось (аналогично)
        float rearDiff = lastRLcompression - lastRRcompression;
        float rearForce = rearDiff * rearAntiRollStiffness;

        if (lastRLcompression > -0.0001f)
            rb.AddForceAtPosition(-transform.up * rearForce, rl.position, ForceMode.Force);
        if (lastRRcompression > -0.0001f)
            rb.AddForceAtPosition(transform.up * rearForce, rr.position, ForceMode.Force);
    }

}
