using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CustomRenderPipeline : RenderPipeline
{
    private CustomRender renderer;
    public CustomRenderPipeline(CustomRenderSettings renderSettings,PostFXSettings postFXSettings) {
        GraphicsSettings.useScriptableRenderPipelineBatching = true;
        GraphicsSettings.lightsUseLinearIntensity = true;//线性灯光
        renderer = new CustomRender(renderSettings,postFXSettings);
    }
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach (Camera camera in cameras) {
            renderer.Render(context, camera);
        }
    }
}
