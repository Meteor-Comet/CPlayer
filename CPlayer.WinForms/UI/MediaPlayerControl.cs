using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CPlayer.WinForms.Core;

namespace CPlayer.WinForms.UI
{
    [ToolboxItem(true)]
    public class MediaPlayerControl : UserControl
    {
        private PictureBox _videoSurface;
        private Panel _controlBar;
        private CustomSeekBar _seekBar;
        private Label _timeLabel;
        private CircleButton _playBtn, _muteBtn, _fullBtn;
        private VolumeSlider _volumeSlider;
        private Label _speedLabelText;
        private Timer _uiTimer;
        private DateTime _lastSeekTime = DateTime.MinValue;
        private double _currentSpeed = 1.0;


        private MediaDecoder _decoder;
        private AudioRenderer _audio;
        private bool _playing, _muted;
        private double _seekTarget = -1;
        private Bitmap _lastFrame;

        public MediaPlayerControl()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint, true);

            BuildUI();
            BuildTimer();
        }

        private void BuildUI()
        {
            BackColor = Color.FromArgb(8, 8, 12);
            Padding = new Padding(0);

            _videoSurface = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            _videoSurface.Click += (_, __) =>
            {
                this.Focus();
                TogglePlay();
            };

            _controlBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                BackColor = Color.Transparent
            };
            _controlBar.Paint += ControlBar_Paint;

            _seekBar = new CustomSeekBar
            {
                Dock = DockStyle.Top,
                Height = 12,
                BackColor = Color.Transparent
            };
            _seekBar.SeekRequested += pos => 
            { 
                _seekTarget = pos; 
                if (_decoder != null && _decoder.Duration > 0)
                {
                    _timeLabel.Text = $"{TimeSpan.FromSeconds(pos * _decoder.Duration):mm\\:ss} / {TimeSpan.FromSeconds(_decoder.Duration):mm\\:ss}";
                }
            };

            _playBtn = new CircleButton("▶", 28) { Location = new Point(14, 16) };
            
            _timeLabel = new Label
            {
                ForeColor = Color.FromArgb(220, 220, 220),
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 10f),
                AutoSize = true,
                Location = new Point(56, 25)
            };

            _speedLabelText = new Label
            {
                Text = "倍速",
                ForeColor = Color.FromArgb(220, 220, 220),
                BackColor = Color.Transparent,
                Font = new Font("Microsoft YaHei", 10.5f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(Width - 180, 24),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Cursor = Cursors.Hand
            };

            var speedMenu = new ContextMenuStrip 
            { 
                ShowImageMargin = false, 
                BackColor = Color.FromArgb(40, 40, 45), 
                ForeColor = Color.White,
                Font = new Font("Consolas", 10f)
            };
            
            double[] speeds = { 2.0, 1.5, 1.25, 1.0, 0.75, 0.5 };
            foreach (var s in speeds)
            {
                var item = new ToolStripMenuItem($"{s:0.0#}x") { ForeColor = Color.White };
                item.Click += (sender, e) => 
                {
                    _currentSpeed = s;
                    _speedLabelText.Text = s == 1.0 ? "倍速" : $"{s:0.0#}x";
                    if (_decoder != null)
                    {
                        _decoder.PlaybackSpeed = _currentSpeed;
                    }
                };
                speedMenu.Items.Add(item);
            }

            _speedLabelText.MouseEnter += (s, e) => 
            {
                speedMenu.Show(_speedLabelText, new Point(0, -speedMenu.Height));
            };

            _muteBtn = new CircleButton("🔊", 22) { Location = new Point(Width - 120, 20), Anchor = AnchorStyles.Right | AnchorStyles.Top };
            
            _volumeSlider = new VolumeSlider
            {
                Size = new Size(60, 14),
                Location = new Point(Width - 88, 28),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Value = 0.8f
            };

            _fullBtn = new CircleButton("⛶", 22) { Location = new Point(Width - 26, 20), Anchor = AnchorStyles.Right | AnchorStyles.Top };
            _volumeSlider.ValueChanged += v =>
            {
                _audio?.SetVolume(v);
                if (_muted && v > 0) ToggleMute();
            };

            _playBtn.Click += (_, __) => TogglePlay();
            _muteBtn.Click += (_, __) => ToggleMute();
            _fullBtn.Click += (_, __) => ToggleFullscreen();

            _controlBar.Controls.AddRange(new Control[]
            {
                _seekBar, _playBtn, _timeLabel,
                _speedLabelText,
                _muteBtn, _volumeSlider, _fullBtn
            });

            Controls.Add(_videoSurface);
            Controls.Add(_controlBar);
            _controlBar.BringToFront();
        }

        private void ControlBar_Paint(object s, PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var brush = new LinearGradientBrush(
                _controlBar.ClientRectangle,
                Color.FromArgb(0, 0, 0, 0),
                Color.FromArgb(240, 0, 0, 0),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(brush, _controlBar.ClientRectangle);
            }
        }

        private void BuildTimer()
        {
            _uiTimer = new Timer { Interval = 16 };
            _uiTimer.Tick += UiTimer_Tick;
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            if (_decoder == null) return;

            if (_seekTarget >= 0)
            {
                _lastSeekTime = DateTime.Now;
                var targetDur = _decoder.Duration;
                if (targetDur > 0)
                {
                    _decoder.Seek(_seekTarget * targetDur);
                }
                _audio?.ClearBuffer();
                _seekTarget = -1;
            }

            if (_playing)
            {
                Bitmap lastFrameToRender = null;
                while (_decoder.VideoChannel.Reader.TryRead(out var frame))
                {
                    var oldFrame = _lastFrame;
                    _lastFrame = new Bitmap(frame.Width, frame.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                    var bmpData = _lastFrame.LockBits(
                        new Rectangle(0, 0, frame.Width, frame.Height),
                        System.Drawing.Imaging.ImageLockMode.WriteOnly,
                        System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                    Marshal.Copy(frame.Data, 0, bmpData.Scan0, frame.Data.Length);
                    _lastFrame.UnlockBits(bmpData);
                    oldFrame?.Dispose();
                    
                    lastFrameToRender = _lastFrame;
                }

                if (lastFrameToRender != null)
                {
                    _videoSurface.Image = lastFrameToRender;

                    var pos = _decoder.CurrentPosition;
                    var dur = _decoder.Duration;
                    if (dur > 0 && (DateTime.Now - _lastSeekTime).TotalMilliseconds > 800)
                    {
                        _timeLabel.Text = $"{TimeSpan.FromSeconds(pos):mm\\:ss} / {TimeSpan.FromSeconds(dur):mm\\:ss}";
                        _seekBar.SetProgress(pos / dur);
                    }
                }

                while (_decoder.AudioChannel.Reader.TryRead(out var audioChunk))
                {
                    _audio?.Feed(audioChunk.PcmBytes);
                }
            }
        }

        public void LoadAndPlay(string filePath)
        {
            Stop();
            try
            {
                FFmpegLoader.RegisterBinaries();
                _decoder = new MediaDecoder();
                _decoder.Open(filePath);
                
                _audio = new AudioRenderer();
                _audio.Init(_decoder.AudioSampleRate, _decoder.AudioChannels);
                _audio.SetVolume(_muted ? 0f : _volumeSlider.Value);
                
                _decoder.Start();
                _uiTimer.Start();
                _playing = true;
                _playBtn.Symbol = "⏸";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load media: {ex}", "Debug Error Info", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Stop();
            }
        }

        private void TogglePlay()
        {
            if (_decoder == null) return;
            _playing = !_playing;
            _playBtn.Symbol = _playing ? "⏸" : "▶";
        }

        public void Stop()
        {
            _uiTimer.Stop();
            _decoder?.Dispose();
            _audio?.Dispose();
            _decoder = null; 
            _audio = null;
            _videoSurface.Image = null;
            _lastFrame?.Dispose();
            _lastFrame = null;
            _playing = false;
            if (_playBtn != null) _playBtn.Symbol = "▶";
            if (_timeLabel != null) _timeLabel.Text = "00:00 / 00:00";
            _seekBar?.SetProgress(0);
        }

        private void ToggleMute()
        {
            _muted = !_muted;
            _audio?.SetVolume(_muted ? 0f : _volumeSlider.Value);
            _muteBtn.Symbol = _muted ? "🔇" : "🔊";
        }

        private void ToggleFullscreen()
        {
            var form = FindForm();
            if (form == null) return;
            if (form.FormBorderStyle == FormBorderStyle.None)
            {
                form.FormBorderStyle = FormBorderStyle.Sizable;
                form.WindowState = FormWindowState.Normal;
            }
            else
            {
                form.FormBorderStyle = FormBorderStyle.None;
                form.WindowState = FormWindowState.Maximized;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Space)
            {
                TogglePlay();
                return true;
            }
            if (keyData == Keys.Left)
            {
                if (_decoder != null && _decoder.Duration > 0)
                {
                    double target = Math.Max(0, _decoder.CurrentPosition - 5);
                    _decoder.Seek(target);
                    _audio?.ClearBuffer();
                }
                return true;
            }
            if (keyData == Keys.Right)
            {
                if (_decoder != null && _decoder.Duration > 0)
                {
                    double target = Math.Min(_decoder.Duration, _decoder.CurrentPosition + 5);
                    _decoder.Seek(target);
                    _audio?.ClearBuffer();
                }
                return true;
            }
            if (keyData == Keys.Up)
            {
                if (_volumeSlider != null) _volumeSlider.Value += 0.05f;
                return true;
            }
            if (keyData == Keys.Down)
            {
                if (_volumeSlider != null) _volumeSlider.Value -= 0.05f;
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) 
            { 
                Stop(); 
                _uiTimer?.Dispose(); 
            }
            base.Dispose(disposing);
        }
    }
}
