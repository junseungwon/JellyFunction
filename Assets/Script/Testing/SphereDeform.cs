using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(SphereCollider))]
public class SphereDeform : MonoBehaviour
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

    SphereCollider sphereCollider;
    float originalRadius;

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
        // 메시 초기화
        mesh = GetComponent<MeshFilter>().mesh;
        originalVertices = mesh.vertices;
        deformedVertices = (Vector3[])originalVertices.Clone();

        // 콜라이더 초기화
        sphereCollider = GetComponent<SphereCollider>();
        originalRadius = sphereCollider.radius;

        // deformRadius 자동 설정 (구 반지름 기준)
        if (deformRadius <= 0f)
            deformRadius = originalRadius * transform.localScale.x * 0.5f;

        if (showDebugLog)
        {
            Debug.Log($"[SphereDeform] Start | 버텍스:{originalVertices.Length} | " +
                      $"originalRadius:{originalRadius:F3} | deformRadius:{deformRadius:F3} | " +
                      $"maxDeformStrength:{maxDeformStrength} | collisionLayers:{(collisionLayers.value == -1 ? "Everything" : collisionLayers.value.ToString())}");
        }
    }

    void Update()
    {
        // 충돌 중이면 목표값으로, 아니면 autoRecover에 따라 0 또는 현재 목표 유지
        float target = isColliding ? targetDeformStrength : (autoRecover ? 0f : targetDeformStrength);
        currentDeformStrength = Mathf.Lerp(currentDeformStrength, target, Time.deltaTime * (isColliding ? deformSpeed : recoverSpeed));

        // 자동 복원 시 거의 복원됐으면 완전히 0으로
        if (autoRecover && !isColliding && currentDeformStrength < 0.001f)
            currentDeformStrength = 0f;

        ApplyDeform();
        UpdateCollider();

        // 주기적 디버그 로그
        if (showDebugLog && debugLogInterval > 0f)
        {
            debugLogAccum += Time.deltaTime;
            if (debugLogAccum >= debugLogInterval)
            {
                debugLogAccum = 0f;
                Debug.Log($"[SphereDeform] 상태 | isColliding:{isColliding} | collisionCount:{collisionCount} | " +
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
                Debug.Log($"[SphereDeform] Enter(무시) | 상대:{collision.gameObject.name} | Layer:{LayerMask.LayerToName(collision.gameObject.layer)} (레이어 마스크에 없음)");
            return;
        }
        collisionCount++;
        isColliding = true;
        UpdateContact(collision);
        if (showDebugLog)
            Debug.Log($"[SphereDeform] Enter | 상대:{collision.gameObject.name} | Layer:{LayerMask.LayerToName(collision.gameObject.layer)} | " +
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
                Debug.Log($"[SphereDeform] Exit(무시) | 상대:{collision.gameObject.name} | Layer:{LayerMask.LayerToName(collision.gameObject.layer)}");
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
                Debug.Log($"[SphereDeform] Exit | 상대:{collision.gameObject.name} | collisionCount→0 | " + (autoRecover ? "변형 복원 시작" : "변형 유지 (autoRecover=off)"));
        }
        else if (showDebugLog)
            Debug.Log($"[SphereDeform] Exit | 상대:{collision.gameObject.name} | collisionCount→{collisionCount} (다른 충돌 유지)");
    }

    /// <summary>지정한 레이어 마스크에 포함되면 true. collisionLayers가 Everything(-1)이면 항상 true.</summary>
    bool IsInCollisionLayers(GameObject go)
    {
        if (collisionLayers.value == -1) return true;  // Everything
        return ((1 << go.layer) & collisionLayers.value) != 0;
    }

    void UpdateContact(Collision collision)
    {
        // 여러 ContactPoint의 평균 위치와 법선 계산
        Vector3 centerPoint = Vector3.zero;
        Vector3 avgNormal = Vector3.zero;

        for (int c = 0; c < collision.contactCount; c++)
        {
            centerPoint += collision.GetContact(c).point;
            avgNormal += collision.GetContact(c).normal;
        }

        latestContactPoint = centerPoint / collision.contactCount;
        latestNormal = avgNormal.normalized;

        // 충격량 기반으로 목표 변형량 결정 (누적 없이 고정값)
        float impulse = collision.impulse.magnitude;
        float prevTarget = targetDeformStrength;
        targetDeformStrength = Mathf.Clamp(impulse * 0.05f, maxDeformStrength * 0.3f, maxDeformStrength);

        if (showDebugLog && !Mathf.Approximately(prevTarget, targetDeformStrength))
            Debug.Log($"[SphereDeform] UpdateContact | impulse:{impulse:F3} → targetDeformStrength:{targetDeformStrength:F4} | contact:{latestContactPoint} | normal:{latestNormal}");
    }

    void ApplyDeform()
    {
        bool changed = false;

        for (int i = 0; i < deformedVertices.Length; i++)
        {
            Vector3 worldVert = transform.TransformPoint(originalVertices[i]);
            float dist = Vector3.Distance(worldVert, latestContactPoint);

            // 목표 버텍스 위치 계산
            Vector3 targetVertex = originalVertices[i];

            if (isColliding && dist < deformRadius)
            {
                float falloff = 1f - (dist / deformRadius);
                falloff = Mathf.Pow(falloff, 2); // 부드러운 곡선

                Vector3 deformDir = latestNormal * currentDeformStrength * falloff;
                targetVertex -= transform.InverseTransformDirection(deformDir);
            }

            // 버텍스를 목표 위치로 Lerp (부드럽게 이동)
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
        // 눌린 만큼 콜라이더 반지름 축소 (Mesh와 근사하게 맞춤)
        //sphereCollider.radius = originalRadius - (currentDeformStrength * 0.5f);
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        if (gizmosOnlyWhenColliding && !isColliding) return;

        Transform t = transform;
        Vector3 center = Application.isPlaying ? (t.position + t.TransformDirection(sphereCollider != null ? sphereCollider.center : Vector3.zero)) : t.position;
        float radius = Application.isPlaying && sphereCollider != null ? (sphereCollider.radius * t.lossyScale.x) : (deformRadius > 0f ? deformRadius : 0.3f);

        // 변형 반경(deformRadius) — 월드 스케일 반영
        float worldDeformRadius = deformRadius * t.lossyScale.x;
        Gizmos.color = gizmoDeformRadiusColor;
        Gizmos.DrawSphere(center, worldDeformRadius);

        // 접촉점 및 법선 (플레이 중이고 유의미한 접촉이 있을 때)
        if (Application.isPlaying && collisionCount > 0)
        {
            Gizmos.color = gizmoContactPointColor;
            Gizmos.DrawWireSphere(latestContactPoint, 0.02f);
            Gizmos.DrawLine(latestContactPoint, latestContactPoint + latestNormal * gizmoNormalLength);
            Gizmos.color = gizmoNormalColor;
            Gizmos.DrawLine(latestContactPoint, latestContactPoint + latestNormal * gizmoNormalLength);
        }

        // 현재 콜라이더 반지름 (플레이 중)
        if (Application.isPlaying && sphereCollider != null)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(center, radius);
        }
    }
}
