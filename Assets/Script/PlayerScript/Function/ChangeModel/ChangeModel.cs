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

        [Tooltip("캐릭터 메시 전환용 브릿지. Character→Ball 시 MeshFilter 부모 켜고 SkinnedMesh 부모 끔")]
        [SerializeField] private MeshConverterBridge _bridge = null;

        [Header("볼 참조")]
        [Tooltip("볼 오브젝트의 CharacterDeform (Press 기능)")]
        [SerializeField] private CharacterDeform _ballDeform = null;

        [Tooltip("활성/비활성 제어할 볼 루트 오브젝트")]
        [SerializeField] private GameObject _ballObject = null;

        [Tooltip("볼에 붙어 있는 SphereCollider. 공 팽창 완료 시 활성화, 모델 전환 시작 시 비활성화")]
        [SerializeField] private SphereCollider _ballCollider = null;

        [Tooltip("볼에 붙어 있는 AutoRotate. 공 팽창 완료 시 활성화, 모델 전환 시작 시 비활성화")]
        [SerializeField] private AutoRotate _ballAutoRotate = null;

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

        /// <summary>캐릭터→볼 전환 시, 볼 팽창(Revert)까지 완료된 뒤 한 번 호출됩니다.</summary>
        public event System.Action OnBallExpansionCompleted;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_ballObject != null)
                _ballObject.SetActive(false);

            if (_ballCollider != null)
                _ballCollider.enabled = false;

            if (_ballAutoRotate != null)
                _ballAutoRotate.enabled = false;

            if (_showDebugLog)
                Debug.Log("[ChangeModel] Awake | ballObject 비활성화, currentState=Character", this);
        }

        #endregion

        #region Public API - 전환

        /// <summary>
        /// 현재 상태에 따라 Forward(캐릭터→볼) 또는 Reverse(볼→캐릭터) 시퀀스를 실행합니다.
        /// 전환 중에는 재호출을 무시합니다.
        /// </summary>
        public void Toggle()
        {
            if (_isTransitioning)
            {
                if (_showDebugLog)
                    Debug.Log("[ChangeModel] Toggle 무시 | 전환 중", this);
                return;
            }

            if (_currentState == ModelState.Character)
            {
                if (_showDebugLog)
                    Debug.Log("[ChangeModel] Toggle | Character → Ball 시퀀스 시작", this);
                StartCharacterToBall();
            }
            else
            {
                if (_showDebugLog)
                    Debug.Log("[ChangeModel] Toggle | Ball → Character 시퀀스 시작", this);
                StartBallToCharacter();
            }
        }

        #endregion

        #region Public API - Press 라우팅

        /// <summary>현재 활성 모드의 Press를 실행합니다. CharacterKeyManager에서 바인딩합니다.</summary>
        public void PressActive()
        {
            if (_isTransitioning) return;

            if (_currentState == ModelState.Character)
            {
                if (_showDebugLog)
                    Debug.Log("[ChangeModel] PressActive | CharacterDeform.Press()", this);
                _characterDeform?.Press();
            }
            else
            {
                if (_showDebugLog)
                    Debug.Log("[ChangeModel] PressActive | BallDeform.Press()", this);
                _ballDeform?.Press();
            }
        }

        /// <summary>현재 활성 모드의 Revert를 실행합니다. CharacterKeyManager에서 바인딩합니다.</summary>
        public void RevertActive()
        {
            if (_isTransitioning) return;

            if (_currentState == ModelState.Character)
            {
                if (_showDebugLog)
                    Debug.Log("[ChangeModel] RevertActive | CharacterDeform.Revert()", this);
                _characterDeform?.Revert();
            }
            else
            {
                if (_showDebugLog)
                    Debug.Log("[ChangeModel] RevertActive | BallDeform.Revert()", this);
                _ballDeform?.Revert();
            }
        }

        /// <summary>현재 활성 모드의 SnapToPress를 실행합니다.</summary>
        public void SnapToPressActive()
        {
            if (_isTransitioning) return;

            if (_currentState == ModelState.Character)
            {
                if (_showDebugLog)
                    Debug.Log("[ChangeModel] SnapToPressActive | CharacterDeform.SnapToPress()", this);
                _characterDeform?.SnapToPress();
            }
            else
            {
                if (_showDebugLog)
                    Debug.Log("[ChangeModel] SnapToPressActive | BallDeform.SnapToPress()", this);
                _ballDeform?.SnapToPress();
            }
        }

        /// <summary>현재 활성 모드의 SnapToOriginal을 실행합니다.</summary>
        public void SnapToOriginalActive()
        {
            if (_isTransitioning) return;

            if (_currentState == ModelState.Character)
            {
                if (_showDebugLog)
                    Debug.Log("[ChangeModel] SnapToOriginalActive | CharacterDeform.SnapToOriginal()", this);
                _characterDeform?.SnapToOriginal();
            }
            else
            {
                if (_showDebugLog)
                    Debug.Log("[ChangeModel] SnapToOriginalActive | BallDeform.SnapToOriginal()", this);
                _ballDeform?.SnapToOriginal();
            }
        }

        /// <summary>
        /// 캐릭터 → 공 모드로 전환합니다. 타임라인 등 외부에서 호출용.
        /// 전환 중이거나 이미 Ball 상태면 무시합니다.
        /// </summary>
        public void ChangeToBall()
        {
            if (_isTransitioning) return;
            if (_currentState == ModelState.Ball) return;
            if (_showDebugLog)
                Debug.Log("[ChangeModel] ChangeToBall (타임라인 호출)", this);
            StartCharacterToBall();
        }

        /// <summary>
        /// 공 → 캐릭터 모드로 전환합니다. 타임라인 등 외부에서 호출용.
        /// 전환 중이거나 이미 Character 상태면 무시합니다.
        /// </summary>
        public void ChangeToCharacter()
        {
            if (_isTransitioning) return;
            if (_currentState == ModelState.Character) return;
            if (_showDebugLog)
                Debug.Log("[ChangeModel] ChangeToCharacter (타임라인 호출)", this);
            StartBallToCharacter();
        }

        #endregion

        #region Forward Sequence: Character → Ball

        private void StartCharacterToBall()
        {
            _isTransitioning = true;

            if (_bridge != null)
                _bridge.SwapAll(useStatic: true);

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

            _ballDeform.OnRevertCompleted += OnBallRevertCompletedForward;

            _currentState = ModelState.Ball;
            _isTransitioning = false;

            if (_showDebugLog)
                Debug.Log("[ChangeModel] Forward 완료 | 상태: Ball (볼 팽창 완료 시 Collider 활성화 예정)");
        }

        /// <summary>볼 Revert(팽창) 완료 시 SphereCollider를 활성화합니다.</summary>
        private void OnBallRevertCompletedForward()
        {
            _ballDeform.OnRevertCompleted -= OnBallRevertCompletedForward;

            if (_ballCollider != null)
                _ballCollider.enabled = true;

            if (_ballAutoRotate != null)
                _ballAutoRotate.enabled = true;

            OnBallExpansionCompleted?.Invoke();

            if (_showDebugLog)
                Debug.Log("[ChangeModel] 볼 팽창 완료 | Ball Collider, AutoRotate 활성화", this);
        }

        #endregion

        #region Reverse Sequence: Ball → Character

        private void StartBallToCharacter()
        {
            _isTransitioning = true;

            if (_ballCollider != null)
                _ballCollider.enabled = false;

            if (_ballAutoRotate != null)
                _ballAutoRotate.enabled = false;

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

            // Revert가 끝난 시점에 MeshFilter → SkinnedMesh로 Swap
            _spherifyDeformer.OnRevertCompleted += OnSpherifyRevertCompletedReverse;
            _spherifyDeformer.RevertToOriginal();
            _characterDeform.Revert();

            _currentState = ModelState.Character;
            _isTransitioning = false;

            if (_showDebugLog)
                Debug.Log("[ChangeModel] Reverse 완료 | 상태: Character (Revert 진행 중)");
        }

        /// <summary>
        /// SpherifyDeformer.RevertToOriginal() 완료 시 호출되어 MeshFilter 부모를 끄고 SkinnedMesh 부모를 켭니다.
        /// </summary>
        private void OnSpherifyRevertCompletedReverse()
        {
            _spherifyDeformer.OnRevertCompleted -= OnSpherifyRevertCompletedReverse;

            if (_bridge != null)
            {
                _bridge.SwapAll(useStatic: false);

                if (_showDebugLog)
                    Debug.Log("[ChangeModel] OnSpherifyRevertCompletedReverse | SwapAll(false) - SkinnedMesh 활성, MeshFilter 비활성", this);
            }
        }

        #endregion
    }
}
