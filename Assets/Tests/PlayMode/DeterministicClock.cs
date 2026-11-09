using AdventureKing.Gameplay;
using NUnit.Framework;

namespace AdventureKing.Tests
{
    /// <summary>
    /// Every PlayMode test runs at noon in daylight: screenshots and audits must not depend on the real Vietnam
    /// time or on a day/night preference saved on this machine. Tests that check night set it themselves.
    /// </summary>
    [SetUpFixture]
    public sealed class DeterministicClock
    {
        [OneTimeSetUp]
        public void PinDaylight()
        {
            DayNightCycle.ModeOverride = DayNightMode.Day;
            DayNightCycle.HourOverride = 12f;
        }

        [OneTimeTearDown]
        public void Release()
        {
            DayNightCycle.ModeOverride = null;
            DayNightCycle.HourOverride = null;
        }
    }
}
