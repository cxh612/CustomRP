using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Serialization;
[System.Serializable]
public struct Directional {
    public TextureSize atlasSize;
}
[System.Serializable]
public class ShadowSettings {
    [Min(0f)]
    public TextureSize ShadowTextureSize = TextureSize._2048;
    // public Directional directional = new Directional {
    //     atlasSize = TextureSize._1024
    // };
    [Min(0)]
    public int MaxDistance=100;
    [Range(0.001f, 1f)]public float ShadowDistanceFade = 0.1f;
    [FormerlySerializedAs("cascadeFade")] public float CascadeFade = 0.1f;
    public float ShadowNearPlane = 0;
    [Header("Cascades")] 
    [Range(1,4)]public int shadowCascades=4;
    [Range(0,1)]public float CascadeRatiosLevel1 = 0.1f;
    [Range(0,1)]public float CascadeRatiosLevel2 = 0.25f;
    [Range(0,1)]public float CascadeRatiosLevel3 = 0.5f;
    private Vector3 _CascadeRatios = new Vector3();
    
    public CascadeBlendMode cascadeBlend;
    public Vector3 CascadeRatios
    {
        get
        {
            _CascadeRatios.x = CascadeRatiosLevel1;
            _CascadeRatios.y = CascadeRatiosLevel2;
            _CascadeRatios.z = CascadeRatiosLevel3;
            return _CascadeRatios;
        }
    }
    public bool CascadeDebug = false;
    public ShadowFilterMode Filter;

    public bool UseShadowMask;
}
public enum ShadowFilterMode {
    PCF2x2, PCF3x3, PCF5x5, PCF7x7
}
public enum CascadeBlendMode {
    Hard, Soft, Dither
}
public enum TextureSize {
    _256 = 256, _512 = 512, _1024 = 1024,
    _2048 = 2048, _4096 = 4096, _8192 = 2028
}
public class Shadow
{
    [SerializeField]
    private ShadowSettings shadows = default;
    struct ShadowedDirectionalLight {
        public int visibleLightIndex;
        public float slopeScaleBias;//灯光参数 light.shadowBias
    }
    private int _ShadowedDirectionalLightCount;
    private const int maxShadowedDirectionalLightCount=4;
    private ShadowedDirectionalLight[] ShadowedDirectionalLights = new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];
    
    const string bufferName = "Shadows";
    private CommandBuffer buffer = new CommandBuffer {
        name = bufferName
    };
    
    private ScriptableRenderContext context;
    private CullingResults cullingResults;
    private ShadowSettings settings;
    private int _CascadeCount=4;
    private Matrix4x4[] _ShadowMatrixs;
    #region ShaderID
    private static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");
    private static int _ShaderMatsId = Shader.PropertyToID("_ShadowMats");
    private int _CascadeCountId = Shader.PropertyToID("_CascadeCount");
    private int _CascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres");
    private int _ShadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");
    private int _CascadeDatasId = Shader.PropertyToID("_CascadeDatas");
    private int _ShadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize");
    
    private static int _ShadowDistanceId = Shader.PropertyToID("_ShadowDistance");
   
    #endregion
    private float _ShadowDistanceFade = 0.1f;
    static Vector4[] _CascadeCullingSpheres = new Vector4[4];
    static Vector4[] _CascadeDatas = new Vector4[4];
    // private Vector3 _CascadeRatios;
    private int _AtlasSize;
    private bool _UseShadowMask;
    static string[] _DirectionalFilterKeywords = {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7",
    };
    static string[] cascadeBlendKeywords = {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };
    static string[] shadowMaskKeywords = {
        "_SHADOW_MASK_ALWAYS" ,
        "_SHADOW_MASK_DISTANCE" 
    }; 
    void SetKeywords (string[] keywords, int enabledIndex) {
        //int enabledIndex = (int)settings.directional.filter - 1;
        for (int i = 0; i < keywords.Length; i++) {
            if (i == enabledIndex) {
                buffer.EnableShaderKeyword(keywords[i]);
            }
            else {
                buffer.DisableShaderKeyword(keywords[i]);
            }
        }
    }
    private void SetKeywords()
    {

        SetKeywords(_DirectionalFilterKeywords, (int)settings.Filter - 1);
        SetKeywords(cascadeBlendKeywords, (int)settings.cascadeBlend - 1);
        if (settings.CascadeDebug)
        {
            buffer.EnableShaderKeyword("DEBUG_CASCADE");
        }
        else
        {
            buffer.DisableShaderKeyword("DEBUG_CASCADE");
        }

        int idx= QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1;
        for (int i = 0; i < shadowMaskKeywords.Length; i++)
        {
            buffer.DisableShaderKeyword(shadowMaskKeywords[i]);
        }
        if (_UseShadowMask)
        {
            buffer.EnableShaderKeyword(shadowMaskKeywords[idx]);
        }
    }

    public void Setup (ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings settings)
    {
        _UseShadowMask = false;
        this.settings = settings;
        this.context = context;
        this.cullingResults = cullingResults;
        _ShadowMatrixs = new Matrix4x4[maxShadowedDirectionalLightCount * _CascadeCount];
        _AtlasSize = (int)settings.ShadowTextureSize;
        _ShadowedDirectionalLightCount = 0;
        _ShadowDistanceFade = settings.ShadowDistanceFade;
        _CascadeCount = settings.shadowCascades;
    }
    
    public void Cleanup () {
        buffer.ReleaseTemporaryRT(dirShadowAtlasId);
        ExecuteBuffer();
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="light"></param>
    /// <param name="visibleLightIndex"></param>
    /// <returns>
    /// x :Shadow strength.
    /// y :Shadew tile index.
    /// z :灯光参数 Normal Bias
    /// </returns>
    public  Vector4  ReserveDirectionalShadows (Light light, int visibleLightIndex) {
        if (_ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount && light.shadows != LightShadows.None && light.shadowStrength > 0f ){//&&cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b)
            LightBakingOutput lightBaking = light.bakingOutput;
            float maskChannel = -1;
            if (lightBaking.lightmapBakeType == LightmapBakeType.Mixed && lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask) {
                _UseShadowMask = true ;
                maskChannel = lightBaking.occlusionMaskChannel;
            }
            
            if (!cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b)) {//backmask依旧显示
                return new Vector4(-light.shadowStrength, 0f, 0f,maskChannel);
            }
            
            ShadowedDirectionalLights[_ShadowedDirectionalLightCount] = new ShadowedDirectionalLight {visibleLightIndex = visibleLightIndex,slopeScaleBias = light.shadowBias};
            return new Vector4(light.shadowStrength, settings.shadowCascades *_ShadowedDirectionalLightCount++,light.shadowNormalBias,maskChannel);
        }
        return new Vector4(0f, 0f, 0f, -1f);
    }
    
    void ExecuteBuffer () {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    private int GetSplit()
    {
        int tiles = _ShadowedDirectionalLightCount * _CascadeCount;
        if (tiles == 1)
        {
            return 1;//1x1
        }else if (tiles <= 4)
        {
            return 2;//2x2
        }
        else
        {
            return 4;//4x4 最多支持4x4
        }
        // return tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
    }

    private int GetTileSize()
    {
        int split = GetSplit();
        return _AtlasSize / split;
    }

    Rect GetTileRect (int index,out Vector4 offset)
    {
        int split = GetSplit();
        int tileSize = GetTileSize();
        offset = new Vector4(index % split, index / split,1.0f/split,1.0f/split);
        return new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize);
    }
    
    void RenderSingleLightShadows(int index)
    {
        ShadowedDirectionalLight light = ShadowedDirectionalLights[index];
        var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
        int tileOffset = index * settings.shadowCascades;// _CascadeCount;
        Vector3 ratios = settings.CascadeRatios;//  _CascadeRatios;
        int tileSize = GetTileSize();
        
        for (int i = 0; i < _CascadeCount; i++)
        {
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(light.visibleLightIndex, 
                i, _CascadeCount, ratios, tileSize,settings.ShadowNearPlane,
                out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
                out ShadowSplitData splitData
            );
            
            if (index == 0) {
                SetCascadeData(i, splitData.cullingSphere, tileSize);
            }
            
            shadowSettings.splitData = splitData;
            int tileIndex = tileOffset + i;
            Rect viewRect = GetTileRect(tileIndex, out Vector4 offset);
            buffer.SetViewport(viewRect);
            buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            buffer.SetGlobalVectorArray(_CascadeDatasId,_CascadeDatas);
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            context.DrawShadows(ref shadowSettings);
            buffer.SetGlobalDepthBias(0, 0f);
            //ExecuteBuffer();
            Matrix4x4 VP  = projectionMatrix * viewMatrix;
            _ShadowMatrixs[tileIndex]=ConvertToAtlasMatrix(VP,ref offset);
        }
    }
    void SetCascadeData (int index, Vector4 cullingSphere, float tileSize) {
        float texelSize = 2f * cullingSphere.w / tileSize;
        float filterSize = texelSize * ((float)settings.Filter + 1f);
        cullingSphere.w -= filterSize;
        cullingSphere.w *= cullingSphere.w;         //这可以通过比较球体中心的平方距离与其平方半径来完成。所以让我们存储正方形半径，这样我们就不必在着色器中计算它。
        _CascadeCullingSpheres[index] = cullingSphere;
        _CascadeDatas[index] = new Vector4(1f / cullingSphere.w, filterSize * 1.4142136f);//√2 
       // _CascadeDatas[index].x = 1f / cullingSphere.w;
    }
    void RenderDirectionalShadows ()
    {
        //创建渲染目标
        buffer.GetTemporaryRT(dirShadowAtlasId, _AtlasSize, _AtlasSize,32,FilterMode.Bilinear,RenderTextureFormat.Depth);
        buffer.SetRenderTarget(dirShadowAtlasId,RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.ClearRenderTarget(true, false, Color.clear);
        buffer.BeginSample(bufferName);
        ExecuteBuffer();
        
        for (int i = 0; i < _ShadowedDirectionalLightCount; i++)
        {
            RenderSingleLightShadows(i);
        }
        buffer.SetGlobalMatrixArray(_ShaderMatsId,_ShadowMatrixs);
        buffer.SetGlobalInt(_CascadeCountId, _CascadeCount);
        float f = 1f - settings.CascadeFade;
        buffer.SetGlobalVector( _ShadowDistanceFadeId , new Vector4(1.0f/ (float)settings.MaxDistance, 1.0f /(float) _ShadowDistanceFade,1f / (1f - f * f)));
        buffer.SetGlobalVectorArray(_CascadeCullingSpheresId, _CascadeCullingSpheres);
        SetKeywords();
        // buffer.SetGlobalDepthBias(0,0);
        buffer.SetGlobalVector(_ShadowAtlasSizeId, new Vector4(_AtlasSize, 1f / _AtlasSize,0,0));
        buffer.SetGlobalFloat(_ShadowDistanceId, settings.MaxDistance);
        buffer.EndSample(bufferName);
        ExecuteBuffer();
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="m"> VP 矩阵</param>
    /// <param name="offset">偏移</param>
    /// <returns></returns>
    private Matrix4x4 ConvertToAtlasMatrix (Matrix4x4 m,ref Vector4 offset) {
        if (SystemInfo.usesReversedZBuffer) {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }
       
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * offset.z;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * offset.z;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * offset.z;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * offset.z;
        
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * offset.w;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * offset.w;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * offset.w;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * offset.w;
        
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);
        return m;
    }
    public void Render()
    {
        if (_ShadowedDirectionalLightCount > 0)
        {
            RenderDirectionalShadows();
        }
        else {
            //缺少纹理的情况下使用我们的着色器加载材质会出问题，因为与阴影采样器不兼容的默认纹理。
            //方法1.可以通过引入着色器关键字来生成省略阴影采样代码的着色器变体来避免这种情况。
            //方法2.在不需要阴影时，采用 1×1 虚拟纹理，从而避免额外的着色器变体。（当前采用方法2）
            buffer.GetTemporaryRT(
                dirShadowAtlasId, 1, 1,
                32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap
            );
        }
    }
}
