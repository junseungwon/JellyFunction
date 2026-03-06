using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace CharacterPressing
{
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

    /// <summary>
    /// 키 입력을 전담 관리하는 매니저.
    /// PressController 프리셋 키들은 코드에서 자동 바인딩되며,
    /// 추가 기능은 [추가 바인딩 목록]에 항목을 넣어 연결합니다.
    /// </summary>
    public class CharacterKeyManager : MonoBehaviour
    {
        [Header("PressController 참조")]
        [Tooltip("코드 자동 바인딩 대상. 같은 오브젝트에 있으면 자동 할당됩니다.")]
        [SerializeField] CharacterPressController _pressController = null;

        [Header("PressController 키 설정")]
        [Tooltip("Press() 호출 키 (KeyCode.None이면 바인딩하지 않음)")]
        [SerializeField] KeyCode _pressKey = KeyCode.Space;

        [Tooltip("Revert() 호출 키 (KeyCode.None이면 바인딩하지 않음)")]
        [SerializeField] KeyCode _revertKey = KeyCode.R;

        [Tooltip("SnapToPress() 호출 키 (KeyCode.None이면 바인딩하지 않음)")]
        [SerializeField] KeyCode _snapPressKey = KeyCode.None;

        [Tooltip("SnapToOriginal() 호출 키 (KeyCode.None이면 바인딩하지 않음)")]
        [SerializeField] KeyCode _snapOriginalKey = KeyCode.None;

        [Header("추가 바인딩 목록")]
        [Tooltip("PressController 외 추가 기능을 바인딩합니다.")]
        [SerializeField] List<KeyBinding> _extraBindings = new List<KeyBinding>();

        [Header("Debug")]
        [Tooltip("켜면 바인딩이 트리거될 때 콘솔에 로그 출력")]
        [SerializeField] bool _showDebugLog = false;

        // 코드에서 자동 생성된 바인딩 목록 (런타임 전용)
        readonly List<KeyBinding> _autoBindings = new List<KeyBinding>();

        void Reset()
        {
            _pressController = GetComponent<CharacterPressController>();
        }

        void Awake()
        {
            if (_pressController == null)
                _pressController = GetComponent<CharacterPressController>();

            RegisterAutoBindings();
        }

        /// <summary>PressController 키들을 코드로 자동 바인딩합니다.</summary>
        void RegisterAutoBindings()
        {
            _autoBindings.Clear();

            if (_pressController == null)
            {
                if (_showDebugLog)
                    Debug.LogWarning("[CharacterKeyManager] PressController가 없어 자동 바인딩을 건너뜁니다.");
                return;
            }

            TryAddAutoBinding("Press",         _pressKey,       KeyTrigger.Down, _pressController.Press);
            TryAddAutoBinding("Revert",        _revertKey,      KeyTrigger.Down, _pressController.Revert);
            TryAddAutoBinding("SnapToPress",   _snapPressKey,   KeyTrigger.Down, _pressController.SnapToPress);
            TryAddAutoBinding("SnapToOriginal",_snapOriginalKey,KeyTrigger.Down, _pressController.SnapToOriginal);

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

        // ─── 런타임 바인딩 관리 API ────────────────────────────────

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
    }
}

