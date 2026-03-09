using UnityEngine;

/// <summary>
/// 플레이어의 이동을 제어하는 컴포넌트. Rigidbody 기반 이동 및 카메라 방향 회전.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    #region Inspector

    [Header("Movement Settings")]
    /// <summary>걷기 속도 (초당 유닛)</summary>
    [SerializeField] private float _walkSpeed = 4f;
    /// <summary>달리기 속도 (초당 유닛)</summary>
    [SerializeField] private float _runSpeed = 8f;
    /// <summary>회전 보간 시간</summary>
    [SerializeField] private float _rotationSmoothTime = 0.1f;

    [Header("References")]
    /// <summary>방향 기준이 되는 카메라 Transform</summary>
    [SerializeField] private Transform _cameraTransform;

    #endregion

    #region Private Fields

    private Rigidbody _rb = null;
    [SerializeField] private Animator _anim = null;
    private float _rotationVelocity = 0f;

    private static readonly int ANIM_SPEED = Animator.StringToHash("WalkSpeed");

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        if (_cameraTransform == null)
            _cameraTransform = Camera.main?.transform;

        // NOTE: Y축(중력)은 Rigidbody가 처리. 넘어짐 방지.
        _rb.freezeRotation = true;
    }

    #endregion

    #region Update - Movement

    private void FixedUpdate()
    {
        ApplyMovement();
    }

    /// <summary>입력 방향에 따라 이동 및 회전을 적용합니다.</summary>
    private void ApplyMovement()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical   = Input.GetAxisRaw("Vertical");
        Vector3 inputDir = new Vector3(horizontal, 0f, vertical).normalized;

        bool isRunning   = Input.GetKey(KeyCode.LeftShift);
        float speed      = isRunning ? _runSpeed : _walkSpeed;

        Vector3 horizontalVelocity = Vector3.zero;

        if (inputDir.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg
                                 + _cameraTransform.eulerAngles.y;

            float smoothAngle = Mathf.SmoothDampAngle(
                transform.eulerAngles.y, targetAngle,
                ref _rotationVelocity, _rotationSmoothTime, Mathf.Infinity, Time.fixedDeltaTime);
            transform.rotation = Quaternion.Euler(0f, smoothAngle, 0f);

            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            horizontalVelocity = moveDir * speed;
        }

        // 수평(XZ)만 설정, Y는 Rigidbody 중력에 맡김
        _rb.velocity = new Vector3(horizontalVelocity.x, _rb.velocity.y, horizontalVelocity.z);

        // Animator
        float animSpeed = inputDir.magnitude * (isRunning ? 2f : 1f);
        if(_anim != null)
        {
            _anim.SetFloat(ANIM_SPEED, animSpeed, 0.1f, Time.fixedDeltaTime);
        }
    }

    #endregion
}
