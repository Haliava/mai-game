using UnityEngine;

[RequireComponent(typeof(Light))]
public class PlayerLightController : MonoBehaviour
{
    [SerializeField] float lightRange = 360f;
    [SerializeField] float centerIntensity = 7f;
    [SerializeField] float edgeIntensity = 4.5f;
    [SerializeField] float centerBrightRadius = 90f;
    [SerializeField] float edgeRadius = 180f;
    [SerializeField] Color lightColor = new Color(0.72f, 0.82f, 1f);

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
        if (lightRange < 300f) lightRange = 360f;
        if (centerIntensity < 6f) centerIntensity = 7f;
        if (edgeIntensity < 4f) edgeIntensity = 4.5f;
        if (centerBrightRadius < 80f) centerBrightRadius = 90f;
        if (edgeRadius < 170f) edgeRadius = 180f;
    }
}
