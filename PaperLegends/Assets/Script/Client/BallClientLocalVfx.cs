using UnityEngine;

[DisallowMultipleComponent]
public class BallClientLocalVfx : MonoBehaviour
{
    [SerializeField] private GameObject Level10VFXPrefab;

    private GameObject level10VfxInstance;

    public void SetLevel(int level)
    {
        if (!GameInitializer.TryGetSkillLevelVfxColor(level, out var vfxColor))
        {
            if (level10VfxInstance != null)
                level10VfxInstance.SetActive(false);
            return;
        }

        if (Level10VFXPrefab == null)
            return;

        if (level10VfxInstance == null)
        {
            level10VfxInstance = Instantiate(Level10VFXPrefab, transform);
            level10VfxInstance.transform.localPosition = Vector3.zero;
            level10VfxInstance.transform.localRotation = Quaternion.identity;
        }

        ApplyVfxColor(level10VfxInstance, vfxColor);
        level10VfxInstance.SetActive(true);
    }

    private static void ApplyVfxColor(GameObject vfxObject, Color color)
    {
        if (vfxObject == null)
            return;

        var particleSystems = vfxObject.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var particleSystem in particleSystems)
        {
            var main = particleSystem.main;
            main.startColor = color;
        }
    }
}
