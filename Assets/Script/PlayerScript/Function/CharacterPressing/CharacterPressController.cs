using UnityEngine;
using UnityEngine.Events;

namespace CharacterPressing
{
    /// <summary>
    /// CharacterDeform을 이용해 압축(Press)과 팽창(Revert) 전환을 제어하는 컨트롤러.
    /// Press와 Revert 각각 독립된 Duration과 AnimationCurve로 속도를 설정할 수 있습니다.
    /// </summary>
    public class CharacterPressController : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("변형을 담당하는 CharacterDeform 컴포넌트")]
        [SerializeField] CharacterDeform _deformer = null;

        [Header("압축 (Press: 0 → 1)")]
        [Tooltip("압축 전환 시간(초)")]
        [SerializeField] float _pressDuration = 0.5f;

        [Tooltip("압축 보간 커브. X=시간 진행(0~1), Y=DeformAmount 보간(0~1)")]
        [SerializeField] AnimationCurve _pressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("팽창 (Revert: 1 → 0)")]
        [Tooltip("팽창(복원) 전환 시간(초)")]
        [SerializeField] float _revertDuration = 0.5f;

        [Tooltip("팽창 보간 커브. X=시간 진행(0~1), Y=DeformAmount 보간(0~1)")]
        [SerializeField] AnimationCurve _revertCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("이벤트")]
        /// <summary>Press() 호출 직후 (전환 시작 시점)</summary>
        public UnityEvent onPressStart;
        /// <summary>Revert() 호출 직후 (전환 시작 시점)</summary>
        public UnityEvent onRevertStart;
        public UnityEvent onPressComplete;
        public UnityEvent onRevertComplete;

        [Header("Debug")]
        [Tooltip("켜면 압축·팽창 시작/완료 시 콘솔에 로그 출력")]
        [SerializeField] bool _showDebugLog = false;

        enum TransitionMode { None, Pressing, Reverting }

        TransitionMode _mode = TransitionMode.None;
        float _transitionStartAmount;
        float _transitionTargetAmount;
        float _transitionElapsed;

        /// <summary>현재 DeformAmount (0~1)</summary>
        public float CurrentAmount => _deformer != null ? _deformer.DeformAmount : 0f;

        // ─── 공개 메서드 ─────────────────────────────────────────

        /// <summary>압축 시작: DeformAmount를 현재값에서 1로 _pressDuration 동안 전환합니다.</summary>
        public void Press()
        {
            if (_deformer == null) return;

            if (_showDebugLog)
                Debug.Log($"[CharacterPressController] Press 시작 | {_deformer.DeformAmount:F2} → 1 | 시간: {_pressDuration}s");

            StartTransition(TransitionMode.Pressing, _deformer.DeformAmount, 1f);
            onPressStart?.Invoke();
        }

        /// <summary>팽창(복원) 시작: DeformAmount를 현재값에서 0으로 _revertDuration 동안 전환합니다.</summary>
        public void Revert()
        {
            if (_deformer == null) return;

            if (_showDebugLog)
                Debug.Log($"[CharacterPressController] Revert 시작 | {_deformer.DeformAmount:F2} → 0 | 시간: {_revertDuration}s");

            StartTransition(TransitionMode.Reverting, _deformer.DeformAmount, 0f);
            onRevertStart?.Invoke();
        }

        /// <summary>즉시 완전 압축 상태(DeformAmount=1)로 설정합니다.</summary>
        public void SnapToPress()
        {
            if (_deformer == null) return;
            _mode = TransitionMode.None;
            _deformer.DeformAmount = 1f;

            if (_showDebugLog)
                Debug.Log("[CharacterPressController] SnapToPress (즉시 압축)");
        }

        /// <summary>즉시 원래 상태(DeformAmount=0)로 설정합니다.</summary>
        public void SnapToOriginal()
        {
            if (_deformer == null) return;
            _mode = TransitionMode.None;
            _deformer.DeformAmount = 0f;

            if (_showDebugLog)
                Debug.Log("[CharacterPressController] SnapToOriginal (즉시 복원)");
        }

        // ─── 내부 로직 ───────────────────────────────────────────

        void StartTransition(TransitionMode mode, float from, float to)
        {
            _mode = mode;
            _transitionStartAmount = from;
            _transitionTargetAmount = to;
            _transitionElapsed = 0f;
        }

        void Update()
        {
            TickTransition();
        }

        void TickTransition()
        {
            if (_mode == TransitionMode.None || _deformer == null) return;

            float duration = _mode == TransitionMode.Pressing ? _pressDuration : _revertDuration;
            AnimationCurve curve = _mode == TransitionMode.Pressing ? _pressCurve : _revertCurve;

            _transitionElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(_transitionElapsed / duration);
            float eased = curve.Evaluate(progress);

            _deformer.DeformAmount = Mathf.Lerp(_transitionStartAmount, _transitionTargetAmount, eased);

            if (progress >= 1f)
                CompleteTransition();
        }

        void CompleteTransition()
        {
            bool wasPressing = _mode == TransitionMode.Pressing;
            _mode = TransitionMode.None;

            if (wasPressing)
            {
                if (_showDebugLog)
                    Debug.Log("[CharacterPressController] 압축 완료 (DeformAmount=1)");
                onPressComplete?.Invoke();
            }
            else
            {
                if (_showDebugLog)
                    Debug.Log("[CharacterPressController] 팽창(복원) 완료 (DeformAmount=0)");
                onRevertComplete?.Invoke();
            }
        }

        void Reset()
        {
            _deformer = GetComponent<CharacterDeform>();
        }
    }
}
