using UnityEngine;
using UnityEngine.Events;

namespace SpherifySystem
{
    /// <summary>
    /// SpherifyDeformer(Feature)의 모든 수치를 소유하고 조정하는 컨트롤러.
    /// AutoStart 설정과 UnityEvent도 이 컨트롤러가 소유합니다.
    /// Feature의 C# event를 구독하여 UnityEvent를 발화합니다.
    /// </summary>
    public class SpherifyController : MonoBehaviour
    {
        #region Inspector

        [Header("참조")]
        [SerializeField] SpherifyDeformer _deformer = null;

        [Header("전환 설정")]
        [Tooltip("전환 시간(초)")]
        [SerializeField] float _transitionDuration = 0.5f;

        [Tooltip("전환 보간 커브")]
        [SerializeField] AnimationCurve _easingCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Auto Start")]
        [Tooltip("Start에서 자동으로 구형 전환 시작")]
        [SerializeField] bool _transformToSphereOnStart = false;

        [Header("Sphere 설정")]
        [Tooltip("자동 반지름 계산 사용 여부")]
        [SerializeField] bool _autoCalcRadius = true;

        [Tooltip("수동 반지름 값")]
        [SerializeField] float _manualRadius = 1f;

        [Header("Performance")]
        [Tooltip("Job System 사용 여부")]
        [SerializeField] bool _useJobSystem = true;

        [Header("이벤트")]
        public UnityEvent onSphereComplete;
        public UnityEvent onRevertComplete;

        [Header("Debug")]
        [Tooltip("켜면 수치 적용 시 콘솔에 로그 출력")]
        [SerializeField] bool _showDebugLog = false;

        #endregion

        #region Unity Lifecycle

        void Awake()
        {
            if (_deformer == null)
                _deformer = GetComponent<SpherifyDeformer>();

            ApplyAll();
            SubscribeFeatureEvents();
        }

        void Start()
        {
            if (_transformToSphereOnStart && _deformer != null)
                _deformer.TransformToSphere();
        }

        void OnDestroy()
        {
            UnsubscribeFeatureEvents();
        }

        void OnValidate()
        {
            if (Application.isPlaying && _deformer != null)
                ApplyAll();
        }

        void Reset()
        {
            _deformer = GetComponent<SpherifyDeformer>();
        }

        #endregion

        #region Event Subscription

        void SubscribeFeatureEvents()
        {
            if (_deformer == null) return;
            _deformer.OnSphereCompleted += HandleSphereCompleted;
            _deformer.OnRevertCompleted += HandleRevertCompleted;
        }

        void UnsubscribeFeatureEvents()
        {
            if (_deformer == null) return;
            _deformer.OnSphereCompleted -= HandleSphereCompleted;
            _deformer.OnRevertCompleted -= HandleRevertCompleted;
        }

        void HandleSphereCompleted() => onSphereComplete?.Invoke();
        void HandleRevertCompleted() => onRevertComplete?.Invoke();

        #endregion

        #region Public API - 수치 조정

        public void ApplyAll()
        {
            if (_deformer == null) return;

            _deformer.TransitionDuration = _transitionDuration;
            _deformer.EasingCurve = _easingCurve;
            _deformer.AutoCalcRadius = _autoCalcRadius;
            _deformer.ManualRadius = _manualRadius;
            _deformer.UseJobSystem = _useJobSystem;

            if (_showDebugLog)
                Debug.Log($"[SpherifyController] 수치 일괄 적용 | Duration:{_transitionDuration} Radius:{_manualRadius} JobSystem:{_useJobSystem}");
        }

        public void SetTransitionDuration(float duration)
        {
            _transitionDuration = Mathf.Max(0.001f, duration);
            if (_deformer != null) _deformer.TransitionDuration = _transitionDuration;
        }

        public void SetEasingCurve(AnimationCurve curve)
        {
            _easingCurve = curve;
            if (_deformer != null) _deformer.EasingCurve = _easingCurve;
        }

        public void SetManualRadius(float radius)
        {
            _manualRadius = Mathf.Max(0.001f, radius);
            if (_deformer != null) _deformer.ManualRadius = _manualRadius;
        }

        public void SetAutoCalcRadius(bool auto)
        {
            _autoCalcRadius = auto;
            if (_deformer != null) _deformer.AutoCalcRadius = _autoCalcRadius;
        }

        public void SetUseJobSystem(bool use)
        {
            _useJobSystem = use;
            if (_deformer != null) _deformer.UseJobSystem = _useJobSystem;
        }

        /// <summary>AutoStart 비활성화 — Sequencer가 Awake에서 호출해 흐름을 직접 제어</summary>
        public void DisableAutoStart()
        {
            _transformToSphereOnStart = false;
            if (_showDebugLog)
                Debug.Log("[SpherifyController] AutoStart 비활성화 (Sequencer 제어 모드)");
        }

        #endregion
    }
}
