using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.ProjectSystem.CSharp.VS
{
    public struct IsValidValueResult
    {
        public string ErrorMessage { get; set; }
        public bool IsValid { get; set; }
    }
}
