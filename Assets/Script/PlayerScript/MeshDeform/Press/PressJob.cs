using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;

namespace PressSystem
{
    [BurstCompile]
    public struct PressJob : IJobParallelFor
    {
        [ReadOnly]  public NativeArray<Vector3> originalVertices;
        [ReadOnly]  public NativeArray<Vector3> pressTargets;
        [WriteOnly] public NativeArray<Vector3> outputVertices;
        public float t;

        public void Execute(int i)
        {
            outputVertices[i] = Vector3.Lerp(originalVertices[i], pressTargets[i], t);
        }
    }

    public class PressJobRunner : System.IDisposable
    {
        NativeArray<Vector3> _origNative;
        NativeArray<Vector3> _targetNative;
        NativeArray<Vector3> _outputNative;
        bool _isDisposed = false;

        public PressJobRunner(PressMeshSnapshot snapshot)
        {
            _origNative   = new NativeArray<Vector3>(snapshot.originalVertices,    Allocator.Persistent);
            _targetNative = new NativeArray<Vector3>(snapshot.pressTargetVertices,  Allocator.Persistent);
            _outputNative = new NativeArray<Vector3>(snapshot.VertexCount,         Allocator.Persistent);
        }

        public void Run(PressMeshSnapshot snapshot, float t)
        {
            if (_isDisposed) return;

            var job = new PressJob
            {
                originalVertices = _origNative,
                pressTargets     = _targetNative,
                outputVertices   = _outputNative,
                t                = t
            };

            job.Schedule(snapshot.VertexCount, 64).Complete();
            _outputNative.CopyTo(snapshot.currentVertices);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            if (_origNative.IsCreated)   _origNative.Dispose();
            if (_targetNative.IsCreated) _targetNative.Dispose();
            if (_outputNative.IsCreated) _outputNative.Dispose();
        }
    }
}
