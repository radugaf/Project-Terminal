using System;
using ProjectTerminal.Globals.Interfaces;

namespace ProjectTerminal.Globals.Services
{
    public class SystemTimeProvider : ITimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
