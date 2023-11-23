using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using UnityEngine;

namespace RuntimeUnityEditor.Core.REPL
{
    //todo redundant?
    public class ReplHelper : MonoBehaviour
    {
        public T Find<T>() where T : Object
        {
            return FindObjectOfType<T>();
        }

        public T[] FindAll<T>() where T : Object
        {
            return FindObjectsOfType<T>();
        }

        public Coroutine RunCoroutine(IEnumerator i)
        {
            return StartCoroutine(i.WrapToIl2Cpp());
        }

        public void EndCoroutine(Coroutine c)
        {
            StopCoroutine(c);
        }
    }
}