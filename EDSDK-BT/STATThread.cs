using System;
using System.Threading;

namespace EDSDK_NET
{
    /// <summary>
    /// Helper class to create or run code on STA threads
    /// </summary>
    public static class STAThread
    {
        /// <summary>
        /// The object that is used to lock the live view thread
        /// </summary>
        public static readonly object m_execLock = new object();

        /// <summary>
        /// States if the calling thread is an STA thread or not
        /// </summary>
        public static bool IsSTAThread => Thread.CurrentThread.GetApartmentState() == ApartmentState.STA;

        /// <summary>
        /// The main thread where everything will be executed on
        /// </summary>
        private static Thread m_main;

        /// <summary>
        /// The action to be executed
        /// </summary>
        private static Action m_runAction;

        /// <summary>
        /// Storage for an exception that might have happened on the execution thread
        /// </summary>
        private static Exception m_runException;

        /// <summary>
        /// States if the execution thread is currently running
        /// </summary>
        private static bool m_isRunning = false;

        /// <summary>
        /// Lock object to make sure only one command at a time is executed
        /// </summary>
        private static readonly object m_runLock = new object();

        /// <summary>
        /// Lock object to synchronize between execution and calling thread
        /// </summary>
        private static readonly object threadLock = new object();

        /// <summary>
        /// Starts the execution thread
        /// </summary>
        internal static void Init()
        {
            if (!m_isRunning)
            {
                m_main = Create(SafeExecutionLoop);
                m_isRunning = true;
                m_main.Start();
            }
        }

        /// <summary>
        /// Shuts down the execution thread
        /// </summary>
        internal static void Shutdown()
        {
            if (m_isRunning)
            {
                m_isRunning = false;
                lock (threadLock) { Monitor.Pulse(threadLock); }
                m_main.Join();
            }
        }

        /// <summary>
        /// Creates an STA thread that can safely execute SDK commands
        /// </summary>
        /// <param name="a">The command to run on this thread</param>
        /// <returns>An STA thread</returns>
        public static Thread Create(Action a)
        {
            var thread = new Thread(new ThreadStart(a));
            thread.SetApartmentState(ApartmentState.STA);
            return thread;
        }


        /// <summary>
        /// Safely executes an SDK command
        /// </summary>
        /// <param name="a">The SDK command</param>
        public static void ExecuteSafely(Action a)
        {
            lock (m_runLock)
            {
                if (!m_isRunning) return;

                if (IsSTAThread)
                {
                    m_runAction = a;
                    lock (threadLock)
                    {
                        Monitor.Pulse(threadLock);
                        Monitor.Wait(threadLock);
                    }
                    if (m_runException != null) throw m_runException;
                }
                else lock (m_execLock) { a(); }
            }
        }

        /// <summary>
        /// Safely executes an SDK command with return value
        /// </summary>
        /// <param name="func">The SDK command</param>
        /// <returns>the return value of the function</returns>
        public static T ExecuteSafely<T>(Func<T> func)
        {
            T result = default;
            ExecuteSafely(delegate { result = func(); });
            return result;
        }

        private static void SafeExecutionLoop()
        {
            lock (threadLock)
            {
                while (true)
                {
                    Monitor.Wait(threadLock);
                    
                    if (!m_isRunning) return;

                    m_runException = null;
                    
                    try { lock (m_execLock) { m_runAction(); } }
                    catch (Exception ex) { m_runException = ex; }
                    
                    Monitor.Pulse(threadLock);
                }
            }
        }
    }
}
