

#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;

public class ButtonAttribute:PropertyAttribute{
    public string label;
    public ButtonAttribute(string label){
        this.label = label;
    }
}



#if UNITY_EDITOR
[UnityEditor.CustomPropertyDrawer(typeof(ButtonAttribute))]
public class EditorButtonVisualize : UnityEditor.PropertyDrawer
{
    public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
    {
        label.text = (attribute as ButtonAttribute).label;
        var tmpWidth = 50;
        Rect ttPosition = new Rect(position.x,position.y,position.width - tmpWidth,position.height);
        UnityEditor.EditorGUI.LabelField(ttPosition,label);
       
        Rect buttonPosition = new Rect(position.x + position.width - tmpWidth,position.y,tmpWidth,position.height);
        if (GUI.Button(buttonPosition, "show")) {
            var targetType = this.fieldInfo.ReflectedType;
            var method = targetType.GetMethod(label.text,System.Reflection.BindingFlags.Instance| System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if(method!=null){
                method.Invoke(property.serializedObject.targetObject,new object[0]);
            }
        }
    }
}
#endif