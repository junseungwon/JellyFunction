using UnityEngine;

namespace PressSystem
{
    /// <summary>
    /// 메시의 원본 버텍스와 "완전히 눌린" 상태(한 축 방향 압축) 타깃 버텍스를 보관.
    /// 지정한 축 좌표만 bounds 중심(또는 수동 값)으로 수렴시킨 위치를 미리 계산.
    /// </summary>
    public class PressMeshSnapshot
    {
        public Vector3[] originalVertices;
        public Vector3[] currentVertices;
        public Vector3[] pressTargetVertices;

        public int VertexCount => originalVertices.Length;

        /// <summary>
        /// 지정한 축 방향으로 한쪽만 압축된 타깃 버텍스 생성.
        /// </summary>
        /// <param name="mesh">대상 메시</param>
        /// <param name="axis">압축할 축 (X/Y/Z)</param>
        /// <param name="pressFrom">눌리는 쪽 (Positive=+축 쪽만 압축, Negative=-축 쪽만 압축)</param>
        /// <param name="squashCenterOnAxis">해당 축의 압축 평면 값 (null이면 mesh.bounds.center의 해당 성분 사용)</param>
        public static PressMeshSnapshot Create(Mesh mesh, PressAxis axis, PressFrom pressFrom, float? squashCenterOnAxis = null)
        {
            Vector3[] source = mesh.vertices;
            int len          = source.Length;
            Bounds bounds    = mesh.bounds;
            float planeVal   = squashCenterOnAxis ?? GetCenterOnAxis(bounds, axis);

            var snapshot = new PressMeshSnapshot
            {
                originalVertices    = new Vector3[len],
                currentVertices     = new Vector3[len],
                pressTargetVertices = new Vector3[len]
            };

            for (int i = 0; i < len; i++)
            {
                Vector3 v = source[i];
                float axisVal = GetAxisValue(v, axis);
                snapshot.originalVertices[i] = v;
                snapshot.currentVertices[i]  = v;
                // 한쪽만 눌림: 평면 바깥쪽에 있는 버텍스만 평면으로 이동
                bool shouldSquash = pressFrom == PressFrom.Positive
                    ? axisVal > planeVal
                    : axisVal < planeVal;
                snapshot.pressTargetVertices[i] = shouldSquash ? WithAxisValue(v, axis, planeVal) : v;
            }

            return snapshot;
        }

        static float GetAxisValue(Vector3 v, PressAxis axis)
        {
            switch (axis)
            {
                case PressAxis.X: return v.x;
                case PressAxis.Y: return v.y;
                case PressAxis.Z: return v.z;
                default: return v.y;
            }
        }

        static float GetCenterOnAxis(Bounds bounds, PressAxis axis)
        {
            switch (axis)
            {
                case PressAxis.X: return bounds.center.x;
                case PressAxis.Y: return bounds.center.y;
                case PressAxis.Z: return bounds.center.z;
                default: return bounds.center.y;
            }
        }

        /// <summary>벡터 v의 지정 축 성분만 value로 바꾼 새 Vector3</summary>
        static Vector3 WithAxisValue(Vector3 v, PressAxis axis, float value)
        {
            switch (axis)
            {
                case PressAxis.X: return new Vector3(value, v.y, v.z);
                case PressAxis.Y: return new Vector3(v.x, value, v.z);
                case PressAxis.Z: return new Vector3(v.x, v.y, value);
                default: return new Vector3(v.x, value, v.z);
            }
        }
    }
}
