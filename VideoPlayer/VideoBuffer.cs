using System;
using System.Runtime.InteropServices;
using Blish_HUD;
using Microsoft.Xna.Framework.Graphics;

namespace CinemaModule.Player
{
    /// <summary>
    /// Manages the raw video frame buffer for LibVLC callbacks.
    /// Handles memory pinning and texture copying with pitch correction.
    /// Thread-safe for concurrent access from LibVLC and game threads.
    /// </summary>
    internal class VideoBuffer : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<VideoBuffer>();

        private readonly object _lock = new object();
        private byte[] _rawBuffer;
        private byte[] _textureBuffer;
        private GCHandle _bufferHandle;
        private bool _isAllocated;
        private volatile IntPtr _bufferPtr;

        public IntPtr BufferPtr => _bufferPtr;

        public void Allocate(int size)
        {
            if (size <= 0)
            {
                Logger.Warn($"Allocate called with invalid size: {size}");
                return;
            }

            lock (_lock)
            {
                // Allocate new buffer BEFORE freeing old one to minimize the window
                // where BufferPtr is zero during format changes
                var newBuffer = new byte[size];
                var newHandle = GCHandle.Alloc(newBuffer, GCHandleType.Pinned);
                var newPtr = newHandle.AddrOfPinnedObject();

                // Now free the old buffer
                if (_bufferHandle.IsAllocated)
                {
                    _bufferHandle.Free();
                }

                // Swap to new buffer atomically
                _rawBuffer = newBuffer;
                _bufferHandle = newHandle;
                _bufferPtr = newPtr;
                _textureBuffer = null; // Clear texture buffer to force recreation with new size
                _isAllocated = true;
                
                Logger.Debug($"Buffer allocated: {size} bytes, ptr={newPtr}");
            }
        }

        public void Free()
        {
            lock (_lock)
            {
                _bufferPtr = IntPtr.Zero;
                
                if (_bufferHandle.IsAllocated)
                {
                    _bufferHandle.Free();
                }

                _rawBuffer = null;
                _textureBuffer = null;
                _isAllocated = false;
            }
        }

        public void CopyToTextureWithPitch(Texture2D texture, int sourceWidth, int sourceHeight, uint pitch)
        {
            if (texture == null || texture.IsDisposed)
            {
                return;
            }

            byte[] localRawBuffer;
            byte[] localTextureBuffer;
            int textureRowBytes;
            int copyHeight;
            int copyRowBytes;
            int sourcePitch = (int)pitch;
            bool needsPitchConversion;

            lock (_lock)
            {
                if (_rawBuffer == null || !_isAllocated)
                {
                    return;
                }

                int textureWidth = texture.Width;
                int textureHeight = texture.Height;
                textureRowBytes = textureWidth * 4;
                int expectedSize = textureRowBytes * textureHeight;

                int copyWidth = Math.Min(textureWidth, sourceWidth);
                copyHeight = Math.Min(textureHeight, sourceHeight);
                copyRowBytes = copyWidth * 4;

                if (_textureBuffer == null || _textureBuffer.Length != expectedSize)
                {
                    _textureBuffer = new byte[expectedSize];
                }

                localRawBuffer = _rawBuffer;
                localTextureBuffer = _textureBuffer;
                needsPitchConversion = sourcePitch != textureRowBytes;
            }

            if (needsPitchConversion)
            {
                int maxSrcOffset = copyHeight * sourcePitch;
                int maxDstOffset = copyHeight * textureRowBytes;
                
                if (maxSrcOffset <= localRawBuffer.Length && maxDstOffset <= localTextureBuffer.Length)
                {
                    for (int row = 0; row < copyHeight; row++)
                    {
                        Buffer.BlockCopy(localRawBuffer, row * sourcePitch, localTextureBuffer, row * textureRowBytes, copyRowBytes);
                    }
                }
            }
            else
            {
                // Fast path: pitch matches, single copy
                int totalBytes = copyHeight * textureRowBytes;
                if (totalBytes <= localRawBuffer.Length && totalBytes <= localTextureBuffer.Length)
                {
                    Buffer.BlockCopy(localRawBuffer, 0, localTextureBuffer, 0, totalBytes);
                }
            }

            try
            {
                texture.SetData(localTextureBuffer);
            }
            catch (ObjectDisposedException)
            {
                // Texture was disposed between check and SetData
            }
        }


        public void Dispose()
        {
            Free();
        }
    }
}
