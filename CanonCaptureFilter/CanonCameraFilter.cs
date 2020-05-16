using DirectShow;
using DirectShow.BaseClasses;
using Sonic;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace CanonCaptureFilter
{
    [ComVisible(false)]
    public class CanonCameraStream : 
        SourceStream, 
        IAMStreamControl, 
        IKsPropertySet, 
        IAMPushSource, 
        IAMLatency, 
        IAMStreamConfig, 
        IAMBufferNegotiation
    {

        #region Constants

        public static HRESULT E_PROP_SET_UNSUPPORTED { get { unchecked { return (HRESULT)0x80070492; } } }
        public static HRESULT E_PROP_ID_UNSUPPORTED { get { unchecked { return (HRESULT)0x80070490; } } }

        #endregion

        #region Variables

        protected object m_csPinLock = new object();
        protected object m_csTimeLock = new object();
        protected long m_rtStart = 0;
        protected long m_rtStreamOffset = 0;
        protected long m_rtStreamOffsetMax = -1;
        protected long m_rtStartAt = -1;
        protected long m_rtStopAt = -1;
        protected int m_dwStopCookie = 0;
        protected int m_dwStartCookie = 0;
        protected bool m_bShouldFlush = false;
        protected bool m_bStartNotified = false;
        protected bool m_bStopNotified = false;
        protected AllocatorProperties m_pProperties = null;
        protected IReferenceClockImpl m_pClock = null;
        
        // Clock Token
        protected int m_dwAdviseToken = 0;
        
        // Clock Semaphore
        protected Semaphore m_hSemaphore = null;
        
        // Clock Start time
        protected long m_rtClockStart = 0;
        
        // Clock Stop time
        protected long m_rtClockStop = 0;

        #endregion

        #region Constructor

        public CanonCameraStream(string name, BaseSourceFilter _filter) : base(name, _filter)
        {
            m_mt.majorType = Guid.Empty;
            GetMediaType(0, ref m_mt);
        }

        #endregion

        #region Overridden Methods

        public override int SetMediaType(AMMediaType mt)
        {
            if (mt == null) return E_POINTER;

            if (mt.formatPtr == IntPtr.Zero) return VFW_E_INVALIDMEDIATYPE;

            HRESULT hr = (HRESULT)CheckMediaType(mt);
            if (hr.Failed) return hr;

            hr = (HRESULT)base.SetMediaType(mt);
            if (hr.Failed) return hr;

            if (m_pProperties != null)
                SuggestAllocatorProperties(m_pProperties);

            return (m_Filter as CanonCameraFilter).SetMediaType(mt);
        }

        public override int CheckMediaType(AMMediaType pmt) => (m_Filter as CanonCameraFilter).CheckMediaType(pmt);

        public override int GetMediaType(int iPosition, ref AMMediaType pMediaType) => (m_Filter as CanonCameraFilter).GetMediaType(iPosition, ref pMediaType);

        public override int DecideBufferSize(ref IMemAllocatorImpl pAlloc, ref AllocatorProperties pProperties)
        {
            if (!IsConnected) return VFW_E_NOT_CONNECTED;
            AllocatorProperties _actual = new AllocatorProperties();
            HRESULT hr = (HRESULT)GetAllocatorProperties(_actual);
            if (SUCCEEDED(hr) && _actual.cBuffers <= pProperties.cBuffers && _actual.cbBuffer <= pProperties.cbBuffer && _actual.cbAlign == pProperties.cbAlign)
            {
                AllocatorProperties Actual = new AllocatorProperties();
                hr = (HRESULT)pAlloc.SetProperties(pProperties, Actual);
                if (SUCCEEDED(hr))
                {
                    pProperties.cbAlign = Actual.cbAlign;
                    pProperties.cbBuffer = Actual.cbBuffer;
                    pProperties.cbPrefix = Actual.cbPrefix;
                    pProperties.cBuffers = Actual.cBuffers;
                }
            }
            return (m_Filter as CanonCameraFilter).DecideBufferSize(ref pAlloc, ref pProperties);
        }

        public override int Active()
        {
            m_rtStart = 0;
            m_bStartNotified = false;
            m_bStopNotified = false;
            {
                lock (m_Filter.FilterLock)
                {
                    m_pClock = m_Filter.Clock;
                    if (m_pClock.IsValid)
                    {
                        m_pClock._AddRef();
                        m_hSemaphore = new Semaphore(0, 0x7FFFFFFF);
                    }
                }
            }
            return base.Active();
        }

        public override int Inactive()
        {
            HRESULT hr = (HRESULT)base.Inactive();
            if (m_pClock != null)
            {
                if (m_dwAdviseToken != 0)
                {
                    m_pClock.Unadvise(m_dwAdviseToken);
                    m_dwAdviseToken = 0;
                }
                m_pClock._Release();
                m_pClock = null;
                if (m_hSemaphore != null)
                {
                    m_hSemaphore.Close();
                    m_hSemaphore = null;
                }
            }
            return hr;
        }

        public override int FillBuffer(ref IMediaSampleImpl _sample)
        {
            {
                if (S_OK == _sample.GetMediaType(out AMMediaType pmt))
                {
                    if (FAILED(SetMediaType(pmt)))
                    {
                        ASSERT(false);
                        _sample.SetMediaType(null);
                    }
                    pmt.Free();
                }
            }
            long _start, _stop;
            HRESULT hr = NOERROR;
            if (FAILED(GetLatency(out long rtLatency)))
                rtLatency = UNITS / 30;

            bool bShouldDeliver = false;
            do
            {
                if (m_dwAdviseToken == 0)
                {
                    m_pClock.GetTime(out m_rtClockStart);
                    hr = (HRESULT)m_pClock.AdvisePeriodic(m_rtClockStart + rtLatency, rtLatency, m_hSemaphore.Handle, out m_dwAdviseToken);
                    hr.Assert();
                }
                else
                {
                    if (!m_hSemaphore.WaitOne())
                        ASSERT(FALSE);
                }
                bShouldDeliver = TRUE;
                _start = m_rtStart;
                _stop = m_rtStart + 1;
                _sample.SetTime(_start, _stop);
                hr = (HRESULT)(m_Filter as CanonCameraFilter).FillBuffer(ref _sample);
                if (FAILED(hr) || S_FALSE == hr) return hr;

                m_pClock.GetTime(out m_rtClockStop);
                _sample.GetTime(out _start, out _stop);

                if (rtLatency > 0 && rtLatency * 3 < m_rtClockStop - m_rtClockStart)
                    m_rtClockStop = m_rtClockStart + rtLatency;

                _stop = _start + (m_rtClockStop - m_rtClockStart);
                m_rtStart = _stop;
                lock (m_csPinLock)
                {
                    _start -= m_rtStreamOffset;
                    _stop -= m_rtStreamOffset;
                }
                _sample.SetTime(_start, _stop);
                m_rtClockStart = m_rtClockStop;

                bShouldDeliver = ((_start >= 0) && (_stop >= 0));

                if (bShouldDeliver)
                {
                    lock (m_csPinLock)
                        if (m_rtStartAt != -1)
                        {
                            if (m_rtStartAt > _start)
                            {
                                bShouldDeliver = FALSE;
                            }
                            else
                            {
                                if (m_dwStartCookie != 0 && !m_bStartNotified)
                                {
                                    m_bStartNotified = TRUE;
                                    hr = (HRESULT)m_Filter.NotifyEvent(EventCode.StreamControlStarted, Marshal.GetIUnknownForObject(this), (IntPtr)m_dwStartCookie);
                                    if (FAILED(hr)) return hr;
                                }
                            }
                        }
                    if (!bShouldDeliver) continue;
                    if (m_rtStopAt != -1)
                    {
                        if (m_rtStopAt < _start)
                        {
                            if (!m_bStopNotified)
                            {
                                m_bStopNotified = TRUE;
                                if (m_dwStopCookie != 0)
                                {
                                    hr = (HRESULT)m_Filter.NotifyEvent(EventCode.StreamControlStopped, Marshal.GetIUnknownForObject(this), (IntPtr)m_dwStopCookie);
                                    if (FAILED(hr)) return hr;
                                }
                                bShouldDeliver = m_bShouldFlush;
                            }
                            else
                            {
                                bShouldDeliver = FALSE;
                            }
                            // EOS
                            if (!bShouldDeliver) return S_FALSE;
                        }
                    }
                }
            }
            while (!bShouldDeliver);

            return NOERROR;
        }

        #endregion

        #region IAMBufferNegotiation Members

        public int SuggestAllocatorProperties(AllocatorProperties pprop)
        {
            if (IsConnected) return VFW_E_ALREADY_CONNECTED;

            HRESULT hr = (HRESULT)(m_Filter as CanonCameraFilter).SuggestAllocatorProperties(pprop);
            if (FAILED(hr))
            {
                m_pProperties = null;
                return hr;
            }

            if (m_pProperties == null)
            {
                m_pProperties = new AllocatorProperties();
                (m_Filter as CanonCameraFilter).GetAllocatorProperties(m_pProperties);
            }
            if (pprop.cbBuffer != -1) m_pProperties.cbBuffer = pprop.cbBuffer;
            if (pprop.cbAlign != -1) m_pProperties.cbAlign = pprop.cbAlign;
            if (pprop.cbPrefix != -1) m_pProperties.cbPrefix = pprop.cbPrefix;
            if (pprop.cBuffers != -1) m_pProperties.cBuffers = pprop.cBuffers;
            return NOERROR;
        }

        public int GetAllocatorProperties(AllocatorProperties pprop)
        {
            if (pprop == null) return E_POINTER;

            if (m_pProperties != null)
            {
                pprop.cbAlign = m_pProperties.cbAlign;
                pprop.cbBuffer = m_pProperties.cbBuffer;
                pprop.cbPrefix = m_pProperties.cbPrefix;
                pprop.cBuffers = m_pProperties.cBuffers;
                return NOERROR;
            }

            if (IsConnected)
            {
                HRESULT hr = (HRESULT)Allocator.GetProperties(pprop);
                if (SUCCEEDED(hr) && pprop.cBuffers > 0 && pprop.cbBuffer > 0) return hr;
            }

            return (m_Filter as CanonCameraFilter).GetAllocatorProperties(pprop);
        }

        #endregion

        #region IAMStreamConfig Members

        public int SetFormat(AMMediaType pmt)
        {
            if (m_Filter.IsActive) return VFW_E_WRONG_STATE;
            HRESULT hr;
            AMMediaType newType = new AMMediaType(pmt);
            AMMediaType oldType = new AMMediaType(m_mt);
            hr = (HRESULT)CheckMediaType(newType);
            if (FAILED(hr)) return hr;
            m_mt.Set(newType);
            if (IsConnected)
            {
                hr = (HRESULT)Connected.QueryAccept(newType);
                if (SUCCEEDED(hr))
                {
                    hr = (HRESULT)m_Filter.ReconnectPin(this, newType);
                    if (SUCCEEDED(hr))
                    {
                        hr = (HRESULT)(m_Filter as CanonCameraFilter).SetMediaType(newType);
                    }
                    else
                    {
                        m_mt.Set(oldType);
                        m_Filter.ReconnectPin(this, oldType);
                    }
                }
            }
            else
            {
                hr = (HRESULT)(m_Filter as CanonCameraFilter).SetMediaType(newType);
            }
            return hr;
        }

        public int GetFormat(out AMMediaType pmt)
        {
            pmt = new AMMediaType(m_mt);
            return NOERROR;
        }

        public int GetNumberOfCapabilities(IntPtr piCount, IntPtr piSize)
        {
            HRESULT hr = (HRESULT)(m_Filter as CanonCameraFilter).GetNumberOfCapabilities(out int iCount, out int iSize);
            if (hr.Failed) return hr;

            if (piCount != IntPtr.Zero)
                Marshal.WriteInt32(piCount, iCount);

            if (piSize != IntPtr.Zero)
                Marshal.WriteInt32(piSize, iSize);

            return hr;
        }

        public int GetStreamCaps(int iIndex, IntPtr ppmt, IntPtr pSCC)
        {
            HRESULT hr = (HRESULT)(m_Filter as CanonCameraFilter).GetStreamCaps(iIndex, out AMMediaType pmt, out VideoStreamConfigCaps _caps);
            if (hr != S_OK) return hr;

            if (ppmt != IntPtr.Zero)
            {
                IntPtr _ptr = Marshal.AllocCoTaskMem(Marshal.SizeOf(pmt));
                Marshal.StructureToPtr(pmt, _ptr, true);
                Marshal.WriteIntPtr(ppmt, _ptr);
            }

            if (pSCC != IntPtr.Zero)
                Marshal.StructureToPtr(_caps, pSCC, false);

            return hr;
        }

        #endregion

        #region IAMPushSource Members

        public int GetPushSourceFlags(out AMPushSourceFlags pFlags)
        {
            pFlags = AMPushSourceFlags.None;
            return NOERROR;
        }

        public int SetPushSourceFlags(AMPushSourceFlags Flags)
        {
            return E_NOTIMPL;
        }

        public int SetStreamOffset(long rtOffset)
        {
            lock (m_csPinLock)
            {
                m_rtStreamOffset = rtOffset;
                if (m_rtStreamOffset > m_rtStreamOffsetMax) m_rtStreamOffsetMax = m_rtStreamOffset;
            }
            return NOERROR;
        }

        public int GetStreamOffset(out long prtOffset)
        {
            prtOffset = m_rtStreamOffset;
            return NOERROR;
        }

        public int GetMaxStreamOffset(out long prtMaxOffset)
        {
            prtMaxOffset = 0;
            if (m_rtStreamOffsetMax == -1)
            {
                HRESULT hr = (HRESULT)GetLatency(out m_rtStreamOffsetMax);
                if (FAILED(hr)) return hr;
                if (m_rtStreamOffsetMax < m_rtStreamOffset) m_rtStreamOffsetMax = m_rtStreamOffset;
            }
            prtMaxOffset = m_rtStreamOffsetMax;
            return NOERROR;
        }

        public int SetMaxStreamOffset(long rtMaxOffset)
        {
            if (rtMaxOffset < m_rtStreamOffset) return E_INVALIDARG;
            m_rtStreamOffsetMax = rtMaxOffset;
            return NOERROR;
        }

        #endregion

        #region IKsPropertySet Members

        public int Set(Guid guidPropSet, int dwPropID, IntPtr pInstanceData, int cbInstanceData, IntPtr pPropData, int cbPropData)
        {
            return E_NOTIMPL;
        }

        public int Get(Guid guidPropSet, int dwPropID, IntPtr pInstanceData, int cbInstanceData, IntPtr pPropData, int cbPropData, out int pcbReturned)
        {
            pcbReturned = Marshal.SizeOf(typeof(Guid));

            if (guidPropSet != PropSetID.Pin)
                return E_PROP_SET_UNSUPPORTED;

            if (dwPropID != (int)AMPropertyPin.Category)
                return E_PROP_ID_UNSUPPORTED;

            if (pPropData == IntPtr.Zero)
                return NOERROR;

            if (cbPropData < Marshal.SizeOf(typeof(Guid)))
                return E_UNEXPECTED;

            Marshal.StructureToPtr(PinCategory.Capture, pPropData, false);
            return NOERROR;
        }

        public int QuerySupported(Guid guidPropSet, int dwPropID, out KSPropertySupport pTypeSupport)
        {
            pTypeSupport = KSPropertySupport.Get;
            if (guidPropSet != PropSetID.Pin)
                return E_PROP_SET_UNSUPPORTED;

            if (dwPropID != (int)AMPropertyPin.Category)
                return E_PROP_ID_UNSUPPORTED;

            return S_OK;
        }

        #endregion

        #region IAMStreamControl Members

        public int StartAt(DsLong ptStart, int dwCookie)
        {
            lock (m_csPinLock)
            {
                if (ptStart != null && ptStart != MAX_LONG)
                {
                    m_rtStartAt = ptStart;
                    m_dwStartCookie = dwCookie;
                }
                else
                {
                    m_rtStartAt = -1;
                    m_dwStartCookie = 0;
                }
            }
            return NOERROR;
        }

        public int StopAt(DsLong ptStop, bool bSendExtra, int dwCookie)
        {
            lock (m_csPinLock)
            {
                if (ptStop != null && ptStop != MAX_LONG)
                {
                    m_rtStopAt = ptStop;
                    m_bShouldFlush = bSendExtra;
                    m_dwStopCookie = dwCookie;
                }
                else
                {
                    m_rtStopAt = -1;
                    m_bShouldFlush = false;
                    m_dwStopCookie = 0;
                }
            }
            return NOERROR;
        }

        public int GetInfo(out AMStreamInfo pInfo)
        {
            lock (m_csPinLock)
            {
                pInfo = new AMStreamInfo
                {
                    dwFlags = AMStreamInfoFlags.None
                };

                if (m_rtStart < m_rtStartAt)
                    pInfo.dwFlags = pInfo.dwFlags | AMStreamInfoFlags.Discarding;

                if (m_rtStartAt != -1)
                {
                    pInfo.dwFlags = pInfo.dwFlags | AMStreamInfoFlags.StartDefined;
                    pInfo.tStart = m_rtStartAt;
                    pInfo.dwStartCookie = m_dwStartCookie;
                }
                if (m_rtStopAt != -1)
                {
                    pInfo.dwFlags = pInfo.dwFlags | AMStreamInfoFlags.StopDefined;
                    pInfo.tStop = m_rtStopAt;
                    pInfo.dwStopCookie = m_dwStopCookie;
                }
                if (m_bShouldFlush) pInfo.dwFlags = pInfo.dwFlags | AMStreamInfoFlags.StopSendExtra;
            }
            return NOERROR;
        }

        #endregion

        #region IAMLatency Members

        public int GetLatency(out long prtLatency) => (m_Filter as CanonCameraFilter).GetLatency(out prtLatency);

        #endregion
    }

    [ComVisible(true)]
    [Guid("B5F4C45B-7286-4B2A-986F-D230B23C2576")]
    [AMovieSetup(Merit.Normal, AMovieSetup.CLSID_VideoInputDeviceCategory)]
    [PropPageSetup(typeof(FilterAboutForm))]
    public class CanonCameraFilter : BaseSourceFilter, IAMFilterMiscFlags
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

        public CanonCameraFilter() : base("Canon Camera Capture")
        {
            m_bmi.bmiHeader = new BitmapInfoHeader();
            AddPin(new CanonCameraStream("Capture", this));
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