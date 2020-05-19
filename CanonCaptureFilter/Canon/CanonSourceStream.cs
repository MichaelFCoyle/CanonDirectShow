using DirectShow;
using DirectShow.BaseClasses;
using Sonic;
using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace CanonCaptureFilter
{
    [ComVisible(false)]
    public class CanonSourceStream :
        SourceStream,
        IAMPushSource,
        IKsPropertySet,
        IAMBufferNegotiation
    {
        #region fields

        BitmapInfo m_bmi = new BitmapInfo() { bmiHeader = new BitmapInfoHeader() };

        byte[] m_bitmapBytes;

        AllocatorProperties m_pProperties;

        readonly object m_csPinLock = new object();

        protected long m_rtStreamOffset = 0;
        protected long m_rtStreamOffsetMax = -1;

        #endregion

        #region constants

        public static HRESULT E_PROP_SET_UNSUPPORTED { get { unchecked { return (HRESULT)0x80070492; } } }
        public static HRESULT E_PROP_ID_UNSUPPORTED { get { unchecked { return (HRESULT)0x80070490; } } }

        #endregion

        public CanonSourceStream(string name, BaseSourceFilter source) : base(name, source)
        {
            Controller.Initialize();
            Controller.FrameReceived += FrameReceived;
        }

        private void FrameReceived(object sender, Bitmap bitmap)
        {
            try
            {
                lock (m_Filter.FilterLock)
                {
                    //m_bitmapBytes = bitmap.ModifiedBitmap(RotateFlipType.Rotate180FlipNone).GetBytes();
                    m_bitmapBytes = bitmap.GetBytes();
                }
            }
            catch { }
        }

        public override int Active()
        {
            Controller.Start();
            return base.Active();
        }

        public override int Inactive()
        {
            HRESULT hr = (HRESULT)base.Inactive();
            Controller.Stop();
            return hr;
        }

        /// <summary>
        /// Called after the format has been decided
        /// </summary>
        /// <param name="pAlloc"></param>
        /// <param name="prop"></param>
        /// <returns></returns>
        public override int DecideBufferSize(ref IMemAllocatorImpl pAlloc, ref AllocatorProperties prop)
        {
            if (!IsConnected) return VFW_E_NOT_CONNECTED;

            AllocatorProperties actual = new AllocatorProperties();
            HRESULT hr = (HRESULT)GetAllocatorProperties(actual);
            if (SUCCEEDED(hr) && actual.cBuffers <= prop.cBuffers && actual.cbBuffer <= prop.cbBuffer && actual.cbAlign == prop.cbAlign)
            {
                AllocatorProperties Actual = new AllocatorProperties();
                hr = (HRESULT)pAlloc.SetProperties(prop, Actual);
                if (SUCCEEDED(hr))
                {
                    prop.cbAlign = Actual.cbAlign;
                    prop.cbBuffer = Actual.cbBuffer;
                    prop.cbPrefix = Actual.cbPrefix;
                    prop.cBuffers = Actual.cBuffers;
                }
            }

            BitmapInfoHeader _bmi = (BitmapInfoHeader)this.CurrentMediaType;
            prop.cbBuffer = _bmi.GetBitmapSize();

            if (prop.cbBuffer < _bmi.ImageSize)
                prop.cbBuffer = _bmi.ImageSize;

            if (prop.cbBuffer < m_bmi.bmiHeader.ImageSize)
                prop.cbBuffer = m_bmi.bmiHeader.ImageSize;

            prop.cBuffers = 1;
            prop.cbAlign = 1;
            prop.cbPrefix = 0;
            return pAlloc.SetProperties(prop, actual);
        }

        public override int FillBuffer(ref IMediaSampleImpl sample)
        {
            sample.GetPointer(out IntPtr ptr);

            lock (m_Filter.FilterLock)
            {
                if (m_bitmapBytes == null)
                    m_bitmapBytes = new byte[m_bmi.bmiHeader.ImageSize];

                Marshal.Copy(m_bitmapBytes, 0, ptr, m_bitmapBytes.Length);

                m_bitmapBytes = null;
            }

            sample.SetActualDataLength(sample.GetSize());
            sample.SetSyncPoint(true);
            return NOERROR;
        }

        /// <summary>
        /// Returns the media type that we will be rendering back to the base class.  
        /// This is where we actually load the video pipeline, we do it up front so that
        /// we have the correct image size for the buffer
        /// </summary>
        /// <param name="pMediaType"></param>
        /// <returns></returns>
        public override int GetMediaType(ref AMMediaType pMediaType)
        {
            pMediaType.majorType = MediaType.Video;
            pMediaType.formatType = FormatType.VideoInfo;
            pMediaType.temporalCompression = false;
            var FPS = (long)Math.Round(Controller.FPS);
            if (FPS == 0) FPS = 15;
            VideoInfoHeader vih = new VideoInfoHeader
            {
                AvgTimePerFrame = UNITS / FPS
            };
            vih.BmiHeader.Compression = BI_RGB;
            vih.BmiHeader.BitCount = (short)Controller.BitDepth;
            vih.BmiHeader.Width = Controller.Width;
            vih.BmiHeader.Height = Controller.Height;
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

        #region IAMBufferNegotiation 

        public int SuggestAllocatorProperties(AllocatorProperties pprop)
        {
            if (IsConnected) return VFW_E_ALREADY_CONNECTED;

            AllocatorProperties _properties = new AllocatorProperties();
            HRESULT hr = (HRESULT)GetAllocatorProperties(_properties);
            if (FAILED(hr)) return hr;
            if (pprop.cbBuffer != -1)
                if (pprop.cbBuffer < _properties.cbBuffer) return E_FAIL;
            if (pprop.cbAlign != -1 && pprop.cbAlign != _properties.cbAlign) return E_FAIL;
            if (pprop.cbPrefix != -1 && pprop.cbPrefix != _properties.cbPrefix) return E_FAIL;
            if (pprop.cBuffers != -1 && pprop.cBuffers < 1) return E_FAIL;

            if (m_pProperties == null)
            {
                m_pProperties = new AllocatorProperties();
                GetAllocatorProperties(m_pProperties);
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

            AMMediaType mt = this.CurrentMediaType;
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

        #endregion

        #region IAMPushSource

        public int GetLatency(out long prtLatency)
        {
            prtLatency = UNITS / 30;
            AMMediaType mt = CurrentMediaType;
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

        public int GetPushSourceFlags([Out] out AMPushSourceFlags pFlags)
        {
            pFlags = AMPushSourceFlags.None;
            return NOERROR;
        }

        public int SetPushSourceFlags([In] AMPushSourceFlags Flags) => E_NOTIMPL;

        public int SetStreamOffset([In] long rtOffset)
        {
            lock (m_csPinLock)
            {
                m_rtStreamOffset = rtOffset;
                if (m_rtStreamOffset > m_rtStreamOffsetMax) m_rtStreamOffsetMax = m_rtStreamOffset;
            }
            return NOERROR;
        }

        public int GetStreamOffset([Out] out long prtOffset)
        {
            prtOffset = 0;
            if (m_rtStreamOffsetMax == -1)
            {
                HRESULT hr = (HRESULT)GetLatency(out m_rtStreamOffsetMax);
                if (FAILED(hr)) return hr;
                if (m_rtStreamOffsetMax < m_rtStreamOffset) m_rtStreamOffsetMax = m_rtStreamOffset;
            }
            prtOffset = m_rtStreamOffsetMax;
            return NOERROR;
        }

        public int GetMaxStreamOffset([Out] out long prtMaxOffset)
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

        public int SetMaxStreamOffset([In] long rtMaxOffset)
        {
            if (rtMaxOffset < m_rtStreamOffset) return E_INVALIDARG;
            m_rtStreamOffsetMax = rtMaxOffset;
            return NOERROR;
        }

        #endregion

        #region IKsPropertySet Members

        public int Set(Guid guidPropSet, int dwPropID, IntPtr pInstanceData, int cbInstanceData, IntPtr pPropData, int cbPropData) => E_NOTIMPL;

        public int Get(Guid guidPropSet, int dwPropID, IntPtr pInstanceData, int cbInstanceData, IntPtr pPropData, int cbPropData, out int pcbReturned)
        {
            pcbReturned = Marshal.SizeOf(typeof(Guid));

            if (guidPropSet != PropSetID.Pin) return E_PROP_SET_UNSUPPORTED;

            if (dwPropID != (int)AMPropertyPin.Category) return E_PROP_ID_UNSUPPORTED;

            if (pPropData == IntPtr.Zero) return NOERROR;

            if (cbPropData < Marshal.SizeOf(typeof(Guid))) return E_UNEXPECTED;

            Marshal.StructureToPtr(PinCategory.Capture, pPropData, false);
            return NOERROR;
        }

        public int QuerySupported(Guid guidPropSet, int dwPropID, out KSPropertySupport pTypeSupport)
        {
            pTypeSupport = KSPropertySupport.Get;
            if (guidPropSet != PropSetID.Pin) return E_PROP_SET_UNSUPPORTED;
            if (dwPropID != (int)AMPropertyPin.Category) return E_PROP_ID_UNSUPPORTED;
            return S_OK;
        }

        #endregion

    }
}