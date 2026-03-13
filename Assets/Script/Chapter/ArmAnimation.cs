using UnityEngine;

public class ArmAnimation : MonoBehaviour
{
    [Header("Animation Event - Arm Stretch")]
    [Tooltip("팔 늘리기/수축 로직. 인스펙터에서 할당 후, 애니메이션 이벤트에서 OnArmStretchStart / OnArmRetract 호출")]
    [SerializeField] private ArmStretch armStretch;
    [SerializeField] private Animator animator;

    private void OnEnable()
    {
        if (armStretch != null)
            armStretch.OnRetractComplete += RestoreAnimatorSpeed;
    }

    private void OnDisable()
    {
        if (armStretch != null)
            armStretch.OnRetractComplete -= RestoreAnimatorSpeed;
    }

    /// <summary>수축 완료 시 애니메이션 속도 복구.</summary>
    private void RestoreAnimatorSpeed()
    {
        if (animator != null)
            animator.speed = 0.5f;
    }

    /// <summary>애니메이션 이벤트용. 팔 늘리기 시작 시점에 호출.</summary>
    public void OnArmStretchStart()
    {
        if (armStretch != null)
            armStretch.StartStretch();
        if (animator != null)
            animator.speed = 0f;
    }

    /// <summary>애니메이션 이벤트용. 팔 수축 시점에 호출.</summary>
    public void OnArmRetract()
    {
        if (armStretch != null)
            armStretch.RetractArm();
    }
}
