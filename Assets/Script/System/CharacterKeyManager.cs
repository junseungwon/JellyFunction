using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using SpherifySystem;

namespace CharacterPressing
{
    #region 타입 정의

    /// <summary>키 입력 감지 타이밍</summary>
    public enum KeyTrigger
    {
        /// <summary>키를 누른 첫 프레임</summary>
        Down,
        /// <summary>키를 누르고 있는 동안 매 프레임</summary>
        Hold,
        /// <summary>키를 뗀 첫 프레임</summary>
        Up
    }

    /// <summary>키 하나와 연결된 이벤트 바인딩</summary>
    [System.Serializable]
    public class KeyBinding
    {
        [Tooltip("바인딩 설명 레이블 (Inspector 구분용)")]
        public string label = "새 바인딩";

        [Tooltip("입력 키")]
        public KeyCode key = KeyCode.None;

        [Tooltip("입력 감지 타이밍 (Down / Hold / Up)")]
        public KeyTrigger trigger = KeyTrigger.Down;

        [Tooltip("조건 충족 시 실행할 이벤트")]
        public UnityEvent onTrigger = new UnityEvent();

        [Tooltip("이 바인딩을 활성화할지 여부")]
        public bool enabled = true;
    }

    #endregion

    /// <summary>
    /// 키 입력을 전담 관리하는 매니저.
    /// 각 Feature 컴포넌트의 실행 메서드를 직접 바인딩합니다.
    /// Controller가 아닌 Feature 컴포넌트를 참조합니다.
    /// 참조가 null인 시스템은 바인딩을 건너뜁니다.
    /// 추가 기능은 [추가 바인딩 목록]에 항목을 넣어 연결합니다.
    /// </summary>
    public class CharacterKeyManager : MonoBehaviour
    {
        #region Inspector - ChangeModel

        [Header("ChangeModel (캐릭터↔볼 전환)")]
        [Tooltip("ChangeModel 컴포넌트. 할당 시 Press/Revert 키가 ChangeModel을 통해 라우팅됩니다.")]
        [SerializeField] ChangeModel _changeModel = null;

        [Header("ChangeModel 키 설정")]
        [Tooltip("캐릭터↔볼 전환 토글 키 (KeyCode.None이면 바인딩하지 않음)")]
        [SerializeField] KeyCode _changeModelToggleKey = KeyCode.T;

        #endregion

        #region Inspector - CharacterDeform (캐릭터 Press)

        [Header("캐릭터 Press (CharacterDeform)")]
        [Tooltip("캐릭터 압축/팽창 변형. 같은 오브젝트에 있으면 Reset 시 자동 할당됩니다.")]
        [SerializeField] CharacterDeform _characterDeform = null;

        [Header("캐릭터 Press 키 설정")]
        [Tooltip("캐릭터 Press() 호출 키 (KeyCode.None이면 바인딩하지 않음)")]
        [SerializeField] KeyCode _pressKey = KeyCode.Space;

        [Tooltip("캐릭터 Revert() 호출 키 (KeyCode.None이면 바인딩하지 않음)")]
        [SerializeField] KeyCode _revertKey = KeyCode.R;

        [Tooltip("캐릭터 SnapToPress() 호출 키 (KeyCode.None이면 바인딩하지 않음)")]
        [SerializeField] KeyCode _snapPressKey = KeyCode.None;

        [Tooltip("캐릭터 SnapToOriginal() 호출 키 (KeyCode.None이면 바인딩하지 않음)")]
        [SerializeField] KeyCode _snapOriginalKey = KeyCode.None;

        #endregion

        #region Inspector - Ball Deform (볼 Press)

        [Header("볼 Press (CharacterDeform)")]
        [Tooltip("볼(Ball) 압축/팽창 변형. 다른 오브젝트에 붙어 있으면 여기서 드래그로 할당합니다.")]
        [SerializeField] CharacterDeform _ballDeform = null;

        [Header("볼 Press 키 설정")]
        [Tooltip("볼 Press() 호출 키 (KeyCode.None이면 바인딩하지 않음)")]
        [SerializeField] KeyCode _ballPressKey = KeyCode.None;

        [Tooltip("볼 Revert() 호출 키 (KeyCode.None이면 바인딩하지 않음)")]
        [SerializeField] KeyCode _ballRevertKey = KeyCode.None;

        [Tooltip("볼 SnapToPress() 호출 키 (KeyCode.None이면 바인딩하지 않음)")]
        [SerializeField] KeyCode _ballSnapPressKey = KeyCode.None;

        [Tooltip("볼 SnapToOriginal() 호출 키 (KeyCode.None이면 바인딩하지 않음)")]
        [SerializeField] KeyCode _ballSnapOriginalKey = KeyCode.None;

        #endregion

        #region Inspector - FootSystem

        [Header("FootSystem 참조")]
        [Tooltip("발자국 감지 기능. 같은 오브젝트에 있으면 Reset 시 자동 할당됩니다.")]
        [SerializeField] FootprintDetectorEvent _footprintDetector = null;

        [Header("FootSystem 키 설정")]
        [Tooltip("발자국 감지 토글 키 (KeyCode.None이면 바인딩하지 않음)")]
        [SerializeField] KeyCode _footToggleKey = KeyCode.F;

        #endregion

        #region Inspector - SpherifyDeformer

        [Header("SpherifyDeformer 참조")]
        [Tooltip("메시 구형 변형 기능. 같은 오브젝트에 있으면 Reset 시 자동 할당됩니다.")]
        [SerializeField] SpherifyDeformer _spherifyDeformer = null;

        [Header("SpherifyDeformer 키 설정")]
        [Tooltip("구체로 변형 키 (KeyCode.None이면 바인딩하지 않음)")]
        [SerializeField] KeyCode _spherifyKey = KeyCode.G;

        [Tooltip("원래 형태로 복원 키 (KeyCode.None이면 바인딩하지 않음)")]
        [SerializeField] KeyCode _spherifyRevertKey = KeyCode.H;

        #endregion

        #region Inspector - SoftBody

        [Header("SoftBody 참조")]
        [Tooltip("SphereDeform 소프트 바디 변형. 같은 오브젝트에 있으면 Reset 시 자동 할당됩니다.")]
        [SerializeField] SphereDeform _sphereDeform = null;

        [Header("SoftBody 키 설정")]
        [Tooltip("SphereDeform 토글 키 (KeyCode.None이면 바인딩하지 않음)")]
        [SerializeField] KeyCode _softBodyToggleKey = KeyCode.B;

        [Tooltip("SphereDeformMesh 토글 키 (KeyCode.None이면 바인딩하지 않음)")]
        [SerializeField] KeyCode _softBodyMeshToggleKey = KeyCode.None;

        #endregion

        #region Inspector - Bounce

        [Header("CharacterBounce 참조")]
        [Tooltip("바운스 WPO Intensity 컨트롤러. 같은 오브젝트에 있으면 Reset 시 자동 할당됩니다.")]
        [SerializeField] CharacterBounceController _bounceController = null;

        [Header("Bounce 키 설정")]
        [Tooltip("Default 상태 전환 키 (KeyCode.None이면 바인딩하지 않음)")]
        [SerializeField] KeyCode _bounceDefaultKey = KeyCode.Alpha1;

        [Tooltip("Excited 상태 전환 키 (KeyCode.None이면 바인딩하지 않음)")]
        [SerializeField] KeyCode _bounceExcitedKey = KeyCode.Alpha2;

        [Tooltip("Intensity 증가 키 - Hold (KeyCode.None이면 바인딩하지 않음)")]
        [SerializeField] KeyCode _bounceIncreaseKey = KeyCode.Equals;

        [Tooltip("Intensity 감소 키 - Hold (KeyCode.None이면 바인딩하지 않음)")]
        [SerializeField] KeyCode _bounceDecreaseKey = KeyCode.Minus;

        #endregion

        #region Inspector - 공통

        [Header("추가 바인딩 목록")]
        [Tooltip("위 시스템 외 추가 기능을 바인딩합니다.")]
        [SerializeField] List<KeyBinding> _extraBindings = new List<KeyBinding>();

        [Header("Debug")]
        [Tooltip("켜면 바인딩이 트리거될 때 콘솔에 로그 출력")]
        [SerializeField] bool _showDebugLog = false;

        readonly List<KeyBinding> _autoBindings = new List<KeyBinding>();

        #endregion

        #region Unity Lifecycle

        void Reset()
        {
            _changeModel        = GetComponent<ChangeModel>();
            _characterDeform    = GetComponent<CharacterDeform>();
            _footprintDetector  = GetComponent<FootprintDetectorEvent>();
            _spherifyDeformer   = GetComponent<SpherifyDeformer>();
            _sphereDeform       = GetComponent<SphereDeform>();
            _bounceController   = GetComponent<CharacterBounceController>();
        }

        void Awake()
        {
            if (_characterDeform == null)
                _characterDeform = GetComponent<CharacterDeform>();

            RegisterAutoBindings();
        }

        #endregion

        #region 자동 바인딩

        /// <summary>각 Feature 컴포넌트의 실행 메서드를 키에 자동 바인딩합니다. 참조가 null인 시스템은 건너뜁니다.</summary>
        void RegisterAutoBindings()
        {
            _autoBindings.Clear();

            // ChangeModel 모드: Toggle 키 + Press/Revert를 ChangeModel로 라우팅
            if (_changeModel != null)
            {
                TryAddAutoBinding("ChangeModelToggle",   _changeModelToggleKey, KeyTrigger.Down, _changeModel.Toggle);
                TryAddAutoBinding("Press",               _pressKey,             KeyTrigger.Down, _changeModel.PressActive);
                TryAddAutoBinding("Revert",              _revertKey,            KeyTrigger.Down, _changeModel.RevertActive);
                TryAddAutoBinding("SnapToPress",         _snapPressKey,         KeyTrigger.Down, _changeModel.SnapToPressActive);
                TryAddAutoBinding("SnapToOriginal",      _snapOriginalKey,      KeyTrigger.Down, _changeModel.SnapToOriginalActive);

                if (_showDebugLog)
                    Debug.Log("[CharacterKeyManager] ChangeModel 모드 활성 — Press/Revert 키가 ChangeModel로 라우팅됩니다.");
            }
            else
            {
                // ChangeModel 없을 때: 캐릭터 Press 직접 바인딩
                if (_characterDeform != null)
                {
                    TryAddAutoBinding("CharPress",          _pressKey,        KeyTrigger.Down, _characterDeform.Press);
                    TryAddAutoBinding("CharRevert",         _revertKey,       KeyTrigger.Down, _characterDeform.Revert);
                    TryAddAutoBinding("CharSnapToPress",    _snapPressKey,    KeyTrigger.Down, _characterDeform.SnapToPress);
                    TryAddAutoBinding("CharSnapToOriginal", _snapOriginalKey, KeyTrigger.Down, _characterDeform.SnapToOriginal);
                }
                else if (_showDebugLog)
                    Debug.LogWarning("[CharacterKeyManager] CharacterDeform(캐릭터) 참조 없음 — 바인딩 건너뜀");

                // 볼 Press 직접 바인딩
                if (_ballDeform != null)
                {
                    TryAddAutoBinding("BallPress",          _ballPressKey,        KeyTrigger.Down, _ballDeform.Press);
                    TryAddAutoBinding("BallRevert",         _ballRevertKey,       KeyTrigger.Down, _ballDeform.Revert);
                    TryAddAutoBinding("BallSnapToPress",    _ballSnapPressKey,    KeyTrigger.Down, _ballDeform.SnapToPress);
                    TryAddAutoBinding("BallSnapToOriginal", _ballSnapOriginalKey, KeyTrigger.Down, _ballDeform.SnapToOriginal);
                }
                else if (_showDebugLog)
                    Debug.LogWarning("[CharacterKeyManager] CharacterDeform(볼) 참조 없음 — 바인딩 건너뜀");
            }

            // FootSystem
            if (_footprintDetector != null)
            {
                TryAddAutoBinding("FootToggle", _footToggleKey, KeyTrigger.Down, _footprintDetector.ToggleDetection);
            }
            else if (_showDebugLog)
                Debug.LogWarning("[CharacterKeyManager] FootprintDetector 참조 없음 — 바인딩 건너뜀");

            // SpherifyDeformer
            if (_spherifyDeformer != null)
            {
                TryAddAutoBinding("SpherifyStart",  _spherifyKey,       KeyTrigger.Down, _spherifyDeformer.TransformToSphere);
                TryAddAutoBinding("SpherifyRevert", _spherifyRevertKey, KeyTrigger.Down, _spherifyDeformer.RevertToOriginal);
            }
            else if (_showDebugLog)
                Debug.LogWarning("[CharacterKeyManager] SpherifyDeformer 참조 없음 — 바인딩 건너뜀");

            // SoftBody (SphereDeform)
            if (_sphereDeform != null)
                TryAddAutoBinding("SoftBodyToggle", _softBodyToggleKey, KeyTrigger.Down, _sphereDeform.ToggleDeform);
            else if (_showDebugLog)
                Debug.LogWarning("[CharacterKeyManager] SphereDeform 참조 없음 — 바인딩 건너뜀");

            else if (_showDebugLog)
                Debug.LogWarning("[CharacterKeyManager] SphereDeformMesh 참조 없음 — 바인딩 건너뜀");

            // CharacterBounce (Controller를 통해 상태/수치 제어)
            if (_bounceController != null)
            {
                TryAddAutoBinding("BounceDefault",  _bounceDefaultKey,  KeyTrigger.Down, _bounceController.SetDefaultState);
                TryAddAutoBinding("BounceExcited",  _bounceExcitedKey,  KeyTrigger.Down, _bounceController.SetExcitedState);
                TryAddAutoBinding("BounceIncrease", _bounceIncreaseKey, KeyTrigger.Hold, _bounceController.IncreaseIntensity);
                TryAddAutoBinding("BounceDecrease", _bounceDecreaseKey, KeyTrigger.Hold, _bounceController.DecreaseIntensity);
            }
            else if (_showDebugLog)
                Debug.LogWarning("[CharacterKeyManager] CharacterBounceController 참조 없음 — 바인딩 건너뜀");

            if (_showDebugLog)
                Debug.Log($"[CharacterKeyManager] 자동 바인딩 완료 | {_autoBindings.Count}개 등록");
        }

        void TryAddAutoBinding(string label, KeyCode key, KeyTrigger trigger, UnityAction action)
        {
            if (key == KeyCode.None) return;

            var binding = new KeyBinding { label = label, key = key, trigger = trigger };
            binding.onTrigger.AddListener(action);
            _autoBindings.Add(binding);

            if (_showDebugLog)
                Debug.Log($"[CharacterKeyManager] 자동 바인딩 등록 | \"{label}\" → Key:{key}");
        }

        #endregion

        #region 입력 처리

        void Update()
        {
            ProcessBindings(_autoBindings);
            ProcessBindings(_extraBindings);
        }

        void ProcessBindings(List<KeyBinding> list)
        {
            foreach (KeyBinding binding in list)
            {
                if (!binding.enabled || binding.key == KeyCode.None) continue;

                bool triggered = binding.trigger switch
                {
                    KeyTrigger.Down => Input.GetKeyDown(binding.key),
                    KeyTrigger.Hold => Input.GetKey(binding.key),
                    KeyTrigger.Up   => Input.GetKeyUp(binding.key),
                    _               => false
                };

                if (!triggered) continue;

                if (_showDebugLog)
                    Debug.Log($"[CharacterKeyManager] 트리거 | \"{binding.label}\" | Key:{binding.key} | Trigger:{binding.trigger}");

                binding.onTrigger?.Invoke();
            }
        }

        #endregion

        #region 런타임 API

        /// <summary>추가 KeyBinding을 런타임에 등록합니다.</summary>
        public void AddBinding(KeyBinding binding)
        {
            if (binding == null) return;
            _extraBindings.Add(binding);

            if (_showDebugLog)
                Debug.Log($"[CharacterKeyManager] 바인딩 추가 | \"{binding.label}\" | Key:{binding.key}");
        }

        /// <summary>레이블로 추가 바인딩을 찾아 제거합니다.</summary>
        public void RemoveBinding(string label)
        {
            int idx = _extraBindings.FindIndex(b => b.label == label);
            if (idx < 0) return;

            if (_showDebugLog)
                Debug.Log($"[CharacterKeyManager] 바인딩 제거 | \"{label}\"");

            _extraBindings.RemoveAt(idx);
        }

        /// <summary>레이블로 바인딩(자동+추가 모두)을 찾아 활성/비활성 전환합니다.</summary>
        public void SetBindingEnabled(string label, bool enabled)
        {
            SetInList(_autoBindings, label, enabled);
            SetInList(_extraBindings, label, enabled);
        }

        void SetInList(List<KeyBinding> list, string label, bool enabled)
        {
            KeyBinding b = list.Find(x => x.label == label);
            if (b == null) return;
            b.enabled = enabled;

            if (_showDebugLog)
                Debug.Log($"[CharacterKeyManager] 바인딩 {(enabled ? "활성화" : "비활성화")} | \"{label}\"");
        }

        /// <summary>자동+추가 바인딩 전체를 활성/비활성 전환합니다.</summary>
        public void SetAllBindingsEnabled(bool enabled)
        {
            foreach (KeyBinding b in _autoBindings) b.enabled = enabled;
            foreach (KeyBinding b in _extraBindings) b.enabled = enabled;
        }

        #endregion
    }
}
