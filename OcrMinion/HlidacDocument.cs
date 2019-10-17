using System;
using System.Collections.Generic;
using System.Text;

namespace OcrMinion
{
    class HlidacDocument
    {
        public string Id { get; set; }
        public DocumentInfo[] Documents { get; set; }
        public string Server { get; set; }
        public DateTime Started { get; set; }
        public DateTime Ends { get; set; }
        public int IsValid { get; set; }
        public string Error { get; set; }

    }

    class DocumentInfo
    {
        public string ContentType { get; set; } = "image/jpeg";
        public string Filename { get; set; }
        public string Text { get; set; }
        public string Confidence { get; set; } = "0.0";
        public bool UsedOCR { get; set; } = true;
        public int Pages { get; set; } = 0;
        public string RemainsInSec { get; set; }
        public string UsedTool { get; set; } = "Tesseract";
        public string Server { get; set; }
    }
}

/*
{  
   "Id”:"00000000-0000-0000-0000-000000000000",
   "Documents":[
      {  
         "ContentType":"image/jpeg",
         "Filename":"_!_img3.jpg",
         "Text":" \n\nKB\n\n \n\nČíslo účtu | 107-5493970277 /0100|\n\n \n\nKomerční banka, a.s., ",
         "Confidence":0.0,
         "UsedOCR":true,
         "Pages":0,
         "RemainsInSec":0.0,
         "UsedTool”:”Tesseract",
         "Server”:”DockerXYZ"
      }
   ],
   "Server”:”DockerXYZ",
   "Started":"2019-09-24T03:07:00.4195551+02:00", začátek tesseractu
   "Ends":"2019-09-24T03:07:30.1851238+02:00", konec tesseractu
   "IsValid":1,
   "Error":null
}

k JSON: 
- Documents.Text - ziskany text z Tesseract
- Documents.Confidence - nekdy vraci Tesseract
- Documents.UsedOCR - vzdyt true
- Documents.Pages - vzdy 0
- Documents.RemainsInSec: doba v sekundach, jak dlouho task bezel
- Documents.UsedTool: ’Tesseract'
- IsValid: pokud vse ok, pak 1. Jinak 0
- Error: pokud nastala chyba, pak sem chybova hlaska
*/