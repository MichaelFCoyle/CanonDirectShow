using EDSDK_NET;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;

namespace CanonCaptureFilter
{
    public static class Controller
    {
        static Controller() => SDK = new SDKHandler();

        static readonly CameraStats m_stats = new CameraStats();

        static bool m_initialized = false;

        public static void Initialize()
        {
            if (m_initialized) return;

            SDK.CameraAdded += HandleCameraAdded;
            SDK.CameraHasShutdown += HandleCameraHasShutdown;
            SDK.LiveViewUpdated += HandleLiveViewUpdated;

            OpenSession();
            m_initialized = true;
        }

        public static void Terminate() => SDK.Dispose();

        public static void Start() => IsStarted = true;

        public static void Stop() => IsStarted = false;

        #region properties

        public static SDKHandler SDK { get; }

        public static bool IsStarted { get; private set; }

        public static int Width => m_stats.Width;

        public static int Height => m_stats.Height;

        public static int BitDepth => m_stats.BitDepth;

        /// <summary> Instantaneous FPS over the last frame </summary>
        public static float FPS => m_stats.FPS;

        /// <summary> Average FPS over the entire run </summary>
        public static float AverageFPS => m_stats.AverageFPS;

        #endregion

        #region events

        /// <summary> When a frame is recieved </summary>
        public static event EventHandler<Bitmap> FrameReceived;

        #endregion

        #region private

        private static void OpenSession()
        {
            CloseSession();
            var cameras = SDK.GetCameraList();
            if (cameras.Count > 0)
            {
                SDK.OpenSession(cameras.First());
                SDK.StartLiveView();
            }
        }

        private static void CloseSession()
        {
            if (SDK.IsLiveViewOn)
                SDK.StopLiveView();
            SDK.CloseSession();
        }

        #endregion

        #region event handlers

        /// <summary>
        /// When a camera shuts down
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void HandleCameraHasShutdown(object sender, EventArgs e) => CloseSession();

        /// <summary>
        /// When a camera is added 
        /// </summary>
        private static void HandleCameraAdded() => OpenSession();

        private static void HandleLiveViewUpdated(Stream stream)
        {
            try
            {
                using (Bitmap bitmap = new Bitmap(stream))
                {
                    m_stats.Update(bitmap);

                    if (IsStarted)
                        FrameReceived?.Invoke(null, (Bitmap)Image.FromStream(stream));
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error\r\n{0}", ex);
            }
        }

        #endregion

    }
}