using System;
using System.Diagnostics;
using System.Drawing;
using System.Timers;

namespace CanonCaptureFilter
{
    /// <summary>
    /// Gather statistics on the camera operation
    /// </summary>
    class CameraStats
    {
        public CameraStats() => m_timer.Elapsed += Timer_Elapsed;

        #region fields

        /// <summary> Set to true after the first frame arrives </summary>
        bool m_started = false;

        /// <summary> The timestamp of the first frame for average FPS </summary>
        DateTime m_firstFrame;

        /// <summary> frames in one second for instataneous FPS </summary>
        int m_intervalFrames = 0;

        /// <summary> Used to get FPS </summary>
        private Stopwatch m_stopWatch = null;

        /// <summary> The timer to measure instantaneous FPS </summary>
        readonly Timer m_timer = new Timer(1000);

        #endregion

        #region properties

        /// <summary> The instantaneous frames per second over the last second </summary>
        public float FPS { get; private set; }

        /// <summary> Duration </summary>
        public double Duration => (DateTime.Now - m_firstFrame).TotalSeconds;

        /// <summary> Average frames per second </summary>
        public float AverageFPS => FramesReceived / (float)Duration;

        /// <summary> The average bitrate </summary>
        public float Bitrate => (BytesReceived / FramesReceived) * AverageFPS;

        /// <summary> Total frames received </summary>
        public int FramesReceived { get; private set; }

        /// <summary> Total bytes received </summary>
        public long BytesReceived { get; private set; }

        /// <summary> Bitmap width </summary>
        public int Width { get; private set; }

        /// <summary> Bitmap height </summary>
        public int Height { get; private set; }

        /// <summary> Bitmap bits per pixel </summary>
        public int BitDepth { get; private set; }

        #endregion

        /// <summary>
        /// Timer for counting frame rate
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                m_timer.Stop();
                if (!m_started) return;

                // get number of frames since the last timer tick

                if (m_stopWatch == null)
                {
                    m_stopWatch = new Stopwatch();
                    m_stopWatch.Start();
                }
                else
                {
                    m_stopWatch.Stop();

                    FPS = 1000.0f * (float)m_intervalFrames / m_stopWatch.ElapsedMilliseconds;

                    m_stopWatch.Reset();
                    m_stopWatch.Start();
                }
                m_intervalFrames = 0;
            }
            catch (Exception ex)
            {
                Trace.TraceError("Timer_Elapsed: {0}", ex);
            }
            finally
            {
                m_timer.Start();
            }
        }

        /// <summary>
        /// Update statistics
        /// </summary>
        /// <param name="frame"></param>
        public void Update(Bitmap frame)
        {
            if (!m_started)
            {
                m_started = true;
                m_firstFrame = DateTime.Now;
                m_timer.Start();
            }

            Width = frame.Width;
            Height = frame.Height;
            BitDepth = Image.GetPixelFormatSize(frame.PixelFormat);

            FramesReceived++;
            m_intervalFrames++;
            BytesReceived += frame.Width * frame.Height * Image.GetPixelFormatSize(frame.PixelFormat);
        }
    }
}