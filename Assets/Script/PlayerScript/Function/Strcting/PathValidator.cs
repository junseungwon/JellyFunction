using UnityEngine;

public static class PathValidator
{
    // Linecast 기반 경로 검증
    // 추후 SphereCast로 교체 시 CheckSegment 내부만 수정
    public static bool IsPathClear(Vector3[] pathPoints, LayerMask obstacleLayer)
    {
        for (int i = 0; i < pathPoints.Length - 1; i++)
        {
            if (!CheckSegment(pathPoints[i], pathPoints[i + 1], obstacleLayer))
                return false;
        }
        return true;
    }

    // 막힌 지점의 인덱스 반환 (-1 = 전 구간 통과)
    public static int GetBlockedIndex(Vector3[] pathPoints, LayerMask obstacleLayer)
    {
        for (int i = 0; i < pathPoints.Length - 1; i++)
        {
            if (!CheckSegment(pathPoints[i], pathPoints[i + 1], obstacleLayer))
                return i;
        }
        return -1;
    }

    // 개별 구간 검사 (Linecast → 추후 SphereCast 교체 포인트)
    private static bool CheckSegment(Vector3 from, Vector3 to, LayerMask obstacleLayer)
    {
        return !Physics.Linecast(from, to, obstacleLayer);
    }

    /// <summary>Linecast 결과를 반환 (디버그용). blocked면 hit에 충돌 정보 저장.</summary>
    public static bool CheckSegmentWithHit(Vector3 from, Vector3 to, LayerMask obstacleLayer, out RaycastHit hit)
    {
        return Physics.Linecast(from, to, out hit, obstacleLayer);
    }

    // 에디터/플레이 디버그용 경로 시각화 (지속 시간 지정 가능)
    public static void DrawDebugPath(Vector3[] pathPoints, LayerMask obstacleLayer, float duration = 2f)
    {
        if (pathPoints == null || pathPoints.Length < 2) return;

        for (int i = 0; i < pathPoints.Length - 1; i++)
        {
            bool clear = CheckSegment(pathPoints[i], pathPoints[i + 1], obstacleLayer);
            Debug.DrawLine(pathPoints[i], pathPoints[i + 1], clear ? Color.green : Color.red, duration);
        }
    }

    /// <summary>막힌 구간이 있으면 hit 위치에 빨간 구 표시 및 로그 출력</summary>
    public static void DrawDebugPathWithHitInfo(Vector3[] pathPoints, LayerMask obstacleLayer, float duration = 2f)
    {
        if (pathPoints == null || pathPoints.Length < 2) return;

        for (int i = 0; i < pathPoints.Length - 1; i++)
        {
            Vector3 from = pathPoints[i];
            Vector3 to = pathPoints[i + 1];
            bool clear = !CheckSegmentWithHit(from, to, obstacleLayer, out RaycastHit hit);

            Debug.DrawLine(from, to, clear ? Color.green : Color.red, duration);

            if (!clear)
            {
                Debug.DrawLine(hit.point, hit.point + hit.normal * 0.5f, Color.yellow, duration);
#if UNITY_EDITOR
                Debug.Log($"[PathValidator] 구간 {i} 막힘 | hit: {hit.collider?.name ?? "null"} | point: {hit.point}");
#endif
            }
        }
    }
}
