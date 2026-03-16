using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

/// <summary>
/// 시작 시 Spline 경로의 시작 위치에 서 있고, 자동으로 경로 끝까지 걸어갑니다.
/// SplineContainer와 이동 대상만 할당하면 됩니다.
/// </summary>
public class JellyWalkSplinePath : MonoBehaviour
{
    [Header("Spline 경로")]
    [Tooltip("이동 경로가 정의된 SplineContainer. 씬의 Spline 오브젝트를 할당하세요.")]
    [SerializeField] private SplineContainer _splineContainer;

    [Tooltip("이동시킬 대상. 비워두면 이 스크립트가 붙은 오브젝트가 이동합니다.")]
    [SerializeField] private Transform _target;

    [Header("이동 설정")]
    [Tooltip("경로를 따라 걸을 속도 (유닛/초).")]
    [SerializeField] private float _walkSpeed = 3f;

    [Tooltip("경로 방향(접선)을 바라보며 걸을지 여부.")]
    [SerializeField] private bool _rotateAlongPath = true;

    [Header("애니메이터")]
    [Tooltip("경로 끝 도착 시 트리거할 Animator. 비워두면 트리거하지 않습니다.")]
    [SerializeField] private Animator _animator;

    [Tooltip("경로 끝 도착 시 발동할 Animator Trigger 파라미터 이름.")]
    [SerializeField] private string _triggerParameterName = "IsArm";

    [Header("디버그")]
    [Tooltip("콘솔에 경로 시작 위치 계산 과정 및 이동 로그를 출력합니다.")]
    [SerializeField] private bool _showDebugLog;

    [Tooltip("씬 뷰에서 경로 시작/끝/현재 위치와 방향을 Gizmo로 그립니다.")]
    [SerializeField] private bool _showDebugGizmos = true;

    [Tooltip("디버그 Gizmo에서 접선/Up 벡터를 그릴 길이.")]
    [SerializeField] private float _debugVectorLength = 1f;

    private float _t; // 0 ~ 1 정규화된 경로 위치
    private float _currentDistance; // 스플라인을 따라 이동한 누적 거리
    private bool _reachedEnd;

    private Transform Target => _target != null ? _target : transform;

    private void Start()
    {
        if (_splineContainer == null)
        {
            if (_showDebugLog)
                Debug.LogWarning("[JellyWalkSplinePath] SplineContainer가 할당되지 않았습니다.", this);
            return;
        }

        if (_showDebugLog)
            LogPathStartCalculation();

        // 시작 시 경로 시작점(Path 위치)에 배치
        _t = 0f;
        _currentDistance = 0f;
        _reachedEnd = false;
        ApplyPositionAndRotation(_t);

        if (_showDebugLog)
            Debug.Log($"[JellyWalkSplinePath] 경로 시작점에 배치 완료. Target 월드 위치 = {Target.position}. 끝까지 걸어갑니다.", this);
    }

    /// <summary>Path 시작 위치가 계산되는 과정을 단계별로 로그합니다.</summary>
    private void LogPathStartCalculation()
    {
        var spline = _splineContainer.Spline;
        Transform containerTransform = _splineContainer.transform;

        Debug.Log("[JellyWalkSplinePath] ========== Path 시작 위치 계산 과정 ==========", this);

        // 1. Spline 기본 정보
        Debug.Log($"[JellyWalkSplinePath] [1] SplineContainer: {_splineContainer.name}, Spline 개수: {_splineContainer.Splines.Count}", this);
        Debug.Log($"[JellyWalkSplinePath] [1] SplineContainer 월드 위치: {containerTransform.position}, 회전: {containerTransform.rotation.eulerAngles}", this);

        // 2. 정규화된 시작 파라미터 t
        float normalizedTStart = 0f;
        Debug.Log($"[JellyWalkSplinePath] [2] 경로 시작 = 정규화 파라미터 t = {normalizedTStart} (0 = 첫 지점, 1 = 마지막 지점)", this);

        // 3. Spline 전체 길이 (거리 기반 t 계산에 사용)
        float splineLength = spline.GetLength();
        Debug.Log($"[JellyWalkSplinePath] [3] Spline 전체 길이(GetLength): {splineLength} 유닛", this);

        // 4. Spline 로컬에서 Evaluate(t=0): 로컬 좌표계 위치·접선·Up
        spline.Evaluate(normalizedTStart, out float3 positionLocal, out float3 tangentLocal, out float3 upLocal);
        Debug.Log($"[JellyWalkSplinePath] [4] Spline.Evaluate(t={normalizedTStart}) → 로컬 좌표계:", this);
        Debug.Log($"      - position (로컬): {positionLocal}", this);
        Debug.Log($"      - tangent (로컬, 방향): {tangentLocal}, 길이: {math.length(tangentLocal):F4}", this);
        Debug.Log($"      - up (로컬): {upLocal}, 길이: {math.length(upLocal):F4}", this);

        // 5. 로컬 → 월드 변환 (SplineContainer transform 적용)
        Vector3 positionWorld = containerTransform.TransformPoint(positionLocal);
        Vector3 tangentWorld = containerTransform.TransformDirection(tangentLocal);
        Vector3 upWorld = containerTransform.TransformDirection(upLocal);
        Debug.Log($"[JellyWalkSplinePath] [5] SplineContainer.TransformPoint/TransformDirection 적용 (로컬 → 월드):", this);
        Debug.Log($"      - 시작 위치 (월드): {positionWorld}", this);
        Debug.Log($"      - 접선 (월드): {tangentWorld.normalized}, 길이: {tangentWorld.magnitude:F4}", this);
        Debug.Log($"      - Up (월드): {upWorld.normalized}", this);

        // 6. SplineContainer.Evaluate 사용 시 결과 (실제 적용되는 값과 동일)
        _splineContainer.Evaluate(normalizedTStart, out float3 posApplied, out float3 tanApplied, out float3 upApplied);
        Debug.Log($"[JellyWalkSplinePath] [6] SplineContainer.Evaluate(t=0) → 실제 적용되는 월드 값:", this);
        Debug.Log($"      - position (월드): {posApplied}", this);
        Debug.Log($"      - tangent: {tanApplied}, up: {upApplied}", this);

        // 7. 적용 대상 및 최종 배치 위치
        Debug.Log($"[JellyWalkSplinePath] [7] 적용 대상: {(_target != null ? _target.name : gameObject.name)}, 최종 배치될 위치 = {posApplied}", this);
        Debug.Log("[JellyWalkSplinePath] ========== 계산 과정 끝 ==========", this);
    }

    private void Update()
    {
        if (_splineContainer == null || _reachedEnd)
            return;

        float length = GetSplineLength();
        if (length <= 0f)
            return;

        float delta = _walkSpeed * Time.deltaTime;
        _currentDistance += delta;

        if (_currentDistance >= length)
        {
            _currentDistance = length;
            _t = 1f;
            _reachedEnd = true;
            ApplyPositionAndRotation(_t);

            if (_animator != null && !string.IsNullOrEmpty(_triggerParameterName))
            {
                _animator.SetTrigger(_triggerParameterName);
                if (_showDebugLog)
                    Debug.Log($"[JellyWalkSplinePath] 경로 끝 도착 → Animator Trigger '{_triggerParameterName}' 발동.", this);
            }

            if (_showDebugLog)
                Debug.Log($"[JellyWalkSplinePath] 경로 끝 도착. 총 거리={length:F2}, currentDistance={_currentDistance:F2}, t={_t:F4}, 최종 위치={Target.position}", this);
            return;
        }

        _t = Mathf.Clamp01(_currentDistance / length);
        ApplyPositionAndRotation(_t);

        if (_showDebugLog && (int)(Time.time * 2) > (int)((Time.time - Time.deltaTime) * 2))
            Debug.Log($"[JellyWalkSplinePath] 이동 중: distance={_currentDistance:F2}/{length:F2}, t={_t:F4}, 위치={Target.position}", this);
    }

    private float GetSplineLength()
    {
        if (_splineContainer == null) return 0f;
        return _splineContainer.Spline.GetLength();
    }

    private void ApplyPositionAndRotation(float normalizedT)
    {
        _splineContainer.Evaluate(normalizedT, out float3 position, out float3 tangent, out float3 up);

        Target.position = position;

        if (_rotateAlongPath && math.lengthsq(tangent) > 0.0001f)
        {
            Target.rotation = Quaternion.LookRotation((Vector3)tangent, (Vector3)up);
        }
    }

    /// <summary>경로 끝에 도착했는지 여부.</summary>
    public bool ReachedEnd => _reachedEnd;

    /// <summary>다시 시작점에 놓고 끝까지 걸어가기를 재시작합니다.</summary>
    public void Restart()
    {
        _t = 0f;
        _currentDistance = 0f;
        _reachedEnd = false;
        if (_splineContainer != null)
            ApplyPositionAndRotation(_t);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!_showDebugGizmos || _splineContainer == null) return;

        float len = _debugVectorLength;
        var spline = _splineContainer.Spline;
        Transform containerTransform = _splineContainer.transform;

        // Path 시작점 (t=0): 녹색 구 + 접선(빨강) / Up(초록)
        spline.Evaluate(0f, out float3 p0Local, out float3 tan0Local, out float3 up0Local);
        Vector3 startWorld = containerTransform.TransformPoint(p0Local);
        Vector3 tan0World = containerTransform.TransformDirection(tan0Local).normalized * len;
        Vector3 up0World = containerTransform.TransformDirection(up0Local).normalized * len;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(startWorld, 0.2f);
        UnityEditor.Handles.Label(startWorld + Vector3.up * 0.5f, "Path 시작 (t=0)");
        Gizmos.color = Color.red;
        Gizmos.DrawLine(startWorld, startWorld + tan0World);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(startWorld, startWorld + up0World);

        // Path 끝점 (t=1): 빨간 구
        spline.Evaluate(1f, out float3 p1Local, out float3 tan1Local, out float3 up1Local);
        Vector3 endWorld = containerTransform.TransformPoint(p1Local);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(endWorld, 0.2f);
        UnityEditor.Handles.Label(endWorld + Vector3.up * 0.5f, "Path 끝 (t=1)");

        // 현재 위치 (플레이 중일 때만 의미 있음): 노란 구 + 접선
        if (Application.isPlaying && Target != null)
        {
            _splineContainer.Evaluate(_t, out float3 pos, out float3 tangent, out float3 up);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(Target.position, 0.15f);
            Gizmos.DrawLine(Target.position, Target.position + (Vector3)math.normalize(tangent) * len);
            UnityEditor.Handles.Label(Target.position + Vector3.up * 0.4f, $"현재 t={_t:F2}");
        }
    }
#endif
}
