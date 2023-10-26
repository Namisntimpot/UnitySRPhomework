using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public partial class PostFXStack
{
    partial void ApplySceneViewStat();

#if UNITY_EDITOR
    partial void ApplySceneViewStat()
    {
        // �����ǰ�����scene��ͼ����������ҹر��� showImageEffects���ص�����ջ.
        if(camera.cameraType == CameraType.SceneView && !SceneView.currentDrawingSceneView.sceneViewState.showImageEffects)
        {
            fxSettings = null;
        }
    }

#endif
}
