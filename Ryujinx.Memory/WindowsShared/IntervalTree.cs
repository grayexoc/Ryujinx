using Ryujinx.Common.Collections;
using System;
using System.Collections.Generic;

namespace Ryujinx.Memory.WindowsShared
{
    /// <summary>
    /// An Augmented Interval Tree based off of the "TreeDictionary"'s Red-Black Tree. Allows fast overlap checking of ranges.
    /// </summary>
    /// <typeparam name="K">Key</typeparam>
    /// <typeparam name="V">Value</typeparam>
    class IntervalTree<K, V> : IntrusiveRedBlackTreeImpl<IntervalTreeNode<K, V>> where K : IComparable<K>
    {
        private const int ArrayGrowthSize = 32;

        #region Public Methods

        /// <summary>
        /// Gets the values of the interval whose key is <paramref name="key"/>.
        /// </summary>
        /// <param name="key">Key of the node value to get</param>
        /// <param name="value">Value with the given <paramref name="key"/></param>
        /// <returns>True if the key is on the dictionary, false otherwise</returns>
        public bool TryGet(K key, out V value)
        {
            IntervalTreeNode<K, V> node = GetNode(key);

            if (node == null)
            {
                value = default;
                return false;
            }

            value = node.Value;
            return true;
        }

        /// <summary>
        /// Returns the start addresses of the intervals whose start and end keys overlap the given range.
        /// </summary>
        /// <param name="start">Start of the range</param>
        /// <param name="end">End of the range</param>
        /// <param name="overlaps">Overlaps array to place results in</param>
        /// <param name="overlapCount">Index to start writing results into the array. Defaults to 0</param>
        /// <returns>Number of intervals found</returns>
        public int Get(K start, K end, ref IntervalTreeNode<K, V>[] overlaps, int overlapCount = 0)
        {
            GetNodes(Root, start, end, ref overlaps, ref overlapCount);

            return overlapCount;
        }

        /// <summary>
        /// Adds a new interval into the tree whose start is <paramref name="start"/>, end is <paramref name="end"/> and value is <paramref name="value"/>.
        /// </summary>
        /// <param name="start">Start of the range to add</param>
        /// <param name="end">End of the range to insert</param>
        /// <param name="value">Value to add</param>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is null</exception>
        public void Add(K start, K end, V value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            BSTInsert(start, end, value, null, out _);
        }

        /// <summary>
        /// Removes a value from the tree, searching for it with <paramref name="key"/>.
        /// </summary>
        /// <param name="key">Key of the node to remove</param>
        /// <returns>Number of deleted values</returns>
        public int Remove(K key)
        {
            return Remove(GetNode(key));
        }

        /// <summary>
        /// Removes a value from the tree, searching for it with <paramref name="key"/>.
        /// </summary>
        /// <param name="nodeToDelete">Node to be removed</param>
        /// <returns>Number of deleted values</returns>
        public int Remove(IntervalTreeNode<K, V> nodeToDelete)
        {
            if (nodeToDelete == null)
            {
                return 0;
            }

            Delete(nodeToDelete);

            Count--;

            return 1;
        }

        /// <summary>
        /// Adds all the nodes in the dictionary into <paramref name="list"/>.
        /// </summary>
        /// <returns>A list of all values sorted by Key Order</returns>
        public List<V> AsList()
        {
            List<V> list = new List<V>();

            AddToList(Root, list);

            return list;
        }

        #endregion

        #region Private Methods (BST)

        /// <summary>
        /// Adds all values that are children of or contained within <paramref name="node"/> into <paramref name="list"/>, in Key Order.
        /// </summary>
        /// <param name="node">The node to search for values within</param>
        /// <param name="list">The list to add values to</param>
        private void AddToList(IntervalTreeNode<K, V> node, List<V> list)
        {
            if (node == null)
            {
                return;
            }

            AddToList(node.Left, list);

            list.Add(node.Value);

            AddToList(node.Right, list);
        }

        /// <summary>
        /// Retrieve the node reference whose key is <paramref name="key"/>, or null if no such node exists.
        /// </summary>
        /// <param name="key">Key of the node to get</param>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null</exception>
        /// <returns>Node reference in the tree</returns>
        private IntervalTreeNode<K, V> GetNode(K key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            IntervalTreeNode<K, V> node = Root;
            while (node != null)
            {
                int cmp = key.CompareTo(node.Start);
                if (cmp < 0)
                {
                    node = node.Left;
                }
                else if (cmp > 0)
                {
                    node = node.Right;
                }
                else
                {
                    return node;
                }
            }
            return null;
        }

        /// <summary>
        /// Retrieve all nodes that overlap the given start and end keys.
        /// </summary>
        /// <param name="start">Start of the range</param>
        /// <param name="end">End of the range</param>
        /// <param name="overlaps">Overlaps array to place results in</param>
        /// <param name="overlapCount">Overlaps count to update</param>
        private void GetNodes(IntervalTreeNode<K, V> node, K start, K end, ref IntervalTreeNode<K, V>[] overlaps, ref int overlapCount)
        {
            if (node == null || start.CompareTo(node.Max) >= 0)
            {
                return;
            }

            GetNodes(node.Left, start, end, ref overlaps, ref overlapCount);

            bool endsOnRight = end.CompareTo(node.Start) > 0;
            if (endsOnRight)
            {
                if (start.CompareTo(node.End) < 0)
                {
                    if (overlaps.Length >= overlapCount)
                    {
                        Array.Resize(ref overlaps, overlapCount + ArrayGrowthSize);
                    }

                    overlaps[overlapCount++] = node;
                }

                GetNodes(node.Right, start, end, ref overlaps, ref overlapCount);
            }
        }

        /// <summary>
        /// Propagate an increase in max value starting at the given node, heading up the tree.
        /// This should only be called if the max increases - not for rebalancing or removals.
        /// </summary>
        /// <param name="node">The node to start propagating from</param>
        private void PropagateIncrease(IntervalTreeNode<K, V> node)
        {
            K max = node.Max;
            IntervalTreeNode<K, V> ptr = node;

            while ((ptr = ptr.Parent) != null)
            {
                if (max.CompareTo(ptr.Max) > 0)
                {
                    ptr.Max = max;
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Propagate recalculating max value starting at the given node, heading up the tree.
        /// This fully recalculates the max value from all children when there is potential for it to decrease.
        /// </summary>
        /// <param name="node">The node to start propagating from</param>
        private void PropagateFull(IntervalTreeNode<K, V> node)
        {
            IntervalTreeNode<K, V> ptr = node;

            do
            {
                K max = ptr.End;

                if (ptr.Left != null && ptr.Left.Max.CompareTo(max) > 0)
                {
                    max = ptr.Left.Max;
                }

                if (ptr.Right != null && ptr.Right.Max.CompareTo(max) > 0)
                {
                    max = ptr.Right.Max;
                }

                ptr.Max = max;
            } while ((ptr = ptr.Parent) != null);
        }

        /// <summary>
        /// Insertion Mechanism for the interval tree. Similar to a BST insert, with the start of the range as the key.
        /// Iterates the tree starting from the root and inserts a new node where all children in the left subtree are less than <paramref name="start"/>, and all children in the right subtree are greater than <paramref name="start"/>.
        /// Each node can contain multiple values, and has an end address which is the maximum of all those values.
        /// Post insertion, the "max" value of the node and all parents are updated.
        /// </summary>
        /// <param name="start">Start of the range to insert</param>
        /// <param name="end">End of the range to insert</param>
        /// <param name="value">Value to insert</param>
        /// <param name="updateFactoryCallback">Optional factory used to create a new value if <paramref name="start"/> is already on the tree</param>
        /// <param name="outNode">Node that was inserted or modified</param>
        /// <returns>True if <paramref name="start"/> was not yet on the tree, false otherwise</returns>
        private bool BSTInsert(K start, K end, V value, Func<K, V, V> updateFactoryCallback, out IntervalTreeNode<K, V> outNode)
        {
            IntervalTreeNode<K, V> parent = null;
            IntervalTreeNode<K, V> node = Root;

            while (node != null)
            {
                parent = node;
                int cmp = start.CompareTo(node.Start);
                if (cmp < 0)
                {
                    node = node.Left;
                }
                else if (cmp > 0)
                {
                    node = node.Right;
                }
                else
                {
                    outNode = node;

                    if (updateFactoryCallback != null)
                    {
                        // Replace
                        node.Value = updateFactoryCallback(start, node.Value);

                        int endCmp = end.CompareTo(node.End);

                        if (endCmp > 0)
                        {
                            node.End = end;
                            if (end.CompareTo(node.Max) > 0)
                            {
                                node.Max = end;
                                PropagateIncrease(node);
                                RestoreBalanceAfterInsertion(node);
                            }
                        }
                        else if (endCmp < 0)
                        {
                            node.End = end;
                            PropagateFull(node);
                        }
                    }

                    return false;
                }
            }
            IntervalTreeNode<K, V> newNode = new IntervalTreeNode<K, V>(start, end, value, parent);
            if (newNode.Parent == null)
            {
                Root = newNode;
            }
            else if (start.CompareTo(parent.Start) < 0)
            {
                parent.Left = newNode;
            }
            else
            {
                parent.Right = newNode;
            }

            PropagateIncrease(newNode);
            Count++;
            RestoreBalanceAfterInsertion(newNode);
            outNode = newNode;
            return true;
        }

        /// <summary>
        /// Removes the value from the dictionary after searching for it with <paramref name="key">.
        /// </summary>
        /// <param name="key">Tree node to be removed</param>
        private void Delete(IntervalTreeNode<K, V> nodeToDelete)
        {
            IntervalTreeNode<K, V> replacementNode;

            if (LeftOf(nodeToDelete) == null || RightOf(nodeToDelete) == null)
            {
                replacementNode = nodeToDelete;
            }
            else
            {
                replacementNode = nodeToDelete.Predecessor;
            }

            IntervalTreeNode<K, V> tmp = LeftOf(replacementNode) ?? RightOf(replacementNode);

            if (tmp != null)
            {
                tmp.Parent = ParentOf(replacementNode);
            }

            if (ParentOf(replacementNode) == null)
            {
                Root = tmp;
            }
            else if (replacementNode == LeftOf(ParentOf(replacementNode)))
            {
                ParentOf(replacementNode).Left = tmp;
            }
            else
            {
                ParentOf(replacementNode).Right = tmp;
            }

            if (replacementNode != nodeToDelete)
            {
                nodeToDelete.Start = replacementNode.Start;
                nodeToDelete.Value = replacementNode.Value;
                nodeToDelete.End = replacementNode.End;
                nodeToDelete.Max = replacementNode.Max;
            }

            PropagateFull(replacementNode);

            if (tmp != null && ColorOf(replacementNode) == Black)
            {
                RestoreBalanceAfterRemoval(tmp);
            }
        }

        #endregion

        #region Private Methods (RBL)

        protected override void RotateLeft(IntervalTreeNode<K, V> node)
        {
            if (node != null)
            {
                base.RotateLeft(node);

                PropagateFull(node);
            }
        }

        protected override void RotateRight(IntervalTreeNode<K, V> node)
        {
            if (node != null)
            {
                base.RotateRight(node);

                PropagateFull(node);
            }
        }

        #endregion

        public bool ContainsKey(K key)
        {
            return GetNode(key) != null;
        }
    }

    /// <summary>
    /// Represents a node in the IntervalTree which contains start and end keys of type K, and a value of generic type V.
    /// </summary>
    /// <typeparam name="K">Key type of the node</typeparam>
    /// <typeparam name="V">Value type of the node</typeparam>
    class IntervalTreeNode<K, V> : IntrusiveRedBlackTreeNode<IntervalTreeNode<K, V>>
    {
        /// <summary>
        /// The start of the range.
        /// </summary>
        public K Start;

        /// <summary>
        /// The end of the range.
        /// </summary>
        public K End;

        /// <summary>
        /// The maximum end value of this node and all its children.
        /// </summary>
        public K Max;

        /// <summary>
        /// Value stored on this node.
        /// </summary>
        public V Value;

        public IntervalTreeNode(K start, K end, V value, IntervalTreeNode<K, V> parent)
        {
            Start = start;
            End = end;
            Max = end;
            Value = value;
            Parent = parent;
        }
    }
}
