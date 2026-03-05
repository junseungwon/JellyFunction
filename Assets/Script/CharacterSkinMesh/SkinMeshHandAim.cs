using UnityEngine;



public class SkinMeshHandAim : MonoBehaviour
{
    public enum ArmSide { Right, Left }

    [Header("타겟")]
    [Tooltip("조준할 물체. 비어 있으면 F키 입력 시 카메라 중앙 레이캐스트로 자동 지정")]
    public Transform target;
    [Tooltip("타겟이 없을 때 레이캐스트로 찾을 레이어")]
    public LayerMask targetLayer = ~0;
    [Tooltip("레이캐스트 최대 거리")]
    public float targetRayDistance = 50f;

    [Header("입력")]
    [Tooltip("조준/발사 트리거 키")]
    public KeyCode aimKey = KeyCode.F;

    [Header("캐릭터 회전")]
    [Tooltip("타겟을 향해 회전시킬 본. 비어 있으면 이 스크립트가 붙은 Transform을 회전")]
    public Transform rotateTargetBone;
    [Tooltip("캐릭터가 타겟을 향해 돌아가는 속도")]
    public float characterRotateSpeed = 5f;
    [Tooltip("캐릭터 회전을 Z축 각도만 사용 (true면 XY 평면 기준 Z각으로 향함)")]
    public bool rotateZOnly = true;

    [Header("팔(ARM 본) 조준")]
    [Tooltip("사용할 팔 (Humanoid 본)")]
    public ArmSide armSide = ArmSide.Right;
    [Tooltip("ARM 본이 타겟을 바라보는 속도 (0이면 즉시)")]
    public float armLookSpeed = 8f;

    [Header("손(날아가는 본) — 패씽으로 할당")]
    [Tooltip("타겟을 향해 이동시킬 본. 하이라키에서 원하는 본(예: Hand)을 드래그하여 할당")]
    public Transform flyingBone;
    [Tooltip("해당 본이 타겟을 향해 이동하는 속도 (m/s)")]
    public float handFlySpeed = 3f;
    [Tooltip("도착 판정 거리 (이만큼 가까우면 도착 후 애니메이션 제어로 복귀)")]
    public float handArriveDistance = 0.15f;
    [Tooltip("도착 후 조준 해제까지 대기 시간")]
    public float handHideDelay = 0.5f;

    [Header("디버그")]
    [Tooltip("캐릭터/팔/손 변경 시 콘솔 로그 출력")]
    public bool enableLog = false;
    [Tooltip("각도 계산 과정을 씬 뷰에 라인으로 표시 (타겟 방향·현재 전방·목표 각)")]
    public bool showAngleDebugDraw = false;

    private Animator animator;
    private Transform upperArmBone;

    private Vector3 currentTargetPosition;
    private bool hasValidTarget;
    private bool isAiming;
    private float armLookBlend;
    private Quaternion armOriginalRotation;
    private float handHideTimer;
    private float logThrottle;

    
    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogWarning("[SkinMeshHandAim] Animator를 찾을 수 없습니다. 자식에 Humanoid 캐릭터가 있는지 확인하세요.");
            return;
        }

        HumanBodyBones upperArm = armSide == ArmSide.Right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm;

        upperArmBone = animator.GetBoneTransform(upperArm);

        if (upperArmBone == null) Debug.LogWarning("[SkinMeshHandAim] ARM 본을 찾을 수 없습니다.");
        if (flyingBone == null) Debug.LogWarning("[SkinMeshHandAim] 날아갈 본(flyingBone)을 Inspector에서 할당하세요.");
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
                armOriginalRotation = upperArmBone.localRotation;
                handHideTimer = 0f;
                Transform rt = rotateTargetBone != null ? rotateTargetBone : transform;
                if (enableLog)
                    Debug.Log($"[SkinMeshHandAim] 조준 시작 | 타겟 위치: {currentTargetPosition} | 기준(rotTarget) 위치: {rt.position}");
            }
        }
    }

    private void LateUpdate()
    {
        if (!isAiming || animator == null || upperArmBone == null) return;

        if (target != null)
            currentTargetPosition = target.position;

        // 회전·방향 계산 기준: rotateTargetBone(또는 transform)의 위치·회전
        Transform rotTarget = rotateTargetBone != null ? rotateTargetBone : transform;

        Vector3 toTarget = currentTargetPosition - rotTarget.position;
        if (rotateZOnly)
        {
            toTarget.z = 0f;
            if (toTarget.sqrMagnitude < 0.001f) toTarget = new Vector3(rotTarget.forward.x, rotTarget.forward.y, 0f);
        }
        else
        {
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.001f) toTarget = rotTarget.forward;
        }

        if (rotateZOnly)
        {
            float targetAngleZRaw = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            float targetAngleZ = targetAngleZRaw;
            // -180~180으로 통일 (90° 방향이 일관되게 나오도록)
            if (targetAngleZ > 180f) targetAngleZ -= 360f;
            if (targetAngleZ < -180f) targetAngleZ += 360f;

            float currentZ = rotTarget.localEulerAngles.z;
            float lerpT = Mathf.Clamp01(characterRotateSpeed * Time.deltaTime);
            float newZ = Mathf.LerpAngle(currentZ, targetAngleZ, lerpT);

            // Z축만 변경 (X,Y는 건드리지 않아서 180° 꼬임 방지, Inspector 값과 일치)
            Vector3 le = rotTarget.localEulerAngles;
            le.z = newZ;
            rotTarget.localEulerAngles = le;

            if (enableLog && Time.time - logThrottle >= 0.2f)
            {
                logThrottle = Time.time;
                Debug.Log($"[SkinMeshHandAim] 각도 디버그 | toTarget.x= {toTarget.x:F3} toTarget.y= {toTarget.y:F3} | " +
                    $"targetAngleZRaw= {targetAngleZRaw:F1}° targetAngleZ(보정후)= {targetAngleZ:F1}° | " +
                    $"currentZ= {currentZ:F1}° lerpT= {lerpT:F3} newZ= {newZ:F1}°");
            }
            if (showAngleDebugDraw)
            {
                float drawLen = 2f;
                Vector3 from = rotTarget.position;
                Vector3 forwardXY = new Vector3(rotTarget.forward.x, rotTarget.forward.y, 0f);
                if (forwardXY.sqrMagnitude > 0.0001f) forwardXY.Normalize();
                else forwardXY = new Vector3(Mathf.Cos(currentZ * Mathf.Deg2Rad), Mathf.Sin(currentZ * Mathf.Deg2Rad), 0f);
                Debug.DrawLine(from, from + toTarget.normalized * drawLen, Color.green, 0.5f);   // 타겟 방향 (XY)
                Debug.DrawLine(from, from + forwardXY * drawLen, Color.blue, 0.5f);            // 현재 전방 (XY)
                Debug.DrawLine(from, currentTargetPosition, Color.red, 0.5f);                  // 타겟까지 직선
            }
        }
        else
        {
            // 전방향 모드도 로컬 기준으로 적용 (부모가 있으면 로컬로 변환)
            Quaternion targetRotWorld = Quaternion.LookRotation(toTarget.normalized);
            Quaternion targetRotLocal = rotTarget.parent != null
                ? Quaternion.Inverse(rotTarget.parent.rotation) * targetRotWorld
                : targetRotWorld;
            rotTarget.localRotation = Quaternion.Slerp(rotTarget.localRotation, targetRotLocal, Mathf.Clamp01(characterRotateSpeed * Time.deltaTime));
            if (enableLog && Time.time - logThrottle >= 0.2f)
            {
                logThrottle = Time.time;
                Debug.Log($"[SkinMeshHandAim] 각도 계산(전방향) | toTarget: {toTarget.normalized} | 대상: {rotTarget.name} | localEuler: {rotTarget.localEulerAngles}");
            }
            if (showAngleDebugDraw)
            {
                float drawLen = 2f;
                Vector3 from = rotTarget.position;
                Debug.DrawLine(from, from + toTarget.normalized * drawLen, Color.green, 0.5f);
                Debug.DrawLine(from, from + rotTarget.forward * drawLen, Color.blue, 0.5f);
                Debug.DrawLine(from, currentTargetPosition, Color.red, 0.5f);
            }
        }

        Vector3 armToTarget = currentTargetPosition - upperArmBone.position;
        if (armToTarget.sqrMagnitude > 0.0001f)
        {
            Quaternion armLookRotWorld = Quaternion.LookRotation(armToTarget.normalized);
            Quaternion armLookRotLocal = upperArmBone.parent != null
                ? Quaternion.Inverse(upperArmBone.parent.rotation) * armLookRotWorld
                : armLookRotWorld;

            if (armLookSpeed <= 0f)
                upperArmBone.localRotation = armLookRotLocal;
            else
            {
                armLookBlend = Mathf.MoveTowards(armLookBlend, 1f, armLookSpeed * Time.deltaTime);
                upperArmBone.localRotation = Quaternion.Slerp(armOriginalRotation, armLookRotLocal, armLookBlend);
            }
            if (enableLog && Time.time - logThrottle >= 0.2f) { logThrottle = Time.time; Debug.Log($"[SkinMeshHandAim] ARM 본 로컬 회전 | blend: {armLookBlend:F2} | localEuler: {upperArmBone.localEulerAngles}"); }
        }

        if (flyingBone != null)
        {
            // flyingBone 이동·회전을 부모 기준 로컬로 적용
            Transform flyParent = flyingBone.parent;
            Vector3 targetLocalPos = flyParent != null ? flyParent.InverseTransformPoint(currentTargetPosition) : currentTargetPosition;
            Vector3 currentLocalPos = flyParent != null ? flyingBone.localPosition : flyingBone.position;
            Vector3 moveLocal = Vector3.MoveTowards(currentLocalPos, targetLocalPos, handFlySpeed * Time.deltaTime);
            flyingBone.localPosition = moveLocal;

            Vector3 boneToTarget = currentTargetPosition - flyingBone.position;
            if (boneToTarget.sqrMagnitude > 0.001f)
            {
                Quaternion lookWorld = Quaternion.LookRotation(boneToTarget.normalized);
                flyingBone.localRotation = flyParent != null
                    ? Quaternion.Inverse(flyParent.rotation) * lookWorld
                    : lookWorld;
            }

            float dist = Vector3.Distance(flyingBone.position, currentTargetPosition);
            if (enableLog && Time.time - logThrottle >= 0.2f) { logThrottle = Time.time; Debug.Log($"[SkinMeshHandAim] flyingBone 이동 | localPos: {moveLocal} | 타겟까지 거리: {dist:F3}"); }
            if (dist <= handArriveDistance)
            {
                handHideTimer += Time.deltaTime;
                if (handHideTimer >= handHideDelay)
                {
                    if (enableLog)
                        Debug.Log($"[SkinMeshHandAim] 조준 종료 | flyingBone 도착, 애니메이션 제어로 복귀");
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
            if (enableLog)
                Debug.Log($"[SkinMeshHandAim] 레이캐스트로 타겟 지정 | 위치: {currentTargetPosition} | hit: {hit.transform?.name ?? "null"}");
        }
    }
}

