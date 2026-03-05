using UnityEngine;

/// <summary>
/// 젤리 캐릭터의 이동, 점프, 물리를 통합 관리하는 최상위 컨트롤러.
/// 이동 입력을 처리하고 JellyPhysicsCore에 상태를 전달합니다.
/// </summary>
[RequireComponent(typeof(JellyPhysicsCore))]
[RequireComponent(typeof(JellyGapTraversal))]
public class JellyCharacterController : MonoBehaviour
{
    [Header("이동")]
    public float moveSpeed = 5f;
    public float rotateSpeed = 10f;

    [Header("점프")]
    public float jumpForce = 8f;
    public float gravityMultiplier = 2f;
    public float groundCheckDistance = 0.1f;
    public LayerMask groundLayer = ~0;

    [Header("착지 변형")]
    [Tooltip("착지 강도에 따른 스쿼시 세기")]
    public float landingSquashScale = 1.5f;

    // 참조
    private JellyPhysicsCore physicsCore;
    private JellyGapTraversal gapTraversal;
    private Rigidbody rb;

    private bool isGrounded;
    private float fallVelocityPrev;
    private Vector3 moveInput;

    private void Awake()
    {
        physicsCore = GetComponent<JellyPhysicsCore>();
        gapTraversal = GetComponent<JellyGapTraversal>();
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
        // 틈 통과 중이면 이동 속도 감소
        float speedMult = physicsCore.isSqueezing ? gapTraversal.squeezeMoveSpeedMultiplier : 1f;

        moveInput = new Vector3(
            Input.GetAxisRaw("Horizontal"),
            0f,
            Input.GetAxisRaw("Vertical")
        ).normalized;

        // 점프
        if (Input.GetButtonDown("Jump") && isGrounded && !physicsCore.isSqueezing)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            // 점프 시 위로 늘어나는 스트레치
            physicsCore.AddDeformation(new Vector3(0f, 0.5f, 0f));
        }

        // 틈 강제 통과 입력 (Space + 이동)
        if (Input.GetKeyDown(KeyCode.E) && moveInput.magnitude > 0.1f)
        {
            gapTraversal.ForceTraverse(moveInput, 0.3f);
        }
    }

    private void ApplyMovement()
    {
        if (physicsCore.isSqueezing && !gapTraversal.canMoveWhileSqueezing) return;

        float speedMult = physicsCore.isSqueezing ? gapTraversal.squeezeMoveSpeedMultiplier : 1f;

        Vector3 velocity = new Vector3(
            moveInput.x * moveSpeed * speedMult,
            rb.velocity.y,
            moveInput.z * moveSpeed * speedMult
        );
        rb.velocity = velocity;

        // 이동 방향으로 회전
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
            rb.AddForce(Physics.gravity * gravityMultiplier, ForceMode.Acceleration);
        }
    }

    private void CheckGrounded()
    {
        float prevFallV = fallVelocityPrev;
        fallVelocityPrev = rb.velocity.y;

        bool wasGrounded = isGrounded;
        isGrounded = Physics.Raycast(transform.position, Vector3.down,
            groundCheckDistance + 0.05f, groundLayer);

        // 착지 감지
        if (!wasGrounded && isGrounded)
        {
            float landSpeed = Mathf.Abs(prevFallV);
            if (landSpeed > 3f)
            {
                OnLanded(landSpeed);
            }
        }
    }

    private void OnLanded(float landSpeed)
    {
        // 착지 강도에 비례한 스쿼시
        float squashAmount = Mathf.Clamp01(landSpeed / 20f) * landingSquashScale;
        physicsCore.AddDeformation(new Vector3(0f, -squashAmount * 0.5f, 0f));

        // 착지 리플 효과는 PhysicsCore.OnCollisionEnter에서 처리됨
    }

    private void OnTraversalComplete()
    {
        Debug.Log("캐릭터: 틈 통과 완료, 정상 이동 재개");
    }

    // ── 외부 시스템 인터페이스 ───────────────────────────
    public bool IsGrounded => isGrounded;
    public bool IsSqueezing => physicsCore.isSqueezing;
    public float SqueezeProgress => physicsCore.squeezeProgress;

    private void OnDestroy()
    {
        if (physicsCore != null)
            physicsCore.OnSqueezeComplete -= OnTraversalComplete;
    }
}
