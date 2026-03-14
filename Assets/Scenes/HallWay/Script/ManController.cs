using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

public class ManController : MonoBehaviour
{
    enum State { Idle, Walking, ArrivedWaiting, TurningPlay, TurningBlend, WalkingBack, Done }

    [Header("참조")]
    [SerializeField] SplineContainer splineContainer;
    [SerializeField] Animator animator;

    [Header("이동 설정")]
    [SerializeField] float speed = 3f;

    [Header("타이밍")]
    [Tooltip("2번 지점 도착 후 Idle 유지 시간 (초)")]
    [SerializeField] float idleWaitTime = 1f;
    [Tooltip("Turn 애니메이션 재생 시간 (초) — Turn 클립 길이에 맞게 조절")]
    [SerializeField] float turnDuration = 2f;
    [Tooltip("Turn → Walk 전환 블렌딩 대기 시간 (초)")]
    [SerializeField] float turnBlendTime = 0.5f;

    [Header("시작 트리거")]
    [SerializeField] bool start;

    [Header("디버그")]
    [Tooltip("콘솔에 상태 전환·도착 로그 출력")]
    [SerializeField] bool showDebugLog = false;
    [Tooltip("이동 중 주기적으로 현재 t, 거리, 상태를 로그 (showDebugLog가 true일 때만)")]
    [SerializeField] bool logMovingProgress = false;
    [Tooltip("이동 로그 출력 간격 (초). 0이면 매 프레임 출력하지 않음")]
    [SerializeField] float moveLogInterval = 0.5f;
#if UNITY_EDITOR
    [Header("디버그 (런타임 읽기 전용)")]
    [SerializeField] string _debugState = "";
    [SerializeField] float _debugT = -1f;
    [SerializeField] float _debugDistance = -1f;
#endif

    State _state = State.Idle;
    float _currentDistance;
    float _t;
    float _timer;
    float _splineLength;
    float _moveLogTimer; // 이동 중 주기 로그용

    void Start()
    {
        if (showDebugLog)
        {
            CacheSplineLength();
            DebugLog($"Start | State=Idle | SplineLength={_splineLength:F2} | Start=true 시 1→2 이동 시작");
            if (splineContainer == null) DebugLogWarning("SplineContainer 미할당");
            if (animator == null) DebugLogWarning("Animator 미할당");
        }
    }

    void Update()
    {
        switch (_state)
        {
            case State.Idle:
                if (start) EnterWalking();
                break;

            case State.Walking:
                UpdateMoving(forward: true);
                break;

            case State.ArrivedWaiting:
                _timer += Time.deltaTime;
                if (_timer >= idleWaitTime) EnterTurning();
                break;

            case State.TurningPlay:
                _timer += Time.deltaTime;
                if (_timer >= turnDuration) EndTurning();
                break;

            case State.TurningBlend:
                _timer += Time.deltaTime;
                if (_timer >= turnBlendTime) EnterWalkingBack();
                break;

            case State.WalkingBack:
                UpdateMoving(forward: false);
                break;
        }

#if UNITY_EDITOR
        _debugState = _state.ToString();
        _debugT = _splineLength > 0f ? Mathf.Clamp01(_currentDistance / _splineLength) : -1f;
        _debugDistance = _currentDistance;
#endif
    }

    // ── 상태 전환 ──────────────────────────────────────────

    void EnterWalking()
    {
        if (showDebugLog && (splineContainer == null || animator == null))
        {
            if (splineContainer == null) DebugLogWarning("SplineContainer가 할당되지 않았습니다.");
            if (animator == null) DebugLogWarning("Animator가 할당되지 않았습니다.");
        }
        CacheSplineLength();
        _currentDistance = 0f;
        _t = 0f;
        ApplyPosition();
        animator.SetBool("Walk", true);
        _state = State.Walking;
        DebugLog($"State: Idle → Walking | t=0, distance=0, splineLength={_splineLength:F2}");
        _moveLogTimer = 0f;
    }

    void EnterTurning()
    {
        animator.SetBool("iDLE", false);
        animator.SetBool("Turn", true);
        _timer = 0f;
        _state = State.TurningPlay;
        DebugLog($"State: ArrivedWaiting → TurningPlay | Turn 애니 재생 시작 (duration={turnDuration:F1}초)");
    }

    void EndTurning()
    {
        animator.SetBool("iDLE", true);
        animator.SetBool("Turn", false);
        _timer = 0f;
        _state = State.TurningBlend;
        DebugLog($"State: TurningPlay → TurningBlend | Turn 종료, Walk1 복귀 대기 (blend={turnBlendTime:F1}초)");
    }

    void EnterWalkingBack()
    {
        animator.SetBool("iDLE", false);
        animator.SetBool("Walk", true);
        _currentDistance = _splineLength;
        _t = 1f;
        _state = State.WalkingBack;
        DebugLog($"State: TurningBlend → WalkingBack | t=1, 역방향 이동 시작 (2→1)");
        _moveLogTimer = 0f;
    }

    // ── 이동 처리 ──────────────────────────────────────────

    void UpdateMoving(bool forward)
    {
        if (splineContainer == null || _splineLength <= 0f) return;

        _currentDistance += (forward ? 1f : -1f) * speed * Time.deltaTime;

        if (forward && _currentDistance >= _splineLength)
        {
            _currentDistance = _splineLength;
            ApplyPosition();
            animator.SetBool("Walk", false);
            animator.SetBool("iDLE", true);
            _timer = 0f;
            _state = State.ArrivedWaiting;
            DebugLog($"도착: 2번 지점 (t=1) | State → ArrivedWaiting | 다음: Idle {idleWaitTime:F1}초 후 Turn");
            return;
        }

        if (!forward && _currentDistance <= 0f)
        {
            _currentDistance = 0f;
            ApplyPosition();
            animator.SetBool("Walk", false);
            animator.SetBool("iDLE", true);
            _state = State.Done;
            DebugLog($"도착: 1번 지점 (t=0) | State → Done | 시퀀스 완료");
            return;
        }

        // 이동 중 주기 로그
        if (showDebugLog && logMovingProgress && moveLogInterval > 0f)
        {
            _moveLogTimer += Time.deltaTime;
            if (_moveLogTimer >= moveLogInterval)
            {
                _moveLogTimer = 0f;
                float t = Mathf.Clamp01(_currentDistance / _splineLength);
                string dir = forward ? "1→2" : "2→1";
                DebugLog($"이동 중 | State={_state} | 방향={dir} | t={t:F3} | distance={_currentDistance:F2}/{_splineLength:F2}");
            }
        }

        ApplyPosition();
    }

    // ── Spline 위치·회전 적용 ──────────────────────────────

    void ApplyPosition()
    {
        if (splineContainer == null || _splineLength <= 0f) return;

        _t = Mathf.Clamp01(_currentDistance / _splineLength);
        splineContainer.Evaluate(_t, out float3 pos, out float3 tangent, out float3 up);

        transform.position = pos;

        if (math.lengthsq(tangent) > 0.0001f)
            transform.rotation = Quaternion.LookRotation((Vector3)tangent, (Vector3)up);
    }

    void CacheSplineLength()
    {
        _splineLength = splineContainer != null ? splineContainer.Spline.GetLength() : 0f;
    }

    // ── 디버그 ─────────────────────────────────────────────

    void DebugLog(string message)
    {
        if (showDebugLog)
            Debug.Log($"[ManController] {message}", this);
    }

    void DebugLogWarning(string message)
    {
        if (showDebugLog)
            Debug.LogWarning($"[ManController] {message}", this);
    }
}
