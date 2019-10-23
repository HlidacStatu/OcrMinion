namespace HlidacStatu.Service.OCRApi
{
    public class DocumentInfo
    {
        public string ContentType { get; } = "image/jpeg";
        public string Filename { get; set; }
        public string Text { get; set; }
        public string Confidence { get; } = "0.0";
        public bool UsedOCR { get; } = true;
        public int Pages { get; } = 0;
        public string RemainsInSec { get; set; }
        public string UsedTool { get; } = "Tesseract";
        public string Server { get; set; }
    }
}
