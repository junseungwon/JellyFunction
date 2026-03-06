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
        [Tooltip("눌리는 방향(축). 해당 축 좌표가 평면으로 수렴합니다.")]
        [SerializeField] PressAxis _pressAxis = PressAxis.Y;
        [Tooltip("프레스가 들어오는 쪽. Positive=+축에서 눌림(위/앞/오른쪽), Negative=-축에서 눌림")]
        [SerializeField] PressFrom _pressFrom = PressFrom.Positive;
        [Tooltip("true면 메시 bounds 중심으로 압축, false면 아래 수동 값 사용")]
        [SerializeField] bool _autoSquashCenter = true;
        [SerializeField] float _manualSquashCenter = 0f;

        [Header("Performance")]
        [SerializeField] bool _useJobSystem = true;

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

            _snapshot = PressMeshSnapshot.Create(_deformMesh, _pressAxis, _pressFrom, _autoSquashCenter ? null : (float?)_manualSquashCenter);

            if (_useJobSystem)
                _jobRunner = new PressJobRunner(_snapshot);
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

        public void ForceRevert()
        {
            PressAmount = 0f;
            System.Array.Copy(
                _snapshot.originalVertices,
                _snapshot.currentVertices,
                _snapshot.VertexCount
            );
            _deformMesh.vertices = _snapshot.currentVertices;
            _deformMesh.RecalculateNormals();
            _deformMesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            _jobRunner?.Dispose();
        }
    }
}
