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
        static Controller() => m_sdk = new SDKHandler();

        static bool m_initialized = false;

        public static void Initialize()
        {
            if (m_initialized) return;

            m_sdk.CameraAdded += HandleCameraAdded;
            m_sdk.CameraHasShutdown += HandleCameraHasShutdown;
            m_sdk.LiveViewUpdated += HandleLiveViewUpdated;
            
            OpenSession();
            m_initialized = true;
        }

        public static void Start() => IsStarted = true;

        public static void Stop() => IsStarted = false;

        #region properties

        public static bool IsStarted { get; private set; }

        public static int Width { get; private set; }
        
        public static int Height { get; private set; }
        
        public static int BitDepth { get; private set; }

        public static int FPS { get { return (int)m_stats.FPS; } }

        #endregion

        private static void OpenSession()
        {
            CloseSession();
            var cameras = m_sdk.GetCameraList();
            if (cameras.Count > 0)
            {
                m_sdk.OpenSession(cameras.First());
                m_sdk.StartLiveView();
            }
        }

        private static void CloseSession()
        {
            if(m_sdk.IsLiveViewOn)
                m_sdk.StopLiveView();
            m_sdk.CloseSession();
        }

        #region event handlers

        private static void HandleCameraHasShutdown(object sender, EventArgs e)
        {
            CloseSession();
        }

        /// <summary>
        /// When a camera is added 
        /// </summary>
        private static void HandleCameraAdded()
        {
            OpenSession();
        }

        private static void HandleLiveViewUpdated(Stream stream)
        {
            Bitmap bitmap = null;
            try
            {
                bitmap = new Bitmap(stream);

                if (!IsStarted && (Width == 0 || Height == 0 || BitDepth == 0))
                {
                    Width = bitmap.Width;
                    Height = bitmap.Height;
                    BitDepth = Image.GetPixelFormatSize(bitmap.PixelFormat);
                }

                m_stats.UpdateStats(bitmap);

                if (IsStarted)
                    FrameReceived?.Invoke(null, (Bitmap)Image.FromStream(stream));
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error\r\n{0}", ex);
            }
            finally
            {
                if (!IsStarted && bitmap != null)
                    bitmap.Dispose();
            }
        }

        #endregion

        /// <summary>
        /// When a frame is recieved
        /// </summary>
        public static event EventHandler<Bitmap> FrameReceived;

        static readonly SDKHandler m_sdk;

        static readonly CameraStats m_stats = new CameraStats();

        public static SDKHandler SDK => m_sdk;
    }
}
