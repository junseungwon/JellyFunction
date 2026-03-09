using UnityEngine;

public class FootprintDetector : MonoBehaviour
{
    #region Inspector - Bones & Settings

    [Header("Foot Bones")]
    [SerializeField] private Transform _leftFootBone;
    [SerializeField] private Transform _rightFootBone;

    [Header("Detection Settings")]
    [SerializeField] private LayerMask _groundLayer;
    [Tooltip("curve가 이 값 이상일 때 착지로 판정 (착지 시 1.0이면 예: 0.9)")]
    [SerializeField] private float _curveThresholdOn = 0.9f;   // 착지 판정 (높을수록 땅에 닿음)
    [Tooltip("curve가 이 값 이하일 때 이륙으로 판정 (공중일 때 0 근처면 예: 0.1)")]
    [SerializeField] private float _curveThresholdOff = 0.1f;  // 이륙 판정 (낮을수록 발 떼짐)
    [SerializeField] private float _raycastDistance = 0.3f;
    [SerializeField] private float _raycastOriginOffset = 0.1f;

    [Header("Footprint Rotation")]
    [SerializeField] private float _footprintNormalOffset = 0.01f; // 지면에서 살짝 띄우기

    [Header("Debug")]
    [SerializeField] private bool _enableDebugLog;
    [SerializeField] private bool _drawRaycastInScene = true;  // Scene 뷰에서 레이 시각화
    [SerializeField] private float _debugDrawDuration = 0.5f;

    [SerializeField] private Animator _animator;
    [SerializeField] private Rigidbody _rigidbody;

    #endregion

    #region Private Fields

    private bool _leftPlanted;
    private bool _rightPlanted;

    // Animator Curve 파라미터 이름 (해시로 캐싱)
    private static readonly int LeftFootHash = Animator.StringToHash("LeftFootprint");
    private static readonly int RightFootHash = Animator.StringToHash("RightFootprint");

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        // 이동 중일 때만 검사 (sqrMagnitude로 성능 최적화)
        if (_rigidbody.velocity.sqrMagnitude < 0.01f)
        {
            if (_enableDebugLog && (_leftPlanted || _rightPlanted))
                //Debug.Log("[FootprintDetector] 이동 정지로 검사 스킵 (velocity 거의 0)");
            return;
        }

        DetectFoot(LeftFootHash, _leftFootBone, ref _leftPlanted, isLeft: true);
        DetectFoot(RightFootHash, _rightFootBone, ref _rightPlanted, isLeft: false);
    }

    #endregion

    #region Private - Detection

    private void DetectFoot(int curveHash, Transform footBone, ref bool isPlanted, bool isLeft)
    {
        float curveValue = _animator.GetFloat(curveHash);
        Debug.Log((isLeft ? "왼발" : "오른발") + " curveValue: " + curveValue);
        // 착지: curve가 1에 가까우면(임계값 이상) 착지로 판정
        if (curveValue > _curveThresholdOn && !isPlanted)
        {
            isPlanted = true;
            if (_enableDebugLog)
                Debug.Log($"[FootprintDetector] 착지 감지 ({(isLeft ? "왼발" : "오른발")}) curve={curveValue:F2}");
            TrySpawnFootprint(footBone, isLeft);
        }
        // 이륙: curve가 0에 가까우면(임계값 이하) 이륙으로 판정
        else if (curveValue < _curveThresholdOff)
        {
            if (_enableDebugLog && isPlanted)
                Debug.Log($"[FootprintDetector] 이륙 ({(isLeft ? "왼발" : "오른발")}) curve={curveValue:F2}");
            isPlanted = false;
        }
    }

    private void TrySpawnFootprint(Transform footBone, bool isLeft)
    {
        Vector3 origin = footBone.position + Vector3.up * _raycastOriginOffset;
        Vector3 rayEnd = origin + Vector3.down * _raycastDistance;

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                _raycastDistance, _groundLayer, QueryTriggerInteraction.Ignore))
        {
            if (_enableDebugLog)
            {
                Vector3 direction = Vector3.down;
                Debug.LogWarning(
                    $"[FootprintDetector] Raycast 실패 ({(isLeft ? "왼발" : "오른발")})\n" +
                    $"  footBone.position= {footBone.position}\n" +
                    $"  origin=          {origin} (footBone + up * {_raycastOriginOffset})\n" +
                    $"  direction=       {direction}\n" +
                    $"  distance=        {_raycastDistance}\n" +
                    $"  rayEnd=          {rayEnd}\n" +
                    $"  groundLayer=     {_groundLayer.value} (mask)");
            }
#if UNITY_EDITOR
            if (_drawRaycastInScene)
                Debug.DrawLine(origin, rayEnd, Color.red, _debugDrawDuration);
#endif
            return;
        }

#if UNITY_EDITOR
        if (_drawRaycastInScene)
            Debug.DrawLine(origin, hit.point, Color.green, _debugDrawDuration);
#endif

        // 지면 법선 기준 발자국 회전 계산
        Vector3 forward = isLeft ? transform.right : -transform.right;
        Quaternion rotation = Quaternion.LookRotation(forward, hit.normal);
        Vector3 position = hit.point + hit.normal * _footprintNormalOffset;

        // 지면 태그로 재질 분기
        string surfaceTag = hit.collider.tag;
        if (_enableDebugLog)
            Debug.LogWarning($"[FootprintDetector] 발자국 스폰 ({(isLeft ? "왼발" : "오른발")}) tag={surfaceTag} pos={position}");

        FootprintManager.Instance.SpawnFootprint(position, rotation, surfaceTag);
    }

    #endregion

    #region Public API

    // ─── 활성화 제어 API ─────────────────────────────────────────

    public void EnableDetection()  => enabled = true;
    public void DisableDetection() => enabled = false;
    public void ToggleDetection()  => enabled = !enabled;

    #endregion

#if UNITY_EDITOR
    #region Editor - Gizmos

    private void OnDrawGizmosSelected()
    {
        if (_leftFootBone != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(
                _leftFootBone.position + Vector3.up * _raycastOriginOffset,
                _leftFootBone.position + Vector3.down * _raycastDistance
            );
        }
        if (_rightFootBone != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(
                _rightFootBone.position + Vector3.up * _raycastOriginOffset,
                _rightFootBone.position + Vector3.down * _raycastDistance
            );
        }
    }

    #endregion
#endif
}
