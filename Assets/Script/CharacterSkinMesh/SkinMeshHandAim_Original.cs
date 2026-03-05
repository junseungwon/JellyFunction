using UnityEngine;

/// <summary>
/// [맨 처음 버전 백업] F키 입력 시 캐릭터가 물체를 향해 회전하고, ARM 본이 물체를 바라보며,
/// 날아가는 손 오브젝트(flyingHandObject)가 물체를 향해 이동합니다.
/// 수정 전 원본 참고용 — 현재 사용 중인 스크립트는 SkinMeshHandAim.cs 입니다.
/// </summary>
public class SkinMeshHandAim_Original : MonoBehaviour
{
    public enum ArmSide { Right, Left }

    [Header("타겟")]
    public Transform target;
    public LayerMask targetLayer = ~0;
    public float targetRayDistance = 50f;

    [Header("입력")]
    public KeyCode aimKey = KeyCode.F;

    [Header("캐릭터 회전")]
    [Tooltip("타겟을 향해 회전시킬 본. 비어 있으면 이 스크립트가 붙은 Transform을 회전 (패씽으로 할당)")]
    public Transform rotateTargetBone;
    public float characterRotateSpeed = 5f;
    public bool rotateYOnly = true;

    [Header("팔(ARM 본) 조준")]
    public ArmSide armSide = ArmSide.Right;
    public float armLookSpeed = 8f;

    [Header("손 날아가기")]
    public Transform flyingHandObject;
    public float handFlySpeed = 3f;
    public float handArriveDistance = 0.15f;
    public float handHideDelay = 0.5f;

    private Animator animator;
    private Transform upperArmBone;
    private Transform handBone;

    private Vector3 currentTargetPosition;
    private bool hasValidTarget;
    private bool isAiming;
    private float armLookBlend;
    private Quaternion armOriginalRotation;
    private float handHideTimer;
    private GameObject flyingHandInstance;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogWarning("[SkinMeshHandAim_Original] Animator를 찾을 수 없습니다.");
            return;
        }

        HumanBodyBones upperArm = armSide == ArmSide.Right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm;
        HumanBodyBones hand = armSide == ArmSide.Right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand;

        upperArmBone = animator.GetBoneTransform(upperArm);
        handBone = animator.GetBoneTransform(hand);

        if (upperArmBone == null) Debug.LogWarning("[SkinMeshHandAim_Original] ARM 본을 찾을 수 없습니다.");
        if (handBone == null) Debug.LogWarning("[SkinMeshHandAim_Original] Hand 본을 찾을 수 없습니다.");

        if (flyingHandObject == null && handBone != null)
        {
            flyingHandInstance = new GameObject("FlyingHandProxy");
            flyingHandInstance.transform.SetParent(null);
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(flyingHandInstance.transform, false);
            sphere.transform.localScale = Vector3.one * 0.08f;
            if (sphere.TryGetComponent<Collider>(out var col)) Destroy(col);
            flyingHandInstance.SetActive(false);
        }
        else if (flyingHandObject != null)
        {
            flyingHandObject.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (animator == null || upperArmBone == null) return;

        if (Input.GetKeyDown(aimKey))
        {
            if (!hasValidTarget && target != null)
            {
                currentTargetPosition = target.position;
                hasValidTarget = true;
            }
            if (!hasValidTarget)
                TryFindTargetByRaycast();

            if (hasValidTarget)
            {
                isAiming = true;
                armLookBlend = 0f;
                armOriginalRotation = upperArmBone.rotation;
                handHideTimer = 0f;

                Transform flyTransform = flyingHandObject != null ? flyingHandObject : (flyingHandInstance != null ? flyingHandInstance.transform : null);
                if (flyTransform != null && handBone != null)
                {
                    flyTransform.position = handBone.position;
                    flyTransform.rotation = handBone.rotation;
                    flyTransform.gameObject.SetActive(true);
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (!isAiming || animator == null || upperArmBone == null) return;

        if (target != null)
            currentTargetPosition = target.position;

        Transform rotTarget = rotateTargetBone != null ? rotateTargetBone : transform;

        Vector3 toTarget = currentTargetPosition - rotTarget.position;
        if (rotateYOnly)
        {
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.001f) toTarget = rotTarget.forward;
        }
        else
        {
            if (toTarget.sqrMagnitude < 0.001f) toTarget = rotTarget.forward;
        }

        if (rotateYOnly)
        {
            Quaternion targetCharRot = Quaternion.LookRotation(toTarget.normalized);
            rotTarget.rotation = Quaternion.Slerp(rotTarget.rotation, targetCharRot, characterRotateSpeed * Time.deltaTime);
        }
        else
        {
            Quaternion targetCharRot = Quaternion.LookRotation(toTarget.normalized);
            rotTarget.rotation = Quaternion.Slerp(rotTarget.rotation, targetCharRot, characterRotateSpeed * Time.deltaTime);
        }

        Vector3 armToTarget = currentTargetPosition - upperArmBone.position;
        if (armToTarget.sqrMagnitude > 0.0001f)
        {
            Quaternion armLookRot = Quaternion.LookRotation(armToTarget.normalized);
            if (armLookSpeed <= 0f)
                upperArmBone.rotation = armLookRot;
            else
            {
                armLookBlend = Mathf.MoveTowards(armLookBlend, 1f, armLookSpeed * Time.deltaTime);
                upperArmBone.rotation = Quaternion.Slerp(armOriginalRotation, armLookRot, armLookBlend);
            }
        }

        Transform flyTransformToMove = flyingHandObject != null ? flyingHandObject : (flyingHandInstance != null ? flyingHandInstance.transform : null);
        if (flyTransformToMove != null && flyTransformToMove.gameObject.activeSelf)
        {
            Vector3 move = Vector3.MoveTowards(flyTransformToMove.position, currentTargetPosition, handFlySpeed * Time.deltaTime);
            flyTransformToMove.position = move;
            flyTransformToMove.LookAt(currentTargetPosition);

            float dist = Vector3.Distance(move, currentTargetPosition);
            if (dist <= handArriveDistance)
            {
                handHideTimer += Time.deltaTime;
                if (handHideTimer >= handHideDelay)
                {
                    flyTransformToMove.gameObject.SetActive(false);
                    isAiming = false;
                }
            }
        }
    }

    private void TryFindTargetByRaycast()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, targetRayDistance, targetLayer))
        {
            currentTargetPosition = hit.point;
            hasValidTarget = true;
            if (target == null)
                target = hit.transform;
        }
    }

    private void OnDestroy()
    {
        if (flyingHandInstance != null)
            Destroy(flyingHandInstance);
    }
}
