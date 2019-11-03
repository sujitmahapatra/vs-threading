﻿#if DEBUG

namespace Microsoft.VisualStudio.Threading
{
    using System;
    using System.Diagnostics;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// A debug help in forcing certain timing conditions to be met as part of replaying a possible race condition.
    /// </summary>
    public static class SyncPoints
    {
        /// <summary>
        /// The lock to enter and wait on as part of this timing control.
        /// </summary>
        private static readonly object SyncObject = new object();

        /// <summary>
        /// The current value of a monotonically increasing sequence number.
        /// </summary>
        private static int current;

        /// <summary>
        /// Blocks the caller until it is time to execute the prescribed step.
        /// </summary>
        /// <param name="step">The sequence number that the calling code should be unblocked for. Callers are only unblocked after the previous step has been unblocked.</param>
        /// <param name="doNotBlockBefore">If the sequence number if smaller than this number, do not block.</param>
        public static void Step(int step, int? doNotBlockBefore = null)
        {
            lock (SyncObject)
            {
                if (doNotBlockBefore.HasValue && current < doNotBlockBefore.Value)
                {
                    Debug.WriteLine($"Allowing step {step} through because the current step {current} is less than {doNotBlockBefore}.");
                    return;
                }

                while (current + 1 < step)
                {
                    Monitor.Wait(SyncObject);
                }

                if (current + 1 == step)
                {
                    Debug.WriteLine($"Allowing step {step} through in sequence." + GetStackTrace());
                    current = step;
                    Monitor.PulseAll(SyncObject);
                }
                else
                {
                    Debug.WriteLine($"Allowing step {step} through because its time in the sequence has already passed.");
                }
            }
        }

        private static string GetStackTrace()
        {
            const string indent = "    ";
            var stackTrace = new StackTrace(2, fNeedFileInfo: true);
            var sb = new StringBuilder();
            sb.Append(Environment.NewLine);
            bool inExternalCode = false;
            foreach (var frame in stackTrace.GetFrames())
            {
                if (frame?.GetFileName() != null && frame.GetMethod() is System.Reflection.MethodBase method)
                {
                    inExternalCode = false;
                    sb.Append(indent);
                    //// at System.Runtime.Remoting.Channels.CrossAppDomainSink.DoDispatch(Byte[] reqStmBuff, SmuggledMethodCallMessage smuggledMcm, SmuggledMethodReturnMessage& smuggledMrm)
                    sb.Append($"at {method.DeclaringType?.FullName}.{method.Name}(");
                    bool firstParameter = true;
                    foreach (var p in method.GetParameters())
                    {
                        if (!firstParameter)
                        {
                            sb.Append(", ");
                        }

                        sb.Append($"{p.ParameterType.Name} {p.Name}");
                        firstParameter = false;
                    }

                    sb.AppendLine($") in {frame.GetFileName()}:{frame.GetFileLineNumber()}");
                }
                else if (!inExternalCode)
                {
                    inExternalCode = true;
                    sb.Append(indent);
                    sb.AppendLine("[External Code]");
                }
            }

            return sb.ToString();
        }
    }
}

#endif
