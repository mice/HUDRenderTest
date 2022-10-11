
#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;

public class LabelAttribute:PropertyAttribute{
    public string label;
    public LabelAttribute(string label){
        this.label = label;
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomPropertyDrawer(typeof(LabelAttribute))]
public class EditorLabelVisualize : UnityEditor.PropertyDrawer
{
    public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
    {
        label.text = (attribute as LabelAttribute).label;
        UnityEditor.EditorGUI.PropertyField(position,property,label);
    }
}
#endif