using UnityEngine;

namespace SpherifySystem
{
    [RequireComponent(typeof(MeshFilter))]
    public class SpherifyDeformer : MonoBehaviour
    {
        [Header("Sphere Settings")]
        [SerializeField] bool  autoCalcRadius = true;
        [SerializeField] float manualRadius   = 1f;

        [Header("Performance")]
        [SerializeField] bool useJobSystem = true;

        // ── 내부 상태 ────────────────────────────────────────────
        MeshFilter       meshFilter;
        Mesh             deformMesh;
        MeshDataSnapshot snapshot;
        SpherifyJobRunner jobRunner;

        // SpherifyController가 이 값을 0~1로 조절
        public float SpherifyAmount { get; set; } = 0f;

        // 현재 반지름 (디버그/외부 참조용)
        public float CurrentRadius { get; private set; }

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
        }

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

        // ── 강제 원본 복원 (외부 호출용) ─────────────────────────
        public void ForceRevert()
        {
            SpherifyAmount = 0f;

            System.Array.Copy(
                snapshot.originalVertices,
                snapshot.currentVertices,
                snapshot.VertexCount
            );

            deformMesh.vertices = snapshot.currentVertices;
            deformMesh.RecalculateNormals();
            deformMesh.RecalculateBounds();
        }

        // ── 리소스 해제 ──────────────────────────────────────────
        void OnDestroy()
        {
            jobRunner?.Dispose();
        }
    }
}
