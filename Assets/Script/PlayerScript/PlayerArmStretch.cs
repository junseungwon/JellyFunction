using UnityEngine;

/// <summary>
/// 플레이어 팔 스트레치(조준) 동작을 제어합니다. 타겟 방향으로 상완·손 본을 회전/이동시킵니다.
/// </summary>
public class PlayerArmStretch : MonoBehaviour
{
    [Header("타겟")]
    [Tooltip("조준할 물체. 비어 있으면 조준 키 입력 시 카메라 중앙 레이캐스트로 자동 지정")]
    [SerializeField] private Transform _targetTransform = null;
    [Tooltip("타겟이 없을 때 레이캐스트로 찾을 레이어")]
    [SerializeField] private LayerMask _targetLayer = ~0;
    [Tooltip("레이캐스트 최대 거리")]
    [SerializeField] private float _targetRayDistance = 50f;

    [Header("입력")]
    [Tooltip("조준/발사 트리거 키")]
    [SerializeField] private KeyCode _aimKey = KeyCode.F;

    [Header("캐릭터 회전")]
    [Tooltip("타겟을 향해 회전시킬 본. 비어 있으면 이 스크립트가 붙은 Transform을 회전")]
    [SerializeField] private Transform _rotateTargetBone = null;
    [Tooltip("캐릭터가 타겟을 향해 돌아가는 속도")]
    [SerializeField] private float _characterRotateSpeed = 5f;

    [Header("팔(ARM 본) 조준")]
    [Tooltip("타겟을 바라보게 할 상완(Upper Arm) 본. 하이라키에서 할당")]
    [SerializeField] private Transform _upperArmBone = null;
    [Tooltip("ARM 본이 타겟을 바라보는 속도 (0이면 즉시)")]
    [SerializeField] private float _armLookSpeed = 8f;

    [Header("손(날아가는 본)")]
    [Tooltip("타겟을 향해 이동시킬 본. 하이라키에서 원하는 본(예: Hand)을 할당")]
    [SerializeField] private Transform _flyingBone = null;
    [Tooltip("해당 본이 타겟을 향해 이동하는 속도 (m/s)")]
    [SerializeField] private float _handFlySpeed = 3f;
    [Tooltip("도착 판정 거리 (이만큼 가까우면 도착 후 조준 해제)")]
    [SerializeField] private float _handArriveDistance = 0.15f;
    [Tooltip("도착 후 조준 해제까지 대기 시간")]
    [SerializeField] private float _handHideDelay = 0.5f;

    [Header("디버그")]
    [SerializeField] private bool _enableLog = false;
    [SerializeField] private bool _showAngleDebugDraw = false;

    private Vector3 _currentTargetPosition = Vector3.zero;
    private bool _hasValidTarget = false;
    private bool _isAiming = false;
    private float _armLookBlend = 0f;
    private Quaternion _armOriginalRotation = Quaternion.identity;
    private float _handHideTimer = 0f;
    private float _logThrottle = 0f;

    private void Awake()
    {
        if (_upperArmBone == null)
            Debug.LogWarning("[PlayerArmStretch] 상완 본(_upperArmBone)을 Inspector에서 할당하세요.");
        if (_flyingBone == null)
            Debug.LogWarning("[PlayerArmStretch] 날아갈 본(_flyingBone)을 Inspector에서 할당하세요.");
    }

    private void Update()
    {
        if (_upperArmBone == null) return;

        if (Input.GetKeyDown(_aimKey))
        {
            if (!_hasValidTarget && _targetTransform != null)
            {
                _currentTargetPosition = _targetTransform.position;
                _hasValidTarget = true;
            }
            if (!_hasValidTarget)
                TryFindTargetByRaycast();

            if (_hasValidTarget)
            {
                _isAiming = true;
                _armLookBlend = 0f;
                _armOriginalRotation = _upperArmBone.localRotation;
                _handHideTimer = 0f;
                Transform rt = _rotateTargetBone != null ? _rotateTargetBone : transform;
                if (_enableLog)
                    Debug.Log($"[PlayerArmStretch] 조준 시작 | 타겟 위치: {_currentTargetPosition} | 기준(rotTarget) 위치: {rt.position}");
            }
        }
    }

    private void LateUpdate()
    {
        if (!_isAiming || _upperArmBone == null) return;

        if (_targetTransform != null)
            _currentTargetPosition = _targetTransform.position;

        Transform rotTarget = _rotateTargetBone != null ? _rotateTargetBone : transform;

        Vector3 toTarget = _currentTargetPosition - rotTarget.position;
        if (toTarget.sqrMagnitude < 0.001f)
            toTarget = rotTarget.forward;
        else
            toTarget.Normalize();

        
        ApplyCharacterRotation(rotTarget, toTarget);
        ApplyArmLookAtTarget();
      
      
      //팔이 늘어나느 부분이고
       ApplyFlyingBoneMovement();
    }

    /// <summary>캐릭터(또는 지정 본)를 타겟 방향으로 X,Y,Z 전체 회전시킵니다.</summary>
    private void ApplyCharacterRotation(Transform rotTarget, Vector3 toTarget)
    {
        Quaternion targetRotWorld = Quaternion.LookRotation(toTarget);
        Quaternion targetRotLocal = rotTarget.parent != null
            ? Quaternion.Inverse(rotTarget.parent.rotation) * targetRotWorld
            : targetRotWorld;
        rotTarget.localRotation = Quaternion.Slerp(rotTarget.localRotation, targetRotLocal, Mathf.Clamp01(_characterRotateSpeed * Time.deltaTime));

        if (_enableLog && Time.time - _logThrottle >= 0.2f)
        {
            _logThrottle = Time.time;
            Debug.Log($"[PlayerArmStretch] 회전 디버그 | toTarget: {toTarget} | localEuler: {rotTarget.localEulerAngles}");
        }
        if (_showAngleDebugDraw)
            DrawAngleDebug(rotTarget, toTarget);
    }

    /// <summary>상완(ARM) 본이 타겟을 바라보도록 로컬 회전을 적용합니다.</summary>
    private void ApplyArmLookAtTarget()
    {
        Vector3 armToTarget = _currentTargetPosition - _upperArmBone.position;
        if (armToTarget.sqrMagnitude <= 0.0001f) return;

        Quaternion armLookRotWorld = Quaternion.LookRotation(armToTarget.normalized);
        Quaternion armLookRotLocal = _upperArmBone.parent != null
            ? Quaternion.Inverse(_upperArmBone.parent.rotation) * armLookRotWorld
            : armLookRotWorld;

        if (_armLookSpeed <= 0f)
            _upperArmBone.localRotation = armLookRotLocal;
        else
        {
            _armLookBlend = Mathf.MoveTowards(_armLookBlend, 1f, _armLookSpeed * Time.deltaTime);
            _upperArmBone.localRotation = Quaternion.Slerp(_armOriginalRotation, armLookRotLocal, _armLookBlend);
        }
    }

    /// <summary>날아가는 본(손)을 타겟 위치로 이동시키고, 도착 시 조준 해제합니다.</summary>
    private void ApplyFlyingBoneMovement()
    {
        if (_flyingBone == null) return;

        Transform flyParent = _flyingBone.parent;
        Vector3 targetLocalPos = flyParent != null ? flyParent.InverseTransformPoint(_currentTargetPosition) : _currentTargetPosition;
        Vector3 currentLocalPos = flyParent != null ? _flyingBone.localPosition : _flyingBone.position;
        Vector3 moveLocal = Vector3.MoveTowards(currentLocalPos, targetLocalPos, _handFlySpeed * Time.deltaTime);
        _flyingBone.localPosition = moveLocal;

        Vector3 boneToTarget = _currentTargetPosition - _flyingBone.position;
        if (boneToTarget.sqrMagnitude > 0.001f)
        {
            Quaternion lookWorld = Quaternion.LookRotation(boneToTarget.normalized);
            _flyingBone.localRotation = flyParent != null
                ? Quaternion.Inverse(flyParent.rotation) * lookWorld
                : lookWorld;
        }

        float dist = Vector3.Distance(_flyingBone.position, _currentTargetPosition);
        if (dist <= _handArriveDistance)
        {
            _handHideTimer += Time.deltaTime;
            if (_handHideTimer >= _handHideDelay)
            {
                if (_enableLog)
                    Debug.Log("[PlayerArmStretch] 조준 종료 | flyingBone 도착, 애니메이션 제어로 복귀");
                _isAiming = false;
            }
        }
    }

    /// <summary>씬 뷰에 타겟 방향·전방·연결선 디버그 라인을 그립니다.</summary>
    private void DrawAngleDebug(Transform rotTarget, Vector3 toTarget)
    {
        float drawLen = 2f;
        Vector3 from = rotTarget.position;
        Debug.DrawLine(from, from + toTarget * drawLen, Color.green, 0.5f);
        Debug.DrawLine(from, from + rotTarget.forward * drawLen, Color.blue, 0.5f);
        Debug.DrawLine(from, _currentTargetPosition, Color.red, 0.5f);
    }

    /// <summary>카메라 중앙에서 레이캐스트로 타겟을 찾습니다.</summary>
    private void TryFindTargetByRaycast()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, _targetRayDistance, _targetLayer))
        {
            _currentTargetPosition = hit.point;
            _hasValidTarget = true;
            if (_targetTransform == null)
                _targetTransform = hit.transform;
            if (_enableLog)
                Debug.Log($"[PlayerArmStretch] 레이캐스트로 타겟 지정 | 위치: {_currentTargetPosition} | hit: {hit.transform?.name ?? "null"}");
        }
    }
}
