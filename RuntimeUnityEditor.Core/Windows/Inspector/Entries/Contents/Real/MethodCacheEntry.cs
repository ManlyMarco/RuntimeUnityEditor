using System;
using System.Reflection;
using HarmonyLib;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    /// <summary>
    /// Represents a method entry in the inspector.
    /// </summary>
    public class MethodCacheEntry : ICacheEntry
    {
        /// <summary>
        /// Creates a new instance of MethodCacheEntry.
        /// </summary>
        public MethodCacheEntry(object instance, MethodInfo methodInfo, Type owner)
        {
            Instance = instance;
            MethodInfo = methodInfo ?? throw new ArgumentNullException(nameof(methodInfo));
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));

            _name = FieldCacheEntry.GetMemberName(instance, methodInfo);
            _returnTypeName = MethodInfo.ReturnType.GetSourceCodeRepresentation();

            ParameterString = GetParameterPreviewString(methodInfo);

            _nameContent = new GUIContent(_name,null, methodInfo.GetFancyDescription());
        }

        internal static string GetParameterPreviewString(MethodBase methodInfo)
        {
            var parameterString = string.Empty;
            var strGenerics = methodInfo.GetGenericArgumentsSafe().Join(p => p.FullDescription(), ", ");
            if (strGenerics.Length > 0) parameterString += "<" + strGenerics + ">";
            var strParams = methodInfo.GetParameters().Join(p => p.ParameterType.FullDescription() + " " + p.Name, ", ");
            parameterString += "(" + strParams + ")";
            return parameterString;
        }

        /// <summary>
        /// MethodInfo for the method.
        /// </summary>
        public MethodInfo MethodInfo { get; }

        /// <summary>
        /// Checks if the method is declared in the owner type.
        /// </summary>
        public bool IsDeclared => Owner == MethodInfo.DeclaringType;

        /// <summary>
        /// The type of the owner of the method.
        /// </summary>
        public Type Owner { get; }

        /// <summary>
        /// The instance of the object that owns the method.
        /// </summary>
        public object Instance { get; }

        /// <summary>
        /// String representation of the method parameters for use in UI.
        /// </summary>
        public string ParameterString { get; }

        private readonly string _name;
        private readonly string _returnTypeName;
        private protected readonly GUIContent _nameContent;

        /// <summary>
        /// Name of the method.
        /// </summary>
        public string Name() => _name;

        /// <summary>
        /// Name of the method's return type for use in UI.
        /// </summary>
        public string TypeName() => _returnTypeName;
        
        /// <inheritdoc />
        public GUIContent GetNameContent() => _nameContent;

        /// <summary>
        /// Not supported for methods.
        /// </summary>
        public object EnterValue() => throw new InvalidOperationException();

        /// <summary>
        /// Not supported for methods.
        /// </summary>
        public object GetValue() => null;
        
        /// <summary>
        /// Not supported for methods.
        /// </summary>
        public void SetValue(object newValue) => throw new InvalidOperationException();

        /// <inheritdoc />
        public Type Type() => MethodInfo.ReturnType;

        /// <summary>
        /// Method's reflection info.
        /// </summary>
        public MemberInfo MemberInfo => MethodInfo;

        /// <summary>
        /// Not supported for methods.
        /// </summary>
        public bool CanSetValue() => false;
        
        /// <summary>
        /// Not supported for methods.
        /// </summary>
        public bool CanEnterValue() => false;

        /// <inheritdoc />
        public int ItemHeight { get; set; } = Inspector.InspectorRecordInitialHeight;
    }
}