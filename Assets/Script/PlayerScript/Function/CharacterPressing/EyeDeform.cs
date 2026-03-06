using UnityEngine;

namespace CharacterPressing
{
    /// <summary>
    /// Press/Revert 시 눈 오브젝트를 이동시키는 컴포넌트.
    /// CharacterPressController의 onPressStart / onRevertStart 이벤트에
    /// Awake()에서 코드로 자동 구독합니다.
    /// </summary>
    public class EyeDeform : MonoBehaviour
    {
        [Header("눈 대상 (Inspector에서 할당)")]
        [Tooltip("이동시킬 눈 Transform 목록. 여러 개 지정 가능")]
        [SerializeField] Transform[] _eyeTargets = null;

        [Header("이동 설정 (Local)")]
        [Tooltip("눈이 이동할 로컬 축 (X/Y/Z)")]
        [SerializeField] PressAxis _moveAxis = PressAxis.Y;

        [Tooltip("Press 시 눈이 이동할 거리. 양수=축 양방향, 음수=반대")]
        [SerializeField] float _pressOffset = 0.1f;

        [Header("Press 애니메이션")]
        [Tooltip("눈이 이동하는 데 걸리는 시간(초)")]
        [SerializeField] float _pressDuration = 0.3f;

        [Tooltip("Press 이동 보간 커브. X=진행(0~1), Y=이동량 보간(0~1)")]
        [SerializeField] AnimationCurve _pressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Revert 애니메이션")]
        [Tooltip("눈이 원래 위치로 돌아오는 데 걸리는 시간(초)")]
        [SerializeField] float _revertDuration = 0.3f;

        [Tooltip("Revert 이동 보간 커브. X=진행(0~1), Y=이동량 보간(0~1)")]
        [SerializeField] AnimationCurve _revertCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("자동 연결")]
        [Tooltip("코드에서 이벤트를 자동 구독할 PressController. 비어 있으면 같은 오브젝트에서 탐색")]
        [SerializeField] CharacterPressController _pressController = null;

        [Header("Debug")]
        [Tooltip("켜면 OnPress / OnRevert 호출 시 콘솔에 로그 출력")]
        [SerializeField] bool _showDebugLog = false;

        Vector3[] _initialPositions;

        enum EyeMode { None, Pressing, Reverting }

        EyeMode _mode = EyeMode.None;
        float _elapsed;
        float _startT;
        float _targetT;
        float _currentT;   // 0 = 원래 위치, 1 = 완전 이동 위치

        void Reset()
        {
            _pressController = GetComponent<CharacterPressController>();
        }

        void Awake()
        {
            CaptureInitials();

            if (_pressController == null)
                _pressController = GetComponent<CharacterPressController>();

            if (_pressController != null)
            {
                _pressController.onPressStart.AddListener(OnPress);
                _pressController.onRevertStart.AddListener(OnRevert);

                if (_showDebugLog)
                    Debug.Log("[EyeDeform] PressController 이벤트 구독 완료");
            }
            else
            {
                Debug.LogWarning("[EyeDeform] CharacterPressController를 찾을 수 없습니다. 이벤트 구독 실패.");
            }
        }

        void OnDestroy()
        {
            if (_pressController != null)
            {
                _pressController.onPressStart.RemoveListener(OnPress);
                _pressController.onRevertStart.RemoveListener(OnRevert);
            }
        }

        /// <summary>눈 초기 위치를 현재 localPosition으로 캡처합니다.</summary>
        public void CaptureInitials()
        {
            if (_eyeTargets == null) return;

            _initialPositions = new Vector3[_eyeTargets.Length];
            for (int i = 0; i < _eyeTargets.Length; i++)
            {
                if (_eyeTargets[i] != null)
                    _initialPositions[i] = _eyeTargets[i].localPosition;
            }

            if (_showDebugLog)
                Debug.Log($"[EyeDeform] CaptureInitials | 눈 {_eyeTargets.Length}개 기준값 캡처");
        }

        /// <summary>눈을 Press 위치로 이동시킵니다. PressController의 onPressStart에서 자동 호출됩니다.</summary>
        public void OnPress()
        {
            if (_showDebugLog)
                Debug.Log($"[EyeDeform] OnPress | 이동량:{_pressOffset} 축:{_moveAxis} 시간:{_pressDuration}s");

            StartTransition(EyeMode.Pressing, _currentT, 1f);
        }

        /// <summary>눈을 원래 위치로 복원합니다. PressController의 onRevertStart에서 자동 호출됩니다.</summary>
        public void OnRevert()
        {
            if (_showDebugLog)
                Debug.Log($"[EyeDeform] OnRevert | 시간:{_revertDuration}s");

            StartTransition(EyeMode.Reverting, _currentT, 0f);
        }

        void StartTransition(EyeMode mode, float from, float to)
        {
            _mode = mode;
            _startT = from;
            _targetT = to;
            _elapsed = 0f;
        }

        void Update()
        {
            if (_mode == EyeMode.None) return;

            float duration = _mode == EyeMode.Pressing ? _pressDuration : _revertDuration;
            AnimationCurve curve = _mode == EyeMode.Pressing ? _pressCurve : _revertCurve;

            _elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(_elapsed / duration);
            float eased = curve.Evaluate(progress);
            _currentT = Mathf.Lerp(_startT, _targetT, eased);

            ApplyEyePositions(_currentT);

            if (progress >= 1f)
            {
                _mode = EyeMode.None;
                if (_showDebugLog)
                    Debug.Log($"[EyeDeform] 이동 완료 | t={_currentT:F2}");
            }
        }

        void ApplyEyePositions(float t)
        {
            if (_eyeTargets == null || _initialPositions == null) return;

            Vector3 axisVec = GetAxisVector(_moveAxis);

            for (int i = 0; i < _eyeTargets.Length; i++)
            {
                if (_eyeTargets[i] == null) continue;
                _eyeTargets[i].localPosition = _initialPositions[i] + axisVec * (_pressOffset * t);
            }
        }

        static Vector3 GetAxisVector(PressAxis axis)
        {
            if (axis == PressAxis.X) return Vector3.right;
            if (axis == PressAxis.Z) return Vector3.forward;
            return Vector3.up;
        }
    }
}
