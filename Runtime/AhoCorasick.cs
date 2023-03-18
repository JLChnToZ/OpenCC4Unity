using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;

namespace OpenCC.Unity.Utils {
    public class AhoCorasick<TKey, TValue> : IDictionary<IEnumerable<TKey>, TValue> {
        readonly TrieNode root;
        int version;
        bool isDirty;

        public ITrieNode Root => root;

        public IEqualityComparer<TKey> Comparer => root.Comparer;

        public int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)] private set;
        }

        bool ICollection<KeyValuePair<IEnumerable<TKey>, TValue>>.IsReadOnly => false;

        public ICollection<IEnumerable<TKey>> Keys => new SequenceCollection(this);

        public ICollection<TValue> Values => new ValueCollection(this);

        public TValue this[IEnumerable<TKey> sequence] {
            get {
                if (!TryGetValue(sequence, out var result))
                    throw new KeyNotFoundException("Specified sequence not found.");
                return result;
            }
            set => Add(sequence, value, true);
        }

        public AhoCorasick(): this(null) {}

        public AhoCorasick(IEqualityComparer<TKey> comparer) {
            root = new TrieNode(this, comparer ?? EqualityComparer<TKey>.Default);
        }

        public void Add(IEnumerable<TKey> sequence, TValue value) {
            if (!Add(sequence, value, false))
                throw new ArgumentException("Specified sequence already exists.", nameof(sequence));
        }

        bool Add(IEnumerable<TKey> sequence, TValue value, bool replace) {
            if (sequence == null) throw new ArgumentNullException(nameof(sequence));
            var current = root;
            foreach (var element in sequence)
                current = current.AddOrCreateChild(element);
            if (current.IsTip) {
                if (!replace) return false;
            } else Count++;
            current.Value = value;
            isDirty = true;
            version++;
            return true;
        }

        public bool Remove(IEnumerable<TKey> sequence) {
            if (sequence == null) throw new ArgumentNullException(nameof(sequence));
            var current = root;
            foreach (var element in sequence)
                if (!current.TryGetValue(element, out current))
                    return false;
            if (current == null) return false;
            var isTip = current.IsTip;
            if (isTip) Count--;
            current.IsTip = false;
            while (current != null && current != root) {
                if (current.IsTip || current.Count > 0) break;
                var parent = current.parent;
                parent.Remove(current.Key);
                current = parent;
            }
            isDirty = true;
            version++;
            return isTip;
        }

        public bool Contains(IEnumerable<TKey> sequence) => TryGetValue(sequence, out var _);

        bool IDictionary<IEnumerable<TKey>, TValue>.ContainsKey(IEnumerable<TKey> key) => TryGetValue(key, out var _);

        public bool TryGetValue(IEnumerable<TKey> sequence, out TValue value) {
            if (sequence == null) throw new ArgumentNullException(nameof(sequence));
            var current = root;
            foreach (var element in sequence)
                if (!current.TryGetValue(element, out current)) {
                    value = default;
                    return false;
                }
            value = current.Value;
            return current.IsTip;
        }

        public void Clear() {
            root.Clear();
            Count = 0;
            version++;
        }

        public void Rebuild() {
            var queue = new Queue<TrieNode>();
            queue.Enqueue(root);
            while (queue.Count > 0) {
                var node = queue.Dequeue();
                foreach (var child in node.Values)
                    queue.Enqueue(child);
                if (node == root) continue;
                var fall = node.parent.fall;
                TrieNode target;
                while (!fall.TryGetValue(node.Key, out target) && fall != root)
                    fall = fall.fall;
                node.fall = target == null || target == node ? root : target;
                node.version = version;
            }
            root.version = version;
            isDirty = false;
        }

        public IEnumerable<Matches> Search(IEnumerable<TKey> sequence) {
            if (sequence == null) throw new ArgumentNullException(nameof(sequence));
            if (isDirty) Rebuild();
            var current = root;
            int i = 0;
            foreach (var element in sequence) {
                var node = current;
                TrieNode target;
                while (!node.TryGetValue(element, out target) && node != root)
                    node = node.fall;
                current = target = node = node == root ? target ?? root : target;
                while (target != root) {
                    if (target.IsTip) yield return new Matches(i + 1 - target.depth, i + 1, target.Value);
                    target = target.fall;
                }
                i++;
            }
        }

        public IEnumerable<Matches> SearchForReplace(IEnumerable<TKey> sequence) {
            var (result, _, count) = Search(sequence).Aggregate((new Matches[8], 0, 0), (state, match) => {
                var (result, offset, count) = state;
                if (offset >= match.end) return state;
                int discardFrom = count;
                for (int i = count - 1; i >= 0; i--) {
                    var top = result[i];
                    if (top.end <= match.start) break;
                    if (top.start < match.start) return state;
                    discardFrom = i;
                }
                count = discardFrom;
                if (result.Length <= count) {
                    int capacity = count;
                    capacity |= capacity >>  1;
                    capacity |= capacity >>  2;
                    capacity |= capacity >>  4;
                    capacity |= capacity >>  8;
                    capacity |= capacity >> 16;
                    var temp = new Matches[capacity + 1];
                    Array.Copy(result, temp, result.Length);
                    result = temp;
                }
                result[count] = match;
                return (result, match.end, count + 1);
            });
            return new ArraySegment<Matches>(result, 0, count);
        }

        void ICollection<KeyValuePair<IEnumerable<TKey>, TValue>>.Add(KeyValuePair<IEnumerable<TKey>, TValue> item) => Add(item.Key, item.Value);

        bool ICollection<KeyValuePair<IEnumerable<TKey>, TValue>>.Contains(KeyValuePair<IEnumerable<TKey>, TValue> item) =>
            TryGetValue(item.Key, out var value) && EqualityComparer<TValue>.Default.Equals(value, item.Value);

        void ICollection<KeyValuePair<IEnumerable<TKey>, TValue>>.CopyTo(KeyValuePair<IEnumerable<TKey>, TValue>[] array, int arrayIndex) {
            if (array == null) throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0 || arrayIndex + Count > array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            foreach (var kv in GetEnumerable(true)) array[arrayIndex++] = kv;
        }

        bool ICollection<KeyValuePair<IEnumerable<TKey>, TValue>>.Remove(KeyValuePair<IEnumerable<TKey>, TValue> item) => Remove(item.Key);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerable(true).GetEnumerator();

        public IEnumerator<KeyValuePair<IEnumerable<TKey>, TValue>> GetEnumerator() => GetEnumerable(true).GetEnumerator();

        IEnumerable<KeyValuePair<IEnumerable<TKey>, TValue>> GetEnumerable(bool requireKey) {
            if (isDirty) Rebuild();
            int version = this.version;
            var stack = new Stack<TrieNode>();
            var keyConcat = requireKey ? new Stack<TKey>() : null;
            stack.Push(root);
            while (stack.Count > 0) {
                var current = stack.Pop();
                foreach (var child in current.Values) stack.Push(child);
                if (!current.IsTip) continue;
                IEnumerable<TKey> key = null;
                if (requireKey) {
                    keyConcat.Clear();
                    var c = current;
                    while (c != root) {
                        keyConcat.Push(c.Key);
                        c = c.parent;
                    }
                    key = keyConcat.ToArray();
                }
                if (this.version != version)
                    throw new InvalidOperationException("Trie has been changed.");
                yield return new KeyValuePair<IEnumerable<TKey>, TValue>(key, current.Value);
            }
        }

        public interface ITrieNode : IDictionary<TKey, ITrieNode> {
            ITrieNode Parent { get; }
            ITrieNode Fallback { get; }
            TKey Key { get; }
            TValue Value { get; }
            bool IsAttached { get; }
            bool IsTip { get; }
            int Depth { get; }
        }

        class TrieNode : Dictionary<TKey, TrieNode>, ITrieNode {
            public readonly AhoCorasick<TKey, TValue> root;
            public readonly TrieNode parent;
            readonly TKey key;
            public readonly int depth;
            public int version;
            public TrieNode fall;
            bool isTip;
            TValue value;
            bool isAttached;

            public bool IsAttached {
                get {
                    if (!isAttached) return false;
                    if (version == root.version) return true;
                    if (root.isDirty) root.Rebuild();
                    if (this == root.root) return true;
                    for (var parent = this.parent; parent != root.root; parent = parent.parent)
                        if (parent == null || !parent.TryGetValue(key, out var value) || value != this)
                            return isAttached = false;
                    return true;
                }
            }

            public TKey Key {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get => key;
            }

            public bool IsTip {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get => isTip;
                set {
                    isTip = value;
                    if (!value) this.value = default;
                }
            }

            public TValue Value {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get => value;
                set {
                    this.value = value;
                    isTip = true;
                }
            }

            ITrieNode ITrieNode.Parent {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get => parent;
            }

            ITrieNode ITrieNode.Fallback { get { Validate(); return fall; } }

            int ITrieNode.Depth { get { Validate(); return depth; } }

            ICollection<TKey> IDictionary<TKey, ITrieNode>.Keys {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Keys;
            }

            ICollection<ITrieNode> IDictionary<TKey, ITrieNode>.Values {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new ChildNodeCollection(Values);
            }

            ITrieNode IDictionary<TKey, ITrieNode>.this[TKey key] {
                [MethodImpl(MethodImplOptions.AggressiveInlining)] get => this[key];
                set => throw new NotSupportedException();
            }

            bool ICollection<KeyValuePair<TKey, ITrieNode>>.IsReadOnly => true;

            public TrieNode(AhoCorasick<TKey, TValue> root, IEqualityComparer<TKey> comparer) : base(comparer) {
                this.root = root;
                fall = this;
                version = root.version;
                isAttached = true;
            }

            TrieNode(TrieNode parent, TKey key, IEqualityComparer<TKey> comparer) : this(parent.root, comparer) {
                this.parent = parent;
                this.key = key;
                depth = parent.depth + 1;
            }

            public TrieNode AddOrCreateChild(TKey key) {
                if (!TryGetValue(key, out var child))
                    this[key] = child = new TrieNode(this, key, Comparer);
                return child;
            }

            void Validate() {
                if (!IsAttached) throw new InvalidOperationException("Current trie node has been detached.");
            }

            void ICollection<KeyValuePair<TKey, ITrieNode>>.CopyTo(KeyValuePair<TKey, ITrieNode>[] array, int arrayIndex) {
                if (array == null) throw new ArgumentNullException(nameof(array));
                if (arrayIndex < 0 || arrayIndex + Count > array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
                foreach (var kv in this) array[arrayIndex++] = new KeyValuePair<TKey, ITrieNode>(kv.Key, kv.Value);
            }

            bool IDictionary<TKey, ITrieNode>.TryGetValue(TKey key, out ITrieNode value) {
                bool result = TryGetValue(key, out var v);
                value = v;
                return result;
            }

            IEnumerator<KeyValuePair<TKey, ITrieNode>> IEnumerable<KeyValuePair<TKey, ITrieNode>>.GetEnumerator() {
                foreach (var kv in this as IEnumerable<KeyValuePair<TKey, TrieNode>>)
                    yield return new KeyValuePair<TKey, ITrieNode>(kv.Key, kv.Value);
            }

            bool ICollection<KeyValuePair<TKey, ITrieNode>>.Contains(KeyValuePair<TKey, ITrieNode> item) =>
                TryGetValue(item.Key, out var value) && value == item.Value;

            void IDictionary<TKey, ITrieNode>.Add(TKey key, ITrieNode value) => throw new NotSupportedException();

            void ICollection<KeyValuePair<TKey, ITrieNode>>.Add(KeyValuePair<TKey, ITrieNode> item) => throw new NotSupportedException();

            bool IDictionary<TKey, ITrieNode>.Remove(TKey key) => false;

            bool ICollection<KeyValuePair<TKey, ITrieNode>>.Remove(KeyValuePair<TKey, ITrieNode> item) => false;

            void ICollection<KeyValuePair<TKey, ITrieNode>>.Clear() => throw new NotSupportedException();
        }

        public struct Matches {
            public int start, end;
            public TValue value;

            public Matches(int start, int end, TValue value) {
                this.start = start;
                this.end = end;
                this.value = value;
            }
        }

        struct SequenceCollection : ICollection<IEnumerable<TKey>> {
            readonly AhoCorasick<TKey, TValue> parent;

            public SequenceCollection(AhoCorasick<TKey, TValue> parent) => this.parent = parent;

            public int Count => parent.Count;

            bool ICollection<IEnumerable<TKey>>.IsReadOnly => true;

            public bool Contains(IEnumerable<TKey> item) => parent.TryGetValue(item, out var _);

            public void CopyTo(IEnumerable<TKey>[] array, int arrayIndex) {
                if (array == null) throw new ArgumentNullException(nameof(array));
                if (arrayIndex < 0 || arrayIndex + parent.Count > array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
                foreach (var kv in parent.GetEnumerable(true))
                    array[arrayIndex++] = kv.Key;
            }

            public IEnumerator<IEnumerable<TKey>> GetEnumerator() => parent.GetEnumerable(true).Select(kv => kv.Key).GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            void ICollection<IEnumerable<TKey>>.Add(IEnumerable<TKey> item) => throw new NotSupportedException();

            void ICollection<IEnumerable<TKey>>.Clear() => throw new NotSupportedException();

            bool ICollection<IEnumerable<TKey>>.Remove(IEnumerable<TKey> item) => false;
        }

        struct ValueCollection : ICollection<TValue> {
            readonly AhoCorasick<TKey, TValue> parent;

            public ValueCollection(AhoCorasick<TKey, TValue> parent) => this.parent = parent;

            public int Count => parent.Count;

            bool ICollection<TValue>.IsReadOnly => true;

            public bool Contains(TValue item) {
                var equalityComparer = EqualityComparer<TValue>.Default;
                foreach (var kv in parent.GetEnumerable(false))
                    if (equalityComparer.Equals(kv.Value, item))
                        return true;
                return false;
            }

            public void CopyTo(TValue[] array, int arrayIndex) {
                if (array == null) throw new ArgumentNullException(nameof(array));
                if (arrayIndex < 0 || arrayIndex + parent.Count > array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
                foreach (var kv in parent.GetEnumerable(false))
                    array[arrayIndex++] = kv.Value;
            }

            public IEnumerator<TValue> GetEnumerator() => parent.GetEnumerable(false).Select(kv => kv.Value).GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            void ICollection<TValue>.Add(TValue item) => throw new NotSupportedException();

            void ICollection<TValue>.Clear() => throw new NotSupportedException();

            bool ICollection<TValue>.Remove(TValue item) => false;
        }

        struct ChildNodeCollection : ICollection<ITrieNode> {
            readonly Dictionary<TKey, TrieNode>.ValueCollection parent;

            public ChildNodeCollection(Dictionary<TKey, TrieNode>.ValueCollection parent) => this.parent = parent;

            public int Count => parent.Count;

            bool ICollection<ITrieNode>.IsReadOnly => true;

            public bool Contains(ITrieNode item) => item is TrieNode trieNode && parent.Contains(trieNode);

            public void CopyTo(ITrieNode[] array, int arrayIndex) {
                if (array == null) throw new ArgumentNullException(nameof(array));
                if (arrayIndex < 0 || arrayIndex + parent.Count > array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
                foreach (var item in parent) array[arrayIndex++] = item;
            }

            public IEnumerator<ITrieNode> GetEnumerator() => parent.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => parent.GetEnumerator();

            void ICollection<ITrieNode>.Add(ITrieNode item) => throw new NotSupportedException();

            void ICollection<ITrieNode>.Clear() => throw new NotSupportedException();

            bool ICollection<ITrieNode>.Remove(ITrieNode item) => false;
        }
    }

    public struct StringReplacer {
        static readonly char[] empty = new char[0];
        char[] chars;
        int length;
        StringBuilder sb;

        public StringReplacer(string source) {
            if (string.IsNullOrEmpty(source)) {
                chars = empty;
                length = 0;
            } else {
                chars = source.ToCharArray();
                length = source.Length;
            }
            sb = null;
        }

        public StringReplacer Replace(AhoCorasick<char, string> ahoCorasick) {
            if (ahoCorasick == null) throw new ArgumentNullException(nameof(ahoCorasick));
            if (length > 0) {
                if (sb != null) {
                    if (chars.Length < sb.Length) chars = new char[sb.Length];
                    sb.CopyTo(0, chars, 0, sb.Length);
                    length = sb.Length;
                }
                int offset = -1;
                foreach (var match in ahoCorasick.SearchForReplace(new ArraySegment<char>(chars, 0, length))) {
                    if (offset < 0) {
                        if (sb == null) sb = new StringBuilder(chars.Length);
                        else sb.Clear();
                        offset = 0;
                    }
                    if (offset < match.start) sb.Append(chars, offset, match.start - offset);
                    sb.Append(match.value);
                    offset = match.end;
                }
                if (offset >= 0 && offset < length) sb.Append(chars, offset, length - offset);
            }
            return this;
        }

        public override string ToString() => this;

        public static implicit operator string(StringReplacer replacer) =>
            replacer.sb != null ? replacer.sb.ToString() :
            replacer.length > 0 ? new string(replacer.chars, 0, replacer.length) :
            string.Empty;
    }

    public static class StringReplacerHelper {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StringReplacer Replace(this string source, AhoCorasick<char, string> ahoCorasick) =>
            new StringReplacer(source).Replace(ahoCorasick);
    }
}