using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CPlayer.WinForms.UI
{
    public class CustomSeekBar : Control
    {
        private float _progress;
        private bool _dragging;
        private bool _hover;
        public event Action<double> SeekRequested;

        public CustomSeekBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); }

        public void SetProgress(double v)
        {
            if (!_dragging)
            {
                _progress = Math.Max(0f, Math.Min((float)v, 1f));
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var trackY = Height / 2;
            var isFocus = _hover || _dragging;
            var trackH = isFocus ? 6 : 3;
            var trackRect = new Rectangle(0, trackY - trackH / 2, Width, trackH);

            using (var bgBrush = new SolidBrush(Color.FromArgb(80, 255, 255, 255)))
            {
                g.FillRectangle(bgBrush, trackRect);
            }

            var fillW = (int)(trackRect.Width * _progress);
            if (fillW > 0)
            {
                var fillRect = new Rectangle(trackRect.X, trackRect.Y, fillW, trackRect.Height);
                using (var fillBrush = new SolidBrush(Color.FromArgb(255, 0, 161, 214)))
                {
                    g.FillRectangle(fillBrush, fillRect);
                }
            }

            if (isFocus && fillW > 0)
            {
                float cx = trackRect.X + fillW;
                float cy = trackY;
                g.FillEllipse(Brushes.White, cx - 7, cy - 7, 14, 14);
                using (var innerBrush = new SolidBrush(Color.FromArgb(255, 0, 161, 214)))
                {
                    g.FillEllipse(innerBrush, cx - 4, cy - 4, 8, 8);
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                UpdateSeek(e.X);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging && e.Button == MouseButtons.Left)
            {
                UpdateSeek(e.X);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _dragging)
            {
                _dragging = false;
                UpdateSeek(e.X);
                SeekRequested?.Invoke(_progress);
            }
        }

        private void UpdateSeek(int x)
        {
            float ratio = Math.Max(0f, Math.Min(x / (float)Width, 1f));
            _progress = ratio;
            Invalidate();
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            if (r.Width <= 0 || r.Height <= 0) return path;

            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
