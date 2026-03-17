using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileRecord.Utils
{
    /// <summary>
    /// 字符串分割辅助类
    /// </summary>
    public static class StringSplitHelper
    {
        /// <summary>
        /// 分割字符串并去除空白（兼容 .NET Framework 4.7.2）
        /// </summary>
        public static string[] SplitAndTrim(string input, char separator)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new string[0];

            return input.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToArray();
        }

        /// <summary>
        /// 分割字符串并去除空白，返回 List（兼容 .NET Framework 4.7.2）
        /// </summary>
        public static List<string> SplitAndTrimToList(string input, char separator)
        {
            return SplitAndTrim(input, separator).ToList();
        }
    }

#if NET472
    /// <summary>
    /// .NET Framework 4.7.2 兼容的并行处理辅助类
    /// </summary>
    public static class ParallelCompat
    {
        /// <summary>
        /// 并行异步处理（.NET 472 兼容版本）
        /// </summary>
        public static async Task ForEachAsync<T>(IEnumerable<T> source, Func<T, CancellationToken, Task> body)
        {
            var tasks = source.Select(item => body(item, CancellationToken.None)).ToArray();
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// 获取相对路径（.NET 472 兼容版本）
        /// </summary>
        public static string GetRelativePath(string relativeTo, string path)
        {
            var uri = new Uri(relativeTo);
            var rel = Uri.UnescapeDataString(uri.MakeRelativeUri(new Uri(path)).ToString());
            return rel.Replace('/', Path.DirectorySeparatorChar);
        }
    }

    /// <summary>
    /// .NET Framework 4.7.2 兼容的优先队列实现
    /// </summary>
    public class CompatiblePriorityQueue<TElement, TPriority> where TPriority : IComparable<TPriority>
    {
        private readonly List<(TElement Element, TPriority Priority)> _items;

        public CompatiblePriorityQueue()
        {
            _items = new List<(TElement, TPriority)>();
        }

        public CompatiblePriorityQueue(IEnumerable<(TElement Element, TPriority Priority)> items)
        {
            _items = items.ToList();
            Heapify();
        }

        public int Count => _items.Count;

        public IEnumerable<(TElement Element, TPriority Priority)> UnorderedItems => _items;

        public void Enqueue(TElement element, TPriority priority)
        {
            _items.Add((element, priority));
            HeapifyUp(_items.Count - 1);
        }

        public bool TryDequeue(out TElement element, out TPriority priority)
        {
            if (_items.Count == 0)
            {
                element = default!;
                priority = default!;
                return false;
            }

            var result = _items[0];
            element = result.Element;
            priority = result.Priority;

            // 将最后一个元素移到顶部并下沉
            _items[0] = _items[_items.Count - 1];
            _items.RemoveAt(_items.Count - 1);

            if (_items.Count > 0)
            {
                HeapifyDown(0);
            }

            return true;
        }

        public TElement Peek()
        {
            if (_items.Count == 0)
                throw new InvalidOperationException("Queue is empty");
            return _items[0].Element;
        }

        private void Heapify()
        {
            for (int i = _items.Count / 2 - 1; i >= 0; i--)
            {
                HeapifyDown(i);
            }
        }

        private void HeapifyUp(int index)
        {
            while (index > 0)
            {
                int parentIndex = (index - 1) / 2;
                if (Compare(_items[index].Priority, _items[parentIndex].Priority) >= 0)
                    break;

                Swap(index, parentIndex);
                index = parentIndex;
            }
        }

        private void HeapifyDown(int index)
        {
            while (true)
            {
                int smallest = index;
                int leftChild = 2 * index + 1;
                int rightChild = 2 * index + 2;

                if (leftChild < _items.Count && 
                    Compare(_items[leftChild].Priority, _items[smallest].Priority) < 0)
                {
                    smallest = leftChild;
                }

                if (rightChild < _items.Count && 
                    Compare(_items[rightChild].Priority, _items[smallest].Priority) < 0)
                {
                    smallest = rightChild;
                }

                if (smallest == index)
                    break;

                Swap(index, smallest);
                index = smallest;
            }
        }

        private int Compare(TPriority a, TPriority b)
        {
            return a.CompareTo(b);
        }

        private void Swap(int i, int j)
        {
            var temp = _items[i];
            _items[i] = _items[j];
            _items[j] = temp;
        }
    }
#endif
}
