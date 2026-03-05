using UnityEngine;

/// <summary>
/// 젤리 캐릭터의 이동, 점프, 물리를 통합 관리하는 최상위 컨트롤러.
/// 이동 입력을 처리하고 SkinMeshJellyPhysicsCore에 상태를 전달합니다.
/// SkinnedMeshRenderer(Humanoid) 캐릭터 대응 버전.
/// 
/// [배치 위치] 최상위 JellyCharacter 오브젝트에 붙입니다.
/// </summary>
[RequireComponent(typeof(SkinMeshJellyPhysicsCore))]
[RequireComponent(typeof(SkinMeshJellyGapTraversal))]
public class SkinMeshJellyCharacterController : MonoBehaviour
{
    [Header("이동")]
    [Tooltip("캐릭터 최대 이동 속도 (m/s)")]
    public float moveSpeed = 5f;
    [Tooltip("이동 방향으로 캐릭터가 회전하는 속도 — 높을수록 즉시 회전")]
    public float rotateSpeed = 10f;
    [Tooltip("0=즉시 반영, 값이 클수록 속도 변화가 완만 (대략 도달 시간 초)")]
    [Range(0f, 0.5f)]
    public float velocitySmoothTime = 0.1f;

    [Header("점프")]
    [Tooltip("점프 시 위로 가해지는 순간 힘의 크기")]
    public float jumpForce = 8f;
    [Tooltip("낙하 중 총 중력 배율 (1=기본 중력, 2=2배 중력) — 높을수록 빠르게 떨어짐")]
    public float gravityMultiplier = 2f;
    [Tooltip("최대 낙하 속도 제한 (m/s) — 터널링 방지용")]
    public float maxFallSpeed = 30f;
    [Tooltip("바닥 판정용 레이캐스트 거리 — 캐릭터 발 밑으로 쏘는 길이")]
    public float groundCheckDistance = 0.2f;
    [Tooltip("바닥으로 판정할 레이어 마스크")]
    public LayerMask groundLayer = ~0;

    [Header("착지 변형")]
    [Tooltip("착지 강도에 따른 스쿼시 세기")]
    public float landingSquashScale = 1.5f;

    private SkinMeshJellyPhysicsCore physicsCore;
    private SkinMeshJellyGapTraversal gapTraversal;
    private Rigidbody rb;

    private bool isGrounded;
    private float fallVelocityPrev;
    private Vector3 moveInput;
    private Vector3 currentVelocityXZ;

    private void Awake()
    {
        physicsCore = GetComponent<SkinMeshJellyPhysicsCore>();
        gapTraversal = GetComponent<SkinMeshJellyGapTraversal>();
        rb = GetComponent<Rigidbody>();

        physicsCore.OnSqueezeComplete += OnTraversalComplete;
    }

    private void Update()
    {
        HandleInput();
        CheckGrounded();
    }

    private void FixedUpdate()
    {
        ApplyMovement();
        ApplyGravity();
    }

    private void HandleInput()
    {
        moveInput = new Vector3(
            Input.GetAxisRaw("Horizontal"),
            0f,
            Input.GetAxisRaw("Vertical")
        ).normalized;

        if (Input.GetButtonDown("Jump") && isGrounded && !physicsCore.isSqueezing)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            physicsCore.AddDeformation(new Vector3(0f, 0.5f, 0f));
        }

        if (Input.GetKeyDown(KeyCode.E) && moveInput.magnitude > 0.1f)
        {
            gapTraversal.ForceTraverse(moveInput, 0.3f);
        }
    }

    private void ApplyMovement()
    {
        if (physicsCore.isSqueezing && !gapTraversal.canMoveWhileSqueezing) return;

        float speedMult = physicsCore.isSqueezing ? gapTraversal.squeezeMoveSpeedMultiplier : 1f;

        Vector3 targetVelocityXZ = new Vector3(
            moveInput.x * moveSpeed * speedMult,
            0f,
            moveInput.z * moveSpeed * speedMult
        );

        float smooth = Mathf.Max(0.001f, velocitySmoothTime);
        float t = 1f - Mathf.Exp(-Time.fixedDeltaTime / smooth);
        currentVelocityXZ = Vector3.Lerp(currentVelocityXZ, targetVelocityXZ, t);

        rb.velocity = new Vector3(
            currentVelocityXZ.x,
            rb.velocity.y,
            currentVelocityXZ.z
        );

        if (moveInput.magnitude > 0.1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveInput);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotateSpeed * Time.fixedDeltaTime);
        }
    }

    private void ApplyGravity()
    {
        if (!isGrounded && rb.velocity.y < 0)
        {
            rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
        }

        if (rb.velocity.y < -maxFallSpeed)
        {
            rb.velocity = new Vector3(rb.velocity.x, -maxFallSpeed, rb.velocity.z);
        }
    }

    private void CheckGrounded()
    {
        float prevFallV = fallVelocityPrev;
        fallVelocityPrev = rb.velocity.y;

        bool wasGrounded = isGrounded;
        isGrounded = Physics.Raycast(transform.position, Vector3.down,
            groundCheckDistance + 0.05f, groundLayer);

        if (!wasGrounded && isGrounded)
        {
            float landSpeed = Mathf.Abs(prevFallV);
            if (landSpeed > 3f)
                OnLanded(landSpeed);
        }
    }

    private void OnLanded(float landSpeed)
    {
        float squashAmount = Mathf.Clamp01(landSpeed / 20f) * landingSquashScale;
        physicsCore.AddDeformation(new Vector3(0f, -squashAmount * 0.5f, 0f));
    }

    private void OnTraversalComplete()
    {
        Debug.Log("캐릭터: 틈 통과 완료, 정상 이동 재개");
    }

    public bool IsGrounded => isGrounded;
    public bool IsSqueezing => physicsCore.isSqueezing;
    public float SqueezeProgress => physicsCore.squeezeProgress;

    private void OnDestroy()
    {
        if (physicsCore != null)
            physicsCore.OnSqueezeComplete -= OnTraversalComplete;
    }
}
