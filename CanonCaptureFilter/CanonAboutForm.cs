﻿using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using EDSDK_NET;
using EDSDKLib;
using DirectShow.BaseClasses;
using System.Runtime.InteropServices;

namespace CanonCaptureFilter
{
    [ComVisible(true)]
    [Guid("C2D28A5D-652A-496E-8F42-A4B58A67A221")]
    public partial class CanonAboutForm : BasePropertyPage
    {
        #region Variables

        List<int> AvList;
        List<int> TvList;
        List<int> ISOList;
        List<Camera> CamList;
        Bitmap Evf_Bmp;
        int LVBw, LVBh, w, h;
        float LVBratio, LVration;

        int ErrCount;
        readonly object ErrLock = new object();

        #endregion

        public CanonAboutForm()
        {
            try
            {
                InitializeComponent();
                Controller.Initialize();
                //CameraHandler = new SDKHandler();
                Controller.SDK.CameraAdded += SDK_CameraAdded;
                Controller.SDK.LiveViewUpdated += SDK_LiveViewUpdated;
                Controller.SDK.ProgressChanged += SDK_ProgressChanged;
                Controller.SDK.CameraHasShutdown += SDK_CameraHasShutdown;
                SavePathTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "RemotePhoto");
                LVBw = myLiveViewPicBox.Width;
                LVBh = myLiveViewPicBox.Height;
                RefreshCamera();
            }
            catch (DllNotFoundException) { ReportError("Canon DLLs not found!", true); }
            catch (Exception ex) { ReportError(ex.Message, true); }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try { Controller.SDK?.Dispose(); }
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
                Evf_Bmp = new Bitmap(img);
                using (Graphics g = myLiveViewPicBox.CreateGraphics())
                {
                    LVBratio = LVBw / (float)LVBh;
                    LVration = Evf_Bmp.Width / (float)Evf_Bmp.Height;
                    if (LVBratio < LVration)
                    {
                        w = LVBw;
                        h = (int)(LVBw / LVration);
                    }
                    else
                    {
                        w = (int)(LVBh * LVration);
                        h = LVBh;
                    }
                    g.DrawImage(Evf_Bmp, 0, 0, w, h);
                }
                Evf_Bmp.Dispose();
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
                if (Controller.SDK.CameraSessionOpen) 
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
                    Controller.SDK.TakePhoto((uint)BulbUpDo.Value);
                else
                    Controller.SDK.TakePhoto();
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void RecordVideoButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (!Controller.SDK.IsFilming)
                {
                    if (STComputerButton.Checked || STBothButton.Checked)
                    {
                        Directory.CreateDirectory(SavePathTextBox.Text);
                        Controller.SDK.StartFilming(SavePathTextBox.Text);
                    }
                    else
                        Controller.SDK.StartFilming();
                    RecordVideoButton.Text = "Stop Video";
                }
                else
                {
                    Controller.SDK.StopFilming();
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
                    Controller.SDK.ImageSaveDirectory = SavePathTextBox.Text;
                }
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void AvCoBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            try 
            {
                Controller.SDK.SetSetting(EDSDK.PropID_Av, CameraValues.AV((string)AvCoBox.SelectedItem)); 
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void TvCoBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                Controller.SDK.SetSetting(EDSDK.PropID_Tv, CameraValues.TV((string)TvCoBox.SelectedItem));
                BulbUpDo.Enabled = (string)TvCoBox.SelectedItem == "Bulb";
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void ISOCoBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            try 
            {
                Controller.SDK.SetSetting(EDSDK.PropID_ISOSpeed, CameraValues.ISO((string)ISOCoBox.SelectedItem)); 
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void WBCoBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                switch (WBCoBox.SelectedIndex)
                {
                    case 0: Controller.SDK.SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_Auto); break;
                    case 1: Controller.SDK.SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_Daylight); break;
                    case 2: Controller.SDK.SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_Cloudy); break;
                    case 3: Controller.SDK.SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_Tangsten); break;
                    case 4: Controller.SDK.SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_Fluorescent); break;
                    case 5: Controller.SDK.SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_Strobe); break;
                    case 6: Controller.SDK.SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_WhitePaper); break;
                    case 7: Controller.SDK.SetSetting(EDSDK.PropID_WhiteBalance, EDSDK.WhiteBalance_Shade); break;
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
                    Controller.SDK.SetSetting(EDSDK.PropID_SaveTo, (uint)EDSDK.EdsSaveTo.Camera);
                    BrowseButton.Enabled = false;
                    SavePathTextBox.Enabled = false;
                }
                else
                {
                    if (STComputerButton.Checked)
                        Controller.SDK.SetSetting(EDSDK.PropID_SaveTo, (uint)EDSDK.EdsSaveTo.Host);
                    else if (STBothButton.Checked)
                        Controller.SDK.SetSetting(EDSDK.PropID_SaveTo, (uint)EDSDK.EdsSaveTo.Both);
                    Controller.SDK.SetCapacity();
                    BrowseButton.Enabled = true;
                    SavePathTextBox.Enabled = true;
                    Directory.CreateDirectory(SavePathTextBox.Text);
                    Controller.SDK.ImageSaveDirectory = SavePathTextBox.Text;
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
                if (!Controller.SDK.IsLiveViewOn)
                {
                    Controller.SDK.StartLiveView();
                    LiveViewButton.Text = "Stop LV";
                }
                else
                {
                    Controller.SDK.StopLiveView();
                    LiveViewButton.Text = "Start LV";
                }
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void LiveViewPicBox_MouseDown(object sender, MouseEventArgs e)
        {
            try
            {
                if (Controller.SDK.IsLiveViewOn && Controller.SDK.IsCoordSystemSet)
                {
                    ushort x = (ushort)((e.X / (double)myLiveViewPicBox.Width) * Controller.SDK.Evf_CoordinateSystem.width);
                    ushort y = (ushort)((e.Y / (double)myLiveViewPicBox.Height) * Controller.SDK.Evf_CoordinateSystem.height);
                    Controller.SDK.SetManualWBEvf(x, y);
                }
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void LiveViewPicBox_SizeChanged(object sender, EventArgs e)
        {
            try
            {
                LVBw = myLiveViewPicBox.Width;
                LVBh = myLiveViewPicBox.Height;
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void FocusNear3Button_Click(object sender, EventArgs e)
        {
            try { Controller.SDK.SetFocus(EDSDK.EvfDriveLens_Near3);  }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void FocusNear2Button_Click(object sender, EventArgs e)
        {
            try { Controller.SDK.SetFocus(EDSDK.EvfDriveLens_Near2); }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void FocusNear1Button_Click(object sender, EventArgs e)
        {
            try { Controller.SDK.SetFocus(EDSDK.EvfDriveLens_Near1); }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void FocusFar1Button_Click(object sender, EventArgs e)
        {
            try { Controller.SDK.SetFocus(EDSDK.EvfDriveLens_Far1); }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void FocusFar2Button_Click(object sender, EventArgs e)
        {
            try { Controller.SDK.SetFocus(EDSDK.EvfDriveLens_Far2); }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void FocusFar3Button_Click(object sender, EventArgs e)
        {
            try { Controller.SDK.SetFocus(EDSDK.EvfDriveLens_Far3); }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        #endregion

        #region Subroutines

        private void CloseSession()
        {
            Controller.SDK.CloseSession();
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
            CamList = Controller.SDK.GetCameraList();
            foreach (Camera cam in CamList) CameraListBox.Items.Add(cam.Info.szDeviceDescription);
            if (Controller.SDK.CameraSessionOpen) CameraListBox.SelectedIndex = CamList.FindIndex(t => t.Ref == Controller.SDK.MainCamera.Ref);
            else if (CamList.Count > 0) CameraListBox.SelectedIndex = 0;
        }

        private void OpenSession()
        {
            if (CameraListBox.SelectedIndex >= 0)
            {
                Controller.SDK.OpenSession(CamList[CameraListBox.SelectedIndex]);
                SessionButton.Text = "Close Session";
                string cameraname = Controller.SDK.MainCamera.Info.szDeviceDescription;
                SessionLabel.Text = cameraname;
                // MFC
                //if (CameraHandler.GetSetting(EDSDK.PropID_AEMode) != EDSDK.AEMode_Manual) MessageBox.Show("Camera is not in manual mode. Some features might not work!");
                AvList = Controller.SDK.GetSettingsList((uint)EDSDK.PropID_Av);
                TvList = Controller.SDK.GetSettingsList((uint)EDSDK.PropID_Tv);
                ISOList = Controller.SDK.GetSettingsList((uint)EDSDK.PropID_ISOSpeed);
                foreach (int Av in AvList) AvCoBox.Items.Add(CameraValues.AV((uint)Av));
                foreach (int Tv in TvList) TvCoBox.Items.Add(CameraValues.TV((uint)Tv));
                foreach (int ISO in ISOList) ISOCoBox.Items.Add(CameraValues.ISO((uint)ISO));
                AvCoBox.SelectedIndex = AvCoBox.Items.IndexOf(CameraValues.AV((uint)Controller.SDK.GetSetting((uint)EDSDK.PropID_Av)));
                TvCoBox.SelectedIndex = TvCoBox.Items.IndexOf(CameraValues.TV((uint)Controller.SDK.GetSetting((uint)EDSDK.PropID_Tv)));
                ISOCoBox.SelectedIndex = ISOCoBox.Items.IndexOf(CameraValues.ISO((uint)Controller.SDK.GetSetting((uint)EDSDK.PropID_ISOSpeed)));
                int wbidx = (int)Controller.SDK.GetSetting((uint)EDSDK.PropID_WhiteBalance);
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
        }

        private void ReportError(string message, bool lockdown)
        {
            int errc;
            lock (ErrLock) { errc = ++ErrCount; }

            if (lockdown) EnableUI(false);

            if (errc < 4) MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            else if (errc == 4) MessageBox.Show("Many errors happened!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            lock (ErrLock) { ErrCount--; }
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
