﻿//--------------------------------------------------------------------------------
// Copyright (C) 2013-2015 JDH Software - <support@jdhsoftware.com>
//
// This program is provided to you under the terms of the Microsoft Public
// License (Ms-PL) as published at https://kryptonoutlookgrid.codeplex.com/license
//
// Visit http://www.jdhsoftware.com and follow @jdhsoftware on Twitter
//
//--------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using ComponentFactory.Krypton.Toolkit;
using System.ComponentModel;
using JDHSoftware.Krypton.Toolkit.Utils.Lang;

namespace JDHSoftware.Krypton.Toolkit.KryptonOutlookGrid
{
    /// <summary>
    /// GroupBox for the Krypton OutlookGrid
    /// </summary>
    public partial class KryptonOutlookGridGroupBox : UserControl
    {
        private List<OutlookGridGroupBoxColumn> columnsList;
        private string _dragColumnToGroupText;

        //Krypton
        private IPalette _palette;
        private PaletteRedirect _paletteRedirect;
        private PaletteBackInheritRedirect _paletteBack;
        private PaletteBorderInheritRedirect _paletteBorder;
        private PaletteContentInheritRedirect _paletteContent;
        private PaletteDataGridViewRedirect _paletteDataGridView;
        private PaletteDataGridViewAll _paletteDataGridViewAll;
        private IDisposable _mementoBack;
        private PaletteBorder _border;

        //Mouse
        private Point _mouse;
        private bool isDragging;
        private int indexselected;

        //Dpi scaling
        private float factorX, factorY;

        //Context menu
        private KryptonContextMenu KCtxMenu;
        private KryptonContextMenuItems _menuItems;
        private KryptonContextMenuItem _menuSortAscending;
        private KryptonContextMenuItem _menuSortDescending;
        private KryptonContextMenuSeparator _menuSeparator1;
        private KryptonContextMenuItem _menuExpand;
        private KryptonContextMenuItem _menuCollapse;
        private KryptonContextMenuItem _menuUnGroup;
        private KryptonContextMenuSeparator _menuSeparator2;
        private KryptonContextMenuItem _menuFullExpand;
        private KryptonContextMenuItem _menuFullCollapse;
        private KryptonContextMenuSeparator _menuSeparator3;
        private KryptonContextMenuItem _menuClearGrouping;
        private KryptonContextMenuItem _menuHideGroupBox;
        private KryptonContextMenuItem _menuGroupInterval;
        private KryptonContextMenuItem _menuSortBySummary;

        /// <summary>
        /// Column Sort Changed Event
        /// </summary>
        public event EventHandler<OutlookGridColumnEventArgs> ColumnSortChanged;
        /// <summary>
        /// Column Group Added Event
        /// </summary>
        public event EventHandler<OutlookGridColumnEventArgs> ColumnGroupAdded;
        /// <summary>
        /// Column Group removed Event
        /// </summary>
        public event EventHandler<OutlookGridColumnEventArgs> ColumnGroupRemoved;
        /// <summary>
        /// Clear grouping event
        /// </summary>
        public event EventHandler ClearGrouping;
        /// <summary>
        /// Full Expand event
        /// </summary>
        public event EventHandler FullExpand;
        /// <summary>
        /// Full Collapse event
        /// </summary>
        public event EventHandler FullCollapse;
        /// <summary>
        /// Group Expand event
        /// </summary>
        public event EventHandler<OutlookGridColumnEventArgs> GroupExpand;
        /// <summary>
        /// Group Collapse event
        /// </summary>
        public event EventHandler<OutlookGridColumnEventArgs> GroupCollapse;
        /// <summary>
        /// Column Group Order Changed Event
        /// </summary>
        public event EventHandler<OutlookGridColumnEventArgs> ColumnGroupOrderChanged;
        /// <summary>
        /// Group Interval Click event
        /// </summary>
        public event EventHandler<OutlookGridColumnEventArgs> GroupIntervalClick;
        /// <summary>
        /// Sort by Summary Count event
        /// </summary>
        public event EventHandler<OutlookGridColumnEventArgs> SortBySummaryCount;

        #region Constructor

        /// <summary>
        /// Constructor
        /// </summary>
        public KryptonOutlookGridGroupBox()
        {
            // To remove flicker we use double buffering for drawing
            SetStyle(
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            InitializeComponent();

            columnsList = new List<OutlookGridGroupBoxColumn>();

            // Cache the current global palette setting
            _palette = KryptonManager.CurrentGlobalPalette;

            // Hook into palette events
            if (_palette != null)
                _palette.PalettePaint += new EventHandler<PaletteLayoutEventArgs>(OnPalettePaint);

            // (4) We want to be notified whenever the global palette changes
            KryptonManager.GlobalPaletteChanged += new EventHandler(OnGlobalPaletteChanged);

            // Create redirection object to the base palette
            _paletteRedirect = new PaletteRedirect(_palette);

            // Create accessor objects for the back, border and content
            // Store the inherit instances
            _paletteBack = new PaletteBackInheritRedirect(_paletteRedirect);
            _paletteBorder = new PaletteBorderInheritRedirect(_paletteRedirect);
            _paletteContent = new PaletteContentInheritRedirect(_paletteRedirect);
            _paletteDataGridView = new PaletteDataGridViewRedirect(_paletteRedirect, null);
            _paletteDataGridViewAll = new PaletteDataGridViewAll(_paletteDataGridView, null);

            // Create storage that maps onto the inherit instances
            _border = new PaletteBorder(_paletteBorder, null);
            _dragColumnToGroupText = LangManager.Instance.GetString("DRAGCOLUMNTOGROUP");

            using (Graphics g = CreateGraphics())
            {
                factorX = g.DpiX > 96 ? (1f * g.DpiX / 96) : 1f;
                factorY = g.DpiY > 96 ? (1f * g.DpiY / 96) : 1f;
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets access to the common textbox appearance entries that other states can override.
        /// </summary>
        [Category("Visuals")]
        [Description("Overrides borders settings.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public PaletteBorder Border
        {
            get { return _border; }
        }

        /// <summary>
        /// Getsor sets the text that appears when no column is grouped.
        /// </summary>
        [Category("Text")]
        [Description("The text that appears when no column is grouped.")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Localizable(true)]
        public String DragColumnToGroupText
        {
            get { return _dragColumnToGroupText; }
            set { _dragColumnToGroupText = value; }
        }

        #endregion

        #region Overrides

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_mementoBack != null)
                {
                    _mementoBack.Dispose();
                    _mementoBack = null;
                }

                // (10) Unhook from the palette events
                if (_palette != null)
                {
                    _palette.PalettePaint -= new EventHandler<PaletteLayoutEventArgs>(OnPalettePaint);
                    _palette = null;
                }

                // (11) Unhook from the static events, otherwise we cannot be garbage collected
                KryptonManager.GlobalPaletteChanged -= new EventHandler(OnGlobalPaletteChanged);
            }

            if (disposing && (components != null))
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Overrides the paint event
        /// </summary>
        /// <param name="e">PaintEventArgs</param>
        protected override void OnPaint(PaintEventArgs e)
        {
            int nextPosition = (int)(5 * factorX);
            if (_palette != null)
            {
                // (3) Get the renderer associated with this palette
                IRenderer renderer = _palette.GetRenderer();

                // (4) Create the rendering context that is passed into all renderer calls
                using (RenderContext renderContext = new RenderContext(this, e.Graphics, e.ClipRectangle, renderer))
                {
                    _paletteBack.Style = PaletteBackStyle.PanelClient;
                    _paletteBorder.Style = PaletteBorderStyle.HeaderPrimary;
                    using (GraphicsPath path = renderer.RenderStandardBorder.GetBackPath(renderContext, e.ClipRectangle, _paletteBorder, VisualOrientation.Top, PaletteState.Normal))
                    {
                        _mementoBack = renderer.RenderStandardBack.DrawBack(renderContext,
                            ClientRectangle,
                            path,
                            _paletteBack,
                            VisualOrientation.Top,
                            PaletteState.Normal,
                            _mementoBack);
                    }
                    renderer.RenderStandardBorder.DrawBorder(renderContext, ClientRectangle, _border, VisualOrientation.Top, PaletteState.Normal);

                    //If no grouped columns, draw to the indicating text
                    if (columnsList.Count == 0)
                    {
                        TextRenderer.DrawText(e.Graphics, _dragColumnToGroupText , _palette.GetContentShortTextFont(PaletteContentStyle.LabelNormalPanel, PaletteState.Normal), e.ClipRectangle, _palette.GetContentShortTextColor1(PaletteContentStyle.LabelNormalPanel, PaletteState.Normal),
                            TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.PreserveGraphicsClipping);
                    }

                    PaletteState state;
                    _paletteBack.Style = PaletteBackStyle.GridHeaderColumnList;
                    _paletteBorder.Style = PaletteBorderStyle.GridHeaderColumnList;
                    // PaintGroupBox(e.Graphics, e.ClipRectangle, this.Font, "Drag a column here to group", columnsList.Count > 0);
                   


                    //Draw the column boxes
                    foreach (OutlookGridGroupBoxColumn current in this.columnsList)
                    {
                        Rectangle rectangle = default(Rectangle);
                        rectangle.Width = (int)(100 * factorX) ;
                        rectangle.X = nextPosition;
                        rectangle.Y = (e.ClipRectangle.Height - (int)(25 * factorY)) / 2;
                        rectangle.Height = (int)(25 * factorY);
                        nextPosition += (int)(105 * factorX); //next position
                        current.Rect = rectangle;

                        if (current.IsHovered)
                            state = PaletteState.Tracking;
                        else if (current.Pressed)
                            state = PaletteState.Pressed;
                        else
                            state = PaletteState.Normal;
                        // Do we need to draw the background?
                        if (_paletteBack.GetBackDraw(PaletteState.Normal) == InheritBool.True)
                        {
                            //Back
                            using (GraphicsPath path = renderer.RenderStandardBorder.GetBackPath(renderContext, rectangle, _paletteBorder, VisualOrientation.Top, PaletteState.Normal))
                            {
                                _mementoBack = renderer.RenderStandardBack.DrawBack(renderContext,
                                    rectangle,
                                    path,
                                    _paletteBack,
                                    VisualOrientation.Top,
                                    state,
                                    _mementoBack);
                            }

                            //Border
                            renderer.RenderStandardBorder.DrawBorder(renderContext, rectangle, _paletteBorder, VisualOrientation.Top, state);

                            //Text
                            TextRenderer.DrawText(e.Graphics, current.Text, _palette.GetContentShortTextFont(PaletteContentStyle.GridHeaderColumnList, state), rectangle, _palette.GetContentShortTextColor1(PaletteContentStyle.GridHeaderColumnList, state),
                                TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.PreserveGraphicsClipping);

                            //Sort Glyph
                            renderer.RenderGlyph.DrawGridSortGlyph(renderContext, current.SortDirection, rectangle, _paletteDataGridViewAll.HeaderColumn.Content, state, false);
                        }

                        //Draw the column box while it is moving
                        if (current.IsMoving)
                        {
                            Rectangle rectangle1 = new Rectangle(_mouse.X, _mouse.Y, current.Rect.Width, current.Rect.Height);
                            //this.Renderer.PaintMovingColumn(graphics, this.currentDragColumn, rectangle1);
                            using (SolidBrush solidBrush = new SolidBrush(Color.FromArgb(70, Color.Gray)))
                            {
                                e.Graphics.FillRectangle(solidBrush, rectangle1);
                            }

                            TextRenderer.DrawText(e.Graphics, current.Text, _palette.GetContentShortTextFont(PaletteContentStyle.GridHeaderColumnList, PaletteState.Disabled), rectangle1, _palette.GetContentShortTextColor1(PaletteContentStyle.GridHeaderColumnList, PaletteState.Disabled),
                                TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.PreserveGraphicsClipping);
                        }
                    }
                }
            }

            base.OnPaint(e);
        }

        /// <summary>
        /// Overrides the MouseDown event.
        /// </summary>
        /// <param name="e">A MouseEventArgs that contains the event data.</param>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            foreach (OutlookGridGroupBoxColumn c in this.columnsList)
            {
                if (c.Rect != null)
                {
                    if (c.Rect.Contains(e.X, e.Y) && (e.Button == System.Windows.Forms.MouseButtons.Left))
                    {
                        c.Pressed = true;
                    }
                }
            }
            this.Invalidate();
            base.OnMouseDown(e);
        }

        /// <summary>
        /// Overrides the MouseUp event.
        /// </summary>
        /// <param name="e">A MouseEventArgs that contains the event data.</param>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            List<OutlookGridGroupBoxColumn> l = new List<OutlookGridGroupBoxColumn>();
            OutlookGridGroupBoxColumn columnMovingInsideGroupBox = null;

            foreach (OutlookGridGroupBoxColumn c in this.columnsList)
            {
                if (c.Rect != null)
                {
                    if (c.IsMoving && !this.Bounds.Contains(e.Location))
                    {
                        l.Add(c);
                    }
                    //We move an existing colum inside the groupbox
                    else if (c.IsMoving && this.Bounds.Contains(e.Location))
                    {
                        columnMovingInsideGroupBox = c;
                    }

                    //Stop moving and pressing
                    c.Pressed = false;
                    c.IsMoving = false;
                }
            }

            //no more dragging
            isDragging = false;

            //Ungroup columns dragged outside the box
            if (l.Count > 0)
            {
                foreach (OutlookGridGroupBoxColumn c in l)
                {
                    //Warn the Grid
                    OnColumnGroupRemoved(new OutlookGridColumnEventArgs(new OutlookGridColumn(c.ColumnName, null, null, SortOrder.None, -1, -1)));

                    columnsList.Remove(c);
                }
            }


            if (columnMovingInsideGroupBox != null)
            {
                if (e.Location.X != columnMovingInsideGroupBox.Rect.X && (e.Location.X < columnMovingInsideGroupBox.Rect.X || e.Location.X > (columnMovingInsideGroupBox.Rect.X + columnMovingInsideGroupBox.Rect.Width)))
                {
                    int i = 0; //first group order is 0

                    foreach (OutlookGridGroupBoxColumn existingColumns in this.columnsList)
                    {
                        if (e.Location.X > (existingColumns.Rect.X + existingColumns.Rect.Width / 2) && existingColumns != columnMovingInsideGroupBox)
                        {
                            i++;
                        }
                    }
                    OnColumnGroupOrderChanged(new OutlookGridColumnEventArgs(new OutlookGridColumn(columnMovingInsideGroupBox.ColumnName, null, null, SortOrder.None, i, -1)));
                    //MessageBox.Show("Changed order = " + i.ToString());
                }
            }

            this.Invalidate();
            base.OnMouseDown(e);
        }

        /// <summary>
        /// Overrides the MouseClick event.
        /// </summary>
        /// <param name="e">A MouseEventArgs that contains the event data.</param>
        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                indexselected = -1;
                for (int i = 0; i < this.columnsList.Count; i++)
                {
                    if (columnsList[i].Rect != null && columnsList[i].Rect.Contains(e.X, e.Y))
                    {
                        indexselected = i;
                    }
                }
                ShowColumnBoxContextMenu();
            }
            else if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                //On MouseClick is before OnMouseUp, so if it is moving, don't click...
                bool somethingIsMoving = false;
                foreach (OutlookGridGroupBoxColumn c in this.columnsList)
                {
                    if (c.IsMoving)
                        somethingIsMoving = true;
                }

                if (!somethingIsMoving)
                {
                    foreach (OutlookGridGroupBoxColumn c in this.columnsList)
                    {
                        if (c.Rect != null && c.Rect.Contains(e.X, e.Y))
                        {
                            c.SortDirection = c.SortDirection == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
                            //Warn the Grid
                            OnColumnSortChanged(new OutlookGridColumnEventArgs(new OutlookGridColumn(c.ColumnName, null, null, c.SortDirection, -1, -1)));
                        }
                    }
                }
            }

            base.OnMouseClick(e);
        }

        /// <summary>
        /// Overrides the MouseMove event.
        /// </summary>
        /// <param name="e">A MouseEventArgs that contains the event data.</param>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _mouse = e.Location;
            foreach (OutlookGridGroupBoxColumn c in this.columnsList)
            {
                if (c.Rect != null)
                {
                    //Update hovering
                    c.IsHovered = c.Rect.Contains(e.X, e.Y);

                    //declare dragging
                    if (c.Rect.Contains(e.X, e.Y) && (e.Button == System.Windows.Forms.MouseButtons.Left) && !isDragging)
                    {
                        isDragging = true;
                        c.IsMoving = true;
                        //Console.WriteLine(_mouse.ToString());
                    }
                }
            }
            Invalidate();
        }

        #endregion

        #region Events

        /// <summary>
        /// Handles OnPalettePaint Event
        /// </summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">A PaletteLayoutEventArgs that contains the event data.</param>
        private void OnPalettePaint(object sender, PaletteLayoutEventArgs e)
        {
            Invalidate();
        }

        /// <summary>
        /// Handles OnGlobalPaletteChanged event
        /// </summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">A EventArgs that contains the event data.</param>
        private void OnGlobalPaletteChanged(object sender, EventArgs e)
        {
            // (5) Unhook events from old palette
            if (_palette != null)
                _palette.PalettePaint -= new EventHandler<PaletteLayoutEventArgs>(OnPalettePaint);

            // (6) Cache the new IPalette that is the global palette
            _palette = KryptonManager.CurrentGlobalPalette;
            _paletteRedirect.Target = _palette; //!!!!!!

            // (7) Hook into events for the new palette
            if (_palette != null)
                _palette.PalettePaint += new EventHandler<PaletteLayoutEventArgs>(OnPalettePaint);

            // (8) Change of palette means we should repaint to show any changes
            Invalidate();
        }

        /// <summary>
        /// Raises the ColumnSortChanged event.
        /// </summary>
        /// <param name="e">A OutlookGridColumnEventArgs that contains the event data.</param>
        protected virtual void OnColumnSortChanged(OutlookGridColumnEventArgs e)
        {
            if (ColumnSortChanged != null)
                ColumnSortChanged(this, e);
        }

        /// <summary>
        /// Raises the ColumnGroupAdded event.
        /// </summary>
        /// <param name="e">A OutlookGridColumnEventArgs that contains the event data.</param>
        protected virtual void OnColumnGroupAdded(OutlookGridColumnEventArgs e)
        {
            if (ColumnGroupAdded != null)
                ColumnGroupAdded(this, e);
        }

        /// <summary>
        /// Raises the ColumnGroupRemoved event.
        /// </summary>
        /// <param name="e">A OutlookGridColumnEventArgs that contains the event data.</param>
        protected virtual void OnColumnGroupRemoved(OutlookGridColumnEventArgs e)
        {
            if (ColumnGroupRemoved != null)
                ColumnGroupRemoved(this, e);
        }

        /// <summary>
        /// Raises the ColumnGroupOrderChanged event.
        /// </summary>
        /// <param name="e">A OutlookGridColumnEventArgs that contains the event data.</param>
        protected virtual void OnColumnGroupOrderChanged(OutlookGridColumnEventArgs e)
        {
            if (ColumnGroupOrderChanged != null)
                ColumnGroupOrderChanged(this, e);
        }

        /// <summary>
        /// Raises the ClearGrouping event.
        /// </summary>
        /// <param name="e">A EventArgs that contains the event data.</param>
        protected virtual void OnClearGrouping(EventArgs e)
        {
            if (ClearGrouping != null)
                ClearGrouping(this, e);
        }

        /// <summary>
        /// Raises the FullExpand event.
        /// </summary>
        /// <param name="e">A EventArgs that contains the event data.</param>
        protected virtual void OnFullExpand(EventArgs e)
        {
            if (FullExpand != null)
                FullExpand(this, e);
        }

        /// <summary>
        /// Raises the FullCollapse event.
        /// </summary>
        /// <param name="e">A EventArgs that contains the event data.</param>
        protected virtual void OnFullCollapse(EventArgs e)
        {
            if (FullCollapse != null)
                FullCollapse(this, e);
        }

        /// <summary>
        /// Raises the Group Expand event.
        /// </summary>
        /// <param name="e">A EventArgs that contains the event data.</param>
        protected virtual void OnGroupExpand(OutlookGridColumnEventArgs e)
        {
            if (GroupExpand != null)
                GroupExpand(this, e);
        }

        /// <summary>
        /// Raises the Group Collapse event.
        /// </summary>
        /// <param name="e">A EventArgs that contains the event data.</param>
        protected virtual void OnGroupCollapse(OutlookGridColumnEventArgs e)
        {
            if (GroupCollapse != null)
                GroupCollapse(this, e);
        }

        /// <summary>
        /// Raises the GroupIntervalClick event.
        /// </summary>
        /// <param name="e">A EventArgs that contains the event data.</param>
        private void OnGroupIntervalClick(OutlookGridColumnEventArgs e)
        {
            if (GroupIntervalClick != null)
                GroupIntervalClick(this, e);
        }

        /// <summary>
        /// Raises the OnSortBySummaryCount event.
        /// </summary>
        /// <param name="e">A EventArgs that contains the event data.</param>
        private void OnSortBySummaryCount(OutlookGridColumnEventArgs e)
        {
            if (SortBySummaryCount != null)
                SortBySummaryCount(this, e);
        }

        /// <summary>
        /// Handles the HideGroupBox event
        /// </summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">A EventArgs that contains the event data.</param>
        private void OnHideGroupBox(object sender, EventArgs e)
        {
            this.Hide();
        }

        /// <summary>
        /// Handles the ClearGrouping event
        /// </summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">A EventArgs that contains the event data.</param>
        private void OnClearGrouping(object sender, EventArgs e)
        {
            OnClearGrouping(new EventArgs());
            columnsList.Clear();
            this.Invalidate();
        }

        /// <summary>
        /// Handles the FullCollapse event
        /// </summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">A EventArgs that contains the event data.</param>
        private void OnFullCollapse(object sender, EventArgs e)
        {
            OnFullCollapse(new EventArgs());
        }

        /// <summary>
        /// Handles the FullExpand event
        /// </summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">A EventArgs that contains the event data.</param>
        private void OnFullExpand(object sender, EventArgs e)
        {
            OnFullExpand(new EventArgs());
        }

        /// <summary>
        /// Handles the GroupCollapse event
        /// </summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">A OutlookGridColumnEventArgs that contains the event data.</param>
        private void OnGroupCollapse(object sender, EventArgs e)
        {
            OnGroupCollapse(new OutlookGridColumnEventArgs(new OutlookGridColumn(columnsList[indexselected].ColumnName, null, null, SortOrder.None, -1, -1)));
        }

        /// <summary>
        /// Handles the GroupExpand event
        /// </summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">A OutlookGridColumnEventArgs that contains the event data.</param>
        private void OnGroupExpand(object sender, EventArgs e)
        {
            OnGroupExpand(new OutlookGridColumnEventArgs(new OutlookGridColumn(columnsList[indexselected].ColumnName, null, null, SortOrder.None, -1, -1)));
        }

        /// <summary>
        /// Handles the SortAscending event
        /// </summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">A EventArgs that contains the event data.</param>
        private void OnSortAscending(object sender, EventArgs e)
        {
            //Change the sortOrder in the list
            OutlookGridGroupBoxColumn col = columnsList[indexselected];
            col.SortDirection = SortOrder.Ascending;
            //Raise event
            OnColumnSortChanged(new OutlookGridColumnEventArgs(new OutlookGridColumn(col.ColumnName, null, null, SortOrder.Ascending, -1, -1)));
            //Redraw
            this.Invalidate();
        }

        /// <summary>
        /// Handles the SortDescending event
        /// </summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">A EventArgs that contains the event data.</param>
        private void OnSortDescending(object sender, EventArgs e)
        {
            //Change the sortOrder in the list
            OutlookGridGroupBoxColumn col = columnsList[indexselected];
            col.SortDirection = SortOrder.Descending;
            //Raise event
            OnColumnSortChanged(new OutlookGridColumnEventArgs(new OutlookGridColumn(col.ColumnName, null, null, SortOrder.Descending, -1, -1)));
            //Redraw
            this.Invalidate();
        }

        /// <summary>
        /// Handles the UnGroup event
        /// </summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">A EventArgs that contains the event data.</param>
        private void OnUngroup(object sender, EventArgs e)
        {
            OutlookGridGroupBoxColumn col = columnsList[indexselected];
            OnColumnGroupRemoved(new OutlookGridColumnEventArgs(new OutlookGridColumn(col.ColumnName, null, null, SortOrder.None, -1, -1)));
            columnsList.Remove(col);
            this.Invalidate();
        }

        /// <summary>
        /// Handles the GroupIntervalClick event
        /// </summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">A EventArgs that contains the event data.</param>
        private void OnGroupIntervalClick(object sender, EventArgs e)
        {
            KryptonContextMenuItem item = (KryptonContextMenuItem)sender;
            OutlookGridGroupBoxColumn col = columnsList[indexselected];
            OutlookGridColumn colEvent = new OutlookGridColumn(col.ColumnName, null, null, SortOrder.None, -1, -1);
            colEvent.GroupingType = new OutlookGridDateTimeGroup(null) { Interval = ((OutlookGridDateTimeGroup.DateInterval)Enum.Parse(typeof(OutlookGridDateTimeGroup.DateInterval), item.Tag.ToString())) };
            col.GroupInterval = ((OutlookGridDateTimeGroup)colEvent.GroupingType).Interval.ToString();
            //Raise event
            OnGroupIntervalClick(new OutlookGridColumnEventArgs(colEvent));
        }

        /// <summary>
        /// Handles the OnSortBySummaryCount event
        /// </summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">A EventArgs that contains the event data.</param>
        private void OnSortBySummaryCount(object sender, EventArgs e)
        {
            KryptonContextMenuItem item = (KryptonContextMenuItem)sender;
            OutlookGridGroupBoxColumn col = columnsList[indexselected];
            OutlookGridColumn colEvent = new OutlookGridColumn(col.ColumnName, null, null, SortOrder.None, -1, -1);
            colEvent.GroupingType = new OutlookGridDefaultGroup(null) { SortBySummaryCount = item.Checked };
            col.SortBySummaryCount = item.Checked;
            //Raise event
            OnSortBySummaryCount(new OutlookGridColumnEventArgs(colEvent));
        }

        /// <summary>
        /// Handles the DragDrop event. Add a new grouping column following a drag drop from the grid
        /// </summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">A DragEventArgs that contains the event data.</param>
        private void KryptonOutlookGridGroupBox_DragDrop(object sender, DragEventArgs e)
        {
            string columnToMove = e.Data.GetData(typeof(string)) as string;
            string columnName;
            string columnText;
            SortOrder sortOrder;
            DataGridViewColumnSortMode sortMode;
            string[] res = columnToMove.Split('|');
            columnName = res[0];
            columnText = res[1];
            sortOrder = (SortOrder)Enum.Parse(typeof(SortOrder), res[2]);//SortOrder.Ascending;
            if (sortOrder == SortOrder.None)
                sortOrder = SortOrder.Ascending;
            sortMode = (DataGridViewColumnSortMode)Enum.Parse(typeof(DataGridViewColumnSortMode), res[3]);
            OutlookGridGroupBoxColumn colToAdd = new OutlookGridGroupBoxColumn(columnName, columnText, sortOrder, res[4]);
            //if (res[4] == typeof(OutlookGridDateTimeGroup).Name)
            colToAdd.GroupInterval = res[5];
            colToAdd.SortBySummaryCount = CommonHelper.StringToBool(res[6]);
            if (!String.IsNullOrEmpty(columnToMove) && !columnsList.Contains(colToAdd) && sortMode != DataGridViewColumnSortMode.NotSortable)
            {
                //Determine the position of the new Group amongst the others 
                int i = 0; //first group order is 0
                //We are sure that we are going to browse the columns from left to right
                foreach (OutlookGridGroupBoxColumn existingColumn in this.columnsList)
                {
                    if (e.X > (existingColumn.Rect.X + existingColumn.Rect.Width / 2))
                    {
                        i++;
                    }
                }
                columnsList.Insert(i, colToAdd);

                //try
                //{
                    //Warns the grid of a new grouping
                    OnColumnGroupAdded(new OutlookGridColumnEventArgs(new OutlookGridColumn(columnName, null, null, sortOrder, i, -1)));
                    this.Invalidate();
                //}
                //catch (Exception exc)
                //{
                //    MessageBox.Show("Failed to group.\n\n Error:" + exc.Message,
                //                      "Grid GroupBox",
                //                      MessageBoxButtons.OK,
                //                      MessageBoxIcon.Error);
                //}
            }
        }

        /// <summary>
        /// Hnadles the DragEnter event.
        /// </summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="e">A DragEventArgs that contains the event data.</param>
        private void KryptonOutlookGridGroupBox_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Show the context menu for column box
        /// </summary>
        private void ShowColumnBoxContextMenu()
        {
            if (_menuItems == null)
            {
                // Create individual items
                _menuSortAscending = new KryptonContextMenuItem(LangManager.Instance.GetString("SORTASCENDING"), Properties.Resources.sort_ascending, new EventHandler(OnSortAscending));
                _menuSortDescending = new KryptonContextMenuItem(LangManager.Instance.GetString("SORTDESCENDING"), Properties.Resources.sort_descending, new EventHandler(OnSortDescending));
                _menuSeparator1 = new KryptonContextMenuSeparator();
                _menuExpand = new KryptonContextMenuItem(LangManager.Instance.GetString("EXPAND"), Properties.Resources.element_plus_16, new EventHandler(OnGroupExpand)); 
                _menuCollapse = new KryptonContextMenuItem(LangManager.Instance.GetString("COLLAPSE"), Properties.Resources.element_minus_16, new EventHandler(OnGroupCollapse));
                _menuUnGroup = new KryptonContextMenuItem(LangManager.Instance.GetString("UNGROUP"), Properties.Resources.element_delete, new EventHandler(OnUngroup));
                _menuSeparator2 = new KryptonContextMenuSeparator();
                _menuFullExpand = new KryptonContextMenuItem(LangManager.Instance.GetString("FULLEXPAND"), Properties.Resources.elements_plus_16, new EventHandler(OnFullExpand));
                _menuFullCollapse = new KryptonContextMenuItem(LangManager.Instance.GetString("FULLCOLLAPSE"), Properties.Resources.elements_minus_16, new EventHandler(OnFullCollapse));
                _menuSeparator3 = new KryptonContextMenuSeparator();
                _menuClearGrouping = new KryptonContextMenuItem(LangManager.Instance.GetString("CLEARGROUPING"), Properties.Resources.element_selection_delete, new EventHandler(OnClearGrouping));
                _menuHideGroupBox = new KryptonContextMenuItem(LangManager.Instance.GetString("HIDEGROUPBOX"), null, new EventHandler(OnHideGroupBox));
                _menuGroupInterval = new KryptonContextMenuItem(LangManager.Instance.GetString("GROUPINTERVAL"));
                _menuSortBySummary = new KryptonContextMenuItem(LangManager.Instance.GetString("SORTBYSUMMARYCOUNT"), null, new EventHandler(OnSortBySummaryCount));
                _menuSortBySummary.CheckOnClick = true;

                //Group Interval
                KryptonContextMenuItems _GroupIntervalItems;
                KryptonContextMenuItem it = null;
                string[] names = Enum.GetNames(typeof(OutlookGridDateTimeGroup.DateInterval));
                KryptonContextMenuItemBase[] arrayOptions = new KryptonContextMenuItemBase[names.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    it = new KryptonContextMenuItem(LangManager.Instance.GetString(names[i]));
                    it.Tag = names[i];
                    it.Click += OnGroupIntervalClick;
                    arrayOptions[i] = it;
                }
                _GroupIntervalItems = new KryptonContextMenuItems(arrayOptions);
                _menuGroupInterval.Items.Add(_GroupIntervalItems);

                // Add items inside an items collection (apart from separator1 which is only added if required)
                _menuItems = new KryptonContextMenuItems(new KryptonContextMenuItemBase[] { _menuSortAscending,
                                                                                            _menuSortDescending,
                                                                                            _menuSortBySummary,
                                                                                            _menuSeparator1,
                                                                                            _menuGroupInterval,
                                                                                            _menuExpand, 
                                                                                            _menuCollapse,
                                                                                            _menuUnGroup,
                                                                                            _menuSeparator2,
                                                                                            _menuFullExpand,
                                                                                            _menuFullCollapse,
                                                                                            _menuSeparator3,
                                                                                            _menuClearGrouping,
                                                                                            _menuHideGroupBox
                                                                                          });
            }

            // Ensure we have a krypton context menu if not already present
            if (this.KCtxMenu == null)
                KCtxMenu = new KryptonContextMenu();


            // Update the individual menu options
            OutlookGridGroupBoxColumn col = null;
            if (indexselected > -1)
                col = columnsList[indexselected];

            _menuSortAscending.Visible = col != null;
            _menuSortDescending.Visible = col != null;
            _menuSortAscending.Checked = col != null && col.SortDirection == SortOrder.Ascending;
            _menuSortDescending.Checked = col != null && col.SortDirection == SortOrder.Descending;
            _menuSortBySummary.Visible = col != null;
            _menuSortBySummary.Checked = col != null && col.SortBySummaryCount;
            _menuExpand.Visible = col != null;
            _menuCollapse.Visible = col != null;
            _menuGroupInterval.Visible = col != null && col.GroupingType == typeof(OutlookGridDateTimeGroup).Name;
            if (_menuGroupInterval.Visible)
            {
                foreach (KryptonContextMenuItem item in ((KryptonContextMenuItems)_menuGroupInterval.Items[0]).Items)
                {
                    item.Checked = item.Tag.ToString() == col.GroupInterval;
                }
            }
            _menuUnGroup.Visible = col != null;
            _menuFullExpand.Enabled = columnsList.Count > 0;
            _menuFullCollapse.Enabled = columnsList.Count > 0;
            _menuClearGrouping.Enabled = columnsList.Count > 0;

            _menuSeparator1.Visible = (_menuSortAscending.Visible || _menuSortDescending.Visible);
            _menuSeparator2.Visible = (_menuExpand.Visible || _menuCollapse.Visible || _menuUnGroup.Visible);
            _menuSeparator3.Visible = (_menuFullExpand.Visible || _menuFullCollapse.Visible);

            if (!KCtxMenu.Items.Contains(_menuItems))
                KCtxMenu.Items.Add(_menuItems);

            // Show the menu!
            KCtxMenu.Show(this);
        }

        /// <summary>
        /// DO NOT USE THIS FUNCTION YOURSELF, USE the corresponding function in OutlookGrid
        /// Update the grouping columns.
        /// </summary>
        /// <param name="list">The list of OutlookGridColumn</param>
        public void UpdateGroupingColumns(List<OutlookGridColumn> list)
        {
            columnsList.Clear();
            OutlookGridGroupBoxColumn colToAdd;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].IsGrouped)
                {
                    colToAdd = new OutlookGridGroupBoxColumn(list[i].DataGridViewColumn.Name, list[i].DataGridViewColumn.HeaderText, list[i].SortDirection, list[i].GroupingType.GetType().Name);
                    if (colToAdd.GroupingType == typeof(OutlookGridDateTimeGroup).Name)
                        colToAdd.GroupInterval = ((OutlookGridDateTimeGroup)list[i].GroupingType).Interval.ToString();
                    columnsList.Add(colToAdd);
                }
            }
            this.Invalidate();
        }


        /// <summary>
        /// Checks if the column exists in the GroupBox control
        /// </summary>
        /// <param name="columnName">The column name.</param>
        /// <returns>True if exists, otherwise false.</returns>
        public bool Contains(string columnName)
        {
            for (int i = 0; i < columnsList.Count; i++)
            {
                if (columnsList[i].ColumnName == columnName)
                    return true;
            }
            return false;
        }

        #endregion
    }
}

