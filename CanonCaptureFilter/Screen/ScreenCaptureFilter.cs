using DirectShow;
using DirectShow.BaseClasses;
using Sonic;
using System;
using System.Runtime.InteropServices;

namespace CanonCaptureFilter
{

    [ComVisible(true)]
    [Guid("B5F4C45B-7286-4B2A-986F-D230B23C2576")]
    [AMovieSetup(Merit.Normal, AMovieSetup.CLSID_VideoInputDeviceCategory)]
    [PropPageSetup(typeof(FilterAboutForm))]
    public class ScreenCaptureFilter : BaseSourceFilter, IAMFilterMiscFlags
    {
        #region Constants

        private const int c_iDefaultWidth = 1024;
        private const int c_iDefaultHeight = 756;
        private const int c_nDefaultBitCount = 32;
        private const int c_iDefaultFPS = 20;
        private const int c_iFormatsCount = 8;
        private const int c_nGranularityW = 160;
        private const int c_nGranularityH = 120;
        private const int c_nMinWidth = 320;
        private const int c_nMinHeight = 240;
        private const int c_nMaxWidth = c_nMinWidth + c_nGranularityW * (c_iFormatsCount - 1);
        private const int c_nMaxHeight = c_nMinHeight + c_nGranularityH * (c_iFormatsCount - 1);
        private const int c_nMinFPS = 1;
        private const int c_nMaxFPS = 30;

        #endregion

        #region Variables

        protected int m_nWidth = c_iDefaultWidth;
        protected int m_nHeight = c_iDefaultHeight;
        protected int m_nBitCount = c_nDefaultBitCount;
        protected long m_nAvgTimePerFrame = UNITS / c_iDefaultFPS;

        protected IntPtr m_hScreenDC = IntPtr.Zero;
        protected IntPtr m_hMemDC = IntPtr.Zero;
        protected IntPtr m_hBitmap = IntPtr.Zero;
        protected BitmapInfo m_bmi = new BitmapInfo();

        protected int m_nMaxWidth = 0;
        protected int m_nMaxHeight = 0;

        #endregion

        #region Constructor

        public ScreenCaptureFilter() : base("Screen Capture")
        {
            m_bmi.bmiHeader = new BitmapInfoHeader();
            AddPin(new ScreenCaptureStream("Capture", this));
        }

        #endregion

        #region Overridden Methods

        protected override int OnInitializePins() => NOERROR;

        public override int Pause()
        {
            if (m_State == FilterState.Stopped)
            {
                m_hScreenDC = Api.CreateDC("DISPLAY", null, null, IntPtr.Zero);
                m_nMaxWidth = Api.GetDeviceCaps(m_hScreenDC, 8); // HORZRES
                m_nMaxHeight = Api.GetDeviceCaps(m_hScreenDC, 10); // VERTRES
                m_hMemDC = Api.CreateCompatibleDC(m_hScreenDC);
                m_hBitmap = Api.CreateCompatibleBitmap(m_hScreenDC, m_nWidth, Math.Abs(m_nHeight));
            }
            return base.Pause();
        }

        public override int Stop()
        {
            int hr = base.Stop();
            if (m_hBitmap != IntPtr.Zero)
            {
                Api.DeleteObject(m_hBitmap);
                m_hBitmap = IntPtr.Zero;
            }
            if (m_hScreenDC != IntPtr.Zero)
            {
                Api.DeleteDC(m_hScreenDC);
                m_hScreenDC = IntPtr.Zero;
            }
            if (m_hMemDC != IntPtr.Zero)
            {
                Api.DeleteDC(m_hMemDC);
                m_hMemDC = IntPtr.Zero;
            }
            return hr;
        }

        #endregion

        #region Methods

        public int CheckMediaType(AMMediaType pmt)
        {
            if (pmt == null) return E_POINTER;
            if (pmt.formatPtr == IntPtr.Zero) return VFW_E_INVALIDMEDIATYPE;
            if (pmt.majorType != MediaType.Video)
                return VFW_E_INVALIDMEDIATYPE;

            if (pmt.subType != MediaSubType.RGB24 && pmt.subType != MediaSubType.RGB32 && pmt.subType != MediaSubType.ARGB32)
                return VFW_E_INVALIDMEDIATYPE;

            BitmapInfoHeader _bmi = pmt;
            if (_bmi == null)
                return E_UNEXPECTED;
            if (_bmi.Compression != BI_RGB)
                return VFW_E_TYPE_NOT_ACCEPTED;
            if (_bmi.BitCount != 24 && _bmi.BitCount != 32)
                return VFW_E_TYPE_NOT_ACCEPTED;

            GetDefaultCaps(0, out VideoStreamConfigCaps _caps);
            if (_bmi.Width < _caps.MinOutputSize.Width || _bmi.Width > _caps.MaxOutputSize.Width)
                return VFW_E_INVALIDMEDIATYPE;

            long _rate = 0;
            {
                VideoInfoHeader _pvi = pmt;
                if (_pvi != null)
                    _rate = _pvi.AvgTimePerFrame;
            }
            {
                VideoInfoHeader2 _pvi = pmt;
                if (_pvi != null)
                    _rate = _pvi.AvgTimePerFrame;
            }
            if (_rate < _caps.MinFrameInterval || _rate > _caps.MaxFrameInterval)
                return VFW_E_INVALIDMEDIATYPE;
            return NOERROR;
        }

        public int SetMediaType(AMMediaType pmt)
        {
            lock (m_Lock)
            {
                if (m_hBitmap != IntPtr.Zero)
                {
                    Api.DeleteObject(m_hBitmap);
                    m_hBitmap = IntPtr.Zero;
                }

                BitmapInfoHeader _bmi = pmt;
                m_bmi.bmiHeader.BitCount = _bmi.BitCount;
                if (_bmi.Height != 0) m_bmi.bmiHeader.Height = _bmi.Height;
                if (_bmi.Width > 0) m_bmi.bmiHeader.Width = _bmi.Width;
                m_bmi.bmiHeader.Compression = BI_RGB;
                m_bmi.bmiHeader.Planes = 1;
                m_bmi.bmiHeader.ImageSize = ALIGN16(m_bmi.bmiHeader.Width) * ALIGN16(Math.Abs(m_bmi.bmiHeader.Height)) * m_bmi.bmiHeader.BitCount / 8;
                m_nWidth = _bmi.Width;
                m_nHeight = _bmi.Height;
                m_nBitCount = _bmi.BitCount;

                {
                    VideoInfoHeader _pvi = pmt;
                    if (_pvi != null)
                        m_nAvgTimePerFrame = _pvi.AvgTimePerFrame;
                }
                {
                    VideoInfoHeader2 _pvi = pmt;
                    if (_pvi != null)
                        m_nAvgTimePerFrame = _pvi.AvgTimePerFrame;
                }
            }
            return NOERROR;
        }

        public int GetMediaType(int iPosition, ref AMMediaType pMediaType)
        {
            if (iPosition < 0) return E_INVALIDARG;
            GetDefaultCaps(0, out VideoStreamConfigCaps _caps);

            int nWidth = 0;
            int nHeight = 0;

            if (iPosition == 0)
            {
                if (Pins.Count > 0 && Pins[0].CurrentMediaType.majorType == MediaType.Video)
                {
                    pMediaType.Set(Pins[0].CurrentMediaType);
                    return NOERROR;
                }
                nWidth = _caps.InputSize.Width;
                nHeight = _caps.InputSize.Height;
            }
            else
            {
                iPosition--;
                nWidth = _caps.MinOutputSize.Width + _caps.OutputGranularityX * iPosition;
                nHeight = _caps.MinOutputSize.Height + _caps.OutputGranularityY * iPosition;
                if (nWidth > _caps.MaxOutputSize.Width || nHeight > _caps.MaxOutputSize.Height)
                {
                    return VFW_S_NO_MORE_ITEMS;
                }
            }

            pMediaType.majorType = MediaType.Video;
            pMediaType.formatType = FormatType.VideoInfo;

            VideoInfoHeader vih = new VideoInfoHeader
            {
                AvgTimePerFrame = m_nAvgTimePerFrame
            };
            vih.BmiHeader.Compression = BI_RGB;
            vih.BmiHeader.BitCount = (short)m_nBitCount;
            vih.BmiHeader.Width = nWidth;
            vih.BmiHeader.Height = nHeight;
            vih.BmiHeader.Planes = 1;
            vih.BmiHeader.ImageSize = vih.BmiHeader.Width * Math.Abs(vih.BmiHeader.Height) * vih.BmiHeader.BitCount / 8;

            if (vih.BmiHeader.BitCount == 32)
                pMediaType.subType = MediaSubType.RGB32;
            if (vih.BmiHeader.BitCount == 24)
                pMediaType.subType = MediaSubType.RGB24;
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
            if (prop.cbBuffer < m_bmi.bmiHeader.ImageSize)
                prop.cbBuffer = m_bmi.bmiHeader.ImageSize;

            prop.cBuffers = 1;
            prop.cbAlign = 1;
            prop.cbPrefix = 0;
            int hr = pAlloc.SetProperties(prop, _actual);
            return hr;
        }

        public int FillBuffer(ref IMediaSampleImpl _sample)
        {
            try
            {
                if (m_hBitmap == IntPtr.Zero)
                    m_hBitmap = Api.CreateCompatibleBitmap(m_hScreenDC, m_nWidth, Math.Abs(m_nHeight));
                _sample.GetPointer(out IntPtr _ptr);

                IntPtr hOldBitmap = Api.SelectObject(m_hMemDC, m_hBitmap);

                Api.StretchBlt(m_hMemDC, 0, 0, m_nWidth, Math.Abs(m_nHeight), m_hScreenDC, 0, 0, m_nMaxWidth, m_nMaxHeight, Api.TernaryRasterOperations.SRCCOPY);

                Api.SelectObject(m_hMemDC, hOldBitmap);

                Api.GetDIBits(m_hMemDC, m_hBitmap, 0, (uint)Math.Abs(m_nHeight), _ptr, ref m_bmi, 0);

                _sample.SetActualDataLength(_sample.GetSize());
                _sample.SetSyncPoint(true);
                return NOERROR;
            }
            catch (Exception ex)
            {
                return E_UNEXPECTED;
            }
        }

        public int GetLatency(out long prtLatency)
        {
            prtLatency = UNITS / 30;
            AMMediaType mt = Pins[0].CurrentMediaType;
            if (mt.majorType == MediaType.Video)
            {
                {
                    VideoInfoHeader _pvi = mt;
                    if (_pvi != null)
                        prtLatency = _pvi.AvgTimePerFrame;
                }
                {
                    VideoInfoHeader2 _pvi = mt;
                    if (_pvi != null)
                        prtLatency = _pvi.AvgTimePerFrame;
                }
            }
            return NOERROR;
        }

        public int GetNumberOfCapabilities(out int iCount, out int iSize)
        {
            iCount = 0;
            AMMediaType mt = new AMMediaType();
            while (GetMediaType(iCount, ref mt) == S_OK) { mt.Free(); iCount++; };
            iSize = Marshal.SizeOf(typeof(VideoStreamConfigCaps));
            return NOERROR;
        }

        public int GetStreamCaps(int iIndex, out AMMediaType ppmt, out VideoStreamConfigCaps _caps)
        {
            ppmt = null;
            _caps = null;
            if (iIndex < 0) return E_INVALIDARG;

            ppmt = new AMMediaType();
            HRESULT hr = (HRESULT)GetMediaType(iIndex, ref ppmt);
            if (FAILED(hr)) return hr;
            if (hr == VFW_S_NO_MORE_ITEMS) return S_FALSE;
            hr = (HRESULT)GetDefaultCaps(iIndex, out _caps);
            return hr;
        }

        public int SuggestAllocatorProperties(AllocatorProperties pprop)
        {
            AllocatorProperties _properties = new AllocatorProperties();
            HRESULT hr = (HRESULT)GetAllocatorProperties(_properties);
            if (FAILED(hr)) return hr;
            if (pprop.cbBuffer != -1)
                if (pprop.cbBuffer < _properties.cbBuffer) return E_FAIL;
            if (pprop.cbAlign != -1 && pprop.cbAlign != _properties.cbAlign) return E_FAIL;
            if (pprop.cbPrefix != -1 && pprop.cbPrefix != _properties.cbPrefix) return E_FAIL;
            if (pprop.cBuffers != -1 && pprop.cBuffers < 1) return E_FAIL;
            return NOERROR;
        }

        public int GetAllocatorProperties(AllocatorProperties pprop)
        {
            AMMediaType mt = Pins[0].CurrentMediaType;
            if (mt.majorType == MediaType.Video)
            {
                int lSize = mt.sampleSize;
                BitmapInfoHeader _bmi = mt;
                if (_bmi != null)
                {
                    if (lSize < _bmi.GetBitmapSize())
                        lSize = _bmi.GetBitmapSize();
                    if (lSize < _bmi.ImageSize)
                        lSize = _bmi.ImageSize;
                }
                pprop.cbBuffer = lSize;
                pprop.cBuffers = 1;
                pprop.cbAlign = 1;
                pprop.cbPrefix = 0;

            }
            return NOERROR;
        }

        public int GetDefaultCaps(int nIndex, out VideoStreamConfigCaps caps)
        {
            caps = new VideoStreamConfigCaps
            {
                guid = FormatType.VideoInfo,
                VideoStandard = AnalogVideoStandard.None
            };
            caps.InputSize.Width = c_iDefaultWidth;
            caps.InputSize.Height = c_iDefaultHeight;
            caps.MinCroppingSize.Width = c_nMinWidth;
            caps.MinCroppingSize.Height = c_nMinHeight;

            caps.MaxCroppingSize.Width = c_nMaxWidth;
            caps.MaxCroppingSize.Height = c_nMaxHeight;
            caps.CropGranularityX = c_nGranularityW;
            caps.CropGranularityY = c_nGranularityH;
            caps.CropAlignX = 0;
            caps.CropAlignY = 0;

            caps.MinOutputSize.Width = caps.MinCroppingSize.Width;
            caps.MinOutputSize.Height = caps.MinCroppingSize.Height;
            caps.MaxOutputSize.Width = caps.MaxCroppingSize.Width;
            caps.MaxOutputSize.Height = caps.MaxCroppingSize.Height;
            caps.OutputGranularityX = caps.CropGranularityX;
            caps.OutputGranularityY = caps.CropGranularityY;
            caps.StretchTapsX = 0;
            caps.StretchTapsY = 0;
            caps.ShrinkTapsX = 0;
            caps.ShrinkTapsY = 0;
            caps.MinFrameInterval = UNITS / c_nMaxFPS;
            caps.MaxFrameInterval = UNITS / c_nMinFPS;
            caps.MinBitsPerSecond = (caps.MinOutputSize.Width * caps.MinOutputSize.Height * c_nDefaultBitCount) * c_nMinFPS;
            caps.MaxBitsPerSecond = (caps.MaxOutputSize.Width * caps.MaxOutputSize.Height * c_nDefaultBitCount) * c_nMaxFPS;

            return NOERROR;
        }

        #endregion

        #region IAMFilterMiscFlags Members

        public int GetMiscFlags() => (int)AMFilterMiscFlags.IsSource;

        #endregion

    }
}