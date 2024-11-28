using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Utils
{
    /// <summary>
    /// Convenience extensions for utilizing multiple threads.
    /// </summary>
    public static class ThreadingExtensions
    {
        /// <inheritdoc cref="RunParallel{TIn,TOut}(IList{TIn},Func{TIn,TOut},int)"/>
        public static IEnumerable<TOut> RunParallel<TIn, TOut>(this IEnumerable<TIn> data, Func<TIn, TOut> work, int workerCount = -1)
        {
            foreach (var result in RunParallel(data.ToList(), work))
                yield return result;
        }

        /// <summary>
        /// Apply a function to a collection of data by spreading the work on multiple threads.
        /// Outputs of the functions are returned to the current thread and yielded one by one.
        /// </summary>
        /// <typeparam name="TIn">Type of the input values.</typeparam>
        /// <typeparam name="TOut">Type of the output values.</typeparam>
        /// <param name="data">Input values for the work function.</param>
        /// <param name="work">Function to apply to the data on multiple threads at once.</param>
        /// <param name="workerCount">Number of worker threads. By default SystemInfo.processorCount is used.</param>
        /// <exception cref="TargetInvocationException">An exception was thrown inside one of the threads, and the operation was aborted.</exception>
        /// <exception cref="ArgumentException">Need at least 1 workerCount.</exception>
        public static IEnumerable<TOut> RunParallel<TIn, TOut>(this IList<TIn> data, Func<TIn, TOut> work, int workerCount = -1)
        {
            if (workerCount < 0)
                workerCount = Mathf.Max(2, Environment.ProcessorCount);
            else if (workerCount == 0)
                throw new ArgumentException("Need at least 1 worker", nameof(workerCount));

            var perThreadCount = Mathf.CeilToInt(data.Count / (float)workerCount);
            var doneCount = 0;

            var lockObj = new object();
            var are = new ManualResetEvent(false);
            IEnumerable<TOut> doneItems = null;
            Exception exceptionThrown = null;

            // Start threads to process the data
            for (var i = 0; i < workerCount; i++)
            {
                int first = i * perThreadCount;
                int last = Mathf.Min(first + perThreadCount, data.Count);
                ThreadPool.QueueUserWorkItem(
                    _ =>
                    {
                        var results = new List<TOut>(perThreadCount);

                        try
                        {
                            for (int dataIndex = first; dataIndex < last; dataIndex++)
                            {
                                if (exceptionThrown != null) break;
                                results.Add(work(data[dataIndex]));
                            }
                        }
                        catch (Exception ex)
                        {
                            exceptionThrown = ex;
                        }

                        lock (lockObj)
                        {
                            doneItems = doneItems == null ? results : results.Concat(doneItems);
                            doneCount++;
                            are.Set();
                        }
                    });
            }

            // Main thread waits for results and returns them until all threads finish
            while (true)
            {
                are.WaitOne();

                IEnumerable<TOut> toOutput;
                bool isDone;
                lock (lockObj)
                {
                    toOutput = doneItems;
                    doneItems = null;
                    isDone = doneCount == workerCount;
                }

                if (toOutput != null)
                {
                    foreach (var doneItem in toOutput)
                        yield return doneItem;
                }

                if (isDone)
                    break;
            }

            if (exceptionThrown != null)
                throw new TargetInvocationException("An exception was thrown inside one of the threads", exceptionThrown);
        }
    }
}
