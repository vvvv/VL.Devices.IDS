using System.Reactive.Disposables;
using System.Runtime.ExceptionServices;

namespace VL.Devices.IDS
{
    internal class IdsPeakLibrary
    {
        private static readonly object s_initLock = new object();
        private static int s_refCount;
        private static ExceptionDispatchInfo? s_exception;

        public static IDisposable Use()
        {
            lock (s_initLock)
            {
                if (s_exception != null)
                    s_exception.Throw();

                if (Interlocked.Increment(ref s_refCount) == 1)
                {
                    try
                    {
                        peak.Library.Initialize();
                    }
                    catch (Exception e)
                    {
                        s_exception = ExceptionDispatchInfo.Capture(e);
                        throw;
                    }
                }

                return Disposable.Create(Release);
            }
        }

        private static void Release()
        {
            lock (s_initLock)
            {
                if (Interlocked.Decrement(ref s_refCount) == 0)
                {
                    peak.DeviceManager.Instance().Reset();
                    peak.Library.Close();
                }
            }
        }
    }
}
