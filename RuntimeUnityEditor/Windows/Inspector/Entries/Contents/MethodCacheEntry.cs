using System;
using System.Reflection;
using HarmonyLib;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    public class MethodCacheEntry : ICacheEntry
    {
        public MethodCacheEntry(object instance, MethodInfo methodInfo, Type owner)
        {
            Instance = instance;
            MethodInfo = methodInfo ?? throw new ArgumentNullException(nameof(methodInfo));
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));

            _name = FieldCacheEntry.GetMemberName(instance, methodInfo);
            _returnTypeName = MethodInfo.ReturnType.FullDescription();

            ParameterString = string.Empty;
            var strGenerics = methodInfo.GetGenericArguments().Join(p => p.FullDescription(), ", ");
            if (strGenerics.Length > 0) ParameterString += "<" + strGenerics + ">";
            var strParams = methodInfo.GetParameters().Join(p => p.ParameterType.FullDescription() + " " + p.Name, ", ");
            ParameterString += "(" + strParams + ")";

            _content = new GUIContent(_name, methodInfo.GetFancyDescription());
        }

        public MethodInfo MethodInfo { get; }
        public bool IsDeclared => Owner == MethodInfo.DeclaringType;
        public Type Owner { get; }
        public object Instance { get; }
        public string ParameterString { get; }
        private readonly string _name;
        private readonly string _returnTypeName;
        private readonly GUIContent _content;

        public string Name() => _name;

        public string TypeName() => _returnTypeName;

        public GUIContent GetNameContent() => _content;

        public object EnterValue() => throw new InvalidOperationException();

        public object GetValue() => null;

        public void SetValue(object newValue) => throw new InvalidOperationException();

        public Type Type() => MethodInfo.ReturnType;

        public bool CanSetValue() => false;

        public bool CanEnterValue() => false;
    }
}