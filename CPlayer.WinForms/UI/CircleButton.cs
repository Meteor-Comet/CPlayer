using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CPlayer.WinForms.UI
{
    public class CircleButton : Control
    {
        private string _symbol;
        public string Symbol
        {
            get => _symbol;
            set
            {
                _symbol = value;
                Invalidate();
            }
        }

        private bool _hover;

        public CircleButton(string symbol, int size)
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Symbol = symbol;
            Size = new Size(size + 8, size + 8);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI Emoji", size * 0.55f);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            
            var r = new Rectangle(1, 1, Width - 2, Height - 2);
            if (_hover)
            {
                using (var b = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                {
                    g.FillEllipse(b, r);
                }
            }

            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using (var brush = new SolidBrush(Color.White))
            {
                g.DrawString(Symbol, Font, brush, ClientRectangle, sf);
            }
        }
    }
}
