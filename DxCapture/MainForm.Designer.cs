namespace DxCapture
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.myPreviewPictureBox = new System.Windows.Forms.PictureBox();
            this.mySaveFileDialog = new System.Windows.Forms.SaveFileDialog();
            this.myCaptureDeviceComboBox = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.myBrowseDestButton = new System.Windows.Forms.Button();
            this.myDestFileNameTextBox = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.myPropertiesButton = new System.Windows.Forms.Button();
            this.myCaptureButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.myPreviewPictureBox)).BeginInit();
            this.SuspendLayout();
            // 
            // pbPreview
            // 
            this.myPreviewPictureBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.myPreviewPictureBox.BackColor = System.Drawing.Color.Black;
            this.myPreviewPictureBox.Location = new System.Drawing.Point(3, 2);
            this.myPreviewPictureBox.Name = "pbPreview";
            this.myPreviewPictureBox.Size = new System.Drawing.Size(629, 422);
            this.myPreviewPictureBox.TabIndex = 0;
            this.myPreviewPictureBox.TabStop = false;
            // 
            // saveFileDialog
            // 
            this.mySaveFileDialog.DefaultExt = "avi";
            this.mySaveFileDialog.Filter = "AVI files |*.avi|All Files (*.*)|*.*";
            // 
            // cmboCaptureDevice
            // 
            this.myCaptureDeviceComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.myCaptureDeviceComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.myCaptureDeviceComboBox.FormattingEnabled = true;
            this.myCaptureDeviceComboBox.Location = new System.Drawing.Point(12, 442);
            this.myCaptureDeviceComboBox.Name = "cmboCaptureDevice";
            this.myCaptureDeviceComboBox.Size = new System.Drawing.Size(333, 21);
            this.myCaptureDeviceComboBox.TabIndex = 15;
            this.myCaptureDeviceComboBox.SelectedIndexChanged += new System.EventHandler(this.CaptureDevice_SelectedIndexChanged);
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(13, 469);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(110, 13);
            this.label2.TabIndex = 14;
            this.label2.Text = "Destination File Name";
            // 
            // btnBrowseDest
            // 
            this.myBrowseDestButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.myBrowseDestButton.Location = new System.Drawing.Point(351, 481);
            this.myBrowseDestButton.Name = "btnBrowseDest";
            this.myBrowseDestButton.Size = new System.Drawing.Size(75, 23);
            this.myBrowseDestButton.TabIndex = 13;
            this.myBrowseDestButton.Text = "Browse";
            this.myBrowseDestButton.UseVisualStyleBackColor = true;
            this.myBrowseDestButton.Click += new System.EventHandler(this.BrowseDest_Click);
            // 
            // tbDestFileName
            // 
            this.myDestFileNameTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.myDestFileNameTextBox.Location = new System.Drawing.Point(12, 483);
            this.myDestFileNameTextBox.Name = "tbDestFileName";
            this.myDestFileNameTextBox.ReadOnly = true;
            this.myDestFileNameTextBox.Size = new System.Drawing.Size(333, 20);
            this.myDestFileNameTextBox.TabIndex = 12;
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(11, 430);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(81, 13);
            this.label1.TabIndex = 11;
            this.label1.Text = "Capture Device";
            // 
            // btnProperties
            // 
            this.myPropertiesButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.myPropertiesButton.Location = new System.Drawing.Point(350, 442);
            this.myPropertiesButton.Name = "btnProperties";
            this.myPropertiesButton.Size = new System.Drawing.Size(75, 23);
            this.myPropertiesButton.TabIndex = 10;
            this.myPropertiesButton.Text = "Properties";
            this.myPropertiesButton.UseVisualStyleBackColor = true;
            this.myPropertiesButton.Click += new System.EventHandler(this.Properties_Click);
            // 
            // btnCapture
            // 
            this.myCaptureButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.myCaptureButton.Location = new System.Drawing.Point(551, 442);
            this.myCaptureButton.Name = "btnCapture";
            this.myCaptureButton.Size = new System.Drawing.Size(75, 23);
            this.myCaptureButton.TabIndex = 16;
            this.myCaptureButton.Text = "Start";
            this.myCaptureButton.UseVisualStyleBackColor = true;
            this.myCaptureButton.Click += new System.EventHandler(this.Capture_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(632, 512);
            this.Controls.Add(this.myCaptureButton);
            this.Controls.Add(this.myCaptureDeviceComboBox);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.myBrowseDestButton);
            this.Controls.Add(this.myDestFileNameTextBox);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.myPropertiesButton);
            this.Controls.Add(this.myPreviewPictureBox);
            this.MinimumSize = new System.Drawing.Size(640, 480);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "DShow Capture";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            ((System.ComponentModel.ISupportInitialize)(this.myPreviewPictureBox)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox myPreviewPictureBox;
        private System.Windows.Forms.SaveFileDialog mySaveFileDialog;
        private System.Windows.Forms.ComboBox myCaptureDeviceComboBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button myBrowseDestButton;
        private System.Windows.Forms.TextBox myDestFileNameTextBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button myPropertiesButton;
        private System.Windows.Forms.Button myCaptureButton;
    }
}

