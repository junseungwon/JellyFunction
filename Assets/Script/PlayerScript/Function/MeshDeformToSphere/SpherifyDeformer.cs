using System;
using UnityEngine;

namespace SpherifySystem
{
    /// <summary>
    /// 메시를 구형으로 변형하는 순수 실행 엔진.
    /// 수치 파라미터는 Inspector에 노출하지 않으며, Controller가 프로퍼티를 통해 설정합니다.
    /// 전환 로직(TransformToSphere/Revert)은 자체 엔진으로 동작합니다.
    /// </summary>
    public class SpherifyDeformer : MonoBehaviour
    {
        #region Inspector - 오브젝트 참조

        [Header("Debug")]
        [SerializeField] bool _showDebugLog = true;

        #endregion

        #region Private Fields - 수치 (Controller가 프로퍼티로 설정)

        bool _autoCalcRadius = true;
        float _manualRadius = 1f;
        bool _useJobSystem = true;
        float _transitionDuration = 0.5f;
        AnimationCurve _easingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        #endregion

        #region Private - Mesh State

        [SerializeField]MeshFilter       meshFilter;
        Mesh             deformMesh;
        MeshDataSnapshot snapshot;
        SpherifyJobRunner jobRunner;

        #endregion

        #region Transition State

        float _currentT    = 0f;
        float _startT      = 0f;
        float _targetT     = 0f;
        float _elapsedTime = 0f;
        bool  _isTransitioning = false;

        #endregion

        #region Events - 콜백 (Controller가 구독)

        public event Action OnSphereCompleted;
        public event Action OnRevertCompleted;

        #endregion

        #region Properties - 수치 (Controller가 설정)

        public float SpherifyAmount
        {
            get => _currentT;
            set => _currentT = Mathf.Clamp01(value);
        }

        public float CurrentAmount => _currentT;
        public float CurrentRadius { get; private set; }

        public float TransitionDuration
        {
            get => _transitionDuration;
            set => _transitionDuration = Mathf.Max(0.001f, value);
        }

        public AnimationCurve EasingCurve
        {
            get => _easingCurve;
            set => _easingCurve = value ?? AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        public float ManualRadius
        {
            get => _manualRadius;
            set => _manualRadius = Mathf.Max(0.001f, value);
        }

        public bool UseJobSystem
        {
            get => _useJobSystem;
            set => _useJobSystem = value;
        }

        public bool AutoCalcRadius
        {
            get => _autoCalcRadius;
            set => _autoCalcRadius = value;
        }

        #endregion

        #region Public API - 전환 실행

        public void TransformToSphere()
        {
            if (_showDebugLog)
                Debug.Log($"[SpherifyDeformer] 구형 전환 시작 | 소요 시간: {_transitionDuration}s");
            SetTarget(1f);
        }

        public void RevertToOriginal()
        {
            if (_showDebugLog)
                Debug.Log("[SpherifyDeformer] 원본 복원 시작");
            SetTarget(0f);
        }

        public void SetSpherifyRatio(float ratio)
        {
            if (_showDebugLog)
                Debug.Log($"[SpherifyDeformer] SpherifyRatio 설정: {ratio:F2}");
            SetTarget(Mathf.Clamp01(ratio));
        }

        public void SnapToSphere()
        {
            _currentT = _targetT = _startT = 1f;
            _elapsedTime    = _transitionDuration;
            _isTransitioning = false;

            if (_showDebugLog)
                Debug.Log("[SpherifyDeformer] 즉시 구형 전환 완료 (Snap)");
        }

        public void SnapToOriginal()
        {
            _currentT = _targetT = _startT = 0f;
            _elapsedTime    = _transitionDuration;
            _isTransitioning = false;
            ForceRevert();

            if (_showDebugLog)
                Debug.Log("[SpherifyDeformer] 즉시 원본 복원 완료 (Snap)");
        }

        #endregion

        #region Public API - Mesh

        public void ForceRevert()
        {
            if (_showDebugLog)
                Debug.Log("[SpherifyDeformer] 강제 원본 복원 시작...");

            _currentT = 0f;

            System.Array.Copy(
                snapshot.originalVertices,
                snapshot.currentVertices,
                snapshot.VertexCount
            );

            deformMesh.vertices = snapshot.currentVertices;
            deformMesh.RecalculateNormals();
            deformMesh.RecalculateBounds();

            if (_showDebugLog)
                Debug.Log("[SpherifyDeformer] 강제 원본 복원 완료");
        }

        public Vector3[] GetCurrentVerticesCopy()
        {
            Vector3[] copy = new Vector3[snapshot.VertexCount];
            System.Array.Copy(snapshot.currentVertices, copy, snapshot.VertexCount);

            if (_showDebugLog)
                Debug.Log($"[SpherifyDeformer] 버텍스 복사 완료 | 복사된 버텍스 수: {copy.Length}");

            return copy;
        }

        public Bounds GetCurrentBounds() => deformMesh.bounds;

        #endregion

        #region Unity Lifecycle

        void Awake()
        {
    
            deformMesh = meshFilter.mesh;

            CurrentRadius = _autoCalcRadius
                ? deformMesh.bounds.extents.magnitude
                : _manualRadius;

            snapshot = MeshDataSnapshot.Create(deformMesh, CurrentRadius);

            if (_useJobSystem)
                jobRunner = new SpherifyJobRunner(snapshot);

            if (_showDebugLog)
                Debug.Log($"[SpherifyDeformer] 초기화 완료 | 버텍스 수: {snapshot.VertexCount} | 반지름: {CurrentRadius:F3} | JobSystem: {_useJobSystem}");
        }

        void Update()
        {
            TickTransition();
        }

        void LateUpdate()
        {
            if (Mathf.Approximately(_currentT, 0f))
                return;

            if (_useJobSystem)
                jobRunner.Run(snapshot, _currentT);
            else
                ApplyOnCPU(_currentT);

            deformMesh.vertices = snapshot.currentVertices;
            deformMesh.RecalculateNormals();
            deformMesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            jobRunner?.Dispose();
        }

        #endregion

        #region Private - Transition

        void SetTarget(float t)
        {
            if (Mathf.Approximately(t, _targetT) && !_isTransitioning) return;

            _startT          = _currentT;
            _targetT         = t;
            _elapsedTime     = 0f;
            _isTransitioning = true;
        }

        void TickTransition()
        {
            if (!_isTransitioning) return;

            _elapsedTime += Time.deltaTime;
            float progress      = Mathf.Clamp01(_elapsedTime / _transitionDuration);
            float easedProgress = _easingCurve.Evaluate(progress);

            _currentT = Mathf.Lerp(_startT, _targetT, easedProgress);

            if (progress >= 1f)
            {
                _currentT        = _targetT;
                _isTransitioning = false;

                if (Mathf.Approximately(_targetT, 1f))
                {
                    if (_showDebugLog)
                        Debug.Log("[SpherifyDeformer] 구형 전환 완료");
                    OnSphereCompleted?.Invoke();
                }
                else if (Mathf.Approximately(_targetT, 0f))
                {
                    if (_showDebugLog)
                        Debug.Log("[SpherifyDeformer] 원본 복원 완료");
                    OnRevertCompleted?.Invoke();
                }
            }
        }

        #endregion

        #region Private - CPU Deform

        void ApplyOnCPU(float t)
        {
            for (int i = 0; i < snapshot.VertexCount; i++)
            {
                snapshot.currentVertices[i] = Vector3.Lerp(
                    snapshot.originalVertices[i],
                    snapshot.sphereTargetVertices[i],
                    t
                );
            }
        }

        #endregion
    }
}
