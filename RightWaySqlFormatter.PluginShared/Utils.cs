using PoorMansTSqlFormatterLib;
using PoorMansTSqlFormatterLib.Formatters;

namespace PoorMansTSqlFormatterPluginShared
{
    public static class Utils
    {
        /// <summary>
        /// Creates a <see cref="SqlFormattingManager"/> from the persisted options string in settings.
        /// </summary>
        public static SqlFormattingManager GetFormattingManager(ISqlSettings settings)
        {
            var options = new TSqlStandardFormatterOptions(settings.OptionsSerialized);
            return new SqlFormattingManager(new TSqlStandardFormatter(options));
        }
    }
}
