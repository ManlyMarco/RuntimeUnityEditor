using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.ObjectTree;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using RuntimeUnityEditor.Core.Utils.ObjectDumper;
using UnityEngine;
using Component = UnityEngine.Component;

namespace RuntimeUnityEditor.Core.Inspector
{
    public sealed partial class Inspector
    {
        private class InspectorTab
        {
            private readonly List<ICacheEntry> _fieldCache = new List<ICacheEntry>();
            private InspectorStackEntryBase _currentStackItem;
            public InspectorStackEntryBase CurrentStackItem
            {
                get => _currentStackItem ?? (InspectorStack.Count > 0 ? _currentStackItem = InspectorStack.Peek() : null);
                set
                {
                    _currentStackItem = value;
                    LoadStackEntry(_currentStackItem);
                }
            }

            public IList<ICacheEntry> FieldCache => _fieldCache;
            public Stack<InspectorStackEntryBase> InspectorStack { get; } = new Stack<InspectorStackEntryBase>();
            public Vector2 InspectorStackScrollPos { get; set; }

            public void Clear()
            {
                InspectorStack.Clear();
                _fieldCache.Clear();
            }

            public void Pop()
            {
                var popdItem = InspectorStack.Pop();

                if (_currentStackItem == null || _currentStackItem == popdItem)
                    _currentStackItem = InspectorStack.Peek();

                LoadStackEntry(_currentStackItem);
            }

            public void Push(InspectorStackEntryBase stackEntry)
            {
                if (CurrentStackItem != null)
                {
                    // Pop everything above the selected item
                    while (InspectorStack.Count > 0 && InspectorStack.Peek() != CurrentStackItem)
                    {
                        InspectorStack.Pop();
                    }
                }

                InspectorStack.Push(stackEntry);
                _currentStackItem = stackEntry;
                LoadStackEntry(stackEntry);
            }

            private void LoadStackEntry(InspectorStackEntryBase stackEntry)
            {
                switch (stackEntry)
                {
                    case InstanceStackEntry instanceStackEntry:
                        _fieldCache.Clear();
                        _fieldCache.AddRange(MemberCollector.CollectAllMembers(instanceStackEntry));
                        break;
                    case StaticStackEntry staticStackEntry:
                        _fieldCache.Clear();
                        _fieldCache.AddRange(MemberCollector.CollectStaticMembers(staticStackEntry));
                        break;
                    case null:
                        _fieldCache.Clear();
                        return;
                    default:
                        throw new InvalidEnumArgumentException(
                            "Invalid stack entry type: " + stackEntry.GetType().FullName);
                }
            }

            public void PopUntil(InspectorStackEntryBase item)
            {
                if (CurrentStackItem == item) return;
                while (CurrentStackItem != null && CurrentStackItem != item) InspectorStack.Pop();
                LoadStackEntry(CurrentStackItem);
            }
        }
    }
}