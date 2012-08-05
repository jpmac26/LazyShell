﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using LAZYSHELL.Undo;

namespace LAZYSHELL
{
    public partial class TilemapEditor : Form
    {
        #region Variables
        // main
        private delegate void Function();
        public PictureBox Picture { get { return pictureBoxLevel; } set { pictureBoxLevel = value; } }
        private Levels levels;
        private MineCart minecart;
        private Level level;
        private Tilemap tilemap;
        private LevelSolidMap solidityMap;
        private Solidity solidity = Solidity.Instance;
        private Tileset tileset;
        private Bitmap tilemapImage, p1Image, p1SolidityImage;
        private Overlay overlay;
        private State state;
        // editors
        private TilesetEditor tilesetEditor;
        private LevelsSolidTiles levelsSolidTiles;
        private LevelsTemplate levelsTemplate;
        private PaletteEditor paletteEditor;
        // main classes
        private LevelMap levelMap { get { return levels.LevelMap; } }
        private LevelLayer layer { get { return level.Layer; } }
        private LevelExits exits { get { return level.LevelExits; } set { level.LevelExits = value; } }
        private LevelEvents events { get { return level.LevelEvents; } set { level.LevelEvents = value; } }
        private LevelNPCs npcs { get { return level.LevelNPCs; } set { level.LevelNPCs = value; } }
        private LevelOverlaps overlaps { get { return level.LevelOverlaps; } set { level.LevelOverlaps = value; } }
        private LevelTileMods tileMods { get { return level.LevelTileMods; } set { level.LevelTileMods = value; } }
        private LevelSolidMods solidMods { get { return level.LevelSolidMods; } set { level.LevelSolidMods = value; } }
        private SolidityTile[] solidTiles { get { return Model.SolidTiles; } }
        private LevelTemplate template { get { return levelsTemplate.Template; } }
        private int width { get { return tilemap.Width_p; } }
        private int height { get { return tilemap.Height_p; } }
        private MCObject[] mushrooms
        {
            get
            {
                if (minecart.Index == 0)
                    return minecart.MinecartData.M7ObjectsA;
                else if (minecart.Index == 1)
                    return minecart.MinecartData.M7ObjectsB;
                return null;
            }
            set
            {
                if (minecart.Index == 0)
                    minecart.MinecartData.M7ObjectsA = value;
                else if (minecart.Index == 1)
                    minecart.MinecartData.M7ObjectsB = value;
            }
        }
        private int erase
        {
            get
            {
                if (minecart != null && minecart.Index < 2)
                    return 0x4F;
                else
                    return 0;
            }
        }
        // buffers
        private CopyBuffer draggedTiles;
        private CopyBuffer copiedTiles;
        private CommandStack commandStack;
        private CommandStack commandStack_S;
        private CommandStack commandStack_TM;
        private CommandStack commandStack_SM;
        private Stack<int> pushes;
        private Stack<int> pops;
        private Bitmap selection;
        private Bitmap selsolidt;
        private Point selsolidt_location = new Point(-1, -1);
        private bool pasteFinal = false;
        // hover variables
        private string mouseOverObject;
        private int mouseOverTile = 0;
        private int mouseOverSolidTile = 0;
        private int mouseOverNPC = -1;
        private int mouseOverNPCInstance = -1;
        private int mouseOverExitField = -1;
        private int mouseOverEventField = -1;
        private int mouseOverOverlap = -1;
        private int mouseOverTileMod = 0;
        private int mouseOverSolidMod = 0;
        private int mouseOverSolidTileNum
        {
            get
            {
                return Bits.GetShort(solidityMap.Tilemap_Bytes, mouseOverSolidTile * 2);
            }
        }
        private int mouseOverMushroom = -1;
        private string mouseDownObject;
        private int mouseDownNPC = -1;
        private int mouseDownNPCInstance = -1;
        private int mouseDownExitField = -1;
        private int mouseDownEventField = -1;
        private int mouseDownOverlap = -1;
        private int mouseDownSolidTile = 0;
        private int mouseDownSolidTileNum
        {
            get
            {
                return Bits.GetShort(solidityMap.Tilemap_Bytes, mouseDownSolidTile * 2);
            }
        }
        private int mouseDownSolidTileIndex = -1;
        private int mouseDownTileMod = 0;
        private int mouseDownSolidMod = 0;
        private int mouseDownMushroom = -1;
        private Point mousePosition = new Point(0, 0);
        private Point mouseDownPosition = new Point(0, 0);
        private Point mouseTilePosition
        {
            get
            {
                return new Point(
                    Math.Min(63, mousePosition.X / 16),
                    Math.Min(63, mousePosition.Y / 16));
            }
        }
        private Point mouseIsometricPosition = new Point(0, 0);
        private Point mouseLastIsometricPosition = new Point(0, 0);
        private Point mouseDownIsometricPosition = new Point(0, 0);
        private Point autoScrollPos = new Point();
        private bool mouseWithinSameBounds = false;
        private bool mouseEnter = false;
        private int zoom = 1; public int Zoom { get { return zoom; } }
        private ZoomPanel zoomPanel;
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
        #endregion
        #region Functions
        // main
        public TilemapEditor(Form parent, Level level, Tilemap tilemap, LevelSolidMap solidityMap, Tileset tileset, Overlay overlay,
            PaletteEditor paletteEditor, TilesetEditor tilesetEditor, LevelsSolidTiles levelsSolidTiles, LevelsTemplate levelsTemplate)
        {
            this.state = State.Instance;
            this.levels = (Levels)parent;
            this.level = level;
            this.tilemap = tilemap;
            this.solidityMap = solidityMap;
            this.tileset = tileset;
            this.overlay = overlay;
            this.tilesetEditor = tilesetEditor;
            this.levelsSolidTiles = levelsSolidTiles;
            this.paletteEditor = paletteEditor;
            this.levelsTemplate = levelsTemplate;
            this.commandStack = new CommandStack();
            this.commandStack_S = new CommandStack();
            this.commandStack_TM = new CommandStack();
            this.commandStack_SM = new CommandStack();
            this.pushes = new Stack<int>();
            this.pops = new Stack<int>();
            InitializeComponent();
            this.pictureBoxLevel.Size = new Size(tilemap.Width_p * zoom, tilemap.Height_p * zoom);
            this.zoomPanel = new ZoomPanel(4);
            SetLevelImage();
            // toggle
            toggleBG.Checked = state.BG;
            toggleCartGrid.Checked = state.CartesianGrid;
            toggleEvents.Checked = state.Events;
            toggleExits.Checked = state.Exits;
            toggleL1.Checked = state.Layer1;
            toggleL2.Checked = state.Layer2;
            toggleL3.Checked = state.Layer3;
            toggleMask.Checked = state.Mask;
            toggleNPCs.Checked = state.NPCs;
            toggleIsoGrid.Checked = state.IsometricGrid;
            toggleOverlaps.Checked = state.Overlaps;
            toggleP1.Checked = state.Priority1;
            toggleSolid.Checked = state.SolidityLayer;
            toggleSolidMods.Checked = state.SolidMods;
            toggleTileMods.Checked = state.TileMods;
        }
        public void Reload(Form parent, Level level, Tilemap tilemap, LevelSolidMap solidmap, Tileset tileset, Overlay overlay,
            PaletteEditor paletteEditor, TilesetEditor tilesetEditor, LevelsSolidTiles levelsSolidTiles, LevelsTemplate levelsTemplate)
        {
            this.pictureBoxLevel.Size = new Size(tilemap.Width_p * zoom, tilemap.Height_p * zoom);
            if (this.level != level)
            {
                this.commandStack = new CommandStack();
                this.commandStack_S = new CommandStack();
                this.commandStack_TM = new CommandStack();
                this.commandStack_SM = new CommandStack();
                this.pushes = new Stack<int>();
                this.pops = new Stack<int>();
            }
            else
            {
                this.commandStack.SetTilemaps(tilemap);
                this.commandStack_S.SetSolidityMaps(solidmap);
            }
            this.levels = (Levels)parent;
            this.level = level;
            this.tilemap = tilemap;
            this.solidityMap = solidmap;
            this.tileset = tileset;
            this.overlay = overlay;
            this.tilesetEditor = tilesetEditor;
            this.levelsSolidTiles = levelsSolidTiles;
            this.paletteEditor = paletteEditor;
            this.levelsTemplate = levelsTemplate;

            p1Image = null;
            p1SolidityImage = null;
            selection = null;
            draggedTiles = null;
            overlay.Select = null;

            SetLevelImage();
        }
        //
        public TilemapEditor(Form parent, Tilemap tilemap, Tileset tileset, Overlay overlay, PaletteEditor paletteEditor, TilesetEditor tilesetEditor)
        {
            this.state = State.Instance2;
            this.minecart = (MineCart)parent;
            this.tilemap = tilemap;
            this.tileset = tileset;
            this.overlay = overlay;
            this.tilesetEditor = tilesetEditor;
            this.paletteEditor = paletteEditor;
            this.commandStack = new CommandStack();
            this.pushes = new Stack<int>();
            this.pops = new Stack<int>();
            InitializeComponent();
            this.pictureBoxLevel.Size = new Size(tilemap.Width_p * zoom, tilemap.Height_p * zoom);
            this.zoomPanel = new ZoomPanel(4);
            SetLevelImage();
            // toggle
            toggleBG.Visible = false;
            toggleMushrooms.Visible = minecart.Index < 2;
            toggleRails.Visible = minecart.Index < 2;
            toggleEvents.Visible = false;
            toggleExits.Visible = false;
            toggleL1.Visible = false;
            toggleL2.Visible = false;
            toggleL3.Visible = false;
            toggleMask.Visible = false;
            toggleNPCs.Visible = false;
            toggleIsoGrid.Visible = false;
            toggleOverlaps.Visible = false;
            toggleP1.Visible = minecart.Index > 1;
            toggleSolid.Visible = false;
            toggleSolidMods.Visible = false;
            toggleTileMods.Visible = false;
            //
            tags.Visible = false;
            editAllLayers.Visible = false;
            buttonDragSolidity.Visible = false;
            buttonEditTemplate.Visible = false;
            toolStripSeparator2.Visible = false;
            toolStripSeparator10.Visible = false;
            toolStripSeparator1.Visible = false;
            toolStripSeparator14.Visible = false;
            toolStripSeparator15.Visible = false;
            toolStripSeparator23.Visible = false;
            //
            toggleCartGrid.Checked = state.CartesianGrid;
            toggleMushrooms.Checked = state.Mushrooms;
            toggleRails.Checked = state.Rails;
            toggleP1.Checked = state.Priority1;
        }
        public void Reload(Form parent, Tilemap tilemap, Tileset tileset, Overlay overlay, PaletteEditor paletteEditor, TilesetEditor levelsTileset)
        {
            this.pictureBoxLevel.Size = new Size(tilemap.Width_p * zoom, tilemap.Height_p * zoom);
            this.commandStack = new CommandStack();
            this.pushes = new Stack<int>();
            this.pops = new Stack<int>();
            this.minecart = (MineCart)parent;
            this.tilemap = tilemap;
            this.tileset = tileset;
            this.overlay = overlay;
            this.tilesetEditor = levelsTileset;
            this.paletteEditor = paletteEditor;
            //
            p1Image = null;
            selection = null;
            draggedTiles = null;
            overlay.Select = null;
            //
            SetLevelImage();
            //
            toggleP1.Visible = minecart.Index > 1;
            toggleMushrooms.Visible = minecart.Index < 2;
            toggleRails.Visible = minecart.Index < 2;
        }
        private void SetLevelImage()
        {
            int[] levelPixels = tilemap.Pixels;
            tilemapImage = new Bitmap(Do.PixelsToImage(levelPixels, tilemap.Width_p, tilemap.Height_p));
            pictureBoxLevel.Invalidate();
        }
        private void UpdateCoordLabels()
        {
            int x = mousePosition.X;
            int y = mousePosition.Y;

            this.labelTileCoords.Text = "(x: " + (x / 16) + ", y: " + (y / 16) + ") Tile  |  ";
            this.labelTileCoords.Text += "(x: " +
                System.Convert.ToString(mouseIsometricPosition.X) + ", y: " +
                System.Convert.ToString(mouseIsometricPosition.Y) + ") Isometric  |  ";
            this.labelTileCoords.Text += "(x: " + x + ", y: " + y + ") Pixel";
        }
        private void ToggleTilesets()
        {
            levels.OpenTileset.Checked = !toggleSolidMods.Checked && !toggleSolid.Checked;
            levels.OpenSolidTileset.Checked = toggleSolidMods.Checked || toggleSolid.Checked;
            if (levels.OpenTileset.Checked)
            {
                levels.openSolidTileset_Click(null, null);
                levels.openTileset_Click(null, null);
            }
            else if (levels.OpenSolidTileset.Checked)
            {
                levels.openTileset_Click(null, null);
                levels.openSolidTileset_Click(null, null);
            }
        }
        // editing
        private bool CompareTiles(int x_, int y_, int layer)
        {
            for (int y = overlay.SelectTS.Y, b = y_; y < overlay.SelectTS.Terminal.Y; y += 16, b += 16)
            {
                for (int x = overlay.SelectTS.X, a = x_; x < overlay.SelectTS.Terminal.X; x += 16, a += 16)
                {
                    if (tilemap.GetTileNum(layer, a, b) != tileset.GetTileNum(layer, x / 16, y / 16))
                        return false;
                }
            }
            return true;
        }
        private void DrawBoundaries(Graphics g)
        {
            Rectangle r = new Rectangle(
                mousePosition.X / 16 * 16 * zoom, mousePosition.Y / 16 * 16 * zoom, 256 * zoom, 224 * zoom);
            Pen insideBorder = new Pen(Color.LightGray, 16);
            Pen edgeBorder = new Pen(Color.Black, 2);
            g.DrawRectangle(insideBorder, r.X - 8, r.Y - 8, r.Width + 16, r.Height + 16);
            g.DrawRectangle(edgeBorder, r.X - 16, r.Y - 16, r.Width + 32, r.Height + 32);
            g.DrawRectangle(edgeBorder, r);
        }
        private Bitmap HightlightedTile(int index)
        {
            int[] pixels = solidity.GetTilePixels(Model.SolidTiles[index]);
            for (int y = 768 - Model.SolidTiles[index].TotalHeight; y < 784; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    if (pixels[y * 32 + x] == 0) continue;
                    Color color = Color.FromArgb(pixels[y * 32 + x]);
                    int r = color.R;
                    int n = 255;
                    int b = 192;
                    if (index == 0)
                        pixels[y * 32 + x] = Color.FromArgb(96, 0, 0, 0).ToArgb();
                    else
                        pixels[y * 32 + x] = Color.FromArgb(255, r, n, b).ToArgb();
                }
            }
            return new Bitmap(Do.PixelsToImage(pixels, 32, 784));
        }
        private void DrawHoverBox(Graphics g)
        {
            int mouseOverSolidTileNum = 0;
            if (state.SolidMods && solidMods.Mods.Count != 0)
                mouseOverSolidTileNum = Bits.GetShort(solidMods.Mod_.Tilemap_Bytes, mouseOverSolidTile * 2);
            if (state.SolidityLayer && mouseOverSolidTileNum == 0)  // if mod map empty, check if solidity map empty
                mouseOverSolidTileNum = Bits.GetShort(solidityMap.Tilemap_Bytes, mouseOverSolidTile * 2);
            if ((state.SolidityLayer || state.SolidMods) && mouseOverSolidTileNum != 0)
            {
                Bitmap image = HightlightedTile(mouseOverSolidTileNum);
                Point p = new Point(
                    solidity.TileCoords[mouseOverSolidTile].X * zoom,
                    solidity.TileCoords[mouseOverSolidTile].Y * zoom - 768);
                Rectangle rsrc = new Rectangle(0, 0, 32, 784);
                Rectangle rdst = new Rectangle(p.X, p.Y, zoom * 32, zoom * 784);
                g.DrawImage(image, rdst, rsrc, GraphicsUnit.Pixel);
            }
            else if (state.SolidityLayer || state.SolidMods || state.NPCs || state.Exits || state.Events || state.Overlaps)
            {
                Point p = new Point(
                    solidity.TileCoords[mouseOverSolidTile].X * zoom,
                    solidity.TileCoords[mouseOverSolidTile].Y * zoom);
                Point[] points = new Point[] { 
                    new Point(p.X + (15 * zoom), p.Y), 
                    new Point(p.X - (1 * zoom), p.Y + (8 * zoom)), 
                    new Point(p.X + (16 * zoom), p.Y + (8 * zoom)), 
                    new Point(p.X + (16 * zoom), p.Y)
                };
                g.FillPolygon(new SolidBrush(Color.FromArgb(96, 0, 0, 0)), points, System.Drawing.Drawing2D.FillMode.Winding);
                points = new Point[] { 
                    new Point(p.X + (17 * zoom), p.Y), 
                    new Point(p.X + (33 * zoom), p.Y + (8 * zoom)), 
                    new Point(p.X + (16 * zoom), p.Y + (8 * zoom)), 
                    new Point(p.X + (16 * zoom), p.Y)
                };
                g.FillPolygon(new SolidBrush(Color.FromArgb(96, 0, 0, 0)), points, System.Drawing.Drawing2D.FillMode.Winding);
                points = new Point[] { 
                    new Point(p.X + (15 * zoom), p.Y + (16 * zoom)), 
                    new Point(p.X - (1 * zoom), p.Y + (8 * zoom)), 
                    new Point(p.X + (16 * zoom), p.Y + (8 * zoom)), 
                    new Point(p.X + (16 * zoom), p.Y + (16 * zoom))
                };
                g.FillPolygon(new SolidBrush(Color.FromArgb(96, 0, 0, 0)), points, System.Drawing.Drawing2D.FillMode.Winding);
                points = new Point[] { 
                    new Point(p.X + (17 * zoom), p.Y + (16 * zoom)), 
                    new Point(p.X + (33 * zoom), p.Y + (8 * zoom)), 
                    new Point(p.X + (16 * zoom), p.Y + (8 * zoom)), 
                    new Point(p.X + (16 * zoom), p.Y + (16 * zoom))
                };
                g.FillPolygon(new SolidBrush(Color.FromArgb(96, 0, 0, 0)), points, System.Drawing.Drawing2D.FillMode.Winding);
            }
            else
            {
                Rectangle r = new Rectangle(mousePosition.X / 16 * 16 * zoom, mousePosition.Y / 16 * 16 * zoom, 16 * zoom, 16 * zoom);
                g.FillRectangle(new SolidBrush(Color.FromArgb(96, 0, 0, 0)), r);
            }
        }
        private void DrawTemplate(Graphics g, int x, int y)
        {
            if (template == null)
            {
                MessageBox.Show("Must select a template to paint to the level.", "LAZY SHELL");
                return;
            }
            Point tL = new Point(x / 16 * 16, y / 16 * 16);
            Point bR = new Point((x / 16 * 16) + template.Size.Width, (y / 16 * 16) + template.Size.Height);
            if (template.Even != (((tL.X / 16) % 2) == 0))
            {
                tL.X += 16;
                bR.X += 16;
            }
            int[][] tiles = new int[3][];
            tiles[0] = new int[template.Tilemaps[0].Length / 2];
            tiles[1] = new int[template.Tilemaps[1].Length / 2];
            tiles[2] = new int[template.Tilemaps[2].Length];
            for (int i = 0; i < tiles[0].Length; i++)
            {
                tiles[0][i] = Bits.GetShort(template.Tilemaps[0], i * 2);
                tiles[1][i] = Bits.GetShort(template.Tilemaps[1], i * 2);
                tiles[2][i] = template.Tilemaps[2][i];
            }
            commandStack.Push(new TileMapEditCommand(this.levels, tilemap, 0, tL, bR, tiles, true, true, true));
            commandStack_S.Push(new SolidityEditCommand(this.levels, this.solidityMap, tL, bR, template.Start, template.Soliditymap));
            solidityMap.Image = null;
            tilemap.RedrawTilemaps();
            tileMods.RedrawTilemaps();
            SetLevelImage();
        }
        private void Draw(Graphics g, int x, int y)
        {
            if (state.TileMods)
            {
                int x_ = x - (tileMods.X * 16);
                int y_ = y - (tileMods.Y * 16);
                if (!tileMods.WithinBounds(x / 16, y / 16) ||
                    overlay.SelectTS.Width / 16 + (x_ / 16) > tileMods.Width ||
                    overlay.SelectTS.Height / 16 + (y_ / 16) > tileMods.Height)
                    return;
                x -= (tileMods.X * 16);
                y -= (tileMods.Y * 16);
            }
            if (state.SolidMods && !solidMods.WithinBounds(mouseOverSolidTile * 2))
                return;
            Tilemap tilemap;
            if (state.TileMods)
                tilemap = levels.TileModsFieldTree.SelectedNode.Parent == null ? tileMods.TilemapA : tileMods.TilemapB;
            else
                tilemap = this.tilemap;
            if (!state.SolidityLayer && !state.SolidMods)
            {
                int layer = tilesetEditor.Layer;
                // cancel if no selection in the tileset is made
                if (overlay.SelectTS == null) return;
                // cancel if writing same tile over itself
                if (CompareTiles(x, y, layer)) return;
                // cancel if layer doesn't exist
                if (this.tileset.Tilesets_Tiles[layer] == null) return;
                p1Image = null;
                Point location = new Point(x, y);
                Point terminal = new Point(
                    x + overlay.SelectTS.Width,
                    y + overlay.SelectTS.Height);
                bool transparent = minecart == null || minecart.Index > 1;
                CommandStack commandStack = state.TileMods ? this.commandStack_TM : this.commandStack;
                commandStack.Push(
                    new TileMapEditCommand(
                        levels, tilemap, layer, location, terminal,
                        tilesetEditor.SelectedTiles.Copies, false, transparent, editAllLayers.Checked));
                if (state.TileMods)
                    tileMods.UpdateTilemaps();
                // draw the tile
                Point p = new Point(x / 16 * 16, y / 16 * 16);
                Bitmap image = Do.PixelsToImage(
                    tilemap.GetPixels(p, overlay.SelectTS.Size),
                    overlay.SelectTS.Width, overlay.SelectTS.Height);
                if (state.TileMods)
                {
                    p.X += tileMods.X * 16;
                    p.Y += tileMods.Y * 16;
                }
                p.X *= zoom;
                p.Y *= zoom;
                Rectangle rsrc = new Rectangle(0, 0, image.Width, image.Height);
                Rectangle rdst = new Rectangle(p.X, p.Y, (int)(image.Width * zoom), (int)(image.Height * zoom));
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(image, rdst, rsrc, GraphicsUnit.Pixel);
            }
            else if (state.SolidityLayer || state.SolidMods)
            {
                // cancel if physical tile editor not open
                if (levelsSolidTiles == null) return;
                // cancel if overwriting the same tile over itself
                Tilemap map = state.SolidMods ? (Tilemap)solidMods.Mod_ : solidityMap;
                if (map.GetTileNum(mouseOverSolidTile) == (ushort)levelsSolidTiles.Index)
                    return;
                Point initial = new Point(x, y);
                Point final = new Point(x + 1, y + 1);
                byte[] temp = new byte[0x20C2];
                Bits.SetShort(temp, mouseOverSolidTile * 2, (ushort)levelsSolidTiles.Index);
                CommandStack commandStack_S = state.SolidMods ? this.commandStack_SM : this.commandStack_S;
                commandStack_S.Push(new SolidityEditCommand(this.levels, map, initial, final, initial, temp));
                if (state.SolidMods)
                    solidMods.Mod_.CopyToTiles();
                solidity.RefreshTilemapImage(map, mouseOverSolidTile * 2);
                map.Image = null;
                p1SolidityImage = null;
                pictureBoxLevel.Invalidate();
                this.pushes.Push(1);
            }
        }
        private void Erase(Graphics g, int x, int y)
        {
            if (state.TileMods)
            {
                if (!tileMods.WithinBounds(x / 16, y / 16))
                    return;
                x -= (tileMods.X * 16);
                y -= (tileMods.Y * 16);
            }
            if (state.SolidMods && !solidMods.WithinBounds(mouseOverSolidTile * 2))
                return;
            Tilemap tilemap;
            if (state.TileMods)
                tilemap = levels.TileModsFieldTree.SelectedNode.Parent == null ? tileMods.TilemapA : tileMods.TilemapB;
            else
                tilemap = this.tilemap;
            if (!state.SolidityLayer && !state.SolidMods)
            {
                int layer = tilesetEditor.Layer;
                // cancel if overwriting the same tile over itself
                if (!editAllLayers.Checked && this.tileset.Tilesets_Tiles[layer] == null) return;
                if (!editAllLayers.Checked && tilemap.GetTileNum(layer, x, y) == erase) return;
                p1Image = null;
                bool transparent = minecart == null || minecart.Index > 1;
                CommandStack commandStack = state.TileMods ? this.commandStack_TM : this.commandStack;
                commandStack.Push(
                    new TileMapEditCommand(
                        this.levels, tilemap, layer, new Point(x, y), new Point(x + 16, y + 16),
                        new int[][] { new int[] { erase }, new int[] { erase }, new int[] { erase }, new int[] { erase } },
                        false, transparent, editAllLayers.Checked));
                if (state.TileMods)
                    tileMods.UpdateTilemaps();
                Point p = new Point(x / 16 * 16, y / 16 * 16);
                Bitmap image = Do.PixelsToImage(tilemap.GetPixels(p, new Size(16, 16)), 16, 16);
                if (state.TileMods)
                {
                    p.X += tileMods.X * 16;
                    p.Y += tileMods.Y * 16;
                }
                p.X *= zoom; p.Y *= zoom;
                Rectangle rsrc = new Rectangle(0, 0, 16, 16);
                Rectangle rdst = new Rectangle(p.X, p.Y, (int)(16 * zoom), (int)(16 * zoom));
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(image, rdst, rsrc, GraphicsUnit.Pixel);
            }
            else if (state.SolidityLayer || state.SolidMods)
            {
                // cancel if overwriting the same tile over itself
                if (solidityMap.GetTileNum(mouseOverSolidTile) == 0)
                    return;
                Point tL = new Point(x, y);
                Point bR = new Point(x + 1, y + 1);
                Tilemap map = state.SolidMods ? (Tilemap)solidMods.Mod_ : solidityMap;
                CommandStack commandStack_S = state.SolidMods ? this.commandStack_SM : this.commandStack_S;
                commandStack_S.Push(new SolidityEditCommand(this.levels, map, tL, bR, tL, new byte[0x20C2]));
                if (state.SolidMods)
                    solidMods.Mod_.CopyToTiles();
                solidity.RefreshTilemapImage(map, mouseOverSolidTile * 2);
                map.Image = null;
                p1SolidityImage = null;
                pictureBoxLevel.Invalidate();
                this.pushes.Push(1);
            }
        }
        private void SelectColor(int x, int y)
        {
            Tilemap tilemap = this.tilemap;
            int layer = tilemap.GetPixelLayer(x, y);
            int tileNum = (y / 16) * (width / 16) + (x / 16);
            int placement = ((x % 16) / 8) + (((y % 16) / 8) * 2);
            Tile tile = this.tileset.Tilesets_Tiles[layer][tilemap.GetTileNum(layer, x, y)];
            Subtile subtile = tile.Subtiles[placement];
            paletteEditor.CurrentColor =
                (subtile.Palette * 16) + subtile.Colors[((y % 16) % 8) * 8 + ((x % 16) % 8)];
            paletteEditor.Show();
        }
        private void Fill(Graphics g, int x, int y)
        {
            Tilemap tilemap = this.tilemap;
            if (!state.SolidityLayer)
            {
                int layer = tilesetEditor.Layer;
                // cancel if no selection in the tileset is made
                if (overlay.SelectTS == null) return;
                // cancel if writing same tile over itself
                if (CompareTiles(x, y, layer)) return;
                // cancel if layer doesn't exist
                if (this.tileset.Tilesets_Tiles[layer] == null) return;
                p1Image = null;
                // store changes
                int[][] changes = new int[3][];
                if (tilemap.Tilemaps_Bytes[0] != null) changes[0] = new int[(width / 16) * (height / 16)];
                if (tilemap.Tilemaps_Bytes[1] != null) changes[1] = new int[(width / 16) * (height / 16)];
                if (tilemap.Tilemaps_Bytes[2] != null) changes[2] = new int[(width / 16) * (height / 16)];
                for (int l = 0; l < 3; l++)
                {
                    Tile[] tiles = tilemap.Tilemaps_Tiles[l];
                    if (changes[l] == null) continue;
                    if (tiles == null) continue;
                    for (int i = 0; i < changes[l].Length && i < tiles.Length; i++)
                    {
                        if (tiles[i] == null) continue;
                        changes[l][i] = tiles[i].TileIndex;
                    }
                }
                // fill up tiles
                Point location = new Point(0, 0);
                Point terminal = new Point(width, height);
                int[] fillTile = tilesetEditor.SelectedTiles.Copies[layer];
                int tile = tilemap.GetTileNum(layer, x, y);
                int vwidth = overlay.SelectTS.Width / 16;
                int vheight = overlay.SelectTS.Height / 16;

                if ((Control.ModifierKeys & Keys.Control) == 0)
                    Do.Fill(changes, layer, editAllLayers.Checked, tile, fillTile, x / 16, y / 16, width / 16, height / 16, vwidth, vheight, "");
                else
                    // non-contiguous fill
                    for (int d = 0; d < height / 16; d += vheight)
                    {
                        for (int c = 0; c < width / 16; c += vwidth)
                        {
                            for (int b = 0; b < vheight; b++)
                            {
                                if (changes[layer][(d + b) * (width / 16) + c] != tile)
                                    break;
                                for (int a = 0; a < vwidth; a++)
                                {
                                    if (changes[layer][(d + b) * (width / 16) + c + a] != tile)
                                        break;
                                    changes[layer][(d + b) * (width / 16) + c + a] = fillTile[b * vwidth + a];
                                }
                            }
                        }
                    }
                bool transparent = minecart == null || minecart.Index > 1;
                commandStack.Push(
                    new TileMapEditCommand(levels, tilemap, layer, location, terminal, changes, false, transparent, false));
            }
            else
            {
                if (solidityMap.GetTileNum(mouseOverSolidTile) == (ushort)levelsSolidTiles.Index)
                    return;
                ushort tile = (ushort)solidityMap.GetTileNum(mouseOverSolidTile);
                ushort fillTile = (ushort)levelsSolidTiles.Index;
                byte[] changes = Bits.Copy(solidityMap.Tilemap_Bytes);
                if ((Control.ModifierKeys & Keys.Control) == 0)
                    Do.Fill(changes, tile, fillTile, (mousePosition.X + 16) / 32 * 32, (mousePosition.Y + 8) / 16 * 16, width, height, "");
                else
                    for (int i = 0; i < changes.Length; i += 2)
                        if (Bits.GetShort(changes, i) == tile)
                            Bits.SetShort(changes, i, fillTile);
                int index = 0;
                int pushes = 0;
                for (int n = 0; n < 128; n++)
                {
                    for (int m = 0; m < 32; m++)
                    {
                        index = n * 32 + m;
                        Point tL = new Point(
                            solidity.TileCoords[index].X + 16,
                            solidity.TileCoords[index].Y + 8);
                        Point bR = new Point(
                            solidity.TileCoords[index].X + 17,
                            solidity.TileCoords[index].Y + 9);
                        if (state.SolidMods)
                            commandStack_S.Push(new SolidityEditCommand(levels, solidMods.Mod_, tL, bR, tL, changes));
                        else
                            commandStack_S.Push(new SolidityEditCommand(levels, solidityMap, tL, bR, tL, changes));
                        pushes++;
                    }
                }
                this.pushes.Push(pushes);
                solidityMap.Image = null;
                p1SolidityImage = null;
                pictureBoxLevel.Invalidate();
            }
        }
        private void Undo()
        {
            if (!state.SolidityLayer && !state.SolidMods)
            {
                if (!state.TileMods)
                    commandStack.UndoCommand();
                else
                    commandStack_TM.UndoCommand();
                p1Image = null;
                SetLevelImage();
                if (level != null)
                    tileMods.ClearImages();
            }
            else
            {
                if (state.SolidMods)
                {
                    commandStack_SM.UndoCommand();
                    solidMods.Mod_.CopyToTiles();
                    solidMods.Mod_.Pixels = Solidity.Instance.GetTilemapPixels(solidMods.Mod_);
                    solidMods.Mod_.Image = null;
                }
                else if (this.pushes.Count > 0)
                {
                    int pushes = this.pushes.Pop();
                    int pops = 0;
                    for (; pushes > 0; pushes--, pops++)
                        commandStack_S.UndoCommand();
                    this.pops.Push(pops);
                    solidityMap.Image = null;
                    p1SolidityImage = null;
                }
                pictureBoxLevel.Invalidate();
            }
        }
        private void Redo()
        {
            if (!state.SolidityLayer && !state.SolidMods)
            {
                if (!state.TileMods)
                    commandStack.RedoCommand();
                else
                    commandStack_TM.RedoCommand();
                p1Image = null;
                SetLevelImage();
                if (level != null)
                    tileMods.ClearImages();
            }
            else
            {
                if (state.SolidMods)
                {
                    commandStack_SM.RedoCommand();
                    solidMods.Mod_.CopyToTiles();
                    solidMods.Mod_.Pixels = Solidity.Instance.GetTilemapPixels(solidMods.Mod_);
                    solidMods.Mod_.Image = null;
                }
                else if (this.pops.Count > 0)
                {
                    int pops = this.pops.Pop();
                    int pushes = 0;
                    for (; pops > 0; pops--, pushes++)
                        commandStack_S.RedoCommand();
                    this.pushes.Push(pushes);
                    solidityMap.Image = null;
                    p1SolidityImage = null;
                }
                pictureBoxLevel.Invalidate();
            }
        }
        private void Cut()
        {
            if (overlay.Select == null || overlay.Select.Size == new Size(0, 0)) return;
            if (state.SolidityLayer || state.SolidMods) return;
            Copy();
            Delete();
        }
        private void Copy()
        {
            if (overlay.Select == null || overlay.Select.Size == new Size(0, 0)) return;
            if (state.SolidityLayer || state.SolidMods) return;
            if (draggedTiles != null)
            {
                this.copiedTiles = draggedTiles;
                return;
            }
            int layer = tilesetEditor.Layer;
            Tilemap tilemap;
            Point location = overlay.Select.Location;
            if (state.TileMods)
            {
                if (!tileMods.WithinBounds(location.X / 16, location.Y / 16))
                    return;
                tilemap = levels.TileModsFieldTree.SelectedNode.Parent == null ? tileMods.TilemapA : tileMods.TilemapB;
                location.X -= tileMods.X * 16;
                location.Y -= tileMods.Y * 16;
            }
            else
                tilemap = this.tilemap;
            if (editAllLayers.Checked)
                selection = new Bitmap(
                    Do.PixelsToImage(
                    tilemap.GetPixels(location, overlay.Select.Size),
                    overlay.Select.Width, overlay.Select.Height));
            else
                selection = new Bitmap(
                    Do.PixelsToImage(
                    tilemap.GetPixels(layer, location, overlay.Select.Size),
                    overlay.Select.Width, overlay.Select.Height));

            int[][] copiedTiles = new int[3][];
            this.copiedTiles = new CopyBuffer(overlay.Select.Width, overlay.Select.Height);
            for (int l = 0; l < 3; l++)
            {
                copiedTiles[l] = new int[(overlay.Select.Width / 16) * (overlay.Select.Height / 16)];
                for (int y = 0; y < overlay.Select.Height / 16; y++)
                {
                    for (int x = 0; x < overlay.Select.Width / 16; x++)
                    {
                        int tileX = location.X + (x * 16);
                        int tileY = location.Y + (y * 16);
                        copiedTiles[l][y * (overlay.Select.Width / 16) + x] = tilemap.GetTileNum(l, tileX, tileY);
                    }
                }
            }
            this.copiedTiles.Copies = copiedTiles;
        }
        /// <summary>
        /// Start dragging a current selection.
        /// </summary>
        private void Drag()
        {
            if (overlay.Select == null || overlay.Select.Size == new Size(0, 0)) return;
            if (!state.SolidityLayer && !state.SolidMods)
            {
                int layer = tilesetEditor.Layer;
                Tilemap tilemap;
                if (state.TileMods)
                    tilemap = levels.TileModsFieldTree.SelectedNode.Parent == null ? tileMods.TilemapA : tileMods.TilemapB;
                else
                    tilemap = this.tilemap;
                if (editAllLayers.Checked)
                    selection = new Bitmap(
                        Do.PixelsToImage(
                        tilemap.GetPixels(overlay.Select.Location, overlay.Select.Size),
                        overlay.Select.Width, overlay.Select.Height));
                else
                    selection = new Bitmap(
                        Do.PixelsToImage(
                        tilemap.GetPixels(layer, overlay.Select.Location, overlay.Select.Size),
                        overlay.Select.Width, overlay.Select.Height));

                int[][] copiedTiles = new int[3][];
                this.draggedTiles = new CopyBuffer(overlay.Select.Width, overlay.Select.Height);
                for (int l = 0; l < 3; l++)
                {
                    copiedTiles[l] = new int[(overlay.Select.Width / 16) * (overlay.Select.Height / 16)];
                    for (int y = 0; y < overlay.Select.Height / 16; y++)
                    {
                        for (int x = 0; x < overlay.Select.Width / 16; x++)
                        {
                            int tileX = overlay.Select.X + (x * 16);
                            int tileY = overlay.Select.Y + (y * 16);
                            copiedTiles[l][y * (overlay.Select.Width / 16) + x] = tilemap.GetTileNum(l, tileX, tileY);
                        }
                    }
                }
                this.draggedTiles.Copies = copiedTiles;
            }
            Delete();
        }
        private void Paste(Point location, CopyBuffer buffer)
        {
            if (state.SolidityLayer || state.SolidMods) return;
            if (buffer == null) return;
            if (!buttonEditSelect.Checked)
                buttonEditSelect.PerformClick();
            state.Move = true;
            // now dragging a new selection
            draggedTiles = buffer;
            overlay.Select = new Overlay.Selection(16, location, buffer.Size);
            pictureBoxLevel.Invalidate();
            pasteFinal = false;
            //levels.AlertLabel();
        }
        /// <summary>
        /// "Cements" either a dragged selection or a newly pasted selection.
        /// </summary>
        /// <param name="buffer">The dragged selection or the newly pasted selection.</param>
        private void PasteFinal(CopyBuffer buffer)
        {
            if (state.SolidityLayer || state.SolidMods) return;
            if (buffer == null) return;
            if (overlay.Select == null) return;
            Point location = new Point();
            location.X = overlay.Select.X / 16 * 16;
            location.Y = overlay.Select.Y / 16 * 16;
            int layer = tilesetEditor.Layer;
            Tilemap tilemap;
            if (state.TileMods)
            {
                if (!tileMods.WithinBounds(location.X / 16, location.Y / 16))
                    return;
                location.X -= tileMods.X * 16;
                location.Y -= tileMods.Y * 16;
                tilemap = levels.TileModsFieldTree.SelectedNode.Parent == null ? tileMods.TilemapA : tileMods.TilemapB;
            }
            else
                tilemap = this.tilemap;
            Point terminal = new Point(location.X + buffer.Width, location.Y + buffer.Height);
            bool transparent = minecart == null || minecart.Index > 1;
            CommandStack commandStack = state.TileMods ? this.commandStack_TM : this.commandStack;
            commandStack.Push(
                new TileMapEditCommand(levels, tilemap, layer, location, terminal, buffer.Copies, true, transparent, editAllLayers.Checked));
            p1Image = null;
            SetLevelImage();
            if (level != null)
                tileMods.ClearImages();
            pasteFinal = true;
        }
        /// <summary>
        /// Cements pasted tiles and clears the selection
        /// </summary>
        private void PasteClear()
        {
            if (copiedTiles != null && !pasteFinal)
                PasteFinal(copiedTiles);
            if (draggedTiles != null)
            {
                PasteFinal(draggedTiles);
                draggedTiles = null;
            }
            state.Move = false;
            overlay.Select = null;
        }
        private void Delete()
        {
            if (overlay.Select == null) return;
            if (overlay.Select.Size == new Size(0, 0)) return;
            if (!state.SolidityLayer && !state.SolidMods)
            {
                int layer = tilesetEditor.Layer;
                if (this.tileset.Tilesets_Tiles[layer] == null || overlay.Select.Size == new Size(0, 0)) return;
                if (overlay.Select == null) return;
                Point location = overlay.Select.Location;
                Point terminal = overlay.Select.Terminal;
                int[][] changes = new int[][]{
                    new int[overlay.Select.Width * overlay.Select.Height],
                    new int[overlay.Select.Width * overlay.Select.Height],
                    new int[overlay.Select.Width * overlay.Select.Height],
                    new int[overlay.Select.Width * overlay.Select.Height]};
                if (erase != 0)
                    for (int i = 0; i < 4; i++)
                        Bits.Fill(changes[i], erase);
                // Verify layer before creating command
                Tilemap tilemap;
                if (state.TileMods)
                {
                    tilemap = levels.TileModsFieldTree.SelectedNode.Parent == null ? tileMods.TilemapA : tileMods.TilemapB;
                    location.X -= (tileMods.X * 16);
                    location.Y -= (tileMods.Y * 16);
                }
                else
                    tilemap = this.tilemap;
                bool transparent = minecart == null || minecart.Index > 1;
                CommandStack commandStack = state.TileMods ? this.commandStack_TM : this.commandStack;
                commandStack.Push(
                    new TileMapEditCommand(
                        levels, tilemap, layer, location, terminal,
                        changes, false, transparent, editAllLayers.Checked));
                p1Image = null;
                SetLevelImage();
                if (level != null)
                    tileMods.ClearImages();
            }
            else
            {
                int index = 0;
                int pushes = 0;
                Tilemap map = state.SolidMods ? (Tilemap)solidMods.Mod_ : solidityMap;
                for (int y = overlay.Select.Y; y < overlay.Select.Y + overlay.Select.Height; y++)
                {
                    for (int x = overlay.Select.X; x < overlay.Select.X + overlay.Select.Width; x++)
                    {
                        if (index == solidity.PixelTiles[y * width + x])
                            continue;
                        index = solidity.PixelTiles[y * width + x];
                        if (map.GetTileNum(index) == 0)
                            continue;
                        Point tL = new Point(
                            solidity.TileCoords[index].X + 16,
                            solidity.TileCoords[index].Y + 8);
                        Point bR = new Point(
                            solidity.TileCoords[index].X + 17,
                            solidity.TileCoords[index].Y + 9);
                        CommandStack commandStack_S = state.SolidMods ? this.commandStack_SM : this.commandStack_S;
                        commandStack_S.Push(new SolidityEditCommand(levels, map, tL, bR, tL, new byte[0x20C2]));
                        if (!state.SolidMods)
                            pushes++;
                    }
                }
                if (!state.SolidMods)
                {
                    this.pushes.Push(pushes);
                    p1SolidityImage = null;
                }
                else
                    map.Pixels = Solidity.Instance.GetTilemapPixels(map);
                map.Image = null;
                pictureBoxLevel.Invalidate();
            }
        }
        //
        #endregion
        #region Event Handlers
        // main
        private void LevelsTilemap_KeyDown(object sender, KeyEventArgs e)
        {
        }
        private void pictureBoxLevel_Paint(object sender, PaintEventArgs e)
        {
            RectangleF clone = e.ClipRectangle;
            SizeF remainder = new SizeF((int)(clone.Width % zoom), (int)(clone.Height % zoom));
            clone.Location = new PointF((int)(clone.X / zoom), (int)(clone.Y / zoom));
            clone.Size = new SizeF((int)(clone.Width / zoom), (int)(clone.Height / zoom));
            clone.Width += (int)(remainder.Width * zoom) + 1;
            clone.Height += (int)(remainder.Height * zoom) + 1;
            RectangleF source, dest;
            float[][] matrixItems ={ 
               new float[] {1, 0, 0, 0, 0},
               new float[] {0, 1, 0, 0, 0},
               new float[] {0, 0, 1, 0, 0},
               new float[] {0, 0, 0, (float)overlayOpacity.Value / 100, 0}, 
               new float[] {0, 0, 0, 0, 1}};
            ColorMatrix cm = new ColorMatrix(matrixItems);
            ImageAttributes ia = new ImageAttributes();
            if (overlayOpacity.Value < 100)
                ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
            Rectangle rdst = new Rectangle(0, 0, zoom * width, zoom * height);
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            if (tilemapImage != null)
            {
                clone.Width = Math.Min(tilemapImage.Width, clone.X + clone.Width) - clone.X;
                clone.Height = Math.Min(tilemapImage.Height, clone.Y + clone.Height) - clone.Y;

                source = clone; source.Location = new PointF(0, 0);
                dest = new RectangleF((int)(clone.X * zoom), (int)(clone.Y * zoom), (int)(clone.Width * zoom), (int)(clone.Height * zoom));
                //if (e.ClipRectangle.Size != new Size(16 * zoom, 16 * zoom))
                e.Graphics.DrawImage(tilemapImage.Clone(clone, PixelFormat.DontCare), dest, source, GraphicsUnit.Pixel);
            }
            if (state.TileMods)
                overlay.DrawLevelTileMods(tileMods, e.Graphics, ia, zoom);
            if (state.Move && selection != null)
            {
                Rectangle rsrc = new Rectangle(0, 0, overlay.Select.Width, overlay.Select.Height);
                rdst = new Rectangle(
                    overlay.Select.X * zoom, overlay.Select.Y * zoom,
                    rsrc.Width * zoom, rsrc.Height * zoom);
                e.Graphics.DrawImage(new Bitmap(selection), rdst, rsrc, GraphicsUnit.Pixel);
                Do.DrawString(e.Graphics, new Point(rdst.X, rdst.Y + rdst.Height),
                    "click/drag", Color.White, Color.Black, new Font("Tahoma", 6.75F, FontStyle.Bold));
            }
            if (state.Priority1 && !state.SolidityLayer)
            {
                cm.Matrix33 = 0.50F;
                ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                if (p1Image == null)
                    p1Image = new Bitmap(Do.PixelsToImage(tilemap.GetPriority1Pixels(), width, height));
                e.Graphics.DrawImage(p1Image, rdst, 0, 0, width, height, GraphicsUnit.Pixel, ia);
            }
            if (state.SolidityLayer)
            {
                if (overlayOpacity.Value < 100)
                    e.Graphics.DrawImage(solidityMap.Image, rdst, 0, 0, width, height, GraphicsUnit.Pixel, ia);
                else
                    e.Graphics.DrawImage(solidityMap.Image, rdst, 0, 0, width, height, GraphicsUnit.Pixel);
                if (state.Priority1)
                {
                    if (p1SolidityImage == null)
                        p1SolidityImage = new Bitmap(Do.PixelsToImage(solidity.GetPriority1Pixels(solidityMap), 1024, 1024));
                    e.Graphics.DrawImage(p1SolidityImage, rdst, 0, 0, width, height, GraphicsUnit.Pixel, ia);
                }
                if (selsolidt != null)
                {
                    Rectangle rsrc = new Rectangle(0, 0, selsolidt.Width, selsolidt.Height);
                    rdst = new Rectangle(
                        selsolidt_location.X * zoom, selsolidt_location.Y * zoom,
                        rsrc.Width * zoom, rsrc.Height * zoom);
                    e.Graphics.DrawImage(selsolidt, rdst, rsrc, GraphicsUnit.Pixel);
                }
            }
            if (state.SolidMods)
            {
                overlay.DrawLevelSolidMods(solidMods, solidTiles, e.Graphics, rdst, ia, zoom);
                overlay.DrawLevelSolidMods(solidMods, e.Graphics, zoom);
            }
            if (state.Exits)
            {
                //if (overlayOpacity.Value < 100)
                //    overlay.DrawLevelExits(exits, e.Graphics, ia, zoom);
                //else
                overlay.DrawLevelExits(exits, e.Graphics, zoom);
                if (tags.Checked)
                    overlay.DrawLevelExitTags(exits, e.Graphics, zoom);
            }
            if (state.Events)
            {
                //if (overlayOpacity.Value < 100)
                //    overlay.DrawLevelEvents(events, e.Graphics, ia, zoom);
                //else
                overlay.DrawLevelEvents(events, e.Graphics, zoom);
                if (tags.Checked)
                    overlay.DrawLevelEventTags(events, e.Graphics, zoom);
            }
            if (state.NPCs)
            {
                if (overlay.NPCImages == null)
                    overlay.DrawLevelNPCs(npcs, Model.NPCProperties);
                //if (overlayOpacity.Value < 100)
                //    overlay.DrawLevelNPCs(npcs, e.Graphics, ia, zoom);
                //else
                overlay.DrawLevelNPCs(npcs, e.Graphics, zoom);
                if (tags.Checked)
                    overlay.DrawLevelNPCTags(npcs, e.Graphics, zoom);
            }
            if (state.Overlaps)
            {
                //if (overlayOpacity.Value < 100)
                //    overlay.DrawLevelOverlaps(overlaps, Model.OverlapTileset, e.Graphics, ia, zoom);
                //else
                overlay.DrawLevelOverlaps(overlaps, Model.OverlapTileset, e.Graphics, zoom);
            }
            if (state.Mushrooms)
            {
                if (minecart.Index == 0)
                    overlay.DrawLevelMushrooms(minecart.MinecartData, minecart.MinecartData.M7ObjectsA, e.Graphics, zoom);
                else if (minecart.Index == 1)
                    overlay.DrawLevelMushrooms(minecart.MinecartData, minecart.MinecartData.M7ObjectsB, e.Graphics, zoom);
            }
            if (state.Rails && minecart.Index < 2)
            {
                overlay.DrawRailProperties(tilemap.Tilemap_Bytes, 64, 64, e.Graphics, zoom);
            }
            if (!state.Dropper && mouseEnter)
                DrawHoverBox(e.Graphics);
            if (state.CartesianGrid)
                overlay.DrawCartesianGrid(e.Graphics, Color.Gray, pictureBoxLevel.Size, new Size(16, 16), zoom, true);
            if (state.IsometricGrid)
                overlay.DrawIsometricGrid(e.Graphics, Color.Gray, pictureBoxLevel.Size, new Size(16, 16), zoom);
            if (state.Mask)
                overlay.DrawLevelMask(e.Graphics, new Point(layer.MaskHighX, layer.MaskHighY), new Point(layer.MaskLowX, layer.MaskLowY), zoom);
            if (state.ShowBoundaries && mouseEnter)
                overlay.DrawBoundaries(e.Graphics, mousePosition, zoom);
            if (state.Select&&overlay.Select != null)
            {
                if (state.CartesianGrid)
                    overlay.DrawSelectionBox(e.Graphics, overlay.Select.Terminal, overlay.Select.Location, zoom, Color.Yellow);
                else
                    overlay.DrawSelectionBox(e.Graphics, overlay.Select.Terminal, overlay.Select.Location, zoom);
            }
        }
        private void pictureBoxLevel_MouseDown(object sender, MouseEventArgs e)
        {
            // in case the tileset selection was dragged
            if (tilesetEditor.DraggedTiles != null)
                tilesetEditor.PasteFinal(tilesetEditor.DraggedTiles);
            // set a floor and ceiling for the coordinates
            int x = Math.Max(0, Math.Min(e.X / zoom, width));
            int y = Math.Max(0, Math.Min(e.Y / zoom, height));
            mouseDownObject = null;
            #region Zooming
            autoScrollPos.X = Math.Abs(panelLevelPicture.AutoScrollPosition.X);
            autoScrollPos.Y = Math.Abs(panelLevelPicture.AutoScrollPosition.Y);
            if ((buttonZoomIn.Checked && e.Button == MouseButtons.Left) || (buttonZoomOut.Checked && e.Button == MouseButtons.Right))
            {
                if (zoom < 8)
                {
                    zoom *= 2;
                    autoScrollPos = new Point(Math.Abs(pictureBoxLevel.Left), Math.Abs(pictureBoxLevel.Top));
                    autoScrollPos.X += e.X;
                    autoScrollPos.Y += e.Y;
                    pictureBoxLevel.Width = width * zoom;
                    pictureBoxLevel.Height = height * zoom;
                    panelLevelPicture.AutoScrollPosition = autoScrollPos;
                    panelLevelPicture.VerticalScroll.SmallChange *= 2;
                    panelLevelPicture.HorizontalScroll.SmallChange *= 2;
                    panelLevelPicture.VerticalScroll.LargeChange *= 2;
                    panelLevelPicture.HorizontalScroll.LargeChange *= 2;
                    pictureBoxLevel.Invalidate();
                    pictureBoxLevel.Focus();
                    zoomPanel.Zoom = zoom * 4;
                    return;
                }
                return;
            }
            else if ((buttonZoomOut.Checked && e.Button == MouseButtons.Left) || (buttonZoomIn.Checked && e.Button == MouseButtons.Right))
            {
                if (zoom > 1)
                {
                    zoom /= 2;
                    autoScrollPos = new Point(Math.Abs(pictureBoxLevel.Left), Math.Abs(pictureBoxLevel.Top));
                    autoScrollPos.X -= e.X / 2;
                    autoScrollPos.Y -= e.Y / 2;
                    pictureBoxLevel.Width = width * zoom;
                    pictureBoxLevel.Height = height * zoom;
                    panelLevelPicture.AutoScrollPosition = autoScrollPos;
                    panelLevelPicture.VerticalScroll.SmallChange /= 2;
                    panelLevelPicture.HorizontalScroll.SmallChange /= 2;
                    panelLevelPicture.VerticalScroll.LargeChange /= 2;
                    panelLevelPicture.HorizontalScroll.LargeChange /= 2;
                    pictureBoxLevel.Invalidate();
                    pictureBoxLevel.Focus();
                    zoomPanel.Zoom = zoom * 4;
                    return;
                }
                return;
            }
            #endregion
            if (e.Button == MouseButtons.Right) return;
            #region Drawing, Erasing, Selecting
            // if moving an object and outside of it, paste it
            if (state.Move && mouseOverObject != "selection")
            {
                // if copied tiles were pasted and not dragging a non-copied selection
                if (copiedTiles != null && draggedTiles == null)
                    PasteFinal(copiedTiles);
                if (draggedTiles != null)
                {
                    PasteFinal(draggedTiles);
                    draggedTiles = null;
                }
                state.Move = false;
            }
            if (state.Select)
            {
                //panelLevelPicture.Focus();
                // if we're not inside a current selection to move it, create a new selection
                if (mouseOverObject != "selection")
                    overlay.Select = new Overlay.Selection(16, x / 16 * 16, y / 16 * 16, 16, 16);
                // otherwise, start dragging current selection
                else if (mouseOverObject == "selection")
                {
                    mouseDownObject = "selection";
                    mouseDownPosition = overlay.Select.MousePosition(x, y);
                    if (!state.Move)    // only do this if the current selection has not been initially moved
                    {
                        state.Move = true;
                        Drag();
                    }
                }
            }
            if (e.Button == MouseButtons.Left)
            {
                if (state.Dropper)
                {
                    SelectColor(x, y);
                    return;
                }
                if (state.Template)
                {
                    DrawTemplate(pictureBoxLevel.CreateGraphics(), x, y);
                    panelLevelPicture.AutoScrollPosition = autoScrollPos;
                    return;
                }
                if (state.Draw)
                {
                    Draw(pictureBoxLevel.CreateGraphics(), x, y);
                    panelLevelPicture.AutoScrollPosition = autoScrollPos;
                    return;
                }
                if (state.Erase)
                {
                    Erase(pictureBoxLevel.CreateGraphics(), x, y);
                    panelLevelPicture.AutoScrollPosition = autoScrollPos;
                    return;
                }
                if (state.Fill)
                {
                    Fill(pictureBoxLevel.CreateGraphics(), x, y);
                    panelLevelPicture.AutoScrollPosition = autoScrollPos;
                    return;
                }
            }
            #endregion
            #region Object Selection
            if (!state.Template && !state.Draw && !state.Select && !state.Erase && e.Button == MouseButtons.Left)
            {
                if (state.Mask && mouseOverObject != null && mouseOverObject.StartsWith("mask"))
                {
                    levels.TabControl.SelectedIndex = 1;
                    mouseDownObject = mouseOverObject;
                }
                if (state.Exits && mouseOverObject == "exit")
                {
                    levels.TabControl.SelectedIndex = 3;
                    mouseDownObject = "exit";
                    mouseDownExitField = mouseOverExitField;
                    exits.CurrentExit = mouseDownExitField;
                    exits.SelectedExit = mouseDownExitField;
                    levels.ExitsFieldTree.SelectedNode = levels.ExitsFieldTree.Nodes[exits.CurrentExit];
                }
                if (state.Events && mouseOverObject == "event" && mouseDownObject == null)
                {
                    levels.TabControl.SelectedIndex = 3;
                    mouseDownObject = "event";
                    mouseDownEventField = mouseOverEventField;
                    events.CurrentEvent = mouseDownEventField;
                    events.SelectedEvent = mouseDownEventField;
                    levels.EventsFieldTree.SelectedNode = levels.EventsFieldTree.Nodes[events.CurrentEvent];
                }
                if (state.NPCs && mouseOverObject == "npc" && mouseDownObject == null)
                {
                    levels.TabControl.SelectedIndex = 2;
                    mouseDownObject = "npc";
                    mouseDownNPC = mouseOverNPC;
                    npcs.CurrentNPC = mouseDownNPC;
                    npcs.SelectedNPC = mouseDownNPC;
                    npcs.IsInstanceSelected = false;
                    levels.NpcObjectTree.SelectedNode = levels.NpcObjectTree.Nodes[npcs.CurrentNPC];
                }
                if (state.NPCs && mouseOverObject == "npc instance" && mouseDownObject == null)
                {
                    levels.TabControl.SelectedIndex = 2;
                    mouseDownObject = "npc instance";
                    mouseDownNPC = mouseOverNPC;
                    mouseDownNPCInstance = mouseOverNPCInstance;
                    npcs.CurrentNPC = mouseDownNPC;
                    npcs.SelectedNPC = mouseDownNPC;
                    npcs.CurrentInstance = mouseDownNPCInstance;
                    npcs.SelectedInstance = mouseDownNPCInstance;
                    npcs.IsInstanceSelected = true;
                    levels.NpcObjectTree.SelectedNode = levels.NpcObjectTree.Nodes[npcs.CurrentNPC].Nodes[npcs.CurrentInstance];
                }
                if (state.Overlaps && mouseOverObject == "overlap" && mouseDownObject == null)
                {
                    levels.TabControl.SelectedIndex = 4;
                    mouseDownObject = "overlap";
                    mouseDownOverlap = mouseOverOverlap;
                    overlaps.CurrentOverlap = mouseDownOverlap;
                    overlaps.SelectedOverlap = mouseDownOverlap;
                    levels.OverlapFieldTree.SelectedNode = levels.OverlapFieldTree.Nodes[overlaps.CurrentOverlap];
                }
                if (state.TileMods && mouseOverObject == "tilemod" && mouseDownObject == null)
                {
                    levels.TabControl.SelectedIndex = 5;
                    mouseDownObject = "tilemod";
                    mouseDownTileMod = mouseOverTileMod;
                    tileMods.CurrentMod = mouseDownTileMod;
                    tileMods.SelectedMod = mouseDownTileMod;
                    levels.TileModsFieldTree.SelectedNode = levels.TileModsFieldTree.Nodes[tileMods.CurrentMod];
                    mouseDownPosition = new Point((x / 16) - tileMods.X, (y / 16) - tileMods.Y);
                }
                if (state.SolidMods && mouseOverObject == "solidmod" && mouseDownObject == null)
                {
                    levels.TabControl.SelectedIndex = 5;
                    mouseDownObject = "solidmod";
                    mouseDownSolidMod = mouseOverSolidMod;
                    solidMods.CurrentMod = mouseDownSolidMod;
                    solidMods.SelectedMod = mouseDownSolidMod;
                    levels.SolidModsFieldTree.SelectedNode = levels.SolidModsFieldTree.Nodes[solidMods.CurrentMod];
                }
                if (state.SolidityLayer && mouseOverObject == "solid tile" && mouseDownObject == null)
                {
                    mouseDownObject = "solid tile";
                    mouseDownSolidTile = mouseOverSolidTile;
                    mouseDownSolidTileIndex = mouseDownSolidTileNum;
                    selsolidt = HightlightedTile(mouseDownSolidTileNum);
                    selsolidt_location = solidity.TileCoords[mouseDownSolidTile];
                    selsolidt_location.Y -= 768;
                    if ((Control.ModifierKeys & Keys.Control) == 0)
                    {
                        Point tL = new Point(x, y);
                        Point bR = new Point(x + 1, y + 1);
                        Tilemap map;
                        if (state.SolidMods && (Tilemap)solidMods.Mod_ != null)
                            map = (Tilemap)solidMods.Mod_;
                        else
                            map = solidityMap;
                        commandStack_S.Push(new SolidityEditCommand(this.levels, map, tL, bR, tL, new byte[0x20C2]));
                        if (state.SolidMods)
                            solidMods.Mod_.CopyToTiles();
                        solidity.RefreshTilemapImage(map, mouseDownSolidTile * 2);
                        map.Image = null;
                        p1SolidityImage = null;
                        this.pushes.Push(1);
                    }
                }
                if (state.Mushrooms && mouseOverObject == "mushroom" && mouseDownObject == null)
                {
                    mouseDownObject = "mushroom";
                    mouseDownMushroom = mouseOverMushroom;
                }
            }
            #endregion
            panelLevelPicture.AutoScrollPosition = autoScrollPos;
            pictureBoxLevel.Invalidate();
        }
        private void pictureBoxLevel_MouseUp(object sender, MouseEventArgs e)
        {
            int x = Math.Max(0, Math.Min(e.X / zoom, width));
            int y = Math.Max(0, Math.Min(e.Y / zoom, height));
            if (mouseDownObject == "solid tile")
            {
                selsolidt = null;
                Point initial = new Point(x, y);
                Point final = new Point(x + 1, y + 1);
                byte[] temp = new byte[0x20C2];
                Bits.SetShort(temp, mouseOverSolidTile * 2, mouseDownSolidTileIndex);
                Tilemap map = state.SolidMods ? (Tilemap)solidMods.Mod_ : solidityMap;
                commandStack_S.Push(new SolidityEditCommand(this.levels, map, initial, final, initial, temp));
                if (state.SolidMods)
                    solidMods.Mod_.CopyToTiles();
                solidity.RefreshTilemapImage(map, mouseOverSolidTile * 2);
                map.Image = null;
                p1SolidityImage = null;
                pictureBoxLevel.Invalidate();
                this.pushes.Push(1);
            }
            mouseDownExitField = -1;
            mouseDownEventField = -1;
            mouseDownNPC = -1;
            mouseDownNPCInstance = -1;
            mouseDownOverlap = -1;
            mouseDownSolidTile = 0;
            mouseDownSolidTileIndex = -1;
            mouseDownMushroom = -1;
            mouseDownObject = null;
            if (state.Draw || state.Erase || state.Fill)
            {
                if (!state.SolidityLayer && !state.SolidMods)
                {
                    SetLevelImage();
                    if (level != null)
                        tileMods.ClearImages();
                }
                else
                    pictureBoxLevel.Invalidate();
            }
            pictureBoxLevel.Focus();
        }
        private void pictureBoxLevel_MouseMove(object sender, MouseEventArgs e)
        {
            // set a floor and ceiling for the coordinates
            int x = Math.Max(0, Math.Min(e.X / zoom, width));
            int y = Math.Max(0, Math.Min(e.Y / zoom, height));
            // must first check if within same bounds as last call of MouseMove event
            if (state.SolidityLayer || state.SolidMods)
                mouseWithinSameBounds = mouseOverSolidTile ==
                    solidity.PixelTiles[Math.Min(y * width + x, (width - 1) * (height - 1))];
            else
                mouseWithinSameBounds = mouseOverTile == (y / 16 * 64) + (x / 16);
            // now set the properties
            mousePosition = new Point(x, y);
            mouseLastIsometricPosition = new Point(mouseIsometricPosition.X, mouseIsometricPosition.Y);
            mouseIsometricPosition.X = solidity.PixelCoords[Math.Min(y * width + x, (width - 1) * (height - 1))].X;
            mouseIsometricPosition.Y = solidity.PixelCoords[Math.Min(y * width + x, (width - 1) * (height - 1))].Y;
            mouseOverTile = (y / 16 * 64) + (x / 16);
            mouseOverSolidTile = solidity.PixelTiles[Math.Min(y * width + x, (width - 1) * (height - 1))];
            mouseOverObject = null;
            UpdateCoordLabels();
            #region Highlight in tileset
            if ((Control.ModifierKeys & Keys.Shift) != 0)
            {
                int index = 0;
                if (!state.SolidityLayer)
                {
                    int layer_ = 0;
                    bool ignoreTransparent = minecart == null;
                    index = tilemap.GetTileNum(0, mousePosition.X, mousePosition.Y, ignoreTransparent);
                    if (index == 0)
                    {
                        layer_++;
                        index = tilemap.GetTileNum(1, mousePosition.X, mousePosition.Y, ignoreTransparent);
                    }
                    if (index == 0)
                    {
                        layer_++;
                        index = tilemap.GetTileNum(2, mousePosition.X, mousePosition.Y, ignoreTransparent);
                    }
                    tilesetEditor.Layer = layer_;
                    tilesetEditor.mousePosition = new Point(index % 16 * 16, index / 16 * 16);
                    tilesetEditor.PictureBox.Invalidate();
                }
                else if (state.SolidityLayer)
                {
                    index = solidityMap.GetTileNum(solidity.PixelTiles[mousePosition.Y * width + mousePosition.X]);
                    if (!levels.OpenSolidTileset.Checked)
                        levels.OpenSolidTileset.PerformClick();
                    levelsSolidTiles.Index = index;
                }
                else if (state.SolidMods)
                {
                    index = solidMods.Mod_.GetTileNum(solidity.PixelTiles[mousePosition.Y * width + mousePosition.X]);
                    if (!levels.OpenSolidTileset.Checked)
                        levels.OpenSolidTileset.PerformClick();
                    levelsSolidTiles.Index = index;
                }
            }
            #endregion
            #region Zooming
            // if either zoom button is checked, don't do anything else
            if (buttonZoomIn.Checked || buttonZoomOut.Checked)
            {
                pictureBoxLevel.Invalidate();
                return;
            }
            #endregion
            #region Dropper
            // show zoom box for selecting colors
            if (state.Dropper)
            {
                zoomPanel.Location = new Point(MousePosition.X + 64, MousePosition.Y);
                zoomPanel.Visible = true;
                zoomPanel.PictureBox.Invalidate();
                pictureBoxLevel.Invalidate();
                return;
            }
            #endregion
            #region Drawing, erasing, selecting
            if (state.Select)
            {
                // if making a new selection
                if (e.Button == MouseButtons.Left && mouseDownObject == null && overlay.Select != null)
                {
                    // cancel if within same bounds as last call
                    if (overlay.Select.Final == new Point(x + 16, y + 16))
                        return;
                    // otherwise, set the lower right edge of the selection
                    overlay.Select.Final = new Point(
                        Math.Min(x + 16, pictureBoxLevel.Width),
                        Math.Min(y + 16, pictureBoxLevel.Height));
                }
                // if dragging the current selection
                else if (!state.SolidityLayer && e.Button == MouseButtons.Left && mouseDownObject == "selection")
                    overlay.Select.Location = new Point(x / 16 * 16 - mouseDownPosition.X, y / 16 * 16 - mouseDownPosition.Y);
                // if mouse not clicked and within the current selection
                else if (!state.SolidityLayer && e.Button == MouseButtons.None && overlay.Select != null && overlay.Select.MouseWithin(x, y))
                {
                    mouseOverObject = "selection";
                    pictureBoxLevel.Cursor = Cursors.SizeAll;
                }
                else
                    pictureBoxLevel.Cursor = Cursors.Cross;
                pictureBoxLevel.Invalidate();
                return;
            }
            if (!state.SolidityLayer && !state.SolidMods)
            {
                if (state.Draw && e.Button == MouseButtons.Left)
                {
                    Draw(pictureBoxLevel.CreateGraphics(), x, y);
                    return;
                }
                else if (state.Erase && e.Button == MouseButtons.Left)
                {
                    Erase(pictureBoxLevel.CreateGraphics(), x, y);
                    return;
                }
            }
            else if (state.SolidityLayer || state.SolidMods)
            {
                if (!mouseWithinSameBounds)
                {
                    if (state.Draw && e.Button == MouseButtons.Left)
                        Draw(pictureBoxLevel.CreateGraphics(), x, y);
                    if (state.Erase && e.Button == MouseButtons.Left)
                        Erase(pictureBoxLevel.CreateGraphics(), x, y);
                }
            }
            #endregion
            #region Objects
            if (!state.Template && !state.Draw && !state.Select && !state.Erase && !state.Dropper && !state.Fill)
            {
                #region Check if dragging a field
                if (mouseDownObject != null && e.Button == MouseButtons.Left)  // if dragging a field
                {
                    //if (Math.Abs(mouseIsometricPosition.X - mouseLastIsometricPosition.X) > 0 ||
                    //    Math.Abs(mouseIsometricPosition.Y - mouseLastIsometricPosition.Y) > 0)
                    //    return;
                    if (mouseDownObject == "maskNW")
                    {
                        levels.LayerMaskLowX.Value = Math.Min(mouseTilePosition.X, layer.MaskHighX);
                        levels.LayerMaskLowY.Value = Math.Min(mouseTilePosition.Y, layer.MaskHighY);
                    }
                    if (mouseDownObject == "maskNE")
                    {
                        levels.LayerMaskHighX.Value = Math.Max(mouseTilePosition.X, layer.MaskLowX);
                        levels.LayerMaskLowY.Value = Math.Min(mouseTilePosition.Y, layer.MaskHighY);
                    }
                    if (mouseDownObject == "maskSW")
                    {
                        levels.LayerMaskLowX.Value = Math.Min(mouseTilePosition.X, layer.MaskHighX);
                        levels.LayerMaskHighY.Value = Math.Max(mouseTilePosition.Y, layer.MaskLowY);
                    }
                    if (mouseDownObject == "maskSE")
                    {
                        levels.LayerMaskHighX.Value = Math.Max(mouseTilePosition.X, layer.MaskLowX);
                        levels.LayerMaskHighY.Value = Math.Max(mouseTilePosition.Y, layer.MaskLowY);
                    }
                    if (mouseDownObject == "maskW")
                        levels.LayerMaskLowX.Value = Math.Min(mouseTilePosition.X, layer.MaskHighX);
                    if (mouseDownObject == "maskE")
                        levels.LayerMaskHighX.Value = Math.Max(mouseTilePosition.X, layer.MaskLowX);
                    if (mouseDownObject == "maskN")
                        levels.LayerMaskLowY.Value = Math.Min(mouseTilePosition.Y, layer.MaskHighY);
                    if (mouseDownObject == "maskS")
                        levels.LayerMaskHighY.Value = Math.Max(mouseTilePosition.Y, layer.MaskLowY);
                    if (mouseDownObject == "exit")
                    {
                        if (levels.ExitX.Value != mouseIsometricPosition.X &&
                            levels.ExitY.Value != mouseIsometricPosition.Y)
                            levels.UpdatingLevel = true;
                        levels.ExitX.Value = mouseIsometricPosition.X;
                        levels.UpdatingLevel = false;
                        levels.ExitY.Value = mouseIsometricPosition.Y;
                    }
                    if (mouseDownObject == "event")
                    {
                        if (levels.EventX.Value != mouseIsometricPosition.X &&
                            levels.EventY.Value != mouseIsometricPosition.Y)
                            levels.UpdatingLevel = true;
                        levels.EventX.Value = mouseIsometricPosition.X;
                        levels.UpdatingLevel = false;
                        levels.EventY.Value = mouseIsometricPosition.Y;
                    }
                    if (mouseDownObject == "npc" || mouseDownObject == "npc instance")
                    {
                        if (levels.NpcXCoord.Value != mouseIsometricPosition.X &&
                            levels.NpcYCoord.Value != mouseIsometricPosition.Y)
                            levels.UpdatingLevel = true;
                        levels.NpcXCoord.Value = mouseIsometricPosition.X;
                        levels.UpdatingLevel = false;
                        levels.NpcYCoord.Value = mouseIsometricPosition.Y;
                    }
                    if (mouseDownObject == "overlap")
                    {
                        if (levels.OverlapX.Value != mouseIsometricPosition.X &&
                            levels.OverlapY.Value != mouseIsometricPosition.Y)
                            levels.UpdatingLevel = true;
                        levels.OverlapX.Value = mouseIsometricPosition.X;
                        levels.UpdatingLevel = false;
                        levels.OverlapY.Value = mouseIsometricPosition.Y;
                    }
                    if (mouseDownObject == "tilemod")
                    {
                        int a = Math.Min(Math.Max(0, mouseTilePosition.X - mouseDownPosition.X), 63);
                        int b = Math.Min(Math.Max(0, mouseTilePosition.Y - mouseDownPosition.Y), 63);
                        if (levels.TileModsX.Value != a &&
                            levels.TileModsY.Value != b)
                            levels.UpdatingLevel = true;
                        levels.TileModsX.Value = a;
                        levels.UpdatingLevel = false;
                        levels.TileModsY.Value = b;
                    }
                    if (mouseDownObject == "solidmod")
                    {
                        if (levels.SolidModsX.Value != mouseIsometricPosition.X &&
                            levels.SolidModsY.Value != mouseIsometricPosition.Y)
                            levels.UpdatingLevel = true;
                        levels.SolidModsX.Value = mouseIsometricPosition.X;
                        levels.UpdatingLevel = false;
                        levels.SolidModsY.Value = mouseIsometricPosition.Y;
                    }
                    if (mouseDownObject == "solid tile")
                    {
                        selsolidt_location = solidity.TileCoords[mouseOverSolidTile];
                        selsolidt_location.Y -= 768;
                    }
                    if (mouseDownObject == "mushroom")
                    {
                        mushrooms[mouseDownMushroom].X = x / 16;
                        mushrooms[mouseDownMushroom].Y = y / 16;
                    }
                    pictureBoxLevel.Invalidate();
                    return;
                }
                #endregion
                #region Check if over an object
                else
                {
                    pictureBoxLevel.Cursor = Cursors.Arrow;
                    if (state.Mask)
                    {
                        if (mouseTilePosition.X == layer.MaskLowX && mouseTilePosition.Y == layer.MaskLowY)
                        {
                            pictureBoxLevel.Cursor = Cursors.SizeNWSE;
                            mouseOverObject = "maskNW";
                        }
                        else if (mouseTilePosition.X == layer.MaskLowX && mouseTilePosition.Y == layer.MaskHighY)
                        {
                            pictureBoxLevel.Cursor = Cursors.SizeNESW;
                            mouseOverObject = "maskSW";
                        }
                        else if (mouseTilePosition.X == layer.MaskHighX && mouseTilePosition.Y == layer.MaskLowY)
                        {
                            pictureBoxLevel.Cursor = Cursors.SizeNESW;
                            mouseOverObject = "maskNE";
                        }
                        else if (mouseTilePosition.X == layer.MaskHighX && mouseTilePosition.Y == layer.MaskHighY)
                        {
                            pictureBoxLevel.Cursor = Cursors.SizeNWSE;
                            mouseOverObject = "maskSE";
                        }
                        else if (mouseTilePosition.X == layer.MaskLowX &&
                            mouseTilePosition.Y <= layer.MaskHighY && mouseTilePosition.Y >= layer.MaskLowY)
                        {
                            pictureBoxLevel.Cursor = Cursors.SizeWE;
                            mouseOverObject = "maskW";
                        }
                        else if (mouseTilePosition.Y == layer.MaskLowY &&
                            mouseTilePosition.X <= layer.MaskHighX && mouseTilePosition.X >= layer.MaskLowX)
                        {
                            pictureBoxLevel.Cursor = Cursors.SizeNS;
                            mouseOverObject = "maskN";
                        }
                        else if (mouseTilePosition.X == layer.MaskHighX &&
                            mouseTilePosition.Y <= layer.MaskHighY && mouseTilePosition.Y >= layer.MaskLowY)
                        {
                            pictureBoxLevel.Cursor = Cursors.SizeWE;
                            mouseOverObject = "maskE";
                        }
                        else if (mouseTilePosition.Y == layer.MaskHighY &&
                            mouseTilePosition.X <= layer.MaskHighX && mouseTilePosition.X >= layer.MaskLowX)
                        {
                            pictureBoxLevel.Cursor = Cursors.SizeNS;
                            mouseOverObject = "maskS";
                        }
                    }
                    if (state.Exits && exits.Count != 0 && mouseOverObject == null)
                    {
                        int index_ext = 0;
                        foreach (Exit exit in exits.Exits)
                        {
                            if (exit.X == mouseIsometricPosition.X &&
                                exit.Y == mouseIsometricPosition.Y)
                            {
                                this.pictureBoxLevel.Cursor = Cursors.Hand;
                                mouseOverExitField = index_ext;
                                mouseOverObject = "exit";
                                break;
                            }
                            else
                            {
                                this.pictureBoxLevel.Cursor = Cursors.Arrow;
                                mouseOverExitField = 0;
                                mouseOverObject = null;
                            }
                            index_ext++;
                        }
                    }
                    if (state.Events && events.Count != 0 && mouseOverObject == null)
                    {
                        int index_evt = 0;
                        foreach (Event event_ in events.Events)
                        {
                            if (event_.X == mouseIsometricPosition.X &&
                                event_.Y == mouseIsometricPosition.Y)
                            {
                                this.pictureBoxLevel.Cursor = Cursors.Hand;
                                mouseOverEventField = index_evt;
                                mouseOverObject = "event";
                                break;
                            }
                            else
                            {
                                this.pictureBoxLevel.Cursor = Cursors.Arrow;
                                mouseOverEventField = 0;
                                mouseOverObject = null;
                            }
                            index_evt++;
                        }
                    }
                    if (state.NPCs && npcs.Count != 0 && mouseOverObject == null)
                    {
                        int index_npc = 0;
                        foreach (NPC npc in npcs.Npcs)
                        {
                            if (npc.X == mouseIsometricPosition.X &&
                                npc.Y == mouseIsometricPosition.Y)
                            {
                                this.pictureBoxLevel.Cursor = Cursors.Hand;
                                mouseOverNPC = index_npc;
                                mouseOverNPCInstance = -1;
                                mouseOverObject = "npc";
                                break;
                            }
                            else
                            {
                                this.pictureBoxLevel.Cursor = Cursors.Arrow;
                                mouseOverNPC = -1;
                                mouseOverObject = null;
                            }
                            // for all of the instances
                            int index_ins = 0;
                            foreach (NPC.Instance instance in npc.Instances)
                            {
                                if (instance.X == mouseIsometricPosition.X &&
                                    instance.Y == mouseIsometricPosition.Y)
                                {
                                    this.pictureBoxLevel.Cursor = Cursors.Hand;
                                    mouseOverNPC = index_npc;
                                    mouseOverNPCInstance = index_ins;
                                    mouseOverObject = "npc instance";
                                    goto finish;
                                }
                                else
                                {
                                    this.pictureBoxLevel.Cursor = Cursors.Arrow;
                                    mouseOverNPCInstance = -1;
                                    mouseOverObject = null;
                                }
                                index_ins++;
                            }
                            index_npc++;
                        }
                    }
                finish:
                    if (state.Overlaps && overlaps.Count != 0 && mouseOverObject == null)
                    {
                        int index_ovr = 0;
                        foreach (Overlap overlap in overlaps.Overlaps)
                        {
                            if (overlap.X == mouseIsometricPosition.X &&
                                overlap.Y == mouseIsometricPosition.Y)
                            {
                                this.pictureBoxLevel.Cursor = Cursors.Hand;
                                mouseOverOverlap = index_ovr;
                                mouseOverObject = "overlap";
                                break;
                            }
                            else
                            {
                                this.pictureBoxLevel.Cursor = Cursors.Arrow;
                                mouseOverOverlap = 0;
                                mouseOverObject = null;
                            }
                            index_ovr++;
                        }
                    }
                    if (state.TileMods && tileMods.Count != 0 && mouseOverObject == null)
                    {
                        int index_tlm = 0;
                        foreach (LevelTileMods.Mod mod in tileMods.Mods)
                        {
                            if (mod.WithinBounds(x / 16, y / 16))
                            {
                                this.pictureBoxLevel.Cursor = Cursors.Hand;
                                mouseOverTileMod = index_tlm;
                                mouseOverObject = "tilemod";
                                break;
                            }
                            else
                            {
                                this.pictureBoxLevel.Cursor = Cursors.Arrow;
                                mouseOverTileMod = 0;
                                mouseOverObject = null;
                            }
                            index_tlm++;
                        }
                    }
                    if (state.SolidMods && solidMods.Count != 0 && mouseOverObject == null)
                    {
                        int index_slm = 0;
                        foreach (LevelSolidMods.LevelMod mod in solidMods.Mods)
                        {
                            if (mod.X == mouseIsometricPosition.X &&
                                mod.Y == mouseIsometricPosition.Y)
                            {
                                this.pictureBoxLevel.Cursor = Cursors.Hand;
                                mouseOverSolidMod = index_slm;
                                mouseOverObject = "solidmod";
                                break;
                            }
                            else
                            {
                                this.pictureBoxLevel.Cursor = Cursors.Arrow;
                                mouseOverSolidMod = 0;
                                mouseOverObject = null;
                            }
                            index_slm++;
                        }
                    }
                    if (state.SolidityLayer && buttonDragSolidity.Checked)
                    {
                        if (mouseOverSolidTileNum != 0)
                        {
                            this.pictureBoxLevel.Cursor = Cursors.Hand;
                            mouseOverObject = "solid tile";
                        }
                        else
                        {
                            this.pictureBoxLevel.Cursor = Cursors.Arrow;
                            mouseOverObject = null;
                        }
                    }
                    if (state.Mushrooms && mouseOverObject == null && mushrooms != null)
                    {
                        for (int i = 0; i < mushrooms.Length; i++)
                        {
                            if (mushrooms[i].X == x / 16 && mushrooms[i].Y == y / 16)
                            {
                                this.pictureBoxLevel.Cursor = Cursors.Hand;
                                mouseOverObject = "mushroom";
                                mouseOverMushroom = i;
                                break;
                            }
                            else
                            {
                                this.pictureBoxLevel.Cursor = Cursors.Arrow;
                                mouseOverObject = null;
                                mouseOverMushroom = -1;
                            }
                        }
                    }
                }
                #endregion
            }
            #endregion
            if (!state.SolidityLayer && !state.SolidMods &&
                !state.NPCs && !state.Exits && !state.Events && !state.Overlaps && !mouseWithinSameBounds)
                pictureBoxLevel.Invalidate();
            else if (state.SolidityLayer || state.SolidMods ||
                state.NPCs || state.Exits || state.Events || state.Overlaps)
                pictureBoxLevel.Invalidate();
        }
        private void pictureBoxLevel_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            //toolStripMenuItem5_Click(null, null);
        }
        private void pictureBoxLevel_MouseEnter(object sender, EventArgs e)
        {
            mouseEnter = true;
            tilesetEditor.HiliteTile = true;
            Form parent;
            if (levels != null)
                parent = levels;
            else
                parent = minecart.MiniGames;
            if (GetForegroundWindow() == parent.Handle)
                pictureBoxLevel.Focus();
            pictureBoxLevel.Invalidate();
        }
        private void pictureBoxLevel_MouseLeave(object sender, EventArgs e)
        {
            mouseEnter = false;
            tilesetEditor.HiliteTile = false;
            zoomPanel.Hide();
            pictureBoxLevel.Invalidate();
        }
        private void pictureBoxLevel_MouseHover(object sender, EventArgs e)
        {
            pictureBoxLevel.Invalidate();
        }
        private void pictureBoxLevel_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            switch (e.KeyData)
            {
                case Keys.Control | Keys.V:
                    buttonEditPaste_Click(null, null); break;
                case Keys.Control | Keys.C:
                    buttonEditCopy_Click(null, null); break;
                case Keys.Delete:
                    buttonEditDelete_Click(null, null); break;
                case Keys.Control | Keys.X:
                    buttonEditCut_Click(null, null); break;
                case Keys.Control | Keys.D:
                    if (draggedTiles != null)
                        PasteFinal(draggedTiles);
                    else
                    {
                        overlay.Select = null;
                        pictureBoxLevel.Invalidate();
                    }
                    break;
                case Keys.Control | Keys.A:
                    if (!state.Select) break;
                    if (draggedTiles != null)
                        PasteFinal(draggedTiles);
                    overlay.Select = new Overlay.Selection(16, 0, 0, width, height);
                    pictureBoxLevel.Invalidate();
                    break;
                case Keys.Control | Keys.Z:
                    buttonEditUndo_Click(null, null); break;
                case Keys.Control | Keys.Y:
                    buttonEditRedo_Click(null, null); break;
            }
        }
        private void panelLevelPicture_Scroll(object sender, ScrollEventArgs e)
        {
            autoScrollPos.X = Math.Abs(panelLevelPicture.AutoScrollPosition.X);
            autoScrollPos.Y = Math.Abs(panelLevelPicture.AutoScrollPosition.Y);
            pictureBoxLevel.Invalidate();
            panelLevelPicture.Invalidate();
        }
        //toolstrip buttons
        private void buttonToggleCartGrid_Click(object sender, EventArgs e)
        {
            state.CartesianGrid = toggleCartGrid.Checked;
            state.IsometricGrid = toggleIsoGrid.Checked = false;
            pictureBoxLevel.Invalidate();
        }
        private void buttonToggleOrthGrid_Click(object sender, EventArgs e)
        {
            state.IsometricGrid = toggleIsoGrid.Checked;
            state.CartesianGrid = toggleCartGrid.Checked = false;
            pictureBoxLevel.Invalidate();
        }
        private void buttonToggleBG_Click(object sender, EventArgs e)
        {
            state.BG = toggleBG.Checked;
            tilemap.RedrawTilemaps();
            tileMods.RedrawTilemaps();
            SetLevelImage();
        }
        private void buttonToggleMask_Click(object sender, EventArgs e)
        {
            state.Mask = toggleMask.Checked;
            pictureBoxLevel.Invalidate();
        }
        private void buttonToggleBoundaries_Click(object sender, EventArgs e)
        {
            buttonToggleBoundaries.Checked = !buttonToggleBoundaries.Checked;
            state.ShowBoundaries = buttonToggleBoundaries.Checked;
            pictureBoxLevel.Invalidate();
        }
        private void buttonToggleL1_Click(object sender, EventArgs e)
        {
            state.Layer1 = toggleL1.Checked;
            tilemap.RedrawTilemaps();
            tileMods.RedrawTilemaps();
            SetLevelImage();
        }
        private void buttonToggleL2_Click(object sender, EventArgs e)
        {
            state.Layer2 = toggleL2.Checked;
            tilemap.RedrawTilemaps();
            tileMods.RedrawTilemaps();
            SetLevelImage();
        }
        private void buttonToggleL3_Click(object sender, EventArgs e)
        {
            state.Layer3 = toggleL3.Checked;
            tilemap.RedrawTilemaps();
            tileMods.RedrawTilemaps();
            SetLevelImage();
        }
        private void buttonToggleP1_Click(object sender, EventArgs e)
        {
            state.Priority1 = toggleP1.Checked;
            pictureBoxLevel.Invalidate();
        }
        private void buttonTogglePhys_Click(object sender, EventArgs e)
        {
            PasteClear();
            state.SolidityLayer = toggleSolid.Checked;
            buttonDragSolidity.Enabled = toggleSolid.Checked;
            if (!buttonDragSolidity.Enabled)
                buttonDragSolidity.Checked = false;
            pictureBoxLevel.Invalidate();
            ToggleTilesets();
        }
        private void buttonToggleTileMods_Click(object sender, EventArgs e)
        {
            PasteClear();
            state.TileMods = toggleTileMods.Checked;
            pictureBoxLevel.Invalidate();
        }
        private void buttonToggleSolidMods_Click(object sender, EventArgs e)
        {
            PasteClear();
            state.SolidMods = toggleSolidMods.Checked;
            pictureBoxLevel.Invalidate();
            ToggleTilesets();
        }
        private void buttonToggleNPCs_Click(object sender, EventArgs e)
        {
            PasteClear();
            state.NPCs = toggleNPCs.Checked;
            pictureBoxLevel.Invalidate();
        }
        private void buttonToggleExits_Click(object sender, EventArgs e)
        {
            PasteClear();
            state.Exits = toggleExits.Checked;
            pictureBoxLevel.Invalidate();
        }
        private void buttonToggleEvents_Click(object sender, EventArgs e)
        {
            PasteClear();
            state.Events = toggleEvents.Checked;
            pictureBoxLevel.Invalidate();
        }
        private void buttonToggleOverlaps_Click(object sender, EventArgs e)
        {
            PasteClear();
            state.Overlaps = toggleOverlaps.Checked;
            pictureBoxLevel.Invalidate();
        }
        private void buttonToggleMushrooms_Click(object sender, EventArgs e)
        {
            PasteClear();
            state.Mushrooms = toggleMushrooms.Checked;
            pictureBoxLevel.Invalidate();
        }
        private void toggleRails_Click(object sender, EventArgs e)
        {
            PasteClear();
            state.Rails = toggleRails.Checked;
            pictureBoxLevel.Invalidate();
            tilesetEditor.Rails = state.Rails;
            tilesetEditor.PictureBox.Invalidate();
            minecart.RailColorKey.Visible = state.Rails;
        }
        private void tags_Click(object sender, EventArgs e)
        {
            pictureBoxLevel.Invalidate();
        }
        private void opacityToolStripButton_Click(object sender, EventArgs e)
        {
            panelOpacity.Visible = !panelOpacity.Visible;
        }
        private void overlayOpacity_ValueChanged(object sender, EventArgs e)
        {
            labelOverlayOpacity.Text = overlayOpacity.Value.ToString() + "%";
            pictureBoxLevel.Invalidate();
        }
        // drawing
        private void buttonEditDraw_Click(object sender, EventArgs e)
        {
            state.Draw = buttonEditDraw.Checked;
            Do.ResetToolStripButtons(toolStrip2, (ToolStripButton)sender, editAllLayers);
            if (buttonEditDraw.Checked)
                this.pictureBoxLevel.Cursor = new Cursor(GetType(), "CursorDraw.cur");
            else if (!buttonEditDraw.Checked)
                this.pictureBoxLevel.Cursor = Cursors.Arrow;
            PasteClear();
            pictureBoxLevel.Invalidate();
        }
        private void buttonEditSelect_Click(object sender, EventArgs e)
        {
            state.Select = buttonEditSelect.Checked;
            Do.ResetToolStripButtons(toolStrip2, (ToolStripButton)sender, editAllLayers);
            if (state.Select)
                this.pictureBoxLevel.Cursor = Cursors.Cross;
            else if (!state.Select)
                this.pictureBoxLevel.Cursor = Cursors.Arrow;
            PasteClear();
            pictureBoxLevel.Invalidate();
        }
        private void selectAll_Click(object sender, EventArgs e)
        {
            if (!state.Select)
                buttonEditSelect.PerformClick();
            PasteClear();
            overlay.Select = new Overlay.Selection(16, 0, 0, width, height);
            pictureBoxLevel.Invalidate();
        }
        private void buttonEditErase_Click(object sender, EventArgs e)
        {
            state.Erase = buttonEditErase.Checked;
            Do.ResetToolStripButtons(toolStrip2, (ToolStripButton)sender, editAllLayers);
            if (state.Erase)
                this.pictureBoxLevel.Cursor = new Cursor(GetType(), "CursorErase.cur");
            else if (!state.Erase)
                this.pictureBoxLevel.Cursor = Cursors.Arrow;

            PasteClear();
            pictureBoxLevel.Invalidate();
        }
        private void buttonEditDropper_Click(object sender, EventArgs e)
        {
            state.Dropper = buttonEditDropper.Checked;
            Do.ResetToolStripButtons(toolStrip2, (ToolStripButton)sender, editAllLayers);
            if (state.Dropper)
                this.pictureBoxLevel.Cursor = new Cursor(GetType(), "CursorDropper.cur");
            else if (!state.Dropper)
                this.pictureBoxLevel.Cursor = Cursors.Arrow;

            PasteClear();
            pictureBoxLevel.Invalidate();
        }
        private void buttonEditFill_Click(object sender, EventArgs e)
        {
            state.Fill = buttonEditFill.Checked;
            Do.ResetToolStripButtons(toolStrip2, (ToolStripButton)sender, editAllLayers);
            if (state.Fill)
                this.pictureBoxLevel.Cursor = new Cursor(GetType(), "CursorFill.cur");
            else if (!state.Fill)
                this.pictureBoxLevel.Cursor = Cursors.Arrow;

            PasteClear();
            pictureBoxLevel.Invalidate();
        }
        private void buttonEditTemplate_Click(object sender, EventArgs e)
        {
            state.Template = buttonEditTemplate.Checked;
            Do.ResetToolStripButtons(toolStrip2, (ToolStripButton)sender, editAllLayers);
            if (buttonEditTemplate.Checked)
                this.pictureBoxLevel.Cursor = new Cursor(GetType(), "CursorTemplate.cur");
            else if (!buttonEditTemplate.Checked)
                this.pictureBoxLevel.Cursor = Cursors.Arrow;

            PasteClear();
            pictureBoxLevel.Invalidate();
        }
        private void buttonEditDelete_Click(object sender, EventArgs e)
        {
            Delete();
        }
        private void buttonEditUndo_Click(object sender, EventArgs e)
        {
            Undo();
        }
        private void buttonEditRedo_Click(object sender, EventArgs e)
        {
            Redo();
        }
        private void buttonEditCut_Click(object sender, EventArgs e)
        {
            Cut();
        }
        private void buttonEditCopy_Click(object sender, EventArgs e)
        {
            Copy();
        }
        private void buttonEditPaste_Click(object sender, EventArgs e)
        {
            if (copiedTiles == null) return;
            // set a floor and ceiling for the coordinates
            int x = Math.Max(0, Math.Min(Math.Abs(panelLevelPicture.AutoScrollPosition.X) / zoom / 16 * 16, width - 1));
            int y = Math.Max(0, Math.Min(Math.Abs(panelLevelPicture.AutoScrollPosition.Y) / zoom / 16 * 16, height - 1));
            x += 32; y += 32;
            if (x + copiedTiles.Width >= width)
                x -= x + copiedTiles.Width - width;
            if (y + copiedTiles.Height >= height)
                y -= x + copiedTiles.Height - height;
            if (draggedTiles != null)
                PasteFinal(draggedTiles);
            Paste(new Point(x, y), copiedTiles);
        }
        private void buttonZoomIn_Click(object sender, EventArgs e)
        {
            Do.ResetToolStripButtons(toolStrip2, (ToolStripButton)sender, editAllLayers);
            if (buttonZoomIn.Checked)
                this.pictureBoxLevel.Cursor = new Cursor(GetType(), "CursorZoomIn.cur");
            else if (!buttonZoomIn.Checked)
                this.pictureBoxLevel.Cursor = Cursors.Arrow;
        }
        private void buttonZoomOut_Click(object sender, EventArgs e)
        {
            Do.ResetToolStripButtons(toolStrip2, (ToolStripButton)sender, editAllLayers);
            if (buttonZoomOut.Checked)
                this.pictureBoxLevel.Cursor = new Cursor(GetType(), "CursorZoomOut.cur");
            else if (!buttonZoomOut.Checked)
                this.pictureBoxLevel.Cursor = Cursors.Arrow;
        }
        private void buttonDragSolidity_Click(object sender, EventArgs e)
        {
            state.ClearDrawSelectErase();
            Do.ResetToolStripButtons(toolStrip2, (ToolStripButton)sender, editAllLayers);
            PasteClear();
            pictureBoxLevel.Invalidate();
        }
        // context menu
        private void contextMenuStrip1_Opening(object sender, CancelEventArgs e)
        {
            if (buttonZoomIn.Checked || buttonZoomOut.Checked)
                e.Cancel = true;
            else if (mouseOverObject == "exit")
            {
                foreach (ToolStripItem item in contextMenuStrip1.Items)
                    item.Visible = false;
                objectFunctionToolStripMenuItem.Text = "Load destination";
                objectFunctionToolStripMenuItem.Tag = mouseOverExitField;
                objectFunctionToolStripMenuItem.Visible = true;
            }
            else if (mouseOverObject == "event")
            {
                foreach (ToolStripItem item in contextMenuStrip1.Items)
                    item.Visible = false;
                objectFunctionToolStripMenuItem.Text = "Edit event's script";
                objectFunctionToolStripMenuItem.Tag = mouseOverEventField;
                objectFunctionToolStripMenuItem.Visible = true;
            }
            else if (mouseOverObject == "npc" || mouseOverObject == "npc instance")
            {
                foreach (ToolStripItem item in contextMenuStrip1.Items)
                    item.Visible = false;
                if (npcs.Npcs[mouseOverNPC].EngageType == 0 || npcs.Npcs[mouseOverNPC].EngageType == 1)
                    objectFunctionToolStripMenuItem.Text = "Edit npc's script";
                else if (npcs.Npcs[mouseOverNPC].EngageType == 2)
                    objectFunctionToolStripMenuItem.Text = "Edit npc's formation pack";
                objectFunctionToolStripMenuItem.Tag = new List<int> { mouseOverNPC, mouseOverNPCInstance };
                objectFunctionToolStripMenuItem.Visible = true;
            }
            else if (minecart != null)
            {
                createTileModToolStripMenuItem.Visible = false;
                exportToBattlefieldToolStripMenuItem.Visible = false;
            }
            else
            {
                foreach (ToolStripItem item in contextMenuStrip1.Items)
                    item.Visible = true;
                objectFunctionToolStripMenuItem.Visible = false;
            }
        }
        private void findInTileset_Click(object sender, EventArgs e)
        {
            int index;
            if (state.SolidityLayer)
            {
                index = solidityMap.GetTileNum(solidity.PixelTiles[mousePosition.Y * width + mousePosition.X]);
                if (!levels.OpenSolidTileset.Checked)
                    levels.OpenSolidTileset.PerformClick();
                levelsSolidTiles.Index = index;
                return;
            }
            if (state.SolidMods)
            {
                index = solidMods.Mod_.GetTileNum(solidity.PixelTiles[mousePosition.Y * width + mousePosition.X]);
                if (!levels.OpenSolidTileset.Checked)
                    levels.OpenSolidTileset.PerformClick();
                levelsSolidTiles.Index = index;
                return;
            }
            int layer = 0;
            bool ignoreTransparent = minecart == null;
            index = tilemap.GetTileNum(0, mousePosition.X, mousePosition.Y, ignoreTransparent);
            if (index == 0)
            {
                layer++;
                index = tilemap.GetTileNum(1, mousePosition.X, mousePosition.Y, ignoreTransparent);
            }
            if (index == 0)
            {
                layer++;
                index = tilemap.GetTileNum(2, mousePosition.X, mousePosition.Y, ignoreTransparent);
            }
            if (index != 0) // only if not all layers empty, otherwise stay at current layer tab
                tilesetEditor.Layer = layer;
            tilesetEditor.MouseDownTile = index;
        }
        private void createTileModToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (overlay.Select == null)
            {
                MessageBox.Show("Must make a selection before creating a new tile mod.", "LAZY SHELL", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (overlay.Select.Width / 16 >= 64 ||
                overlay.Select.Height / 16 >= 64)
            {
                MessageBox.Show("Selection must be smaller than 64x64 tiles.", "LAZY SHELL", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            levels.TabControl.SelectedIndex = 5;
            bool instance = false;
            if (levels.TileModsFieldTree.SelectedNode != null &&
                levels.TileModsFieldTree.SelectedNode.Nodes.Count == 0 &&
                levels.TileModsFieldTree.SelectedNode.Parent == null)
                instance = MessageBox.Show("Create as an alternate tile mod?", "LAZY SHELL",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
            if (!instance && !levels.AddNewTileMod())
                return;
            if (instance && !levels.AddNewTileModInstance())
                return;
            if (!instance)
            {
                levels.TileModsX.Value = overlay.Select.X / 16;
                levels.TileModsY.Value = overlay.Select.Y / 16;
                levels.TileModsWidth.Value = overlay.Select.Width / 16;
                levels.TileModsHeight.Value = overlay.Select.Height / 16;
            }
            bool[] empty = new bool[3];
            byte[][] tilemaps = new byte[3][];
            int width = instance ? tileMods.Width : overlay.Select.Width / 16;
            int height = instance ? tileMods.Height : overlay.Select.Height / 16;
            for (int l = 0; l < 3; l++)
            {
                if (l < 2)
                    tilemaps[l] = new byte[(width * height) * 2];
                else
                    tilemaps[l] = new byte[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int tileX = overlay.Select.Location.X + (x * 16);
                        int tileY = overlay.Select.Location.Y + (y * 16);
                        int tile = this.tilemap.GetTileNum(l, tileX, tileY);
                        if (tile != 0)
                            empty[l] = false;
                        int index = y * width + x;
                        if (l < 2)
                            Bits.SetShort(tilemaps[l], index * 2, (ushort)tile);
                        else
                            tilemaps[l][index] = (byte)tile;
                    }
                }
                if (!instance)
                {
                    levels.TileModsLayers.SetItemChecked(l, !empty[l]);
                    levels.tileModsLayers_SelectedIndexChanged(null, null);
                    tileMods.TilemapsA[l] = tilemaps[l];
                }
                else
                    tileMods.TilemapsB[l] = tilemaps[l];
            }
            if (!instance)
                tileMods.TilemapA = new LevelTilemap(level, tileset, tileMods.Mod_, false);
            else
                tileMods.TilemapB = new LevelTilemap(level, tileset, tileMods.Mod_, true);
            if (!toggleTileMods.Checked)
                toggleTileMods.PerformClick();
            tileMods.UpdateTilemaps();
            pictureBoxLevel.Invalidate();
        }
        private void saveImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (minecart == null)
                Do.Export(tilemapImage, "level." + level.Index.ToString("d3") + ".png");
            else
                Do.Export(tilemapImage, "minecart." + minecart.Index.ToString("d2") + ".png");
        }
        private void exportToBattlefieldToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (overlay.Select == null)
            {
                MessageBox.Show("Must make a selection before exporting to battlefield.");
                return;
            }
            Tile[] tiles = new Tile[32 * 32];
            int counter = 0;
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++, counter++)
                {
                    int tileX = overlay.Select.X + (x * 16);
                    int tileY = overlay.Select.Y + (y * 16);
                    int index = tilemap.GetTileNum(tilesetEditor.Layer, tileX, tileY);
                    if (y < overlay.Select.Height / 16 && x < overlay.Select.Width / 16)
                        tiles[counter] = this.tileset.Tilesets_Tiles[tilesetEditor.Layer][index].Copy();
                    else
                        tiles[counter] = new Tile(counter);
                    tiles[counter].TileIndex = counter;
                }
            }
            counter = 256;
            for (int y = 0; y < 16; y++)
            {
                for (int x = 16; x < 32; x++, counter++)
                {
                    int tileX = overlay.Select.X + (x * 16);
                    int tileY = overlay.Select.Y + (y * 16);
                    int index = tilemap.GetTileNum(tilesetEditor.Layer, tileX, tileY);
                    if (y < overlay.Select.Height / 16 && x < overlay.Select.Width / 16)
                        tiles[counter] = this.tileset.Tilesets_Tiles[tilesetEditor.Layer][index].Copy();
                    else
                        tiles[counter] = new Tile(counter);
                    tiles[counter].TileIndex = counter;
                }
            }
            counter = 512;
            for (int y = 16; y < 32; y++)
            {
                for (int x = 0; x < 16; x++, counter++)
                {
                    int tileX = overlay.Select.X + (x * 16);
                    int tileY = overlay.Select.Y + (y * 16);
                    int index = tilemap.GetTileNum(tilesetEditor.Layer, tileX, tileY);
                    if (y < overlay.Select.Height / 16 && x < overlay.Select.Width / 16)
                        tiles[counter] = this.tileset.Tilesets_Tiles[tilesetEditor.Layer][index].Copy();
                    else
                        tiles[counter] = new Tile(counter);
                    tiles[counter].TileIndex = counter;
                }
            }
            counter = 768;
            for (int y = 16; y < 32; y++)
            {
                for (int x = 16; x < 32; x++, counter++)
                {
                    int tileX = overlay.Select.X + (x * 16);
                    int tileY = overlay.Select.Y + (y * 16);
                    int index = tilemap.GetTileNum(tilesetEditor.Layer, tileX, tileY);
                    if (y < overlay.Select.Height / 16 && x < overlay.Select.Width / 16)
                        tiles[counter] = this.tileset.Tilesets_Tiles[tilesetEditor.Layer][index].Copy();
                    else
                        tiles[counter] = new Tile(counter);
                    tiles[counter].TileIndex = counter;
                }
            }
            Battlefield battlefield = new Battlefield(Model.Data, 0);
            battlefield.GraphicSetA = levelMap.GraphicSetA;
            battlefield.GraphicSetB = levelMap.GraphicSetB;
            battlefield.GraphicSetC = levelMap.GraphicSetC;
            battlefield.GraphicSetD = levelMap.GraphicSetD;
            battlefield.GraphicSetE = levelMap.GraphicSetE;
            PaletteSet paletteset = this.levels.PaletteSet.Copy();
            BattlefieldTileSet battlefieldTileset = new BattlefieldTileSet(battlefield, paletteset, tiles);
            battlefieldTileset.Battlefield = battlefield;
            battlefieldTileset.PaletteSet = paletteset;
            battlefieldTileset.TileSetLayer = tiles;
            Do.Export(battlefieldTileset, "tilemap_battlefield");
        }
        private void objectFunctionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (objectFunctionToolStripMenuItem.Text == "Load destination")
            {
                Exit exit = exits.Exits[(int)objectFunctionToolStripMenuItem.Tag];
                if (exit.ExitType == 0)
                    levels.Index = exit.Destination;
                else
                {
                    if (Model.Program.WorldMaps == null || !Model.Program.WorldMaps.Visible)
                        Model.Program.CreateWorldMapsWindow();
                    Model.Program.WorldMaps.Index_l = exit.Destination;
                    Model.Program.WorldMaps.BringToFront();
                }
            }
            else if (objectFunctionToolStripMenuItem.Text == "Edit event's script")
            {
                Event event_ = events.Events[(int)objectFunctionToolStripMenuItem.Tag];
                if (Model.Program.EventScripts == null || !Model.Program.EventScripts.Visible)
                    Model.Program.CreateEventScriptsWindow();
                Model.Program.EventScripts.EventName.SelectedIndex = 0;
                Model.Program.EventScripts.EventNum.Value = event_.RunEvent;
                Model.Program.EventScripts.BringToFront();
            }
            else if (objectFunctionToolStripMenuItem.Text == "Edit npc's script")
            {
                List<int> tag = (List<int>)objectFunctionToolStripMenuItem.Tag;
                NPC npc = npcs.Npcs[tag[0]];
                NPC instance = null;
                if (npc.Instances.Count > 0 && tag[1] >= 0)
                    instance = npc.Instances[tag[1]];
                if (Model.Program.EventScripts == null || !Model.Program.EventScripts.Visible)
                    Model.Program.CreateEventScriptsWindow();
                Model.Program.EventScripts.EventName.SelectedIndex = 0;
                if (instance == null)
                    Model.Program.EventScripts.EventNum.Value = npc.EventORpack + npc.PropertyB;
                else
                    Model.Program.EventScripts.EventNum.Value = npc.EventORpack + instance.PropertyB;
                Model.Program.EventScripts.BringToFront();
            }
            else if (objectFunctionToolStripMenuItem.Text == "Edit npc's formation pack")
            {
                List<int> tag = (List<int>)objectFunctionToolStripMenuItem.Tag;
                NPC npc = npcs.Npcs[tag[0]];
                NPC instance = null;
                if (npc.Instances.Count > 0 && tag[1] >= 0)
                    instance = npc.Instances[tag[1]];
                if (Model.Program.Formations == null || !Model.Program.Formations.Visible)
                    Model.Program.CreateFormationsWindow();
                int pack = npc.EventORpack + (instance == null ? npc.PropertyB : instance.PropertyB);
                Model.Program.Formations.PackIndex = pack;
                Model.Program.Formations.FormationIndex = Model.FormationPacks[pack].PackFormations[0];
                Model.Program.Formations.BringToFront();
            }
        }
        #endregion
    }
}