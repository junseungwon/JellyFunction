using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using CharacterPressing;

/// <summary>
/// 챕터 2-3 전용 시퀀스 스크립트.
/// 시작 시 캐릭터 → 볼 변형 후 Spline 경로를 4구역 속도 제어로 이동하고,
/// tag "Zone"인 트리거에 들어가면 해당 구역 도착으로 판정 후 정지·원래 모형으로 복귀합니다.
/// </summary>
public class Jelly_Chapter2_3 : MonoBehaviour
{
    [Header("캐릭터 변환")]
    [Tooltip("캐릭터 ↔ 볼 전환 담당. ChangeModel 컴포넌트가 붙은 오브젝트를 할당하세요.")]
    [SerializeField] private ChangeModel _changeModel = null;

    [Header("Spline 경로")]
    [Tooltip("이동 경로가 정의된 SplineContainer.")]
    [SerializeField] private SplineContainer _splineContainer = null;

    [Tooltip("이동시킬 대상. 비워두면 이 컴포넌트가 붙은 오브젝트가 이동합니다.")]
    [SerializeField] private Transform _target = null;

    [Tooltip("이동 시 경로 방향(접선)을 바라보도록 회전할지 여부.")]
    [SerializeField] private bool _rotateAlongPath = true;

    [Header("시작 / 종료 설정")]
    [Tooltip("볼 변형·팽창이 완전히 끝난 뒤, 이동 시작 전 추가 대기 시간 (초).")]
    [SerializeField] private float _delayAfterExpansion = 2f;

    [Tooltip("구역 4 정지 후 캐릭터 모형 복귀까지 대기 시간 (초).")]
    [SerializeField] private float _revertDelay = 1f;

    [Header("속도 설정")]
    [Tooltip("기본 이동 속도 (유닛/초). 구역 1 및 구역 3 복귀 기준값.")]
    [SerializeField] private float _normalSpeed = 3f;

    [Tooltip("감속 구역(구역 2)의 최저 속도 (유닛/초).")]
    [SerializeField] private float _slowSpeed = 1f;

    [Header("구역 경계 (0 ~ 1, Normalized)")]
    [Tooltip("감속 시작 지점. 이 값부터 구역 2가 시작됩니다.")]
    [Range(0f, 1f)]
    [SerializeField] private float _zone2Start = 0.25f;

    [Tooltip("재가속 시작 지점. 이 값부터 구역 3이 시작됩니다.")]
    [Range(0f, 1f)]
    [SerializeField] private float _zone3Start = 0.50f;

    [Tooltip("정지 지점(참고용). 실제 정지·복귀는 tag \"Zone\" 트리거 충돌로 판정합니다.")]
    [Range(0f, 1f)]
    [SerializeField] private float _zone4Start = 0.75f;

    [Header("구역 도달 판정 (Trigger)")]
    [Tooltip("이 오브젝트(또는 Target)에 Collider + Rigidbody가 있어야 Trigger가 감지됩니다. Tag \"Zone\"인 트리거에 들어가면 해당 구역 도착으로 판정 후 정지·복귀합니다.")]
    [SerializeField] private bool _useTriggerForZoneArrival = true;

    [Header("디버그")]
    [Tooltip("켜면 시퀀스 단계·구역 전환·속도 등 세부 로그를 콘솔에 출력합니다.")]
    [SerializeField] private bool _showDebugLog = false;

    [Tooltip("이동 중 구역/진행률 로그를 출력할 최소 간격 (초). 0이면 구역 전환 시에만 출력.")]
    [SerializeField] private float _debugLogInterval = 0.5f;

    private bool _isMoving;
    private bool _isReverting;
    private bool _ballExpansionDone;
    private float _currentDistance;
    private float _t;
    private int _lastLoggedZone;
    private float _lastDebugLogTime;

    private Transform Target => _target != null ? _target : transform;

    private void Start()
    {
        if (_showDebugLog)
        {
            Debug.Log("[Chapter2_3] Start — 시퀀스 시작", this);
            if (_changeModel == null) Debug.LogWarning("[Chapter2_3] ChangeModel이 할당되지 않았습니다. 변형이 동작하지 않습니다.", this);
            if (_splineContainer == null) Debug.LogWarning("[Chapter2_3] SplineContainer가 할당되지 않았습니다. 이동 전에 할당 필요.", this);
        }
        StartCoroutine(PlaySequence());
    }

    private void Update()
    {
        if (!_isMoving || _splineContainer == null)
            return;

        float length = GetSplineLength();
        if (length <= 0f)
        {
            if (_showDebugLog) Debug.LogWarning("[Chapter2_3] Spline 길이가 0입니다. 이동 스킵.", this);
            return;
        }

        float speed = GetCurrentSpeed(_t);
        _currentDistance += speed * Time.deltaTime;
        _t = Mathf.Clamp01(_currentDistance / length);

        int zone = GetZone(_t);
        if (_showDebugLog && (zone != _lastLoggedZone || (_debugLogInterval > 0f && Time.time - _lastDebugLogTime >= _debugLogInterval)))
        {
            _lastLoggedZone = zone;
            _lastDebugLogTime = Time.time;
            float pct = _t * 100f;
            Vector3 pos = GetPositionAt(_t);
            float nextZoneRemaining = GetRemainingDistanceToNextZone(zone, length);
            string nextStr = zone < 4 ? $"다음 구역까지 {nextZoneRemaining:F2}" : "도착";
            Debug.Log($"[Chapter2_3] 구역 {zone} | t={_t:F3} ({pct:F1}%) | speed={speed:F2} | distance={_currentDistance:F2}/{length:F2} | {nextStr} | pos={pos}", this);
        }

        // 스플라인 끝(t=1) 도달 시 트리거 없어도 정지·원래 모습으로 복귀 (폴백)
        if (_t >= 1f && !_isReverting)
        {
            _currentDistance = length;
            _t = 1f;
            ApplyPositionAndRotation(1f);
            _isMoving = false;
            _isReverting = true;

            if (_showDebugLog)
                Debug.Log($"[Chapter2_3] 스플라인 끝 도달 — 이동 정지. t=1.0 → {_revertDelay}초 후 원래 모습으로 복귀", this);

            StartCoroutine(StopAndRevert());
            return;
        }

        ApplyPositionAndRotation(_t);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_useTriggerForZoneArrival || !other.CompareTag("Zone"))
            return;
        if (!_isMoving || _isReverting)
            return;

        _isMoving = false;

        if (_showDebugLog)
        {
            Vector3 pos = Target.position;
            Debug.Log($"[Chapter2_3] Zone 트리거 도달 — 이동 정지. pos={pos} → {_revertDelay}초 후 원래 모습으로 복귀", this);
        }

        _isReverting = true;
        StartCoroutine(StopAndRevert());
    }

    /// <summary>현재 t에 해당하는 구역 번호 (1~4) 반환.</summary>
    private int GetZone(float t)
    {
        if (t < _zone2Start) return 1;
        if (t < _zone3Start) return 2;
        if (t < _zone4Start) return 3;
        return 4;
    }

    /// <summary>현재 구역에서 다음 구역 경계까지 남은 거리(스플라인 연장 기준)를 반환합니다. 구역 4이면 0.</summary>
    private float GetRemainingDistanceToNextZone(int zone, float length)
    {
        float nextBoundaryDistance = zone switch
        {
            1 => _zone2Start * length,
            2 => _zone3Start * length,
            3 => _zone4Start * length,
            _ => length
        };
        float remaining = nextBoundaryDistance - _currentDistance;
        return Mathf.Max(0f, remaining);
    }

    private System.Collections.IEnumerator PlaySequence()
    {
        _ballExpansionDone = false;
        _lastLoggedZone = 0;
        _lastDebugLogTime = 0f;

        // 한 프레임 대기: 모든 MonoBehaviour.Start() 완료를 보장합니다.
        // SpherifyDeformer는 Start()에서 _initialized = true로 설정하므로,
        // 같은 프레임에 ChangeToBall()을 호출하면 초기화 전이라 TransformToSphere()가 무시됩니다.
        yield return null;

        if (_showDebugLog)
            Debug.Log("[Chapter2_3] PlaySequence 시작 (모든 Start() 완료 확인됨)", this);

        yield return StartCoroutine(WaitForBallExpansion());

        if (_showDebugLog)
            Debug.Log($"[Chapter2_3] 볼 팽창 완료. 추가 대기 {_delayAfterExpansion}초 시작", this);

        yield return new WaitForSeconds(_delayAfterExpansion);

        if (_showDebugLog)
            Debug.Log("[Chapter2_3] 추가 대기 완료. Spline 이동 시작 여부 확인 중...", this);

        if (_splineContainer == null)
        {
            Debug.LogWarning("[Chapter2_3] SplineContainer가 할당되지 않아 이동을 시작할 수 없습니다. 시퀀스 종료.", this);
            yield break;
        }

        _currentDistance = 0f;
        _t = 0f;
        _isMoving = true;

        float length = GetSplineLength();
        if (_showDebugLog)
        {
            Debug.Log($"[Chapter2_3] Spline 이동 시작. length={length:F2}, normalSpeed={_normalSpeed}, slowSpeed={_slowSpeed}, zone2/3/4 t={_zone2Start:F2}/{_zone3Start:F2}/{_zone4Start:F2}", this);
            LogZoneBoundaryPositions(length);
        }
    }

    /// <summary>
    /// ChangeModel의 볼 변형 및 팽창이 완전히 끝날 때까지 대기합니다.
    /// 내부적으로 OnBallExpansionCompleted 이벤트 하나로만 완료를 판별합니다.
    /// </summary>
    private System.Collections.IEnumerator WaitForBallExpansion()
    {
        if (_changeModel == null)
        {
            Debug.LogWarning("[Chapter2_3] ChangeModel이 할당되지 않아 변형 없이 진행합니다.", this);
            yield break;
        }

        // 이미 전환 중이면 완료될 때까지 대기 후 상태 재확인
        if (_changeModel.IsTransitioning)
        {
            if (_showDebugLog)
                Debug.Log("[Chapter2_3] ChangeModel이 이미 전환 중. 완료까지 대기...", this);
            yield return new WaitUntil(() => !_changeModel.IsTransitioning);
            if (_showDebugLog)
                Debug.Log($"[Chapter2_3] 외부 전환 완료. 현재 상태: {_changeModel.CurrentState}", this);
        }

        // 이미 Ball 상태 → 팽창까지 완료된 것으로 간주
        if (_changeModel.CurrentState == ChangeModel.ModelState.Ball)
        {
            if (_showDebugLog)
                Debug.Log("[Chapter2_3] 이미 Ball 상태. 변형 단계 건너뜀.", this);
            yield break;
        }

        if (_changeModel.CurrentState != ChangeModel.ModelState.Character)
        {
            Debug.LogWarning($"[Chapter2_3] 예상치 못한 ModelState: {_changeModel.CurrentState}. 변형 없이 진행합니다.", this);
            yield break;
        }

        // OnBallExpansionCompleted 구독 후 ChangeToBall() 호출
        // 이벤트는 ChangeModel 내부 시퀀스의 최종 단계인 BallDeform.Revert() 완료 시 발생합니다:
        //   ① StartCharacterToBall(): Bridge.SwapAll(static) + SpherifyDeformer.TransformToSphere() + CharacterDeform.Press() 동시 시작
        //   ② OnSphereCompletedForward(): 볼 오브젝트 활성화, BallDeform.Revert() (팽창 시작)
        //   ③ OnBallRevertCompletedForward(): Collider/AutoRotate 활성화, OnBallExpansionCompleted 발생  ← 여기서 대기 해제
        _changeModel.OnBallExpansionCompleted += OnBallExpansionCompleted;

        try
        {
            if (_showDebugLog)
                Debug.Log("[Chapter2_3] OnBallExpansionCompleted 구독 완료. ChangeToBall() 호출", this);

            _changeModel.ChangeToBall();

            if (_showDebugLog)
                Debug.Log("[Chapter2_3] ① ChangeToBall() 호출 완료. SpherifyDeformer.TransformToSphere() + CharacterDeform.Press() 진행 중...", this);

            yield return new WaitUntil(() => _ballExpansionDone);

            if (_showDebugLog)
                Debug.Log("[Chapter2_3] ③ BallDeform 팽창(Revert) 완료. Ball Collider·AutoRotate 활성화됨.", this);
        }
        finally
        {
            _changeModel.OnBallExpansionCompleted -= OnBallExpansionCompleted;
        }
    }

    private void OnBallExpansionCompleted()
    {
        if (_showDebugLog)
            Debug.Log("[Chapter2_3] OnBallExpansionCompleted 콜백 수신 — ② OnSphereCompletedForward → BallDeform.Revert() 완료", this);
        _ballExpansionDone = true;
    }

    /// <summary>도착(Zone 트리거) 후 _revertDelay 초 대기한 뒤 원래 모습(캐릭터)으로 복귀시킵니다.</summary>
    private System.Collections.IEnumerator StopAndRevert()
    {
        if (_showDebugLog)
            Debug.Log($"[Chapter2_3] StopAndRevert — 도착. {_revertDelay}초 후 원래 모습(캐릭터)으로 복귀 예정", this);

        yield return new WaitForSeconds(_revertDelay);

        if (_changeModel == null)
        {
            Debug.LogWarning("[Chapter2_3] ChangeModel이 없어 원래 모습으로 복귀할 수 없습니다. Inspector에서 할당하세요.", this);
            yield break;
        }

        _changeModel.ChangeToCharacter();

        if (_showDebugLog)
            Debug.Log("[Chapter2_3] ChangeToCharacter() 호출 완료 — 원래 모습(캐릭터)으로 복귀 시퀀스 진행 중.", this);
    }

    private float GetCurrentSpeed(float t)
    {
        if (t < _zone2Start)
            return _normalSpeed;

        if (t < _zone3Start)
        {
            float zoneT = Mathf.InverseLerp(_zone2Start, _zone3Start, t);
            return Mathf.Lerp(_normalSpeed, _slowSpeed, zoneT);
        }

        if (t < _zone4Start)
        {
            float zoneT = Mathf.InverseLerp(_zone3Start, _zone4Start, t);
            return Mathf.Lerp(_slowSpeed, _normalSpeed, zoneT);
        }

        return _normalSpeed;
    }

    private float GetSplineLength()
    {
        if (_splineContainer == null) return 0f;
        return _splineContainer.Spline.GetLength();
    }

    /// <summary>정규화된 t(0~1)에 해당하는 Spline 위의 월드 포지션을 반환합니다.</summary>
    private Vector3 GetPositionAt(float normalizedT)
    {
        if (_splineContainer == null) return Vector3.zero;
        _splineContainer.Evaluate(normalizedT, out float3 position, out _, out _);
        return position;
    }

    /// <summary>디버그: 구역 경계(t)별 월드 포지션을 한 번 로그합니다.</summary>
    private void LogZoneBoundaryPositions(float length)
    {
        if (!_showDebugLog || _splineContainer == null) return;
        Vector3 p0 = GetPositionAt(0f);
        Vector3 pZone2 = GetPositionAt(_zone2Start);
        Vector3 pZone3 = GetPositionAt(_zone3Start);
        Vector3 pZone4 = GetPositionAt(_zone4Start);
        Vector3 p1 = GetPositionAt(1f);
        float dZone2 = _zone2Start * length;
        float dZone3 = _zone3Start * length;
        float dZone4 = _zone4Start * length;
        Debug.Log($"[Chapter2_3] ===== 구역 경계 위치 (t → 거리 → 월드 포지션) =====\n" +
            $"  t=0.00 (시작)     | 거리=0.00/{length:F2} | pos={p0}\n" +
            $"  t={_zone2Start:F2} (구역2 시작) | 거리={dZone2:F2}/{length:F2} | pos={pZone2}\n" +
            $"  t={_zone3Start:F2} (구역3 시작) | 거리={dZone3:F2}/{length:F2} | pos={pZone3}\n" +
            $"  t={_zone4Start:F2} (구역4 시작) | 거리={dZone4:F2}/{length:F2} | pos={pZone4}\n" +
            $"  t=1.00 (끝)       | 거리={length:F2}/{length:F2} | pos={p1}", this);
    }

    private void ApplyPositionAndRotation(float normalizedT)
    {
        _splineContainer.Evaluate(normalizedT, out float3 position, out float3 tangent, out float3 up);

        Target.position = position;

        if (_rotateAlongPath && math.lengthsq(tangent) > 0.0001f)
            Target.rotation = Quaternion.LookRotation((Vector3)tangent, (Vector3)up);
    }
}
