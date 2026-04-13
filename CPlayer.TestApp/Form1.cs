using System;
using System.Drawing;
using System.Windows.Forms;
using CPlayer.WinForms.UI;

namespace CPlayer.TestApp
{
    public class Form1 : Form
    {
        private MediaPlayerControl _player;
        private Button _btnOpen;

        public Form1()
        {
            Text = "CPlayer Test App";
            Size = new Size(800, 600);
            BackColor = Color.FromArgb(20, 20, 20);
            
            _player = new MediaPlayerControl
            {
                Dock = DockStyle.Fill
            };

            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.FromArgb(30, 30, 30)
            };

            _btnOpen = new Button
            {
                Text = "Open Media...",
                Location = new Point(10, 8),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _btnOpen.FlatAppearance.BorderSize = 0;
            _btnOpen.Click += BtnOpen_Click;
            
            topPanel.Controls.Add(_btnOpen);

            Controls.Add(_player);
            Controls.Add(topPanel);
        }

        private void BtnOpen_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Media Files|*.mp4;*.mkv;*.avi;*.mov;*.mp3;*.flac|All Files|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    _player.LoadAndPlay(ofd.FileName);
                    Text = $"CPlayer - {System.IO.Path.GetFileName(ofd.FileName)}";
                }
            }
        }
    }
}
