﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace LAZYSHELL
{
    public partial class NewMessageBox : Form
    {
        public Button Button1 { get { return button1; } set { button1 = value; } }
        public NewMessageBox(string title, string description, string contents, string fontfamily)
        {
            InitializeComponent();
            this.Text = title;
            this.label1.Text = description;
            this.richTextBox1.Text = contents;
            if (fontfamily != "")
                richTextBox1.Font = new Font(fontfamily, 8.25F);
        }
        public NewMessageBox(string title, string description, string contents)
        {
            InitializeComponent();
            this.Text = title;
            this.label1.Text = description;
            this.richTextBox1.Text = contents;
        }
        private void buttonOK_Click(object sender, EventArgs e)
        {
            this.Close();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("You are about to clear the entire history of all past actions performed within the Lazy Shell application.\n\n" +
                "Are you sure you want to do this?", "LAZY SHELL", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            Model.History = "";
            richTextBox1.Text = "";
        }
        private void buttonCopy_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(richTextBox1.Text);
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
            history.Write(richTextBox1.Text);
            history.Close();
        }
    }
    public static class NewMessage
    {
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
    }
}