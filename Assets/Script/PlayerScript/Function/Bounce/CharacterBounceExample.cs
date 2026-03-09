using UnityEngine;

public class CharacterBounceExample : MonoBehaviour
{
    [SerializeField] private CharacterBounceController bounceController;
    [SerializeField, Range(0f, 0.2f)] private float wpoIntensity = 0.1f;

    private float _prevIntensity;

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
}
