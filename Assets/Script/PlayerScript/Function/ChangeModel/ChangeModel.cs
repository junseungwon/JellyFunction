using UnityEngine;
using SpherifySystem;

namespace CharacterPressing
{
    /// <summary>
    /// 캐릭터 모드와 볼 모드를 전환하는 혼합 기능 컴포넌트.
    /// Toggle() 호출 시 아래 두 시퀀스 중 하나를 실행합니다.
    ///
    /// [Forward: Character → Ball]
    ///   SpherifyDeformer.TransformToSphere() + CharacterDeform.Press() 동시 실행
    ///   → 구형 전환 완료 시: 볼 오브젝트 활성화(SnapToPress → Revert), 캐릭터 오브젝트 비활성화
    ///
    /// [Reverse: Ball → Character]
    ///   BallDeform.Press() 실행
    ///   → Press 완료 시: 캐릭터 오브젝트 활성화, 볼 비활성화
    ///                    SpherifyDeformer.RevertToOriginal() + CharacterDeform.Revert()
    /// </summary>
    public class ChangeModel : MonoBehaviour
    {
        #region Types

        public enum ModelState
        {
            Character,
            Ball
        }

        #endregion

        #region Inspector

        [Header("캐릭터 참조")]
        [Tooltip("캐릭터 오브젝트의 SpherifyDeformer (구형 전환 기능)")]
        [SerializeField] private SpherifyDeformer _spherifyDeformer = null;

        [Tooltip("캐릭터 오브젝트의 CharacterDeform (Press 기능)")]
        [SerializeField] private CharacterDeform _characterDeform = null;

        [Tooltip("활성/비활성 제어할 캐릭터 루트 오브젝트")]
        [SerializeField] private GameObject _characterObject = null;

        [Header("볼 참조")]
        [Tooltip("볼 오브젝트의 CharacterDeform (Press 기능)")]
        [SerializeField] private CharacterDeform _ballDeform = null;

        [Tooltip("활성/비활성 제어할 볼 루트 오브젝트")]
        [SerializeField] private GameObject _ballObject = null;

        [Header("Debug")]
        [Tooltip("켜면 전환 시작/완료 시 콘솔에 로그 출력")]
        [SerializeField] private bool _showDebugLog = false;

        #endregion

        #region Private Fields

        private ModelState _currentState = ModelState.Character;
        private bool _isTransitioning = false;

        #endregion

        #region Properties

        public ModelState CurrentState => _currentState;
        public bool IsTransitioning => _isTransitioning;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_ballObject != null)
                _ballObject.SetActive(false);
        }

        #endregion

        #region Public API - 전환

        /// <summary>
        /// 현재 상태에 따라 Forward(캐릭터→볼) 또는 Reverse(볼→캐릭터) 시퀀스를 실행합니다.
        /// 전환 중에는 재호출을 무시합니다.
        /// </summary>
        public void Toggle()
        {
            if (_isTransitioning) return;

            if (_currentState == ModelState.Character)
                StartCharacterToBall();
            else
                StartBallToCharacter();
        }

        #endregion

        #region Public API - Press 라우팅

        /// <summary>현재 활성 모드의 Press를 실행합니다. CharacterKeyManager에서 바인딩합니다.</summary>
        public void PressActive()
        {
            if (_isTransitioning) return;

            if (_currentState == ModelState.Character)
                _characterDeform?.Press();
            else
                _ballDeform?.Press();
        }

        /// <summary>현재 활성 모드의 Revert를 실행합니다. CharacterKeyManager에서 바인딩합니다.</summary>
        public void RevertActive()
        {
            if (_isTransitioning) return;

            if (_currentState == ModelState.Character)
                _characterDeform?.Revert();
            else
                _ballDeform?.Revert();
        }

        /// <summary>현재 활성 모드의 SnapToPress를 실행합니다.</summary>
        public void SnapToPressActive()
        {
            if (_isTransitioning) return;

            if (_currentState == ModelState.Character)
                _characterDeform?.SnapToPress();
            else
                _ballDeform?.SnapToPress();
        }

        /// <summary>현재 활성 모드의 SnapToOriginal을 실행합니다.</summary>
        public void SnapToOriginalActive()
        {
            if (_isTransitioning) return;

            if (_currentState == ModelState.Character)
                _characterDeform?.SnapToOriginal();
            else
                _ballDeform?.SnapToOriginal();
        }

        #endregion

        #region Forward Sequence: Character → Ball

        private void StartCharacterToBall()
        {
            _isTransitioning = true;

            if (_showDebugLog)
                Debug.Log("[ChangeModel] Forward 시작 | Character → Ball");

            _spherifyDeformer.OnSphereCompleted += OnSphereCompletedForward;
            _spherifyDeformer.TransformToSphere();
            _characterDeform.Press();
        }

        private void OnSphereCompletedForward()
        {
            _spherifyDeformer.OnSphereCompleted -= OnSphereCompletedForward;

            _ballObject.SetActive(true);
            _ballDeform.SnapToPress();
            _characterObject.SetActive(false);
            _ballDeform.Revert();

            _currentState = ModelState.Ball;
            _isTransitioning = false;

            if (_showDebugLog)
                Debug.Log("[ChangeModel] Forward 완료 | 상태: Ball");
        }

        #endregion

        #region Reverse Sequence: Ball → Character

        private void StartBallToCharacter()
        {
            _isTransitioning = true;

            if (_showDebugLog)
                Debug.Log("[ChangeModel] Reverse 시작 | Ball → Character");

            _ballDeform.OnPressCompleted += OnBallPressCompletedReverse;
            _ballDeform.Press();
        }

        private void OnBallPressCompletedReverse()
        {
            _ballDeform.OnPressCompleted -= OnBallPressCompletedReverse;

            _characterObject.SetActive(true);
            _ballObject.SetActive(false);
            _spherifyDeformer.RevertToOriginal();
            _characterDeform.Revert();

            _currentState = ModelState.Character;
            _isTransitioning = false;

            if (_showDebugLog)
                Debug.Log("[ChangeModel] Reverse 완료 | 상태: Character");
        }

        #endregion
    }
}
