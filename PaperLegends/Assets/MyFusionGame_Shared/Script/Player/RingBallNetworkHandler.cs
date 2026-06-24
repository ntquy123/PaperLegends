using System.Collections;
using Fusion;
using UnityEngine;

public class RingBallNetworkHandler : NetworkBehaviour
{
    [SerializeField] private Transform InterpolationTarget;
#if !UNITY_SERVER
    [SerializeField] private Renderer _ballRenderer;
    [SerializeField] private Renderer _cateyeRenderer;
    [SerializeField] private bool _hasCateye = true;
    private GameObject _ballVisualInstance;
#endif
    public override void Spawned()
    {
        base.Spawned();
#if !UNITY_SERVER
        EnsureBallModelVisualInstance();
        StartCoroutine(ApplyDefaultVisuals());
#endif
    }

#if !UNITY_SERVER
    private void EnsureBallModelVisualInstance()
    {
        if (_ballVisualInstance != null || GameInitializer.Instance.BallModelVisual == null)
            return;

        var parent = InterpolationTarget != null ? InterpolationTarget : transform;
        _ballVisualInstance = Instantiate(GameInitializer.Instance.BallModelVisual);
        _ballVisualInstance.transform.SetParent(parent, false);
        _ballVisualInstance.transform.localPosition = Vector3.zero;
        _ballVisualInstance.transform.localRotation = Quaternion.identity;
        _ballVisualInstance.transform.localScale = Vector3.one;

        if (_ballRenderer == null)
            _ballRenderer = _ballVisualInstance.GetComponent<Renderer>();

        if (_cateyeRenderer == null)
        {
            var cateye = _ballVisualInstance.transform.Find("Cateye");
            if (cateye != null)
                _cateyeRenderer = cateye.GetComponent<Renderer>();
        }
    }

    private IEnumerator ApplyDefaultVisuals()
    {
        CacheRenderers();

        yield return ApplyBallMaterial();
        yield return ApplyCateyeMaterial();
    }

    private void CacheRenderers()
    {
        EnsureBallModelVisualInstance();

        var root = _ballVisualInstance != null ? _ballVisualInstance.transform : transform;

        if (_ballRenderer == null)
        {
            _ballRenderer = _ballVisualInstance != null
                ? _ballVisualInstance.GetComponent<Renderer>()
                : GetComponent<Renderer>();
        }

        if (_cateyeRenderer == null)
        {
            var cateye = root.Find("Cateye");
            if (cateye != null)
                _cateyeRenderer = cateye.GetComponent<Renderer>();
        }
    }

    private IEnumerator ApplyBallMaterial()
    {
        if (_ballRenderer == null)
            yield break;

        Material ballMaterial = null;
        yield return AddressablesHelper.LoadAsset<Material>(AddressablePaths.Items.DefaultCuliMaterial, mat => ballMaterial = mat);

        if (ballMaterial != null)
        {
            _ballRenderer.enabled = true;
            _ballRenderer.material = ballMaterial;
        }
    }

    private IEnumerator ApplyCateyeMaterial()
    {
        if (_cateyeRenderer == null)
            yield break;

        if (!_hasCateye)
        {
            _cateyeRenderer.gameObject.SetActive(false);
            yield break;
        }

        Material cateyeMaterial = null;
        int cateyeId = Random.Range(1, 4);
        string cateyePath = $"{AddressablePaths.Items.CuliCateyeRingBall}/{cateyeId}.mat";

        yield return AddressablesHelper.LoadAsset<Material>(cateyePath, mat => cateyeMaterial = mat);

        if (cateyeMaterial == null)
        {
            yield return AddressablesHelper.LoadAsset<Material>(AddressablePaths.Items.DefaultCateyeCuliMaterial, mat => cateyeMaterial = mat);
        }

        if (cateyeMaterial != null)
        {
            _cateyeRenderer.gameObject.SetActive(true);
            _cateyeRenderer.enabled = true;
            _cateyeRenderer.material = cateyeMaterial;
        }
    }
#endif
}
