using UnityEngine;
using System.Collections;

/// <summary>
/// [배치 위치] 부모 오브젝트 JellyCharacter에 붙입니다.
/// JellyMeshDeformer는 자식에서 자동으로 찾습니다.
/// </summary>
public class JellyGapTraversal : MonoBehaviour
{
    [Header("틈 감지")]
    public float gapDetectionRange = 1.5f;
    [Range(3, 10)] public int detectionRayCount = 5;
    public LayerMask gapWallLayer = ~0;
    [Range(0.1f, 0.9f)] public float minPassableRatio = 0.25f;

    [Header("통과 동작")]
    public float traversalSpeed = 2f;
    public bool canMoveWhileSqueezing = true;
    [Range(0f, 1f)] public float squeezeMoveSpeedMultiplier = 0.4f;

    [Header("콜라이더 조정")]
    public bool adjustColliderOnSqueeze = true;

    [Header("디버그")]
    public bool showDebugGizmos = true;

    private JellyPhysicsCore physicsCore;
    private JellyMeshDeformer meshDeformer;
    private Rigidbody rb;
    private CapsuleCollider capsuleCollider;

    private float originalColliderRadius;
    private float originalColliderHeight;
    private Vector3 originalColliderCenter;

    private bool isTraversing = false;
    private GapInfo currentGap;

    public struct GapInfo
    {
        public bool detected;
        public Vector3 entryPoint;
        public Vector3 exitPoint;
        public Vector3 passAxis;
        public Vector3 squeezeAxis;
        public float gapWidth;
        public float gapHeight;
        public float compressionNeeded;
    }

    private float characterRadius;
    private float characterHeight;

    private void Awake()
    {
        // 같은 오브젝트(부모)에서 찾기
        physicsCore = GetComponent<JellyPhysicsCore>();
        rb = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();

        // 자식에서 JellyMeshDeformer 찾기
        meshDeformer = GetComponentInChildren<JellyMeshDeformer>();

        if (physicsCore == null) Debug.LogError("[JellyGapTraversal] 부모에 JellyPhysicsCore가 없습니다!");
        if (meshDeformer == null) Debug.LogError("[JellyGapTraversal] 자식에 JellyMeshDeformer가 없습니다!");

        if (capsuleCollider != null)
        {
            originalColliderCenter = capsuleCollider.center;
            originalColliderRadius = capsuleCollider.radius;
            originalColliderHeight = capsuleCollider.height;
            characterRadius = originalColliderRadius;
            characterHeight = originalColliderHeight;
        }
        else
        {
            characterRadius = 0.4f;
            characterHeight = 1.0f;
        }
    }

    private void Update()
    {
        if (!isTraversing) DetectGap();
    }

    private void DetectGap()
    {
        if (rb.velocity.magnitude < 0.1f) return;

        Vector3 moveDir = rb.velocity.normalized;
        currentGap = ScanForGap(moveDir);

        if (currentGap.detected && currentGap.gapWidth < characterRadius * 2f * 0.7f)
        {
            if (Vector3.Distance(transform.position, currentGap.entryPoint) < 0.5f)
                TryTraverseGap(currentGap);
        }
    }

    private GapInfo ScanForGap(Vector3 direction)
    {
        GapInfo info = new GapInfo();
        direction.y = 0;
        direction.Normalize();

        Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
        float halfWidth = characterRadius * 1.5f;

        int clearCount = 0;
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        for (int h = 0; h < detectionRayCount; h++)
        {
            float hOff = Mathf.Lerp(-halfWidth, halfWidth, (float)h / (detectionRayCount - 1));
            for (int v = 0; v < detectionRayCount; v++)
            {
                float vOff = Mathf.Lerp(0.1f, characterHeight * 0.95f, (float)v / (detectionRayCount - 1));
                Vector3 origin = transform.position + right * hOff + Vector3.up * vOff;

                if (!Physics.Raycast(origin, direction, gapDetectionRange, gapWallLayer))
                {
                    minX = Mathf.Min(minX, hOff); maxX = Mathf.Max(maxX, hOff);
                    minY = Mathf.Min(minY, vOff); maxY = Mathf.Max(maxY, vOff);
                    clearCount++;
                }
            }
        }

        float ratio = (float)clearCount / (detectionRayCount * detectionRayCount);
        if (ratio > 0.05f && ratio < 0.8f)
        {
            info.detected = true;
            info.gapWidth = maxX - minX;
            info.gapHeight = maxY - minY;
            info.passAxis = direction;
            info.squeezeAxis = right;
            Vector3 center = transform.position + right * ((minX + maxX) * 0.5f) + Vector3.up * ((minY + maxY) * 0.5f);
            info.entryPoint = center;
            info.exitPoint = center + direction * 2f;
            info.compressionNeeded = Mathf.Clamp(info.gapWidth / (characterRadius * 2f), 0.15f, 1f);
        }

        return info;
    }

    public void TryTraverseGap(GapInfo gap)
    {
        if (isTraversing) return;
        if (gap.compressionNeeded < physicsCore.maxCompression)
        {
            physicsCore.AddDeformation(new Vector3(Random.Range(-0.1f, 0.1f), 0.1f, Random.Range(-0.1f, 0.1f)));
            return;
        }
        StartCoroutine(TraverseGapSequence(gap));
    }

    private IEnumerator TraverseGapSequence(GapInfo gap)
    {
        isTraversing = true;
        physicsCore.isSqueezing = true;

        Vector3 localAxis = meshDeformer.transform.InverseTransformDirection(gap.squeezeAxis);
        float comp = gap.compressionNeeded;
        float exp = Mathf.Clamp(1f / Mathf.Sqrt(Mathf.Max(comp, 0.01f)), 1f, 2.5f);

        // ── 핵심: 통과 시작 전에 벽 레이어 충돌을 끔 ──────────────
        // 이렇게 해야 콜라이더가 줄어들기 전에 벽에 막히지 않음
        int jellyLayer = gameObject.layer;
        int wallLayer = GetWallLayerIndex();
        if (wallLayer >= 0)
            Physics.IgnoreLayerCollision(jellyLayer, wallLayer, true);

        // ── 1단계: 메시 변형 + 콜라이더를 함께 점진적으로 축소 ────
        float elapsed = 0f;
        float entryDuration = 0.5f;
        while (elapsed < entryDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / entryDuration);

            float curComp = Mathf.Lerp(1f, comp, t);
            float curExp = Mathf.Lerp(1f, exp, t);

            // 메시 변형
            meshDeformer.ApplySqueezeDeform(localAxis, curComp, curExp);

            // 콜라이더도 동일한 t로 점진적 축소
            if (adjustColliderOnSqueeze && capsuleCollider != null)
            {
                capsuleCollider.radius = Mathf.Lerp(originalColliderRadius, originalColliderRadius * comp, t);
                capsuleCollider.height = Mathf.Lerp(originalColliderHeight, originalColliderHeight * (1f / comp), t);
            }

            physicsCore.squeezeProgress = t * 0.4f;
            yield return null;
        }

        // ── 2단계: 이동 (콜라이더 충분히 줄었으므로 kinematic 이동) ─
        rb.isKinematic = true;
        Vector3 startPos = transform.position;
        float moveTime = Vector3.Distance(transform.position, gap.exitPoint) / traversalSpeed;
        elapsed = 0f;
        while (elapsed < moveTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveTime;
            transform.position = Vector3.Lerp(startPos, gap.exitPoint, Mathf.SmoothStep(0f, 1f, t));
            physicsCore.squeezeProgress = 0.4f + t * 0.4f;
            yield return null;
        }
        rb.isKinematic = false;

        // ── 3단계: 반동 복구 + 콜라이더도 함께 원래 크기로 ─────────
        elapsed = 0f;
        float exitDuration = 0.6f;
        while (elapsed < exitDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / exitDuration);

            float overshoot = 1f + Mathf.Sin(t * Mathf.PI) * 0.3f * (1f - t);
            float curComp = Mathf.Lerp(comp, overshoot, t);
            float curExp = Mathf.Lerp(exp, 1f / Mathf.Max(overshoot, 0.01f), t);

            meshDeformer.ApplySqueezeDeform(localAxis, curComp, curExp);

            // 콜라이더는 overshoot 없이 직선 복구 (벽에 걸리지 않게)
            if (adjustColliderOnSqueeze && capsuleCollider != null)
            {
                capsuleCollider.radius = Mathf.Lerp(originalColliderRadius * comp, originalColliderRadius, t);
                capsuleCollider.height = Mathf.Lerp(originalColliderHeight * (1f / comp), originalColliderHeight, t);
            }

            physicsCore.squeezeProgress = 0.8f + t * 0.2f;
            yield return null;
        }

        // ── 완전 복구 ────────────────────────────────────────────────
        // 콜라이더 원복 후 충돌 재활성화
        if (adjustColliderOnSqueeze && capsuleCollider != null)
        {
            capsuleCollider.radius = originalColliderRadius;
            capsuleCollider.height = originalColliderHeight;
            capsuleCollider.center = originalColliderCenter;
        }

        // 콜라이더가 완전히 원복된 뒤에 충돌 다시 켬
        if (wallLayer >= 0)
            Physics.IgnoreLayerCollision(jellyLayer, wallLayer, false);

        physicsCore.isSqueezing = false;
        isTraversing = false;
        physicsCore.squeezeProgress = 1f;
        physicsCore.currentDeformation = Vector3.zero;
        physicsCore.OnSqueezeComplete?.Invoke();
    }

    /// <summary>gapWallLayer 마스크에서 실제 레이어 인덱스를 추출합니다.</summary>
    private int GetWallLayerIndex()
    {
        int mask = gapWallLayer.value;
        for (int i = 0; i < 32; i++)
        {
            if ((mask & (1 << i)) != 0)
                return i; // 첫 번째 지정된 레이어 반환
        }
        return -1;
    }

    public void ForceTraverse(Vector3 direction, float gapSize)
    {
        TryTraverseGap(new GapInfo
        {
            detected = true,
            passAxis = direction,
            squeezeAxis = Vector3.Cross(direction, Vector3.up).normalized,
            gapWidth = gapSize,
            gapHeight = characterHeight,
            compressionNeeded = gapSize / (characterRadius * 2f),
            entryPoint = transform.position + direction * 0.5f,
            exitPoint = transform.position + direction * 1.5f
        });
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !currentGap.detected) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(currentGap.entryPoint, 0.15f);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(currentGap.entryPoint, currentGap.exitPoint);
        Vector3 right = Vector3.Cross(currentGap.passAxis, Vector3.up).normalized;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(currentGap.entryPoint - right * currentGap.gapWidth * 0.5f,
                        currentGap.entryPoint + right * currentGap.gapWidth * 0.5f);
    }
}