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
        public static PressMeshSnapshot Create(
            Mesh      mesh,
            PressAxis axis,
            PressFrom pressFrom,
            float?    squashCenterOnAxis = null,
            float     lateralSpread = 0f)
        {
            Vector3[] source = mesh.vertices;
            int len          = source.Length;
            Bounds bounds    = mesh.bounds;
            float axisMin    = GetAxisValue(bounds.min, axis);
            float axisMax    = GetAxisValue(bounds.max, axis);
            float planeVal   = squashCenterOnAxis ?? GetCenterOnAxis(bounds, axis);
            Vector2 spreadCenter = GetSpreadCenter(bounds, axis);

            planeVal = ClampPlaneToRange(planeVal, axisMin, axisMax);

            var snapshot = new PressMeshSnapshot
            {
                originalVertices    = new Vector3[len],
                currentVertices     = new Vector3[len],
                pressTargetVertices = new Vector3[len]
            };

            for (int i = 0; i < len; i++)
            {
                Vector3 v     = source[i];
                float axisVal = GetAxisValue(v, axis);
                snapshot.originalVertices[i] = v;
                snapshot.currentVertices[i]  = v;
                bool shouldSquash = pressFrom == PressFrom.Positive
                    ? axisVal > planeVal
                    : axisVal < planeVal;
                snapshot.pressTargetVertices[i] = shouldSquash
                    ? CalcPressedTarget(v, axis, planeVal, axisMin, axisMax, spreadCenter, lateralSpread, pressFrom)
                    : CalcSpreadOnly(v, axis, spreadCenter, lateralSpread);
            }

            int squashCount = CountSquashTargets(snapshot.pressTargetVertices, snapshot.originalVertices);
            Debug.Log($"[PressMeshSnapshot] Create 완료 | 버텍스: {len} | 축: {axis} | 방향: {pressFrom} | 평면값: {planeVal:F3} | " +
                      $"축 범위: [{axisMin:F3} ~ {axisMax:F3}] | 팽창: {lateralSpread:F2} | 압축 대상 버텍스 수: {squashCount} (0이면 눌림 없음)");
            if (squashCount == 0)
                Debug.LogWarning($"[PressMeshSnapshot] 압축 대상이 0개입니다. Press From({pressFrom})에 맞는 쪽에 버텍스가 있는지 확인하세요.");
            return snapshot;
        }

        /// <summary>
        /// 외부 버텍스 배열과 Bounds를 직접 받아 Snapshot 생성.
        /// SpherePressSequencer에서 구형 완료 버텍스를 Press 기준점으로 주입할 때 사용.
        /// </summary>
        public static PressMeshSnapshot CreateFromVertices(
            Vector3[] baseVertices,
            Bounds    baseBounds,
            PressAxis axis,
            PressFrom pressFrom,
            float?    squashCenterOnAxis = null,
            float     lateralSpread = 0f)
        {
            int len          = baseVertices.Length;
            float axisMin    = GetAxisValue(baseBounds.min, axis);
            float axisMax    = GetAxisValue(baseBounds.max, axis);
            float planeVal   = squashCenterOnAxis ?? GetCenterOnAxis(baseBounds, axis);
            Vector2 spreadCenter = GetSpreadCenter(baseBounds, axis);

            planeVal = ClampPlaneToRange(planeVal, axisMin, axisMax);

            var snapshot = new PressMeshSnapshot
            {
                originalVertices    = new Vector3[len],
                currentVertices     = new Vector3[len],
                pressTargetVertices = new Vector3[len]
            };

            for (int i = 0; i < len; i++)
            {
                Vector3 v     = baseVertices[i];
                float axisVal = GetAxisValue(v, axis);
                snapshot.originalVertices[i] = v;
                snapshot.currentVertices[i]  = v;
                bool shouldSquash = pressFrom == PressFrom.Positive
                    ? axisVal > planeVal
                    : axisVal < planeVal;
                snapshot.pressTargetVertices[i] = shouldSquash
                    ? CalcPressedTarget(v, axis, planeVal, axisMin, axisMax, spreadCenter, lateralSpread, pressFrom)
                    : CalcSpreadOnly(v, axis, spreadCenter, lateralSpread);
            }

            int squashCount = CountSquashTargets(snapshot.pressTargetVertices, snapshot.originalVertices);
            Debug.Log($"[PressMeshSnapshot] CreateFromVertices 완료 | 버텍스: {len} | 축: {axis} | 방향: {pressFrom} | 평면값: {planeVal:F3} | " +
                      $"축 범위: [{axisMin:F3} ~ {axisMax:F3}] | 팽창: {lateralSpread:F2} | 압축 대상 버텍스 수: {squashCount} (0이면 눌림 없음)");
            if (squashCount == 0)
                Debug.LogWarning($"[PressMeshSnapshot] 압축 대상이 0개입니다. Press From({pressFrom})에 맞는 쪽에 버텍스가 있는지 확인하세요.");
            return snapshot;
        }

        /// <summary>
        /// 눌리는 버텍스 타깃: 압축 축은 planeVal로 수렴, 측면은 팽창.
        /// pressDepth가 클수록 (원래 위치가 press 방향 끝에 가까울수록) 더 많이 퍼짐.
        /// </summary>
        static Vector3 CalcPressedTarget(
            Vector3   v,
            PressAxis axis,
            float     planeVal,
            float     axisMin,
            float     axisMax,
            Vector2   spreadCenter,
            float     lateralSpread,
            PressFrom pressFrom)
        {
            Vector3 flat = WithAxisValue(v, axis, planeVal);

            if (lateralSpread <= 0f)
                return flat;

            float range      = Mathf.Max(axisMax - axisMin, 0.0001f);
            float axisVal    = GetAxisValue(v, axis);
            float pressDepth = pressFrom == PressFrom.Positive
                ? (axisVal - axisMin) / range
                : (axisMax - axisVal) / range;

            Vector2 spreadPos = GetSpreadPos(flat, axis);
            Vector2 dir       = spreadPos - spreadCenter;
            Vector2 expanded  = spreadCenter + dir * (1f + pressDepth * lateralSpread);

            return SetSpreadPos(flat, axis, expanded);
        }

        /// <summary>
        /// 눌리지 않는 버텍스 타깃: 압축 축 위치는 유지하고 측면만 팽창.
        /// 밀가루 전체가 옆으로 퍼지는 효과 — 눌리는 쪽만이 아닌 메시 전체에 적용.
        /// </summary>
        static Vector3 CalcSpreadOnly(Vector3 v, PressAxis axis, Vector2 spreadCenter, float lateralSpread)
        {
            if (lateralSpread <= 0f)
                return v;

            Vector2 spreadPos = GetSpreadPos(v, axis);
            Vector2 dir       = spreadPos - spreadCenter;
            Vector2 expanded  = spreadCenter + dir * (1f + lateralSpread);

            return SetSpreadPos(v, axis, expanded);
        }

        /// <summary>눌리지 않는 두 축의 중심점 (spread 기준점)</summary>
        static Vector2 GetSpreadCenter(Bounds bounds, PressAxis axis)
        {
            switch (axis)
            {
                case PressAxis.X: return new Vector2(bounds.center.y, bounds.center.z);
                case PressAxis.Z: return new Vector2(bounds.center.x, bounds.center.y);
                default:          return new Vector2(bounds.center.x, bounds.center.z); // Y
            }
        }

        /// <summary>벡터에서 press 축을 제외한 두 성분 추출</summary>
        static Vector2 GetSpreadPos(Vector3 v, PressAxis axis)
        {
            switch (axis)
            {
                case PressAxis.X: return new Vector2(v.y, v.z);
                case PressAxis.Z: return new Vector2(v.x, v.y);
                default:          return new Vector2(v.x, v.z); // Y
            }
        }

        /// <summary>벡터의 press 축을 제외한 두 성분을 spreadPos 값으로 교체</summary>
        static Vector3 SetSpreadPos(Vector3 v, PressAxis axis, Vector2 spreadPos)
        {
            switch (axis)
            {
                case PressAxis.X: return new Vector3(v.x, spreadPos.x, spreadPos.y);
                case PressAxis.Z: return new Vector3(spreadPos.x, spreadPos.y, v.z);
                default:          return new Vector3(spreadPos.x, v.y, spreadPos.y); // Y
            }
        }

        /// <summary>평면값이 축 범위 밖이면 경고만 출력. (비율 방식으로 변경되어 보정 불필요)</summary>
        static float ClampPlaneToRange(float planeVal, float axisMin, float axisMax)
        {
            if (planeVal >= axisMax || planeVal <= axisMin)
                Debug.LogWarning($"[PressMeshSnapshot] planeVal({planeVal:F4})이 축 범위 [{axisMin:F4} ~ {axisMax:F4}] 밖입니다. PressDeformer의 비율 변환을 확인하세요.");
            return planeVal;
        }

        static int CountSquashTargets(Vector3[] targets, Vector3[] originals)
        {
            int count = 0;
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != originals[i]) count++;
            }
            return count;
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
