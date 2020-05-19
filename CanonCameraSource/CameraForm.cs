using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using EDSDK_NET;
using EDSDKLib;
using CanonCaptureFilter;

namespace CanonCameraSource
{
    public partial class CameraForm : Form
    {
        #region Variables

        SDKHandler CameraHandler => Controller.SDK;

        List<int> m_AvList;
        List<int> m_TvList;
        List<int> m_ISOList;
        List<Camera> m_CamList;
        Bitmap m_Evf_Bmp;
        int m_LVBw, m_LVBh, m_w, m_h;
        float m_LVBratio, m_LVration;

        int m_ErrCount;
        readonly object m_ErrLock = new object();

        #endregion

        public CameraForm()
        {
            try
            {
                InitializeComponent();
                Controller.Initialize();
                CameraHandler.CameraAdded += SDK_CameraAdded;
                CameraHandler.LiveViewUpdated += SDK_LiveViewUpdated;
                CameraHandler.ProgressChanged += SDK_ProgressChanged;
                CameraHandler.CameraHasShutdown += SDK_CameraHasShutdown;
                SavePathTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "RemotePhoto");
                m_LVBw = myLiveViewPicBox.Width;
                m_LVBh = myLiveViewPicBox.Height;
                RefreshCamera();
            }
            catch (DllNotFoundException) { ReportError("Canon DLLs not found!", true); }
            catch (Exception ex) { ReportError(ex.Message, true); }
        }


        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try { CameraHandler?.Dispose(); }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        #region SDK Events

        private void SDK_ProgressChanged(int Progress)
        {
            try
            {
                if (Progress == 100) Progress = 0;
                this.Invoke((Action)delegate { MainProgressBar.Value = Progress; });
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void SDK_LiveViewUpdated(Stream img)
        {
            try
            {
                m_Evf_Bmp = new Bitmap(img);
                using (Graphics g = myLiveViewPicBox.CreateGraphics())
                {
                    m_LVBratio = m_LVBw / (float)m_LVBh;
                    m_LVration = m_Evf_Bmp.Width / (float)m_Evf_Bmp.Height;
                    if (m_LVBratio < m_LVration)
                    {
                        m_w = m_LVBw;
                        m_h = (int)(m_LVBw / m_LVration);
                    }
                    else
                    {
                        m_w = (int)(m_LVBh * m_LVration);
                        m_h = m_LVBh;
                    }
                    g.DrawImage(m_Evf_Bmp, 0, 0, m_w, m_h);
                }
                m_Evf_Bmp.Dispose();
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void SDK_CameraAdded()
        {
            try { RefreshCamera(); }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void SDK_CameraHasShutdown(object sender, EventArgs e)
        {
            try { CloseSession(); }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        #endregion

        #region Session

        private void SessionButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (CameraHandler.CameraSessionOpen)
                    CloseSession();
                else
                    OpenSession();
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            try { RefreshCamera(); }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        #endregion

        #region Settings

        private void TakePhotoButton_Click(object sender, EventArgs e)
        {
            try
            {
                if ((string)TvCoBox.SelectedItem == "Bulb")
                    CameraHandler.TakePhoto((uint)BulbUpDo.Value);
                else
                    CameraHandler.TakePhoto();
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void RecordVideoButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (!CameraHandler.IsFilming)
                {
                    if (STComputerButton.Checked || STBothButton.Checked)
                    {
                        Directory.CreateDirectory(SavePathTextBox.Text);
                        CameraHandler.StartFilming(SavePathTextBox.Text);
                    }
                    else
                        CameraHandler.StartFilming();
                    RecordVideoButton.Text = "Stop Video";
                }
                else
                {
                    CameraHandler.StopFilming();
                    RecordVideoButton.Text = "Record Video";
                }
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (Directory.Exists(SavePathTextBox.Text)) SaveFolderBrowser.SelectedPath = SavePathTextBox.Text;
                if (SaveFolderBrowser.ShowDialog() == DialogResult.OK)
                {
                    SavePathTextBox.Text = SaveFolderBrowser.SelectedPath;
                    CameraHandler.ImageSaveDirectory = SavePathTextBox.Text;
                }
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void AvCoBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                CameraHandler.SetSetting(EDSDK.PropID_Av, CameraValues.AV((string)AvCoBox.SelectedItem));
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void TvCoBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                CameraHandler.SetSetting(EDSDK.PropID_Tv, CameraValues.TV((string)TvCoBox.SelectedItem));
                BulbUpDo.Enabled = (string)TvCoBox.SelectedItem == "Bulb";
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void ISOCoBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                CameraHandler.SetSetting(EDSDK.PropID_ISOSpeed, CameraValues.ISO((string)ISOCoBox.SelectedItem));
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void WBCoBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                switch (WBCoBox.SelectedIndex)
                {
                    case 0: CameraHandler.SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_Auto); break;
                    case 1: CameraHandler.SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_Daylight); break;
                    case 2: CameraHandler.SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_Cloudy); break;
                    case 3: CameraHandler.SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_Tangsten); break;
                    case 4: CameraHandler.SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_Fluorescent); break;
                    case 5: CameraHandler.SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_Strobe); break;
                    case 6: CameraHandler.SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_WhitePaper); break;
                    case 7: CameraHandler.SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_Shade); break;
                }
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void SaveToButton_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                if (STCameraButton.Checked)
                {
                    CameraHandler.SetSetting(EDSDK.PropID_SaveTo, (uint)EDSDK.EdsSaveTo.Camera);
                    BrowseButton.Enabled = false;
                    SavePathTextBox.Enabled = false;
                }
                else
                {
                    if (STComputerButton.Checked)
                        CameraHandler.SetSetting(EDSDK.PropID_SaveTo, (uint)EDSDK.EdsSaveTo.Host);
                    else if (STBothButton.Checked)
                        CameraHandler.SetSetting(EDSDK.PropID_SaveTo, (uint)EDSDK.EdsSaveTo.Both);
                    CameraHandler.SetCapacity();
                    BrowseButton.Enabled = true;
                    SavePathTextBox.Enabled = true;
                    Directory.CreateDirectory(SavePathTextBox.Text);
                    CameraHandler.ImageSaveDirectory = SavePathTextBox.Text;
                }
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        #endregion

        #region Live view

        private void LiveViewButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (!CameraHandler.IsLiveViewOn)
                {
                    CameraHandler.StartLiveView();
                    LiveViewButton.Text = "Stop LV";
                }
                else
                {
                    CameraHandler.StopLiveView();
                    LiveViewButton.Text = "Start LV";
                }
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void LiveViewPicBox_MouseDown(object sender, MouseEventArgs e)
        {
            try
            {
                if (CameraHandler.IsLiveViewOn && CameraHandler.IsCoordSystemSet)
                {
                    ushort x = (ushort)((e.X / (double)myLiveViewPicBox.Width) * CameraHandler.Evf_CoordinateSystem.width);
                    ushort y = (ushort)((e.Y / (double)myLiveViewPicBox.Height) * CameraHandler.Evf_CoordinateSystem.height);
                    CameraHandler.SetManualWBEvf(x, y);
                }
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void LiveViewPicBox_SizeChanged(object sender, EventArgs e)
        {
            try
            {
                m_LVBw = myLiveViewPicBox.Width;
                m_LVBh = myLiveViewPicBox.Height;
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void FocusNear3Button_Click(object sender, EventArgs e)
        {
            try { CameraHandler.SetFocus(EDSDK.EvfDriveLens_Near3); }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void FocusNear2Button_Click(object sender, EventArgs e)
        {
            try { CameraHandler.SetFocus(EDSDK.EvfDriveLens_Near2); }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void FocusNear1Button_Click(object sender, EventArgs e)
        {
            try { CameraHandler.SetFocus(EDSDK.EvfDriveLens_Near1); }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void FocusFar1Button_Click(object sender, EventArgs e)
        {
            try { CameraHandler.SetFocus(EDSDK.EvfDriveLens_Far1); }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void FocusFar2Button_Click(object sender, EventArgs e)
        {
            try { CameraHandler.SetFocus(EDSDK.EvfDriveLens_Far2); }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void FocusFar3Button_Click(object sender, EventArgs e)
        {
            try { CameraHandler.SetFocus(EDSDK.EvfDriveLens_Far3); }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        #endregion

        #region Subroutines

        private void CloseSession()
        {
            CameraHandler.CloseSession();
            AvCoBox.Items.Clear();
            TvCoBox.Items.Clear();
            ISOCoBox.Items.Clear();
            SettingsGroupBox.Enabled = false;
            LiveViewGroupBox.Enabled = false;
            SessionButton.Text = "Open Session";
            SessionLabel.Text = "No open session";
            RefreshCamera();//Closing the session invalidates the current camera pointer
        }

        private void RefreshCamera()
        {
            CameraListBox.Items.Clear();
            m_CamList = CameraHandler.GetCameraList();
            foreach (Camera cam in m_CamList)
                CameraListBox.Items.Add(cam.Info.szDeviceDescription);

            if (CameraHandler.CameraSessionOpen)
                CameraListBox.SelectedIndex = m_CamList.FindIndex(t => t.Ref == CameraHandler.MainCamera.Ref);
            else if (m_CamList.Count > 0)
                CameraListBox.SelectedIndex = 0;
        }

        private void OpenSession()
        {
            if (CameraListBox.SelectedIndex < 0)
                return;
            CameraHandler.OpenSession(m_CamList[CameraListBox.SelectedIndex]);
            SessionButton.Text = "Close Session";
            string cameraname = CameraHandler.MainCamera.Info.szDeviceDescription;
            SessionLabel.Text = cameraname;
            // MFC
            //if (CameraHandler.GetSetting(EDSDK.PropID_AEMode) != EDSDK.AEMode_Manual) MessageBox.Show("Camera is not in manual mode. Some features might not work!");

            m_AvList = CameraHandler.GetSettingsList((uint)EDSDK.PropID_Av);
            foreach (int Av in m_AvList) AvCoBox.Items.Add(CameraValues.AV((uint)Av));
            AvCoBox.SelectedIndex = AvCoBox.Items.IndexOf(CameraValues.AV((uint)CameraHandler.GetSetting((uint)EDSDK.PropID_Av)));

            m_TvList = CameraHandler.GetSettingsList((uint)EDSDK.PropID_Tv);
            foreach (int Tv in m_TvList) TvCoBox.Items.Add(CameraValues.TV((uint)Tv));
            TvCoBox.SelectedIndex = TvCoBox.Items.IndexOf(CameraValues.TV((uint)CameraHandler.GetSetting((uint)EDSDK.PropID_Tv)));

            m_ISOList = CameraHandler.GetSettingsList((uint)EDSDK.PropID_ISOSpeed);
            foreach (int ISO in m_ISOList) ISOCoBox.Items.Add(CameraValues.ISO((uint)ISO));
            ISOCoBox.SelectedIndex = ISOCoBox.Items.IndexOf(CameraValues.ISO((uint)CameraHandler.GetSetting((uint)EDSDK.PropID_ISOSpeed)));

            int wbidx = (int)CameraHandler.GetSetting((uint)EDSDK.PropID_WhiteBalance);
            switch (wbidx)
            {
                case EDSDK.WhiteBalance_Auto: WBCoBox.SelectedIndex = 0; break;
                case EDSDK.WhiteBalance_Daylight: WBCoBox.SelectedIndex = 1; break;
                case EDSDK.WhiteBalance_Cloudy: WBCoBox.SelectedIndex = 2; break;
                case EDSDK.WhiteBalance_Tangsten: WBCoBox.SelectedIndex = 3; break;
                case EDSDK.WhiteBalance_Fluorescent: WBCoBox.SelectedIndex = 4; break;
                case EDSDK.WhiteBalance_Strobe: WBCoBox.SelectedIndex = 5; break;
                case EDSDK.WhiteBalance_WhitePaper: WBCoBox.SelectedIndex = 6; break;
                case EDSDK.WhiteBalance_Shade: WBCoBox.SelectedIndex = 7; break;
                default: WBCoBox.SelectedIndex = -1; break;
            }
            SettingsGroupBox.Enabled = true;
            LiveViewGroupBox.Enabled = true;
        }

        private void ReportError(string message, bool lockdown)
        {
            int errc;
            lock (m_ErrLock) { errc = ++m_ErrCount; }

            if (lockdown) EnableUI(false);

            if (errc < 4)
                MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else if (errc == 4)
                MessageBox.Show("Many errors happened!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            lock (m_ErrLock) { m_ErrCount--; }
        }

        private void EnableUI(bool enable)
        {
            if (InvokeRequired) Invoke((Action)delegate { EnableUI(enable); });
            else
            {
                SettingsGroupBox.Enabled = enable;
                InitGroupBox.Enabled = enable;
                LiveViewGroupBox.Enabled = enable;
            }
        }

        #endregion
    }
}