using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshCollider))]
public class SphereDeformMesh : MonoBehaviour
{
    [Header("변형 설정")]
    public float deformRadius = 0.3f;       // 눌림 면적 반경 (구 반지름의 0.5배 권장)
    public float maxDeformStrength = 0.1f;  // 최대 눌림 깊이
    public float deformSpeed = 5f;          // 눌리는 속도
    public float recoverSpeed = 3f;         // 복원 속도
    [Tooltip("켜면 충돌이 끝났을 때 자동으로 원래 형태로 복원됩니다. 끄면 눌린 상태로 유지됩니다.")]
    public bool autoRecover = true;

    [Header("충돌 레이어")]
    [Tooltip("이 레이어에 있는 오브젝트와만 충돌 시 변형됩니다. 비어 있으면 모든 레이어에 반응합니다.")]
    public LayerMask collisionLayers = -1;  // -1 = Everything

    [Header("Debug")]
    [Tooltip("켜면 콘솔에 초기화·충돌·변형 관련 로그를 출력합니다.")]
    [SerializeField] bool showDebugLog = false;
    [Tooltip("켜면 Scene 뷰에 변형 반경·접촉점·법선을 기즈모로 그립니다.")]
    [SerializeField] bool drawGizmos = false;
    [Tooltip("변형 반경(deformRadius) 기즈모 색")]
    [SerializeField] Color gizmoDeformRadiusColor = new Color(1f, 0.5f, 0f, 0.15f);
    [Tooltip("접촉점(latestContactPoint) 기즈모 색")]
    [SerializeField] Color gizmoContactPointColor = Color.green;
    [Tooltip("법선(latestNormal) 선 색 및 길이")]
    [SerializeField] Color gizmoNormalColor = Color.cyan;
    [SerializeField] float gizmoNormalLength = 0.5f;
    [Tooltip("충돌 중일 때만 기즈모 그리기 (비활성 시 항상 그리기)")]
    [SerializeField] bool gizmosOnlyWhenColliding = false;
    [Tooltip("0보다 크면 이 주기(초)마다 현재 변형 상태를 로그합니다. 0이면 이벤트 시에만 로그")]
    [SerializeField] float debugLogInterval = 0f;

    Mesh mesh;
    Vector3[] originalVertices;
    Vector3[] deformedVertices;

    MeshCollider meshCollider;

    // 현재 변형 상태
    float currentDeformStrength = 0f;
    float targetDeformStrength = 0f;
    Vector3 latestContactPoint;
    Vector3 latestNormal;
    bool isColliding = false;
    int collisionCount = 0;  // 유효 레이어 충돌 개수
    float debugLogAccum = 0f;

    void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;
        originalVertices = mesh.vertices;
        deformedVertices = (Vector3[])originalVertices.Clone();

        meshCollider = GetComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;

        if (deformRadius <= 0f)
            deformRadius = 0.3f;

        if (showDebugLog)
        {
            Debug.Log($"[SphereDeformMesh] Start | 버텍스:{originalVertices.Length} | " +
                      $"deformRadius:{deformRadius:F3} | maxDeformStrength:{maxDeformStrength} | " +
                      $"collisionLayers:{(collisionLayers.value == -1 ? "Everything" : collisionLayers.value.ToString())}");
        }
    }

    void Update()
    {
        float target = isColliding ? targetDeformStrength : (autoRecover ? 0f : targetDeformStrength);
        currentDeformStrength = Mathf.Lerp(currentDeformStrength, target, Time.deltaTime * (isColliding ? deformSpeed : recoverSpeed));

        if (autoRecover && !isColliding && currentDeformStrength < 0.001f)
            currentDeformStrength = 0f;

        ApplyDeform();
        UpdateCollider();

        if (showDebugLog && debugLogInterval > 0f)
        {
            debugLogAccum += Time.deltaTime;
            if (debugLogAccum >= debugLogInterval)
            {
                debugLogAccum = 0f;
                Debug.Log($"[SphereDeformMesh] 상태 | isColliding:{isColliding} | collisionCount:{collisionCount} | " +
                         $"currentStrength:{currentDeformStrength:F4} | targetStrength:{targetDeformStrength:F4} | " +
                         $"contact:{latestContactPoint} | normal:{latestNormal}");
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!IsInCollisionLayers(collision.gameObject))
        {
            if (showDebugLog)
                Debug.Log($"[SphereDeformMesh] Enter(무시) | 상대:{collision.gameObject.name} | Layer:{LayerMask.LayerToName(collision.gameObject.layer)} (레이어 마스크에 없음)");
            return;
        }
        collisionCount++;
        isColliding = true;
        UpdateContact(collision);
        if (showDebugLog)
            Debug.Log($"[SphereDeformMesh] Enter | 상대:{collision.gameObject.name} | Layer:{LayerMask.LayerToName(collision.gameObject.layer)} | " +
                     $"contacts:{collision.contactCount} | impulse:{collision.impulse.magnitude:F3} | collisionCount→{collisionCount}");
    }

    void OnCollisionStay(Collision collision)
    {
        if (!IsInCollisionLayers(collision.gameObject)) return;
        isColliding = true;
        UpdateContact(collision);
    }

    void OnCollisionExit(Collision collision)
    {
        if (!IsInCollisionLayers(collision.gameObject))
        {
            if (showDebugLog)
                Debug.Log($"[SphereDeformMesh] Exit(무시) | 상대:{collision.gameObject.name} | Layer:{LayerMask.LayerToName(collision.gameObject.layer)}");
            return;
        }
        collisionCount--;
        if (collisionCount <= 0)
        {
            collisionCount = 0;
            isColliding = false;
            if (autoRecover)
                targetDeformStrength = 0f;
            if (showDebugLog)
                Debug.Log($"[SphereDeformMesh] Exit | 상대:{collision.gameObject.name} | collisionCount→0 | " + (autoRecover ? "변형 복원 시작" : "변형 유지 (autoRecover=off)"));
        }
        else if (showDebugLog)
            Debug.Log($"[SphereDeformMesh] Exit | 상대:{collision.gameObject.name} | collisionCount→{collisionCount} (다른 충돌 유지)");
    }

    /// <summary>지정한 레이어 마스크에 포함되면 true. collisionLayers가 Everything(-1)이면 항상 true.</summary>
    bool IsInCollisionLayers(GameObject go)
    {
        if (collisionLayers.value == -1) return true;
        return ((1 << go.layer) & collisionLayers.value) != 0;
    }

    void UpdateContact(Collision collision)
    {
        Vector3 centerPoint = Vector3.zero;
        Vector3 avgNormal = Vector3.zero;

        for (int c = 0; c < collision.contactCount; c++)
        {
            centerPoint += collision.GetContact(c).point;
            avgNormal += collision.GetContact(c).normal;
        }

        latestContactPoint = centerPoint / collision.contactCount;
        latestNormal = avgNormal.normalized;

        float impulse = collision.impulse.magnitude;
        float prevTarget = targetDeformStrength;
        targetDeformStrength = Mathf.Clamp(impulse * 0.05f, maxDeformStrength * 0.3f, maxDeformStrength);

        if (showDebugLog && !Mathf.Approximately(prevTarget, targetDeformStrength))
            Debug.Log($"[SphereDeformMesh] UpdateContact | impulse:{impulse:F3} → targetDeformStrength:{targetDeformStrength:F4} | contact:{latestContactPoint} | normal:{latestNormal}");
    }

    void ApplyDeform()
    {
        bool changed = false;

        for (int i = 0; i < deformedVertices.Length; i++)
        {
            Vector3 worldVert = transform.TransformPoint(originalVertices[i]);
            float dist = Vector3.Distance(worldVert, latestContactPoint);

            Vector3 targetVertex = originalVertices[i];

            if (isColliding && dist < deformRadius)
            {
                float falloff = 1f - (dist / deformRadius);
                falloff = Mathf.Pow(falloff, 2);

                Vector3 deformDir = latestNormal * currentDeformStrength * falloff;
                targetVertex -= transform.InverseTransformDirection(deformDir);
            }

            if (deformedVertices[i] != targetVertex)
            {
                deformedVertices[i] = Vector3.Lerp(
                    deformedVertices[i],
                    targetVertex,
                    Time.deltaTime * (isColliding ? deformSpeed : recoverSpeed) * 10f
                );
                changed = true;
            }
        }

        if (changed)
        {
            mesh.vertices = deformedVertices;
            mesh.RecalculateNormals();
        }
    }

    void UpdateCollider()
    {
        // 변형된 메시를 MeshCollider에 실시간 반영
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        if (gizmosOnlyWhenColliding && !isColliding) return;

        Transform t = transform;
        Vector3 center = t.position;

        float worldDeformRadius = deformRadius * t.lossyScale.x;
        Gizmos.color = gizmoDeformRadiusColor;
        Gizmos.DrawSphere(center, worldDeformRadius);

        if (Application.isPlaying && collisionCount > 0)
        {
            Gizmos.color = gizmoContactPointColor;
            Gizmos.DrawWireSphere(latestContactPoint, 0.02f);
            Gizmos.DrawLine(latestContactPoint, latestContactPoint + latestNormal * gizmoNormalLength);
            Gizmos.color = gizmoNormalColor;
            Gizmos.DrawLine(latestContactPoint, latestContactPoint + latestNormal * gizmoNormalLength);
        }
    }
}
