﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Windows.Forms;
using LAZYSHELL.Properties;
using LAZYSHELL.Undo;

namespace LAZYSHELL
{
    public partial class WorldMaps : Form
    {
        #region Variables
        private long checksum;
        // main
        private delegate void Function();
        private bool updating = false;
        private int index { get { return (int)worldMapName.SelectedIndex; } }
        private WorldMap[] worldMaps { get { return Model.WorldMaps; } set { Model.WorldMaps = value; } }
        private PaletteSet palettes { get { return Model.Palettes; } set { Model.Palettes = value; } }
        private WorldMap worldMap { get { return worldMaps[index]; } set { worldMaps[index] = value; } }
        private PaletteSet logoPalette { get { return Model.WorldMapLogoPalette; } set { Model.WorldMapLogoPalette = value; } }
        private Tileset tileset;
        private byte[] tileset_bytes { get { return Model.WorldMapTilesets[worldMap.Tileset]; } }
        private Tileset logoTileset;
        private Overlay overlay = new Overlay();
        private Bitmap tilesetImage;
        private Bitmap locationImage;
        private Bitmap locationImage_S;
        private Bitmap marioImage;
        private Bitmap logoImage;
        private Bitmap locationText;
        private Settings settings = Settings.Default;
        private EditLabel labelWindow;
        // mouse
        private int zoom = 1;
        private bool mouseEnter = false;
        private int mouseDownTile = 0;
        private string mouseOverObject;
        private string mouseDownObject;
        private Point mouseDownPosition;
        private Point mousePosition;
        private bool moving = false;
        private bool defloating = false;
        // editors
        private TileEditor tileEditor;
        private PaletteEditor paletteEditor;
        private GraphicEditor graphicEditor;
        private GraphicEditor logoGraphicEditor;
        private GraphicEditor spriteEditor;
        private PaletteEditor logoPaletteEditor;
        // buffers and stacks
        private Bitmap selection;
        private CopyBuffer draggedTiles;
        private CopyBuffer copiedTiles;
        private CommandStack commandStack = new CommandStack(true);
        private int commandCount = 0;
        // old
        private ArrayList[] worldMapLocations;
        private int[] pointActivePixels;
        private int diffX, diffY;
        #endregion
        #region Methods
        // main
        public WorldMaps()
        {
            fontPalettes[0] = new PaletteSet(Model.ROM, 0, 0x3DFEE0, 2, 16, 32);
            fontPalettes[1] = new PaletteSet(Model.ROM, 0, 0x3E2D55, 2, 16, 32);
            fontPalettes[2] = new PaletteSet(Model.ROM, 0, 0x01EF40, 2, 16, 32);
            for (int i = 0; i < fontDialogue.Length; i++)
                fontDialogue[i] = new FontCharacter(i, FontType.Dialogue);
            InitializeComponent();
            this.worldMapName.Items.AddRange(Lists.Numerize(Lists.WorldMapNames));
            this.music.Items.AddRange(Lists.Numerize(Lists.MusicNames));
            this.music.SelectedIndex = Model.ROM[0x037DCF];
            Do.AddShortcut(toolStrip3, Keys.Control | Keys.S, new EventHandler(save_Click));
            Do.AddShortcut(toolStrip3, Keys.F1, helpTips);
            Do.AddShortcut(toolStrip3, Keys.F2, baseConvertor);
            toolTip1.InitialDelay = 0;
            labelWindow = new EditLabel(worldMapName, null, "World Maps", true);
            InitializeLocationsEditor();
            worldMapName.SelectedIndex = 0;
            //LoadPaletteEditor();
            //LoadGraphicEditor();
            //LoadLogoPaletteEditor();
            //LoadLogoGraphicEditor();
            LoadTileEditor();
            new ToolTipLabel(this, baseConvertor, helpTips);
            new History(this, worldMapName, null);
            checksum = Do.GenerateChecksum(worldMaps, Model.WorldMapGraphics, Model.WorldMapPalettes,
                Model.WorldMapSprites, Model.WorldMapTilesets, Model.Locations);
        }
        private void RefreshWorldMap()
        {
            Cursor.Current = Cursors.WaitCursor;
            updating = true;
            this.worldMapTileset.Value = worldMap.Tileset;
            this.pointCount.Value = worldMap.Locations;
            this.worldMapXCoord.Value = worldMap.X;
            this.worldMapYCoord.Value = worldMap.Y;
            AddLocations();
            Location temp;
            if (worldMapLocations[index] != null &&
                worldMapLocations[index].Count > 0)
            {
                temp = (Location)worldMapLocations[index][0];
                locationNum.Value = temp.Index;
            }
            else
                MessageBox.Show("There are not enough locations left to add to the current world map.\nTry reducing the location count used by earlier world maps.", "LAZY SHELL");
            tileset = new Tileset(tileset_bytes, Model.WorldMapGraphics, palettes, 16, 16, TilesetType.WorldMap);
            logoTileset = new Tileset(Model.WorldMapLogoTileset, Model.WorldMapLogos, logoPalette, 16, 16, TilesetType.WorldMapLogo);
            SetWorldMapImage();
            SetLocationsImage();
            SetBannerImage();
            updating = false;
            GC.Collect();
            Cursor.Current = Cursors.Arrow;
        }
        // tooltips
        public void Assemble()
        {
            Model.ROM[0x037DCF] = (byte)this.music.SelectedIndex;
            //
            Model.Compress(Model.WorldMapLogos, 0x3E004C, 0x2000, 0xE1C, "World map logos, banners");
            //
            foreach (WorldMap wm in worldMaps)
                wm.Assemble();
            // Graphics
            Model.WorldMapLogoPalette.Assemble(Model.MenuPalettes, 0xE0);
            int offset = Bits.GetShort(Model.ROM, 0x3E000C) + 0x3E0000;
            int maxComp = Bits.GetShort(Model.ROM, 0x3E000E) - Bits.GetShort(Model.ROM, 0x3E000C);
            Model.Compress(Model.MenuPalettes, offset, 0x200, maxComp, "World map logo palettes");
            Model.Compress(Model.WorldMapGraphics, 0x3E2E81, 0x8000, 0x56F6, "Graphics");
            // Tilesets
            byte[] compress = new byte[0x800];
            int totalSize = 0;
            int pOffset = 0x3E0014;
            int dOffset = 0x3E929F;
            int size = 0;
            for (int i = 0; i < Model.WorldMapTilesets.Length; i++)
            {
                Bits.SetShort(Model.ROM, pOffset, (ushort)dOffset);
                size = Comp.Compress(Model.WorldMapTilesets[i], compress);
                totalSize += size + 1;
                if (totalSize > 0x4D8)
                {
                    MessageBox.Show(
                        "Recompressed tilesets exceed allotted ROM space by " + (totalSize - 0x4D6).ToString() + " bytes.\nSaving has been discontinued for tilesets " + i.ToString() + " and higher.\nChange or delete some tiles to reduce the size.",
                        "LAZY SHELL");
                    break;
                }
                else
                {
                    Model.ROM[dOffset] = 1; dOffset++;
                    Bits.SetBytes(Model.ROM, dOffset, compress, 0, size - 1);
                    dOffset += size;
                    pOffset += 2;
                }
            }
            Bits.SetShort(Model.ROM, pOffset, (ushort)dOffset);
            Model.Compress(Model.WorldMapLogoTileset, dOffset, 0x800, 0xC1, "World map logo tileset");
            // Palettes
            palettes.Assemble(Model.WorldMapPalettes, 0);
            Model.Compress(Model.WorldMapPalettes, 0x3E988C, 0x100, 0xD4, "Palette set");
            foreach (Location mp in locations)
                mp.Assemble();
            AssembleLocationTexts();
            checksum = Do.GenerateChecksum(worldMaps, Model.WorldMapGraphics, Model.WorldMapPalettes,
                Model.WorldMapSprites, Model.WorldMapTilesets, Model.Locations);
        }
        private void AddLocations()
        {
            worldMapLocations = new ArrayList[worldMaps.Length];
            for (int i = 0, b = 0; i < locations.Length && b < worldMaps.Length; b++)
            {
                worldMapLocations[b] = new ArrayList();
                for (int a = 0; i < locations.Length && a < worldMaps[b].Locations; a++, i++)
                    worldMapLocations[b].Add(locations[i]);
            }
        }
        private void SetWorldMapImage()
        {
            int[] pixels = Do.TilesetToPixels(tileset.Tileset_tiles, 16, 16, 0, false);
            tilesetImage = new Bitmap(Do.PixelsToImage(pixels, 256, 256));
            pictureBoxTileset.BackColor = Color.FromArgb(palettes.Reds[0], palettes.Greens[0], palettes.Blues[0]);
            pictureBoxTileset.Invalidate();
        }
        private void SetLocationsImage()
        {
            SetActiveLocations();
            SetWorldMapTextImage();
        }
        private void SetBannerImage()
        {
            int[] pixels = Do.TilesetToPixels(logoTileset.Tileset_tiles, 16, 16, 0, false);
            logoImage = new Bitmap(Do.PixelsToImage(pixels, 256, 256));
            pictureBoxTileset.Invalidate();
        }
        private void SetWorldMapTextImage()
        {
            int[] pixels = drawName.GetPreview(fontDialogue, Model.FontPaletteDialogue.Palettes[1], locations[index_l].Name, false);
            int[] cropped;
            Rectangle region = Do.Crop(pixels, out cropped, 256, 32, true, false, true, false);
            locationText = new Bitmap(Do.PixelsToImage(cropped, region.Width, region.Height));
            pictureBoxTileset.Invalidate();
        }
        // drawing
        private void SetActiveLocations()
        {
            pointActivePixels = new int[256 * 256];
            int[] point = Do.GetPixelRegion(Model.WorldMapSprites, 0x20, GetPointPalette(), 16, 0, 1, 2, 1, 0);
            Location temp;
            for (int i = 0; i < worldMap.Locations; i++)
            {
                temp = (Location)worldMapLocations[index][i];
                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        if (point[y * 16 + x] != 0 &&
                            (y + temp.Y) >= 0 &&
                            (y + temp.Y) < 256 &&
                            (x + temp.X) >= 0 &&
                            (x + temp.X) < 256)
                            pointActivePixels[(y + temp.Y) * 256 + x + temp.X] = temp.Index + 1;
                    }
                }
            }
        }
        private int[] GetLocationPixels(bool hilite)
        {
            int[] pixels = Do.GetPixelRegion(Model.WorldMapSprites, 0x20, GetPointPalette(), 16, 0, 1, 2, 1, 0);
            if (hilite)
                return Do.Hilite(pixels, 16, 8);
            else
                return pixels;
        }
        private int[] GetMarioPixels()
        {
            int[] pixels = new int[16 * 32];
            int[] bottom = Do.GetPixelRegion(Model.WorldMapSprites, 0x20, GetPointPalette(), 16, 10, 0, 2, 2, 0);
            int[] top = Do.GetPixelRegion(Model.WorldMapSprites, 0x20, GetPointPalette(), 16, 4, 0, 2, 2, 0);
            Rectangle r = new Rectangle(0, 0, 16, 16);
            Do.PixelsToPixels(bottom, pixels, 16, 16, r, r);
            Do.PixelsToPixels(top, pixels, 16, 16, r, new Rectangle(0, 16, 16, 16));
            return pixels;
        }
        private int[] GetPointPalette()
        {
            double multiplier = 8; // 8;
            ushort color = 0;
            int[] red = new int[16], green = new int[16], blue = new int[16];
            for (int i = 0; i < 16; i++) // 16 colors in palette
            {
                color = Bits.GetShort(Model.ROM, i * 2 + 0x3DFF00);
                red[i] = (byte)((color % 0x20) * multiplier);
                green[i] = (byte)(((color >> 5) % 0x20) * multiplier);
                blue[i] = (byte)(((color >> 10) % 0x20) * multiplier);
            }
            int[] temp = new int[16];
            for (int i = 0; i < 16; i++)
                temp[i] = Color.FromArgb(255, red[i], green[i], blue[i]).ToArgb();
            return temp;
        }
        // Editor loading
        private void LoadPaletteEditor()
        {
            if (paletteEditor == null)
            {
                paletteEditor = new PaletteEditor(new Function(PaletteUpdate), palettes, 8, 0, 8);
                paletteEditor.FormClosing += new FormClosingEventHandler(editor_FormClosing);
            }
            else
                paletteEditor.Reload(new Function(PaletteUpdate), palettes, 8, 0, 8);
        }
        private void LoadGraphicEditor()
        {
            if (graphicEditor == null)
            {
                graphicEditor = new GraphicEditor(new Function(GraphicUpdate),
                    tileset.Graphics, tileset.Graphics.Length, 0, palettes, 0, 0x20);
                graphicEditor.FormClosing += new FormClosingEventHandler(editor_FormClosing);
            }
            else
                graphicEditor.Reload(new Function(GraphicUpdate),
                    tileset.Graphics, tileset.Graphics.Length, 0, palettes, 0, 0x20);
        }
        private void LoadLogoPaletteEditor()
        {
            if (logoPaletteEditor == null)
            {
                logoPaletteEditor = new PaletteEditor(new Function(LogoPaletteUpdate), logoPalette, 1, 0, 1);
                logoPaletteEditor.FormClosing += new FormClosingEventHandler(editor_FormClosing);
            }
            else
                logoPaletteEditor.Reload(new Function(LogoPaletteUpdate), logoPalette, 1, 0, 1);
        }
        private void LoadLogoGraphicEditor()
        {
            if (logoGraphicEditor == null)
            {
                logoGraphicEditor = new GraphicEditor(new Function(LogoGraphicUpdate),
                    Model.WorldMapLogos, Model.WorldMapLogos.Length, 0, logoPalette, 0, 0x20);
                logoGraphicEditor.FormClosing += new FormClosingEventHandler(editor_FormClosing);
            }
            else
                logoGraphicEditor.Reload(new Function(LogoGraphicUpdate),
                    Model.WorldMapLogos, Model.WorldMapLogos.Length, 0, logoPalette, 0, 0x20);
        }
        private void LoadSpriteEditor()
        {
            if (spriteEditor == null)
            {
                spriteEditor = new GraphicEditor(new Function(LogoGraphicUpdate),
                    Model.WorldMapSprites, Model.WorldMapSprites.Length, 0, logoPalette, 0, 0x20);
                spriteEditor.FormClosing += new FormClosingEventHandler(editor_FormClosing);
            }
            else
                spriteEditor.Reload(new Function(LogoGraphicUpdate),
                    Model.WorldMapSprites, Model.WorldMapSprites.Length, 0, logoPalette, 0, 0x20);
        }
        private void LoadTileEditor()
        {
            if (tileEditor == null)
            {
                tileEditor = new TileEditor(new Function(TileUpdate),
                this.tileset.Tileset_tiles[mouseDownTile],
                tileset.Graphics, palettes, 0x20, true);
                tileEditor.FormClosing += new FormClosingEventHandler(editor_FormClosing);
            }
            else
                tileEditor.Reload(new Function(TileUpdate),
                this.tileset.Tileset_tiles[mouseDownTile],
                tileset.Graphics, palettes, 0x20);
        }
        // Editor updating
        private void TileUpdate()
        {
            tileset.DrawTileset(tileset.Tileset_tiles, tileset.Tileset_bytes);
            SetWorldMapImage();
        }
        private void PaletteUpdate()
        {
            this.tileset = new Tileset(tileset_bytes, Model.WorldMapGraphics, palettes, 16, 16, TilesetType.WorldMap);
            SetWorldMapImage();
            LoadGraphicEditor();
            LoadTileEditor();
            checksum--;   // b/c switching colors won't modify checksum
        }
        private void GraphicUpdate()
        {
            this.tileset = new Tileset(tileset_bytes, Model.WorldMapGraphics, palettes, 16, 16, TilesetType.WorldMap);
            SetWorldMapImage();
            LoadTileEditor();
        }
        private void LogoPaletteUpdate()
        {
            logoTileset = new Tileset(Model.WorldMapLogoTileset, Model.WorldMapLogos, logoPalette, 16, 16, TilesetType.WorldMapLogo);
            SetBannerImage();
            LoadLogoGraphicEditor();
            checksum--;
        }
        private void LogoGraphicUpdate()
        {
            logoTileset = new Tileset(Model.WorldMapLogoTileset, Model.WorldMapLogos, logoPalette, 16, 16, TilesetType.WorldMapLogo);
            SetBannerImage();
            checksum--;
        }
        // Editing
        private void DrawHoverBox(Graphics g)
        {
            Rectangle r = new Rectangle(mousePosition.X / 16 * 16 * zoom, mousePosition.Y / 16 * 16 * zoom, 16 * zoom, 16 * zoom);
            g.FillRectangle(new SolidBrush(Color.FromArgb(96, 0, 0, 0)), r);
        }
        private void Copy()
        {
            if (overlay.SelectTS == null)
                return;
            if (draggedTiles != null)
            {
                this.copiedTiles = draggedTiles;
                return;
            }
            // make the copy
            int x_ = overlay.SelectTS.Location.X / 16;
            int y_ = overlay.SelectTS.Location.Y / 16;
            this.copiedTiles = new CopyBuffer(overlay.SelectTS.Width, overlay.SelectTS.Height);
            Tile[] copiedTiles = new Tile[(overlay.SelectTS.Width / 16) * (overlay.SelectTS.Height / 16)];
            for (int y = 0; y < overlay.SelectTS.Height / 16; y++)
            {
                for (int x = 0; x < overlay.SelectTS.Width / 16; x++)
                {
                    copiedTiles[y * (overlay.SelectTS.Width / 16) + x] =
                        tileset.Tileset_tiles[(y + y_) * 16 + x + x_].Copy();
                }
            }
            this.copiedTiles.Tiles = copiedTiles;
        }
        /// <summary>
        /// Start dragging a selection.
        /// </summary>
        private void Drag()
        {
            if (overlay.SelectTS == null)
                return;
            // make the copy
            int x_ = overlay.SelectTS.Location.X / 16;
            int y_ = overlay.SelectTS.Location.Y / 16;
            this.draggedTiles = new CopyBuffer(overlay.SelectTS.Width, overlay.SelectTS.Height);
            Tile[] draggedTiles = new Tile[(overlay.SelectTS.Width / 16) * (overlay.SelectTS.Height / 16)];
            for (int y = 0; y < overlay.SelectTS.Height / 16; y++)
            {
                for (int x = 0; x < overlay.SelectTS.Width / 16; x++)
                {
                    draggedTiles[y * (overlay.SelectTS.Width / 16) + x] =
                        tileset.Tileset_tiles[(y + y_) * 16 + x + x_].Copy();
                }
            }
            this.draggedTiles.Tiles = draggedTiles;
            selection = new Bitmap(this.draggedTiles.Image);
            Delete();
        }
        private void Cut()
        {
            if (overlay.SelectTS == null || overlay.SelectTS.Size == new Size(0, 0))
                return;
            Copy();
            Delete();
            if (commandCount > 0)
            {
                commandStack.Push(commandCount);
                commandCount = 0;
            }
        }
        private void Paste(Point location, CopyBuffer buffer)
        {
            if (buffer == null)
                return;
            moving = true;
            // now dragging a new selection
            draggedTiles = buffer;
            selection = buffer.Image;
            overlay.SelectTS = new Overlay.Selection(16, location, buffer.Size, pictureBoxTileset);
            pictureBoxTileset.Invalidate();
            defloating = false;
        }
        /// <summary>
        /// "Cements" either a dragged selection or a newly pasted selection.
        /// </summary>
        /// <param name="buffer">The dragged selection or the newly pasted selection.</param>
        private void Defloat(CopyBuffer buffer)
        {
            byte[] oldTileset = Bits.Copy(tileset.Tileset_bytes);
            //
            selection = null;
            int x_ = overlay.SelectTS.X / 16;
            int y_ = overlay.SelectTS.Y / 16;
            for (int y = 0; y < buffer.Height / 16; y++)
            {
                for (int x = 0; x < buffer.Width / 16; x++)
                {
                    if (y + y_ < 0 || y + y_ >= 16 ||
                        x + x_ < 0 || x + x_ >= 16)
                        continue;
                    Tile tile = buffer.Tiles[y * (buffer.Width / 16) + x];
                    tileset.Tileset_tiles[(y + y_) * 16 + x + x_] = tile.Copy();
                    tileset.Tileset_tiles[(y + y_) * 16 + x + x_].Index = (y + y_) * 16 + x + x_;
                }
            }
            tileset.DrawTileset(tileset.Tileset_tiles, tileset.Tileset_bytes);
            commandStack.Push(commandCount + 1);
            commandCount = 0;
            SetWorldMapImage();
            defloating = true;
            //
            commandStack.Push(new TilesetCommand(tileset, oldTileset, Model.WorldMapGraphics, 0x20, worldMapName));
        }
        private void Defloat()
        {
            if (copiedTiles != null && !defloating)
                Defloat(copiedTiles);
            if (draggedTiles != null)
            {
                Defloat(draggedTiles);
                draggedTiles = null;
            }
            moving = false;
            overlay.SelectTS = null;
            Cursor.Position = Cursor.Position;
        }
        private void Delete()
        {
            if (overlay.SelectTS == null)
                return;
            byte[] oldTileset = Bits.Copy(tileset.Tileset_bytes);
            //
            int x_ = overlay.SelectTS.Location.X / 16;
            int y_ = overlay.SelectTS.Location.Y / 16;
            for (int y = 0; y < overlay.SelectTS.Height / 16 && y + y_ < 0x100; y++)
            {
                for (int x = 0; x < overlay.SelectTS.Width / 16 && x + x_ < 0x100; x++)
                    tileset.Tileset_tiles[(y + y_) * 16 + x + x_].Clear();
            }
            tileset.DrawTileset(tileset.Tileset_tiles, tileset.Tileset_bytes);
            SetWorldMapImage();
            //
            commandStack.Push(new TilesetCommand(tileset, oldTileset, Model.WorldMapGraphics, 0x20, worldMapName));
            commandCount++;
        }
        private void Flip(string type)
        {
            if (draggedTiles != null)
                Defloat(draggedTiles);
            if (overlay.SelectTS == null)
                return;
            int x_ = overlay.SelectTS.Location.X / 16;
            int y_ = overlay.SelectTS.Location.Y / 16;
            CopyBuffer buffer = new CopyBuffer(overlay.SelectTS.Width, overlay.SelectTS.Height);
            Tile[] copiedTiles = new Tile[(overlay.SelectTS.Width / 16) * (overlay.SelectTS.Height / 16)];
            for (int y = 0; y < overlay.SelectTS.Height / 16; y++)
            {
                for (int x = 0; x < overlay.SelectTS.Width / 16; x++)
                {
                    copiedTiles[y * (overlay.SelectTS.Width / 16) + x] =
                        tileset.Tileset_tiles[(y + y_) * 16 + x + x_].Copy();
                }
            }
            if (type == "mirror")
                Do.FlipHorizontal(copiedTiles, overlay.SelectTS.Width / 16, overlay.SelectTS.Height / 16);
            else if (type == "invert")
                Do.FlipVertical(copiedTiles, overlay.SelectTS.Width / 16, overlay.SelectTS.Height / 16);
            buffer.Tiles = copiedTiles;
            Defloat(buffer);
            tileset.DrawTileset(tileset.Tileset_tiles, tileset.Tileset_bytes);
            SetWorldMapImage();
        }
        #endregion
        #region Eventhandlers
        // main
        private void WorldMaps_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == (Keys.Control | Keys.S))
                Assemble();
        }
        private void WorldMaps_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Do.GenerateChecksum(worldMaps, Model.WorldMapGraphics, Model.WorldMapPalettes,
                Model.WorldMapSprites, Model.WorldMapTilesets, Model.Locations) == checksum)
                goto Close;
            DialogResult result = MessageBox.Show(
                "World Maps have not been saved.\n\nWould you like to save changes?", "LAZY SHELL",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (result == DialogResult.Yes)
                Assemble();
            else if (result == DialogResult.No)
            {
                Model.Locations = null;
                Model.WorldMapGraphics = null;
                Model.WorldMapPalettes = null;
                Model.WorldMaps = null;
                Model.WorldMapSprites = null;
                Model.WorldMapTilesets[0] = null;
                Model.Palettes = null;
            }
            else if (result == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
        Close:
            tileEditor.Close();
            tileEditor.Dispose();
            if (paletteEditor != null)
            {
                paletteEditor.Close();
                paletteEditor.Dispose();
            }
            if (graphicEditor != null)
            {
                graphicEditor.Close();
                graphicEditor.Dispose();
            }
            if (logoPaletteEditor != null)
            {
                logoPaletteEditor.Close();
                logoPaletteEditor.Dispose();
            }
            if (logoGraphicEditor != null)
            {
                logoGraphicEditor.Close();
                logoGraphicEditor.Dispose();
            }
        }
        private void worldMapName_SelectedIndexChanged(object sender, EventArgs e)
        {
            Defloat();
            RefreshWorldMap();
        }
        private void showLocations_Click(object sender, EventArgs e)
        {
            toolStrip2.Enabled = !showLocations.Checked || !showBanner.Checked;
            if (showLocations.Checked)
            {
                buttonEditSelect.Checked = false;
                if (draggedTiles != null)
                    Defloat(draggedTiles);
                overlay.SelectTS = null;
                pictureBoxTileset.Cursor = Cursors.SizeAll;
            }
            else
                pictureBoxTileset.Cursor = Cursors.Arrow;
            pictureBoxTileset.Invalidate();
        }
        private void showBanner_Click(object sender, EventArgs e)
        {
            toolStrip2.Enabled = !showLocations.Checked || !showBanner.Checked;
            if (showLocations.Checked)
            {
                buttonEditSelect.Checked = false;
                if (draggedTiles != null)
                    Defloat(draggedTiles);
                overlay.SelectTS = null;
                pictureBoxTileset.Cursor = Cursors.SizeAll;
            }
            else
                pictureBoxTileset.Cursor = Cursors.Arrow;
            pictureBoxTileset.Invalidate();
        }
        private void pointCount_ValueChanged(object sender, EventArgs e)
        {
            if (updating)
                return;
            worldMap.Locations = (byte)pointCount.Value;
            AddLocations();
            SetLocationsImage();
        }
        private void worldMapTileset_ValueChanged(object sender, EventArgs e)
        {
            if (updating)
                return;
            worldMap.Tileset = (byte)worldMapTileset.Value;
            tileset = new Tileset(tileset_bytes, Model.WorldMapGraphics, palettes, 16, 16, TilesetType.WorldMap);
            SetWorldMapImage();
        }
        private void worldMapXCoord_ValueChanged(object sender, EventArgs e)
        {
            if (updating)
                return;
            worldMap.X = (sbyte)worldMapXCoord.Value;
            pictureBoxTileset.Invalidate();
        }
        private void worldMapYCoord_ValueChanged(object sender, EventArgs e)
        {
            if (updating)
                return;
            worldMap.Y = (sbyte)worldMapYCoord.Value;
            pictureBoxTileset.Invalidate();
        }
        // image
        private void pictureBoxTileset_Paint(object sender, PaintEventArgs e)
        {
            if (tilesetImage == null)
                return;
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            Rectangle rdst = new Rectangle(0, 0, 256, 256);
            if (showLocations.Checked || showBanner.Checked)
            {
                double third = 100.0 / 3.0;
                rdst.Y -= 8;
                rdst.Width = (int)Do.PercentIncrease(third, 256.0);
                rdst.Height = (int)Do.PercentIncrease(third, 256.0);
                double x = worldMap.X;
                double y = worldMap.Y;
                x = (int)Do.PercentIncrease(third, x);
                y = (int)Do.PercentIncrease(third, y);
                x -= Do.PercentDecrease(third, 256) / 4.0;
                y -= Do.PercentDecrease(third, 256) / 4.0;
                rdst.Offset((int)x, (int)y);
            }
            if (buttonToggleBG.Checked)
                e.Graphics.Clear(Color.FromArgb(palettes.Palette[0]));
            if (tilesetImage != null)
                e.Graphics.DrawImage(tilesetImage, rdst, 0, 0, 256, 256, GraphicsUnit.Pixel);
            if (showLocations.Checked)
            {
                foreach (Location location in worldMapLocations[index])
                {
                    if (locationImage == null)
                        locationImage = new Bitmap(Do.PixelsToImage(GetLocationPixels(false), 16, 8));
                    if (locationImage_S == null)
                        locationImage_S = new Bitmap(Do.PixelsToImage(GetLocationPixels(true), 16, 8));
                    if (marioImage == null)
                        marioImage = new Bitmap(Do.PixelsToImage(GetMarioPixels(), 16, 32));
                    if (location.Index == locationNum.Value)
                    {
                        e.Graphics.DrawImage(locationImage_S, location.X, location.Y);
                    }
                    else
                        e.Graphics.DrawImage(locationImage, location.X, location.Y);
                }
            }
            if (showBanner.Checked)
            {
                if (logoImage != null)
                    e.Graphics.DrawImage(logoImage, 0, -8);
                if (locationText != null)
                    e.Graphics.DrawImage(locationText, 128 - (locationText.Width / 2), 182);
            }
            if (moving && selection != null)
            {
                Rectangle rsrc = new Rectangle(0, 0, overlay.SelectTS.Width, overlay.SelectTS.Height);
                rdst = new Rectangle(
                    overlay.SelectTS.X * zoom, overlay.SelectTS.Y * zoom,
                    rsrc.Width * zoom, rsrc.Height * zoom);
                e.Graphics.DrawImage(new Bitmap(selection), rdst, rsrc, GraphicsUnit.Pixel);
                Do.DrawString(e.Graphics, new Point(rdst.X, rdst.Y + rdst.Height),
                    "click/drag", Color.White, Color.Black, new Font("Tahoma", 6.75F, FontStyle.Bold));
            }
            float[][] matrixItems ={ 
               new float[] {1, 0, 0, 0, 0},
               new float[] {0, 1, 0, 0, 0},
               new float[] {0, 0, 1, 0, 0},
               new float[] {0, 0, 0, 0.50F, 0}, 
               new float[] {0, 0, 0, 0, 1}};
            ColorMatrix cm = new ColorMatrix(matrixItems);
            ImageAttributes ia = new ImageAttributes();
            ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
            if (mouseEnter && !showLocations.Checked && !showBanner.Checked)
                DrawHoverBox(e.Graphics);
            if (buttonToggleCartGrid.Checked)
                overlay.DrawTileGrid(e.Graphics, Color.Gray, pictureBoxTileset.Size, new Size(16, 16), 1, true);
            if (overlay.SelectTS != null)
            {
                if (buttonToggleCartGrid.Checked)
                    overlay.SelectTS.DrawSelectionBox(e.Graphics, 1, Color.Yellow);
                else
                    overlay.SelectTS.DrawSelectionBox(e.Graphics, 1);
            }
        }
        private void pictureBoxTileset_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
                return;
            mouseDownObject = null;
            // set a floor and ceiling for the coordinates
            int x = Math.Max(0, Math.Min(e.X, pictureBoxTileset.Width));
            int y = Math.Max(0, Math.Min(e.Y, pictureBoxTileset.Height));
            pictureBoxTileset.Focus();
            if (buttonEditSelect.Checked)
            {
                // if moving an object and outside of it, paste it
                if (moving && mouseOverObject != "selection")
                {
                    // if copied tiles were pasted and not dragging a non-copied selection
                    if (copiedTiles != null && draggedTiles == null)
                        Defloat(copiedTiles);
                    if (draggedTiles != null)
                    {
                        Defloat(draggedTiles);
                        draggedTiles = null;
                    }
                    selection = null;
                    moving = false;
                }
                // if making a new selection
                if (e.Button == MouseButtons.Left && mouseOverObject == null)
                    overlay.SelectTS = new Overlay.Selection(16, x / 16 * 16, y / 16 * 16, 16, 16, pictureBoxTileset);
                // if moving a current selection
                if (e.Button == MouseButtons.Left && mouseOverObject == "selection")
                {
                    mouseDownObject = "selection";
                    mouseDownPosition = overlay.SelectTS.MousePosition(x, y);
                    if (!moving)    // only do this if the current selection has not been initially moved
                    {
                        moving = true;
                        Drag();
                    }
                }
            }
            else if (showLocations.Checked)
            {
                if (pointActivePixels[y * 256 + x] != 0)
                {
                    locationNum.Value = pointActivePixels[y * 256 + x] - 1;
                    diffX = (int)(x - locationXCoord.Value);
                    diffY = (int)(y - locationYCoord.Value);
                    mouseDownObject = "location";
                    SetLocationsImage();
                }
                else
                {
                    diffX = (int)(x - worldMapXCoord.Value);
                    diffY = (int)(y - worldMapYCoord.Value);
                    mouseDownObject = "tileset";
                }
            }
            mouseDownTile = y / 16 * 16 + (x / 16);
            LoadTileEditor();
        }
        private void pictureBoxTileset_MouseMove(object sender, MouseEventArgs e)
        {
            // set a floor and ceiling for the coordinates
            int x = Math.Max(0, Math.Min(e.X, pictureBoxTileset.Width));
            int y = Math.Max(0, Math.Min(e.Y, pictureBoxTileset.Height));
            mouseOverObject = null;
            mousePosition = new Point(x, y);
            if (buttonEditSelect.Checked)
            {
                // if making a new selection
                if (e.Button == MouseButtons.Left && mouseDownObject == null && overlay.SelectTS != null)
                    overlay.SelectTS.Final = new Point(
                        Math.Min(x + 16, pictureBoxTileset.Width),
                        Math.Min(y + 16, pictureBoxTileset.Height));
                // if dragging the current selection
                if (e.Button == MouseButtons.Left && mouseDownObject == "selection")
                    overlay.SelectTS.Location = new Point(
                        x / 16 * 16 - mouseDownPosition.X,
                        y / 16 * 16 - mouseDownPosition.Y);
                // check if over selection
                if (e.Button == MouseButtons.None && overlay.SelectTS != null && overlay.SelectTS.MouseWithin(x, y))
                {
                    mouseOverObject = "selection";
                    pictureBoxTileset.Cursor = Cursors.SizeAll;
                }
                else
                    pictureBoxTileset.Cursor = Cursors.Cross;
            }
            else if (showLocations.Checked)
            {
                if (e.Button == MouseButtons.Left)
                {
                    if (mouseDownObject == "location")
                    {
                        x = Math.Max(0, Math.Min(e.X - diffX, pictureBoxTileset.Width));
                        y = Math.Max(0, Math.Min(e.Y - diffY, pictureBoxTileset.Height));
                        locationXCoord.Value = Math.Min(255, x);
                        locationYCoord.Value = Math.Min(255, y);
                    }
                    if (mouseDownObject == "tileset")
                    {
                        x = Math.Max(-128, Math.Min(e.X - diffX, pictureBoxTileset.Width));
                        y = Math.Max(-128, Math.Min(e.Y - diffY, pictureBoxTileset.Height));
                        worldMapXCoord.Value = Math.Min(127, x);
                        worldMapYCoord.Value = Math.Min(127, y);
                    }
                }
                else
                {
                    if (pointActivePixels[e.Y * 256 + e.X] != 0)
                        pictureBoxTileset.Cursor = Cursors.Hand;
                    else
                        pictureBoxTileset.Cursor = Cursors.SizeAll;
                }
            }
            pictureBoxTileset.Invalidate();
        }
        private void pictureBoxTileset_MouseUp(object sender, MouseEventArgs e)
        {
            SetLocationsImage();
        }
        private void pictureBoxTileset_MouseClick(object sender, MouseEventArgs e)
        {
        }
        private void pictureBoxTileset_MouseDoubleClick(object sender, MouseEventArgs e)
        {
        }
        private void pictureBoxTileset_MouseEnter(object sender, EventArgs e)
        {
            mouseEnter = true;
            pictureBoxTileset.Focus();
            pictureBoxTileset.Invalidate();
        }
        private void pictureBoxTileset_MouseLeave(object sender, EventArgs e)
        {
            mouseEnter = false;
            pictureBoxTileset.Invalidate();
        }
        private void pictureBoxTileset_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            switch (e.KeyData)
            {
                case Keys.B: buttonToggleBG.PerformClick(); break;
                case Keys.G: buttonToggleCartGrid.PerformClick(); break;
                case Keys.S: buttonEditSelect.PerformClick(); break;
                case Keys.Control | Keys.V: buttonEditPaste.PerformClick(); break;
                case Keys.Control | Keys.C: buttonEditCopy.PerformClick(); break;
                case Keys.Delete: buttonEditDelete.PerformClick(); break;
                case Keys.Control | Keys.X: buttonEditCut.PerformClick(); break;
                case Keys.Control | Keys.D:
                    if (draggedTiles != null)
                        Defloat(draggedTiles);
                    else
                    {
                        overlay.SelectTS = null;
                        pictureBoxTileset.Invalidate();
                    }
                    break;
                case Keys.Control | Keys.A:
                    overlay.SelectTS = new Overlay.Selection(16, 0, 0, 256, 256, pictureBoxTileset);
                    pictureBoxTileset.Invalidate();
                    break;
                case Keys.Control | Keys.Z: buttonEditUndo.PerformClick(); break;
                case Keys.Control | Keys.Y: buttonEditRedo.PerformClick(); break;
            }
        }
        // drawing buttons
        private void buttonToggleCartGrid_Click(object sender, EventArgs e)
        {
            pictureBoxTileset.Invalidate();
        }
        private void buttonToggleBG_Click(object sender, EventArgs e)
        {
            pictureBoxTileset.Invalidate();
        }
        private void buttonEditDelete_Click(object sender, EventArgs e)
        {
            if (!moving)
                Delete();
            else
            {
                moving = false;
                draggedTiles = null;
                pictureBoxTileset.Invalidate();
            }
            if (!moving && commandCount > 0)
            {
                commandStack.Push(commandCount);
                commandCount = 0;
            }
        }
        private void buttonEditCopy_Click(object sender, EventArgs e)
        {
            Copy();
        }
        private void buttonEditCut_Click(object sender, EventArgs e)
        {
            Cut();
        }
        private void buttonEditPaste_Click(object sender, EventArgs e)
        {
            if (draggedTiles != null)
                Defloat(draggedTiles);
            Paste(new Point(16, 16), copiedTiles);
        }
        private void buttonEditUndo_Click(object sender, EventArgs e)
        {
            commandStack.UndoCommand();
            SetWorldMapImage();
        }
        private void buttonEditRedo_Click(object sender, EventArgs e)
        {
            commandStack.RedoCommand();
            SetWorldMapImage();
        }
        private void buttonEditSelect_Click(object sender, EventArgs e)
        {
            if (buttonEditSelect.Checked)
                this.pictureBoxTileset.Cursor = System.Windows.Forms.Cursors.Cross;
            else
                this.pictureBoxTileset.Cursor = System.Windows.Forms.Cursors.Arrow;
            Defloat();
            this.pictureBoxTileset.Invalidate();
        }
        // open editors
        private void openPalettes_Click(object sender, EventArgs e)
        {
            if (paletteEditor == null)
                LoadPaletteEditor();
            paletteEditor.Visible = true;
        }
        private void openGraphics_Click(object sender, EventArgs e)
        {
            if (graphicEditor == null)
                LoadGraphicEditor();
            graphicEditor.Visible = true;
        }
        private void openLogos_Click(object sender, EventArgs e)
        {
            if (logoGraphicEditor == null)
                LoadLogoGraphicEditor();
            logoGraphicEditor.Visible = true;
        }
        private void logoBannerPalettesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (logoPaletteEditor == null)
                LoadLogoPaletteEditor();
            logoPaletteEditor.Visible = true;
        }
        private void spriteGraphicsToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }
        private void openTileEditor_Click(object sender, EventArgs e)
        {
            tileEditor.Visible = true;
        }
        private void editor_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            ((Form)sender).Hide();
        }
        // menu strip
        private void save_Click(object sender, EventArgs e)
        {
            Assemble();
        }
        private void import_Click(object sender, EventArgs e)
        {
            new IOElements(worldMaps, index, "IMPORT WORLD MAPS...").ShowDialog();
            RefreshWorldMap();
        }
        private void export_Click(object sender, EventArgs e)
        {
            new IOElements(worldMaps, index, "EXPORT WORLD MAPS...").ShowDialog();
        }
        private void clear_Click(object sender, EventArgs e)
        {
            ClearElements clearElements = new ClearElements(worldMaps, index, "CLEAR WORLD MAPS...");
            clearElements.ShowDialog();
            if (clearElements.DialogResult == DialogResult.Cancel)
                return;
            RefreshWorldMap();
        }
        private void resetWorldMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("You're about to undo all changes to the current world map. Go ahead with reset?",
                "LAZY SHELL", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                return;
            int pointer = Bits.GetShort(Model.ROM, worldMap.Tileset * 2 + 0x3E0014);
            int offset = 0x3E0000 + pointer + 1;
            Model.WorldMapTilesets[worldMap.Tileset] = Comp.Decompress(Model.ROM, offset, 0x800);
            worldMap = new WorldMap(index);
            RefreshWorldMap();
        }
        private void resetLocationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("You're about to undo all changes to the current location. Go ahead with reset?",
                "LAZY SHELL", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                return;
            location = new Location(index_l);
            AddLocations();
            RefreshLocationEditor();
            SetLocationsImage();
        }
        // context menu strip
        private void mirrorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Flip("mirror");
        }
        private void invertToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Flip("invert");
        }
        private void saveImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Do.Export(tilesetImage, "worldMap." + index.ToString("d2") + ".png");
        }
        #endregion
    }
}
