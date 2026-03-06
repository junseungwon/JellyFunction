using UnityEngine;
using UnityEngine.Events;

namespace SpherifySystem
{
    public class SpherifyController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] SpherifyDeformer deformer;

        [Header("Transition Settings")]
        [SerializeField] float          transitionDuration = 0.5f;
        [SerializeField] AnimationCurve easingCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Events")]
        public UnityEvent onSphereComplete;   // 구형 전환 완료 시
        public UnityEvent onRevertComplete;   // 원본 복원 완료 시

        [Header("Auto Start")]
        [SerializeField] bool transformToSphereOnStart = true;
        [SerializeField] bool logOnSphereComplete;

        [Header("Debug")]
        [SerializeField] bool _showDebugLog = true;

        // ── 내부 상태 ────────────────────────────────────────────
        float currentT    = 0f;
        float startT      = 0f;
        float targetT     = 0f;
        float elapsedTime = 0f;
        bool  isTransitioning = false;

        // 현재 SpherifyAmount 외부 참조용
        public float CurrentAmount => currentT;

        // ── 공개 API ─────────────────────────────────────────────

        /// <summary>구형으로 전환</summary>
        public void TransformToSphere()
        {
            if (_showDebugLog)
                Debug.Log($"[SpherifyController] 구형 전환 시작 | 소요 시간: {transitionDuration}s");
            SetTarget(1f);
        }

        /// <summary>원본으로 복원</summary>
        public void RevertToOriginal()
        {
            if (_showDebugLog)
                Debug.Log("[SpherifyController] 원본 복원 시작");
            SetTarget(0f);
        }

        /// <summary>0~1 사이 임의 비율로 설정</summary>
        public void SetSpherifyRatio(float ratio)
        {
            if (_showDebugLog)
                Debug.Log($"[SpherifyController] SpherifyRatio 설정: {ratio:F2}");
            SetTarget(Mathf.Clamp01(ratio));
        }

        /// <summary>즉시 구형으로 전환 (애니메이션 없음)</summary>
        public void SnapToSphere()
        {
            currentT = targetT = startT = 1f;
            elapsedTime    = transitionDuration;
            isTransitioning = false;
            deformer.SpherifyAmount = 1f;

            if (_showDebugLog)
                Debug.Log("[SpherifyController] ✅ 즉시 구형 전환 완료 (Snap)");
        }

        /// <summary>즉시 원본으로 복원 (애니메이션 없음)</summary>
        public void SnapToOriginal()
        {
            currentT = targetT = startT = 0f;
            elapsedTime    = transitionDuration;
            isTransitioning = false;
            deformer.ForceRevert();

            if (_showDebugLog)
                Debug.Log("[SpherifyController] ✅ 즉시 원본 복원 완료 (Snap)");
        }

        // ── 내부 전환 처리 ───────────────────────────────────────
        void SetTarget(float t)
        {
            if (Mathf.Approximately(t, targetT) && isTransitioning == false) return;

            startT          = currentT; // 현재 위치를 시작점으로 저장 (역전 안전)
            targetT         = t;
            elapsedTime     = 0f;
            isTransitioning = true;
        }

        void Update()
        {
            if (!isTransitioning) return;

            elapsedTime += Time.deltaTime;
            float progress      = Mathf.Clamp01(elapsedTime / transitionDuration);
            float easedProgress = easingCurve.Evaluate(progress);

            currentT = Mathf.Lerp(startT, targetT, easedProgress);
            deformer.SpherifyAmount = currentT;

            // 전환 완료 체크
            if (progress >= 1f)
            {
                currentT        = targetT;
                isTransitioning = false;

                if (Mathf.Approximately(targetT, 1f))
                {
                    if (_showDebugLog)
                        Debug.Log("[SpherifyController] ✅ 구형 전환 완료");
                    onSphereComplete?.Invoke();
                }
                else if (Mathf.Approximately(targetT, 0f))
                {
                    if (_showDebugLog)
                        Debug.Log("[SpherifyController] ✅ 원본 복원 완료");
                    onRevertComplete?.Invoke();
                }
            }
        }

        /// <summary>AutoStart 비활성화 — Sequencer가 Awake에서 호출해 흐름을 직접 제어</summary>
        public void DisableAutoStart()
        {
            transformToSphereOnStart = false;
            if (_showDebugLog)
                Debug.Log("[SpherifyController] AutoStart 비활성화 (Sequencer 제어 모드)");
        }

        // ── Inspector 자동 연결 ──────────────────────────────────
        void Reset()
        {
            deformer = GetComponent<SpherifyDeformer>();
        }

        void Start()
        {
            if (logOnSphereComplete)
                onSphereComplete.AddListener(() => Debug.Log("구형 전환 완료!"));

            if (transformToSphereOnStart)
                TransformToSphere();
        }
    }
}
