using System.Runtime.Serialization;
using Un4seen.Bass;

namespace Woofer.Core.Exceptions
{
    [Serializable]
    internal class BassException : Exception
    {
        private BASSError BassStatus { get; set; }

        public BassException()
        {
        }

        public BassException(BASSError bassStatus) : base(bassStatus.ToString())
        {
            BassStatus = bassStatus;
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