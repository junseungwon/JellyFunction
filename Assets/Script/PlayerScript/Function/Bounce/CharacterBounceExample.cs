using UnityEngine;

public class CharacterBounceExample : MonoBehaviour
{
    #region Inspector

    [SerializeField] private CharacterBounceController bounceController;
    [SerializeField, Range(0f, 0.2f)] private float wpoIntensity = 0.1f;

    #endregion

    #region Private Fields

    private float _prevIntensity;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        _prevIntensity = wpoIntensity;
        //bounceController.SetExcitedState();
    }

    private void Update()
    {
        if (_prevIntensity == wpoIntensity) return;

        bounceController.WPOIntensity = wpoIntensity;
        _prevIntensity = wpoIntensity;
    }

    #endregion
}
