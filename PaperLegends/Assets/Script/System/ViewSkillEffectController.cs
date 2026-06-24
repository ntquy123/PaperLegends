using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[System.Serializable]
public class ViewSkillEffectController
{
    [Header("VIEW SKILL CONFIG")]
    [SerializeField] private float viewSkillDuration = 8f;
    [SerializeField] private float viewSkillExposureBoost = -0.65f;
    [SerializeField] private float viewSkillBloomIntensity = 6f;
    [SerializeField] private Light viewSkillMainLight;
    [SerializeField] private float viewSkillLightIntensityMultiplier = 0.35f;
    [SerializeField] private GameObject viewSkillVfxPrefab;

    private readonly List<GameObject> _viewSkillVfxInstances = new List<GameObject>();
    private Coroutine _viewSkillCoroutine;
    private Volume _viewSkillVolume;
    private ColorAdjustments _viewSkillColorAdjustments;
    private Bloom _viewSkillBloom;
    private MonoBehaviour _owner;
    private bool _cachedLightSettings;
    private float _originalLightIntensity;
    private Color _originalLightColor;
    private bool _originalLightEnabled;

    public void Initialize(MonoBehaviour owner)
    {
        _owner = owner;
    }

    public void SetViewSkillVfxPrefab(GameObject prefab)
    {
        viewSkillVfxPrefab = prefab;
    }

    public bool TryActivate()
    {
        if (_owner == null)
        {
            Debug.LogWarning("[ViewSkill] Owner is missing, cannot start effect.");
            return false;
        }

        if (_viewSkillCoroutine != null)
            _owner.StopCoroutine(_viewSkillCoroutine);

        _viewSkillCoroutine = _owner.StartCoroutine(HandleViewSkillRoutine());
        return true;
    }

    public void StopViewSkillEffect()
    {
        if (_owner == null)
            return;

        if (_viewSkillCoroutine != null)
        {
            _owner.StopCoroutine(_viewSkillCoroutine);
            _viewSkillCoroutine = null;
        }

        DisableViewSkillVisuals();
    }

    private IEnumerator HandleViewSkillRoutine()
    {
        EnableViewSkillVisuals();
        yield return new WaitForSeconds(viewSkillDuration);
        DisableViewSkillVisuals();
        _viewSkillCoroutine = null;
    }

    private void EnableViewSkillVisuals()
    {
        EnsureViewSkillVolume();
        if (_viewSkillVolume != null)
            _viewSkillVolume.weight = 1f;

        DimMainLight();
        SpawnViewSkillVfx();
    }

    private void DisableViewSkillVisuals()
    {
        if (_viewSkillVolume != null)
            _viewSkillVolume.weight = 0f;

        ClearViewSkillVfx();
        RestoreMainLight();
    }

    private void EnsureViewSkillVolume()
    {
        if (_viewSkillVolume != null || _owner == null)
            return;

        _viewSkillVolume = _owner.gameObject.AddComponent<Volume>();
        _viewSkillVolume.isGlobal = true;
        _viewSkillVolume.priority = 50f;
        _viewSkillVolume.weight = 0f;
        _viewSkillVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();

        _viewSkillColorAdjustments = _viewSkillVolume.profile.Add<ColorAdjustments>(true);
        _viewSkillColorAdjustments.saturation.overrideState = true;
        _viewSkillColorAdjustments.saturation.value = -100f;
        _viewSkillColorAdjustments.postExposure.overrideState = true;
        _viewSkillColorAdjustments.postExposure.value = viewSkillExposureBoost;

        _viewSkillBloom = _viewSkillVolume.profile.Add<Bloom>(true);
        _viewSkillBloom.threshold.overrideState = true;
        _viewSkillBloom.threshold.value = 0.75f;
        _viewSkillBloom.intensity.overrideState = true;
        _viewSkillBloom.intensity.value = viewSkillBloomIntensity;
    }

    private void DimMainLight()
    {
        if (viewSkillMainLight == null)
            viewSkillMainLight = ResolveMainLight();

        if (viewSkillMainLight == null)
            return;

        if (!_cachedLightSettings)
        {
            _originalLightIntensity = viewSkillMainLight.intensity;
            _originalLightColor = viewSkillMainLight.color;
            _originalLightEnabled = viewSkillMainLight.enabled;
            _cachedLightSettings = true;
        }

        viewSkillMainLight.enabled = true;
        viewSkillMainLight.intensity = _originalLightIntensity * Mathf.Clamp01(viewSkillLightIntensityMultiplier);
        viewSkillMainLight.color = _originalLightColor;
    }

    private void RestoreMainLight()
    {
        if (viewSkillMainLight == null || !_cachedLightSettings)
            return;

        viewSkillMainLight.intensity = _originalLightIntensity;
        viewSkillMainLight.color = _originalLightColor;
        viewSkillMainLight.enabled = _originalLightEnabled;
    }

    private Light ResolveMainLight()
    {
        if (RenderSettings.sun != null)
            return RenderSettings.sun;

        var lights = Object.FindObjectsOfType<Light>(true);
        foreach (var light in lights)
        {
            if (light != null && light.type == LightType.Directional)
                return light;
        }

        return null;
    }

    private void SpawnViewSkillVfx()
    {
        ClearViewSkillVfx();

        if (viewSkillVfxPrefab == null)
            return;

        var targets = GameObject.FindGameObjectsWithTag("BallPlayer");
        foreach (var target in targets)
        {
            if (target == null)
                continue;

            var instance = Object.Instantiate(
                viewSkillVfxPrefab,
                target.transform.position,
                Quaternion.identity,
                target.transform);
            _viewSkillVfxInstances.Add(instance);
        }
    }

    private void ClearViewSkillVfx()
    {
        if (_viewSkillVfxInstances.Count == 0)
            return;

        foreach (var instance in _viewSkillVfxInstances)
        {
            if (instance != null)
                Object.Destroy(instance);
        }

        _viewSkillVfxInstances.Clear();
    }
}
