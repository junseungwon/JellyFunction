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
    /// Bone 스케일 변경 + 대상 오브젝트의 로컬 위치 감소를 담당하는 순수 변형 컴포넌트.
    /// 전환 제어·입력은 CharacterPressController에서 담당합니다.
    /// 모든 변환은 Local 기준입니다.
    /// </summary>
    public class CharacterDeform : MonoBehaviour
    {
        #region Inspector

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

        [Header("Bone 스케일 (Local)")]
        [Tooltip("압축 시 해당 축 localScale 목표 최소값 (이 값까지 줄어듦)")]
        [Range(0.01f, 1f)]
        [SerializeField] float _boneScaleMin = 0.3f;

        [Tooltip("스케일이 이 값 아래로 내려가지 않도록 하는 최저 한계")]
        [Range(0.01f, 1f)]
        [SerializeField] float _boneScaleMinLimit = 0.01f;

        [Header("위치 감소 (Local)")]
        [Tooltip("압축 시 로컬 축으로 줄어들 총 거리 (양수 = 음의 방향으로 감소)")]
        [SerializeField] float _heightDownAmount = 0.5f;

        [Tooltip("위치 축이 이 값 아래로 내려가지 않도록 하는 최저값 (로컬 좌표)")]
        [SerializeField] float _positionAxisMinValue = -1000f;

        [Header("연속 이동 (선택)")]
        [Tooltip("켜면 매 프레임 _heightDownSpeedPerSecond만큼 위치 축으로 로컬 이동 (DeformAmount와 무관)")]
        [SerializeField] bool _useContinuousHeightSpeed = false;

        [Tooltip("매 초당 로컬 위치 축 이동량 (감소 방향은 음수, 예: -0.01)")]
        [SerializeField] float _heightDownSpeedPerSecond = -0.01f;

        [Header("Debug")]
        [Tooltip("켜면 초기화 시 콘솔에 로그 출력")]
        [SerializeField] bool _showDebugLog = false;

        [Tooltip("켜면 Scene 뷰에 위치 축·최저값·Bone 축을 기즈모로 표시")]
        [SerializeField] bool _drawGizmos = false;

        [Tooltip("기즈모 선/구 크기")]
        [SerializeField] float _gizmoSize = 0.5f;

        #endregion

        #region Private Fields

        float _initialBoneScaleAxis;
        float _initialLocalPosAxis;
        Vector3 _initialLocalScaleBone;
        bool _initialized;

        float _deformAmount;

        /// <summary>
        /// 변형량 0~1. 설정 즉시 Bone 스케일과 위치에 반영됩니다.
        /// </summary>
        public float DeformAmount
        {
            get => _deformAmount;
            set
            {
                _deformAmount = Mathf.Clamp01(value);
                Apply(_deformAmount);
            }
        }

        #endregion

        #region Unity Lifecycle

        void Awake()
        {
            CaptureInitials();
        }

        void OnValidate()
        {
            if (Application.isPlaying && _initialized)
                Apply(_deformAmount);
        }

        /// <summary>
        /// 현재 Bone·위치 대상의 로컬 값을 기준값으로 캡처합니다.
        /// 런타임에서 대상 오브젝트를 바꾼 뒤 다시 호출하면 기준값이 갱신됩니다.
        /// </summary>
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
            if (!_useContinuousHeightSpeed || _heightTarget == null) return;

            float delta = _heightDownSpeedPerSecond * Time.deltaTime;
            float current = GetPositionAxis(_heightTarget.localPosition, _positionAxis);
            float next = Mathf.Max(current + delta, _positionAxisMinValue);
            _heightTarget.localPosition = WithPositionAxis(_heightTarget.localPosition, _positionAxis, next);
        }

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

        // ─── 축 유틸 ──────────────────────────────────────────────

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

        // ─── 기즈모 ───────────────────────────────────────────────

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
