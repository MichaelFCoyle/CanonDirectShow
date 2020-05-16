using DirectShow;
using DirectShow.BaseClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CanonCameraSource
{
    #region Screen Capture

    [ComVisible(true)]
    public class ScreenCaptureStream : SourceStream
    {
        #region Constructor

        public ScreenCaptureStream(string _name, BaseSourceFilter _filter) : base(_name, _filter) { }

        #endregion

        #region Overridden Methods

        public override int GetMediaType(ref AMMediaType pMediaType) => (m_Filter as ScreenCaptureFilter).GetMediaType(ref pMediaType);

        public override int DecideBufferSize(ref IMemAllocatorImpl pAlloc, ref AllocatorProperties prop)
        {
            if (!IsConnected) return VFW_E_NOT_CONNECTED;
            return (m_Filter as ScreenCaptureFilter).DecideBufferSize(ref pAlloc, ref prop);
        }

        public override int FillBuffer(ref IMediaSampleImpl pSample) => (m_Filter as ScreenCaptureFilter).FillBuffer(ref pSample);

        #endregion
    }

    [ComVisible(true)]
    [Guid("63E2D3DC-9266-4277-A796-6DCD5400772C")]
    [AMovieSetup(true)]
    [PropPageSetup(typeof(FilterAboutForm))]
    public class ScreenCaptureFilter : BaseSourceFilter
    {
        #region Variables

        protected int m_nWidth = 640;
        protected int m_nHeight = 480;
        protected long m_nAvgTimePerFrame = UNITS / 20;
        protected long m_lLastSampleTime = 0;

        protected IntPtr m_hScreenDC = IntPtr.Zero;
        protected IntPtr m_hMemDC = IntPtr.Zero;
        protected IntPtr m_hBitmap = IntPtr.Zero;
        protected BitmapInfo m_bmi = new BitmapInfo();

        protected int m_nMaxWidth = 0;
        protected int m_nMaxHeight = 0;

        #endregion

        #region Constructor

        public ScreenCaptureFilter() : base("CSharp Screen Capture Filter") { }

        #endregion

        #region Overridden Methods

        protected override int OnInitializePins()
        {
            AddPin(new ScreenCaptureStream("Output", this));
            return NOERROR;
        }

        public override int Pause()
        {
            if (m_State == FilterState.Stopped)
            {
                m_lLastSampleTime = 0;
                m_hScreenDC = CreateDC("DISPLAY", null, null, IntPtr.Zero);
                m_nMaxWidth = GetDeviceCaps(m_hScreenDC, 8); // HORZRES
                m_nMaxHeight = GetDeviceCaps(m_hScreenDC, 10); // VERTRES
                m_hMemDC = CreateCompatibleDC(m_hScreenDC);
            }
            return base.Pause();
        }

        public override int Stop()
        {
            int hr = base.Stop();
            if (m_hBitmap != IntPtr.Zero)
            {
                DeleteObject(m_hBitmap);
                m_hBitmap = IntPtr.Zero;
            }

            if (m_hScreenDC != IntPtr.Zero)
            {
                DeleteDC(m_hScreenDC);
                m_hScreenDC = IntPtr.Zero;
            }

            if (m_hMemDC != IntPtr.Zero)
            {
                DeleteDC(m_hMemDC);
                m_hMemDC = IntPtr.Zero;
            }

            return hr;
        }

        #endregion

        #region Methods

        public int GetMediaType(ref AMMediaType pMediaType)
        {
            pMediaType.majorType = DirectShow.MediaType.Video;
            pMediaType.subType = DirectShow.MediaSubType.RGB32;
            pMediaType.formatType = DirectShow.FormatType.VideoInfo;

            VideoInfoHeader vih = new VideoInfoHeader
            {
                AvgTimePerFrame = m_nAvgTimePerFrame,
                BmiHeader = new BitmapInfoHeader()
            };
            vih.BmiHeader.Size = Marshal.SizeOf(typeof(BitmapInfoHeader));
            vih.BmiHeader.Compression = 0;
            vih.BmiHeader.BitCount = 32;
            vih.BmiHeader.Width = m_nWidth;
            vih.BmiHeader.Height = m_nHeight;
            vih.BmiHeader.Planes = 1;
            vih.BmiHeader.ImageSize = vih.BmiHeader.Width * vih.BmiHeader.Height * vih.BmiHeader.BitCount / 8;
            vih.SrcRect = new DsRect();
            vih.TargetRect = new DsRect();

            AMMediaType.SetFormat(ref pMediaType, ref vih);
            pMediaType.fixedSizeSamples = true;
            pMediaType.sampleSize = vih.BmiHeader.ImageSize;

            return NOERROR;
        }

        public int DecideBufferSize(ref IMemAllocatorImpl pAlloc, ref AllocatorProperties prop)
        {
            AllocatorProperties _actual = new AllocatorProperties();

            BitmapInfoHeader _bmi = (BitmapInfoHeader)Pins[0].CurrentMediaType;
            prop.cbBuffer = _bmi.GetBitmapSize();
            if (prop.cbBuffer < _bmi.ImageSize)
                prop.cbBuffer = _bmi.ImageSize;

            prop.cBuffers = 1;

            int hr = pAlloc.SetProperties(prop, _actual);
            return hr;
        }

        public int FillBuffer(ref IMediaSampleImpl _sample)
        {
            BitmapInfoHeader _bmi = (BitmapInfoHeader)Pins[0].CurrentMediaType;
            if (m_hBitmap == IntPtr.Zero)
            {
                m_hBitmap = CreateCompatibleBitmap(m_hScreenDC, _bmi.Width, Math.Abs(_bmi.Height));
                m_bmi.bmiHeader = _bmi;
            }

            _sample.GetPointer(out IntPtr _ptr);

            IntPtr hOldBitmap = SelectObject(m_hMemDC, m_hBitmap);

            StretchBlt(m_hMemDC, 0, 0, m_nWidth, m_nHeight, m_hScreenDC, 0, 0, m_nMaxWidth, m_nMaxHeight, TernaryRasterOperations.SRCCOPY);

            SelectObject(m_hMemDC, hOldBitmap);

            GetDIBits(m_hMemDC, m_hBitmap, 0, (uint)m_nHeight, _ptr, ref m_bmi, 0);

            _sample.SetActualDataLength(_bmi.ImageSize);
            _sample.SetSyncPoint(true);
            long _stop = m_lLastSampleTime + m_nAvgTimePerFrame;
            _sample.SetTime((DsLong)m_lLastSampleTime, (DsLong)_stop);
            m_lLastSampleTime = _stop;
            return NOERROR;
        }

        #endregion

        #region API

        [StructLayout(LayoutKind.Sequential)]
        protected struct BitmapInfo
        {
            public BitmapInfoHeader bmiHeader;
            public int[] bmiColors;
        }

        private enum TernaryRasterOperations : uint
        {
            SRCCOPY = 0x00CC0020,
            SRCPAINT = 0x00EE0086,
            SRCAND = 0x008800C6,
            SRCINVERT = 0x00660046,
            SRCERASE = 0x00440328,
            NOTSRCCOPY = 0x00330008,
            NOTSRCERASE = 0x001100A6,
            MERGECOPY = 0x00C000CA,
            MERGEPAINT = 0x00BB0226,
            PATCOPY = 0x00F00021,
            PATPAINT = 0x00FB0A09,
            PATINVERT = 0x005A0049,
            DSTINVERT = 0x00550009,
            BLACKNESS = 0x00000042,
            WHITENESS = 0x00FF0062,
            CAPTUREBLT = 0x40000000
        }

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string lpszOutput, IntPtr lpInitData);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll", ExactSpelling = true, PreserveSig = true, SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool StretchBlt(IntPtr hdcDest, int nXOriginDest, int nYOriginDest,
            int nWidthDest, int nHeightDest,
            IntPtr hdcSrc, int nXOriginSrc, int nYOriginSrc, int nWidthSrc, int nHeightSrc,
            TernaryRasterOperations dwRop);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan,
           uint cScanLines, [Out] IntPtr lpvBits, ref BitmapInfo lpbmi, uint uUsage);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        #endregion
    }

    #endregion

}
