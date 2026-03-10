using UnityEngine;

/// <summary>
/// 발자국 감지 (애니메이션 이벤트 방식).
/// Animator Curve 대신 애니메이션 클립에 넣은 Animation Event에서
/// 착지 시점에 OnLeftFootDown / OnRightFootDown 을 호출하도록 설정해야 합니다.
/// </summary>
public class FootprintDetectorEvent : MonoBehaviour
{
    #region Inspector - Bones & Settings

    [Header("Foot Bones")]
    [SerializeField] private Transform _leftFootBone;
    [SerializeField] private Transform _rightFootBone;

    [Header("Detection Settings")]
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private float _raycastDistance = 0.3f;
    [SerializeField] private float _raycastOriginOffset = 0.1f;

    [Header("Footprint Rotation")]
    [SerializeField] private float _footprintNormalOffset = 0.01f;

    [Header("Debug")]
    [SerializeField] private bool _enableDebugLog = false;
    [SerializeField] private bool _drawRaycastInScene = true;
    [SerializeField] private float _debugDrawDuration = 0.5f;

    #endregion

    #region Animation Event API (애니메이션 클립에서 호출)

    /// <summary>
    /// 왼발 착지 시점에 애니메이션 이벤트로 호출합니다.
    /// Animation Event: Function = OnLeftFootDown, (Parameter 없음)
    /// </summary>
    public void OnLeftFootDown()
    {
        if (_leftFootBone == null) return;
        if (_enableDebugLog)
            Debug.Log("[FootprintDetectorEvent] OnLeftFootDown (Animation Event)");
        TrySpawnFootprint(_leftFootBone, true);
    }

    /// <summary>
    /// 오른발 착지 시점에 애니메이션 이벤트로 호출합니다.
    /// Animation Event: Function = OnRightFootDown, (Parameter 없음)
    /// </summary>
    public void OnRightFootDown()
    {
        if (_rightFootBone == null) return;
        if (_enableDebugLog)
            Debug.Log("[FootprintDetectorEvent] OnRightFootDown (Animation Event)");
        TrySpawnFootprint(_rightFootBone, false);
    }

    #endregion

    #region Private - Spawn

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
                    $"[FootprintDetectorEvent] Raycast 실패 ({(isLeft ? "왼발" : "오른발")})\n" +
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

        Vector3 forward = isLeft ? transform.right : -transform.right;
        Quaternion rotation = Quaternion.LookRotation(forward, hit.normal);
        Vector3 position = hit.point + hit.normal * _footprintNormalOffset;

        string surfaceTag = hit.collider.tag;
        if (_enableDebugLog)
            Debug.Log($"[FootprintDetectorEvent] 발자국 스폰 ({(isLeft ? "왼발" : "오른발")}) tag={surfaceTag} pos={position}");

        FootprintManager.Instance.SpawnFootprint(position, rotation, surfaceTag);
    }

    #endregion

    #region Public API

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
