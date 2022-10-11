using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestCopyRTexture : MonoBehaviour
{
    public Texture2D texture;
    public RenderTexture rTexture;
    // Start is called before the first frame update
    [Button("TestEqCopy")]
    public string _x;

    [Button("TestSubCopy")]
    public string _y;

    [Range(0,256)]
    public int offset_y;

    public void TestEqCopy()
    {
        Graphics.CopyTexture(texture, rTexture);
    }

    public void TestSubCopy()
    {
        var tmpInt = rTexture.width;
        Graphics.CopyTexture(texture,0,0,0, offset_y, tmpInt, tmpInt, rTexture,0,0,0,0);
    }
}
