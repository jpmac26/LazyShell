﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace LazyShell
{
    public partial class NewMessageBox : Controls.NewForm
    {
        // Variables
        public Button Button1
        {
            get { return buttonClear; }
            set { buttonClear = value; }
        }

        // Constructors
        public NewMessageBox(string title, string description, string contents)
        {
            InitializeComponent();
            this.Text = title;
            Bitmap icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath).ToBitmap();
            this.pictureBox1.Image = icon;
            this.label1.Text = description;
            this.messageText.Text = contents;
        }
        public NewMessageBox(string title, string description, string contents, string fontFamily)
        {
            InitializeComponent();
            this.Text = title;
            Bitmap icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath).ToBitmap();
            this.pictureBox1.Image = icon;
            this.label1.Text = description;
            this.messageText.Text = contents;
            if (fontFamily != "")
                messageText.Font = new Font(fontFamily, 8.25F);
        }
        public NewMessageBox(string title, string description, string contents, string fontFamily, MessageIcon messageIcon)
        {
            InitializeComponent();
            this.Text = title;
            Bitmap icon;
            switch (messageIcon)
            {
                case MessageIcon.None:
                    icon = new Bitmap(32, 32);
                    break;
                case MessageIcon.Error:
                    icon = SystemIcons.Error.ToBitmap();
                    break;
                case MessageIcon.Info:
                    icon = SystemIcons.Information.ToBitmap();
                    break;
                case MessageIcon.Warning:
                    icon = SystemIcons.Warning.ToBitmap();
                    break;
                default:
                    icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath).ToBitmap();
                    break;
            }
            this.pictureBox1.Image = icon;
            this.label1.Text = description;
            if (fontFamily != "")
                this.messageText.Font = new Font(fontFamily, 8.25F);
            this.messageText.Text = contents;
        }

        #region Event Handlers

        private void buttonOK_Click(object sender, EventArgs e)
        {
            this.Close();
        }
        private void buttonClear_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("You are about to clear the entire history of all past actions performed within the Lazy Shell application.\n\n" +
                "Are you sure you want to do this?", "LAZY SHELL", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            Model.History = "";
            messageText.Text = "";
        }
        private void buttonCopy_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(messageText.Text);
        }
        private void buttonExport_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
            saveFileDialog.FilterIndex = 0;
            saveFileDialog.FileName = Model.GetFileNameWithoutPath() + " - History.txt";
            saveFileDialog.RestoreDirectory = true;
            if (saveFileDialog.ShowDialog() != DialogResult.OK)
                return;
            StreamWriter history = File.CreateText(saveFileDialog.FileName);
            history.Write(messageText.Text);
            history.Close();
        }

        #endregion
    }
    public static class NewMessage
    {
        // Constructors
        public static void Show(string title, string description, string contents, int width, int height, bool showclear)
        {
            NewMessageBox newMessageBox = new NewMessageBox(title, description, contents);
            newMessageBox.Width = width;
            newMessageBox.Height = height;
            if (showclear)
                newMessageBox.Button1.Visible = true;
            newMessageBox.ShowDialog();
        }
        public static void Show(string title, string description, string contents)
        {
            new NewMessageBox(title, description, contents).ShowDialog();
        }
        public static void Show(string title, string description, string contents, string fontfamily)
        {
            new NewMessageBox(title, description, contents, fontfamily).ShowDialog();
        }
        public static void Show(string title, string description, string contents, MessageIcon messageIcon)
        {
            new NewMessageBox(title, description, contents, "", messageIcon).ShowDialog();
        }
    }
}
