using System.Collections;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using CharacterPressing;

/// <summary>
/// HallWay 전용 챕터 시퀀스 스크립트.
///
/// [Phase0] 캐릭터 모드 → Spline 이동 → Chapter1 Zone 트리거 도달
/// [Phase1] idle Trigger 발동 → 볼 변환 + 팽창 대기
/// [Phase2] 볼 모드 → Spline 이동 → Chapter3 Zone 트리거 도달
/// [Phase3] 볼→캐릭터 복귀 대기 → Run Trigger 발동
/// [Phase4] 캐릭터 모드 → Spline 이동 → Chapter4 Zone 트리거 도달
/// [Done]
///
/// Zone 트리거는 Tag "Zone"인 Collider로 판정합니다.
/// </summary>
public class Jelly_HallWaySequence : MonoBehaviour
{
    #region Inspector

    [Header("캐릭터 변환")]
    [Tooltip("캐릭터 ↔ 볼 전환 담당. ChangeModel 컴포넌트가 붙은 오브젝트를 할당하세요.")]
    [SerializeField] private ChangeModel _changeModel = null;

    [Tooltip("idle / Run 파라미터(Trigger)가 있는 Animator")]
    [SerializeField] private Animator _animator = null;

    [Tooltip("idle Trigger 파라미터 이름")]
    [SerializeField] private string _idleTrigger = "idle";

    [Tooltip("Run Trigger 파라미터 이름")]
    [SerializeField] private string _runTrigger = "Run";

    [Header("Spline 경로")]
    [Tooltip("이동 경로가 정의된 SplineContainer (처음부터 끝까지 하나의 연속 Spline).")]
    [SerializeField] private SplineContainer _splineContainer = null;

    [Tooltip("이동시킬 대상. 비워두면 이 컴포넌트가 붙은 오브젝트가 이동합니다.")]
    [SerializeField] private Transform _target = null;

    [Tooltip("이동 시 경로 방향(접선)을 바라보도록 회전할지 여부.")]
    [SerializeField] private bool _rotateAlongPath = true;

    [Header("속도 / 대기 설정")]
    [Tooltip("모든 이동 구간의 기본 속도 (유닛/초).")]
    [SerializeField] private float _moveSpeed = 3f;

    [Tooltip("볼 팽창 완료 후 이동 시작 전 추가 대기 시간 (초).")]
    [SerializeField] private float _delayAfterExpansion = 1f;

    [Tooltip("캐릭터 완전 복귀(SpherifyDeformer 포함) 후 이동 시작 전 대기 시간 (초).")]
    [SerializeField] private float _delayAfterRevert = 1f;

    [Header("디버그")]
    [Tooltip("켜면 각 Phase 전환 및 이동 상태를 콘솔에 출력합니다.")]
    [SerializeField] private bool _showDebugLog = false;

    #endregion

    #region Private Fields (추가)

    private bool _characterRestoreDone = false;

    #endregion

    #region Phase Enum

    private enum Phase
    {
        Phase0_Move,       // 캐릭터 모드, Chapter0 → Chapter1 이동
        Phase1_Transform,  // idle Trigger + ChangeToBall + 팽창 대기
        Phase2_Move,       // 볼 모드, Chapter1 → Chapter3 이동
        Phase3_Transform,  // ChangeToCharacter 복귀 대기 + Run Trigger
        Phase4_Move,       // 캐릭터 모드, Chapter3 → Chapter4 이동
        Done
    }

    #endregion

    #region Private Fields

    private Phase _currentPhase = Phase.Phase0_Move;
    private bool _isMoving = false;
    private float _currentDistance = 0f;
    private float _t = 0f;

    private bool _ballExpansionDone = false;

    private Transform Target => _target != null ? _target : transform;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (_showDebugLog)
        {
            Debug.Log("[HallWaySeq] Start — Phase0 시작. 캐릭터 → Chapter1 방향 이동 준비.", this);
            if (_changeModel == null) Debug.LogWarning("[HallWaySeq] ChangeModel이 할당되지 않았습니다.", this);
            if (_animator == null) Debug.LogWarning("[HallWaySeq] Animator가 할당되지 않았습니다.", this);
            if (_splineContainer == null) Debug.LogWarning("[HallWaySeq] SplineContainer가 할당되지 않았습니다.", this);
        }

        StartCoroutine(StartPhase0());
    }

    private void Update()
    {
        if (!_isMoving || _splineContainer == null) return;

        float length = GetSplineLength();
        if (length <= 0f) return;

        _currentDistance += _moveSpeed * Time.deltaTime;
        _t = Mathf.Clamp01(_currentDistance / length);

        ApplyPositionAndRotation(_t);

        if (_showDebugLog && Time.frameCount % 30 == 0)
            Debug.Log($"[HallWaySeq] {_currentPhase} | t={_t:F3} | dist={_currentDistance:F2}/{length:F2} | pos={Target.position}", this);
    }

    /// <summary>
    /// 외부 Zone 트리거에서 호출합니다.
    /// 현재 Phase에 따라 다음 단계로 전환합니다.
    /// HallWayZoneTrigger 컴포넌트를 Box Collider 오브젝트에 붙이고 이 메서드를 연결하세요.
    /// </summary>
    public void OnZoneReached()
    {
        switch (_currentPhase)
        {
            case Phase.Phase0_Move:
                if (!_isMoving) return;
                _isMoving = false;
                Debug.LogError("[HallWaySeq] Zone 진입: Chapter1 도달 → Phase1_Transform 시작", this);
                _currentPhase = Phase.Phase1_Transform;
                StartCoroutine(Phase1_TransformSequence());
                break;

            case Phase.Phase2_Move:
                if (!_isMoving) return;
                _isMoving = false;
                Debug.LogError("[HallWaySeq] Zone 진입: Chapter3 도달 → Phase3_Transform 시작", this);
                _currentPhase = Phase.Phase3_Transform;
                StartCoroutine(Phase3_TransformSequence());
                break;

            case Phase.Phase4_Move:
                if (!_isMoving) return;
                _isMoving = false;
                _currentPhase = Phase.Done;
                Debug.LogError("[HallWaySeq] Zone 진입: Chapter4 도달 → 시퀀스 완료 (Done)", this);
                break;

            default:
                if (_showDebugLog)
                    Debug.Log($"[HallWaySeq] OnZoneReached 수신 — 현재 Phase({_currentPhase})에서는 무시됨.", this);
                break;
        }
    }

    #endregion

    #region Phase Coroutines

    private IEnumerator StartPhase0()
    {
        // 모든 MonoBehaviour.Start() 완료를 보장하는 한 프레임 대기
        yield return null;

        if (_splineContainer == null)
        {
            Debug.LogWarning("[HallWaySeq] SplineContainer가 없어 시퀀스를 시작할 수 없습니다.", this);
            yield break;
        }

        _currentDistance = 0f;
        _t = 0f;
        ApplyPositionAndRotation(0f);

        _currentPhase = Phase.Phase0_Move;
        _isMoving = true;

        if (_showDebugLog) Debug.Log($"[HallWaySeq] Phase0 이동 시작. SplineLength={GetSplineLength():F2}, speed={_moveSpeed}", this);
    }

    /// <summary>Phase1: idle Trigger → ChangeToBall → 팽창 대기 → Phase2 이동 시작</summary>
    private IEnumerator Phase1_TransformSequence()
    {
        if (_showDebugLog) Debug.Log("[HallWaySeq] Phase1: idle Trigger 발동", this);
        _animator?.SetTrigger(_idleTrigger);

        yield return StartCoroutine(WaitForBallExpansion());

        if (_showDebugLog) Debug.Log($"[HallWaySeq] Phase1 완료. {_delayAfterExpansion}초 대기 후 Phase2 이동 시작", this);
        yield return new WaitForSeconds(_delayAfterExpansion);

        _currentPhase = Phase.Phase2_Move;
        _isMoving = true;

        if (_showDebugLog) Debug.Log("[HallWaySeq] Phase2 이동 시작 (볼 모드 → Chapter3)", this);
    }

    /// <summary>Phase3: ChangeToCharacter → SpherifyDeformer 완전 복귀 대기 → 1초 후 Run Trigger → Phase4 이동 시작</summary>
    private IEnumerator Phase3_TransformSequence()
    {
        if (_changeModel == null)
        {
            Debug.LogWarning("[HallWaySeq] ChangeModel이 없어 캐릭터 복귀를 건너뜁니다.", this);
        }
        else
        {
            _characterRestoreDone = false;
            _changeModel.OnCharacterRestoreCompleted += OnCharacterRestoreCompleted;

            try
            {
                if (_showDebugLog) Debug.Log("[HallWaySeq] Phase3: ChangeToCharacter() 호출 — SpherifyDeformer 복귀 완료까지 대기", this);
                _changeModel.ChangeToCharacter();
                yield return new WaitUntil(() => _characterRestoreDone);
                if (_showDebugLog) Debug.Log("[HallWaySeq] Phase3: 캐릭터 완전 복귀 완료 (SpherifyDeformer 포함)", this);
            }
            finally
            {
                _changeModel.OnCharacterRestoreCompleted -= OnCharacterRestoreCompleted;
            }
        }

        if (_showDebugLog) Debug.Log($"[HallWaySeq] Phase3: {_delayAfterRevert}초 대기 후 Run Trigger 발동 및 Phase4 이동", this);
        yield return new WaitForSeconds(_delayAfterRevert);

        if (_showDebugLog) Debug.Log("[HallWaySeq] Phase3: Run Trigger 발동", this);
        _animator?.SetTrigger(_runTrigger);

        _currentPhase = Phase.Phase4_Move;
        _isMoving = true;

        if (_showDebugLog) Debug.Log("[HallWaySeq] Phase4 이동 시작 (캐릭터 모드 → Chapter4)", this);
    }

    private void OnCharacterRestoreCompleted() => _characterRestoreDone = true;

    #endregion

    #region Ball Expansion Wait (Jelly_Chapter2_3 패턴 재사용)

    private IEnumerator WaitForBallExpansion()
    {
        _ballExpansionDone = false;

        if (_changeModel == null)
        {
            Debug.LogWarning("[HallWaySeq] ChangeModel이 없어 변형 없이 진행합니다.", this);
            yield break;
        }

        if (_changeModel.IsTransitioning)
        {
            if (_showDebugLog) Debug.Log("[HallWaySeq] ChangeModel 전환 중. 완료 대기...", this);
            yield return new WaitUntil(() => !_changeModel.IsTransitioning);
        }

        if (_changeModel.CurrentState == ChangeModel.ModelState.Ball)
        {
            if (_showDebugLog) Debug.Log("[HallWaySeq] 이미 Ball 상태. 변형 단계 건너뜀.", this);
            yield break;
        }

        if (_changeModel.CurrentState != ChangeModel.ModelState.Character)
        {
            Debug.LogWarning($"[HallWaySeq] 예상치 못한 상태: {_changeModel.CurrentState}. 변형 없이 진행합니다.", this);
            yield break;
        }

        _changeModel.OnBallExpansionCompleted += OnBallExpansionCompleted;

        try
        {
            if (_showDebugLog) Debug.Log("[HallWaySeq] ChangeToBall() 호출 — SpherifyDeformer + CharacterDeform.Press() 진행 중...", this);
            _changeModel.ChangeToBall();
            yield return new WaitUntil(() => _ballExpansionDone);
            if (_showDebugLog) Debug.Log("[HallWaySeq] 볼 팽창 완료. Collider·AutoRotate 활성화됨.", this);
        }
        finally
        {
            _changeModel.OnBallExpansionCompleted -= OnBallExpansionCompleted;
        }
    }

    private void OnBallExpansionCompleted() => _ballExpansionDone = true;

    #endregion

    #region Spline Helpers

    private float GetSplineLength()
    {
        if (_splineContainer == null) return 0f;
        return _splineContainer.Spline.GetLength();
    }

    private void ApplyPositionAndRotation(float normalizedT)
    {
        if (_splineContainer == null) return;
        _splineContainer.Evaluate(normalizedT, out float3 position, out float3 tangent, out float3 up);

        Target.position = position;

        if (_rotateAlongPath && math.lengthsq(tangent) > 0.0001f)
            Target.rotation = Quaternion.LookRotation((Vector3)tangent, (Vector3)up);
    }

    #endregion
}
