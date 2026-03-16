using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// HallWay 구역 진입을 감지하고 외부 시퀀스 스크립트에 이벤트를 전달하는 트리거 컴포넌트.
/// Box Collider(Is Trigger 체크)가 붙은 오브젝트에 추가하고,
/// Sequence 필드에 Jelly_HallWaySequence를 할당하세요.
/// 플레이어 오브젝트가 Tag "Player"로 설정되어 있어야 감지됩니다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class HallWayZoneTrigger : MonoBehaviour
{
    [Tooltip("이벤트를 전달할 HallWay 시퀀스 스크립트.")]
    [SerializeField] private Jelly_HallWaySequence _sequence = null;

    [Tooltip("트리거에 진입할 오브젝트의 Tag. 이 Tag와 일치하는 오브젝트만 감지합니다.")]
    [SerializeField] private string _targetTag = "Player";

    [Tooltip("한 번 트리거되면 다시 작동하지 않도록 비활성화합니다.")]
    [SerializeField] private bool _triggerOnce = true;

    [Header("추가 이벤트 (선택)")]
    [Tooltip("Zone 진입 시 Sequence 외에 추가로 호출할 이벤트.")]
    [SerializeField] private UnityEvent _onZoneEntered;

    [Header("디버그")]
    [SerializeField] private bool _showDebugLog = false;

    private bool _triggered = false;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (!col.isTrigger)
        {
            col.isTrigger = true;
            if (_showDebugLog)
                Debug.Log($"[HallWayZoneTrigger] {name} — Collider를 Trigger로 자동 설정했습니다.", this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("충돌");
        if (_triggered && _triggerOnce) return;
        if (!other.CompareTag(_targetTag)) return;
        Debug.Log("충돌이됨 메세지 보낼거임.");
        _triggered = true;

        if (_showDebugLog)
            Debug.Log($"[HallWayZoneTrigger] {name} — '{other.name}'(Tag:{_targetTag}) 진입 감지. Sequence.OnZoneReached() 호출.", this);

        _sequence?.OnZoneReached();
        _onZoneEntered?.Invoke();

        if (_triggerOnce)
            GetComponent<Collider>().enabled = false;
    }

    /// <summary>외부에서 강제로 Zone 진입 이벤트를 발생시킵니다. 테스트 용도.</summary>
    [ContextMenu("Force Trigger (테스트)")]
    public void ForceTrigger()
    {
        if (_showDebugLog)
            Debug.Log($"[HallWayZoneTrigger] {name} — ForceTrigger() 호출.", this);

        _sequence?.OnZoneReached();
        _onZoneEntered?.Invoke();
    }
}
