namespace Loupedeck.HaTTaPticPlugin
{
    /// <summary>
    /// Required by the Loupedeck Plugin SDK.
    /// HaTTaPtic has no associated application — it runs standalone.
    /// </summary>
    public class HaTTaPticApplication : ClientApplication
    {
        public HaTTaPticApplication()
        {
        }

        protected override string GetProcessName() => "";

        protected override string GetBundleName() => "";
    }
}
