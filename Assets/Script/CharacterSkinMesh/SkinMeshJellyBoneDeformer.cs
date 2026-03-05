using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// SkinnedMeshRenderer + Humanoid 캐릭터에 젤리 변형을 적용합니다.
/// LateUpdate에서 본의 localScale만 조작하여 애니메이션과 충돌 없이 동작합니다.
/// 
/// [배치 위치] Animator가 있는 캐릭터 모델 오브젝트에 붙입니다.
/// SkinMeshJellyPhysicsCore는 부모(또는 자신)에서 자동으로 찾습니다.
/// </summary>
public class SkinMeshJellyBoneDeformer : MonoBehaviour
{
    [Header("본 스프링")]
    [Tooltip("본별 스프링 탄성 — 높을수록 빠르게 원래 스케일로 복귀")]
    [Range(5f, 80f)] public float boneSpringStrength = 25f;
    [Tooltip("본별 스프링 감쇠 — 높을수록 진동이 빨리 수렴")]
    [Range(1f, 15f)] public float boneDamping = 4f;

    [Header("코어 변형 (스쿼시/스트레치)")]
    [Tooltip("PhysicsCore 변형값에 곱해지는 배율 — 높을수록 스쿼시/스트레치가 과장됨")]
    [Range(0f, 5f)] public float deformMultiplier = 3.0f;
    [Tooltip("스쿼시↔스트레치 비율 — 1에 가까울수록 XZ 보상이 커져 찌그러진 느낌 강해짐")]
    [Range(0f, 1f)] public float squashStretchRatio = 0.8f;
    [Tooltip("true면 Y축 압축 시 XZ축이 자동 팽창하여 부피를 보존")]
    public bool preserveVolume = true;

    [Header("체인 워블 (이동 시 출렁임)")]
    [Tooltip("본 체인을 따라 전파되는 워블 진폭")]
    [Range(0f, 0.5f)] public float chainWobbleAmplitude = 0.12f;
    [Tooltip("본 체인 간 위상 지연 (초)")]
    [Range(0f, 0.5f)] public float chainPhaseDelay = 0.08f;

    [Header("이동 젤리 (본 스프링 구동)")]
    [Tooltip("이동 시 각 본 스프링에 가해지는 출렁임 힘 — 높을수록 물렁물렁")]
    [Range(0f, 3f)] public float movementJiggleStrength = 1.2f;

    [Header("기본 출렁임 (Idle)")]
    [Tooltip("가만히 있어도 적용되는 호흡/젤리 출렁임 강도")]
    [Range(0f, 1f)] public float idleJiggleStrength = 0.3f;
    [Tooltip("Idle 출렁임 속도 (Hz)")]
    [Range(0.5f, 5f)] public float idleJiggleFrequency = 1.5f;

    [Header("리플 (충격 파동)")]
    [Tooltip("충격 시 본 체인을 따라 퍼지는 파동 효과 ON/OFF")]
    public bool enableRipple = true;
    [Tooltip("리플 파동이 본 스케일에 미치는 세기")]
    [Range(0f, 0.3f)] public float rippleStrength = 0.06f;
    [Tooltip("리플 파동 전파 속도 (rad/s) — 높을수록 빠르게 퍼짐")]
    public float rippleSpeed = 5f;
    [Tooltip("리플 감쇠 속도 — 높을수록 파동이 빨리 사라짐")]
    public float rippleDecay = 3f;

    [Header("사지 반응 감쇠")]
    [Tooltip("팔/다리의 변형 반응 비율 (1=몸통과 동일, 0=반응 없음)")]
    [Range(0f, 1f)] public float limbResponseFactor = 0.4f;

    [Header("착지 보정")]
    [Tooltip("스쿼시 시 Hips를 아래로 보정하여 발이 바닥에 유지되도록")]
    public bool compensateGroundOnSquash = true;
    [Tooltip("스쿼시 시 Hips를 아래로 밀어주는 양의 배율 — 높을수록 더 많이 내려감")]
    [Range(0f, 2f)] public float groundCompensationScale = 0.3f;

    // ── 내부 데이터 ───────────────────────────────────────

    private Animator animator;
    private SkinMeshJellyPhysicsCore physicsCore;

    private class BoneState
    {
        public Transform transform;
        public Vector3 originalScale;
        public Vector3 springOffset;
        public Vector3 springVelocity;
        public int chainIndex;
        public float weight;
        public bool isHips;
    }

    private BoneState[] boneStates;
    private BoneState hipsBone;
    private float time;

    private Vector3 squeezeScaleOverride = Vector3.one;
    private bool isSqueezingScale;

    private struct RippleWave
    {
        public float strength;
        public float startTime;
        public Vector3 scaleDirection;
    }
    private readonly List<RippleWave> ripples = new List<RippleWave>();

    private static readonly (HumanBodyBones bone, int chain, float weight, bool isHips)[] BoneConfig =
    {
        (HumanBodyBones.Hips,          0, 1.0f, true),
        (HumanBodyBones.Spine,         1, 0.9f, false),
        (HumanBodyBones.Chest,         2, 0.7f, false),
        (HumanBodyBones.UpperChest,    3, 0.5f, false),
        (HumanBodyBones.Head,          4, 0.3f, false),
        (HumanBodyBones.LeftUpperLeg,  1, 0.6f, false),
        (HumanBodyBones.LeftLowerLeg,  2, 0.3f, false),
        (HumanBodyBones.RightUpperLeg, 1, 0.6f, false),
        (HumanBodyBones.RightLowerLeg, 2, 0.3f, false),
        (HumanBodyBones.LeftUpperArm,  3, 0.4f, false),
        (HumanBodyBones.LeftLowerArm,  4, 0.2f, false),
        (HumanBodyBones.RightUpperArm, 3, 0.4f, false),
        (HumanBodyBones.RightLowerArm, 4, 0.2f, false),
    };

    // ── 초기화 ────────────────────────────────────────────

    private void Awake()
    {
        animator = GetComponent<Animator>();
        physicsCore = GetComponentInParent<SkinMeshJellyPhysicsCore>();

        if (animator == null)
        {
            Debug.LogError($"[SkinMeshJellyBoneDeformer] Animator가 없습니다! ({gameObject.name})");
            enabled = false;
            return;
        }
        if (physicsCore == null)
        {
            Debug.LogError($"[SkinMeshJellyBoneDeformer] 부모 계층에 SkinMeshJellyPhysicsCore가 없습니다! ({gameObject.name})");
            enabled = false;
            return;
        }

        physicsCore.OnImpact += OnImpact;
        physicsCore.OnSqueezeProgress += OnSqueezeProgress;
    }

    private void Start()
    {
        InitializeBones();
    }

    private void InitializeBones()
    {
        var list = new List<BoneState>();

        foreach (var (bone, chain, weight, isHips) in BoneConfig)
        {
            Transform t = animator.GetBoneTransform(bone);
            if (t == null) continue;

            bool isCoreBone = bone == HumanBodyBones.Hips ||
                              bone == HumanBodyBones.Spine ||
                              bone == HumanBodyBones.Chest ||
                              bone == HumanBodyBones.UpperChest;

            float finalWeight = isCoreBone ? weight : weight * limbResponseFactor;

            var state = new BoneState
            {
                transform = t,
                originalScale = t.localScale,
                springOffset = Vector3.zero,
                springVelocity = Vector3.zero,
                chainIndex = chain,
                weight = finalWeight,
                isHips = isHips,
            };

            list.Add(state);
            if (isHips) hipsBone = state;
        }

        boneStates = list.ToArray();
    }

    // ── 메인 루프 (Animator 이후 실행) ────────────────────

    private void LateUpdate()
    {
        if (boneStates == null || boneStates.Length == 0) return;

        time += Time.deltaTime;

        DriveMovementJiggle();
        UpdateBoneSprings();
        UpdateRipples();

        if (physicsCore.isSqueezing)
            ApplySqueezeMode();
        else
            ApplyNormalMode();
    }

    // ── 이동 젤리 (스프링 구동) ───────────────────────────

    private void DriveMovementJiggle()
    {
        float speed = physicsCore.CurrentSpeed;
        bool isMoving = speed > 0.3f;

        for (int i = 0; i < boneStates.Length; i++)
        {
            var b = boneStates[i];
            Vector3 force = Vector3.zero;

            if (isMoving && movementJiggleStrength > 0.001f)
            {
                float speedFactor = Mathf.Clamp01(speed / 5f);
                float movePhase = time * physicsCore.wobbleFrequency * 1.5f
                                  - b.chainIndex * chainPhaseDelay * 15f;
                float moveWave = Mathf.Sin(movePhase);

                force += new Vector3(
                    moveWave * 0.7f,
                    -moveWave,
                    moveWave * 0.7f
                ) * movementJiggleStrength * speedFactor * b.weight;
            }

            if (idleJiggleStrength > 0.001f)
            {
                float idlePhase1 = time * idleJiggleFrequency * Mathf.PI * 2f
                                   - b.chainIndex * 0.6f;
                float idlePhase2 = time * idleJiggleFrequency * Mathf.PI * 1.3f
                                   - b.chainIndex * 0.9f;

                float yBreath = Mathf.Sin(idlePhase1);
                float xzSway = Mathf.Sin(idlePhase2) * 0.5f;

                force += new Vector3(
                    xzSway,
                    yBreath,
                    xzSway * 0.6f
                ) * idleJiggleStrength * b.weight;
            }

            b.springVelocity += force * Time.deltaTime;
        }
    }

    // ── 본별 스프링 ──────────────────────────────────────

    private void UpdateBoneSprings()
    {
        for (int i = 0; i < boneStates.Length; i++)
        {
            var b = boneStates[i];
            Vector3 spring = -b.springOffset * boneSpringStrength;
            Vector3 damp = -b.springVelocity * boneDamping;

            b.springVelocity += (spring + damp) * Time.deltaTime;
            b.springOffset += b.springVelocity * Time.deltaTime;
        }
    }

    // ── 리플 (충격 파동) ──────────────────────────────────

    private void UpdateRipples()
    {
        if (!enableRipple || ripples.Count == 0) return;

        for (int ri = ripples.Count - 1; ri >= 0; ri--)
        {
            float elapsed = time - ripples[ri].startTime;
            float decay = ripples[ri].strength * Mathf.Exp(-rippleDecay * elapsed);

            if (decay < 0.001f)
            {
                ripples.RemoveAt(ri);
                continue;
            }

            for (int i = 0; i < boneStates.Length; i++)
            {
                float wave = Mathf.Sin(boneStates[i].chainIndex * 1.5f - elapsed * rippleSpeed);
                Vector3 effect = ripples[ri].scaleDirection * (wave * decay * rippleStrength);
                boneStates[i].springVelocity += effect;
            }
        }
    }

    // ── 일반 모드: 코어 변형 + 워블 + 스프링 ──────────────

    private void ApplyNormalMode()
    {
        Vector3 coreScale = ComputeCoreScale();

        for (int i = 0; i < boneStates.Length; i++)
        {
            var b = boneStates[i];
            Vector3 finalScale;

            if (b.isHips)
            {
                finalScale = coreScale + b.springOffset;
            }
            else
            {
                Vector3 wobble = ComputeChainWobble(b.chainIndex, b.weight);
                finalScale = Vector3.one + b.springOffset * b.weight + wobble;
            }

            ClampScale(ref finalScale);
            b.transform.localScale = Vector3.Scale(b.originalScale, finalScale);
        }

        ApplyGroundCompensation(coreScale.y);
    }

    // ── 스퀴즈 모드: GapTraversal이 직접 제어 + 스프링 ────

    private void ApplySqueezeMode()
    {
        for (int i = 0; i < boneStates.Length; i++)
        {
            var b = boneStates[i];
            Vector3 finalScale;

            if (b.isHips && isSqueezingScale)
            {
                finalScale = squeezeScaleOverride + b.springOffset;
            }
            else
            {
                Vector3 weightedSqueeze = Vector3.Lerp(Vector3.one, squeezeScaleOverride, b.weight);
                finalScale = weightedSqueeze + b.springOffset * b.weight;
            }

            ClampScale(ref finalScale);
            b.transform.localScale = Vector3.Scale(b.originalScale, finalScale);
        }
    }

    // ── 코어 스케일 계산 ──────────────────────────────────

    private Vector3 ComputeCoreScale()
    {
        Vector3 coreDeform = physicsCore.currentDeformation * deformMultiplier;

        if (preserveVolume)
        {
            float yScale = Mathf.Clamp(1f + coreDeform.y, physicsCore.maxCompression, physicsCore.maxExpansion);
            float xzScale = Mathf.Lerp(1f, Mathf.Sqrt(1f / Mathf.Max(yScale, 0.01f)), squashStretchRatio);
            return new Vector3(xzScale, yScale, xzScale);
        }

        return Vector3.one + coreDeform;
    }

    // ── 체인 워블 ─────────────────────────────────────────

    private Vector3 ComputeChainWobble(int chainIndex, float weight)
    {
        float speed = physicsCore.CurrentSpeed;
        if (speed < 0.5f) return Vector3.zero;

        float intensity = Mathf.Clamp01(speed / 5f) * chainWobbleAmplitude * weight;
        float phase = time * physicsCore.wobbleFrequency - chainIndex * chainPhaseDelay;
        float wave = Mathf.Sin(phase * Mathf.PI * 2f);

        return new Vector3(wave * intensity, -wave * intensity * 0.5f, wave * intensity);
    }

    // ── 착지 보정 ─────────────────────────────────────────

    private void ApplyGroundCompensation(float yScale)
    {
        if (!compensateGroundOnSquash || hipsBone == null) return;
        if (yScale >= 0.99f) return;

        float offset = (1f - yScale) * groundCompensationScale;
        hipsBone.transform.localPosition += Vector3.down * offset;
    }

    // ── 유틸리티 ──────────────────────────────────────────

    private static void ClampScale(ref Vector3 scale)
    {
        scale.x = Mathf.Max(scale.x, 0.05f);
        scale.y = Mathf.Max(scale.y, 0.05f);
        scale.z = Mathf.Max(scale.z, 0.05f);
    }

    // ── 외부 호출 (SkinMeshJellyGapTraversal에서 사용) ────

    /// <summary>
    /// 틈 통과 시 월드 스페이스 압축 축 기준으로 본 스케일을 변형합니다.
    /// </summary>
    public void ApplySqueezeDeform(Vector3 worldSqueezeAxis, float compression, float expansion)
    {
        if (hipsBone == null) return;

        isSqueezingScale = true;

        Vector3 localAxis = hipsBone.transform.InverseTransformDirection(worldSqueezeAxis).normalized;

        Vector3 scale;
        scale.x = Mathf.Lerp(expansion, compression, Mathf.Abs(localAxis.x));
        scale.y = Mathf.Lerp(expansion, compression, Mathf.Abs(localAxis.y));
        scale.z = Mathf.Lerp(expansion, compression, Mathf.Abs(localAxis.z));

        squeezeScaleOverride = scale;
    }

    /// <summary>스퀴즈 상태를 초기화합니다.</summary>
    public void ResetSqueeze()
    {
        isSqueezingScale = false;
        squeezeScaleOverride = Vector3.one;

        if (boneStates == null) return;
        for (int i = 0; i < boneStates.Length; i++)
            boneStates[i].transform.localScale = boneStates[i].originalScale;
    }

    // ── 이벤트 핸들러 ─────────────────────────────────────

    private void OnImpact(Vector3 direction, float strength)
    {
        if (boneStates == null) return;

        if (enableRipple)
        {
            ripples.Add(new RippleWave
            {
                strength = strength,
                startTime = time,
                scaleDirection = new Vector3(
                    Mathf.Abs(direction.x),
                    direction.y,
                    Mathf.Abs(direction.z)
                ) * 0.3f
            });
        }

        for (int i = 0; i < boneStates.Length; i++)
        {
            boneStates[i].springVelocity += new Vector3(
                direction.x * strength,
                direction.y * strength * 0.5f,
                direction.z * strength
            ) * boneStates[i].weight;
        }
    }

    private void OnSqueezeProgress(float progress) { }

    // ── 정리 ──────────────────────────────────────────────

    private void OnDestroy()
    {
        if (boneStates != null)
        {
            for (int i = 0; i < boneStates.Length; i++)
            {
                if (boneStates[i].transform != null)
                    boneStates[i].transform.localScale = boneStates[i].originalScale;
            }
        }

        if (physicsCore != null)
        {
            physicsCore.OnImpact -= OnImpact;
            physicsCore.OnSqueezeProgress -= OnSqueezeProgress;
        }
    }
}
