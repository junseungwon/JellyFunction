using UnityEngine;
using UnityEngine.Events;

namespace CharacterPressing
{
    /// <summary>
    /// CharacterDeform(Feature)의 모든 수치를 소유하고 조정하는 컨트롤러.
    /// UnityEvent도 이 컨트롤러가 소유하며, Feature의 C# event를 구독하여 발화합니다.
    /// </summary>
    public class CharacterPressController : MonoBehaviour
    {
        #region Inspector

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

        [Header("Bone 스케일")]
        [Tooltip("압축 시 해당 축 localScale 목표 최소값")]
        [Range(0.01f, 1f)]
        [SerializeField] float _boneScaleMin = 0.3f;

        [Tooltip("스케일 최저 한계")]
        [Range(0.01f, 1f)]
        [SerializeField] float _boneScaleMinLimit = 0.01f;

        [Header("위치 감소")]
        [Tooltip("압축 시 로컬 축으로 줄어들 총 거리")]
        [SerializeField] float _heightDownAmount = 0.5f;

        [Tooltip("위치 축 최저값 (로컬 좌표)")]
        [SerializeField] float _positionAxisMinValue = -1000f;

        [Header("연속 이동 (선택)")]
        [Tooltip("켜면 매 프레임 heightDownSpeedPerSecond만큼 위치 축으로 로컬 이동")]
        [SerializeField] bool _useContinuousHeightSpeed = false;

        [Tooltip("매 초당 로컬 위치 축 이동량 (감소 방향은 음수)")]
        [SerializeField] float _heightDownSpeedPerSecond = -0.01f;

        [Header("이벤트")]
        public UnityEvent onPressStart;
        public UnityEvent onRevertStart;
        public UnityEvent onPressComplete;
        public UnityEvent onRevertComplete;

        [Header("Debug")]
        [Tooltip("켜면 수치 적용 시 콘솔에 로그 출력")]
        [SerializeField] bool _showDebugLog = false;

        #endregion

        #region Unity Lifecycle

        void Awake()
        {
            if (_deformer == null)
                _deformer = GetComponent<CharacterDeform>();

            ApplyAll();
            SubscribeFeatureEvents();
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
            _deformer = GetComponent<CharacterDeform>();
        }

        #endregion

        #region Event Subscription

        void SubscribeFeatureEvents()
        {
            if (_deformer == null) return;
            _deformer.OnPressStarted += HandlePressStarted;
            _deformer.OnRevertStarted += HandleRevertStarted;
            _deformer.OnPressCompleted += HandlePressCompleted;
            _deformer.OnRevertCompleted += HandleRevertCompleted;
        }

        void UnsubscribeFeatureEvents()
        {
            if (_deformer == null) return;
            _deformer.OnPressStarted -= HandlePressStarted;
            _deformer.OnRevertStarted -= HandleRevertStarted;
            _deformer.OnPressCompleted -= HandlePressCompleted;
            _deformer.OnRevertCompleted -= HandleRevertCompleted;
        }

        void HandlePressStarted() => onPressStart?.Invoke();
        void HandleRevertStarted() => onRevertStart?.Invoke();
        void HandlePressCompleted() => onPressComplete?.Invoke();
        void HandleRevertCompleted() => onRevertComplete?.Invoke();

        #endregion

        #region Public API - 수치 조정

        public void ApplyAll()
        {
            if (_deformer == null) return;

            _deformer.PressDuration = _pressDuration;
            _deformer.PressCurve = _pressCurve;
            _deformer.RevertDuration = _revertDuration;
            _deformer.RevertCurve = _revertCurve;
            _deformer.BoneScaleMin = _boneScaleMin;
            _deformer.BoneScaleMinLimit = _boneScaleMinLimit;
            _deformer.HeightDownAmount = _heightDownAmount;
            _deformer.PositionAxisMinValue = _positionAxisMinValue;
            _deformer.UseContinuousHeightSpeed = _useContinuousHeightSpeed;
            _deformer.HeightDownSpeedPerSecond = _heightDownSpeedPerSecond;

            if (_showDebugLog)
                Debug.Log($"[CharacterPressController] 수치 일괄 적용 | PressDur:{_pressDuration} RevertDur:{_revertDuration} ScaleMin:{_boneScaleMin}");
        }

        public void SetPressDuration(float duration)
        {
            _pressDuration = Mathf.Max(0.001f, duration);
            if (_deformer != null) _deformer.PressDuration = _pressDuration;
        }

        public void SetPressCurve(AnimationCurve curve)
        {
            _pressCurve = curve;
            if (_deformer != null) _deformer.PressCurve = _pressCurve;
        }

        public void SetRevertDuration(float duration)
        {
            _revertDuration = Mathf.Max(0.001f, duration);
            if (_deformer != null) _deformer.RevertDuration = _revertDuration;
        }

        public void SetRevertCurve(AnimationCurve curve)
        {
            _revertCurve = curve;
            if (_deformer != null) _deformer.RevertCurve = _revertCurve;
        }

        public void SetBoneScaleMin(float value)
        {
            _boneScaleMin = Mathf.Clamp(value, 0.01f, 1f);
            if (_deformer != null) _deformer.BoneScaleMin = _boneScaleMin;
        }

        public void SetBoneScaleMinLimit(float value)
        {
            _boneScaleMinLimit = Mathf.Clamp(value, 0.01f, 1f);
            if (_deformer != null) _deformer.BoneScaleMinLimit = _boneScaleMinLimit;
        }

        public void SetHeightDownAmount(float value)
        {
            _heightDownAmount = value;
            if (_deformer != null) _deformer.HeightDownAmount = _heightDownAmount;
        }

        public void SetPositionAxisMinValue(float value)
        {
            _positionAxisMinValue = value;
            if (_deformer != null) _deformer.PositionAxisMinValue = _positionAxisMinValue;
        }

        public void SetUseContinuousHeightSpeed(bool value)
        {
            _useContinuousHeightSpeed = value;
            if (_deformer != null) _deformer.UseContinuousHeightSpeed = _useContinuousHeightSpeed;
        }

        public void SetHeightDownSpeedPerSecond(float value)
        {
            _heightDownSpeedPerSecond = value;
            if (_deformer != null) _deformer.HeightDownSpeedPerSecond = _heightDownSpeedPerSecond;
        }

        #endregion
    }
}
