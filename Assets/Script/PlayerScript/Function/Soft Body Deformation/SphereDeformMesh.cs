using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshCollider))]
public class SphereDeformMesh : MonoBehaviour
{
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

    MeshCollider meshCollider;

    Dictionary<Collider, ContactInfo> activeContacts = new Dictionary<Collider, ContactInfo>();
    Dictionary<Collider, ContactInfo> recoveringContacts = new Dictionary<Collider, ContactInfo>();

    float debugLogAccum = 0f;

    bool IsColliding => activeContacts.Count > 0;

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
            Debug.Log($"[SphereDeformMesh] Start | 버텍스:{originalVertices.Length} | deformRadius:{deformRadius:F3} | maxDeformStrength:{maxDeformStrength}");
    }

    void Update()
    {
        UpdateStrengths();
        ApplyDeform();
        UpdateCollider();

        if (showDebugLog && debugLogInterval > 0f)
        {
            debugLogAccum += Time.deltaTime;
            if (debugLogAccum >= debugLogInterval)
            {
                debugLogAccum = 0f;
                Debug.Log($"[SphereDeformMesh] 상태 | 활성 충돌:{activeContacts.Count} | 복원 중:{recoveringContacts.Count}");
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!IsInCollisionLayers(collision.gameObject)) return;

        Collider key = collision.collider;
        recoveringContacts.Remove(key);

        ContactInfo info = BuildContactInfo(collision, key);
        activeContacts[key] = info;

        if (showDebugLog)
            Debug.Log($"[SphereDeformMesh] Enter | {collision.gameObject.name} | 활성 충돌 수:{activeContacts.Count}");
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
            Debug.Log($"[SphereDeformMesh] Exit | {collision.gameObject.name} | 활성 충돌 수:{activeContacts.Count}");
    }

    void UpdateStrengths()
    {
        float dt = Time.deltaTime;

        var activeKeys = new List<Collider>(activeContacts.Keys);
        foreach (var key in activeKeys)
        {
            ContactInfo info = activeContacts[key];
            info.currentStrength = Mathf.Lerp(info.currentStrength, info.targetStrength, dt * deformSpeed);
            activeContacts[key] = info;
        }

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
        var allContacts = new List<ContactInfo>();
        foreach (var v in activeContacts.Values) allContacts.Add(v);
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
                if (totalDeformDir.magnitude > maxDeformStrength)
                    totalDeformDir = totalDeformDir.normalized * maxDeformStrength;
            }
            else
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

    void UpdateCollider()
    {
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;
    }

    ContactInfo BuildContactInfo(Collision collision, Collider key)
    {
        Vector3 centerPoint = Vector3.zero;
        Vector3 avgNormal = Vector3.zero;

        for (int c = 0; c < collision.contactCount; c++)
        {
            centerPoint += collision.GetContact(c).point;
            avgNormal += collision.GetContact(c).normal;
        }

        centerPoint /= collision.contactCount;
        avgNormal = avgNormal.normalized;

        float impulse = collision.impulse.magnitude;
        float strength = Mathf.Clamp(impulse * 0.05f, maxDeformStrength * 0.3f, maxDeformStrength);

        float prevCurrent = 0f;
        if (activeContacts.TryGetValue(key, out ContactInfo prev))
            prevCurrent = prev.currentStrength;

        return new ContactInfo
        {
            point = centerPoint,
            normal = avgNormal,
            currentStrength = prevCurrent,
            targetStrength = strength
        };
    }

    bool IsInCollisionLayers(GameObject go)
    {
        if (collisionLayers.value == -1) return true;
        return ((1 << go.layer) & collisionLayers.value) != 0;
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        if (gizmosOnlyWhenColliding && !IsColliding) return;

        Transform t = transform;
        Vector3 center = t.position;

        float worldDeformRadius = deformRadius * t.lossyScale.x;
        Gizmos.color = gizmoDeformRadiusColor;
        Gizmos.DrawSphere(center, worldDeformRadius);

        if (!Application.isPlaying) return;

        Gizmos.color = gizmoContactPointColor;
        foreach (var contact in activeContacts.Values)
        {
            Gizmos.DrawWireSphere(contact.point, 0.02f);
            Gizmos.DrawLine(contact.point, contact.point + contact.normal * gizmoNormalLength);
        }

        Gizmos.color = Color.yellow;
        foreach (var contact in recoveringContacts.Values)
        {
            Gizmos.DrawWireSphere(contact.point, 0.015f);
            Gizmos.DrawLine(contact.point, contact.point + contact.normal * gizmoNormalLength * 0.5f);
        }

        Gizmos.color = gizmoNormalColor;
    }
}
