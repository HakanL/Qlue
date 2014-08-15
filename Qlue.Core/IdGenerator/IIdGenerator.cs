using System;

namespace Qlue
{
    public interface IIdGenerator
    {
        long GetNextId(string scopeName);
    }
}
