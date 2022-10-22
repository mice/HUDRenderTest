using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;


public partial class UIPrefabRegistration
{
    public UIText SetText(int index, string text)
    {
        if (index < draws.Length && draws[index] is UIText uiText)
        {
            uiText.text = text;
            return uiText;
        }
        return null;
    }

    public UIImage SetSprite(int index, Sprite sprite)
    {
        if (index < draws.Length && draws[index] is UIImage uiImg)
        {
            uiImg.sprite = sprite;
            if (sprite == null)
            {
                TextureCall(sprite.texture);
            }
           
            return uiImg;
        }
        return null;
    }

    public IUIDrawTarget SetWidth(int index, int width)
    {
        var item = index < draws.Length ? owner.targets[index] : null;
        if (item is RectTransform rtm)
        {
            rtm.sizeDelta = new Vector2(width, rtm.sizeDelta.y);
            return draws[index];
        }
        return null;
    }
}
