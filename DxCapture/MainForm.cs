using Sonic;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace DxCapture
{
    public partial class MainForm : Form
    {
        VideoCaptureGraph m_Capture = null;

        public MainForm() => InitializeComponent();

        private void MainForm_Load(object sender, EventArgs e)
        {
            m_Capture = new VideoCaptureGraph { VideoControl = myPreviewPictureBox };
            m_Capture.OnPlaybackStart += Playback_OnPlaybackStart;
            m_Capture.OnPlaybackStop += Playback_OnPlaybackStop;

            Playback_OnPlaybackStop(sender, e);
            myCaptureButton.Enabled = false;

            List<DSDevice> _devices = (new DSVideoCaptureCategory()).Objects;
            foreach (DSDevice _device in _devices) 
                myCaptureDeviceComboBox.Items.Add(_device);

            if (myCaptureDeviceComboBox.Items.Count > 0) myCaptureDeviceComboBox.SelectedIndex = 0;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            m_Capture?.Stop();
            m_Capture?.Dispose();
            m_Capture = null;
        }

        private void Playback_OnPlaybackStop(object sender, EventArgs e)
        {
            myCaptureButton.Text = "Capture";
            myPropertiesButton.Enabled = (m_Capture.CaptureDevice != null && m_Capture.CaptureDevice.HaveProperties);
            myBrowseDestButton.Enabled = true;
            myCaptureDeviceComboBox.Enabled = true;
        }

        private void Playback_OnPlaybackStart(object sender, EventArgs e)
        {
            myCaptureButton.Text = "Stop";
            myPropertiesButton.Enabled = false;
            myBrowseDestButton.Enabled = false;
            myCaptureDeviceComboBox.Enabled = false;
        }

        private void CaptureDevice_SelectedIndexChanged(object sender, EventArgs e)
        {
            m_Capture.CaptureDevice = null;
            if (myCaptureDeviceComboBox.SelectedItem != null)
            {
                m_Capture.CaptureDevice = ((DSDevice)myCaptureDeviceComboBox.SelectedItem).Filter;
                if (m_Capture.CaptureDevice != null && !m_Capture.CaptureDevice.IsValid)
                    m_Capture.CaptureDevice = null;
            }
            myCaptureButton.Enabled = (m_Capture.CaptureDevice != null && m_Capture.CaptureDevice.IsValid);
            myPropertiesButton.Enabled = (m_Capture.CaptureDevice != null && m_Capture.CaptureDevice.HaveProperties);
        }

        private void Properties_Click(object sender, EventArgs e) => m_Capture.CaptureDevice.ShowProperties(Handle);

        private void BrowseDest_Click(object sender, EventArgs e)
        {
            if (mySaveFileDialog.ShowDialog() != DialogResult.OK)
                return;
            myDestFileNameTextBox.Text = mySaveFileDialog.FileName;
            m_Capture.OutputFileName = mySaveFileDialog.FileName;
        }

        private void Capture_Click(object sender, EventArgs e)
        {
            if (m_Capture.IsStopped)
                m_Capture.Start();
            else
                m_Capture.Stop();
        }
    }
}
