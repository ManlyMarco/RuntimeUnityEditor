using System;
using System.Linq;
using System.Reflection;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    public class MethodCacheEntry : CacheEntryBase
    {
        public MethodCacheEntry(object ins, MethodInfo m) : base(GetMethodName(ins, m))
        {
            if (m == null)
                throw new ArgumentNullException(nameof(m));

            _instance = ins;
            MethodInfo = m;
        }

        private static string GetMethodName(object ins, MethodBase methodInfo)
        {
            if (methodInfo != null)
            {
                var name = FieldCacheEntry.GetMemberName(ins, methodInfo);

                var genericArguments = methodInfo.GetGenericArguments();
                if (genericArguments.Any())
                {
                    name += "<" + string.Join(", ", genericArguments.Select(x => x.Name).ToArray()) + ">";
                }

                return name;
            }
            return "INVALID";
        }

        public MethodInfo MethodInfo { get; }

        private readonly object _instance;
        private object _valueCache;

        public override object GetValueToCache()
        {
            return (_instance == null ? "Static " : "") + "Method call - enter to evaluate";
        }

        public override object GetValue()
        {
            return _valueCache ?? base.GetValue();
        }

        public override object EnterValue()
        {
            try
            {
                // If this is the first user clicked, eval the method and display the result. second time enter as normal
                if (_valueCache == null)
                {
                    var result = MethodInfo.Invoke(_instance, null);

                    _valueCache = result;
                    return null;
                }

                return _valueCache;
            }
            catch (Exception ex)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, $"Failed to evaluate the method {Name()} - {ex.Message}");
                _valueCache = ex;
                return null;
            }
        }

        protected override bool OnSetValue(object newValue)
        {
            return false;
        }

        public override Type Type()
        {
            return MethodInfo.ReturnType;
        }

        public override bool CanSetValue()
        {
            return false;
        }

        public override bool CanEnterValue()
        {
            return _valueCache == null || base.CanEnterValue();
        }
    }
}