using System;
using NAudio.Wave;

namespace CPlayer.WinForms.Core
{
    public class AudioRenderer : IDisposable
    {
        private WaveOutEvent _waveOut;
        private BufferedWaveProvider _buffer;

        public void Init(int sampleRate, int channels)
        {
            if (sampleRate <= 0) sampleRate = 44100;
            if (channels <= 0) channels = 2;

            _buffer = new BufferedWaveProvider(new WaveFormat(sampleRate, 16, channels))
            {
                BufferDuration = TimeSpan.FromSeconds(2),
                DiscardOnBufferOverflow = true
            };
            
            _waveOut = new WaveOutEvent { DesiredLatency = 80 };
            _waveOut.Init(_buffer);
            _waveOut.Play();
        }

        public void Feed(byte[] pcm16)
        {
            if (_buffer != null)
            {
                _buffer.AddSamples(pcm16, 0, pcm16.Length);
            }
        }

        public void SetVolume(float v)
        {
            if (_waveOut != null)
            {
                _waveOut.Volume = Math.Max(0f, Math.Min(v, 1f));
            }
        }

        public void ClearBuffer()
        {
            _buffer?.ClearBuffer();
        }

        public void Dispose()
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut = null;
        }
    }
}
