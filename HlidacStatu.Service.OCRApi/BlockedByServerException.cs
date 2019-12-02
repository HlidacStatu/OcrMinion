using System;
using System.Collections.Generic;
using System.Text;

namespace HlidacStatu.Service.OCRApi
{
    public class BlockedByServerException : DelayedException
    {
        public BlockedByServerException(string message, int delayInSec) : base(message)
        {
            DelayInSec = delayInSec;
        }
        //public BlockedByServerException(string message) : base(message){}
        //public BlockedByServerException(string message, Exception innerException) : base(message, innerException){}
        
    }
}
