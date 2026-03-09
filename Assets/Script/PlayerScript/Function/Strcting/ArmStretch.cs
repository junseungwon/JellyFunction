using UnityEngine;
using System.Collections;

public class ArmStretch : MonoBehaviour
{
    [Header("References")]
    [Tooltip("팔 시작점(어깨) 위치 Transform")]
    [SerializeField] private Transform  armOrigin;
    [Tooltip("손/목표물 Transform (경로 끝점)")]
    [SerializeField] private Transform  target;
    [Tooltip("튜브 메시를 그릴 TubeMeshBuilder 컴포넌트")]
    [SerializeField] private TubeMeshBuilder tubeMeshBuilder;

    [Header("Bezier Settings")]
    [Tooltip("베지어 제어점을 origin-target 중간에서 위로 올리는 높이")]
    [SerializeField] private float controlPointUpOffset = 1.5f;
    [Tooltip("제어점 오프셋 각도(도). 0=위쪽, 양수=타겟 방향으로 휘어짐, 음수=원점 방향으로 휘어짐")]
    [SerializeField] private float controlPointAngleDegrees = 0f;
    [Tooltip("경로 구간 수. 실제 경로 점 개수 = pathSamples + 1")]
    [SerializeField] private int   pathSamples          = 12;
    [Tooltip("경로 구간 수의 최소값. pathSamples가 이 값보다 작으면 이 값으로 올립니다")]
    [SerializeField] private int   _minPathSamples      = 2;

    [Header("Path Orientation")]
    [Tooltip("베지어 곡선이 '위로' 휘어질 기준 Up 방향을 월드가 아니라 로컬 기준으로 사용할지 여부")]
    [SerializeField] private bool _useLocalUp = true;
    [Tooltip("로컬 Up 기준이 될 Transform. 비우면 armOrigin의 Up을 사용")]
    [SerializeField] private Transform _upReference;

    [Header("Validation")]
    [Tooltip("경로가 막혀 있는지 검사할 레이어 (여기 포함된 콜라이더에 닿으면 막힘)")]
    [SerializeField] private LayerMask obstacleLayer;

    [Header("Stretch Input")]
    [Tooltip("팔 늘리기 시작/해제에 사용할 키")]
    [SerializeField] private KeyCode stretchKey = KeyCode.Mouse0;

    [Header("Mesh Timing")]
    [Tooltip("버튼을 뗀 후 튜브 메시가 사라지기까지 대기 시간(초). 0이면 즉시 제거, 2면 2초 후 제거")]
    [SerializeField] private float _meshClearDelay = 2f;
    [Tooltip("켜면 버튼 한 번 누를 때마다 구간 간격으로 천천히 끝까지 그려짐. 끄면 한 번에 전체 생성")]
    [SerializeField] private bool _useGradualMeshGrowth = true;
    [Tooltip("점진 생성 시 기본 구간(다음 지점) 사이 대기 시간(초)")]
    [SerializeField] private float _meshGrowInterval = 0.1f;
    [Tooltip("메시가 늘어날 때 속도 곡선 (X: 진행도 0~1, Y: 딜레이 배율)")]
    [SerializeField] private AnimationCurve _meshGrowCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    [Tooltip("메시가 줄어들 때 기본 구간(이전 지점) 사이 대기 시간(초)")]
    [SerializeField] private float _meshShrinkInterval = 0.1f;
    [Tooltip("메시가 줄어들 때 속도 곡선 (X: 진행도 0~1, Y: 딜레이 배율)")]
    [SerializeField] private AnimationCurve _meshShrinkCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    [Header("Hand Model")]
    [Tooltip("팔 끝에 표시할 손(또는 말단) 모델 Transform")]
    [SerializeField] private Transform _handModel;
    [Tooltip("켜면 튜브가 늘어나는 베지어 경로를 따라 손 모델도 함께 이동")]
    [SerializeField] private bool _moveHandAlongPath = true;

    [Header("Grab Settings")]
    [Tooltip("켜면 팔이 완전히 뻗었을 때 target 오브젝트를 잡아 끌어당김")]
    [SerializeField] private bool _enableGrab = true;

    [Header("Debug")]
        [Tooltip("경로·제어점 디버그 라인 유지 시간(초)")]
    [SerializeField] private float _pathDrawDuration = 10f;
    [Tooltip("베지어 제어점·곡선 시각화")]
    [SerializeField] private bool _drawBezierCurve = true;
    [Tooltip("origin/target/controlPoint 위치에 축 마커 표시")]
    [SerializeField] private bool _drawPathPointMarkers = true;
    [Tooltip("막힌 구간 시 충돌 지점·로그 출력")]
    [SerializeField] private bool _drawPathWithHitInfo = true;
    [Tooltip("경로 검사 상세 로그")]
    [SerializeField] private bool _enableLog = true;
    [Tooltip("TryStretch 시 origin/targetPos/controlPoint/pathPoints 값 콘솔 출력")]
    [SerializeField] private bool _logPathValues = true;
    // 현재 팔 상태
    private enum ArmState { Idle, Stretching, Grabbed }
    private ArmState _state = ArmState.Idle;

    private Vector3[] _currentPath;
    private float _clearMeshTimer = -1f;
    private Transform _grabbedObject = null;
    private Coroutine _growMeshCoroutine = null;
    private bool _isGrowing = false;
    private Coroutine _shrinkMeshCoroutine = null;
    private bool _isShrinking = false;
    /// <summary>키를 뗐을 때 성장 중이면 true. 성장 완료 후 한 번만 수축/클리어 실행.</summary>
    private bool _retractPending = false;

    private void SetHandTransform(Vector3 position, Vector3 forward, bool active)
    {
        if (_handModel == null) return;
        _handModel.gameObject.SetActive(active);
        _handModel.position = position;
        if (forward != Vector3.zero)
            _handModel.rotation = Quaternion.LookRotation(forward);
    }

    private void Update()
    {
        if (Input.GetKeyDown(stretchKey))
        {
            _clearMeshTimer = -1f;
            if (_shrinkMeshCoroutine != null)
            {
                StopCoroutine(_shrinkMeshCoroutine);
                _shrinkMeshCoroutine = null;
                _isShrinking = false;
                if (tubeMeshBuilder != null)
                    tubeMeshBuilder.ClearMesh();
            }
            TryStretch();
        }

        if (Input.GetKeyUp(stretchKey))
            RetractArm();

        if (_state == ArmState.Stretching)
            UpdateStretch();

        if (_clearMeshTimer >= 0f)
        {
            _clearMeshTimer -= Time.deltaTime;
            if (_clearMeshTimer <= 0f)
            {
                _clearMeshTimer = -1f;
                if (tubeMeshBuilder != null)
                {
                    if (_useGradualMeshGrowth && _currentPath != null && _currentPath.Length > 2)
                        _shrinkMeshCoroutine = StartCoroutine(ShrinkMeshCoroutine(_currentPath));
                    else
                        tubeMeshBuilder.ClearMesh();
                }
                if (_moveHandAlongPath && _handModel != null && _shrinkMeshCoroutine == null)
                    SetHandTransform(armOrigin.position, Vector3.forward, false);
                if (_enableLog && _shrinkMeshCoroutine == null)
                    Debug.Log("ArmStretch: 메시 제거 (지연 리셋)");
            }
        }
    }

    private void TryStretch()
    {
        if (target == null) return;

        Vector3 origin       = armOrigin.position;
        Vector3 targetPos    = target.position;
        // 베지어가 휘어질 기준 Up 방향 (월드/로컬 선택)
        Vector3 upDir = Vector3.up;
        if (_useLocalUp)
        {
            Transform refT = _upReference != null ? _upReference : armOrigin;
            if (refT != null)
                upDir = refT.up;
        }
        Vector3 controlPoint = BezierPath.GetAutoControlPoint(origin, targetPos, upDir, controlPointUpOffset, _logPathValues, controlPointAngleDegrees);

        int effectiveSamples = Mathf.Max(_minPathSamples, pathSamples);
        // 1. 베지어 경로 샘플링
        Vector3[] pathPoints = BezierPath.SamplePath(origin, controlPoint, targetPos, effectiveSamples, _logPathValues);

        // 2. 디버그 시각화
        if (_drawBezierCurve)
            BezierPath.DrawDebugCurve(origin, controlPoint, targetPos, effectiveSamples, _pathDrawDuration, _logPathValues);
        if (_drawPathPointMarkers)
        {
            DrawDebugPoint(origin, Color.green, 0.15f, _pathDrawDuration);   // 어깨
            DrawDebugPoint(targetPos, Color.red, 0.15f, _pathDrawDuration); // 손 타겟
            DrawDebugPoint(controlPoint, Color.yellow, 0.15f, _pathDrawDuration); // 제어점

            // 경로 샘플의 첫 시작 지점(메시/손이 실제로 따라가기 시작하는 위치)을 별도 표시
            if (pathPoints != null && pathPoints.Length > 0)
            {
                // origin과 거의 같겠지만, 샘플된 첫 포인트를 확인하고 싶을 때 도움이 됨
                Vector3 firstSample = pathPoints[0];
                DrawDebugPoint(firstSample, Color.cyan, 0.12f, _pathDrawDuration);

                // 콘솔 디버그: 경로 첫 샘플 지점 정보
                if (_enableLog)
                    Debug.Log($"ArmStretch Debug: 첫 경로 샘플 지점 pathPoints[0]={firstSample}, length={pathPoints.Length}");
            }
        }
        if (_drawPathWithHitInfo)
            PathValidator.DrawDebugPathWithHitInfo(pathPoints, obstacleLayer, _pathDrawDuration);
        else
            PathValidator.DrawDebugPath(pathPoints, obstacleLayer, _pathDrawDuration);

        // 2-1. 경로 관련 값 콘솔 출력
        if (_logPathValues)
            LogPathValues(origin, targetPos, controlPoint, effectiveSamples, pathPoints);

        // 3. 경로 유효성 검사
        if (!PathValidator.IsPathClear(pathPoints, obstacleLayer))
        {
            int blockedIdx = PathValidator.GetBlockedIndex(pathPoints, obstacleLayer);
            if (_enableLog)
                Debug.Log($"ArmStretch: 경로가 막혀 있어 팔을 늘릴 수 없습니다. (막힌 구간 인덱스: {blockedIdx})");
            return;
        }

        // 4. 경로 유효 → 상태 전환 및 경로 저장
        _currentPath = pathPoints;
        _state       = ArmState.Stretching;

        // 손 모델 초기 위치/회전 세팅 (시작점)
        if (_moveHandAlongPath && _handModel != null && _currentPath.Length >= 2)
        {
            Vector3 start = _currentPath[0];
            Vector3 next  = _currentPath[1];
            SetHandTransform(start, (next - start).normalized, true);
        }

        if (tubeMeshBuilder != null)
        {
            if (_growMeshCoroutine != null)
            {
                StopCoroutine(_growMeshCoroutine);
                _growMeshCoroutine = null;
            }
            if (_useGradualMeshGrowth)
                _growMeshCoroutine = StartCoroutine(GrowMeshCoroutine(pathPoints));
            else
                tubeMeshBuilder.UpdateMesh(_currentPath);
        }

        if (_enableLog)
            Debug.Log("ArmStretch: 경로 유효 (튜브 메시 생성)");
    }

    private void TryGrab()
    {
        if (target == null) return;
        _grabbedObject = target;
        _state = ArmState.Grabbed;
        if (_enableLog)
            Debug.Log($"ArmStretch: [{target.name}] 잡음");
    }

    private void ReleaseGrab()
    {
        if (_grabbedObject == null) return;
        if (_enableLog)
            Debug.Log($"ArmStretch: [{_grabbedObject.name}] 놓음 (플레이어 도착)");
        _grabbedObject = null;
    }

    /// <summary>디버그: 위치에 XYZ 축 마커를 그립니다.</summary>
    private void DrawDebugPoint(Vector3 position, Color color, float size, float duration)
    {
        Debug.DrawLine(position - Vector3.right * size, position + Vector3.right * size, color, duration);
        Debug.DrawLine(position - Vector3.up * size, position + Vector3.up * size, color, duration);
        Debug.DrawLine(position - Vector3.forward * size, position + Vector3.forward * size, color, duration);
    }

    /// <summary>디버그: 경로 계산에 사용된 값들을 콘솔에 출력합니다.</summary>
    private void LogPathValues(Vector3 origin, Vector3 targetPos, Vector3 controlPoint, int pathSamples, Vector3[] pathPoints)
    {
        int last = pathPoints != null && pathPoints.Length > 0 ? pathPoints.Length - 1 : 0;
        string msg = $"[ArmStretch 경로 값]\n" +
            $"  origin(어깨)    = {origin}\n" +
            $"  targetPos(손)  = {targetPos}\n" +
            $"  controlPoint   = {controlPoint}\n" +
            $"  controlPointUpOffset = {controlPointUpOffset}\n" +
            $"  pathSamples    = {pathSamples}\n" +
            $"  pathPoints.Length = {pathPoints?.Length ?? 0}\n";
        if (pathPoints != null && pathPoints.Length > 0)
        {
            msg += $"  pathPoints[0]  = {pathPoints[0]}\n";
            if (pathPoints.Length > 1)
                msg += $"  pathPoints[{last}] = {pathPoints[last]}";
        }
        Debug.Log(msg);
    }

    /// <summary>
    /// 경로를 2점 → 3점 → … → 전체 로 늘려가며 메시를 구간마다 _meshGrowInterval * 커브값 간격으로 갱신합니다.
    /// </summary>
    private IEnumerator GrowMeshCoroutine(Vector3[] pathPoints)
    {
        _isGrowing = true;
        if (pathPoints == null || pathPoints.Length < 2)
        {
            _isGrowing = false;
            yield break;
        }
        yield return null;

        int totalSteps = pathPoints.Length - 1; // 2점부터 시작하므로 -1

        for (int count = 2; count <= pathPoints.Length; count++)
        {
            Vector3[] slice = new Vector3[count];
            for (int i = 0; i < count; i++)
                slice[i] = pathPoints[i];
            if (tubeMeshBuilder != null)
                tubeMeshBuilder.UpdateMesh(slice);

            // 손 모델을 현재 튜브 끝으로 이동
            if (_moveHandAlongPath && _handModel != null && count >= 2)
            {
                Vector3 tip     = slice[count - 1];
                Vector3 tipPrev = slice[count - 2];
                SetHandTransform(tip, (tip - tipPrev).normalized, true);
            }

            // 진행도(0~1)에 따라 커브 값으로 딜레이를 조절
            float t = (float)(count - 1) / totalSteps;
            float curveMultiplier = _meshGrowCurve != null ? _meshGrowCurve.Evaluate(t) : 1f;
            float wait = _meshGrowInterval * Mathf.Max(0f, curveMultiplier);

            yield return new WaitForSeconds(wait);
        }
        _isGrowing = false;
        _growMeshCoroutine = null;

        // 모든 구간까지 메시 생성 완료 디버그 로그
        if (_enableLog)
            Debug.LogWarning("ArmStretch: 메시 전체 생성 완료 (경로 끝까지 도달)");

        if (_enableGrab)
            TryGrab();

        // 키를 뗀 상태로 생성이 끝났으면 이 시점에 수축/클리어 실행
        if (_retractPending)
        {
            _retractPending = false;
            DoRetractEffect();
        }
    }

    /// <summary>
    /// 경로를 끝에서부터 한 구간씩 줄여가며 메시를 _meshShrinkInterval * 커브값 간격으로 역순 제거합니다.
    /// </summary>
    private IEnumerator ShrinkMeshCoroutine(Vector3[] pathPoints)
    {
        _isShrinking = true;
        if (pathPoints == null || pathPoints.Length < 2)
        {
            if (tubeMeshBuilder != null)
                tubeMeshBuilder.ClearMesh();
            _isShrinking = false;
            _shrinkMeshCoroutine = null;
            if (_moveHandAlongPath && _handModel != null)
                SetHandTransform(armOrigin.position, Vector3.forward, false);
            yield break;
        }
        int totalSteps = pathPoints.Length - 1;

        for (int count = pathPoints.Length - 1; count >= 2; count--)
        {
            Vector3[] slice = new Vector3[count];
            for (int i = 0; i < count; i++)
                slice[i] = pathPoints[i];
            if (tubeMeshBuilder != null)
                tubeMeshBuilder.UpdateMesh(slice);

            // 손 모델을 현재 튜브 끝으로 이동 (역순)
            if (_moveHandAlongPath && _handModel != null && count >= 2)
            {
                Vector3 tip     = slice[count - 1];
                Vector3 tipPrev = slice[count - 2];
                SetHandTransform(tip, (tip - tipPrev).normalized, true);
            }

            // 잡은 오브젝트를 현재 팔 끝에 붙여서 이동
            if (_grabbedObject != null && count >= 2)
                _grabbedObject.position = slice[count - 1];

            // 진행도(0~1)에 따라 커브 값으로 딜레이를 조절 (역순 제거)
            float t = (float)(count - 1) / totalSteps;
            float curveMultiplier = _meshShrinkCurve != null ? _meshShrinkCurve.Evaluate(t) : 1f;
            float wait = _meshShrinkInterval * Mathf.Max(0f, curveMultiplier);

            yield return new WaitForSeconds(wait);
        }
        if (tubeMeshBuilder != null)
            tubeMeshBuilder.ClearMesh();
        _isShrinking = false;
        _shrinkMeshCoroutine = null;
        if (_moveHandAlongPath && _handModel != null)
            SetHandTransform(armOrigin.position, Vector3.forward, false);
        ReleaseGrab();
        if (_enableLog)
            Debug.Log("ArmStretch: 메시 역순 제거 완료");
    }

    private void UpdateStretch()
    {
        if (target == null) return;
        if (_isGrowing) return;

        Vector3 origin       = armOrigin.position;
        Vector3 targetPos    = target.position;
        // 베지어가 휘어질 기준 Up 방향 (월드/로컬 선택)
        Vector3 upDir = Vector3.up;
        if (_useLocalUp)
        {
            Transform refT = _upReference != null ? _upReference : armOrigin;
            if (refT != null)
                upDir = refT.up;
        }
        Vector3 controlPoint = BezierPath.GetAutoControlPoint(origin, targetPos, upDir, controlPointUpOffset, _logPathValues, controlPointAngleDegrees);

        int effectiveSamples = Mathf.Max(_minPathSamples, pathSamples);
        _currentPath = BezierPath.SamplePath(origin, controlPoint, targetPos, effectiveSamples, _logPathValues);
        if (_drawBezierCurve)
            BezierPath.DrawDebugCurve(origin, controlPoint, targetPos, effectiveSamples, _pathDrawDuration, _logPathValues);
        if (_drawPathWithHitInfo)
            PathValidator.DrawDebugPathWithHitInfo(_currentPath, obstacleLayer, _pathDrawDuration);
        else
            PathValidator.DrawDebugPath(_currentPath, obstacleLayer, _pathDrawDuration);
        if (tubeMeshBuilder != null)
            tubeMeshBuilder.UpdateMesh(_currentPath);

        // 지속적으로 목표를 향해 경로를 조정하는 동안에도 손 모델을 경로 끝으로 붙여 줌
        if (_moveHandAlongPath && _handModel != null && _currentPath != null && _currentPath.Length >= 2)
        {
            Vector3 tip     = _currentPath[_currentPath.Length - 1];
            Vector3 tipPrev = _currentPath[_currentPath.Length - 2];
            SetHandTransform(tip, (tip - tipPrev).normalized, true);
        }
    }

    private void RetractArm()
    {
        _state = ArmState.Idle;

        // 점진 생성 중이면 수축은 성장이 끝난 뒤에 실행 (생성되다 말리는 현상 방지)
        if (_useGradualMeshGrowth && _isGrowing)
        {
            _retractPending = true;
            if (_enableLog)
                Debug.Log("ArmStretch: 키 뗌 — 메시 전체 생성 완료 후 수축 예정");
            return;
        }

        DoRetractEffect();
    }

    /// <summary>수축/클리어 실행 (_meshClearDelay에 따라 즉시 수축 또는 타이머 시작)</summary>
    private void DoRetractEffect()
    {
        if (_meshClearDelay <= 0f)
        {
            if (tubeMeshBuilder != null)
            {
                if (_useGradualMeshGrowth && _currentPath != null && _currentPath.Length > 2)
                    _shrinkMeshCoroutine = StartCoroutine(ShrinkMeshCoroutine(_currentPath));
                else
                    tubeMeshBuilder.ClearMesh();
            }
            if (_moveHandAlongPath && _handModel != null && _shrinkMeshCoroutine == null)
                SetHandTransform(armOrigin.position, Vector3.forward, false);
            if (_enableLog && _shrinkMeshCoroutine == null)
                Debug.Log("ArmStretch: 팔 수축");
        }
        else
        {
            _clearMeshTimer = _meshClearDelay;
            if (_enableLog)
                Debug.Log($"ArmStretch: 팔 수축 (메시 {_meshClearDelay}초 후 역순 제거)");
        }
    }
}
