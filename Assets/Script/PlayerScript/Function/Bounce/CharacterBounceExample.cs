using UnityEngine;

public class CharacterBounceExample : MonoBehaviour
{
    #region Inspector

    [SerializeField] private CharacterBounceController _bounceController = null;
    [SerializeField, Range(0f, 0.2f)] private float _wpoIntensity = 0.1f;

    #endregion

    #region Private Fields

    private float _prevIntensity;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        _prevIntensity = _wpoIntensity;
    }

    private void Update()
    {
        if (_prevIntensity == _wpoIntensity) return;

        _bounceController.WPOIntensity = _wpoIntensity;
        _prevIntensity = _wpoIntensity;
    }

    #endregion
}
