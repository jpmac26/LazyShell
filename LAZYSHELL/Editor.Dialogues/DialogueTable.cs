﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Drawing;

namespace LAZYSHELL
{
    [Serializable()]
    public class DialogueTable : Element
    {
        [NonSerialized()]
        private byte[] data;
        public override byte[] Data { get { return data; } set { data = value; } }
        public override int Index { get { return index; } set { index = value; } }
        private int index;
        private char[] dialogue;
        private int dialogueOffset;
        private int dialoguePtr;
        private ushort[] dialoguePointer = new ushort[8];
        private bool error = false;
        private int caretPositionSymbol;
        private int caretPositionNotSymbol;
        [NonSerialized()]
        private TextHelper textHelper;

        private int duplicateDialogues; public int DuplicateDialogues { get { return duplicateDialogues; } set { duplicateDialogues = value; } }
        private int withinDialogues; public int WithinDialogues { get { return withinDialogues; } set { withinDialogues = value; } }
        private int withinDialoguesLocation; public int WithinDialoguesLocation { get { return withinDialoguesLocation; } set { withinDialoguesLocation = value; } }

        /*****************************************************************************
         * Get Methods
         * **************************************************************************/
        public char[] RawDialogue { get { return dialogue; } }
        public string GetDialogue(bool symbols)
        {
            if (!error)
                return new string(textHelper.DecodeText(dialogue, symbols, true));
            else
                return new string(dialogue);
        }
        // Text with symbols, not byte values - Symbols true = {02} - false = {Line Break (press A)}
        public int DialogueLen { get { return dialogue.Length; } }
        public int DialogueOffset { get { return dialogueOffset; } set { dialogueOffset = value; } }
        public int DialoguePtr
        {
            get
            {
                return Bits.GetShort(data, 0x249000 + index * 2);
            }
        }    // this is used to find duplicates
        public int GetCaretPosition(bool symbol)
        {
            if (symbol)
                return caretPositionSymbol;
            else
                return caretPositionNotSymbol;
        }
        /*****************************************************************************
         * Set Methods
         * **************************************************************************/
        public bool SetDialogue(string value, bool symbols)
        {
            this.dialogue = textHelper.EncodeText(value.ToCharArray(), symbols, true);
            this.error = textHelper.Error;
            return !error;
        }
        // Text with byte values, not symbols
        public void SetCaretPosition(int value, bool symbol)
        {
            if (symbol)
                this.caretPositionSymbol = value;
            else
                this.caretPositionNotSymbol = value;
        }

        // Constructor
        public DialogueTable(byte[] data, int dialogueNum)
        {
            this.data = data;
            this.index = dialogueNum;
            this.textHelper = TextHelper.Instance;
            InitializeDialogue(data);
        }

        // Dissasembler
        private void InitializeDialogue(byte[] data)
        {
            dialogue = GetDialogue(data);
        }
        public ushort Assemble(ushort offset)
        {
            return SaveDialogue(offset);
        }
        private ushort SaveDialogue(ushort offset)
        {
            int dlgOffset = 0;
            char[] raw;
            Bits.SetShort(data, 0x249000 + index * 2, offset);
            dlgOffset = offset + 0x249100;
            raw = new char[dialogue.Length + 1]; dialogue.CopyTo(raw, 0);
            Bits.SetCharArray(data, dlgOffset, raw);
            return (ushort)raw.Length;
        }
        private char[] GetDialogue(byte[] data)
        {
            dialoguePtr = Bits.GetShort(data, 0x249000 + index * 2); // from pointer table
            dialogueOffset = dialoguePtr + 0x249100;

            int count = dialogueOffset;
            int len = 0;
            byte ptr = 0x01;
            while (ptr != 0x00 && ptr != 0x06)
            {
                ptr = data[count];
                if (ptr == 0x0B || ptr == 0x0D || ptr == 0x1C)
                {
                    len++;
                    count++;
                }
                len++;
                count++;
            }
            len--;
            char[] dialogue = new char[len];
            for (int i = 0; i < len; i++)
                dialogue[i] = (char)data[dialogueOffset + i];
            return dialogue;
        }

        public bool CompatibleWithGame()
        {
            for (int i = 0; i < dialogue.Length; i++)
            {
                if (dialogue[i] == '\x0b' || dialogue[i] == '\x0d')
                {
                    i += 2;
                }
                if (!IsValidCharTmp(dialogue[i]))
                    return false;
            }
            return true;
        }
        private bool IsValidCharTmp(char toTest)
        {
            if (toTest >= '\x00' && toTest <= '\x1C')
                return true;
            if (toTest >= '\x20' && toTest <= '\x5A')
                return true;
            if (toTest >= '\x61' && toTest <= '\x7A')
                return true;
            if (toTest >= '\x8E' && toTest <= '\x9C')
                return true;
            return false;
        }

        public string GetDialogueStub(bool textCodeFormat)
        {
            string temp = GetDialogue(textCodeFormat);
            if (temp.Length > 40)
            {
                temp = temp.Substring(0, 37);
                return temp + "...";
            }
            else
                return temp;
        }
        public override void Clear()
        {
            dialogue = new char[0];
        }
        public FileStream Serialize()
        {
            return null;
        }
        public void Deserialize()
        {
        }
    }
}