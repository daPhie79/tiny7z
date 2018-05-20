using System;
using System.IO;

namespace pdj.tiny7z.Common
{
    /// <summary>
    /// MultiStream - Allows a multiple number of streams to be treated as one. Stream obtained (and get ownership) from delegate.
    /// </summary>
    public class MultiStream : AbstractMultiStream
    {
        protected override Stream NextStream()
        {
            return onNextStream((ulong)currentIndex);
        }

        protected override void CloseStream()
        {
            base.CloseStream();
            onCloseStream?.Invoke((ulong)currentIndex, internalStream);
        }

        private Func<ulong, Stream> onNextStream;
        private Action<ulong, Stream> onCloseStream;

        public MultiStream(UInt64 numStreams, Func<ulong, Stream> onNextStream, Action<ulong, Stream> onCloseStream = null)
            : base(numStreams)
        {
            this.onNextStream = onNextStream;
            this.onCloseStream = onCloseStream;
        }
    }
}
