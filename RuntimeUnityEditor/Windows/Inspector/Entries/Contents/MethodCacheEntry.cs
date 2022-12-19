using System;
using System.Reflection;
using HarmonyLib;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    public class MethodCacheEntry : ICacheEntry
    {
        public MethodCacheEntry(object instance, MethodInfo methodInfo)
        {
            Instance = instance;
            MethodInfo = methodInfo ?? throw new ArgumentNullException(nameof(methodInfo));

            _name = FieldCacheEntry.GetMemberName(instance, methodInfo);
            _returnTypeName = MethodInfo.ReturnType.FullDescription();

            _text = string.Empty;
            var strGenerics = methodInfo.GetGenericArguments().Join(p => p.FullDescription(), ", ");
            if (strGenerics.Length > 0) _text += "<" + strGenerics + ">";
            var strParams = methodInfo.GetParameters().Join(p => p.ParameterType.FullDescription() + " " + p.Name, ", ");
            _text += "(" + strParams + ")";

            _content = new GUIContent(_name, methodInfo.GetFancyDescription());
        }

        public MethodInfo MethodInfo { get; }
        public object Instance { get; }

        private readonly string _name;
        private readonly string _text;
        private readonly string _returnTypeName;
        private readonly GUIContent _content;

        public string Name() => _name;

        public string TypeName() => _returnTypeName;

        public GUIContent GetNameContent() => _content;

        public object EnterValue() => throw new InvalidOperationException();

        public object GetValue() => _text;

        public void SetValue(object newValue) => throw new InvalidOperationException();

        public Type Type() => MethodInfo.ReturnType;

        public bool CanSetValue() => false;

        public bool CanEnterValue() => false;
    }
}