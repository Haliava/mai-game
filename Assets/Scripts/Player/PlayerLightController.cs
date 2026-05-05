using UnityEngine;

[RequireComponent(typeof(Light))]
public class PlayerLightController : MonoBehaviour
{
    [SerializeField] float lightRange = 2800f;
    [SerializeField] float centerIntensity = 16f;
    [SerializeField] float edgeIntensity = 11f;
    [SerializeField] float centerBrightRadius = 80f;
    [SerializeField] float edgeRadius = 190f;
    [SerializeField] Color lightColor = new Color(0.82f, 0.9f, 1f);
    [Header("Ash Light Rig")]
    [SerializeField] float headFillRange = 520f;
    [SerializeField] float headFillIntensity = 12f;
    [SerializeField] float forwardSpotRange = 950f;
    [SerializeField] float forwardSpotIntensity = 26f;
    [SerializeField] float forwardSpotAngle = 82f;
    [SerializeField] bool forwardSpotCastsShadows = true;
    [SerializeField] float forwardShadowStrength = 0.55f;
    [SerializeField] Color forwardSpotColor = new Color(0.72f, 0.86f, 1f);

    Light playerLight;
    Light headFillLight;
    Light forwardSpotLight;

    void Awake()
    {
        UpgradeLegacyLightValues();
        playerLight = GetComponent<Light>();
        playerLight.type = LightType.Point;
        playerLight.range = lightRange;
        playerLight.color = lightColor;
        playerLight.shadows = LightShadows.None;
        EnsureLightRig();
    }

    void Update()
    {
        EnsureLightRig();
        Vector2 planar = new Vector2(transform.position.x, transform.position.z);
        float t = Mathf.InverseLerp(centerBrightRadius, edgeRadius, planar.magnitude);
        playerLight.range = lightRange;
        playerLight.intensity = Mathf.Lerp(centerIntensity, edgeIntensity, t);

        if (headFillLight != null)
        {
            headFillLight.type = LightType.Point;
            headFillLight.range = headFillRange;
            headFillLight.intensity = headFillIntensity;
            headFillLight.color = lightColor;
            headFillLight.shadows = LightShadows.None;
        }

        if (forwardSpotLight != null)
        {
            forwardSpotLight.type = LightType.Spot;
            forwardSpotLight.range = forwardSpotRange;
            forwardSpotLight.intensity = forwardSpotIntensity;
            forwardSpotLight.spotAngle = forwardSpotAngle;
            forwardSpotLight.color = forwardSpotColor;
            forwardSpotLight.shadows = forwardSpotCastsShadows ? LightShadows.Soft : LightShadows.None;
            forwardSpotLight.shadowStrength = forwardShadowStrength;
        }
    }

    void UpgradeLegacyLightValues()
    {
        if (lightRange < 2200f || lightRange > 4200f) lightRange = 2800f;
        if (centerIntensity < 10f || centerIntensity > 40f) centerIntensity = 16f;
        if (edgeIntensity < 7f || edgeIntensity > 26f) edgeIntensity = 11f;
        if (centerBrightRadius < 50f || centerBrightRadius > 140f) centerBrightRadius = 80f;
        if (edgeRadius < 140f || edgeRadius > 280f) edgeRadius = 190f;
        if (headFillRange < 200f || headFillRange > 1200f) headFillRange = 520f;
        if (headFillIntensity < 5f || headFillIntensity > 30f) headFillIntensity = 12f;
        if (forwardSpotRange < 300f || forwardSpotRange > 1600f) forwardSpotRange = 950f;
        if (forwardSpotIntensity < 8f || forwardSpotIntensity > 60f) forwardSpotIntensity = 26f;
        if (forwardSpotAngle < 45f || forwardSpotAngle > 120f) forwardSpotAngle = 82f;
        if (forwardShadowStrength < 0.15f || forwardShadowStrength > 1f) forwardShadowStrength = 0.55f;
    }

    void EnsureLightRig()
    {
        Transform rigParent = Camera.main != null ? Camera.main.transform : transform;
        headFillLight = EnsureChildLight(rigParent, headFillLight, "AshHeadFill", LightType.Point);
        forwardSpotLight = EnsureChildLight(rigParent, forwardSpotLight, "AshForwardBeam", LightType.Spot);
    }

    Light EnsureChildLight(Transform parent, Light current, string objectName, LightType lightType)
    {
        GameObject lightObject;
        if (current != null)
        {
            lightObject = current.gameObject;
        }
        else
        {
            Transform existing = parent.Find(objectName);
            lightObject = existing != null ? existing.gameObject : new GameObject(objectName);
        }

        if (lightObject.transform.parent != parent)
        {
            lightObject.transform.SetParent(parent, false);
        }

        lightObject.name = objectName;
        lightObject.transform.localPosition = Vector3.zero;
        lightObject.transform.localRotation = Quaternion.identity;
        Light light = lightObject.GetComponent<Light>();
        if (light == null) light = lightObject.AddComponent<Light>();
        light.type = lightType;
        return light;
    }
}
