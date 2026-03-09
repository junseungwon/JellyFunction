using System;
using UnityEngine;

namespace CharacterPressing
{
    /// <summary>압축/위치에 사용할 로컬 축 (X/Y/Z)</summary>
    public enum PressAxis
    {
        X,
        Y,
        Z
    }

    /// <summary>
    /// Bone 스케일 변경 + 대상 오브젝트의 로컬 위치 감소를 담당하는 순수 실행 엔진.
    /// 수치 파라미터는 Inspector에 노출하지 않으며, Controller가 프로퍼티를 통해 설정합니다.
    /// 전환 로직(Press/Revert)은 자체 엔진으로 동작합니다.
    /// 모든 변환은 Local 기준입니다.
    /// </summary>
    public class CharacterDeform : MonoBehaviour
    {
        #region Inspector - 오브젝트 참조 / 구조적 설정

        [Header("대상 (Inspector에서 할당)")]
        [Tooltip("스케일을 변경할 핵심 Bone")]
        [SerializeField] Transform _coreBone = null;

        [Tooltip("위치를 줄일 오브젝트 (로컬 위치 적용)")]
        [SerializeField] Transform _heightTarget = null;

        [Header("축 설정")]
        [Tooltip("Bone 스케일을 줄일 로컬 축 (X/Y/Z)")]
        [SerializeField] PressAxis _boneScaleAxis = PressAxis.Y;

        [Tooltip("위치를 줄일 로컬 축 (X/Y/Z)")]
        [SerializeField] PressAxis _positionAxis = PressAxis.Y;

        [Header("Debug")]
        [Tooltip("켜면 초기화·전환 시작/완료 시 콘솔에 로그 출력")]
        [SerializeField] bool _showDebugLog = false;

        [Tooltip("켜면 Scene 뷰에 위치 축·최저값·Bone 축을 기즈모로 표시")]
        [SerializeField] bool _drawGizmos = false;

        [Tooltip("기즈모 선/구 크기")]
        [SerializeField] float _gizmoSize = 0.5f;

        #endregion

        #region Private Fields - 수치 (Controller가 프로퍼티로 설정)

        float _boneScaleMin = 0.3f;
        float _boneScaleMinLimit = 0.01f;
        float _heightDownAmount = 0.5f;
        float _positionAxisMinValue = -1000f;
        bool _useContinuousHeightSpeed = false;
        float _heightDownSpeedPerSecond = -0.01f;
        float _pressDuration = 0.5f;
        AnimationCurve _pressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        float _revertDuration = 0.5f;
        AnimationCurve _revertCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        #endregion

        #region Private Fields - 런타임 상태

        float _initialBoneScaleAxis;
        float _initialLocalPosAxis;
        Vector3 _initialLocalScaleBone;
        bool _initialized;
        float _deformAmount;

        #endregion

        #region Transition State

        enum TransitionMode { None, Pressing, Reverting }

        TransitionMode _mode = TransitionMode.None;
        float _transitionStartAmount;
        float _transitionTargetAmount;
        float _transitionElapsed;

        #endregion

        #region Events - 콜백 (Controller가 구독)

        /// <summary>Press 전환 시작 시 호출</summary>
        public event Action OnPressStarted;
        /// <summary>Revert 전환 시작 시 호출</summary>
        public event Action OnRevertStarted;
        /// <summary>Press 전환 완료 시 호출</summary>
        public event Action OnPressCompleted;
        /// <summary>Revert 전환 완료 시 호출</summary>
        public event Action OnRevertCompleted;

        #endregion

        #region Properties - 수치 (Controller가 설정)

        public float DeformAmount
        {
            get => _deformAmount;
            set
            {
                _deformAmount = Mathf.Clamp01(value);
                Apply(_deformAmount);
            }
        }

        public float CurrentAmount => _deformAmount;

        public float PressDuration
        {
            get => _pressDuration;
            set => _pressDuration = Mathf.Max(0.001f, value);
        }

        public AnimationCurve PressCurve
        {
            get => _pressCurve;
            set => _pressCurve = value ?? AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        public float RevertDuration
        {
            get => _revertDuration;
            set => _revertDuration = Mathf.Max(0.001f, value);
        }

        public AnimationCurve RevertCurve
        {
            get => _revertCurve;
            set => _revertCurve = value ?? AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        public float BoneScaleMin
        {
            get => _boneScaleMin;
            set => _boneScaleMin = Mathf.Clamp(value, 0.01f, 1f);
        }

        public float BoneScaleMinLimit
        {
            get => _boneScaleMinLimit;
            set => _boneScaleMinLimit = Mathf.Clamp(value, 0.01f, 1f);
        }

        public float HeightDownAmount
        {
            get => _heightDownAmount;
            set => _heightDownAmount = value;
        }

        public float PositionAxisMinValue
        {
            get => _positionAxisMinValue;
            set => _positionAxisMinValue = value;
        }

        public bool UseContinuousHeightSpeed
        {
            get => _useContinuousHeightSpeed;
            set => _useContinuousHeightSpeed = value;
        }

        public float HeightDownSpeedPerSecond
        {
            get => _heightDownSpeedPerSecond;
            set => _heightDownSpeedPerSecond = value;
        }

        #endregion

        #region Public API - 전환 실행

        public void Press()
        {
            if (_showDebugLog)
                Debug.Log($"[CharacterDeform] Press 시작 | {_deformAmount:F2} → 1 | 시간: {_pressDuration}s");

            BeginTransition(TransitionMode.Pressing, _deformAmount, 1f);
            OnPressStarted?.Invoke();
        }

        public void Revert()
        {
            if (_showDebugLog)
                Debug.Log($"[CharacterDeform] Revert 시작 | {_deformAmount:F2} → 0 | 시간: {_revertDuration}s");

            BeginTransition(TransitionMode.Reverting, _deformAmount, 0f);
            OnRevertStarted?.Invoke();
        }

        public void SnapToPress()
        {
            _mode = TransitionMode.None;
            DeformAmount = 1f;

            if (_showDebugLog)
                Debug.Log("[CharacterDeform] SnapToPress (즉시 압축)");
        }

        public void SnapToOriginal()
        {
            _mode = TransitionMode.None;
            DeformAmount = 0f;

            if (_showDebugLog)
                Debug.Log("[CharacterDeform] SnapToOriginal (즉시 복원)");
        }

        #endregion

        #region Unity Lifecycle

        void Awake()
        {
            CaptureInitials();
        }

        public void CaptureInitials()
        {
            if (_coreBone != null)
            {
                _initialLocalScaleBone = _coreBone.localScale;
                _initialBoneScaleAxis = GetScaleAxis(_coreBone.localScale, _boneScaleAxis);
            }

            if (_heightTarget != null)
                _initialLocalPosAxis = GetPositionAxis(_heightTarget.localPosition, _positionAxis);

            _initialized = true;

            if (_showDebugLog)
                Debug.Log($"[CharacterDeform] CaptureInitials | Bone축:{_boneScaleAxis} scale:{_initialBoneScaleAxis:F3} | Pos축:{_positionAxis} pos:{_initialLocalPosAxis:F3}");
        }

        void Update()
        {
            TickTransition();

            if (_useContinuousHeightSpeed && _heightTarget != null)
            {
                float delta = _heightDownSpeedPerSecond * Time.deltaTime;
                float current = GetPositionAxis(_heightTarget.localPosition, _positionAxis);
                float next = Mathf.Max(current + delta, _positionAxisMinValue);
                _heightTarget.localPosition = WithPositionAxis(_heightTarget.localPosition, _positionAxis, next);
            }
        }

        #endregion

        #region Private - Transition

        void BeginTransition(TransitionMode mode, float from, float to)
        {
            _mode = mode;
            _transitionStartAmount = from;
            _transitionTargetAmount = to;
            _transitionElapsed = 0f;
        }

        void TickTransition()
        {
            if (_mode == TransitionMode.None) return;

            float duration = _mode == TransitionMode.Pressing ? _pressDuration : _revertDuration;
            AnimationCurve curve = _mode == TransitionMode.Pressing ? _pressCurve : _revertCurve;

            _transitionElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(_transitionElapsed / duration);
            float eased = curve.Evaluate(progress);

            DeformAmount = Mathf.Lerp(_transitionStartAmount, _transitionTargetAmount, eased);

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
                    Debug.Log("[CharacterDeform] 압축 완료 (DeformAmount=1)");
                OnPressCompleted?.Invoke();
            }
            else
            {
                if (_showDebugLog)
                    Debug.Log("[CharacterDeform] 팽창(복원) 완료 (DeformAmount=0)");
                OnRevertCompleted?.Invoke();
            }
        }

        #endregion

        #region Private - Apply

        void Apply(float amount)
        {
            if (!_initialized) return;

            if (_coreBone != null)
            {
                float scaleVal = Mathf.Lerp(_initialBoneScaleAxis, _boneScaleMin, amount);
                scaleVal = Mathf.Max(scaleVal, _boneScaleMinLimit);
                _coreBone.localScale = WithScaleAxis(_initialLocalScaleBone, _boneScaleAxis, scaleVal);
            }

            if (_heightTarget != null && !_useContinuousHeightSpeed)
            {
                float targetPos = _initialLocalPosAxis - _heightDownAmount * amount;
                targetPos = Mathf.Max(targetPos, _positionAxisMinValue);
                _heightTarget.localPosition = WithPositionAxis(_heightTarget.localPosition, _positionAxis, targetPos);
            }
        }

        #endregion

        #region Axis Helpers

        static float GetPositionAxis(Vector3 p, PressAxis axis)
        {
            if (axis == PressAxis.X) return p.x;
            if (axis == PressAxis.Z) return p.z;
            return p.y;
        }

        static float GetScaleAxis(Vector3 s, PressAxis axis)
        {
            if (axis == PressAxis.X) return s.x;
            if (axis == PressAxis.Z) return s.z;
            return s.y;
        }

        static Vector3 WithPositionAxis(Vector3 p, PressAxis axis, float value)
        {
            if (axis == PressAxis.X) return new Vector3(value, p.y, p.z);
            if (axis == PressAxis.Z) return new Vector3(p.x, p.y, value);
            return new Vector3(p.x, value, p.z);
        }

        static Vector3 WithScaleAxis(Vector3 s, PressAxis axis, float value)
        {
            if (axis == PressAxis.X) return new Vector3(value, s.y, s.z);
            if (axis == PressAxis.Z) return new Vector3(s.x, s.y, value);
            return new Vector3(s.x, value, s.z);
        }

        static Vector3 GetAxisDirectionWorld(Transform t, PressAxis axis)
        {
            if (axis == PressAxis.X) return t.TransformDirection(Vector3.right);
            if (axis == PressAxis.Z) return t.TransformDirection(Vector3.forward);
            return t.TransformDirection(Vector3.up);
        }

        #endregion

        #region Gizmos

        void OnDrawGizmos()
        {
            if (!_drawGizmos) return;

            float size = _gizmoSize;

            if (_heightTarget != null)
            {
                Vector3 origin = _heightTarget.position;
                Vector3 axisDir = GetAxisDirectionWorld(_heightTarget, _positionAxis);

                Gizmos.color = Color.green;
                Gizmos.DrawLine(origin, origin + axisDir * size);
                Gizmos.DrawWireSphere(origin + axisDir * size * 0.5f, size * 0.1f);

                Vector3 minPosLocal = WithPositionAxis(_heightTarget.localPosition, _positionAxis, _positionAxisMinValue);
                Vector3 minPosWorld = _heightTarget.parent != null
                    ? _heightTarget.parent.TransformPoint(minPosLocal)
                    : minPosLocal;
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(minPosWorld, size * 0.15f);
            }

            if (_coreBone != null)
            {
                Gizmos.color = Color.cyan;
                Vector3 axisDir = GetAxisDirectionWorld(_coreBone, _boneScaleAxis);
                Vector3 origin = _coreBone.position;
                Gizmos.DrawLine(origin, origin + axisDir * size);
                Gizmos.DrawWireSphere(origin + axisDir * size * 0.5f, size * 0.1f);
            }
        }

        #endregion
    }
}
