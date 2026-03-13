using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

/// <summary>
/// 젤리 캐릭터를 Spline 경로를 따라 이동시킵니다.
/// 타임라인 시그널에서 StartMove() / StopMove()를 호출해 사용하세요.
/// </summary>
public class JellySplineMove : MonoBehaviour
{
    [Header("Spline 경로")]
    [Tooltip("이동 경로가 정의된 SplineContainer. 씬에 배치된 Spline 오브젝트를 할당하세요.")]
    [SerializeField] private SplineContainer _splineContainer = null;

    [Tooltip("이동시킬 대상. 비워두면 이 컴포넌트가 붙은 오브젝트가 이동합니다.")]
    [SerializeField] private Transform _target = null;

    [Header("이동 설정")]
    [Tooltip("경로를 따라 이동하는 속도 (유닛/초).")]
    [SerializeField] private float _speed = 3f;

    [Tooltip("이동 시 경로 방향(접선)을 바라보도록 회전할지 여부.")]
    [SerializeField] private bool _rotateAlongPath = true;

    [Tooltip("경로 진행 방향. 체크 해제 시 끝→처음 방향으로 이동.")]
    [SerializeField] private bool _forward = true;

    [Header("디버그")]
    [SerializeField] private bool _showDebugLog = false;

    private bool _isMoving;
    private float _t; // 0 ~ 1 normalized
    private float _currentDistance; // 스플라인을 따라 이동한 누적 거리

    private Transform Target => _target != null ? _target : transform;

    private void Start()
    {
        StartMoveFromBeginning();
    }

    private void Update()
    {
        if (!_isMoving || _splineContainer == null)
            return;

        float length = GetSplineLength();
        if (length <= 0f)
            return;

        float delta = _speed * Time.deltaTime;
        _currentDistance += _forward ? delta : -delta;

        if (_currentDistance >= length)
        {
            _currentDistance = length;
            _isMoving = false;
            if (_showDebugLog)
                Debug.Log("[JellySplineMove] 경로 끝 도착 (Forward)", this);
        }
        else if (_currentDistance <= 0f)
        {
            _currentDistance = 0f;
            _isMoving = false;
            if (_showDebugLog)
                Debug.Log("[JellySplineMove] 경로 시작 도착 (Reverse)", this);
        }

        _t = Mathf.Clamp01(_currentDistance / length);
        ApplyPositionAndRotation(_t);
    }

    private float GetSplineLength()
    {
        if (_splineContainer == null) return 0f;
        var spline = _splineContainer.Spline;
        return spline.GetLength();
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

    // ========== 타임라인 시그널에서 호출할 메서드 ==========

    /// <summary>경로 따라 이동을 시작합니다. 타임라인 시그널에서 호출.</summary>
    public void StartMove()
    {
        if (_splineContainer == null)
        {
            if (_showDebugLog)
                Debug.LogWarning("[JellySplineMove] SplineContainer가 할당되지 않아 이동을 시작할 수 없습니다.", this);
            return;
        }
        _isMoving = true;
        if (_showDebugLog)
            Debug.Log("[JellySplineMove] 이동 시작", this);
    }

    /// <summary>현재 위치에서 이동을 중단합니다.</summary>
    public void StopMove()
    {
        _isMoving = false;
        if (_showDebugLog)
            Debug.Log("[JellySplineMove] 이동 중단", this);
    }

    /// <summary>경로 시작(0)으로 초기화한 뒤 이동을 시작합니다.</summary>
    public void StartMoveFromBeginning()
    {
        _currentDistance = 0f;
        _t = 0f;
        StartMove();
    }

    /// <summary>경로 끝(1)으로 초기화한 뒤 역방향 이동을 시작합니다.</summary>
    public void StartMoveFromEnd()
    {
        float length = GetSplineLength();
        _currentDistance = length;
        _t = 1f;
        _isMoving = true;
        if (_showDebugLog)
            Debug.Log("[JellySplineMove] 끝에서 역방향 이동 시작", this);
    }

    /// <summary>경로 상의 특정 지점(0~1)으로 즉시 텔레포트합니다. 이동은 시작하지 않습니다.</summary>
    public void SetNormalizedPosition(float normalizedPosition)
    {
        float length = GetSplineLength();
        _t = Mathf.Clamp01(normalizedPosition);
        _currentDistance = _t * length;
        if (_splineContainer != null)
            ApplyPositionAndRotation(_t);
    }

    
}
