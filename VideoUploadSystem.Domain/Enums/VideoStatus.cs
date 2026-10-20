using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoUploadSystem.Domain.Enums
{
    public enum VideoStatus
    {
        Uploading = 1,
        Uploaded = 2,
        Processing = 3,
        Ready = 4,
        Failed = 5
    }
}
