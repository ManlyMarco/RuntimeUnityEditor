using System;
using System.Collections;
using System.Collections.Generic;

namespace RuntimeUnityEditor.Core.ObjectTree
{
    /// <summary>
    /// Based on OrderedSet from answer https://stackoverflow.com/a/17853085 by AndreasHassing and  George Mamaladze
    /// </summary>
    /// <inheritdoc />
    internal class OrderedSet<T> : ICollection<T>
    {
        private readonly IDictionary<T, LinkedListNode<T>> _mDictionary;
        private readonly LinkedList<T> _mLinkedList;

        public OrderedSet()
            : this(EqualityComparer<T>.Default)
        {
        }

        public OrderedSet(IEqualityComparer<T> comparer)
        {
            _mDictionary = new Dictionary<T, LinkedListNode<T>>(comparer);
            _mLinkedList = new LinkedList<T>();
        }

        public int Count => _mDictionary.Count;

        public virtual bool IsReadOnly => _mDictionary.IsReadOnly;

        void ICollection<T>.Add(T item)
        {
            AddLast(item);
        }

        public bool AddLast(T item)
        {
            if (_mDictionary.ContainsKey(item)) return false;

            var node = _mLinkedList.AddLast(item);
            _mDictionary.Add(item, node);
            return true;
        }

        public bool InsertSorted(T item, IComparer<T> comparer)
        {
            if (_mDictionary.ContainsKey(item)) return false;

            var currentNode = _mLinkedList.First;
            while (currentNode != null)
            {
                if (comparer.Compare(currentNode.Value, item) >= 0)
                {
                    _mLinkedList.AddBefore(currentNode, item);
                    break;
                }
                currentNode = currentNode.Next;
            }

            if (currentNode == null)
                currentNode = _mLinkedList.AddLast(item);

            _mDictionary.Add(item, currentNode);
            return true;
        }

        public void Clear()
        {
            _mLinkedList.Clear();
            _mDictionary.Clear();
        }

        public bool Remove(T item)
        {
            if (item == null) return false;
            var found = _mDictionary.TryGetValue(item, out var node);
            if (!found) return false;
            _mDictionary.Remove(item);
            _mLinkedList.Remove(node);
            return true;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _mLinkedList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool Contains(T item)
        {
            return item != null && _mDictionary.ContainsKey(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _mLinkedList.CopyTo(array, arrayIndex);
        }

        public void RemoveAll(Predicate<T> func)
        {
            var currentNode = _mLinkedList.First;
            while (currentNode != null)
            {
                var nextNode = currentNode.Next;
                if (func(currentNode.Value))
                {
                    _mDictionary.Remove(currentNode.Value);
                    _mLinkedList.Remove(currentNode);
                }
                currentNode = nextNode;
            }
        }
    }
}
