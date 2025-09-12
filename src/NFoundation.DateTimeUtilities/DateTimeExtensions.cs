using NodaTime;

namespace NFoundation
{
    public static class DateTimeExtensions
    {
        public static DateTime Round(this DateTime dateTime, TimeSpan span)
        {
            long ticks = (dateTime.Ticks + (span.Ticks / 2) + 1) / span.Ticks;
            return new DateTime(ticks * span.Ticks, dateTime.Kind);
        }

        public static DateTime Floor(this DateTime dateTime, TimeSpan span)
        {
            long ticks = (dateTime.Ticks / span.Ticks);
            return new DateTime(ticks * span.Ticks, dateTime.Kind);
        }

        public static DateTime Ceiling(this DateTime dateTime, TimeSpan span)
        {
            long ticks = (dateTime.Ticks + span.Ticks - 1) / span.Ticks;
            return new DateTime(ticks * span.Ticks, dateTime.Kind);
        }

        public static string ToUtcIso8601String(this DateTime dateTime)
        {
            // To Universal Time assumes local if DateTime.Kind is unspecified. If already Utc then time is not converted.
            return dateTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ");
        }

        public static string ToIso8601String(this DateTime dateTime)
        {
            if (dateTime.Kind == DateTimeKind.Utc)
                return ToUtcIso8601String(dateTime);
            else
                return dateTime.ToString("yyyy-MM-ddTHH:mm:ss.ffffff");
        }

        public static string ToUtcIso8601DateString(this DateTime dateTime)
        {
            return dateTime.ToUniversalTime().Date.ToString("yyyy-MM-dd");
        }

        public static string ToIso8601DateString(this DateTime dateTime)
        {
            if (dateTime.Kind == DateTimeKind.Utc)
                return ToUtcIso8601DateString(dateTime);
            else
                return dateTime.Date.ToString("yyyy-MM-dd");
        }

        public static DateTime GetStartOfWeek(this DateTime dateTime, DayOfWeek startOfWeek)
        {
            int diff = (7 + (dateTime.DayOfWeek - startOfWeek)) % 7;
            return dateTime.AddDays(-1 * diff).Date;
        }

        public static DateTime GetStartOfMonth(this DateTime dateTime)
        {
            return dateTime.Date.AddDays(-1 * (dateTime.Day - 1));
        }

        public static DateTime GetStartOfQuarter(this DateTime dateTime)
        {
            int quarterIndex = (dateTime.Month - 1) / 3;
            return new DateTime(dateTime.Year, (quarterIndex * 3) + 1, 1, 0, 0, 0, dateTime.Kind);
        }

        public static DateTime GetStartOfHalf(this DateTime dateTime)
        {
            int startMonth = (dateTime.Month <= 6) ? 1 : 7;
            return new DateTime(dateTime.Year, startMonth, 1, 0, 0, 0, dateTime.Kind);
        }

        public static DateTime GetStartOfYear(this DateTime dateTime)
        {
            return new DateTime(dateTime.Year, 1, 1, 0, 0, 0, dateTime.Kind);
        }

        public static DateTime ConvertFromTimeZoneToUtc(this DateTime dateTime, string timeZone)
        {
            if (dateTime.Kind == DateTimeKind.Utc)
                return dateTime;
            else
            {
                var localtime = LocalDateTime.FromDateTime(dateTime);
                DateTimeZone zone = DateTimeZoneProviders.Tzdb[timeZone];
                var zonedtime = localtime.InZoneLeniently(zone);
                return zonedtime.ToInstant().InZone(zone).ToDateTimeUtc();
            }
        }

        public static DateTime ConvertFromUtcToTimeZone(this DateTime dateTime, string timeZone)
        {
            if (dateTime.Kind != DateTimeKind.Utc)
                throw new ArgumentException($"{nameof(dateTime)} is not a UTC DateTime (Kind property is not Utc).");

            var instant = Instant.FromDateTimeUtc(dateTime);
            var zonedLocal = instant.InZone(DateTimeZoneProviders.Tzdb[timeZone]);
            return zonedLocal.ToDateTimeUnspecified();
        }

        /// <summary>
        /// Every workweek has its own number. 
        /// In Europe this is very often used. 
        /// There is an ISO 8601 standard which describes how this number is calculated: 
        /// Week 01 of a year is, by definition, the first week that has the calendar year's first Thursday in it. 
        /// This is equivalent to the week that contains the fourth day of January. 

        /// </summary>
        /// <param name="fromDate"></param>
        /// <returns></returns>
        public static int GetIso8601WeekNumber(this DateTime dateTime)
        {
            // Get jan 1st of the year
            DateTime startOfYear = dateTime.AddDays(-dateTime.Day + 1).AddMonths(-dateTime.Month + 1);
            // Get dec 31st of the year
            DateTime endOfYear = startOfYear.AddYears(1).AddDays(-1);
            // ISO 8601 weeks start with Monday 
            // The first week of a year includes the first Thursday 
            // DayOfWeek returns 0 for sunday up to 6 for saterday
            int[] iso8601Correction = { 6, 7, 8, 9, 10, 4, 5 };
            int nds = dateTime.Subtract(startOfYear).Days + iso8601Correction[(int)startOfYear.DayOfWeek];
            int wk = nds / 7;
            switch (wk)
            {
                case 0:
                    // Return weeknumber of dec 31st of the previous year
                    return GetIso8601WeekNumber(startOfYear.AddDays(-1));
                case 53:
                    // If dec 31st falls before thursday it is week 01 of next year
                    if (endOfYear.DayOfWeek < DayOfWeek.Thursday)
                        return 1;
                    else
                        return wk;
                default: return wk;
            }
        }

        public static int GetIso8601YearNumber(this DateTime calendarDate)
        {
            int isoYear;

            int calendarMonthNumber = calendarDate.Month;
            int calendarYearNumber = calendarDate.Year;

            if ((calendarMonthNumber == 1) && (GetIso8601WeekNumber(calendarDate) > 51))
                isoYear = calendarYearNumber - 1;
            else if ((calendarMonthNumber == 12) && (GetIso8601WeekNumber(calendarDate) == 1))
                isoYear = calendarYearNumber + 1;
            else isoYear = calendarYearNumber;

            return isoYear;
        }

        public static string GetIntervalName(this DateTime dateTime, DateTimeInterval interval)
        {
            DateTime intervalStart = dateTime.GetIntervalStart(interval);

            switch (interval)
            {
                case DateTimeInterval.Second:
                    return (string.Format("{0:D4}-{1:D2}-{2:D2} {3:D2}:{4:D2}:{5:D2}", intervalStart.Year, intervalStart.Month, intervalStart.Day, intervalStart.Hour, intervalStart.Minute, intervalStart.Second));
                case DateTimeInterval.Minute:
                    return (string.Format("{0:D4}-{1:D2}-{2:D2} {3:D2}:{4:D2}", intervalStart.Year, intervalStart.Month, intervalStart.Day, intervalStart.Hour, intervalStart.Minute));
                case DateTimeInterval.Hour:
                    return (string.Format("{0:D4}-{1:D2}-{2:D2} {3:D2}:00", intervalStart.Year, intervalStart.Month, intervalStart.Day, intervalStart.Hour));
                case DateTimeInterval.Day:
                    return (string.Format("{0:D4}-{1:D2}-{2:D2}", intervalStart.Year, intervalStart.Month, intervalStart.Day));
                case DateTimeInterval.Week:
                    return (string.Format("{0:D4} W{1:D2}", DateTimeExtensions.GetIso8601YearNumber(intervalStart), DateTimeExtensions.GetIso8601WeekNumber(intervalStart)));
                case DateTimeInterval.Month:
                    return (string.Format("{0:D4}-{1:D2}", intervalStart.Year, intervalStart.Month));
                case DateTimeInterval.Quarter:
                    {
                        int quarter = ((dateTime.Month - 1) / 3) + 1;
                        return (string.Format("{0:D4} Q{1}", intervalStart.Year, quarter));
                    }
                case DateTimeInterval.Half:
                    {
                        int half = ((dateTime.Month - 1) / 6) + 1;
                        return (string.Format("{0:D4} H{1}", intervalStart.Year, half));
                    }
                case DateTimeInterval.Year:
                    return (string.Format("{0:D4}", intervalStart.Year));
                default:
                    throw new Exception("Unsupported interval '" + interval.ToString() + "'");
            }

        }

        public static DateTime GetIntervalStart(this DateTime dateTime, DateTimeInterval interval)
        {
            switch (interval)
            {
                case DateTimeInterval.Second:
                    return dateTime.Floor(new TimeSpan(0, 0, 1));
                case DateTimeInterval.Minute:
                    return dateTime.Floor(new TimeSpan(0, 1, 0));
                case DateTimeInterval.Hour:
                    return dateTime.Floor(new TimeSpan(1, 0, 0));
                case DateTimeInterval.Day:
                    return dateTime.Floor(new TimeSpan(1, 0, 0, 0));
                case DateTimeInterval.Week:
                    return dateTime.GetStartOfWeek(DayOfWeek.Monday); // ISO8601 Business week starts on Monday
                case DateTimeInterval.Month:
                    return new DateTime(dateTime.Year, dateTime.Month, 1, 0, 0, 0, dateTime.Kind);
                case DateTimeInterval.Quarter:
                    {
                        int quarterIndex = ((dateTime.Month - 1) / 3);
                        return new DateTime(dateTime.Year, (quarterIndex * 3) + 1, 1, 0, 0, 0, dateTime.Kind);
                    }
                case DateTimeInterval.Half:
                    {
                        int halfIndex = ((dateTime.Month - 1) / 6);
                        return new DateTime(dateTime.Year, (halfIndex * 6) + 1, 1, 0, 0, 0, dateTime.Kind);
                    }
                case DateTimeInterval.Year:
                    return new DateTime(dateTime.Year, 1, 1, 0, 0, 0, dateTime.Kind);
                default:
                    throw new Exception("Unsupported interval '" + interval.ToString() + "'");
            }
        }

        public static DateTime GetNextIntervalStart(this DateTime dateTime, DateTimeInterval interval)
        {
            switch (interval)
            {
                case DateTimeInterval.Second:
                    return dateTime.AddSeconds(1).GetIntervalStart(interval);
                case DateTimeInterval.Minute:
                    return dateTime.AddMinutes(1).GetIntervalStart(interval);
                case DateTimeInterval.Hour:
                    return dateTime.AddHours(1).GetIntervalStart(interval);
                case DateTimeInterval.Day:
                    return dateTime.AddDays(1).GetIntervalStart(interval);
                case DateTimeInterval.Week:
                    return dateTime.AddDays(7).GetIntervalStart(interval);
                case DateTimeInterval.Month:
                    return dateTime.AddMonths(1).GetIntervalStart(interval);
                case DateTimeInterval.Quarter:
                    return dateTime.AddMonths(3).GetIntervalStart(interval);
                case DateTimeInterval.Half:
                    return dateTime.AddMonths(6).GetIntervalStart(interval);
                case DateTimeInterval.Year:
                    return dateTime.AddYears(1).GetIntervalStart(interval);
                default:
                    throw new Exception("Unsupported interval '" + interval.ToString() + "'");
            }
        }

        public static DateTimeRange GetInterval(this DateTime dateTime, DateTimeInterval interval)
        {
            return new DateTimeRange
            {
                StartTimestampUtc = GetIntervalStart(dateTime, interval),
                EndTimestampUtc = GetNextIntervalStart(dateTime, interval)
            };
        }
    }
}
