using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace XSharpPowerTools.Helpers
{
    public interface IResultComparer : IComparer
    {
        public string SqlOrderBy { get; }
    }
}
