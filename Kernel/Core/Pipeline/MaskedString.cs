namespace Core.Primitives
{
    /// <summary>
    /// Masking style
    /// </summary>
    public enum MaskingStyle 
    { 
        /// <summary>
        /// Mask all
        /// </summary>
        All, 

        /// <summary>
        /// Show first 4 characters
        /// </summary>
        ShowFirst4, 

        /// <summary>
        /// Show last 4 characters
        /// </summary>
        ShowLast4, 

        /// <summary>
        /// Email pattern mask
        /// </summary>
        Email 
    }

    /// <summary>
    /// A safe string wrapper that masks content by default but preserves format utility.
    /// </summary>
    public readonly struct MaskedString(string value, MaskingStyle style = MaskingStyle.All)
    {
        private readonly string _value = value ?? string.Empty;
        private readonly MaskingStyle _style = style;

        /// <summary>
        /// Display masked data
        /// </summary>
        /// <returns></returns>
        public string Unveil() => _value;

        /// <summary>
        /// Cast to string
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (string.IsNullOrEmpty(_value)) return "";

            return _style switch
            {
                MaskingStyle.ShowLast4 => _value.Length > 4
                    ? string.Concat(new string('*', _value.Length - 4), _value.AsSpan(_value.Length - 4))
                    : "****",
                MaskingStyle.ShowFirst4 => _value.Length > 4
                    ? _value[..4] + new string('*', _value.Length - 4)
                    : "****",
                MaskingStyle.Email => MaskEmail(_value),
                _ => "********"
            };
        }

        private static string MaskEmail(string email)
        {
            var atIndex = email.IndexOf('@');
            if (atIndex <= 1) return "****@****.com";
            return email[..2] + "****" + email[atIndex..];
        }

        /// <summary>
        /// Mask string
        /// </summary>
        /// <param name="value"></param>
        public static implicit operator MaskedString(string value) => new(value);
    }
}