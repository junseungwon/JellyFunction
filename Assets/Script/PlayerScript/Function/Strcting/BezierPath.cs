using UnityEngine;

public static class BezierPath
{
    #region Point & Tangent

    // 2차 베지어 곡선 위치 계산
    public static Vector3 GetPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1f - t;
        return (u * u * p0) + (2f * u * t * p1) + (t * t * p2);
    }

    // 베지어 접선 벡터 (Ring 방향 결정용)
    public static Vector3 GetTangent(float t, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        float u = 1f - t;
        return (2f * u * (p1 - p0) + 2f * t * (p2 - p1)).normalized;
    }

    #endregion

    #region Control Point

    // 제어점 자동 계산 (중간점 + 위 방향 오프셋, 각도로 기울임)
    /// <param name="angleDegrees">제어점 오프셋 각도(도). 0=위쪽, 양수=타겟 방향으로 기울임, 음수=원점 방향</param>
    public static Vector3 GetAutoControlPoint(Vector3 origin, Vector3 target, float upOffset = 1.5f, bool logValues = false, float angleDegrees = 0f)
    {
        // 기본 월드 위(Vector3.up)를 사용하는 기존 버전
        return GetAutoControlPoint(origin, target, Vector3.up, upOffset, logValues, angleDegrees);
    }

    /// <summary>
    /// 로컬 Up 방향(예: 캐릭터 기준 위)을 지정할 수 있는 제어점 자동 계산 버전.
    /// </summary>
    /// <param name="origin">시작점</param>
    /// <param name="target">끝점</param>
    /// <param name="up">기준이 될 Up 방향 (보통 Transform.up)</param>
    /// <param name="upOffset">중간 지점에서 위쪽으로 얼마나 올릴지</param>
    /// <param name="logValues">디버그 로그 출력 여부</param>
    /// <param name="angleDegrees">Up 기준으로 타겟/원점 방향으로 기울일 각도</param>
    public static Vector3 GetAutoControlPoint(Vector3 origin, Vector3 target, Vector3 up, float upOffset, bool logValues, float angleDegrees)
    {
        Vector3 mid = (origin + target) * 0.5f;
        Vector3 toTarget = (target - origin).normalized;

        if (up == Vector3.zero)
            up = Vector3.up;

        // 오프셋 방향: 기준 Up에서 angleDegrees만큼 타겟/원점 방향으로 기울임
        Vector3 axis = Vector3.Cross(up, toTarget);
        if (axis.sqrMagnitude < 0.0001f)
            axis = Vector3.forward;
        else
            axis.Normalize();
        Vector3 offsetDir = Quaternion.AngleAxis(angleDegrees, axis) * up;
        Vector3 result = mid + offsetDir * upOffset;

        if (logValues)
            Debug.Log($"[BezierPath.GetAutoControlPoint] origin={origin} target={target} up={up} upOffset={upOffset} angleDegrees={angleDegrees} => mid={mid} result={result}");
        return result;
    }

    #endregion

    #region Sampling

    // 베지어 경로를 samples개 포인트로 샘플링
    public static Vector3[] SamplePath(Vector3 p0, Vector3 p1, Vector3 p2, int samples, bool logValues = false)
    {
        if (samples < 1)
        {
            if (logValues)
                Debug.LogWarning("[BezierPath.SamplePath] samples < 1, returning single point.");
            return new Vector3[] { p0 };
        }

        Vector3[] points = new Vector3[samples + 1];
        for (int i = 0; i <= samples; i++)
        {
            float t = i / (float)samples;
            points[i] = GetPoint(t, p0, p1, p2);
        }

        if (logValues)
        {
            int last = points.Length - 1;
            Vector3 ptMid = GetPoint(0.5f, p0, p1, p2);
            //Debug.Log($"[BezierPath.SamplePath] p0={p0} p1={p1} p2={p2} samples={samples} => " +
                //$"count={points.Length} first={points[0]} mid(t=0.5)={ptMid} last={points[last]}");
        }
        return points;
    }

    #endregion

    #region Debug

    /// <summary>디버그용: 베지어 곡선과 제어점을 씬 뷰에 그립니다.</summary>
    /// <param name="duration">라인 유지 시간(초). 0이면 1프레임.</param>
    /// <param name="logValues">true면 제어점·샘플 수 로그 출력</param>
    public static void DrawDebugCurve(Vector3 p0, Vector3 p1, Vector3 p2, int samples, float duration = 2f, bool logValues = false)
    {
        if (samples < 1)
        {
            if (logValues)
                Debug.LogWarning("[BezierPath.DrawDebugCurve] samples < 1, skip draw.");
            return;
        }

        if (logValues)
            //Debug.Log($"[BezierPath.DrawDebugCurve] p0={p0} p1={p1} p2={p2} samples={samples} duration={duration}");

        // 제어점 연결선 (회색)
        Debug.DrawLine(p0, p1, Color.gray, duration);
        Debug.DrawLine(p1, p2, Color.gray, duration);

        // 곡선 (연한 파랑)
        Vector3 prev = p0;
        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector3 pt = GetPoint(t, p0, p1, p2);
            Debug.DrawLine(prev, pt, new Color(0.4f, 0.6f, 1f), duration);
            prev = pt;
        }

        // 제어점 위치 강조 (짧은 축)
        float s = 0.08f;
        Debug.DrawLine(p0 - Vector3.right * s, p0 + Vector3.right * s, Color.white, duration);
        Debug.DrawLine(p1 - Vector3.right * s, p1 + Vector3.right * s, Color.yellow, duration);
        Debug.DrawLine(p2 - Vector3.right * s, p2 + Vector3.right * s, Color.white, duration);
    }

    #endregion
}
