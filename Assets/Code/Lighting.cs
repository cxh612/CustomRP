using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class Lighting
{
    const string bufferName = "Lighting";

    CommandBuffer buffer = new CommandBuffer {
        name = bufferName
    };
    
    // private CommandBuffer _Cmd;
    private ShadowSettings _ShadowSettings;
    private Shadow _Shadow = new Shadow();
    private static int _CustomLightDirs = Shader.PropertyToID("_DirectionalLightDirections");
    private static int _CustomLightCount = Shader.PropertyToID("_DirectionalLightCount");
    private static int _CustomLightColors = Shader.PropertyToID("_DirectionalLightColors");
    private static int _DirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");
   
    private static int _CustomViewDir = Shader.PropertyToID("_CustomViewDir");
    private ScriptableRenderContext _Context;
    private CullingResults _CullingResults;
    private Camera _Camera;
    const int maxDirLightCount = 4;
    static Vector4[]
        dirLightColors = new Vector4[maxDirLightCount],
        dirLightDirections = new Vector4[maxDirLightCount],
        dirLightShadowData = new Vector4[maxDirLightCount];
    public Lighting(ShadowSettings shadowSettings)
    {
        _ShadowSettings = shadowSettings;
    }
    public void Setup(ScriptableRenderContext context,CullingResults cullingResults,Camera camera)
    {
        _Context = context;
        _Camera = camera;
        _CullingResults = cullingResults;
        
        buffer.BeginSample(bufferName);
        _Shadow.Setup(_Context,_CullingResults,_ShadowSettings);
        SetupLight();
        _Shadow.Render();
        buffer.EndSample(bufferName);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
    public void Cleanup () {
        _Shadow.Cleanup();
    }
    void SetupDirectionalLight (int index,ref VisibleLight visibleLight) {
        dirLightColors[index] =  visibleLight.finalColor;                          //等价light.color.linear * light.intensity
        dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2); //  -visibleLight.light.transform.forward;
        dirLightShadowData[index] = _Shadow.ReserveDirectionalShadows(visibleLight.light, index);
    }
    
    public void SetupLight()
    {
        LODGroup.crossFadeAnimationDuration=2.0f;
        NativeArray<VisibleLight> visibleLights = _CullingResults.visibleLights;
        int dirLightCount = 0;
        for (int i = 0; i < visibleLights.Length; i++)
        {
            VisibleLight visibleLight = visibleLights[i];
            if (visibleLight.lightType == LightType.Directional)//过滤其他类型光照
            {
                SetupDirectionalLight(dirLightCount++, ref visibleLight);
                if (dirLightCount >= maxDirLightCount)
                {
                    break;
                }
            }
        }
        
        buffer.SetGlobalInt(_CustomLightCount,dirLightCount);
        buffer.SetGlobalVectorArray(_CustomLightColors ,dirLightColors);
        buffer.SetGlobalVectorArray(_CustomLightDirs,dirLightDirections);
        buffer.SetGlobalVectorArray(_DirLightShadowDataId, dirLightShadowData);
        buffer.SetGlobalVector(_CustomViewDir,-_Camera.transform.forward.normalized);
        _Context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }
}
