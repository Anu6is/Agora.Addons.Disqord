using Emporia.Domain.Common;
using HumanTimeParser.Core.Culture;
using HumanTimeParser.Core.TimeConstructs;
using HumanTimeParser.Core.Tokenization.Tokens;
using HumanTimeParser.English;
using System.Globalization;

namespace Agora.Addons.Disqord.Parsers
{
    public class EmporiumTimeParser : EnglishTimeParser
    {
        private TimeSpan Offset;
        public DateTimeOffset Clock => SystemClock.Now.ToOffset(Offset);        

        public EmporiumTimeParser() : base(new TimeParsingCulture(CultureInfo.InvariantCulture, ClockType.TwentyFourHour)) { }

        public EmporiumTimeParser WithOffset(TimeSpan offset)
        {
            Offset = offset;
            return this;
        }

        protected override bool ParseTimeOfDayToken(TimeOfDayToken timeOfDayToken)
        {
            State.StartingDate = Clock.DateTime;
            
            return base.ParseTimeOfDayToken(timeOfDayToken);
        }

        protected override DateTime ConstructDateTime()
        {
            State.StartingDate = Clock.DateTime;    
            
            return base.ConstructDateTime();
        }
    }
}
