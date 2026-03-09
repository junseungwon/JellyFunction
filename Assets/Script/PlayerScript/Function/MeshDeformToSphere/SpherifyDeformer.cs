using UnityEngine;

namespace SpherifySystem
{
    [RequireComponent(typeof(MeshFilter))]
    public class SpherifyDeformer : MonoBehaviour
    {
        #region Inspector

        [Header("Sphere Settings")]
        [SerializeField] bool  autoCalcRadius = true;
        [SerializeField] float manualRadius   = 1f;

        [Header("Performance")]
        [SerializeField] bool useJobSystem = true;

        [Header("Debug")]
        [SerializeField] bool _showDebugLog = true;

        #endregion

        #region Private - State

        // ── 내부 상태 ────────────────────────────────────────────
        MeshFilter       meshFilter;
        Mesh             deformMesh;
        MeshDataSnapshot snapshot;
        SpherifyJobRunner jobRunner;

        // SpherifyController가 이 값을 0~1로 조절
        public float SpherifyAmount { get; set; } = 0f;

        // 현재 반지름 (디버그/외부 참조용)
        public float CurrentRadius { get; private set; }

        #endregion

        #region Unity Lifecycle

        // ── 초기화 ───────────────────────────────────────────────
        void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            deformMesh = meshFilter.mesh; // 인스턴스 메시 복사본 취득

            CurrentRadius = autoCalcRadius
                ? deformMesh.bounds.extents.magnitude
                : manualRadius;

            snapshot = MeshDataSnapshot.Create(deformMesh, CurrentRadius);

            if (useJobSystem)
                jobRunner = new SpherifyJobRunner(snapshot);

            if (_showDebugLog)
                Debug.Log($"[SpherifyDeformer] 초기화 완료 | 버텍스 수: {snapshot.VertexCount} | 반지름: {CurrentRadius:F3} | JobSystem: {useJobSystem}");
        }

        #endregion

        #region Update - Deform

        // ── 변형 적용 ────────────────────────────────────────────
        void LateUpdate()
        {
            // t = 0이면 원본 그대로 → 연산 스킵
            if (Mathf.Approximately(SpherifyAmount, 0f))
                return;

            if (useJobSystem)
                jobRunner.Run(snapshot, SpherifyAmount);
            else
                ApplyOnCPU(SpherifyAmount);

            deformMesh.vertices = snapshot.currentVertices;
            deformMesh.RecalculateNormals();
            deformMesh.RecalculateBounds();
        }

        // ── CPU 단순 Lerp (저버텍스 메시용) ──────────────────────
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

        #region Public API

        // ── 강제 원본 복원 (외부 호출용) ─────────────────────────
        public void ForceRevert()
        {
            if (_showDebugLog)
                Debug.Log("[SpherifyDeformer] 강제 원본 복원 시작...");

            SpherifyAmount = 0f;

            System.Array.Copy(
                snapshot.originalVertices,
                snapshot.currentVertices,
                snapshot.VertexCount
            );

            deformMesh.vertices = snapshot.currentVertices;
            deformMesh.RecalculateNormals();
            deformMesh.RecalculateBounds();

            if (_showDebugLog)
                Debug.Log("[SpherifyDeformer] ✅ 강제 원본 복원 완료");
        }

        // ── 외부 연계용 (SpherePressSequencer) ───────────────────
        /// <summary>현재 변형된 버텍스 배열의 복사본 반환 (구형 완료 직후 Sequencer에서 호출)</summary>
        public Vector3[] GetCurrentVerticesCopy()
        {
            Vector3[] copy = new Vector3[snapshot.VertexCount];
            System.Array.Copy(snapshot.currentVertices, copy, snapshot.VertexCount);

            if (_showDebugLog)
                Debug.Log($"[SpherifyDeformer] 버텍스 복사 완료 | 복사된 버텍스 수: {copy.Length}");

            return copy;
        }

        /// <summary>현재 메시 Bounds 반환 (LateUpdate에서 RecalculateBounds 후 항상 최신 상태)</summary>
        public Bounds GetCurrentBounds() => deformMesh.bounds;

        #endregion

        #region Cleanup

        // ── 리소스 해제 ──────────────────────────────────────────
        void OnDestroy()
        {
            jobRunner?.Dispose();
        }

        #endregion
    }
}
