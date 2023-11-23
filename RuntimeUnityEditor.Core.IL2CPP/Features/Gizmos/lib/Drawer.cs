using System;
using System.Collections.Generic;
using RuntimeUnityEditor.Core.Gizmos.lib.Drawers;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Gizmos.lib
{
    public abstract class Drawer
    {
        private static Dictionary<Type, Drawer> typeToDrawer = null;

        public abstract int Draw(ref Vector3[] buffer, params object[] args);

        public Drawer()
        {

        }

        public static Drawer Get<T>() where T : class
        {
            //find all drawers
            if (typeToDrawer == null)
            {
                typeToDrawer = new Dictionary<Type, Drawer>();

                //add defaults
                typeToDrawer.Add(typeof(CubeDrawer), new CubeDrawer());
                typeToDrawer.Add(typeof(LineDrawer), new LineDrawer());
                typeToDrawer.Add(typeof(PolygonDrawer), new PolygonDrawer());
                typeToDrawer.Add(typeof(SquareDrawer), new SquareDrawer());
                typeToDrawer.Add(typeof(ArcDrawer), new ArcDrawer());

                ////find extras
                //var types = AccessTools.AllTypes();
                //foreach (Type type in types)
                //{
                //    if (type.IsAbstract)
                //    {
                //        continue;
                //    }
                //
                //    if (type.IsSubclassOf(typeof(Drawer)) && !typeToDrawer.ContainsKey(type))
                //    {
                //        try
                //        {
                //            Drawer value = (Drawer)Activator.CreateInstance(type);
                //            typeToDrawer[type] = value;
                //        }
                //        catch (Exception e)
                //        {
                //            Debug.LogError($"couldnt register drawer of type {type} because {e.Message}");
                //        }
                //    }
                //}
            }

            if (typeToDrawer.TryGetValue(typeof(T), out Drawer drawer))
            {
                return drawer;
            }
            else
            {
                return null;
            }
        }
    }
}
