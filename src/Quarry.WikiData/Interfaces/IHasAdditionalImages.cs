using System;
using System.Collections.Generic;
using System.Text;

namespace Quarry.WikiData.Interfaces
{
    public interface IHasAdditionalImages
    {
        List<string> AdditionalImages { get; set; }
    }
}
