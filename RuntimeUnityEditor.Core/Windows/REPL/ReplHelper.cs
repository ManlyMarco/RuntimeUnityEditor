using System.Collections;
using UnityEngine;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

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
            return this.AbstractStartCoroutine(i);
        }

        public void EndCoroutine(Coroutine c)
        {
            StopCoroutine(c);
        }
    }
}