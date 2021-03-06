﻿#pragma warning disable 0618 // ignore obsolete since this is the editor for said obsolete type
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

using com.spacepuppy;
using com.spacepuppy.Dynamic;
using com.spacepuppy.Scenario;
using com.spacepuppy.Utils;

using com.spacepuppyeditor.Base;

namespace com.spacepuppyeditor.Scenario
{

    [CustomEditor(typeof(i_SetValueOnEntity))]
    public class i_SetValueOnEntityInspector : SPEditor
    {

        private VariantReferencePropertyDrawer _variantDrawer = new VariantReferencePropertyDrawer();

        protected override void OnSPInspectorGUI()
        {
            this.serializedObject.Update();

            this.DrawDefaultInspectorExcept("_searchEntity", "_componentType", "_memberName", "_value", "_mode");
            this.DrawPropertyField("_searchEntity");
            this.DrawPropertyField("_componentType"); //uses the TypeReference PropertyDrawer

            var compTypeProp = this.serializedObject.FindProperty("_componentType");
            var memberProp = this.serializedObject.FindProperty("_memberName");
            var valueProp = this.serializedObject.FindProperty("_value");
            var modeProp = this.serializedObject.FindProperty("_mode");


            //SELECT MEMBER
            System.Reflection.MemberInfo selectedMember;
            memberProp.stringValue = SPEditorGUILayout.ReflectedPropertyField(EditorHelper.TempContent("Property", "The property on the target to set."),
                                                                              TypeReferencePropertyDrawer.GetTypeFromTypeReference(compTypeProp),
                                                                              memberProp.stringValue,
                                                                              out selectedMember);
            this.serializedObject.ApplyModifiedProperties();


            //MEMBER VALUE TO SET TO
            if (selectedMember != null)
            {
                var propType = DynamicUtil.GetInputType(selectedMember);
                var emode = modeProp.GetEnumValue<i_SetValue.SetMode>();
                if (emode == i_SetValue.SetMode.Toggle)
                {
                    //EditorGUILayout.LabelField(EditorHelper.TempContent(valueProp.displayName), EditorHelper.TempContent(propType.Name));
                    var evtp = VariantReference.GetVariantType(propType);
                    var cache = SPGUI.Disable();
                    EditorGUILayout.EnumPopup(EditorHelper.TempContent("Value"), evtp);
                    cache.Reset();
                }
                else
                {
                    if (DynamicUtil.TypeIsVariantSupported(propType))
                    {
                        //draw the default variant as the method accepts anything
                        _variantDrawer.RestrictVariantType = false;
                        _variantDrawer.ForcedObjectType = null;
                        var label = EditorHelper.TempContent("Value", "The value to set to.");
                        _variantDrawer.OnGUI(EditorGUILayout.GetControlRect(true, _variantDrawer.GetPropertyHeight(valueProp, label)), valueProp, label);
                    }
                    else
                    {
                        _variantDrawer.RestrictVariantType = true;
                        _variantDrawer.TypeRestrictedTo = propType;
                        _variantDrawer.ForcedObjectType = (TypeUtil.IsType(propType, typeof(UnityEngine.Object))) ? propType : null;
                        var label = EditorHelper.TempContent("Value", "The value to set to.");
                        _variantDrawer.OnGUI(EditorGUILayout.GetControlRect(true, _variantDrawer.GetPropertyHeight(valueProp, label)), valueProp, label);
                    }
                }

                if (com.spacepuppy.Dynamic.Evaluator.WillArithmeticallyCompute(propType))
                {
                    EditorGUILayout.PropertyField(modeProp);
                }
                else
                {
                    //modeProp.SetEnumValue(i_SetValue.SetMode.Set);
                    EditorGUI.BeginChangeCheck();
                    emode = (i_SetValue.SetMode)SPEditorGUILayout.EnumPopupExcluding(EditorHelper.TempContent(modeProp.displayName), emode, i_SetValue.SetMode.Decrement, i_SetValue.SetMode.Increment);
                    if (EditorGUI.EndChangeCheck())
                        modeProp.SetEnumValue(emode);
                }
            }
            else
            {
                modeProp.SetEnumValue(i_SetValue.SetMode.Set);
            }

            this.serializedObject.ApplyModifiedProperties();
        }

    }
}
