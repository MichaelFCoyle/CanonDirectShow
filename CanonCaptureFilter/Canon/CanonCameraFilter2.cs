using DirectShow;
using DirectShow.BaseClasses;
using System;
using System.Runtime.InteropServices;

namespace CanonCaptureFilter
{
    [ComVisible(true)]
    [Guid("0E507CD8-A5FA-45E7-A3C2-5ACAF2E35C8A")]
    [AMovieSetup(Merit.Normal, AMovieSetup.CLSID_VideoInputDeviceCategory)]
    [PropPageSetup(typeof(CanonAboutForm))]
    public class CanonCameraFilter2 : BaseSourceFilter, IAMFilterMiscFlags
    {
        public CanonCameraFilter2() : base("Canon Camera Capture") { }

        protected override int OnInitializePins() => AddPin(new CanonSourceStream("Capture", this));

        /// <summary>
        /// A filter is considered to be a live source if either of the following are true:
        /// The filter returns the AM_FILTER_MISC_FLAGS_IS_SOURCE flag from the IAMFilterMiscFlags::GetMiscFlags 
        /// method, AND at least one of its output pins exposes the IAMPushSource interface.
        /// </summary>
        /// <returns></returns>
        public int GetMiscFlags() => (int)AMFilterMiscFlags.IsSource;
    }

}