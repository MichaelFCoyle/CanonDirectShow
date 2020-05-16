using DirectShow.BaseClasses;
using System;
using System.Runtime.InteropServices;

namespace CanonCaptureFilter
{
    [ComVisible(true)]
    [Guid("3EFB481C-F35F-434c-A085-C3DFEFF65D94")]
    public partial class AboutForm : BasePropertyPage
    {
        public AboutForm()
        {
            InitializeComponent();
        }
    }
}
