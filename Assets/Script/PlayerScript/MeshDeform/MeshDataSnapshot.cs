using UnityEngine;

namespace SpherifySystem
{
    public class MeshDataSnapshot
    {
        public Vector3[] originalVertices;
        public Vector3[] currentVertices;
        public Vector3[] sphereTargetVertices;

        public int VertexCount => originalVertices.Length;

        public static MeshDataSnapshot Create(Mesh mesh, float radius)
        {
            Vector3[] source = mesh.vertices;
            int len          = source.Length;
            Vector3 center   = mesh.bounds.center;

            var snapshot = new MeshDataSnapshot
            {
                originalVertices      = new Vector3[len],
                currentVertices       = new Vector3[len],
                sphereTargetVertices  = new Vector3[len]
            };

            for (int i = 0; i < len; i++)
            {
                snapshot.originalVertices[i]    = source[i];
                snapshot.currentVertices[i]     = source[i];

                Vector3 dir = source[i] - center;
                // dir이 zero벡터(중심점과 완전히 겹치는 버텍스) 예외 처리
                snapshot.sphereTargetVertices[i] = dir == Vector3.zero
                    ? center
                    : center + dir.normalized * radius;
            }

            return snapshot;
        }
    }
}
