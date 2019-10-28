/*
 * (C) Copyright 2019 Maciej Izak
 */

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DnsMx
{
    public static class TasksUtils
    {
        public static async Task WhenAllOrError(IEnumerable<Task> tasks)
        {
            var killer = new TaskCompletionSource<object>();
            var tasksArray = tasks as Task[] ?? tasks.ToArray();
            foreach (var task in tasksArray)
                _ = task.ContinueWith(a =>
                {
                    if (a.IsCanceled)
                        killer.TrySetCanceled();
                    else if (a.IsFaulted && a.Exception?.InnerException != null)
                        killer.TrySetException(a.Exception.InnerException);
                });
            await await Task.WhenAny(killer.Task, Task.WhenAll(tasksArray));
        }
    }
}