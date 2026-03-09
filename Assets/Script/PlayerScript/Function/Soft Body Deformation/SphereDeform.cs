using System.Collections.Generic;
using UnityEngine;


public class SphereDeform : MonoBehaviour
{
    #region Inspector - 변형 설정

    [Header("변형 설정")]
    public float deformRadius = 0.3f;
    public float maxDeformStrength = 0.1f;
    public float deformSpeed = 5f;
    public float recoverSpeed = 3f;
    [Tooltip("켜면 충돌이 끝났을 때 자동으로 원래 형태로 복원됩니다.")]
    public bool autoRecover = true;

    [Header("다중 충돌 합산 방식")]
    [Tooltip("Additive: 충돌이 겹칠수록 깊어짐 / Max: 가장 강한 충돌 하나만 적용")]
    public DeformBlendMode blendMode = DeformBlendMode.Additive;

    public enum DeformBlendMode { Additive, Max }

    [Header("충돌 레이어")]
    [Tooltip("이 레이어에 있는 오브젝트와만 충돌 시 변형됩니다. 비어 있으면 모든 레이어에 반응합니다.")]
    public LayerMask collisionLayers = -1;

    [Header("Debug")]
    [SerializeField] bool showDebugLog = false;
    [SerializeField] bool drawGizmos = false;
    [SerializeField] Color gizmoDeformRadiusColor = new Color(1f, 0.5f, 0f, 0.15f);
    [SerializeField] Color gizmoContactPointColor = Color.green;
    [SerializeField] Color gizmoNormalColor = Color.cyan;
    [SerializeField] float gizmoNormalLength = 0.5f;
    [SerializeField] bool gizmosOnlyWhenColliding = false;
    [SerializeField] float debugLogInterval = 0f;

    #endregion

    #region Private - Data

    // 개별 충돌 데이터
    struct ContactInfo
    {
        public Vector3 point;
        public Vector3 normal;
        public float currentStrength;
        public float targetStrength;
    }

    Mesh mesh;
    Vector3[] originalVertices;
    Vector3[] deformedVertices;

    [SerializeField]SphereCollider sphereCollider;
    [SerializeField]MeshFilter meshFilter;
    float originalRadius;

    // 충돌 오브젝트별 접촉 정보 (Key: Collider)
    Dictionary<Collider, ContactInfo> activeContacts = new Dictionary<Collider, ContactInfo>();

    // 복원 중인 접촉 (Exit 후 서서히 복원)
    Dictionary<Collider, ContactInfo> recoveringContacts = new Dictionary<Collider, ContactInfo>();

    float debugLogAccum = 0f;

    bool IsColliding => activeContacts.Count > 0;

    #endregion

    #region Unity Lifecycle

    void Start()
    {
        mesh = meshFilter.mesh;
        originalVertices = mesh.vertices;
        deformedVertices = (Vector3[])originalVertices.Clone();
        originalRadius = sphereCollider.radius;

        if (deformRadius <= 0f)
            deformRadius = originalRadius * transform.localScale.x * 0.5f;

        if (showDebugLog)
            Debug.Log($"[SphereDeform] Start | 버텍스:{originalVertices.Length} | originalRadius:{originalRadius:F3} | deformRadius:{deformRadius:F3}");
    }

    void Update()
    {
        UpdateStrengths();
        ApplyDeform();

        if (showDebugLog && debugLogInterval > 0f)
        {
            debugLogAccum += Time.deltaTime;
            if (debugLogAccum >= debugLogInterval)
            {
                debugLogAccum = 0f;
                Debug.Log($"[SphereDeform] 상태 | 활성 충돌:{activeContacts.Count} | 복원 중:{recoveringContacts.Count}");
            }
        }
    }

    #endregion

    #region Collision

    void OnCollisionEnter(Collision collision)
    {
        if (!IsInCollisionLayers(collision.gameObject)) return;

        Collider key = collision.collider;
        recoveringContacts.Remove(key);

        ContactInfo info = BuildContactInfo(collision, key);
        activeContacts[key] = info;

        if (showDebugLog)
            Debug.Log($"[SphereDeform] Enter | {collision.gameObject.name} | 활성 충돌 수:{activeContacts.Count}");
    }

    void OnCollisionStay(Collision collision)
    {
        if (!IsInCollisionLayers(collision.gameObject)) return;

        Collider key = collision.collider;
        ContactInfo info = BuildContactInfo(collision, key);
        activeContacts[key] = info;
    }

    void OnCollisionExit(Collision collision)
    {
        if (!IsInCollisionLayers(collision.gameObject)) return;

        Collider key = collision.collider;

        if (activeContacts.TryGetValue(key, out ContactInfo info))
        {
            if (autoRecover)
            {
                info.targetStrength = 0f;
                recoveringContacts[key] = info;
            }
            activeContacts.Remove(key);
        }

        if (showDebugLog)
            Debug.Log($"[SphereDeform] Exit | {collision.gameObject.name} | 활성 충돌 수:{activeContacts.Count}");
    }

    #endregion

    #region Deform Logic

    // 각 ContactInfo의 currentStrength를 targetStrength 방향으로 Lerp
    void UpdateStrengths()
    {
        float dt = Time.deltaTime;

        // 활성 충돌 강도 업데이트
        var activeKeys = new List<Collider>(activeContacts.Keys);
        foreach (var key in activeKeys)
        {
            ContactInfo info = activeContacts[key];
            info.currentStrength = Mathf.Lerp(info.currentStrength, info.targetStrength, dt * deformSpeed);
            activeContacts[key] = info;
        }

        // 복원 중인 충돌 강도 업데이트
        var recoverKeys = new List<Collider>(recoveringContacts.Keys);
        foreach (var key in recoverKeys)
        {
            ContactInfo info = recoveringContacts[key];
            info.currentStrength = Mathf.Lerp(info.currentStrength, 0f, dt * recoverSpeed);

            if (info.currentStrength < 0.001f)
                recoveringContacts.Remove(key);
            else
                recoveringContacts[key] = info;
        }
    }

    void ApplyDeform()
    {
        // 활성 + 복원 중 접촉 전체 병합
        var allContacts = new List<ContactInfo>();
        foreach (var v in activeContacts.Values)   allContacts.Add(v);
        foreach (var v in recoveringContacts.Values) allContacts.Add(v);

        bool changed = false;

        for (int i = 0; i < deformedVertices.Length; i++)
        {
            Vector3 worldVert = transform.TransformPoint(originalVertices[i]);
            Vector3 totalDeformDir = Vector3.zero;

            if (blendMode == DeformBlendMode.Additive)
            {
                foreach (var contact in allContacts)
                {
                    float dist = Vector3.Distance(worldVert, contact.point);
                    if (dist >= deformRadius) continue;

                    float falloff = Mathf.Pow(1f - (dist / deformRadius), 2);
                    totalDeformDir += contact.normal * contact.currentStrength * falloff;
                }
                // Additive 모드는 maxDeformStrength로 클램프
                if (totalDeformDir.magnitude > maxDeformStrength)
                    totalDeformDir = totalDeformDir.normalized * maxDeformStrength;
            }
            else // Max
            {
                float maxMag = 0f;
                foreach (var contact in allContacts)
                {
                    float dist = Vector3.Distance(worldVert, contact.point);
                    if (dist >= deformRadius) continue;

                    float falloff = Mathf.Pow(1f - (dist / deformRadius), 2);
                    Vector3 candidate = contact.normal * contact.currentStrength * falloff;
                    if (candidate.magnitude > maxMag)
                    {
                        maxMag = candidate.magnitude;
                        totalDeformDir = candidate;
                    }
                }
            }

            Vector3 targetVertex = originalVertices[i] - transform.InverseTransformDirection(totalDeformDir);

            if (deformedVertices[i] != targetVertex)
            {
                float speed = allContacts.Count > 0 ? deformSpeed : recoverSpeed;
                deformedVertices[i] = Vector3.Lerp(deformedVertices[i], targetVertex, Time.deltaTime * speed * 10f);
                changed = true;
            }
        }

        if (changed)
        {
            mesh.vertices = deformedVertices;
            mesh.RecalculateNormals();
        }
    }

    ContactInfo BuildContactInfo(Collision collision, Collider key)
    {
        Vector3 centerPoint = Vector3.zero;
        Vector3 avgNormal = Vector3.zero;

        for (int c = 0; c < collision.contactCount; c++)
        {
            centerPoint += collision.GetContact(c).point;
            avgNormal   += collision.GetContact(c).normal;
        }

        centerPoint /= collision.contactCount;
        avgNormal = avgNormal.normalized;

        float impulse = collision.impulse.magnitude;
        float strength = Mathf.Clamp(impulse * 0.05f, maxDeformStrength * 0.3f, maxDeformStrength);

        // 기존 currentStrength 유지 (갑작스러운 초기화 방지)
        float prevCurrent = 0f;
        if (activeContacts.TryGetValue(key, out ContactInfo prev))
            prevCurrent = prev.currentStrength;

        return new ContactInfo
        {
            point           = centerPoint,
            normal          = avgNormal,
            currentStrength = prevCurrent,
            targetStrength  = strength
        };
    }

    bool IsInCollisionLayers(GameObject go)
    {
        if (collisionLayers.value == -1) return true;
        return ((1 << go.layer) & collisionLayers.value) != 0;
    }

    #endregion

    #region Gizmos

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        if (gizmosOnlyWhenColliding && !IsColliding) return;

        Transform t = transform;
        Vector3 center = Application.isPlaying
            ? t.position + t.TransformDirection(sphereCollider != null ? sphereCollider.center : Vector3.zero)
            : t.position;

        float worldDeformRadius = deformRadius * t.lossyScale.x;
        Gizmos.color = gizmoDeformRadiusColor;
        Gizmos.DrawSphere(center, worldDeformRadius);

        if (!Application.isPlaying) return;

        // 활성 접촉점 (초록)
        Gizmos.color = gizmoContactPointColor;
        foreach (var contact in activeContacts.Values)
        {
            Gizmos.DrawWireSphere(contact.point, 0.02f);
            Gizmos.DrawLine(contact.point, contact.point + contact.normal * gizmoNormalLength);
        }

        // 복원 중인 접촉점 (노랑)
        Gizmos.color = Color.yellow;
        foreach (var contact in recoveringContacts.Values)
        {
            Gizmos.DrawWireSphere(contact.point, 0.015f);
            Gizmos.DrawLine(contact.point, contact.point + contact.normal * gizmoNormalLength * 0.5f);
        }

        // 법선 색
        Gizmos.color = gizmoNormalColor;
        if (sphereCollider != null)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(center, sphereCollider.radius * t.lossyScale.x);
        }
    }

    #endregion

    #region Public API

    // ─── 활성화 제어 API ─────────────────────────────────────────

    public void EnableDeform()  => enabled = true;
    public void DisableDeform() => enabled = false;
    public void ToggleDeform()  => enabled = !enabled;

    #endregion
}
