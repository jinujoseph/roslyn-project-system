//--------------------------------------------------------------------------------------------
// StringExtensions
//
// Extensions to the string class.
//
// Copyright(c) 2014 Microsoft Corporation
//--------------------------------------------------------------------------------------------
using System;
using System.IO;
using System.Text;

namespace Microsoft.VisualStudio.ProjectSystem.DotNet.Utilities
{
    public static class StringExtensions
    {
        /// <summary>
        /// Makes sure the string has a trailing backslash
        /// </summary>
        public static string EnsureTrailingBackslash(this string s)
        {
            return s.EnsureTrailingChar('\\');
        }

        /// <summary>
        /// Gets rid of a trailing backslash
        /// </summary>
        public static string RemoveTrailingBackslash(this string s)
        {
            return s.RemoveTrailingChar('\\');
        }

        /// <summary>
        /// Ensures the given string is wrapped in double quotes.
        /// </summary>
        public static string WrapInDoubleQuotes(this string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "\"\"";
            }                
            else
            {
                return s.EnsureStartingChar('\"').EnsureTrailingChar('\"');
            }
        }

        /// <summary>
        /// Removes leading and trailng quotes. Only does so if both are present
        /// </summary>
        public static string UnquoteString(this string s)
        {
            if(s.StartsWith("\"", StringComparison.Ordinal) && s.EndsWith("\"", StringComparison.Ordinal))
            {
                return s.Substring(1, s.Length-2);
            }

            return s;
        }

        /// <summary>
        /// Makes sure the string has the trailing character
        /// </summary>
        public static string EnsureTrailingChar(this string s, char ch)
        {
            if (s.Length == 0 || s[s.Length - 1] != ch)
            {
                return s + ch;
            }

            return s;
        }

        /// <summary>
        /// Makes sure the string has the starting character
        /// </summary>
        public static string EnsureStartingChar(this string s, char ch)
        {
            if (s.Length == 0 || s[0] != ch)
            {
                return ch + s;
            }

            return s;
        }

        /// <summary>
        /// Gets rid of the trailing char
        /// </summary>
        public static string RemoveTrailingChar(this string s, char ch)
        {
            if (s.Length > 0 && s[s.Length - 1] == ch)
            {
                return s.Substring(0, s.Length - 1);
            }

            return s;
        }
        
        /// <summary>
        /// Check if string has only numbers
        /// </summary>
        public static bool IsDigital(this string s)
        {
            int i = 0;
            return int.TryParse(s, out i);
        }

        /// <summary>
        /// Check if string has correct port vaue.
        /// We allow the user to blank out the box or enter any number less or equal max port.
        /// </summary>
        public static bool IsValidPortNumber(this string s)
        {
            bool isValid = false;

            if (s.IsDigital())
            {
                int num = 0;
                if (int.TryParse(s, out num) && num <= ushort.MaxValue && num >= 0)
                {
                    isValid = true;
                }
            }
            else if (s.Length == 0)
            {
                isValid = true;
            }

            return isValid;
        }

        /// <summary>
        /// Gets the number of extensions specified from a filename.
        /// 
        /// eg:
        ///     test.bar.cs
        /// 
        /// count = 1
        ///     return .cs
        /// count = 2
        ///     returns .bar.cs
        /// count = 3
        ///     return null ("test" is not an extension. However, for .test.bar.cs
        ///     it will return .test.bar.cs
        /// </summary>
        public static string GetFileExtensions(this string filename, int count)
        {
            if(count == 0)
            {
                return null;
            }
            var extensions = Path.GetFileName(filename).Split('.');
            if(extensions.Length > count)
            {
                StringBuilder sb = new StringBuilder();
                for(int i = extensions.Length - count; i < extensions.Length; i++)
                {
                    sb.Append('.');
                    sb.Append(extensions[i]);
                }
                return sb.ToString();
            }
            return null;
        }
    }
}

