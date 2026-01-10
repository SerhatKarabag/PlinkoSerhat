using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Plinko.Utils
{
    public static class TaskExtensions
    {
        public static async Task WithLogging(this Task task, string context = null)
        {
            if (task == null) return;

            try
            {
                await task;
            }
            catch (Exception ex)
            {
                var contextInfo = string.IsNullOrEmpty(context) ? "" : $"[{context}] ";
                Debug.LogError($"{contextInfo}Task failed: {ex}");
            }
        }
    }
}
