using ManagedBass;
using System.Runtime.Serialization;

namespace Woofer.Core.Modules.AudioPlayerModule.Exceptions
{
    [Serializable]
    internal class BassException : Exception
    {
        private Errors BassError { get; set; }

        public BassException()
        {
        }

        public BassException(Errors bassStatus) : base(bassStatus.ToString())
        {
            BassError = bassStatus;
        }

        public BassException(string? message) : base(message)
        {
        }

        public BassException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected BassException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}