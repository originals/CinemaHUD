using System;
using System.Runtime.InteropServices;
using Blish_HUD;

namespace CinemaModule.Player
{
    /// <summary>
    /// Handles LibVLC video format negotiation callbacks.
    /// Configures the video format (RGBA) and allocates the frame buffer.
    /// </summary>
    internal class VideoFormatHandler
    {
        private static readonly Logger Logger = Logger.GetLogger<VideoFormatHandler>();
        private static readonly byte[] RgbaChroma = System.Text.Encoding.ASCII.GetBytes("RGBA");

        private readonly VideoBuffer _buffer;
        private bool _isInitialized;

        public bool IsInitialized => _isInitialized;
        public uint Width { get; private set; }
        public uint Height { get; private set; }
        public uint Pitch { get; private set; }

        public VideoFormatHandler(VideoBuffer buffer)
        {
            _buffer = buffer;
        }

        public uint HandleFormatCallback(ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines)
        {
            Marshal.Copy(RgbaChroma, 0, chroma, 4);

            pitches = width * 4;
            lines = height;

            Width = width;
            Height = height;
            Pitch = pitches;

            int size = (int)(pitches * lines);
            _buffer.Allocate(size);

            _isInitialized = true;

            return 1;
        }

        public void Reset()
        {
            _isInitialized = false;
            Width = 0;
            Height = 0;
            Pitch = 0;
        }
    }
}
