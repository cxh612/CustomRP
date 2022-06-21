using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class PostFXStack {

    const string bufferName = "Post FX";
    const int maxBloomPyramidLevels = 16;
    int bloomPyramidId;
    enum Pass {
        BloomHorizontal,
        BloomVertical,
        Copy,
        BloomAdd,
        BloomScatter,
        BloomPrefilter,
        BloomPrefilterFireflies,
        BloomScatterFinal,
    }
    CommandBuffer buffer = new CommandBuffer {
        name = bufferName
    };
    private bool _UseHDR;
    private PostFXSettings _Settings;
    private ScriptableRenderContext _Context;
    private int _SourceId = Shader.PropertyToID("_PostFXSource");
    private int _Source2Id = Shader.PropertyToID("_PostFXSource2");
    private int bloomBucibicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling");
    private int bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter");
    private int bloomThresholdId = Shader.PropertyToID("_BloomThreshold");
    private int bloomIntensityId = Shader.PropertyToID("_BloomIntensity");
    public bool IsActive =>  _Settings != null && _Settings.IsActive;
    private Camera _Camera;

    public PostFXStack (PostFXSettings postFXSettingsXSettings) {
        _Settings = postFXSettingsXSettings;
        bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
        for (int i = 1; i < maxBloomPyramidLevels * 2; i++) {
            Shader.PropertyToID("_BloomPyramid" + i);
        }
    }
    void DoBloom (int sourceId) {
        PostFXSettings.BloomSettings bloom = _Settings.Bloom;
        buffer.SetGlobalFloat(bloomBucibicUpsamplingId, bloom.bicubicUpsampling ? 1f : 0f);
        buffer.BeginSample("Bloom Down");
        int width = _Camera.pixelWidth / 2, height = _Camera.pixelHeight / 2;
        if (bloom.maxIterations == 0 || bloom.intensity <= 0f || height < bloom.downscaleLimit*2 || width < bloom.downscaleLimit*2) {
            Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
            buffer.EndSample("Bloom Down");
            return;
        }
        
        Vector4 threshold;
        threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
        threshold.y = threshold.x * bloom.thresholdKnee;
        threshold.z = 2f * threshold.y;
        threshold.w = 0.25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;
        buffer.SetGlobalVector(bloomThresholdId, threshold);

        bool isHdr = _UseHDR && this._Settings.allowHDR;
        RenderTextureFormat format =  isHdr?RenderTextureFormat.DefaultHDR: RenderTextureFormat.Default;
        buffer.GetTemporaryRT(bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format);
        
        Draw(sourceId, bloomPrefilterId, bloom.fadeFireflies ? Pass.BloomPrefilterFireflies : Pass.BloomPrefilter);
        width /= 2;
        height /= 2;
        int fromId = bloomPrefilterId, toId = bloomPyramidId+1;
        int i;
        for (i = 0; i < bloom.maxIterations; i++) {
            if (height < bloom.downscaleLimit || width < bloom.downscaleLimit) break;
            int midId = toId - 1;
            
            buffer.GetTemporaryRT(midId, width, height, 0, FilterMode.Bilinear, format);
            buffer.GetTemporaryRT(toId, width, height, 0, FilterMode.Bilinear, format);
            
            Draw(fromId, midId, Pass.BloomHorizontal);
            Draw(midId, toId, Pass.BloomVertical);
            fromId = toId;
            toId += 2;
            width /= 2;
            height /= 2;
        }
        buffer.EndSample("Bloom Down");
        buffer.BeginSample("Bloom Up");
        buffer.ReleaseTemporaryRT(bloomPrefilterId);
        buffer.SetGlobalFloat(bloomIntensityId, 1f);
        Pass combinePass,finalPass;;
        float finalIntensity;
        if (bloom.mode == PostFXSettings.BloomSettings.Mode.Additive) {
            combinePass=finalPass = Pass.BloomAdd;
            buffer.SetGlobalFloat(bloomIntensityId, 1f);
            finalIntensity = bloom.intensity;
        }
        else {
            combinePass = Pass.BloomScatter;
            finalPass = Pass.BloomScatterFinal;
            buffer.SetGlobalFloat(bloomIntensityId, bloom.scatter);
            finalIntensity = Mathf.Min(bloom.intensity, 0.95f);
        }
        
        if (i > 1) {
             buffer.ReleaseTemporaryRT(fromId - 1);
             toId -= 5;
             for (i -= 1; i > 0; i--) {
                   buffer.SetGlobalTexture(_Source2Id, toId + 1);
                   Draw(fromId, toId, combinePass);// Pass.BloomCombine);
                   buffer.ReleaseTemporaryRT(fromId);
                   buffer.ReleaseTemporaryRT(toId + 1);
                   fromId = toId;
                   toId -= 2;
             }
        }else {
           buffer.ReleaseTemporaryRT(bloomPyramidId);
       }
        buffer.SetGlobalFloat(bloomIntensityId, finalIntensity);
        buffer.SetGlobalTexture(_Source2Id, sourceId);//
        Draw(fromId, BuiltinRenderTextureType.CameraTarget, finalPass);// Pass.BloomCombine);
        buffer.ReleaseTemporaryRT(fromId);
        buffer.EndSample("Bloom Up");
    }
    
    void Draw (RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass) {
        buffer.SetGlobalTexture(_SourceId, from);
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, _Settings.Material, (int)pass, MeshTopology.Triangles, 3);
    }
    
    public void Setup (ScriptableRenderContext context, Camera camera,bool useHDR) {
        _Context = context;
        _Camera = camera;
        _UseHDR = useHDR;
    }
    public void Render (int sourceId)
    {
#if UNITY_EDITOR
        DrawGizmosBeforeFX();
#endif
        DoBloom(sourceId);
        _Context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
#if UNITY_EDITOR
        DrawGizmosAfterFX();
#endif
    }
    #region Gizmos
#if UNITY_EDITOR
    private void DrawGizmosBeforeFX () {
        if (Handles.ShouldRenderGizmos()) {
            _Context.DrawGizmos(_Camera, GizmoSubset.PreImageEffects);
        }
    }

    private void DrawGizmosAfterFX () {
        if (Handles.ShouldRenderGizmos()) {
            _Context.DrawGizmos(_Camera, GizmoSubset.PostImageEffects);
        }
    }
#endif
    #endregion
}