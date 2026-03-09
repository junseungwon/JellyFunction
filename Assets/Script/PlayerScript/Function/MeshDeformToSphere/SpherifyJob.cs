using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;

namespace SpherifySystem
{
    #region Job

    // ── Job 구조체 ────────────────────────────────────────────────
    [BurstCompile]
    public struct SpherifyJob : IJobParallelFor
    {
        [ReadOnly]  public NativeArray<Vector3> originalVertices;
        [ReadOnly]  public NativeArray<Vector3> sphereTargets;
        [WriteOnly] public NativeArray<Vector3> outputVertices;
        public float t;

        public void Execute(int i)
        {
            outputVertices[i] = Vector3.Lerp(originalVertices[i], sphereTargets[i], t);
        }
    }

    #endregion

    #region Job Runner

    // ── NativeArray 캐싱 래퍼 ────────────────────────────────────
    public class SpherifyJobRunner : System.IDisposable
    {
        NativeArray<Vector3> origNative;
        NativeArray<Vector3> targetNative;
        NativeArray<Vector3> outputNative;

        bool isDisposed = false;

        public SpherifyJobRunner(MeshDataSnapshot snapshot)
        {
            origNative   = new NativeArray<Vector3>(snapshot.originalVertices,     Allocator.Persistent);
            targetNative = new NativeArray<Vector3>(snapshot.sphereTargetVertices, Allocator.Persistent);
            outputNative = new NativeArray<Vector3>(snapshot.VertexCount,          Allocator.Persistent);

            Debug.Log($"[SpherifyJobRunner] 생성 완료 | 버텍스 수: {snapshot.VertexCount}");
        }

        public void Run(MeshDataSnapshot snapshot, float t)
        {
            if (isDisposed) return;

            var job = new SpherifyJob
            {
                originalVertices = origNative,
                sphereTargets    = targetNative,
                outputVertices   = outputNative,
                t                = t
            };

            job.Schedule(snapshot.VertexCount, 64).Complete();
            outputNative.CopyTo(snapshot.currentVertices);
        }

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            if (origNative.IsCreated)   origNative.Dispose();
            if (targetNative.IsCreated) targetNative.Dispose();
            if (outputNative.IsCreated) outputNative.Dispose();

            Debug.Log("[SpherifyJobRunner] ✅ Dispose 완료");
        }
    }

    #endregion
}
