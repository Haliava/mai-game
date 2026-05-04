using UnityEngine;

[RequireComponent(typeof(Light))]
public class PlayerLightController : MonoBehaviour
{
    [SerializeField] float lightRange = 130f;
    [SerializeField] float centerIntensity = 1.8f;
    [SerializeField] float edgeIntensity = 1.15f;
    [SerializeField] float centerBrightRadius = 70f;
    [SerializeField] float edgeRadius = 160f;
    [SerializeField] Color lightColor = new Color(0.82f, 0.9f, 1f);

    Light playerLight;

    void Awake()
    {
        UpgradeLegacyLightValues();
        playerLight = GetComponent<Light>();
        playerLight.type = LightType.Point;
        playerLight.range = lightRange;
        playerLight.color = lightColor;
        playerLight.shadows = LightShadows.None;
    }

    void Update()
    {
        Vector2 planar = new Vector2(transform.position.x, transform.position.z);
        float t = Mathf.InverseLerp(centerBrightRadius, edgeRadius, planar.magnitude);
        playerLight.range = lightRange;
        playerLight.intensity = Mathf.Lerp(centerIntensity, edgeIntensity, t);
    }

    void UpgradeLegacyLightValues()
    {
        if (lightRange > 220f || lightRange < 80f) lightRange = 130f;
        if (centerIntensity > 6f || centerIntensity < 0.8f) centerIntensity = 1.8f;
        if (edgeIntensity > 4f || edgeIntensity < 0.5f) edgeIntensity = 1.15f;
        if (centerBrightRadius > 140f || centerBrightRadius < 40f) centerBrightRadius = 70f;
        if (edgeRadius > 240f || edgeRadius < 100f) edgeRadius = 160f;
    }
}
