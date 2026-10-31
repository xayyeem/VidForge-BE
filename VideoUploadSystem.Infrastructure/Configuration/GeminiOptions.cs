using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoUploadSystem.Infrastructure.Configuration
{
    public sealed class GeminiOptions
    {
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gemini-2.5-flash-image";
        public string BaseUrl { get; set; } =
            "https://generativelanguage.googleapis.com/";
        public string ThumbnailOutputDirectory { get; set; } = "H:\\VideoUploadSystem\\AI thumbnail";
        public int NumberOfImages { get; set; } = 3;
    }
}
