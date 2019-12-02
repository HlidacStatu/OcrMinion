using System;
using System.Collections.Generic;
using System.Text;

namespace HlidacStatu.Service.OCRApi
{
    public abstract class DelayedException: Exception
    {
        public DelayedException(string message) : base(message){}

        public DelayedException(string message, Exception innerException) : base(message, innerException){}

        public int DelayInSec { get; set; }
    }
}
