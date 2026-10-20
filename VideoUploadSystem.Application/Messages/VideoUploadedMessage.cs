using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoUploadSystem.Application.Messages
{
    public class VideoUploadedMessage
    {
        public Guid VideoId { get; set; }
        public string StoragePath { get; set; }
        public string FileName { get; set; }
    }
}
