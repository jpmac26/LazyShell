﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using LAZYSHELL.Properties;

namespace LAZYSHELL
{
    public partial class AlliesEditor : Form
    {
        private Model model;
        private Settings settings = Settings.Default;
        private Allies alliesEditor;
        private LevelUps levelUpsEditor;
        public AlliesEditor(Model model)
        {
            this.model = model;
            settings.Keystrokes[0x20] = "\x20";
            settings.KeystrokesMenu[0x20] = "\x20";
            InitializeComponent();
            Do.AddShortcut(toolStrip3, Keys.Control | Keys.S, new EventHandler(save_Click));
            Do.AddShortcut(toolStrip3, Keys.F1, helpTips);
            Do.AddShortcut(toolStrip3, Keys.F2, baseConversion);
            this.toolTip1.InitialDelay = 0;
            // create editors
            levelUpsEditor = new LevelUps(model);
            levelUpsEditor.TopLevel = false;
            levelUpsEditor.Dock = DockStyle.Left;
            levelUpsEditor.SetToolTips(toolTip1);
            panel1.Controls.Add(levelUpsEditor);
            levelUpsEditor.Visible = true;
            alliesEditor = new Allies(model);
            alliesEditor.TopLevel = false;
            alliesEditor.Dock = DockStyle.Left;
            alliesEditor.SetToolTips(toolTip1);
            panel1.Controls.Add(alliesEditor);
            alliesEditor.Visible = true;
            new ToolTipLabel(this, toolTip1, baseConversion, helpTips);
        }
        public void Assemble()
        {
            foreach (Character c in model.Characters)
                c.Assemble();
            foreach (Slot s in model.Slots)
                s.Assemble();
        }
        private void AlliesEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "Allies have not been saved.\n\nWould you like to save changes?", "LAZY SHELL",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (result == DialogResult.Yes)
                Assemble();
            else if (result == DialogResult.No)
            {
                model.Characters = null;
                model.Slots = null;
                return;
            }
            else if (result == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
        }
        private void save_Click(object sender, EventArgs e)
        {
            Assemble();
        }
        private void import_Click(object sender, EventArgs e)
        {
            new IOElements((Element[])model.Characters, alliesEditor.Index, "IMPORT CHARACTERS...", model).ShowDialog();
            alliesEditor.RefreshCharacter();
            levelUpsEditor.RefreshLevel();
        }
        private void export_Click(object sender, EventArgs e)
        {
            new IOElements((Element[])model.Characters, alliesEditor.Index, "EXPORT CHARACTERS...", model).ShowDialog();
        }
        private void clear_Click(object sender, EventArgs e)
        {
            new ClearElements(model.Characters, alliesEditor.Index, "CLEAR CHARACTERS...").ShowDialog();
            alliesEditor.RefreshCharacter();
            levelUpsEditor.RefreshLevel();
        }
        private void showNewGameStats_Click(object sender, EventArgs e)
        {
            alliesEditor.Visible = showNewGameStats.Checked;
        }
        private void showLevelUps_Click(object sender, EventArgs e)
        {
            levelUpsEditor.Visible = showLevelUps.Checked;
        }
    }
}