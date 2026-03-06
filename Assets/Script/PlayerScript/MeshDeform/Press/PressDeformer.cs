using UnityEngine;

namespace PressSystem
{
    /// <summary>프레스로 눌리는 축 (해당 축 방향으로 압축)</summary>
    public enum PressAxis
    {
        X,
        Y,
        Z
    }

    /// <summary>프레스가 들어오는 방향. 한쪽만 눌리고 반대쪽은 유지됩니다.</summary>
    public enum PressFrom
    {
        /// <summary>+축 방향에서 눌림 (해당 축 양수 쪽 버텍스만 압축)</summary>
        Positive,
        /// <summary>-축 방향에서 눌림 (해당 축 음수 쪽 버텍스만 압축)</summary>
        Negative
    }

    [RequireComponent(typeof(MeshFilter))]
    public class PressDeformer : MonoBehaviour
    {
        [Header("Press Settings")]
        [Tooltip("눌리는 방향(축). 해당 축 좌표가 최저지점으로 수렴합니다.")]
        [SerializeField] PressAxis _pressAxis = PressAxis.Y;
        [Tooltip("프레스가 들어오는 쪽. Positive=+축에서 눌림(위/앞/오른쪽), Negative=-축에서 눌림")]
        [SerializeField] PressFrom _pressFrom = PressFrom.Positive;

        [Header("Press Floor (최저지점)")]
        [Tooltip("ON: 메시 bounds 중심을 최저지점으로 자동 계산 (눌리는 쪽의 50%)\nOFF: 아래 비율 값을 사용")]
        [SerializeField] bool _autoFloor = true;
        [Tooltip("0~1 비율로 최저지점 지정. 메시 축 범위 내 상대적 위치.\n" +
                 "[Positive 기준] 0=완전 납작, 0.3=살짝 눌림, 0.7=중간, 1=눌림 없음\n" +
                 "[Negative 기준] 0=눌림 없음, 0.3=중간, 0.7=살짝 눌림, 1=완전 납작")]
        [Range(0f, 1f)]
        [SerializeField] float _pressFloorRatio = 0.5f;

        [Header("Lateral Spread (옆으로 퍼짐)")]
        [Tooltip("눌릴 때 수직 축으로 퍼지는 강도. 0=퍼짐 없음, 1=최대 팽창")]
        [Range(0f, 1f)]
        [SerializeField] float _lateralSpread = 0.3f;

        [Header("Performance")]
        [SerializeField] bool _useJobSystem = true;

        [Header("Debug")]
        [SerializeField] bool _showDebugLog = true;

        MeshFilter _meshFilter = null;
        Mesh _deformMesh = null;
        PressMeshSnapshot _snapshot = null;
        PressJobRunner _jobRunner = null;

        /// <summary>0~1: 눌림 강도 (Controller에서 설정)</summary>
        public float PressAmount { get; set; } = 0f;

        void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _deformMesh = _meshFilter.mesh;

            float? absoluteFloor = _autoFloor ? null : (float?)RatioToAbsolute(_deformMesh.bounds, _pressAxis, _pressFloorRatio);
            float floorVal = absoluteFloor ?? GetAxisValue(_deformMesh.bounds.center, _pressAxis);

            _snapshot = PressMeshSnapshot.Create(_deformMesh, _pressAxis, _pressFrom, absoluteFloor, _lateralSpread);

            if (_useJobSystem)
                _jobRunner = new PressJobRunner(_snapshot);

            if (_showDebugLog)
                Debug.Log($"[PressDeformer] 초기화 완료 | 버텍스 수: {_snapshot.VertexCount} | 축: {_pressAxis} | 방향: {_pressFrom} | 최저지점(절대): {floorVal:F4} | 팽창: {_lateralSpread:F2} | JobSystem: {_useJobSystem}");
        }

        /// <summary>0~1 비율을 해당 축의 절대 좌표로 변환</summary>
        static float RatioToAbsolute(Bounds bounds, PressAxis axis, float ratio)
        {
            float axisMin = GetAxisValue(bounds.min, axis);
            float axisMax = GetAxisValue(bounds.max, axis);
            return axisMin + Mathf.Clamp01(ratio) * (axisMax - axisMin);
        }

        static float GetAxisValue(Vector3 v, PressAxis axis)
        {
            if (axis == PressAxis.X) return v.x;
            if (axis == PressAxis.Z) return v.z;
            return v.y;
        }

        void LateUpdate()
        {
            if (Mathf.Approximately(PressAmount, 0f))
                return;

            if (_useJobSystem)
                _jobRunner.Run(_snapshot, PressAmount);
            else
                ApplyOnCPU(PressAmount);

            _deformMesh.vertices = _snapshot.currentVertices;
            _deformMesh.RecalculateNormals();
            _deformMesh.RecalculateBounds();
        }

        void ApplyOnCPU(float t)
        {
            for (int i = 0; i < _snapshot.VertexCount; i++)
            {
                _snapshot.currentVertices[i] = Vector3.Lerp(
                    _snapshot.originalVertices[i],
                    _snapshot.pressTargetVertices[i],
                    t
                );
            }
        }

        /// <summary>
        /// 외부 버텍스 배열 기준으로 Snapshot 재초기화.
        /// SpherePressSequencer에서 구형 완료 후 호출 — 구형 버텍스를 눌림의 기준점으로 설정.
        /// </summary>
        public void RebuildSnapshot(Vector3[] baseVertices, Bounds baseBounds)
        {
            if (_showDebugLog)
                Debug.Log($"[PressDeformer] RebuildSnapshot 시작 | 버텍스 수: {baseVertices.Length} | Bounds 중심: {baseBounds.center} | 크기: {baseBounds.size}");

            _jobRunner?.Dispose();
            _jobRunner = null;

            float? absoluteFloor = _autoFloor ? null : (float?)RatioToAbsolute(baseBounds, _pressAxis, _pressFloorRatio);
            float floorVal = absoluteFloor ?? GetAxisValue(baseBounds.center, _pressAxis);
            _snapshot = PressMeshSnapshot.CreateFromVertices(
                baseVertices, baseBounds,
                _pressAxis, _pressFrom,
                absoluteFloor, _lateralSpread
            );

            if (_useJobSystem)
                _jobRunner = new PressJobRunner(_snapshot);

            if (_showDebugLog)
                Debug.Log($"[PressDeformer] ✅ RebuildSnapshot 완료 | 축: {_pressAxis} | 방향: {_pressFrom} | 최저지점(절대): {floorVal:F4} | 팽창: {_lateralSpread:F2}");
        }

        public void ForceRevert()
        {
            if (_showDebugLog)
                Debug.Log("[PressDeformer] 강제 원본 복원 시작...");

            PressAmount = 0f;
            System.Array.Copy(
                _snapshot.originalVertices,
                _snapshot.currentVertices,
                _snapshot.VertexCount
            );
            _deformMesh.vertices = _snapshot.currentVertices;
            _deformMesh.RecalculateNormals();
            _deformMesh.RecalculateBounds();

            if (_showDebugLog)
                Debug.Log("[PressDeformer] ✅ 강제 원본 복원 완료");
        }

        void OnDestroy()
        {
            _jobRunner?.Dispose();
        }
    }
}
