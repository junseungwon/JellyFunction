using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using SpherifySystem;
using PressSystem;

/// <summary>
/// Spherify → Press 순서를 자동으로 제어하는 상위 시퀀서.
/// AutoStart가 켜져 있으면 Start 시점에 전체 흐름이 자동 시작됩니다.
///
/// ※ 이 컴포넌트가 붙어 있으면 SpherifyController·PressController의
///   개별 AutoStart는 Awake에서 자동으로 비활성화됩니다.
/// </summary>
public class SpherePressSequencer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] SpherifyController _spherifyController = null;
    [SerializeField] SpherifyDeformer   _spherifyDeformer   = null;
    [SerializeField] PressController    _pressController    = null;
    [SerializeField] PressDeformer      _pressDeformer      = null;

    [Header("Auto Start")]
    [Tooltip("true면 Start 시 자동으로 시퀀스 시작 (Spherify → Press)")]
    [SerializeField] bool _autoStart = true;

    [Header("Sequence Settings")]
    [Tooltip("구형 전환 완료 후 Press 시작까지의 대기 시간 (초)")]
    [SerializeField] float _delayBeforePress = 0f;

    [Header("Events")]
    public UnityEvent onSequenceComplete;

    [Header("Debug")]
    [SerializeField] bool _showDebugLog = true;

    // ── 상태 ──────────────────────────────────────────────────
    public enum SequenceState { Idle, Spherifying, PressReady, Pressing, Done }
    SequenceState _state = SequenceState.Idle;
    public SequenceState State => _state;

    // ── 초기화 ────────────────────────────────────────────────
    void Awake()
    {
        if (_showDebugLog)
            Debug.Log("[Sequencer] 초기화 시작 — 개별 AutoStart 비활성화 중...");

        _spherifyController.DisableAutoStart();
        _pressController.DisableAutoStart();

        if (_showDebugLog)
            Debug.Log("[Sequencer] ✅ 초기화 완료");
    }

    void Start()
    {
        _spherifyController.onSphereComplete.AddListener(OnSphereComplete);
        _pressController.onPressComplete.AddListener(OnPressComplete);

        if (_showDebugLog)
            Debug.Log($"[Sequencer] 준비 완료 | AutoStart: {_autoStart} | Press 대기 시간: {_delayBeforePress}s");

        if (_autoStart)
            StartSequence();
    }

    // ── 공개 API ──────────────────────────────────────────────

    /// <summary>시퀀스 시작 (Idle 상태일 때만 동작)</summary>
    public void StartSequence()
    {
        if (_state != SequenceState.Idle)
        {
            if (_showDebugLog)
                Debug.Log($"[Sequencer] StartSequence 무시 (현재 상태: {_state})");
            return;
        }

        if (_showDebugLog)
            Debug.Log("[Sequencer] 시퀀스 시작 — 구형 전환 단계");

        _state = SequenceState.Spherifying;
        _spherifyController.TransformToSphere();
    }

    /// <summary>모든 상태를 원본으로 초기화 (재시작 목적)</summary>
    public void ResetSequence()
    {
        if (_showDebugLog)
            Debug.Log("[Sequencer] ResetSequence — 원본으로 초기화");

        StopAllCoroutines();
        _spherifyDeformer.enabled = true;
        _spherifyController.RevertToOriginal();
        _pressController.RevertToOriginal();
        _state = SequenceState.Idle;

        if (_showDebugLog)
            Debug.Log("[Sequencer] ✅ ResetSequence 완료");
    }

    // ── 이벤트 수신 ───────────────────────────────────────────
    void OnSphereComplete()
    {
        if (_state != SequenceState.Spherifying) return;

        if (_showDebugLog)
            Debug.Log("[Sequencer] ✅ 구형 전환 완료 — Press 준비 단계로 전환");

        _state = SequenceState.PressReady;

        _spherifyDeformer.enabled = false;

        Vector3[] sphereVerts  = _spherifyDeformer.GetCurrentVerticesCopy();
        Bounds    sphereBounds = _spherifyDeformer.GetCurrentBounds();
        _pressDeformer.RebuildSnapshot(sphereVerts, sphereBounds);

        _state = SequenceState.Pressing;

        if (_delayBeforePress > 0f)
        {
            if (_showDebugLog)
                Debug.Log($"[Sequencer] Press 대기 중... {_delayBeforePress}s");
            StartCoroutine(DelayedPress());
        }
        else
        {
            if (_showDebugLog)
                Debug.Log("[Sequencer] 프레스 단계 시작");
            _pressController.Press();
        }
    }

    void OnPressComplete()
    {
        if (_state != SequenceState.Pressing) return;

        if (_showDebugLog)
            Debug.Log("[Sequencer] ✅ 프레스 완료 — 전체 시퀀스 완료");

        _state = SequenceState.Done;
        onSequenceComplete?.Invoke();
    }

    IEnumerator DelayedPress()
    {
        yield return new WaitForSeconds(_delayBeforePress);

        if (_showDebugLog)
            Debug.Log("[Sequencer] 대기 종료 — 프레스 단계 시작");
        _pressController.Press();
    }

    // ── Inspector 자동 연결 ───────────────────────────────────
    void Reset()
    {
        _spherifyController = GetComponent<SpherifyController>();
        _spherifyDeformer   = GetComponent<SpherifyDeformer>();
        _pressController    = GetComponent<PressController>();
        _pressDeformer      = GetComponent<PressDeformer>();
    }

    void OnDestroy()
    {
        if (_spherifyController != null)
            _spherifyController.onSphereComplete.RemoveListener(OnSphereComplete);
        if (_pressController != null)
            _pressController.onPressComplete.RemoveListener(OnPressComplete);
    }
}
