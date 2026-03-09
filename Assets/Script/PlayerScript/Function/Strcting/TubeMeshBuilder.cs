using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 경로(pathPoints)를 따라 튜브 형태의 메시를 생성합니다.
/// 베지어 경로 등으로 만든 점 배열을 받아, 각 점을 중심으로 원형 링을 쌓아 올려 연결합니다.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class TubeMeshBuilder : MonoBehaviour
{
    #region Inspector - Tube Settings

    [Header("Tube Settings")]
    [Tooltip("튜브 단면(링) 하나당 정점 개수. 클수록 원에 가깝게 됨")]
    [SerializeField] private int ringVertexCount = 8;
    [Tooltip("경로 시작 쪽(어깨) 반지름. 일정 두께 사용 시 전체에 이 값 적용")]
    [SerializeField] private float startRadius   = 0.2f;
    [Tooltip("경로 끝 쪽(손) 반지름. '일정 두께' 꺼져 있을 때만 사용")]
    [SerializeField] private float endRadius     = 0.01f;
    [Tooltip("켜면 경로 전체 두께를 startRadius로 일정하게 유지. 끄면 시작→끝으로 점점 가늘어짐")]
    [SerializeField] private bool useUniformRadius = true;

    [Header("Space")]
    [Tooltip("켜면 전달된 경로(월드)를 로컬 좌표로 변환해 메시 생성. 부모가 움직여도 메시가 함께 이동")]
    [SerializeField] private bool _useLocalSpace = true;
    [Tooltip("로컬 좌표 기준이 될 Transform. 비어 있으면 이 오브젝트(메시) Transform 사용")]
    [SerializeField] private Transform _spaceReference;

    [Header("Debug")]
    [Tooltip("켜면 UpdateMesh/ClearMesh 등 동작을 콘솔에 출력")]
    [SerializeField] private bool _enableLog = false;

    #endregion

    #region Private - Mesh Data

    // --- 메시 데이터 (런타임) ---
    private Mesh _mesh;
    private List<Vector3> _vertices  = new List<Vector3>();
    private List<int>     _triangles = new List<int>();
    private bool          _trianglesBuilt = false;
    private int           _lastSegmentCount = -1;
    private int           _lastRingVertexCount = -1;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _mesh = new Mesh { name = "ArmTubeMesh" };
        GetComponent<MeshFilter>().mesh = _mesh;
        if (_enableLog)
            Debug.Log("[TubeMeshBuilder] Awake: Mesh 생성 및 MeshFilter 할당");
    }

    #endregion

    #region Public API

    /// <summary>
    /// 경로 포인트 배열을 받아 튜브 메시를 갱신합니다.
    /// 정점은 매번 재계산하고, 삼각형 인덱스는 경로 길이가 바뀌지 않는 한 최초 1회만 생성합니다.
    /// </summary>
    /// <param name="pathPoints">경로를 이루는 월드 좌표 점 배열 (최소 2개). useLocalSpace면 자동으로 로컬로 변환</param>
    public void UpdateMesh(Vector3[] pathPoints)
    {
        if (pathPoints == null || pathPoints.Length < 2)
        {
            if (_enableLog)
                Debug.LogWarning($"[TubeMeshBuilder] UpdateMesh: pathPoints가 null이거나 길이 2 미만 (Length={pathPoints?.Length ?? 0})");
            return;
        }

        // 로컬 좌표 사용 시 기준 Transform으로 변환 (기본: 이 오브젝트)
        Vector3[] pointsForBuild = pathPoints;
        if (_useLocalSpace)
        {
            Transform refT = _spaceReference != null ? _spaceReference : transform;
            pointsForBuild = new Vector3[pathPoints.Length];
            for (int i = 0; i < pathPoints.Length; i++)
                pointsForBuild[i] = refT.InverseTransformPoint(pathPoints[i]);
        }

        int segments = pointsForBuild.Length - 1;

        // 구간 수나 링 정점 수가 바뀌면 삼각형을 다시 만들어야 함
        // (점진적 생성 시 2→3→… 구간으로 늘어날 때, 또는 재생 중 인스펙터에서 ringVertexCount를 조정할 때)
        if (segments != _lastSegmentCount || ringVertexCount != _lastRingVertexCount)
        {
            _trianglesBuilt = false;
            _lastSegmentCount = segments;
            _lastRingVertexCount = ringVertexCount;
        }

        BuildVertices(pointsForBuild, segments);

        // 삼각형은 구간 수가 같으면 재사용 (경로만 바뀌는 경우)
        bool firstBuild = !_trianglesBuilt;
        if (!_trianglesBuilt)
        {
            BuildTriangles(segments);
            _trianglesBuilt = true;
            if (_enableLog)
                Debug.Log($"[TubeMeshBuilder] UpdateMesh: 삼각형 최초 생성 segments={segments} ringVertexCount={ringVertexCount} triangles={_triangles.Count}");
        }

        ApplyMesh();

        if (_enableLog)
            Debug.Log($"[TubeMeshBuilder] UpdateMesh: pathPoints={pathPoints.Length} segments={segments} vertices={_vertices.Count} firstBuild={firstBuild} localSpace={_useLocalSpace}");
    }

    /// <summary>
    /// 메시를 비우고, 다음 UpdateMesh 호출 시 삼각형을 다시 만들도록 플래그를 리셋합니다.
    /// </summary>
    public void ClearMesh()
    {
        _mesh.Clear();
        _trianglesBuilt = false;
        _lastSegmentCount = -1;
        _lastRingVertexCount = -1;
        if (_enableLog)
            Debug.Log("[TubeMeshBuilder] ClearMesh: 메시 초기화, 다음 UpdateMesh 시 삼각형 재생성");
    }

    #endregion

    #region Private - Build Mesh

    /// <summary>
    /// 경로의 각 점을 중심으로 원형 링의 정점을 생성합니다.
    /// 진행 방향(forward)을 기준으로 right/up을 구하고, 원주를 ringVertexCount만큼 나누어 정점을 추가합니다.
    /// </summary>
    private void BuildVertices(Vector3[] pathPoints, int segments)
    {
        _vertices.Clear();

        for (int i = 0; i <= segments; i++)
        {
            float t       = i / (float)segments;
            // 일정 두께면 startRadius만, 아니면 시작→끝으로 선형 보간
            float radius  = useUniformRadius ? startRadius : Mathf.Lerp(startRadius, endRadius, t);
            Vector3 center = pathPoints[i];

            // 진행 방향: 다음 점으로의 방향 (마지막 구간만 이전 구간과 동일 방향 사용)
            Vector3 forward;
            if (i < segments)
                forward = (pathPoints[i + 1] - pathPoints[i]).normalized;
            else
                forward = (pathPoints[i] - pathPoints[i - 1]).normalized;

            AddRingVertices(center, forward, radius);
        }
    }

    /// <summary>
    /// 한 개의 링(원 단면) 정점을 center 주변에 추가합니다.
    /// forward 축을 기준으로 right/up을 만들고, 원주를 ringVertexCount 등분한 각도로 offset을 더해 정점을 넣습니다.
    /// </summary>
    /// <param name="center">링 중심 (경로 위 한 점)</param>
    /// <param name="forward">경로 진행 방향 (정규화)</param>
    /// <param name="radius">반지름</param>
    private void AddRingVertices(Vector3 center, Vector3 forward, float radius)
    {
        // forward에 수직인 right: forward × up. 평행이면 right = Vector3.right 로 폴백
        Vector3 right = Vector3.Cross(forward, Vector3.up).normalized;
        if (right == Vector3.zero) right = Vector3.right;
        Vector3 up = Vector3.Cross(right, forward).normalized;

        for (int j = 0; j < ringVertexCount; j++)
        {
            float angle  = (j / (float)ringVertexCount) * Mathf.PI * 2f;
            Vector3 offset = (Mathf.Cos(angle) * right + Mathf.Sin(angle) * up) * radius;
            _vertices.Add(center + offset);
        }
    }

    /// <summary>
    /// 구간(segments)과 링 정점 수에 맞춰 삼각형 인덱스를 채웁니다.
    /// 인접한 두 링의 대응 정점을 이어 두 삼각형(quad)으로 만듭니다.
    /// </summary>
    private void BuildTriangles(int segments)
    {
        _triangles.Clear();

        for (int i = 0; i < segments; i++)
        {
            for (int j = 0; j < ringVertexCount; j++)
            {
                // 현재 링의 j번 정점, 다음 링의 같은 각도 정점, 그리고 j+1 번 정점들
                int curr   = i * ringVertexCount + j;
                int next   = i * ringVertexCount + (j + 1) % ringVertexCount;
                int currUp = curr + ringVertexCount;
                int nextUp = next + ringVertexCount;

                // 한 사각형을 두 삼각형으로 (CCW)
                _triangles.Add(curr);
                _triangles.Add(currUp);
                _triangles.Add(next);

                _triangles.Add(next);
                _triangles.Add(currUp);
                _triangles.Add(nextUp);
            }
        }

        if (_enableLog)
            Debug.Log($"[TubeMeshBuilder] BuildTriangles: segments={segments} ringVertexCount={ringVertexCount} triangleCount={_triangles.Count}");
    }

    /// <summary>
    /// 생성된 정점·삼각형을 Mesh에 적용하고, 노멀과 바운드를 재계산합니다.
    /// </summary>
    private void ApplyMesh()
    {
        // 이전 프레임의 삼각형이 현재 정점 수와 맞지 않을 수 있으므로
        // 항상 메시에 저장된 데이터를 지우고 다시 세팅하여
        // "vertices is too small" 오류를 방지합니다.
        _mesh.Clear();

        _mesh.SetVertices(_vertices);

        if (_trianglesBuilt)
            _mesh.SetTriangles(_triangles, 0);

        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        if (_enableLog)
            Debug.Log($"[TubeMeshBuilder] ApplyMesh: vertices={_vertices.Count} triangles 적용={_trianglesBuilt}");
    }

    #endregion
}
