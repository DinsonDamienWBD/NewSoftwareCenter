namespace Core.Configuration
{
    /// <summary>
    /// Implementing this allows the Kernel to automatically inject settings from the DW.
    /// </summary>
    public interface IConfigurable<TSettings> where TSettings : class, new()
    {
        /// <summary>
        /// Applies the provided settings to the implementing class.
        /// </summary>
        /// <param name="settings"></param>
        void ApplySettings(TSettings settings);
    }
}