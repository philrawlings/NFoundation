namespace NFoundation.Temporal
{
    public class DateTimeRange : IEquatable<DateTimeRange>, ICloneable
    {
        public DateTime StartTimestampUtc { get; set; }
        public DateTime EndTimestampUtc { get; set; }

        public override int GetHashCode()
        {
            return HashCode.Combine(StartTimestampUtc, EndTimestampUtc);
        }

        public override bool Equals(object? obj)
        {
            if (obj is null)
                return false;

            DateTimeRange? dtRange = obj as DateTimeRange;
            if (dtRange is null)
                return false;
            else
                return Equals(dtRange);
        }

        public bool Equals(DateTimeRange? other)
        {
            if (other is null)
                return false;

            if (this.StartTimestampUtc == other.StartTimestampUtc &&
                this.EndTimestampUtc == other.EndTimestampUtc)
                return true;
            else
                return false;
        }

        public static bool operator ==(DateTimeRange? item1, DateTimeRange? item2)
        {
            if (item1 is null || (item2) is null)
                return object.Equals(item1, item2);

            return item1.Equals(item2);
        }

        public static bool operator !=(DateTimeRange? item1, DateTimeRange? item2)
        {
            if (item1 is null || item2 is null)
                return !object.Equals(item1, item2);

            return !(item1.Equals(item2));
        }

        public List<DateTimeRange> GetIntervalDateTimeRanges(DateTimeInterval dtInterval)
        {
            List<DateTimeRange> dtRanges = new List<DateTimeRange>();

            DateTime dt = this.StartTimestampUtc;

            do
            {
                DateTime intervalStart = dt.GetIntervalStart(dtInterval);
                DateTime intervalEnd = intervalStart.GetNextIntervalStart(dtInterval);

                dtRanges.Add(new DateTimeRange
                {
                    StartTimestampUtc = intervalStart,
                    EndTimestampUtc = intervalEnd
                });

                dt = intervalEnd;

            } while (dt < this.EndTimestampUtc);

            return dtRanges;
        }

        public override string ToString()
        {
            return StartTimestampUtc.ToString() + " - " + EndTimestampUtc.ToString();
        }

        public object Clone()
        {
            return new DateTimeRange
            {
                StartTimestampUtc = this.StartTimestampUtc,
                EndTimestampUtc = this.EndTimestampUtc
            };
        }
    }
}
