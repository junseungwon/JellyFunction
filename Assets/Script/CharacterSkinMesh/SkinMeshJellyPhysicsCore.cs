using UnityEngine;
using System.Collections;

/// <summary>
/// 젤리 캐릭터의 물리 상태를 중앙 관리합니다.
/// 압축/팽창/진동 상태와 현재 변형 텐션을 추적합니다.
/// SkinnedMeshRenderer(Humanoid) 캐릭터 대응 버전.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SkinMeshJellyPhysicsCore : MonoBehaviour
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
    [Tooltip("충돌/착지 시 변형되는 세기 — 높을수록 충격에 크게 찌그러짐")]
    [Range(0f, 1f)] public float impactDeformStrength = 0.6f;
    [Tooltip("충돌 변형 후 원래 형태로 돌아가는 속도")]
    public float impactRecoverySpeed = 8f;

    [Header("이동 워블")]
    [Tooltip("이동 시 관성에 의한 워블(출렁임) 세기")]
    [Range(0f, 1f)] public float movementWobbleStrength = 0.3f;
    [Tooltip("워블 진동 주파수 (Hz) — 높을수록 빠르게 출렁임")]
    public float wobbleFrequency = 4f;

    [Header("속도 소스")]
    [Tooltip("true면 Animator.velocity 사용 (루트모션), false면 Rigidbody.linearVelocity 사용")]
    public bool useAnimatorVelocity = false;

    [HideInInspector] public Vector3 currentDeformation;
    [HideInInspector] public float compressionRatio = 1f;
    [HideInInspector] public bool isSqueezing = false;
    [HideInInspector] public float squeezeProgress = 0f;

    public float CurrentSpeed { get; private set; }
    public Vector3 CurrentVelocity { get; private set; }

    private Rigidbody rb;
    private Animator animator;
    private Vector3 previousVelocity;
    private Vector3 deformationVelocity;
    private Vector3 targetDeformation;
    private float wobblePhase;

    public System.Action<Vector3, float> OnImpact;
    public System.Action<float> OnSqueezeProgress;
    public System.Action OnSqueezeComplete;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
    }

    private void FixedUpdate()
    {
        UpdateVelocity();
        ComputeImpactDeformation();
        ComputeMovementWobble();
        SpringUpdate();
        previousVelocity = CurrentVelocity;
    }

    private void UpdateVelocity()
    {
        if (useAnimatorVelocity && animator != null)
            CurrentVelocity = animator.velocity;
        else
            CurrentVelocity = rb.velocity;

        CurrentSpeed = CurrentVelocity.magnitude;
    }

    private void ComputeImpactDeformation()
    {
        Vector3 velocityDelta = CurrentVelocity - previousVelocity;
        float impactMagnitude = velocityDelta.magnitude;

        if (impactMagnitude > 1f)
        {
            Vector3 impactDir = velocityDelta.normalized;
            float strength = Mathf.Clamp01(impactMagnitude / 20f) * impactDeformStrength;
            AddDeformation(impactDir * strength);
            OnImpact?.Invoke(impactDir, strength);
        }
    }

    private void ComputeMovementWobble()
    {
        wobblePhase += Time.fixedDeltaTime * wobbleFrequency;
        float speed = CurrentSpeed;
        if (speed > 0.5f)
        {
            Vector3 moveDir = CurrentVelocity.normalized;
            float wobble = Mathf.Sin(wobblePhase) * movementWobbleStrength * Mathf.Clamp01(speed / 5f);
            targetDeformation = -moveDir * wobble * 0.8f;
        }
        else
        {
            targetDeformation = Vector3.zero;
        }
    }

    private void SpringUpdate()
    {
        if (isSqueezing) return;

        Vector3 springForce = -elasticity * (currentDeformation - targetDeformation);
        Vector3 dampingForce = -damping * deformationVelocity;
        Vector3 acceleration = springForce + dampingForce;

        deformationVelocity += acceleration * Time.fixedDeltaTime;
        currentDeformation += deformationVelocity * Time.fixedDeltaTime;

        compressionRatio = 1f + currentDeformation.y;
        compressionRatio = Mathf.Clamp(compressionRatio, maxCompression, maxExpansion);
    }

    public void AddDeformation(Vector3 deformDelta)
    {
        currentDeformation += deformDelta;
    }

    public void StartSqueeze(Vector3 squeezeAxis, float gapRatio)
    {
        if (isSqueezing) return;
        StartCoroutine(SqueezeCoroutine(squeezeAxis, gapRatio));
    }

    private IEnumerator SqueezeCoroutine(Vector3 squeezeAxis, float gapRatio)
    {
        isSqueezing = true;
        squeezeProgress = 0f;

        float entryDuration = 0.4f;
        float t = 0;
        while (t < entryDuration)
        {
            t += Time.deltaTime;
            squeezeProgress = Mathf.Lerp(0f, 0.5f, t / entryDuration);
            currentDeformation = squeezeAxis * (-1f + gapRatio) +
                                  Vector3.one * (1f - gapRatio) * 0.5f;
            OnSqueezeProgress?.Invoke(squeezeProgress);
            yield return null;
        }

        yield return new WaitForSeconds(0.1f);
        squeezeProgress = 0.5f;

        float exitDuration = 0.3f;
        t = 0;
        while (t < exitDuration)
        {
            t += Time.deltaTime;
            squeezeProgress = Mathf.Lerp(0.5f, 1f, t / exitDuration);
            float bounce = Mathf.Sin(t / exitDuration * Mathf.PI);
            currentDeformation = -squeezeAxis * bounce * 0.4f;
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
        Vector3 normal = collision.GetContact(0).normal;
        float force = collision.relativeVelocity.magnitude;
        if (force > 2f)
        {
            float strength = Mathf.Clamp01(force / 15f) * impactDeformStrength;
            AddDeformation(-normal * strength);
        }
    }
}
