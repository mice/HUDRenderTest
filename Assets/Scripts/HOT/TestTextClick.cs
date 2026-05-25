using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TestTextClick : MonoBehaviour, IPointerClickHandler
{
    public Text text;
    public Font font;
    public Texture2D texture2D;
    public Texture fontTexture;
    private RectTransform _rectTransform;
    private List<UIVertex> vertexs = new List<UIVertex>();

    private int t_width = 3;
    public RawImage rawImg;

    private Vector2Int textureSize;

    [Button("InitText")]
    public string _x;
    void Start()
    {
        this._rectTransform = (RectTransform)transform;
        if (text != null)
        {
            InitText();
        }
    }

    void InitText()
    {
        font = text.font;
        var texture = font.material.GetTexture("_MainTex");
        fontTexture = texture;
        if(texture is Texture2D t2d)
        {
            if (texture2D == null || texture2D.width!= t_width)
            {
                texture2D = new Texture2D(t_width, t_width, TextureFormat.Alpha8, false);
            }
            texture2D.Apply();
            textureSize = new Vector2Int(t2d.width, t2d.height);
            
            Graphics.CopyTexture(texture, 0, 0, 0, 0, t_width, t_width, texture2D, 0, 0, 0, 0);
            if (rawImg != null)
            {
                rawImg.texture = texture2D;
            }
        }
    }


    public void OnPointerClick(PointerEventData eventData)
    {
        Vector2 lp;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform, eventData.position, eventData.pressEventCamera, out lp);
       
        OnClickAt(lp);
    }

    virtual protected void OnClickAt(Vector2 lp)
    {
        vertexs.Clear();
        text.cachedTextGenerator.GetVertices(vertexs);
        if (vertexs.Count == 4)
        {
            var contains = Contains(lp,vertexs);
            UnityEngine.Debug.LogError($":::Contains:{contains}");
         
            if (!(contains))
            {
                text.color =  Color.white;
                return;
            }

            float x_ratio = Mathf.InverseLerp(vertexs[0].position.x, vertexs[1].position.x, lp.x);
            float y_ratio = Mathf.InverseLerp(vertexs[0].position.y, vertexs[3].position.y, lp.y);

            float uv_x = Mathf.Lerp(vertexs[0].uv0.x, vertexs[1].uv0.x, x_ratio);
            float uv_y = Mathf.Lerp(vertexs[0].uv0.y, vertexs[3].uv0.y, y_ratio);
          
            Vector2Int point = new Vector2Int(Mathf.RoundToInt(textureSize.x * uv_x),Mathf.RoundToInt(textureSize.y * uv_y));

            var hitted = IsHit(point);
          
            text.color = hitted ? Color.red : Color.white;
        }
        else
        {
            text.color = Color.white;
        }
    }

    /// <summary>
    /// 缺少边界修正
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    private bool IsHit(Vector2Int point)
    {
        var texture = font.material.GetTexture("_MainTex");
        var fixed_x = Mathf.Clamp(point.x - 1, 0, texture.width - 2);
        var fixed_y = Mathf.Clamp(point.y - 1, 0, texture.height - 2);
        Graphics.CopyTexture(texture, 0, 0, fixed_x, fixed_y, t_width, t_width, texture2D, 0, 0, 0, 0);

        var pixel = texture2D.GetPixels(0);
        var totalAlpha = 0.0f;
        foreach(var item in pixel)
        {
            totalAlpha += item.a;
        }
        return totalAlpha/pixel.Length > 0.5f;
    }

    /**
    * Return true if the given point is contained inside the boundary.
    * See: http://www.ecse.rpi.edu/Homepages/wrf/Research/Short_Notes/pnpoly.html
    * @param test The point to check
    * @return true if the point is inside the boundary, false otherwise
    *
    */
    public static bool Contains(Vector2 test, List<UIVertex> points)
    {
        int i;
        int j;
        bool result = false;
        for (i = 0, j = points.Count - 1; i < points.Count; j = i++)
        {
            if ((points[i].position.y > test.y) != (points[j].position.y > test.y) &&
                (test.x < (points[j].position.x - points[i].position.x) * (test.y - points[i].position.y) / (points[j].position.y - points[i].position.y) + points[i].position.x))
            {
                result = !result;
            }
        }
        return result;
    }
}
