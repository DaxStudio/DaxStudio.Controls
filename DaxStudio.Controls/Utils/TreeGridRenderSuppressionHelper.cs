using System;

namespace DaxStudio.Controls.Utils
{
    /// <summary>
    /// Helper class to coordinate render suppression across TreeGrid components
    /// </summary>
    internal static class TreeGridRenderSuppressionHelper
    {
        private static bool _isRenderingSuppressed = false;
        
        /// <summary>
        /// Gets or sets whether rendering is currently suppressed
        /// </summary>
        public static bool IsRenderingSuppressed
        {
            get => _isRenderingSuppressed;
            set => _isRenderingSuppressed = value;
        }
    }
}
