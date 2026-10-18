using UnityEngine;

// A slow, shallow fluorescent fluctuation. No abrupt strobing or added colliders.
public sealed class HorrorLamp : MonoBehaviour
{
    [Range(0,.3f)] public float intensityVariation=.09f;
    [Min(.05f)] public float variationSpeed=.6f;
    Light source;
    float originalIntensity,phase;
    public void Initialize(int seed)
    {
        source=GetComponentInChildren<Light>();
        if(source)originalIntensity=source.intensity;
        phase=(seed&1023)*.731f;
    }
    void Update()
    {
        if(!source)return;
        float noise=Mathf.PerlinNoise(phase,Time.time*variationSpeed);
        source.intensity=originalIntensity*(1-intensityVariation+noise*intensityVariation*2);
    }
}
