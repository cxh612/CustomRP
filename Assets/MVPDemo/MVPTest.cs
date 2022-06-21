using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
public class MVPTest : MonoBehaviour
{
    private Camera _Camera;
    public Transform _Target;
    public Renderer _Render;
    public Transform _FlagTarget;
    // Start is called before the first frame update

    public Matrix4x4 GetScale(Vector3 scale)
    {
        Matrix4x4 S =  Matrix4x4.identity;
        S.m00 = scale.x;
        S.m11 = scale.y;
        S.m22 = scale.z;
        return S;
    }
    public Matrix4x4 GetTranslate(Vector3 position)
    {
        Matrix4x4 T =  Matrix4x4.identity;
        T.m03 = position.x;
        T.m13 = position.y;
        T.m23 = position.z;
        T.m33 = 1;
        return T;
    }

    public static Matrix4x4 GetRotate(Vector3 rotation)
    {
        float Deg2Rad = (float)Math.PI / 180f;
        
        Matrix4x4 Rx =  Matrix4x4.identity;
        float r = rotation.x*Deg2Rad;
        // x
        Rx.m11 = (float)Math.Cos(r);
        Rx.m12 = -(float)Math.Sin(r);
        
        Rx.m21 = (float)Math.Sin(r);
        Rx.m22 = (float)Math.Cos(r);
        /// 1,       0,       0,0
        /// 0,  Cos(x), -Sin(y),0
        //  0,  Sin(x), Cos(y),0,0
        //  0,       0,       0,0,1
        
        Matrix4x4 Ry =  Matrix4x4.identity;
        //y
        r = rotation.y*Deg2Rad;
        Ry.m00 = (float)Math.Cos(r);
        Ry.m02 = (float)Math.Sin(r);
        
        Ry.m20 = -(float)Math.Sin(r);
        Ry.m22 = (float)Math.Cos(r);
        //Cos(r),    0 , -Sin(r)  ,0
        //0     ,    1 ,       0  ,0
        //-Sin(r),   0 ,   Cos(r) ,0
        //0   ,      0 ,        0 ,1
        
        Matrix4x4 Rz =  Matrix4x4.identity;
        //z
        r = rotation.z*Deg2Rad;
        Rz.m00 = (float)Math.Cos(r);
        Rz.m01 = -(float)Math.Sin(r);
        
        Rz.m10 = (float)Math.Sin(r);
        Rz.m11 = (float)Math.Cos(r);
        //Cos(r),  -Sin(r),   0  ,0
        //Sin(r),   Cos(r),   0  ,0
        //0     ,        0,   1  ,0
        //0     ,        0,   0  ,1
        return  Rx * Ry* Rz;
    }

    private Matrix4x4 GetTRS(Transform tf)
    {
        Matrix4x4 T = GetTranslate(tf.transform.localPosition);
        Matrix4x4 R = GetRotate(tf.transform.localEulerAngles);
       // Matrix4x4 R = Rotate(tf.transform.localEulerAngles);
       Matrix4x4 S = GetScale(tf.transform.localScale);
        
        Matrix4x4 Custom = T * R * S;//*Matrix4x4.Rotate(tf.localRotation)*Matrix4x4.Scale(tf.localScale);
        Matrix4x4 TRS = Matrix4x4.TRS(tf.localPosition,tf.localRotation,tf.localScale);//tf.localToWorldMatrix;
        
        // Debug.Log("-------TRS-------");
        // Debug.Log(TRS);
        // Debug.Log("-------localToWorldMatrix-------");
        // Debug.Log(tf.localToWorldMatrix);
        // Debug.Log("-------Custom-------");
        // Debug.Log(Custom);
        return TRS;
    }

    public Matrix4x4 GetView(Vector3 cameraPosWS,Vector3 rotation)
    {
        //Vector3 cameraPosWS = tf.localPosition;
        Matrix4x4 V = Matrix4x4.identity;
        V.m03 = -cameraPosWS.x;
        V.m13 = -cameraPosWS.y;
        V.m23 = cameraPosWS.z;
        V.m22 = -1;

        Matrix4x4 R = GetRotate(rotation);
        //return _Camera.worldToCameraMatrix;
        return V*R;
    }
    
    public static Matrix4x4 GetOrtho(float l, float r, float b, float t, float n, float f)
    {
        Matrix4x4 tm = Matrix4x4.identity;//平移
        tm.m03 = -(r + l) / 2;
        tm.m13 = -(t + b) / 2;
        tm.m23 = f;
        
        Matrix4x4 sm = Matrix4x4.identity;//缩放部分
        sm.m00 = 2.0f / (r - l);
        sm.m11 = -2.0f / (t - b);
        sm.m22 = 1.0f / (f - n);
        return sm*tm;
    }

    public Matrix4x4 GetProject(Camera camera)
    {
        //https://www.csdn.net/tags/OtDaAg5sNDI4MzYtYmxvZwO0O0OO0O0O.html
        float aspect = camera.aspect;
        float fov = camera.fieldOfView;
        
        float n = camera.nearClipPlane;
        float f = camera.farClipPlane;

        if (camera.orthographic)
        {
            float halfSize = camera.orthographicSize;
            float t = halfSize;
            float b = -halfSize;
            halfSize = camera.orthographicSize*aspect;
            float l = -halfSize;
            float r = halfSize;
            return GetOrtho(l,r,b,t,n,f);
        }
        else
        {
            Matrix4x4 pt = Matrix4x4.identity;
            pt.m00 = n;
            pt.m11 = n;
            pt.m22 = n + f;
            pt.m23 = -n * f;
            pt.m32 = 1;

            float t =2* (float)Math.Tan(fov / 2.0f)*Math.Abs(n);
            float b = -t;

            float r = t * aspect;
            float l = -r;

            Matrix4x4 o = GetOrtho(l,r,b,t,n,f);
            return pt*o;
        }
    }

    // Update is called once per frame
    public void Render(Camera camera,CommandBuffer cmd)
    {
     //   if (camera.cameraType != CameraType.Game) return;
        _Camera = camera;
        Matrix4x4 M = GetTRS(_Render.transform);
        //Debug.Log("=========M=============");
        //Debug.Log(M);
        Matrix4x4 V =  GetView(_Camera.transform.localPosition,_Camera.transform.localEulerAngles);
        //Debug.Log("=========V=============");
        //Debug.Log(_Camera.worldToCameraMatrix);
        //Debug.Log(V);
        Matrix4x4 P =  GetProject(_Camera);// Matrix4x4.Perspective(_Camera.fieldOfView, _Camera.aspect, _Camera.nearClipPlane, _Camera.farClipPlane);
        
        //Debug.Log("=========P1=============");
        //Debug.Log(P);
        P = GetGPUProjectionMatrix(P, false);
        // Debug.Log("=========P2=============");
        // Debug.Log(P);
         
        //P = _Camera.projectionMatrix;
        Matrix4x4 MVP = P*V*M;
        Matrix4x4 VP = P*V;
        
        cmd.SetGlobalMatrix("MVP",MVP);
        cmd.SetGlobalMatrix("VP",VP);
        if (_Camera.cameraType == CameraType.Game)
        {
            cmd.SetGlobalInt("IsGame",1);
        }
        else
        {
            cmd.SetGlobalInt("IsGame",0);
        }

        //_Render.material.SetMatrix("MVP", MVP);
        //_Render.material.SetMatrix("VP", VP);
        Vector4 p = new Vector4(0, 0, 0, 1);
        p = MVP * p;
        p = p / p.w;
       // Debug.Log(p);
       // Debug.Log((p.y+1)/2);
    }
    private static Matrix4x4 GetGPUProjectionMatrix(Matrix4x4 mat, bool renderToTexture)
    {
        // GL.GetGPUProjectionMatrix(_Camera.projectionMatrix, false);
        Matrix4x4 P = mat;
        bool d3d = SystemInfo.graphicsDeviceVersion.IndexOf("Direct3D") > -1;
        if (d3d)
        {
            // Invert Y for rendering to a render texture
            if (renderToTexture)
            {
                for (int i = 0; i < 4; i++)
                {
                    P[1, i] = -P[1, i];
                }
            }
            // Scale and bias from OpenGL -> D3D depth range
            for (int i = 0; i < 4; i++)
            {
                P[2, i] = P[2, i] * 0.5f + P[3, i] * 0.5f;
            }
        }
        return P;
    }
    //实际方法，执行效率更高，难以理解
    public static Matrix4x4 Rotate(Vector3 r) {
        float Deg2Rad = (float)Math.PI / 180f;
        float radX = r.x * Deg2Rad;
        float radY = r.y * Deg2Rad;
        float radZ = r.z * Deg2Rad;
        float sinX = (float)Math.Sin(radX);
        float cosX = (float)Math.Cos(radX);
        float sinY = (float)Math.Sin(radY);
        float cosY = (float)Math.Cos(radY);
        float sinZ = (float)Math.Sin(radZ);
        float cosZ = (float)Math.Cos(radZ);

        Matrix4x4 matrix = new Matrix4x4();
        matrix.SetColumn(0, new Vector4(
            cosY * cosZ,
            cosX * sinZ + sinX * sinY * cosZ,
            sinX * sinZ - cosX * sinY * cosZ,
            0f
        ));
        matrix.SetColumn(1, new Vector4(
            -cosY * sinZ,
            cosX * cosZ - sinX * sinY * sinZ,
            sinX * cosZ + cosX * sinY * sinZ,
            0f
        ));
        matrix.SetColumn(2, new Vector4(
            sinY,
            -sinX * cosY,
            cosX * cosY,
            0f
        ));
        matrix.SetColumn(3, new Vector4(0f, 0f, 0f, 1f));
        return matrix;
    }
}
