using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CPlayer.WinForms.UI
{
    public class VolumeSlider : Control
    {
        private float _value = 1f;
        private bool _dragging;
        public event Action<float> ValueChanged;

        public float Value
        {
            get => _value;
            set
            {
                _value = Math.Max(0f, Math.Min(value, 1f));
                Invalidate();
            }
        }

        public VolumeSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var trackY = Height / 2;
            var trackH = 4;
            var trackRect = new Rectangle(4, trackY - trackH / 2, Width - 8, trackH);

            using (var bgBrush = new SolidBrush(Color.FromArgb(60, 255, 255, 255)))
            using (var bgPath = RoundedRect(trackRect, 2))
            {
                g.FillPath(bgBrush, bgPath);
            }

            var fillW = (int)(trackRect.Width * _value);
            if (fillW > 0)
            {
                var fillRect = new Rectangle(trackRect.X, trackRect.Y, fillW, trackRect.Height);
                using (var fillBrush = new LinearGradientBrush(fillRect,
                    Color.FromArgb(255, 0, 210, 190),
                    Color.FromArgb(255, 0, 140, 255),
                    LinearGradientMode.Horizontal))
                using (var fillPath = RoundedRect(fillRect, 2))
                {
                    g.FillPath(fillBrush, fillPath);
                }
            }

            float cx = trackRect.X + trackRect.Width * _value;
            float cy = trackY;
            g.FillEllipse(Brushes.White, cx - 5, cy - 5, 10, 10);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                UpdateValue(e.X);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging && e.Button == MouseButtons.Left)
            {
                UpdateValue(e.X);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _dragging)
            {
                _dragging = false;
            }
        }

        private void UpdateValue(int x)
        {
            float ratio = Math.Max(0f, Math.Min((x - 4f) / (Width - 8f), 1f));
            Value = ratio;
            ValueChanged?.Invoke(Value);
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
