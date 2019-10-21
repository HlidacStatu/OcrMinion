using System;

namespace OcrMinion
{
    internal class HlidacTask
    {
        //{"TaskId":"00000000-0000-0000-0000-000000000000","Priority":5,"Intensity":0,"OrigFilename":"testfile.jpg","localTempFile":null}
        public string TaskId { get; set; }
        public int Priority { get; set; }
        public int Intensity { get; set; }
        public string OrigFileName { get; set; }
        public string LocalTempFile { get; set; }
        public string InternalFileName { get; } = Guid.NewGuid().ToString();
        public bool IsValid { get; set; } = true;
    }
}