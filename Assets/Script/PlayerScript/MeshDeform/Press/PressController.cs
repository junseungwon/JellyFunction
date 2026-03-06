using UnityEngine;
using UnityEngine.Events;

namespace PressSystem
{
    public class PressController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] PressDeformer _deformer = null;

        [Header("Transition Settings")]
        [SerializeField] float _transitionDuration = 0.5f;
        [SerializeField] AnimationCurve _easingCurve =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Events")]
        public UnityEvent onPressComplete;
        public UnityEvent onRevertComplete;

        [Header("Auto Start")]
        [SerializeField] bool _pressOnStart = false;
        [SerializeField] bool _logOnPressComplete = false;

        float _currentT = 0f;
        float _startT = 0f;
        float _targetT = 0f;
        float _elapsedTime = 0f;
        bool _isTransitioning = false;

        public float CurrentAmount => _currentT;

        public void Press() => SetTarget(1f);
        public void RevertToOriginal() => SetTarget(0f);
        public void SetPressRatio(float ratio) => SetTarget(Mathf.Clamp01(ratio));

        public void SnapToPress()
        {
            _currentT = _targetT = _startT = 1f;
            _elapsedTime = _transitionDuration;
            _isTransitioning = false;
            _deformer.PressAmount = 1f;
        }

        public void SnapToOriginal()
        {
            _currentT = _targetT = _startT = 0f;
            _elapsedTime = _transitionDuration;
            _isTransitioning = false;
            _deformer.ForceRevert();
        }

        void SetTarget(float t)
        {
            if (Mathf.Approximately(t, _targetT) && !_isTransitioning) return;

            _startT = _currentT;
            _targetT = t;
            _elapsedTime = 0f;
            _isTransitioning = true;
        }

        void Update()
        {
            if (!_isTransitioning) return;

            _elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(_elapsedTime / _transitionDuration);
            float easedProgress = _easingCurve.Evaluate(progress);

            _currentT = Mathf.Lerp(_startT, _targetT, easedProgress);
            _deformer.PressAmount = _currentT;

            if (progress >= 1f)
            {
                _currentT = _targetT;
                _isTransitioning = false;

                if (Mathf.Approximately(_targetT, 1f))
                    onPressComplete?.Invoke();
                else if (Mathf.Approximately(_targetT, 0f))
                    onRevertComplete?.Invoke();
            }
        }

        void Reset()
        {
            _deformer = GetComponent<PressDeformer>();
        }

        void Start()
        {
            if (_logOnPressComplete)
                onPressComplete.AddListener(() => Debug.Log("프레스 완료!"));

            if (_pressOnStart)
                Press();
        }
    }
}
