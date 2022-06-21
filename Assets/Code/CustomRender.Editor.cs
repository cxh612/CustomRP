using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

public partial class CustomRender
{
    partial void PrepareBuffer ();
    partial void DrawGizmos ();//相机
    partial void DrawUnsupportedShaders();//错误Shader绘制
    partial void PrepareForSceneWindow ();//UI窗口
#if UNITY_EDITOR
    string SampleName { get; set; }
    static ShaderTagId[] legacyShaderTagIds = {
        new ShaderTagId("Always"),
        new ShaderTagId("ForwardBase"),
        new ShaderTagId("PrepassBase"),
        new ShaderTagId("Vertex"),
        new ShaderTagId("VertexLMRGBM"),
        new ShaderTagId("VertexLM")
    };
    private Material _ErrorMaterial;
    
    partial void DrawUnsupportedShaders () {
        if (_ErrorMaterial == null) {
            _ErrorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
        }
        var drawingSettings = new DrawingSettings(new ShaderTagId("Always"), new SortingSettings(_Camera)) {
            overrideMaterial = _ErrorMaterial
        };
        for (int i = 1; i < legacyShaderTagIds.Length; i++) {
            drawingSettings.SetShaderPassName(i, legacyShaderTagIds[i]);
        }
        
        var f = GetFiltering(RenderQueueRange.all);
        _Context.DrawRenderers(_CullingResults,ref drawingSettings,ref f);
    }
    partial void DrawGizmos(){
        if (Handles.ShouldRenderGizmos()) {
            _Context.DrawGizmos(_Camera, GizmoSubset.PreImageEffects);
            _Context.DrawGizmos(_Camera, GizmoSubset.PostImageEffects);
        }
    }
    /// <summary>
    /// UI 
    /// </summary>
    partial void PrepareForSceneWindow () {
        if (_Camera.cameraType == CameraType.SceneView) {
            ScriptableRenderContext.EmitWorldGeometryForSceneView(_Camera);
        }
    }
    
    partial void PrepareBuffer () {
        Profiler.BeginSample("Editor Only");
        SampleName = _Buffer.name = _Camera.name;
        Profiler.EndSample();
    }
#else
	const string SampleName = bufferName;
#endif
}
