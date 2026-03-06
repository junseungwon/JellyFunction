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
        [SerializeField] bool transformToSphereOnStart = true;  // true면 Start 시 구형으로 전환
        [SerializeField] bool logOnSphereComplete;               // 구형 전환 완료 시 디버그 로그

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
        public void TransformToSphere() => SetTarget(1f);

        /// <summary>원본으로 복원</summary>
        public void RevertToOriginal() => SetTarget(0f);

        /// <summary>0~1 사이 임의 비율로 설정</summary>
        public void SetSpherifyRatio(float ratio) => SetTarget(Mathf.Clamp01(ratio));

        /// <summary>즉시 구형으로 전환 (애니메이션 없음)</summary>
        public void SnapToSphere()
        {
            currentT = targetT = startT = 1f;
            elapsedTime    = transitionDuration;
            isTransitioning = false;
            deformer.SpherifyAmount = 1f;
        }

        /// <summary>즉시 원본으로 복원 (애니메이션 없음)</summary>
        public void SnapToOriginal()
        {
            currentT = targetT = startT = 0f;
            elapsedTime    = transitionDuration;
            isTransitioning = false;
            deformer.ForceRevert();
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
                    onSphereComplete?.Invoke();
                else if (Mathf.Approximately(targetT, 0f))
                    onRevertComplete?.Invoke();
            }
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
