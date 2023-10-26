using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PropertyPerObjectMaterial : MonoBehaviour
{
    // 仅仅用于修改 Custom/UnLit shader 的 _BaseColor 属性
    // 会导致SRP Batching失效.
    static int baseColorID = Shader.PropertyToID("_BaseColor");

    [SerializeField]   // 即使是private，也能在inspector上看到它
    Color baseColor = Color.white;

    static MaterialPropertyBlock materialPropertyBlock;    // 设置renderer的材质属性不依赖于某个MaterialPropertyBlock的实例，所以用一个就可以了.
                                                           // Start is called before the first frame update
    private void Awake()
    {
        OnValidate();
    }

    private void OnValidate()  // 组件每次被加载或者修改的时候会调用它. 非editor模式下，因为组件肯定不会被修改，所以只用在开头在Awake的时候调用一次即可.
    {
        if (materialPropertyBlock == null)
            materialPropertyBlock = new MaterialPropertyBlock();
        materialPropertyBlock.SetColor(baseColorID, baseColor);
        // 获取自己的renderer(MeshRenderer), 并修改材质属性.
        GetComponent<Renderer>().SetPropertyBlock(materialPropertyBlock);
    }
}
