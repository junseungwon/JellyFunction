using UnityEngine;
using System.Collections;

/// <summary>
/// 젤리 캐릭터의 물리 상태를 중앙 관리합니다.
/// 압축/팽창/진동 상태와 현재 변형 텐션을 추적합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class JellyPhysicsCore : MonoBehaviour
{
    [Header("젤리 기본 속성")]
    [Tooltip("탄성 계수 - 높을수록 더 빠르게 원형 복구")]
    [Range(0.1f, 50f)] public float elasticity = 15f;
    [Tooltip("감쇠 계수 - 높을수록 진동이 빨리 수렴")]
    [Range(0.1f, 10f)] public float damping = 3f;
    [Tooltip("최대 압축 비율 (0.2 = 최대 80% 압축)")]
    [Range(0.1f, 0.9f)] public float maxCompression = 0.3f;
    [Tooltip("최대 팽창 비율 (2.0 = 최대 200% 팽창)")]
    [Range(1.1f, 3f)] public float maxExpansion = 1.8f;

    [Header("충돌 반응")]
    [Range(0f, 1f)] public float impactDeformStrength = 0.6f;
    public float impactRecoverySpeed = 8f;

    [Header("이동 워블")]
    [Range(0f, 1f)] public float movementWobbleStrength = 0.3f;
    public float wobbleFrequency = 4f;

    // 현재 물리 상태 (다른 컴포넌트에서 읽기 전용)
    [HideInInspector] public Vector3 currentDeformation;   // 현재 변형 벡터
    [HideInInspector] public float compressionRatio = 1f;  // 현재 압축률 (1=원형)
    [HideInInspector] public bool isSqueezing = false;     // 틈 통과 중 여부
    [HideInInspector] public float squeezeProgress = 0f;   // 틈 통과 진행도 (0~1)

    private Rigidbody rb;
    private Vector3 previousVelocity;
    private Vector3 deformationVelocity; // 스프링 속도
    private Vector3 targetDeformation;
    private float wobblePhase;

    // 이벤트
    public System.Action<Vector3, float> OnImpact;    // (방향, 강도)
    public System.Action<float> OnSqueezeProgress;    // (0~1)
    public System.Action OnSqueezeComplete;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        ComputeImpactDeformation();
        ComputeMovementWobble();
        SpringUpdate();
        previousVelocity = rb.velocity;
    }

    /// <summary>충돌/착지 충격에 의한 순간 변형</summary>
    private void ComputeImpactDeformation()
    {
        Vector3 velocityDelta = rb.velocity - previousVelocity;
        float impactMagnitude = velocityDelta.magnitude;

        if (impactMagnitude > 1f)
        {
            Vector3 impactDir = velocityDelta.normalized;
            float strength = Mathf.Clamp01(impactMagnitude / 20f) * impactDeformStrength;
            // 충격 방향으로 찌그러짐 추가
            AddDeformation(impactDir * strength);
            OnImpact?.Invoke(impactDir, strength);
        }
    }

    /// <summary>이동 시 관성에 의한 워블</summary>
    private void ComputeMovementWobble()
    {
        wobblePhase += Time.fixedDeltaTime * wobbleFrequency;
        float speed = rb.velocity.magnitude;
        if (speed > 0.5f)
        {
            Vector3 moveDir = rb.velocity.normalized;
            float wobble = Mathf.Sin(wobblePhase) * movementWobbleStrength * Mathf.Clamp01(speed / 5f);
            targetDeformation = -moveDir * wobble * 0.3f; // 이동 반대방향으로 살짝 늘어남
        }
        else
        {
            targetDeformation = Vector3.zero;
        }
    }

    /// <summary>스프링-댐퍼 시스템으로 변형 업데이트</summary>
    private void SpringUpdate()
    {
        if (isSqueezing) return; // 틈 통과 중엔 별도 처리

        // Hooke's Law + 감쇠: F = -k*x - c*v
        Vector3 springForce = -elasticity * (currentDeformation - targetDeformation);
        Vector3 dampingForce = -damping * deformationVelocity;
        Vector3 acceleration = springForce + dampingForce;

        deformationVelocity += acceleration * Time.fixedDeltaTime;
        currentDeformation += deformationVelocity * Time.fixedDeltaTime;

        // 압축률 계산 (Y축 기준)
        compressionRatio = 1f + currentDeformation.y;
        compressionRatio = Mathf.Clamp(compressionRatio, maxCompression, maxExpansion);
    }

    public void AddDeformation(Vector3 deformDelta)
    {
        currentDeformation += deformDelta;
    }

    /// <summary>틈 통과 시퀀스 시작</summary>
    public void StartSqueeze(Vector3 squeezeAxis, float gapRatio)
    {
        if (isSqueezing) return;
        StartCoroutine(SqueezeCoroutine(squeezeAxis, gapRatio));
    }

    private IEnumerator SqueezeCoroutine(Vector3 squeezeAxis, float gapRatio)
    {
        isSqueezing = true;
        squeezeProgress = 0f;

        // 1단계: 변형 진입 (0 → 0.5)
        float entryDuration = 0.4f;
        float t = 0;
        while (t < entryDuration)
        {
            t += Time.deltaTime;
            squeezeProgress = Mathf.Lerp(0f, 0.5f, t / entryDuration);
            // 통과 축으로 압축, 수직 축으로 팽창 (부피 보존)
            currentDeformation = squeezeAxis * (-1f + gapRatio) + // 틈 방향 압축
                                  Vector3.one * (1f - gapRatio) * 0.5f; // 수직 팽창
            OnSqueezeProgress?.Invoke(squeezeProgress);
            yield return null;
        }

        // 2단계: 통과 유지 (0.5)
        yield return new WaitForSeconds(0.1f);
        squeezeProgress = 0.5f;

        // 3단계: 빠져나오면서 역진동 (0.5 → 1)
        float exitDuration = 0.3f;
        t = 0;
        while (t < exitDuration)
        {
            t += Time.deltaTime;
            squeezeProgress = Mathf.Lerp(0.5f, 1f, t / exitDuration);
            float bounce = Mathf.Sin(t / exitDuration * Mathf.PI);
            currentDeformation = -squeezeAxis * bounce * 0.4f; // 빠져나오며 반대 방향 진동
            OnSqueezeProgress?.Invoke(squeezeProgress);
            yield return null;
        }

        isSqueezing = false;
        squeezeProgress = 1f;
        currentDeformation = Vector3.zero;
        OnSqueezeComplete?.Invoke();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 착지나 벽 충돌 시 즉각 변형
        Vector3 normal = collision.GetContact(0).normal;
        float force = collision.relativeVelocity.magnitude;
        if (force > 2f)
        {
            float strength = Mathf.Clamp01(force / 15f) * impactDeformStrength;
            AddDeformation(-normal * strength);
        }
    }
}
