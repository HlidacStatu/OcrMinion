using System;

namespace HlidacStatu.Service.OCRApi
{
    public class ServerHasNoTasksException : DelayedException
    {
        public ServerHasNoTasksException(int delayInSec) : base("Server does not have any content to parse at this moment.")
        {
            DelayInSec = delayInSec;
        }
    }
}
