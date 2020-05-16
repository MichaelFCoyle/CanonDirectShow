using DirectShow;
using DirectShow.BaseClasses;
using EDSDK_NET;
using Sonic;
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace CanonCaptureFilter
{
    [ComVisible(true)]
    [Guid("0E507CD8-A5FA-45E7-A3C2-5ACAF2E35C8A")]
    [AMovieSetup(Merit.Normal, AMovieSetup.CLSID_VideoInputDeviceCategory)]
    [PropPageSetup(typeof(FilterAboutForm))]
    public class CanonCameraFilter2 : BaseSourceFilter
    {
        public CanonCameraFilter2() :base("Canon Camera Capture 2")
        {

        }

        protected override int OnInitializePins()
        {
            return AddPin(new CanonSourceStream("Capture", this));
        }

    }

    [ComVisible(false)]
    public class CanonSourceStream : SourceStream, IAMPushSource
    {
        SDKHandler m_CameraHandler = new SDKHandler();
        BitmapInfo m_bmi = new BitmapInfo() { bmiHeader = new BitmapInfoHeader() };
        Bitmap m_lastFrame;

        int m_width;
        int m_height;
        int m_bitsPerPixel;

        protected IntPtr m_hMemDC = IntPtr.Zero;
        protected IntPtr m_hBitmap = IntPtr.Zero;

        public CanonSourceStream(string name, BaseSourceFilter source):base(name,source)
        {
            m_CameraHandler.CameraAdded += SDK_CameraAdded;
            m_CameraHandler.LiveViewUpdated += SDK_LiveViewUpdated;
            m_CameraHandler.ProgressChanged += SDK_ProgressChanged;
            m_CameraHandler.CameraHasShutdown += SDK_CameraHasShutdown;
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

            AllocatorProperties _actual = new AllocatorProperties();
            HRESULT hr = (HRESULT)GetAllocatorProperties(_actual);
            if (SUCCEEDED(hr) && _actual.cBuffers <= prop.cBuffers && _actual.cbBuffer <= prop.cbBuffer && _actual.cbAlign == prop.cbAlign)
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
            return pAlloc.SetProperties(prop, _actual);
        }

        public override int FillBuffer(ref IMediaSampleImpl _sample)
        {
            _sample.GetPointer(out IntPtr _ptr);

            using (Graphics g = Graphics.FromImage(m_lastFrame))
            {
                m_hMemDC = Api.CreateCompatibleDC(g.GetHdc());
                m_hBitmap = Api.CreateCompatibleBitmap(m_hMemDC, m_width, Math.Abs(m_height));

                Api.GetDIBits(m_hMemDC, m_hBitmap, 0, (uint)Math.Abs(m_height), _ptr, ref m_bmi, 0);


            }

            _sample.SetActualDataLength(_sample.GetSize());
            _sample.SetSyncPoint(true);
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
            if (m_lastFrame == null)
                return base.GetMediaType(ref pMediaType);

            pMediaType.majorType = MediaType.Video;
            pMediaType.formatType = FormatType.VideoInfo;
            pMediaType.temporalCompression = false;


            VideoInfoHeader vih = new VideoInfoHeader
            {
                AvgTimePerFrame = UNITS / 30
            };
            vih.BmiHeader.Compression = BI_RGB;
            vih.BmiHeader.BitCount = (short)m_bitsPerPixel;
            vih.BmiHeader.Width = m_width;
            vih.BmiHeader.Height = m_height;
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

        #region IAMBufferNegotiation Members

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

        AllocatorProperties m_pProperties;
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

        #region camera event handlers

        private void SDK_CameraHasShutdown(object sender, EventArgs e)
        {
        }

        private void SDK_ProgressChanged(int Progress)
        {
        }


        private void SDK_LiveViewUpdated(Stream stream)
        {
            try
            {
                try
                {
                    if (m_lastFrame != null)
                        m_lastFrame.Dispose();
                    m_width = 0;
                    m_height = 0;
                    m_bitsPerPixel = 0;
                }
                catch { }

                m_lastFrame = new Bitmap(stream);

                // figure out the size, bit depth etc?
                m_width = m_lastFrame.Width;
                m_height = m_lastFrame.Height;
                m_bitsPerPixel = Image.GetPixelFormatSize(m_lastFrame.PixelFormat);

                
            }
            catch
            {

            }
        }

        private void SDK_CameraAdded()
        {
        }

        #endregion

        #region IAMPushSource

        public int GetLatency(out long prtLatency)
        {
            throw new NotImplementedException();
        }

        public int GetPushSourceFlags([Out] out AMPushSourceFlags pFlags)
        {
            throw new NotImplementedException();
        }

        public int SetPushSourceFlags([In] AMPushSourceFlags Flags)
        {
            throw new NotImplementedException();
        }

        public int SetStreamOffset([In] long rtOffset)
        {
            throw new NotImplementedException();
        }

        public int GetStreamOffset([Out] out long prtOffset)
        {
            throw new NotImplementedException();
        }

        public int GetMaxStreamOffset([Out] out long prtMaxOffset)
        {
            throw new NotImplementedException();
        }

        public int SetMaxStreamOffset([In] long rtMaxOffset)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}