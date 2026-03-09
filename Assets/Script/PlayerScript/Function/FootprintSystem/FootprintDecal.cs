using System;
using System.Collections;
using UnityEngine;

public class FootprintDecal : MonoBehaviour
{
    #region Inspector

    [SerializeField] private MeshRenderer _renderer;

    #endregion

    #region Private Fields

    private MaterialPropertyBlock _propBlock;
    private Coroutine _lifetimeCoroutine;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _propBlock = new MaterialPropertyBlock();
    }

    #endregion

    #region Public API

    // 풀에서 꺼낼 때 호출
    public void Activate(float lifetime, float fadeOutDuration, Action<FootprintDecal> onRelease)
    {
        if (_lifetimeCoroutine != null)
            StopCoroutine(_lifetimeCoroutine);
        _lifetimeCoroutine = StartCoroutine(LifetimeRoutine(lifetime, fadeOutDuration, onRelease));
    }

    #endregion

    #region Private

    private IEnumerator LifetimeRoutine(float lifetime, float fadeOutDuration, Action<FootprintDecal> onRelease)
    {
        // 알파 1로 초기화
        SetAlpha(1f);
        yield return new WaitForSeconds(lifetime - fadeOutDuration);

        // 페이드아웃
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(1f - (elapsed / fadeOutDuration));
            yield return null;
        }

        onRelease?.Invoke(this);
    }

    private void SetAlpha(float alpha)
    {
        _renderer.GetPropertyBlock(_propBlock);
        _propBlock.SetFloat("_Alpha", alpha);
        _renderer.SetPropertyBlock(_propBlock);
    }

    #endregion
}
