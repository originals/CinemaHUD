using Blish_HUD;
using System;
using System.Runtime.InteropServices;

namespace CinemaModule.Player
{
    internal class VideoCallbackHandler
    {
        #region Fields

        private static readonly Logger Logger = Logger.GetLogger<VideoCallbackHandler>();

        private readonly VideoBuffer _buffer;
        private volatile bool _frameDirty;
        private volatile bool _lockFailed;

        public bool IsFrameDirty => _frameDirty;
        public bool LockFailed => _lockFailed;

        #endregion

        #region Constructor

        public VideoCallbackHandler(VideoBuffer buffer)
        {
            _buffer = buffer;
        }

        #endregion

        #region LibVLC Callbacks

        public IntPtr LockCallback(IntPtr opaque, IntPtr planes)
        {
            IntPtr bufferPtr = _buffer.BufferPtr;
            
            if (bufferPtr == IntPtr.Zero)
            {
                _lockFailed = true;
                Logger.Debug("LockCallback: Buffer not ready (BufferPtr is zero)");
                return IntPtr.Zero;
            }

            _lockFailed = false;
            Marshal.WriteIntPtr(planes, bufferPtr);
            return IntPtr.Zero;
        }

        public void DisplayCallback(IntPtr opaque, IntPtr picture)
        {
            // Only mark frame dirty if lock succeeded
            if (!_lockFailed)
            {
                _frameDirty = true;
            }
        }

        #endregion

        #region Public Methods

        public void ClearFrameDirty()
        {
            _frameDirty = false;
        }

        public void Reset()
        {
            _frameDirty = false;
            _lockFailed = false;
        }

        #endregion
    }
}
