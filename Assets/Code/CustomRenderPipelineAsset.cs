using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
[CreateAssetMenu(menuName = "Rendering/Custom Render Pipeline")]
public class CustomRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField]
    PostFXSettings postFXSettings = default;
    public CustomRenderSettings Settings;
    protected override RenderPipeline CreatePipeline()
    {
        return new CustomRenderPipeline(Settings, postFXSettings);
    }
}
