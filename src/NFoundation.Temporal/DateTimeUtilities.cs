using NodaTime;
using NodaTime.TimeZones;
using System.Globalization;

namespace NFoundation.Temporal
{
    public static class DateTimeUtilities
    {
        public static DateTime ParseIso8601Utc(string dateTimeString)
        {
            // Examples:
            // 2022-01-01T08:45:22Z
            // 2022-01-01T08:45:22.123Z
            // 2022-01-01T08:45:22.123456Z
            // note: also supports , as the decimal separator

            if (dateTimeString is null)
                throw new ArgumentNullException(nameof(dateTimeString));

            if (dateTimeString.Length < 20)
                throw new Exception($"Cannot parse date/time string '{dateTimeString}'. String must be at least 20 characters in length.");

            if (!dateTimeString.EndsWith("Z"))
                throw new Exception($"Cannot parse date/time string '{dateTimeString}' as UTC, the supplied string does not have a 'Z' suffix.");

            if (dateTimeString[10] != 'T')
                throw new Exception($"Cannot parse date/time string '{dateTimeString}' as UTC, the supplied string does not have a 'T' at position 10.");

            if (dateTimeString[19] == ',')
            {
                var chars = dateTimeString.ToCharArray();
                chars[19] = '.';
                dateTimeString = new string(chars);
            }

            // Convert to DateTime with Kind = Utc, without any conversions based on local system settings. 
            return DateTime.Parse(dateTimeString, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        }

        /// <summary>
        /// <para>Converts a Date/Time or time string to a DateTime type. If the date is not specified, it will be set to today's date</para>
        /// <para>Input string is expected to be in UTC.</para>
        /// </summary>
        /// <param name="s">String to convert</param>
        /// <returns>DateTime in Utc Format</returns>
        public static DateTime ParseUtcTimeStringAsDateTime(string s)
        {
            // Accepts a variey of inputs including:
            //
            // 24 Hour Times
            // 10:05
            // 10:05:06
            // 10:05.06.123456
            //
            // 12 Hour Times
            // 10:05 PM
            // 10:05:06 PM
            // 10:05.06.123456 PM
            //
            // 24 Hour Date/Times (YYYY-MM-DD will be ignored T and Z are optional)
            // YYYY-MM-DD[T]10:05[Z]
            // YYYY-MM-DD[T]10:05:06[Z]
            // YYYY-MM-DD[T]10:05.06.123456[Z]
            //
            // 12 Hour Date/Times
            // 2014-05-12 12:13:14 AM
            //
            // Any other formats may give undefined results

            if (string.IsNullOrEmpty(s))
                throw new ArgumentException("Cannot parse time, the supplied parameter is null or empty");

            // If the date is not specified, it will be set to today's date. When using this to query a database time field, the date part is typically ignored.
            return DateTime.Parse(s, null, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }

        /// <summary>
        /// <para>Converts a Date/Time or time string to a TimeSpan type. Only time information is retained</para>
        /// <para>Input string is expected to be in UTC.</para>
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static TimeSpan ParseUtcTimeStringAsTimeSpan(string s)
        {
            return ParseUtcTimeStringAsDateTime(s).TimeOfDay;
        }

        /// <summary>
        /// Checks whether time zone is a valid IANA TZDB ID
        /// </summary>
        /// <param name="timeZone">Time Zone string in IANA TZ DB format</param>
        /// <param name="canonicalOnly">Specifies whether time zone must be canonical (rejects aliases and deprecated IDs)</param>
        /// <returns></returns>
        public static bool IsValidTimeZone(string timeZone, bool canonicalOnly = false)
        {
            if (timeZone is null)
                throw new ArgumentNullException(nameof(timeZone));

            // Time zone is case sensitive
            if (canonicalOnly)
                return GetCanonicalTzdbIDs().Any(id => id == timeZone);
            else
                return DateTimeZoneProviders.Tzdb.Ids.Any(id => id == timeZone);
        }

        /// <summary>
        /// More info: https://en.m.wikipedia.org/wiki/List_of_tz_database_time_zones
        /// </summary>
        /// <returns>Read only collection of Canonical ID strings</returns>
        public static IEnumerable<string> GetCanonicalTzdbIDs()
        {
            var map = TzdbDateTimeZoneSource.Default.CanonicalIdMap;
            var canonicalIDs = map.Values.Distinct();
            return canonicalIDs;
        }
    }
}
