using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using FFmpeg.AutoGen;

namespace CPlayer.WinForms.Core
{
    public record VideoFrame(byte[] Data, int Width, int Height, double Pts);
    public record AudioChunk(byte[] PcmBytes, int SampleRate, int Channels);

    public unsafe class MediaDecoder : IDisposable
    {
        private AVFormatContext* _fmtCtx;
        private AVCodecContext* _vCodecCtx;
        private AVCodecContext* _aCodecCtx;
        private int _vStreamIdx = -1, _aStreamIdx = -1;
        
        private double _pendingSeekTarget = -1;

        private Thread _decodeThread;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        
        private double _playbackSpeed = 1.0;
        private double _pendingSpeed = -1;
        public double PlaybackSpeed
        {
            get => _playbackSpeed;
            set => Interlocked.Exchange(ref _pendingSpeed, value);
        }

        private volatile bool _isEof = false;
        public bool IsEof => _isEof;

        public Channel<VideoFrame> VideoChannel { get; } =
            Channel.CreateBounded<VideoFrame>(new BoundedChannelOptions(8)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

        public Channel<AudioChunk> AudioChannel { get; } =
            Channel.CreateBounded<AudioChunk>(new BoundedChannelOptions(32)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

        public double Duration { get; private set; }
        public double CurrentPosition { get; private set; }
        public int VideoWidth { get; private set; }
        public int VideoHeight { get; private set; }
        public int AudioSampleRate { get; private set; }
        public int AudioChannels { get; private set; }

        public void Open(string url)
        {
            AVFormatContext* fmt = null;
            ThrowOnError(ffmpeg.avformat_open_input(&fmt, url, null, null));
            _fmtCtx = fmt;
            ThrowOnError(ffmpeg.avformat_find_stream_info(_fmtCtx, null));

            Duration = _fmtCtx->duration / (double)ffmpeg.AV_TIME_BASE;

            for (int i = 0; i < (int)_fmtCtx->nb_streams; i++)
            {
                var codec = _fmtCtx->streams[i]->codecpar->codec_type;
                if (codec == AVMediaType.AVMEDIA_TYPE_VIDEO && _vStreamIdx < 0) _vStreamIdx = i;
                if (codec == AVMediaType.AVMEDIA_TYPE_AUDIO && _aStreamIdx < 0) _aStreamIdx = i;
            }

            if (_vStreamIdx >= 0) OpenCodec(_vStreamIdx, out _vCodecCtx);
            if (_aStreamIdx >= 0) OpenCodec(_aStreamIdx, out _aCodecCtx);

            VideoWidth = _vCodecCtx != null ? _vCodecCtx->width : 0;
            VideoHeight = _vCodecCtx != null ? _vCodecCtx->height : 0;
            
            if (_aCodecCtx != null)
            {
                AudioSampleRate = _aCodecCtx->sample_rate;
                AudioChannels = _aCodecCtx->ch_layout.nb_channels;
            }
        }

        private void OpenCodec(int streamIdx, out AVCodecContext* ctx)
        {
            var par = _fmtCtx->streams[streamIdx]->codecpar;
            var codec = ffmpeg.avcodec_find_decoder(par->codec_id);
            ctx = ffmpeg.avcodec_alloc_context3(codec);
            ffmpeg.avcodec_parameters_to_context(ctx, par);
            ThrowOnError(ffmpeg.avcodec_open2(ctx, codec, null));
        }

        public void Start()
        {
            _decodeThread = new Thread(DecodeLoop) { IsBackground = true, Name = "FFmpegDecode" };
            _decodeThread.Start();
        }

        private void DecodeLoop()
        {
            var pkt = ffmpeg.av_packet_alloc();
            var frame = ffmpeg.av_frame_alloc();
            
            // Video SwsContext
            var swsCtx = (SwsContext*)null;
            var rgbFrame = ffmpeg.av_frame_alloc();
            int vBufSize = VideoWidth > 0 ? ffmpeg.av_image_get_buffer_size(AVPixelFormat.AV_PIX_FMT_BGR24, VideoWidth, VideoHeight, 1) : 0;
            byte* vBuf = null;
            if (vBufSize > 0)
            {
                vBuf = (byte*)ffmpeg.av_malloc((ulong)vBufSize);
                var dataTemp = new byte_ptrArray4();
                var linesizeTemp = new int_array4();
                
                ffmpeg.av_image_fill_arrays(ref dataTemp, ref linesizeTemp,
                    vBuf, AVPixelFormat.AV_PIX_FMT_BGR24, VideoWidth, VideoHeight, 1);
                    
                rgbFrame->data.UpdateFrom(dataTemp);
                rgbFrame->linesize.UpdateFrom(linesizeTemp);
            }

            // Audio SwrContext
            var swrCtx = (SwrContext*)null;
            int outSampleRate = 44100;
            AVChannelLayout outChLayout;
            ffmpeg.av_channel_layout_default(&outChLayout, 2); // default stereo
            AVSampleFormat outSampleFmt = AVSampleFormat.AV_SAMPLE_FMT_S16;

            if (_aCodecCtx != null)
            {
                outSampleRate = _aCodecCtx->sample_rate;
                if(outSampleRate <= 0) outSampleRate = 44100;
                
                ThrowOnError(ffmpeg.swr_alloc_set_opts2(&swrCtx, 
                    &outChLayout, outSampleFmt, outSampleRate,
                    &_aCodecCtx->ch_layout, _aCodecCtx->sample_fmt, _aCodecCtx->sample_rate,
                    0, null));
                ThrowOnError(ffmpeg.swr_init(swrCtx));
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            double clockOffset = 0;

            bool isEof = false;
            while (!_cts.Token.IsCancellationRequested)
            {
                double targetSeek = Interlocked.Exchange(ref _pendingSeekTarget, -1);
                if (targetSeek >= 0)
                {
                    long ts = (long)(targetSeek * ffmpeg.AV_TIME_BASE);
                    ffmpeg.av_seek_frame(_fmtCtx, -1, ts, ffmpeg.AVSEEK_FLAG_BACKWARD);
                    if (_vCodecCtx != null) ffmpeg.avcodec_flush_buffers(_vCodecCtx);
                    if (_aCodecCtx != null) ffmpeg.avcodec_flush_buffers(_aCodecCtx);

                    while (VideoChannel.Reader.TryRead(out _)) { }
                    while (AudioChannel.Reader.TryRead(out _)) { }
                    
                    sw.Restart();
                    clockOffset = targetSeek;
                    isEof = false;
                    _isEof = false; // 同步公开属性，让 UI 知道已恢复
                }

                if (isEof)
                {
                    Thread.Sleep(100);
                    continue;
                }

                int readRet = ffmpeg.av_read_frame(_fmtCtx, pkt);
                if (readRet < 0)
                {
                    isEof = true;
                    _isEof = true; // 通知 UI：视频已播放结束
                    continue;
                }

                if (pkt->stream_index == _vStreamIdx && _vCodecCtx != null)
                {
                    ffmpeg.avcodec_send_packet(_vCodecCtx, pkt);
                    while (ffmpeg.avcodec_receive_frame(_vCodecCtx, frame) == 0)
                    {
                        swsCtx = ffmpeg.sws_getCachedContext(swsCtx,
                            frame->width, frame->height, (AVPixelFormat)frame->format,
                            VideoWidth, VideoHeight, AVPixelFormat.AV_PIX_FMT_BGR24,
                            ffmpeg.SWS_BILINEAR, null, null, null);

                        ffmpeg.sws_scale(swsCtx,
                            frame->data, frame->linesize, 0, frame->height,
                            rgbFrame->data, rgbFrame->linesize);

                        var data = new byte[vBufSize];
                        Marshal.Copy((IntPtr)vBuf, data, 0, vBufSize);

                        var pts = frame->pts * ffmpeg.av_q2d(_fmtCtx->streams[_vStreamIdx]->time_base);
                        CurrentPosition = pts;

                        double masterClock = clockOffset + sw.Elapsed.TotalSeconds * _playbackSpeed;

                        double targetSpd = Interlocked.Exchange(ref _pendingSpeed, -1);
                        if (targetSpd > 0 && targetSpd != _playbackSpeed)
                        {
                            clockOffset = masterClock;
                            sw.Restart();
                            _playbackSpeed = targetSpd;
                            masterClock = clockOffset;
                        }

                        while (pts > masterClock && !_cts.Token.IsCancellationRequested)
                        {
                            Thread.Sleep(2);
                            masterClock = clockOffset + sw.Elapsed.TotalSeconds * _playbackSpeed;
                        }

                        VideoChannel.Writer.TryWrite(new VideoFrame(data, VideoWidth, VideoHeight, pts));
                    }
                }
                else if (pkt->stream_index == _aStreamIdx && _aCodecCtx != null)
                {
                    ffmpeg.avcodec_send_packet(_aCodecCtx, pkt);
                    while (ffmpeg.avcodec_receive_frame(_aCodecCtx, frame) == 0)
                    {
                        int maxOutSamples = ffmpeg.swr_get_out_samples(swrCtx, frame->nb_samples);
                        int outBytesPerSample = ffmpeg.av_get_bytes_per_sample(outSampleFmt);
                        int outChannels = outChLayout.nb_channels;
                        int aBufSize = maxOutSamples * outBytesPerSample * outChannels;
                        
                        var aBuf = (byte*)ffmpeg.av_malloc((ulong)aBufSize);
                        var outData = new byte*[] { aBuf };
                        
                        int outSamples;
                        fixed (byte** pOutData = outData)
                        {
                            outSamples = ffmpeg.swr_convert(swrCtx, pOutData, maxOutSamples, frame->extended_data, frame->nb_samples);
                        }
                        
                        if (outSamples > 0)
                        {
                            int actualBufSize = outSamples * outBytesPerSample * outChannels;
                            var pcmBytes = new byte[actualBufSize];
                            Marshal.Copy((IntPtr)aBuf, pcmBytes, 0, actualBufSize);
                            
                            AudioChannel.Writer.TryWrite(new AudioChunk(pcmBytes, outSampleRate, outChannels));
                        }
                        ffmpeg.av_free(aBuf);
                    }
                }

                ffmpeg.av_packet_unref(pkt);
            }

            // Cleanup
            if (swsCtx != null) ffmpeg.sws_freeContext(swsCtx);
            if (swrCtx != null) ffmpeg.swr_free(&swrCtx);
            ffmpeg.av_frame_free(&frame);
            ffmpeg.av_frame_free(&rgbFrame);
            if (vBuf != null) ffmpeg.av_free(vBuf);
            ffmpeg.av_packet_free(&pkt);
        }

        public void Seek(double seconds)
        {
            Interlocked.Exchange(ref _pendingSeekTarget, seconds);
        }

        private static void ThrowOnError(int ret)
        {
            if (ret < 0)
            {
                var buf = new byte[1024];
                fixed (byte* b = buf)
                    ffmpeg.av_strerror(ret, b, 1024);
                throw new Exception(Encoding.UTF8.GetString(buf).TrimEnd('\0'));
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _decodeThread?.Join(2000);
            
            fixed (AVCodecContext** p = &_vCodecCtx) if (*p != null) ffmpeg.avcodec_free_context(p);
            fixed (AVCodecContext** p = &_aCodecCtx) if (*p != null) ffmpeg.avcodec_free_context(p);
            fixed (AVFormatContext** p = &_fmtCtx) if (*p != null) ffmpeg.avformat_close_input(p);
            
            _cts.Dispose();
        }
    }
}
