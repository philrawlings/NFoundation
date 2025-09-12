using NFoundation.Temporal;

namespace NFoundation.Test.Temporal
{
    public class DateTimeUtilitiesTests
    {
        [Fact]
        public void DateTimeStartTests()
        {
            DateTime dt = new DateTime(2022, 11, 12, 12, 45, 22, DateTimeKind.Utc);
            DateTime startOfWeek = dt.GetStartOfWeek(DayOfWeek.Monday);
            Assert.Equal(new DateTime(2022, 11, 7), startOfWeek);

            startOfWeek = dt.GetStartOfWeek(DayOfWeek.Sunday);
            Assert.Equal(new DateTime(2022, 11, 6), startOfWeek);

            DateTime startOfMonth = dt.GetStartOfMonth();
            Assert.Equal(new DateTime(2022, 11, 1), startOfMonth);

            DateTime startOfQuarter = dt.GetStartOfQuarter();
            Assert.Equal(new DateTime(2022, 10, 1), startOfQuarter);
        }

        [Fact]
        public void ParseDateTimeTests()
        {
            DateTime dt = DateTimeUtilities.ParseIso8601Utc("2022-05-30T12:45:22.123456Z");
            Assert.Equal(new DateTime(2022, 05, 30, 12, 45, 22, DateTimeKind.Utc).AddTicks(1234560), dt);

            dt = DateTimeUtilities.ParseIso8601Utc("2022-05-30T12:45:22,123456Z"); // Different culture
            Assert.Equal(new DateTime(2022, 05, 30, 12, 45, 22, DateTimeKind.Utc).AddTicks(1234560), dt);

            dt = DateTimeUtilities.ParseIso8601Utc("2023-01-23T14:23:33.783Z");
            Assert.Equal(new DateTime(2023, 01, 23, 14, 23, 33, DateTimeKind.Utc).AddTicks(7830000), dt);

            dt = DateTimeUtilities.ParseIso8601Utc("2023-01-23T14:23:33,783Z"); // Different culture
            Assert.Equal(new DateTime(2023, 01, 23, 14, 23, 33, DateTimeKind.Utc).AddTicks(7830000), dt);

            dt = DateTimeUtilities.ParseIso8601Utc("2023-02-07T10:31:00Z");
            Assert.Equal(new DateTime(2023, 02, 07, 10, 31, 00, DateTimeKind.Utc), dt);
        }
    }
}
