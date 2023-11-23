using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.Runtime;
using UnityEngine;

namespace RuntimeUnityEditor.Core.IL2CPP.Utils
{
    internal static class IL2CppExtensions
    {
        public static void Set(this RectOffset obj, int left, int right, int top, int bottom)
        {
            obj.left = left;
            obj.right = right;
            obj.top = top;
            obj.bottom = bottom;
        }


        /*
        public static object? TryCast(this Il2CppObjectBase @this, Type type)
        {
            IntPtr nativeClassPtr = Il2CppClassPointerStore.GetNativeClassPointer(type);
            if (nativeClassPtr == IntPtr.Zero)
            {
                DefaultInterpolatedStringHandler interpolatedStringHandler = new DefaultInterpolatedStringHandler(32, 1);
                interpolatedStringHandler.AppendFormatted(type);
                interpolatedStringHandler.AppendLiteral(" is not an Il2Cpp reference type");
                throw new ArgumentException(interpolatedStringHandler.ToStringAndClear());
            }
            IntPtr num = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(@this.Pointer);
            if (!Il2CppInterop.Runtime.IL2CPP.il2cpp_class_is_assignable_from(nativeClassPtr, num))
                return null;


            if(!RuntimeSpecificsStore.IsInjected(num))
                return Il2CppObjectBase.InitializerStore<object>.Initializer(@this.Pointer);

            return RuntimeSpecificsStore.IsInjected(num) && ClassInjectorBase.GetMonoObjectFromIl2CppPointer(@this.Pointer) is T fromIl2CppPointer ? fromIl2CppPointer : Il2CppObjectBase.InitializerStore<T>.Initializer(@this.Pointer);
        }*/
    }
}
