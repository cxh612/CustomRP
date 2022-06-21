using UnityEngine;
using UnityEngine.Rendering;

[System.Serializable]
public class CustomRenderSettings {
    [SerializeField]
    public bool allowHDR = true;
    public ShadowSettings ShadowConfig = new ShadowSettings();
}
public partial class CustomRender 
{
    #region Pass
    private PostFXStack _PostFXStack;// = new PostFXStack();
    private Lighting _Lighting;
    #endregion
    #region ShaderID
    private static ShaderTagId _UnlitShaderTagId1 = new ShaderTagId("SRPDefaultUnlit");
    private static ShaderTagId _CustomLitShaderTagId = new ShaderTagId("CustomLit");
    private static int _FrameBufferId = Shader.PropertyToID("_CameraFrameBuffer");
    #endregion
    const string _BufferName = "Render Camera";
    CommandBuffer _Buffer = new CommandBuffer {
        name = _BufferName
    };
    
    private CullingResults _CullingResults;
    private ScriptableRenderContext _Context;
    private Camera _Camera;
    private CustomRenderSettings _RenderSettings;
    public CustomRender(CustomRenderSettings renderSettings, PostFXSettings postFXSettings)
    {
        _RenderSettings = renderSettings;
        _Lighting = new Lighting(renderSettings.ShadowConfig);
        _PostFXStack = new PostFXStack(postFXSettings);
    }

    public void Render(ScriptableRenderContext context, Camera camera)
    {
        _Context = context;
        _Camera = camera;
        PrepareBuffer();        //修改profile name相机
        PrepareForSceneWindow();//SceneWindow UI
        if (!Cull(_RenderSettings.ShadowConfig.MaxDistance)) {
            return;
        }
        _Buffer.BeginSample(SampleName);
        ExcuteCommand();
        _Lighting.Setup(_Context,_CullingResults,_Camera);
        Setup();
        
        //Draw
        DrawVisibleGeometry();//绘制可见几何体
        DrawUnsupportedShaders();//Draw Error
        DrawGizmos();
        
       if (_PostFXStack.IsActive) {
           _PostFXStack.Render(_FrameBufferId);
       }
       Cleanup();
       _Buffer.EndSample(SampleName);
       Submit();
    }
    
    void Cleanup () {
        _Lighting.Cleanup();
        if (_PostFXStack.IsActive) {
            _Buffer.ReleaseTemporaryRT(_FrameBufferId);
        }
    }

    public bool Cull(int maxShadowDistance)
    {
        if (_Camera.TryGetCullingParameters(out ScriptableCullingParameters p))
        {
            p.shadowDistance = Mathf.Min(maxShadowDistance, _Camera.farClipPlane);// shaderMaxDistance;
            _CullingResults = _Context.Cull(ref p);
            return true;
        }
        return false;
    }

    private void Setup()
    {
        _Context.SetupCameraProperties(_Camera);
        CameraClearFlags flags = _Camera.clearFlags;
        bool hdr = _RenderSettings.allowHDR && _Camera.allowHDR;
        _PostFXStack.Setup(_Context,_Camera, hdr);
        if (_PostFXStack.IsActive) {
            if (flags > CameraClearFlags.Color) {
                flags = CameraClearFlags.Color;
            }
            _Buffer.GetTemporaryRT(_FrameBufferId, _Camera.pixelWidth, _Camera.pixelHeight, 32, FilterMode.Bilinear, hdr ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
            _Buffer.SetRenderTarget(_FrameBufferId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        }
        _Buffer.EndSample(_BufferName);// 打断Profile Sample
        _Buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth, flags == CameraClearFlags.Color, flags == CameraClearFlags.Color ? _Camera.backgroundColor.linear : Color.clear);
        _Buffer.BeginSample(_BufferName);// 打断打断Profile Sample
        ExcuteCommand();
    }

    private void ExcuteCommand()
    {
        _Context.ExecuteCommandBuffer(_Buffer);
        _Buffer.Clear();
    }
    
    private void Submit()
    {
       
        ExcuteCommand();
        _Context.Submit();
    }
    
    private void DrawVisibleGeometry()
    {
        var sortingSettings = new SortingSettings(_Camera);//渲染时执行的排序类型
        sortingSettings.criteria = SortingCriteria.CommonOpaque;
        var drawingSettings = new DrawingSettings(_UnlitShaderTagId1, sortingSettings);
        drawingSettings.SetShaderPassName(1,_CustomLitShaderTagId);
        drawingSettings.enableInstancing = true;
        drawingSettings.perObjectData = PerObjectData.Lightmaps | PerObjectData.ShadowMask | PerObjectData.LightProbe |
                                        PerObjectData.LightProbeProxyVolume | PerObjectData.OcclusionProbe | PerObjectData.OcclusionProbeProxyVolume | PerObjectData.ReflectionProbes;
        
        var filteringSettings = GetFiltering(RenderQueueRange.opaque);
        _Context.DrawRenderers(_CullingResults,ref drawingSettings,ref  filteringSettings);
        
        //天空盒
        _Context.DrawSkybox(_Camera);
        //透明物体
        filteringSettings=GetFiltering(RenderQueueRange.transparent);
        filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        sortingSettings.criteria = SortingCriteria.CommonTransparent;
        drawingSettings.sortingSettings = sortingSettings;
        _Context.DrawRenderers(_CullingResults, ref drawingSettings, ref filteringSettings);
    }
    
    private FilteringSettings GetFiltering(RenderQueueRange queue)
    {
        FilteringSettings filteringSettings = new FilteringSettings(queue);
        filteringSettings.excludeMotionVectorObjects = true;//TODO:
        filteringSettings.layerMask = -1;// LayerMask.GetMask("Default") | LayerMask.GetMask("Water") ;
        filteringSettings.renderingLayerMask = uint.MaxValue;//(uint)(1<<7);
        filteringSettings.renderQueueRange = queue;
        //filteringSettings.sortingLayerRange = new SortingLayerRange(0, 100);//See:Unity Project Setting->Tags and Layers.
        return filteringSettings;
    }
}
