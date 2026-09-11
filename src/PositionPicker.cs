using System;
using System.Drawing;
using System.Windows.Forms;

namespace DadsOnCall
{
    internal sealed class PositionPicker : UserControl
    {
        internal static readonly string[] PositionNames =
        {
            "Top left", "Top center", "Top right", "Center left", "Center", "Center right",
            "Bottom left", "Bottom center", "Bottom right"
        };
        private readonly Button[] sections = new Button[9];
        private readonly ToolTip toolTip = new ToolTip();
        private ScreenPosition selectedPosition;
        internal event Action<ScreenPosition> PositionChanged;

        public PositionPicker(ScreenPosition initialPosition)
        {
            SuspendLayout();
            AutoScaleMode = AutoScaleMode.Inherit;
            Size = new Size(156, 112);
            Padding = new Padding(4, 4, 4, 16);
            AccessibleName = "Screen position picker";
            var screen = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3,
                BackColor = Color.FromArgb(30, 38, 50), Padding = new Padding(3), Margin = new Padding(0)
            };
            screen.SuspendLayout();
            for (int i = 0; i < 3; i++)
            {
                screen.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
                screen.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 3));
            }
            for (int i = 0; i < sections.Length; i++)
            {
                var position = (ScreenPosition)i;
                var button = new Button
                {
                    Dock = DockStyle.Fill, Margin = new Padding(1), FlatStyle = FlatStyle.Flat,
                    AccessibleName = "Position " + PositionNames[i], UseVisualStyleBackColor = false
                };
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(105, 126, 152);
                toolTip.SetToolTip(button, PositionNames[i]);
                button.Click += delegate
                {
                    SelectedPosition = position;
                    var handler = PositionChanged;
                    if (handler != null) handler(position);
                };
                button.Paint += delegate(object sender, PaintEventArgs e)
                {
                    if (SelectedPosition != position) return;
                    int width = Math.Max(4, button.Width / 2);
                    int height = Math.Max(3, button.Height / 3);
                    e.Graphics.FillRectangle(Brushes.White, (button.Width - width) / 2, (button.Height - height) / 2, width, height);
                };
                sections[i] = button;
                screen.Controls.Add(button, i % 3, i / 3);
            }
            Controls.Add(screen);
            SelectedPosition = initialPosition;
            screen.ResumeLayout(false);
            ResumeLayout(true);
        }

        internal ScreenPosition SelectedPosition
        {
            get { return selectedPosition; }
            set
            {
                selectedPosition = Enum.IsDefined(typeof(ScreenPosition), value) ? value : ScreenPosition.TopRight;
                for (int i = 0; i < sections.Length; i++)
                {
                    bool selected = i == (int)selectedPosition;
                    sections[i].BackColor = selected ? Color.FromArgb(48, 106, 175) : Color.FromArgb(69, 83, 103);
                    sections[i].AccessibleDescription = selected ? "Selected position" : "Click to move the indicator here";
                    sections[i].Invalidate();
                }
                AccessibleDescription = PositionNames[(int)selectedPosition];
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int standHeight = Math.Max(2, Padding.Bottom / 2);
            using (var brush = new SolidBrush(Color.FromArgb(30, 38, 50)))
            {
                e.Graphics.FillRectangle(brush, Width / 2 - standHeight / 2, Height - Padding.Bottom, standHeight, standHeight);
                e.Graphics.FillRectangle(brush, Width / 2 - Width / 8, Height - standHeight, Width / 4, Math.Max(2, standHeight / 3));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) toolTip.Dispose();
            base.Dispose(disposing);
        }
    }
}
