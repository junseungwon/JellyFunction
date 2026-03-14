using UnityEngine;

public class AnimationInvoke : MonoBehaviour
{
    private Animator animator;

    [Header("회전 보간")]
    [Tooltip("목표 회전으로 도달하는 속도 (초당 각도). 값이 클수록 빨리 0에 수렴")]
    [SerializeField] private float rotationLerpSpeed = 90f;

    private Quaternion targetLocalRotation;
    private bool isRotatingTowardsTarget;

    void Start()
    {
        animator = GetComponent<Animator>();
        targetLocalRotation = transform.localRotation;
        isRotatingTowardsTarget = false;
    }

    void Update()
    {
        if (!isRotatingTowardsTarget) return;

        transform.localRotation = Quaternion.RotateTowards(
            transform.localRotation,
            targetLocalRotation,
            rotationLerpSpeed * Time.deltaTime
        );

        if (Quaternion.Angle(transform.localRotation, targetLocalRotation) < 0.01f)
        {
            transform.localRotation = targetLocalRotation;
            isRotatingTowardsTarget = false;
        }
    }

    public void InvokeAnimationIdle()
    {
        animator.SetTrigger("Idle");
    }

    public void InvokeAnimationWalk(bool Rotate)
    {
        animator.SetTrigger("Walk");
        if (Rotate)
        {
            RotateCharacter(0, 0, 0);
        }
    }

    public void InvokeAnimationTurn()
    {
        animator.SetTrigger("Turn");
    }

    /// <summary>
    /// 캐릭터 로컬 회전을 목표 각도로 천천히 보간합니다.
    /// </summary>
    public void RotateCharacter(float x, float y, float z)
    {
        targetLocalRotation = Quaternion.Euler(x, y, z);
        isRotatingTowardsTarget = true;
    }
}
