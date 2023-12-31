﻿/*
 * This file contains code derived from react-native-sqlite-storage (https://github.com/andpor/react-native-sqlite-storage).
 * 
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 andpor
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 * 
 * AwaitingQueue.cs
 * 
 * Serializes the work of all Tasks that are added to its queue. Awaits the
 * Task returned by the current work item before moving on to the next work
 * item.
 * 
 * This class is not thread-safe. All methods should be called from the same thread
 * or LimitedConcurrencyActionQueue. `await` must cause the continuation to run on
 * the same thread or LimitedConcurrencyActionQueue.
 * 
 * Motivation:
 *   When you `await` a Task, you have to consider all of the things that could have
 *   changed by the time your continuation runs. For example:
 *
 *      class Recorder
 *      {
 *          private MediaCapture _captureMedia;
 *
 *          public async Task StartRecording()
 *          {
 *              _captureMedia = new MediaCapture();
 *              await _captureMedia.InitializeAsync();
 *              // Lots of things could have changed by the time we get here.
 *              // For example, maybe `_captureMedia` is null!
 *              await _captureMedia.StartRecordToStreamAsync(...);
 *          }
 *
 *          public async Task StopRecording()
 *          {
 *              // This code can run while `StartRecording` is in the middle
 *              // of running.
 *
 *              if (_captureMedia != null)
 *              {
 *                  // Code to clean up _captureMedia...
 *                  _captureMedia = null;
 *              }
 *          }
 *      }
 *
 *   Alternatively, you can use `AwaitingQueue` to serialize async work that
 *   interacts with each other to prevent any interleavings. Example:
 *
 *      class Recorder
 *      {
 *          private AwaitingQueue _awaitingQueue = new AwaitingQueue();
 *          private MediaCapture _captureMedia;
 *
 *          public async Task StartRecording()
 *          {
 *              _awaitingQueue.RunOrDispatch(async () =>
 *              {
 *                  // This code won't run until all of the other Tasks
 *                  // that were added to the `_awaitingQueue` before us
 *                  // have already completed.
 *
 *                  _captureMedia = new MediaCapture();
 *                  await _captureMedia.InitializeAsync(captureInitSettings);
 *                  // We can think of `StartRecording` as being atomic which
 *                  // means we don't have to worry about anything we care about
 *                  // changing by the time we get here. For example, `_captureMedia`
 *                  // is guaranteed to be non-null by design.
 *                  await _captureMedia.StartRecordToStreamAsync(...);
 *              });
 *          }
 *
 *          public async Task StopRecording()
 *          {
 *              _awaitingQueue.RunOrDispatch(() =>
 *              {
 *                  // This code won't run until all of the other Tasks
 *                  // that were added to the `_awaitingQueue` before us
 *                  // have already completed. This means this code can't
 *                  // run while `StartRecording` is in the middle of running.
 *
 *                  if (_captureMedia != null)
 *                  {
 *                      // Code to clean up _captureMedia...
 *                      _captureMedia = null;
 *                  }
 *              });
 *          }
 *      }
 */

namespace MyApp.REPLServer.WebSocketServerNamespace
{
    /// <summary>
    /// Serializes the work of all Tasks that are added to its queue. Awaits the
    /// Task returned by the current work item before moving on to the next work
    /// item.
    /// </summary>
    /// <remarks>
    /// This class is not thread-safe. All methods should be called from the same thread
    /// or LimitedConcurrencyActionQueue. `await` must cause the continuation to run on
    /// the same thread or LimitedConcurrencyActionQueue.
    /// </remarks>
    /// <typeparam name="T">The type of value yielded by each Task in the queue.</typeparam>
    internal class AwaitingQueue<T>
    {
        private const string _tag = nameof(AwaitingQueue);

        private class WorkItemInfo
        {
            public readonly Func<Task<T>> WorkItem;
            public readonly TaskCompletionSource<T> CompletionSource;
            public readonly CancellationToken CancellationToken;

            public WorkItemInfo(Func<Task<T>> workItem, TaskCompletionSource<T> completionSource, CancellationToken cancellationToken)
            {
                WorkItem = workItem;
                CompletionSource = completionSource;
                CancellationToken = cancellationToken;
            }
        }

        private bool _running = false;
        private readonly Queue<WorkItemInfo> _workQueue = new Queue<WorkItemInfo>();

        private async void StartWorkLoopIfNeeded()
        {
            if (_running)
            {
                return;
            }

            try
            {
                _running = true;
                while (_workQueue.Count > 0)
                {
                    var workItemInfo = _workQueue.Dequeue();

                    if (workItemInfo.CancellationToken.IsCancellationRequested)
                    {
                        workItemInfo.CompletionSource.SetCanceled();
                    }
                    else
                    {
                        //RnLog.Info($"UI AwaitingQueue: Start {currentName}");
                        try
                        {
                            var result = await workItemInfo.WorkItem();
                            workItemInfo.CompletionSource.SetResult(result);
                        }
                        catch (Exception ex)
                        {
                            workItemInfo.CompletionSource.SetException(ex);
                        }
                        //RnLog.Info($"UI AwaitingQueue: End {currentName}");
                    }
                }
                _running = false; // Ensure _running is updated before firing the event
                QueueEmptied?.Invoke(this, null);
            }
            finally
            {
                // Before exiting this method, ensure _running is updated
                _running = false;
            }
        }

        /// <summary>
        /// Adds `workItem` to the queue. If the queue is currently empty and not
        /// executing any work items, executes `workItem` immediately and synchronously.
        /// </summary>
        /// <param name="workItem">The work item to add to the queue.</param>
        /// <returns>
        /// A Task which completes when `workItem` finishes executing. The returned
        /// Task resolves to the result or exception from `workItem`.
        /// </returns>
        public Task<T> RunOrQueue(Func<Task<T>> workItem)
        {
            return RunOrQueue(workItem, CancellationToken.None);
        }

        /// <summary>
        /// Adds `workItem` to the queue. If the queue is currently empty and not
        /// executing any work items, executes `workItem` immediately and synchronously.
        /// </summary>
        /// <param name="workItem">The work item to add to the queue.</param>
        /// <param name="cancellationToken">
        /// The cancellation token associated with the work item. The work item will
        /// be skipped if the cancellation token is canceled before the work item begins.
        /// </param>
        /// <returns>
        /// A Task which completes when `workItem` finishes executing. The returned
        /// Task resolves to the result or exception from `workItem`. If the
        /// cancellation token is canceled before `workItem` begins, Task is canceled.
        /// </returns>
        public Task<T> RunOrQueue(Func<Task<T>> workItem, CancellationToken cancellationToken)
        {
            //RnLog.Info($"UI AwaitingQueue: Add {name}");
            TaskCompletionSource<T> completionSource = new TaskCompletionSource<T>();

            _workQueue.Enqueue(new WorkItemInfo(workItem, completionSource, cancellationToken));
            StartWorkLoopIfNeeded();
            return completionSource.Task;
        }


        /// <summary>
        /// Indicates that the AwaitingQueue has finished executing all of its
        /// currently scheduled work items.
        /// </summary>
        /// <remarks>
        /// Fires on the thread or LimitedConcurrencyActionQueue of the code that
        /// has been conusming the AwaitingQueue.
        /// </remarks>
        public event EventHandler QueueEmptied;
    }

    /// <summary>
    /// Serializes the work of all Tasks that are added to its queue. Awaits the
    /// Task returned by the current work item before moving on to the next work
    /// item.
    /// </summary>
    /// <remarks>
    /// This class is not thread-safe. All methods should be called from the same thread
    /// or LimitedConcurrencyActionQueue. `await` must cause the continuation to run on
    /// the same thread or LimitedConcurrencyActionQueue.
    /// </remarks>
    public class AwaitingQueue
    {
        private AwaitingQueue<object> _awaitingQueue = new AwaitingQueue<object>();

        /// <summary>
        /// Adds `workItem` to the queue. If the queue is currently empty and not
        /// executing any work items, executes `workItem` immediately and synchronously.
        /// </summary>
        /// <param name="workItem">The work item to add to the queue.</param>
        /// <returns>
        /// A Task which completes when `workItem` finishes executing. The returned
        /// Task throws any exceptions that `workItem` may have thrown.
        /// </returns>
        public Task RunOrQueue(Func<Task> workItem)
        {
            return RunOrQueue(workItem, CancellationToken.None);
        }

        /// <summary>
        /// Adds `workItem` to the queue. If the queue is currently empty and not
        /// executing any work items, executes `workItem` immediately and synchronously.
        /// </summary>
        /// <param name="workItem">The work item to add to the queue.</param>
        /// <param name="cancellationToken">
        /// The cancellation token associated with the work item. The work item will
        /// be skipped if the cancellation token is canceled before the work item begins.
        /// </param>
        /// <returns>
        /// A Task which completes when `workItem` finishes executing. The returned
        /// Task throws any exceptions that `workItem` may have thrown. If the
        /// cancellation token is canceled before `workItem` begins, Task is canceled.
        /// </returns>
        public Task RunOrQueue(Func<Task> workItem, CancellationToken cancellationToken)
        {
            return _awaitingQueue.RunOrQueue(async () =>
            {
                await workItem();
                return null;
            }, cancellationToken);
        }

        /// <summary>
        /// Indicates that the AwaitingQueue has finished executing all of its
        /// currently scheduled work items.
        /// </summary>
        /// <remarks>
        /// Fires on the thread or LimitedConcurrencyActionQueue of the code that
        /// has been conusming the AwaitingQueue.
        /// </remarks>
        public event EventHandler QueueEmptied
        {
            add
            {
                _awaitingQueue.QueueEmptied += value;
            }
            remove
            {
                _awaitingQueue.QueueEmptied -= value;
            }
        }
    }
}
