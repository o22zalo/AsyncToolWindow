using System;

namespace AsyncToolWindowSample
{
    /// <summary>
    /// Centralised GUID + command-ID constants.
    /// Values must match the GuidSymbol / IDSymbol entries in VSCommandTable.vsct.
    /// </summary>
    internal static class PackageGuids
    {
        public const string PackageGuidString    = "6e3b2e95-902b-4385-a966-30c06ab3c7a6";
        public static readonly Guid PackageGuid  = new Guid(PackageGuidString);

        public const string CommandSetGuidString = "9cc1062b-4c82-46d2-adcb-f5c17d55fb85";
        public static readonly Guid CommandSetGuid = new Guid(CommandSetGuidString);
    }

    /// <summary>
    /// Integer command IDs — must match IDSymbol values in VSCommandTable.vsct.
    /// </summary>
    internal static class PackageIds
    {
        // Existing
        public const int ShowToolWindowId   = 0x0100;

        // §6 dynamic commands
        public const int CmdIdToggleFeature = 0x0200;
        public const int CmdIdDocAction     = 0x0201;
        public const int CmdIdContextInfo   = 0x0202;

        // §Config — Config Editor tool window
        public const int CmdIdConfigEditor  = 0x0300;

        // §Settings — Settings dialog (Tools menu)
        public const int CmdIdSettings      = 0x0400;
    }
}
