using System.Collections;

namespace XSharpPowerTools.Helpers
{
    public interface IResultComparer : IComparer
    {
        public string SqlOrderBy { get; }
    }
}
