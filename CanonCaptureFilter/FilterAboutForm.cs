using DirectShow.BaseClasses;
using System.Runtime.InteropServices;

namespace CanonCaptureFilter
{
    [ComVisible(true)]
    [Guid("56F4E96B-D101-4de8-BC48-8C4311C9C8C4")]
    public partial class FilterAboutForm : BasePropertyPage
    {
        public FilterAboutForm()
        {
            InitializeComponent();
        }
    }
}
