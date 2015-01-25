// Copyright (c) 2007-2012  Michael Chapman
// http://ipaddresscontrollib.googlecode.com

// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:

// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
// CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
// TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
// SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.


using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using System.Windows.Forms.Design.Behavior;
using System.Windows.Forms.VisualStyles;

namespace TCPUtils
{
    namespace IPAddressControlBox
    {
        [DesignerAttribute(typeof(IPAddressControlDesigner))]
        public class IPAddressControl : ContainerControl
        {
            #region Public Constants

            public const int FieldCount = 4;

            #endregion // Public Constants

            #region Public Events

            public event EventHandler<FieldChangedEventArgs> FieldChangedEvent;

            #endregion //Public Events

            #region Public Properties

            [Browsable(true)]
            public bool AllowInternalTab
            {
                get
                {
                    foreach (FieldControl fc in _fieldControls)
                    {
                        return fc.TabStop;
                    }

                    return false;
                }
                set
                {
                    foreach (FieldControl fc in _fieldControls)
                    {
                        fc.TabStop = value;
                    }
                }
            }

            [Browsable(true)]
            public bool AnyBlank
            {
                get
                {
                    foreach (FieldControl fc in _fieldControls)
                    {
                        if (fc.Blank)
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }

            [Browsable(true)]
            public bool AutoHeight
            {
                get { return _autoHeight; }
                set
                {
                    _autoHeight = value;

                    if (_autoHeight)
                    {
                        AdjustSize();
                    }
                }
            }

            [Browsable(false)]
            public int Baseline
            {
                get
                {
                    NativeMethods.TEXTMETRIC textMetric = GetTextMetrics(Handle, Font);

                    int offset = textMetric.tmAscent + 1;

                    switch (BorderStyle)
                    {
                        case BorderStyle.Fixed3D:
                            offset += Fixed3DOffset.Height;
                            break;
                        case BorderStyle.FixedSingle:
                            offset += FixedSingleOffset.Height;
                            break;
                    }

                    return offset;
                }
            }

            [Browsable(true)]
            public bool Blank
            {
                get
                {
                    foreach (FieldControl fc in _fieldControls)
                    {
                        if (!fc.Blank)
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            [Browsable(true)]
            public BorderStyle BorderStyle
            {
                get { return _borderStyle; }
                set
                {
                    _borderStyle = value;
                    AdjustSize();
                    Invalidate();
                }
            }

            [Browsable(false)]
            public override bool Focused
            {
                get
                {
                    foreach (FieldControl fc in _fieldControls)
                    {
                        if (fc.Focused)
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }

            [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            public IPAddress IPAddress
            {
                get { return new IPAddress(GetAddressBytes()); }
                set
                {
                    Clear();

                    if (null == value) { return; }

                    if (value.AddressFamily == AddressFamily.InterNetwork)
                    {
                        SetAddressBytes(value.GetAddressBytes());
                    }
                }
            }

            [Browsable(true)]
            public override Size MinimumSize
            {
                get { return CalculateMinimumSize(); }
            }

            [Browsable(true)]
            public bool ReadOnly
            {
                get { return _readOnly; }
                set
                {
                    _readOnly = value;

                    foreach (FieldControl fc in _fieldControls)
                    {
                        fc.ReadOnly = _readOnly;
                    }

                    foreach (DotControl dc in _dotControls)
                    {
                        dc.ReadOnly = _readOnly;
                    }

                    Invalidate();
                }
            }

            [Bindable(true)]
            [Browsable(true)]
            [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
            public override string Text
            {
                get
                {
                    StringBuilder sb = new StringBuilder(); ;

                    for (int index = 0; index < _fieldControls.Length; ++index)
                    {
                        sb.Append(_fieldControls[index].Text);

                        if (index < _dotControls.Length)
                        {
                            sb.Append(_dotControls[index].Text);
                        }
                    }

                    return sb.ToString();
                }
                set
                {
                    Parse(value);
                }
            }

            #endregion // Public Properties

            #region Public Methods

            public void Clear()
            {
                foreach (FieldControl fc in _fieldControls)
                {
                    fc.Clear();
                }
            }

            public byte[] GetAddressBytes()
            {
                byte[] bytes = new byte[FieldCount];

                for (int index = 0; index < FieldCount; ++index)
                {
                    bytes[index] = _fieldControls[index].Value;
                }

                return bytes;
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", Justification = "Using Bytes seems more informative than SetAddressValues.")]
            public void SetAddressBytes(byte[] bytes)
            {
                Clear();

                if (bytes == null)
                {
                    return;
                }

                int length = Math.Min(FieldCount, bytes.Length);

                for (int i = 0; i < length; ++i)
                {
                    _fieldControls[i].Text = bytes[i].ToString(CultureInfo.InvariantCulture);
                }
            }

            public void SetFieldFocus(int fieldIndex)
            {
                if ((fieldIndex >= 0) && (fieldIndex < FieldCount))
                {
                    _fieldControls[fieldIndex].TakeFocus(Direction.Forward, Selection.All);
                }
            }

            public void SetFieldRange(int fieldIndex, byte rangeLower, byte rangeUpper)
            {
                if ((fieldIndex >= 0) && (fieldIndex < FieldCount))
                {
                    _fieldControls[fieldIndex].RangeLower = rangeLower;
                    _fieldControls[fieldIndex].RangeUpper = rangeUpper;
                }
            }

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();

                for (int index = 0; index < FieldCount; ++index)
                {
                    sb.Append(_fieldControls[index].ToString());

                    if (index < _dotControls.Length)
                    {
                        sb.Append(_dotControls[index].ToString());
                    }
                }

                return sb.ToString();
            }

            #endregion Public Methods

            #region Constructors

            public IPAddressControl()
            {
                BackColor = SystemColors.Window;

                ResetBackColorChanged();

                for (int index = 0; index < _fieldControls.Length; ++index)
                {
                    _fieldControls[index] = new FieldControl();

                    _fieldControls[index].CreateControl();

                    _fieldControls[index].FieldIndex = index;
                    _fieldControls[index].Name = "FieldControl" + index.ToString(CultureInfo.InvariantCulture);
                    _fieldControls[index].Parent = this;

                    _fieldControls[index].CedeFocusEvent += new EventHandler<CedeFocusEventArgs>(OnCedeFocus);
                    _fieldControls[index].Click += new EventHandler(OnSubControlClicked);
                    _fieldControls[index].DoubleClick += new EventHandler(OnSubControlDoubleClicked);
                    _fieldControls[index].GotFocus += new EventHandler(OnFieldGotFocus);
                    _fieldControls[index].KeyDown += new KeyEventHandler(OnFieldKeyDown);
                    _fieldControls[index].KeyPress += new KeyPressEventHandler(OnFieldKeyPressed);
                    _fieldControls[index].KeyUp += new KeyEventHandler(OnFieldKeyUp);
                    _fieldControls[index].LostFocus += new EventHandler(OnFieldLostFocus);
                    _fieldControls[index].MouseClick += new MouseEventHandler(OnSubControlMouseClicked);
                    _fieldControls[index].MouseDoubleClick += new MouseEventHandler(OnSubControlMouseDoubleClicked);
                    _fieldControls[index].MouseEnter += new EventHandler(OnSubControlMouseEntered);
                    _fieldControls[index].MouseHover += new EventHandler(OnSubControlMouseHovered);
                    _fieldControls[index].MouseLeave += new EventHandler(OnSubControlMouseLeft);
                    _fieldControls[index].MouseMove += new MouseEventHandler(OnSubControlMouseMoved);
                    _fieldControls[index].PreviewKeyDown += new PreviewKeyDownEventHandler(OnFieldPreviewKeyDown);
                    _fieldControls[index].TextChangedEvent += new EventHandler<TextChangedEventArgs>(OnFieldTextChanged);

                    Controls.Add(_fieldControls[index]);

                    if (index < (FieldCount - 1))
                    {
                        _dotControls[index] = new DotControl();

                        _dotControls[index].CreateControl();

                        _dotControls[index].Name = "DotControl" + index.ToString(CultureInfo.InvariantCulture);
                        _dotControls[index].Parent = this;

                        _dotControls[index].Click += new EventHandler(OnSubControlClicked);
                        _dotControls[index].DoubleClick += new EventHandler(OnSubControlDoubleClicked);
                        _dotControls[index].MouseClick += new MouseEventHandler(OnSubControlMouseClicked);
                        _dotControls[index].MouseDoubleClick += new MouseEventHandler(OnSubControlMouseDoubleClicked);
                        _dotControls[index].MouseEnter += new EventHandler(OnSubControlMouseEntered);
                        _dotControls[index].MouseHover += new EventHandler(OnSubControlMouseHovered);
                        _dotControls[index].MouseLeave += new EventHandler(OnSubControlMouseLeft);
                        _dotControls[index].MouseMove += new MouseEventHandler(OnSubControlMouseMoved);

                        Controls.Add(_dotControls[index]);
                    }
                }

                SetStyle(ControlStyles.AllPaintingInWmPaint, true);
                SetStyle(ControlStyles.ContainerControl, true);
                SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
                SetStyle(ControlStyles.ResizeRedraw, true);
                SetStyle(ControlStyles.UserPaint, true);
                SetStyle(ControlStyles.FixedWidth, true);
                SetStyle(ControlStyles.FixedHeight, true);

                _referenceTextBox.AutoSize = true;

                Cursor = Cursors.IBeam;

                AutoScaleDimensions = new SizeF(96F, 96F);
                AutoScaleMode = AutoScaleMode.Dpi;

                Size = MinimumSize;

                DragEnter += new DragEventHandler(IPAddressControl_DragEnter);
                DragDrop += new DragEventHandler(IPAddressControl_DragDrop);
            }

            #endregion // Constructors

            #region Protected Methods

            protected override void Dispose(bool disposing)
            {
                if (disposing) { Cleanup(); }
                base.Dispose(disposing);
            }

            protected override void OnBackColorChanged(EventArgs e)
            {
                base.OnBackColorChanged(e);
                _backColorChanged = true;
            }

            protected override void OnFontChanged(EventArgs e)
            {
                base.OnFontChanged(e);
                AdjustSize();
            }

            protected override void OnGotFocus(EventArgs e)
            {
                base.OnGotFocus(e);
                _focused = true;
                _fieldControls[0].TakeFocus(Direction.Forward, Selection.All);
            }

            protected override void OnLostFocus(EventArgs e)
            {
                if (!Focused)
                {
                    _focused = false;
                    base.OnLostFocus(e);
                }
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                if (!_hasMouse)
                {
                    _hasMouse = true;
                    base.OnMouseEnter(e);
                }
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                if (!HasMouse)
                {
                    base.OnMouseLeave(e);
                    _hasMouse = false;
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (null == e) { throw new ArgumentNullException("e"); }

                base.OnPaint(e);

                Color backColor = BackColor;

                if (!_backColorChanged)
                {
                    if (!Enabled || ReadOnly)
                    {
                        backColor = SystemColors.Control;
                    }
                }

                using (SolidBrush backgroundBrush = new SolidBrush(backColor))
                {
                    e.Graphics.FillRectangle(backgroundBrush, ClientRectangle);
                }

                Rectangle rectBorder = new Rectangle(ClientRectangle.Left, ClientRectangle.Top,
                   ClientRectangle.Width - 1, ClientRectangle.Height - 1);

                switch (BorderStyle)
                {
                    case BorderStyle.Fixed3D:

                        if (Application.RenderWithVisualStyles)
                        {
                            using (Pen pen = new Pen(VisualStyleInformation.TextControlBorder))
                            {
                                e.Graphics.DrawRectangle(pen, rectBorder);
                            }
                            rectBorder.Inflate(-1, -1);
                            e.Graphics.DrawRectangle(SystemPens.Window, rectBorder);
                        }
                        else
                        {
                            ControlPaint.DrawBorder3D(e.Graphics, ClientRectangle, Border3DStyle.Sunken);
                        }
                        break;

                    case BorderStyle.FixedSingle:

                        ControlPaint.DrawBorder(e.Graphics, ClientRectangle,
                           SystemColors.WindowFrame, ButtonBorderStyle.Solid);
                        break;
                }
            }

            protected override void OnSizeChanged(EventArgs e)
            {
                base.OnSizeChanged(e);
                AdjustSize();
            }

            #endregion // Protected Methods

            #region Private Properties

            private bool HasMouse
            {
                get
                {
                    return DisplayRectangle.Contains(PointToClient(MousePosition));
                }
            }

            #endregion  // Private Properties

            #region Private Methods

            private void AdjustSize()
            {
                Size newSize = MinimumSize;

                if (Width > newSize.Width)
                {
                    newSize.Width = Width;
                }

                if (Height > newSize.Height)
                {
                    newSize.Height = Height;
                }

                if (AutoHeight)
                {
                    Size = new Size(newSize.Width, MinimumSize.Height);
                }
                else
                {
                    Size = newSize;
                }

                LayoutControls();
            }

            private Size CalculateMinimumSize()
            {
                Size minimumSize = new Size(0, 0);

                foreach (FieldControl fc in _fieldControls)
                {
                    minimumSize.Width += fc.Width;
                    minimumSize.Height = Math.Max(minimumSize.Height, fc.Height);
                }

                foreach (DotControl dc in _dotControls)
                {
                    minimumSize.Width += dc.Width;
                    minimumSize.Height = Math.Max(minimumSize.Height, dc.Height);
                }

                switch (BorderStyle)
                {
                    case BorderStyle.Fixed3D:
                        minimumSize.Width += 6;
                        minimumSize.Height += (GetSuggestedHeight() - minimumSize.Height);
                        break;
                    case BorderStyle.FixedSingle:
                        minimumSize.Width += 4;
                        minimumSize.Height += (GetSuggestedHeight() - minimumSize.Height);
                        break;
                }

                return minimumSize;
            }

            private void Cleanup()
            {
                foreach (DotControl dc in _dotControls)
                {
                    Controls.Remove(dc);
                    dc.Dispose();
                }

                foreach (FieldControl fc in _fieldControls)
                {
                    Controls.Remove(fc);
                    fc.Dispose();
                }

                _dotControls = null;
                _fieldControls = null;
            }

            private int GetSuggestedHeight()
            {
                _referenceTextBox.BorderStyle = BorderStyle;
                _referenceTextBox.Font = Font;
                return _referenceTextBox.Height;
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1806", Justification = "What should be done if ReleaseDC() doesn't work?")]
            private static NativeMethods.TEXTMETRIC GetTextMetrics(IntPtr hwnd, Font font)
            {
                IntPtr hdc = NativeMethods.GetWindowDC(hwnd);

                NativeMethods.TEXTMETRIC textMetric;
                IntPtr hFont = font.ToHfont();

                try
                {
                    IntPtr hFontPrevious = NativeMethods.SelectObject(hdc, hFont);
                    NativeMethods.GetTextMetrics(hdc, out textMetric);
                    NativeMethods.SelectObject(hdc, hFontPrevious);
                }
                finally
                {
                    NativeMethods.ReleaseDC(hwnd, hdc);
                    NativeMethods.DeleteObject(hFont);
                }

                return textMetric;
            }

            private void IPAddressControl_DragDrop(object sender, System.Windows.Forms.DragEventArgs e)
            {
                Text = e.Data.GetData(DataFormats.Text).ToString();
            }

            private void IPAddressControl_DragEnter(object sender, System.Windows.Forms.DragEventArgs e)
            {
                if (e.Data.GetDataPresent(DataFormats.Text))
                {
                    e.Effect = DragDropEffects.Copy;
                }
                else
                {
                    e.Effect = DragDropEffects.None;
                }
            }

            private void LayoutControls()
            {
                SuspendLayout();

                int difference = Width - MinimumSize.Width;

                Debug.Assert(difference >= 0);

                int numOffsets = _fieldControls.Length + _dotControls.Length + 1;

                int div = difference / (numOffsets);
                int mod = difference % (numOffsets);

                int[] offsets = new int[numOffsets];

                for (int index = 0; index < numOffsets; ++index)
                {
                    offsets[index] = div;

                    if (index < mod)
                    {
                        ++offsets[index];
                    }
                }

                int x = 0;
                int y = 0;

                switch (BorderStyle)
                {
                    case BorderStyle.Fixed3D:
                        x = Fixed3DOffset.Width;
                        y = Fixed3DOffset.Height;
                        break;
                    case BorderStyle.FixedSingle:
                        x = FixedSingleOffset.Width;
                        y = FixedSingleOffset.Height;
                        break;
                }

                int offsetIndex = 0;

                x += offsets[offsetIndex++];

                for (int i = 0; i < _fieldControls.Length; ++i)
                {
                    _fieldControls[i].Location = new Point(x, y);

                    x += _fieldControls[i].Width;

                    if (i < _dotControls.Length)
                    {
                        x += offsets[offsetIndex++];
                        _dotControls[i].Location = new Point(x, y);
                        x += _dotControls[i].Width;
                        x += offsets[offsetIndex++];
                    }
                }

                ResumeLayout(false);
            }

            private void OnCedeFocus(Object sender, CedeFocusEventArgs e)
            {
                switch (e.Action)
                {
                    case Action.Home:

                        _fieldControls[0].TakeFocus(Action.Home);
                        return;

                    case Action.End:

                        _fieldControls[FieldCount - 1].TakeFocus(Action.End);
                        return;

                    case Action.Trim:

                        if (e.FieldIndex == 0)
                        {
                            return;
                        }

                        _fieldControls[e.FieldIndex - 1].TakeFocus(Action.Trim);
                        return;
                }

                if ((e.Direction == Direction.Reverse && e.FieldIndex == 0) ||
                     (e.Direction == Direction.Forward && e.FieldIndex == (FieldCount - 1)))
                {
                    return;
                }

                int fieldIndex = e.FieldIndex;

                if (e.Direction == Direction.Forward)
                {
                    ++fieldIndex;
                }
                else
                {
                    --fieldIndex;
                }

                _fieldControls[fieldIndex].TakeFocus(e.Direction, e.Selection);
            }

            private void OnFieldGotFocus(Object sender, EventArgs e)
            {
                if (!_focused)
                {
                    _focused = true;
                    base.OnGotFocus(EventArgs.Empty);
                }
            }

            private void OnFieldKeyDown(Object sender, KeyEventArgs e)
            {
                OnKeyDown(e);
            }

            private void OnFieldKeyPressed(Object sender, KeyPressEventArgs e)
            {
                OnKeyPress(e);
            }

            private void OnFieldPreviewKeyDown(Object sender, PreviewKeyDownEventArgs e)
            {
                OnPreviewKeyDown(e);
            }

            private void OnFieldKeyUp(Object sender, KeyEventArgs e)
            {
                OnKeyUp(e);
            }

            private void OnFieldLostFocus(Object sender, EventArgs e)
            {
                if (!Focused)
                {
                    _focused = false;
                    base.OnLostFocus(EventArgs.Empty);
                }
            }

            private void OnFieldTextChanged(Object sender, TextChangedEventArgs e)
            {
                if (null != FieldChangedEvent)
                {
                    FieldChangedEventArgs args = new FieldChangedEventArgs();
                    args.FieldIndex = e.FieldIndex;
                    args.Text = e.Text;
                    FieldChangedEvent(this, args);
                }

                OnTextChanged(EventArgs.Empty);
            }

            private void OnSubControlClicked(object sender, EventArgs e)
            {
                OnClick(e);
            }

            private void OnSubControlDoubleClicked(object sender, EventArgs e)
            {
                OnDoubleClick(e);
            }

            private void OnSubControlMouseClicked(object sender, MouseEventArgs e)
            {
                OnMouseClick(e);
            }

            private void OnSubControlMouseDoubleClicked(object sender, MouseEventArgs e)
            {
                OnMouseDoubleClick(e);
            }

            private void OnSubControlMouseEntered(object sender, EventArgs e)
            {
                OnMouseEnter(e);
            }

            private void OnSubControlMouseHovered(object sender, EventArgs e)
            {
                OnMouseHover(e);
            }

            private void OnSubControlMouseLeft(object sender, EventArgs e)
            {
                OnMouseLeave(e);
            }

            private void OnSubControlMouseMoved(object sender, MouseEventArgs e)
            {
                OnMouseMove(e);
            }

            private void Parse(String text)
            {
                Clear();

                if (null == text)
                {
                    return;
                }

                int textIndex = 0;

                int index = 0;

                for (index = 0; index < _dotControls.Length; ++index)
                {
                    int findIndex = text.IndexOf(_dotControls[index].Text, textIndex, StringComparison.Ordinal);

                    if (findIndex >= 0)
                    {
                        _fieldControls[index].Text = text.Substring(textIndex, findIndex - textIndex);
                        textIndex = findIndex + _dotControls[index].Text.Length;
                    }
                    else
                    {
                        break;
                    }
                }

                _fieldControls[index].Text = text.Substring(textIndex);
            }

            // a hack to remove an FxCop warning
            private void ResetBackColorChanged()
            {
                _backColorChanged = false;
            }

            #endregion Private Methods

            #region Private Data

            private bool _autoHeight = true;
            private bool _backColorChanged;
            private BorderStyle _borderStyle = BorderStyle.Fixed3D;
            private DotControl[] _dotControls = new DotControl[FieldCount - 1];
            private FieldControl[] _fieldControls = new FieldControl[FieldCount];
            private bool _focused;
            private bool _hasMouse;
            private bool _readOnly;

            private Size Fixed3DOffset = new Size(3, 3);
            private Size FixedSingleOffset = new Size(2, 2);

            private TextBox _referenceTextBox = new TextBox();

            #endregion  // Private Data
        }

        class IPAddressControlDesigner : ControlDesigner
        {
            public override SelectionRules SelectionRules
            {
                get
                {
                    IPAddressControl control = (IPAddressControl)Control;

                    if (control.AutoHeight)
                    {
                        return SelectionRules.Moveable | SelectionRules.Visible | SelectionRules.LeftSizeable | SelectionRules.RightSizeable;
                    }
                    else
                    {
                        return SelectionRules.AllSizeable | SelectionRules.Moveable | SelectionRules.Visible;
                    }
                }
            }

            public override IList SnapLines
            {
                get
                {
                    IPAddressControl control = (IPAddressControl)Control;

                    IList snapLines = base.SnapLines;

                    snapLines.Add(new SnapLine(SnapLineType.Baseline, control.Baseline));

                    return snapLines;
                }
            }
        }

        internal class NativeMethods
        {
            private NativeMethods() { }

            [DllImport("user32.dll")]
            public static extern IntPtr GetWindowDC(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

            [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool GetTextMetrics(IntPtr hdc, out TEXTMETRIC lptm);

            [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

            [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DeleteObject(IntPtr hdc);

            [Serializable, StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct TEXTMETRIC
            {
                public int tmHeight;
                public int tmAscent;
                public int tmDescent;
                public int tmInternalLeading;
                public int tmExternalLeading;
                public int tmAveCharWidth;
                public int tmMaxCharWidth;
                public int tmWeight;
                public int tmOverhang;
                public int tmDigitizedAspectX;
                public int tmDigitizedAspectY;
                public char tmFirstChar;
                public char tmLastChar;
                public char tmDefaultChar;
                public char tmBreakChar;
                public byte tmItalic;
                public byte tmUnderlined;
                public byte tmStruckOut;
                public byte tmPitchAndFamily;
                public byte tmCharSet;
            }
        }

        internal class FieldControl : TextBox
        {
            #region Public Constants

            public const byte MinimumValue = 0;
            public const byte MaximumValue = 255;

            #endregion // Public Constants

            #region Public Events

            public event EventHandler<CedeFocusEventArgs> CedeFocusEvent;
            public event EventHandler<TextChangedEventArgs> TextChangedEvent;

            #endregion // Public Events

            #region Public Properties

            public bool Blank
            {
                get { return (TextLength == 0); }
            }

            public int FieldIndex
            {
                get { return _fieldIndex; }
                set { _fieldIndex = value; }
            }

            public override Size MinimumSize
            {
                get
                {
                    Graphics g = Graphics.FromHwnd(Handle);

                    Size minimumSize = TextRenderer.MeasureText(g,
                       Properties.Resources.FieldMeasureText, Font, Size,
                       _textFormatFlags);

                    g.Dispose();

                    return minimumSize;
                }
            }

            public byte RangeLower
            {
                get { return _rangeLower; }
                set
                {
                    if (value < MinimumValue)
                    {
                        _rangeLower = MinimumValue;
                    }
                    else if (value > _rangeUpper)
                    {
                        _rangeLower = _rangeUpper;
                    }
                    else
                    {
                        _rangeLower = value;
                    }

                    if (Value < _rangeLower)
                    {
                        Text = _rangeLower.ToString(CultureInfo.InvariantCulture);
                    }
                }
            }

            public byte RangeUpper
            {
                get { return _rangeUpper; }
                set
                {
                    if (value < _rangeLower)
                    {
                        _rangeUpper = _rangeLower;
                    }
                    else if (value > MaximumValue)
                    {
                        _rangeUpper = MaximumValue;
                    }
                    else
                    {
                        _rangeUpper = value;
                    }

                    if (Value > _rangeUpper)
                    {
                        Text = _rangeUpper.ToString(CultureInfo.InvariantCulture);
                    }
                }
            }

            public byte Value
            {
                get
                {
                    byte result;

                    if (!Byte.TryParse(Text, out result))
                    {
                        result = RangeLower;
                    }

                    return result;
                }
            }

            #endregion // Public Properties

            #region Public Methods

            public void TakeFocus(Action action)
            {
                Focus();

                switch (action)
                {
                    case Action.Trim:

                        if (TextLength > 0)
                        {
                            int newLength = TextLength - 1;
                            base.Text = Text.Substring(0, newLength);
                        }

                        SelectionStart = TextLength;

                        return;

                    case Action.Home:

                        SelectionStart = 0;
                        SelectionLength = 0;

                        return;

                    case Action.End:

                        SelectionStart = TextLength;

                        return;
                }
            }

            public void TakeFocus(Direction direction, Selection selection)
            {
                Focus();

                if (selection == Selection.All)
                {
                    SelectionStart = 0;
                    SelectionLength = TextLength;
                }
                else
                {
                    SelectionStart = (direction == Direction.Forward) ? 0 : TextLength;
                }
            }

            public override string ToString()
            {
                return Value.ToString(CultureInfo.InvariantCulture);
            }

            #endregion // Public Methods

            #region Constructors

            public FieldControl()
            {
                BorderStyle = BorderStyle.None;
                MaxLength = 3;
                Size = MinimumSize;
                TabStop = false;
                TextAlign = HorizontalAlignment.Center;
            }

            #endregion //Constructors

            #region Protected Methods

            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (null == e) { throw new ArgumentNullException("e"); }

                base.OnKeyDown(e);

                switch (e.KeyCode)
                {
                    case Keys.Home:
                        SendCedeFocusEvent(Action.Home);
                        return;

                    case Keys.End:
                        SendCedeFocusEvent(Action.End);
                        return;
                }

                if (IsCedeFocusKey(e))
                {
                    SendCedeFocusEvent(Direction.Forward, Selection.All);
                    e.SuppressKeyPress = true;
                    return;
                }
                else if (IsForwardKey(e))
                {
                    if (e.Control)
                    {
                        SendCedeFocusEvent(Direction.Forward, Selection.All);
                        return;
                    }
                    else if (SelectionLength == 0 && SelectionStart == TextLength)
                    {
                        SendCedeFocusEvent(Direction.Forward, Selection.None);
                        return;
                    }
                }
                else if (IsReverseKey(e))
                {
                    if (e.Control)
                    {
                        SendCedeFocusEvent(Direction.Reverse, Selection.All);
                        return;
                    }
                    else if (SelectionLength == 0 && SelectionStart == 0)
                    {
                        SendCedeFocusEvent(Direction.Reverse, Selection.None);
                        return;
                    }
                }
                else if (IsBackspaceKey(e))
                {
                    HandleBackspaceKey(e);
                }
                else if (!IsNumericKey(e) &&
                          !IsEditKey(e) &&
                          !IsEnterKey(e))
                {
                    e.SuppressKeyPress = true;
                }
            }

            protected override void OnParentBackColorChanged(EventArgs e)
            {
                base.OnParentBackColorChanged(e);
                BackColor = Parent.BackColor;
            }

            protected override void OnParentForeColorChanged(EventArgs e)
            {
                base.OnParentForeColorChanged(e);
                ForeColor = Parent.ForeColor;
            }

            protected override void OnSizeChanged(EventArgs e)
            {
                base.OnSizeChanged(e);
                Size = MinimumSize;
            }

            protected override void OnTextChanged(EventArgs e)
            {
                base.OnTextChanged(e);

                if (!Blank)
                {
                    int value;
                    if (!Int32.TryParse(Text, out value))
                    {
                        base.Text = String.Empty;
                    }
                    else
                    {
                        if (value > RangeUpper)
                        {
                            base.Text = RangeUpper.ToString(CultureInfo.InvariantCulture);
                            SelectionStart = 0;
                        }
                        else if ((TextLength == MaxLength) && (value < RangeLower))
                        {
                            base.Text = RangeLower.ToString(CultureInfo.InvariantCulture);
                            SelectionStart = 0;
                        }
                        else
                        {
                            int originalLength = TextLength;
                            int newSelectionStart = SelectionStart;

                            base.Text = value.ToString(CultureInfo.InvariantCulture);

                            if (TextLength < originalLength)
                            {
                                newSelectionStart -= (originalLength - TextLength);
                                SelectionStart = Math.Max(0, newSelectionStart);
                            }
                        }
                    }
                }

                if (null != TextChangedEvent)
                {
                    TextChangedEventArgs args = new TextChangedEventArgs();
                    args.FieldIndex = FieldIndex;
                    args.Text = Text;
                    TextChangedEvent(this, args);
                }

                if (TextLength == MaxLength && Focused && SelectionStart == TextLength)
                {
                    SendCedeFocusEvent(Direction.Forward, Selection.All);
                }
            }

            protected override void OnValidating(System.ComponentModel.CancelEventArgs e)
            {
                base.OnValidating(e);

                if (!Blank)
                {
                    if (Value < RangeLower)
                    {
                        Text = RangeLower.ToString(CultureInfo.InvariantCulture);
                    }
                }
            }

            protected override void WndProc(ref Message m)
            {
                switch (m.Msg)
                {
                    case 0x007b:  // WM_CONTEXTMENU
                        return;
                }

                base.WndProc(ref m);
            }

            #endregion // Protected Methods

            #region Private Methods

            private void HandleBackspaceKey(KeyEventArgs e)
            {
                if (!ReadOnly && (TextLength == 0 || (SelectionStart == 0 && SelectionLength == 0)))
                {
                    SendCedeFocusEvent(Action.Trim);
                    e.SuppressKeyPress = true;
                }
            }

            private static bool IsBackspaceKey(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Back)
                {
                    return true;
                }

                return false;
            }

            private bool IsCedeFocusKey(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.OemPeriod ||
                     e.KeyCode == Keys.Decimal ||
                     e.KeyCode == Keys.Space)
                {
                    if (TextLength != 0 && SelectionLength == 0 && SelectionStart != 0)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool IsEditKey(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Back ||
                     e.KeyCode == Keys.Delete)
                {
                    return true;
                }
                else if (e.Modifiers == Keys.Control &&
                          (e.KeyCode == Keys.C ||
                            e.KeyCode == Keys.V ||
                            e.KeyCode == Keys.X))
                {
                    return true;
                }

                return false;
            }

            private static bool IsEnterKey(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter ||
                     e.KeyCode == Keys.Return)
                {
                    return true;
                }

                return false;
            }

            private static bool IsForwardKey(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Right ||
                     e.KeyCode == Keys.Down)
                {
                    return true;
                }

                return false;
            }

            private static bool IsNumericKey(KeyEventArgs e)
            {
                if (e.KeyCode < Keys.NumPad0 || e.KeyCode > Keys.NumPad9)
                {
                    if (e.KeyCode < Keys.D0 || e.KeyCode > Keys.D9)
                    {
                        return false;
                    }
                }

                return true;
            }

            private static bool IsReverseKey(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Left ||
                     e.KeyCode == Keys.Up)
                {
                    return true;
                }

                return false;
            }

            private void SendCedeFocusEvent(Action action)
            {
                if (null != CedeFocusEvent)
                {
                    CedeFocusEventArgs args = new CedeFocusEventArgs();
                    args.FieldIndex = FieldIndex;
                    args.Action = action;
                    CedeFocusEvent(this, args);
                }
            }

            private void SendCedeFocusEvent(Direction direction, Selection selection)
            {
                if (null != CedeFocusEvent)
                {
                    CedeFocusEventArgs args = new CedeFocusEventArgs();
                    args.FieldIndex = FieldIndex;
                    args.Action = Action.None;
                    args.Direction = direction;
                    args.Selection = selection;
                    CedeFocusEvent(this, args);
                }
            }

            #endregion // Private Methods

            #region Private Data

            private int _fieldIndex = -1;
            private byte _rangeLower; // = MinimumValue;  // this is removed for FxCop approval
            private byte _rangeUpper = MaximumValue;

            private TextFormatFlags _textFormatFlags = TextFormatFlags.HorizontalCenter |
               TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

            #endregion // Private Data
        }

        internal enum Direction
        {
            Forward,
            Reverse
        }

        internal enum Selection
        {
            None,
            All
        }

        internal enum Action
        {
            None,
            Trim,
            Home,
            End
        }

        internal class CedeFocusEventArgs : EventArgs
        {
            private int _fieldIndex;
            private Action _action;
            private Direction _direction;
            private Selection _selection;

            public int FieldIndex
            {
                get { return _fieldIndex; }
                set { _fieldIndex = value; }
            }

            public Action Action
            {
                get { return _action; }
                set { _action = value; }
            }

            public Direction Direction
            {
                get { return _direction; }
                set { _direction = value; }
            }

            public Selection Selection
            {
                get { return _selection; }
                set { _selection = value; }
            }
        }

        internal class TextChangedEventArgs : EventArgs
        {
            private int _fieldIndex;
            private String _text;

            public int FieldIndex
            {
                get { return _fieldIndex; }
                set { _fieldIndex = value; }
            }

            public String Text
            {
                get { return _text; }
                set { _text = value; }
            }
        }

        public class FieldChangedEventArgs : EventArgs
        {
            private int _fieldIndex;
            private String _text;

            public int FieldIndex
            {
                get { return _fieldIndex; }
                set { _fieldIndex = value; }
            }

            public String Text
            {
                get { return _text; }
                set { _text = value; }
            }
        }

        internal class DotControl : Control
        {
            #region Public Properties

            public override Size MinimumSize
            {
                get
                {
                    using (Graphics g = Graphics.FromHwnd(Handle))
                    {
                        _sizeText = g.MeasureString(Text, Font, -1, _stringFormat);
                    }

                    // MeasureString() cuts off the bottom pixel for descenders no matter
                    // which StringFormatFlags are chosen.  This doesn't matter for '.' but
                    // it's here in case someone wants to modify the text.
                    //
                    _sizeText.Height += 1F;

                    return _sizeText.ToSize();
                }
            }

            public bool ReadOnly
            {
                get
                {
                    return _readOnly;
                }
                set
                {
                    _readOnly = value;
                    Invalidate();
                }
            }

            #endregion // Public Properties

            #region Public Methods

            public override string ToString()
            {
                return Text;
            }

            #endregion // Public Methods

            #region Constructors

            public DotControl()
            {
                Text = Properties.Resources.FieldSeparator;

                _stringFormat = StringFormat.GenericTypographic;
                _stringFormat.FormatFlags = StringFormatFlags.MeasureTrailingSpaces;

                BackColor = SystemColors.Window;
                Size = MinimumSize;
                TabStop = false;

                SetStyle(ControlStyles.AllPaintingInWmPaint, true);
                SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
                SetStyle(ControlStyles.ResizeRedraw, true);
                SetStyle(ControlStyles.UserPaint, true);

                SetStyle(ControlStyles.FixedHeight, true);
                SetStyle(ControlStyles.FixedWidth, true);
            }

            #endregion // Constructors

            #region Protected Methods

            protected override void OnFontChanged(EventArgs e)
            {
                base.OnFontChanged(e);
                Size = MinimumSize;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                if (null == e) { throw new ArgumentNullException("e"); }

                base.OnPaint(e);

                Color backColor = BackColor;

                if (!_backColorChanged)
                {
                    if (!Enabled || ReadOnly)
                    {
                        backColor = SystemColors.Control;
                    }
                }

                Color textColor = ForeColor;

                if (!Enabled)
                {
                    textColor = SystemColors.GrayText;
                }
                else if (ReadOnly)
                {
                    if (!_backColorChanged)
                    {
                        textColor = SystemColors.WindowText;
                    }
                }

                using (SolidBrush backgroundBrush = new SolidBrush(backColor))
                {
                    e.Graphics.FillRectangle(backgroundBrush, ClientRectangle);
                }

                using (SolidBrush foreBrush = new SolidBrush(textColor))
                {
                    float x = (float)ClientRectangle.Width / 2F - _sizeText.Width / 2F;
                    e.Graphics.DrawString(Text, Font, foreBrush,
                       new RectangleF(x, 0F, _sizeText.Width, _sizeText.Height), _stringFormat);
                }
            }

            protected override void OnParentBackColorChanged(EventArgs e)
            {
                base.OnParentBackColorChanged(e);
                BackColor = Parent.BackColor;
                _backColorChanged = true;
            }

            protected override void OnParentForeColorChanged(EventArgs e)
            {
                base.OnParentForeColorChanged(e);
                ForeColor = Parent.ForeColor;
            }

            protected override void OnSizeChanged(EventArgs e)
            {
                base.OnSizeChanged(e);
                base.Size = MinimumSize;
            }

            #endregion // Protected Methods

            #region Private Data

            private bool _backColorChanged;
            private bool _readOnly;

            private StringFormat _stringFormat;
            private SizeF _sizeText;

            #endregion // Private Data
        }
    }
}