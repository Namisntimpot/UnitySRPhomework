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
        // 如果当前相机是scene视图的相机，并且关闭了 showImageEffects，关掉后处理栈.
        if(camera.cameraType == CameraType.SceneView && !SceneView.currentDrawingSceneView.sceneViewState.showImageEffects)
        {
            fxSettings = null;
        }
    }

#endif
}
