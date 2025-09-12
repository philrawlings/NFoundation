using NFoundation.Temporal;

namespace NFoundation.Test.Temporal
{
    public class DateTimeExtensionsTests
    {
        [Fact]
        public void GetNextHourIntervalTest()
        {
            DateTime dt = new DateTime(2022, 10, 12, 12, 45, 22, DateTimeKind.Utc);
            DateTime nextDt = dt.GetNextIntervalStart(DateTimeInterval.Hour);
            Assert.Equal(new DateTime(2022, 10, 12, 13, 00, 00, DateTimeKind.Utc), nextDt);

            dt = new DateTime(2022, 10, 12, 12, 0, 0, DateTimeKind.Utc);
            nextDt = dt.GetNextIntervalStart(DateTimeInterval.Hour);
            Assert.Equal(new DateTime(2022, 10, 12, 13, 00, 00, DateTimeKind.Utc), nextDt);
        }

        [Fact]
        public void GetNextDayIntervalTest()
        {
            DateTime dt = new DateTime(2022, 10, 12, 12, 45, 22, DateTimeKind.Utc);
            DateTime nextDt = dt.GetNextIntervalStart(DateTimeInterval.Day);
            Assert.Equal(new DateTime(2022, 10, 13, 00, 00, 00, DateTimeKind.Utc), nextDt);

            dt = new DateTime(2022, 10, 17, 0, 0, 0, DateTimeKind.Utc);
            nextDt = dt.GetNextIntervalStart(DateTimeInterval.Day);
            Assert.Equal(new DateTime(2022, 10, 18, 00, 00, 00, DateTimeKind.Utc), nextDt);
        }

        [Fact]
        public void GetNextWeekIntervalTest()
        {
            DateTime dt = new DateTime(2022, 10, 12, 12, 45, 22, DateTimeKind.Utc);
            DateTime nextDt = dt.GetNextIntervalStart(DateTimeInterval.Week);
            Assert.Equal(new DateTime(2022, 10, 17, 00, 00, 00, DateTimeKind.Utc), nextDt);

            dt = new DateTime(2022, 10, 17, 0, 0, 0, DateTimeKind.Utc);
            nextDt = dt.GetNextIntervalStart(DateTimeInterval.Week);
            Assert.Equal(new DateTime(2022, 10, 24, 00, 00, 00, DateTimeKind.Utc), nextDt);
        }

        [Fact]
        public void GetNextMonthIntervalTest()
        {
            DateTime dt = new DateTime(2022, 10, 12, 12, 45, 22, DateTimeKind.Utc);
            DateTime nextDt = dt.GetNextIntervalStart(DateTimeInterval.Month);
            Assert.Equal(new DateTime(2022, 11, 01, 00, 00, 00, DateTimeKind.Utc), nextDt);

            dt = new DateTime(2022, 10, 01, 0, 0, 0, DateTimeKind.Utc);
            nextDt = dt.GetNextIntervalStart(DateTimeInterval.Month);
            Assert.Equal(new DateTime(2022, 11, 01, 00, 00, 00, DateTimeKind.Utc), nextDt);
        }

        [Fact]
        public void GetNextQuarterIntervalTest()
        {
            DateTime dt = new DateTime(2022, 11, 12, 12, 45, 22, DateTimeKind.Utc);
            DateTime nextDt = dt.GetNextIntervalStart(DateTimeInterval.Quarter);
            Assert.Equal(new DateTime(2023, 01, 01, 00, 00, 00, DateTimeKind.Utc), nextDt);

            dt = new DateTime(2022, 10, 01, 0, 0, 0, DateTimeKind.Utc);
            nextDt = dt.GetNextIntervalStart(DateTimeInterval.Quarter);
            Assert.Equal(new DateTime(2023, 01, 01, 00, 00, 00, DateTimeKind.Utc), nextDt);
        }
    }
}
