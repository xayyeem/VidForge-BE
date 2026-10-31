using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoUploadSystem.Application.DTOs
{
    public sealed class GenerateThumbnailRequest
    {
        [Required]
        [StringLength(2000, MinimumLength = 10)]
        public string Prompt { get; set; } = string.Empty;
    }
}
