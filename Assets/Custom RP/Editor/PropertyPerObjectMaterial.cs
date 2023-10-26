using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PropertyPerObjectMaterial : MonoBehaviour
{
    // ���������޸� Custom/UnLit shader �� _BaseColor ����
    // �ᵼ��SRP BatchingʧЧ.
    static int baseColorID = Shader.PropertyToID("_BaseColor");

    [SerializeField]   // ��ʹ��private��Ҳ����inspector�Ͽ�����
    Color baseColor = Color.white;

    static MaterialPropertyBlock materialPropertyBlock;    // ����renderer�Ĳ������Բ�������ĳ��MaterialPropertyBlock��ʵ����������һ���Ϳ�����.
                                                           // Start is called before the first frame update
    private void Awake()
    {
        OnValidate();
    }

    private void OnValidate()  // ���ÿ�α����ػ����޸ĵ�ʱ��������. ��editorģʽ�£���Ϊ����϶����ᱻ�޸ģ�����ֻ���ڿ�ͷ��Awake��ʱ�����һ�μ���.
    {
        if (materialPropertyBlock == null)
            materialPropertyBlock = new MaterialPropertyBlock();
        materialPropertyBlock.SetColor(baseColorID, baseColor);
        // ��ȡ�Լ���renderer(MeshRenderer), ���޸Ĳ�������.
        GetComponent<Renderer>().SetPropertyBlock(materialPropertyBlock);
    }
}
