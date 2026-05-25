using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public interface IUIDrawTarget
{
    void DoGenerate(IUIData meshData, Transform root = null);//UIMeshData
}