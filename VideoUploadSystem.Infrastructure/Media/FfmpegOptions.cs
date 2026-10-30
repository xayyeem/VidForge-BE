using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoUploadSystem.Infrastructure.Media
{
    public class FfmpegOptions
    {
        public string FfmpegPath { get; set; } = string.Empty;
        public string FfprobePath { get; set; } = string.Empty;
        public string ThumbnailOutputDirectory { get; set; } = string.Empty;
    }
}
